using CodeProfiler.Core;
using CodeProfiler.Core.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.Json;

namespace CodeProfiler.SqlServer;

public class SqlServerCodeProfiler : ICodeProfiler
{
    private readonly Options _options;
    private readonly ILogger<SqlServerCodeProfiler> _logger;
    private readonly BlockingCollection<ProfiledSection> _log = new();
    private readonly Thread _worker;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public SqlServerCodeProfiler(IOptions<Options> options, ILogger<SqlServerCodeProfiler> logger)
    {
        _options = options.Value;        
        _logger = logger;
        _worker = new Thread(() => LogProcessor(_cancellationTokenSource.Token));
        _worker.Start();
    }

    private void LogProcessor(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = new List<ProfiledSection>();

            for (int i = 0; i < _options.BatchSize && _log.TryTake(out var item); i++) 
            {
                batch.Add(item);
            }

            if (batch.Count > 0)
            {
                BulkInsert(_options, batch, _logger);
            }

            if (_log.Count == 0) Thread.Sleep(TimeSpan.FromSeconds(5));
        }
    }

    public void Log(ProfiledSection profiledSection)
    {
        profiledSection.Stop();
        _log.Add(profiledSection);        
    }

    public void Stop()
    {
        _cancellationTokenSource.Cancel();
        _worker.Join();
    }

    public static void BulkInsert(Options options, IEnumerable<ProfiledSection> sections, ILogger<SqlServerCodeProfiler> logger)
    {
        try
        {            
            using var cn = new SqlConnection(options.ConnectionString);
            cn.Open();

            CreateTableIfNotExists(cn, options);

            string baseCmd =
                $@"INSERT INTO [{options.Schema}].[{options.TableName}] (
                    [OperationName], [UserName], [Parameters], [StartTimeUtc], [Duration]
                ) VALUES ";

            // I chunk the blocks in a fixed size so we're not at the mercy of overly large batches
            foreach (var chunk in sections.Chunk(50))
            {
                var values = chunk.Select(tb =>
                {
                    var result = TrySerialize(tb.Parameters);
                    var insertJson = result.Success ? $"'{result.Json!.Replace("'", "''")}'" : "NULL";

                    return $@"(
                            '{tb.OperationName}', '{tb.UserName}', {insertJson}, '{tb.StartTime}', {tb.Duration}
                        )";
                });
                var sql = baseCmd + string.Join(",\r\n", values);

                using var cmd = new SqlCommand(sql, cn);
                cmd.ExecuteNonQuery();
            }
        }
        catch (Exception exc)
        {
            logger.LogError(exc, "Error in SqlServerProfiler");
        }
    }

    private static (bool Success, string? Json) TrySerialize(object? parameters)
    {
        try
        {
            var result = JsonSerializer.Serialize(parameters);
            return (true, result);
        }
        catch 
        {
            return (false, default);
        }
    }

    private static void CreateTableIfNotExists(SqlConnection cn, Options options)
    {
        if (!SchemaExists(cn, options.Schema)) CreateSchema(cn, options.Schema);

        if (TableExists(cn, options.Schema, options.TableName)) return;
        
        string createTable =
            $@"CREATE TABLE [{options.Schema}].[{options.TableName}] (
                [Id] bigint identity(1,1),
                [OperationName] nvarchar(100) NOT NULL,
                [UserName] nvarchar(50) NOT NULL,
                [Parameters] nvarchar(max) NULL,
                [StartTimeUtc] datetime2 NOT NULL,
                [StartDate] AS CAST([StartTimeUtc] AS date) PERSISTED,
                [Duration] bigint NOT NULL
            )";

        using var cmd = new SqlCommand(createTable, cn);
        cmd.ExecuteNonQuery();

        // help from https://stackoverflow.com/a/12874132/2023653
        cmd.CommandText = "CREATE INDEX [IX_logCodeProfiler_StartDate] ON [log].[CodeProfiler] ([StartDate])";
        cmd.ExecuteNonQuery();
    }

    private static void CreateSchema(SqlConnection connection, string schema)
    {
        using var cmd = new SqlCommand($"CREATE SCHEMA [{schema}]", connection);
        cmd.ExecuteNonQuery();
    }

    private static bool TableExists(SqlConnection connection, string schema, string tableName)
    {
        using var cmd = new SqlCommand("SELECT 1 FROM [sys].[tables] WHERE [name]=@name AND SCHEMA_NAME([schema_id])=@schema", connection);
        cmd.Parameters.AddWithValue("name", tableName);
        cmd.Parameters.AddWithValue("schema", schema);
        var result = cmd.ExecuteScalar() ?? 0;
        return result.Equals(1);
    }

    private static bool SchemaExists(SqlConnection connection, string schema)
    {
        using var cmd = new SqlCommand("SELECT 1 FROM [sys].[schemas] WHERE [name]=@schema", connection);
        cmd.Parameters.AddWithValue("schema", schema);
        var result = cmd.ExecuteScalar() ?? 0;
        return result.Equals(1);
    }
}
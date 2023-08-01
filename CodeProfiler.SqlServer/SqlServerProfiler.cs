using CodeProfiler.Core;
using CodeProfiler.Core.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace CodeProfiler.SqlServer;

public class SqlServerProfiler : IProfiler
{
    private readonly Options _options;
    private readonly ILogger<SqlServerProfiler> _logger;
    private readonly List<ProfiledSection> _sections = new();

    public SqlServerProfiler(IOptions<Options> options, ILogger<SqlServerProfiler> logger)
    {
        _options = options.Value;        
        _logger = logger;
    }

    public void Close() => InsertTimedBlocks();    

    public void Log(ProfiledSection profiledSection)
    {
        profiledSection.Stop();
        _sections.Add(profiledSection);
        if (_sections.Count >= _options.BatchSize) InsertTimedBlocks();
    }

    public static void BulkInsert(Options options, IEnumerable<ProfiledSection> sections, ILogger<SqlServerProfiler> logger)
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
                    var json = JsonSerializer.Serialize(tb.Parameters);
                    var insertJson = !string.IsNullOrEmpty(json) ? $"'{json}'" : "NULL";

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

    private void InsertTimedBlocks()
    {
        Task.Run(() => BulkInsert(_options, _sections, _logger));
        _sections.Clear();
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
                [Duration] bigint NOT NULL
            )";

        using var cmd = new SqlCommand(createTable, cn);
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
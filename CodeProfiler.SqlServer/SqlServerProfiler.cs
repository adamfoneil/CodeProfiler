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
    private readonly List<TimedBlock> _timedBlocks = new();

    public SqlServerProfiler(IOptions<Options> options, ILogger<SqlServerProfiler> logger)
    {
        _options = options.Value;        
        _logger = logger;
    }

    public void Close() => InsertTimedBlocks();    

    public void Log(TimedBlock timedBlock)
    {
        timedBlock.Stop();
        _timedBlocks.Add(timedBlock);
        if (_timedBlocks.Count >= _options.BatchSize) InsertTimedBlocks();
    }

    private void InsertTimedBlocks()
    {
        Task.Run(() =>
        {
            try
            {
                using var cn = new SqlConnection(_options.ConnectionString);

                CreateTableIfNotExists(cn);

                string baseCmd =
                    $@"INSERT INTO [{_options.Schema}].[{_options.TableName}] (
                        [OperationName], [UserName], [Parameters], [StartTime], [Duration]
                    ) VALUES ";

                // I chunk the blocks in a fixed size so we're not at the mercy of overly large batches
                foreach (var chunk in _timedBlocks.Chunk(30))
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
                _logger.LogError(exc, "Error in SqlServerProfiler");
            }
        });
        
        _timedBlocks.Clear();
    }

    private void CreateTableIfNotExists(SqlConnection cn)
    {
        throw new NotImplementedException();
    }
}
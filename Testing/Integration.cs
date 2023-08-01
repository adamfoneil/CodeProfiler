using CodeProfiler.Core;
using CodeProfiler.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SqlServer.LocalDb;

namespace Testing;

[TestClass]
public class Integration
{
    private const string DbName = "Profiler";

    [ClassInitialize]
    public static void Startup(TestContext context)
    {
        using var cn = LocalDb.GetConnection(DbName);
        ExecuteWithoutError(cn, "DROP TABLE [log].[CodeBlocks]");
        ExecuteWithoutError(cn, "DROP SCHEMA [log]");
    }

    private static void ExecuteWithoutError(SqlConnection cn, string command)
    {
        try
        {
            using var cmd = new SqlCommand(command, cn);
            cmd.ExecuteNonQuery();
        }
        catch 
        {
            // ignore
        }
    }

    [TestMethod]
    public void CoreExecution()
    {        
        var logger = LoggerFactory.Create(config =>
        {
            config.AddConsole();            
        }).CreateLogger<SqlServerCodeProfiler>();

        var options = new Options()
        {
            ConnectionString = LocalDb.GetConnectionString(DbName),
            BatchSize = 10,
            Schema = "log",
            TableName = "CodeBlocks"
        };

        var sections = Enumerable.Range(1, 100).Select(i => new ProfiledSection($"Integration.CoreWithParams.{i}", parameters: new
        {
            id = i,
            name = "o'neil"
        })).Concat(Enumerable.Range(1, 100).Select(i => new ProfiledSection($"Integration.CoreWithoutParams.{i}")));

        SqlServerCodeProfiler.BulkInsert(options, sections, logger);
    }
}
namespace CodeProfiler.SqlServer;

public class Options
{
    public string ConnectionString { get; set; } = default!;
    public string Schema { get; set; } = "dbo";
    public string TableName { get; set; } = default!;
    public int BatchSize = 50;
}

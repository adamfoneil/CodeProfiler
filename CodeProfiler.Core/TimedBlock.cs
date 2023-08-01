using System.Diagnostics;

namespace CodeProfiler.Core;

public class TimedBlock
{
    private readonly Stopwatch _stopwatch;
    private readonly DateTime _startTime;

    public TimedBlock(string operationName, string userName, object? parameters = null) : this()
    {
        OperationName = operationName;
        UserName = userName;
        Parameters = parameters;
    }

    public TimedBlock()
    {
        _startTime = DateTime.UtcNow;
        _stopwatch = Stopwatch.StartNew();
    }

    public required string OperationName { get; init; }
    public required string UserName { get; init; }
    public object? Parameters { get; init; }

    public void Stop() => _stopwatch.Stop();

    public DateTime StartTime => _startTime;
    public long Duration => _stopwatch.ElapsedMilliseconds;
}

using System.Diagnostics;

namespace CodeProfiler.Core;

public class ProfiledSection
{
    private readonly Stopwatch _stopwatch;
    private readonly DateTime _startTime;

    private const string DefaultUserName = "system";

    public ProfiledSection(string operationName, string? userName = null, object? parameters = null) : this()
    {
        OperationName = operationName;
        UserName = userName ?? DefaultUserName;
        Parameters = parameters;
    }

    public ProfiledSection()
    {
        _startTime = DateTime.UtcNow;
        _stopwatch = Stopwatch.StartNew();
    }

    public string OperationName { get; init; } = default!;
    public string UserName { get; init; } = DefaultUserName;
    public object? Parameters { get; init; }

    public void Stop() => _stopwatch.Stop();

    public DateTime StartTime => _startTime;
    public long Duration => _stopwatch.ElapsedMilliseconds;
}

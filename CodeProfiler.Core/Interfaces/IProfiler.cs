namespace CodeProfiler.Core.Interfaces;

public interface IProfiler
{
    void Log(TimedBlock timedBlock);
    void Close();
}

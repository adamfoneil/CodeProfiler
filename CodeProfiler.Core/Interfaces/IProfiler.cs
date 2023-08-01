namespace CodeProfiler.Core.Interfaces;

public interface IProfiler
{
    void Log(ProfiledSection profiledSection);
    void Close();
}

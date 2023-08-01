I wanted a very simple code profiler that logs to SQL Server (or any destination, really -- but the initial implemented target is for [SQL Server](https://github.com/adamfoneil/CodeProfiler/blob/master/CodeProfiler.SqlServer/SqlServerCodeProfiler.cs)). It works like this:

```csharp
@inject ICodeProfiler Profiler

var section = new ProfiledSection("method name or some other identifier");

/// do some stuff that takes time

Profiler.Log(section); // record the duration along with 
```
I'm sure this has been done before, but as usual I like to build stuff and learn. This is the first time I've used a [blocking collection](https://github.com/adamfoneil/CodeProfiler/blob/master/CodeProfiler.SqlServer/SqlServerCodeProfiler.cs#L15) before. Yes I got help from ChatGPT when asking how to implement a logging service properly.

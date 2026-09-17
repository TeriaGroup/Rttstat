using Microsoft.Extensions.Logging;

namespace Netpulse.Infrastructure.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _dir;
    private readonly object _gate = new();

    public FileLoggerProvider(string dir)
    {
        _dir = dir;
        Directory.CreateDirectory(dir);
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(_dir, _gate, categoryName);

    public void Dispose() { }

    private sealed class FileLogger : ILogger
    {
        private readonly string _dir;
        private readonly object _gate;
        private readonly string _category;

        public FileLogger(string dir, object gate, string category)
        {
            _dir = dir;
            _gate = gate;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var line = $"{DateTimeOffset.Now:O} {logLevel} {_category} {formatter(state, exception)}{Environment.NewLine}";
            if (exception is not null) line += exception + Environment.NewLine;
            var path = Path.Combine(_dir, $"rttstat-{DateTime.Now:yyyyMMdd}.log");
            lock (_gate)
            {
                File.AppendAllText(path, line);
                Cleanup();
            }
        }

        private void Cleanup()
        {
            var files = Directory.GetFiles(_dir, "rttstat-*.log").OrderByDescending(f => f).Skip(7).ToList();
            foreach (var f in files)
            {
                try { File.Delete(f); } catch { /* ignore */ }
            }
        }
    }
}

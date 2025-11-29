using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace AegisMint.Service.Logging;

public class FileLoggerProvider : ILoggerProvider
{
    private readonly string _path;
    private readonly object _gate = new();

    public FileLoggerProvider(string path)
    {
        _path = path;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(_path, _gate, categoryName);

    public void Dispose()
    {
    }

    private sealed class FileLogger : ILogger
    {
        private readonly string _path;
        private readonly object _gate;
        private readonly string _category;

        public FileLogger(string path, object gate, string category)
        {
            _path = path;
            _gate = gate;
            _category = category;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            var line = $"{DateTimeOffset.UtcNow:O}|{logLevel}|{_category}|{message}";
            if (exception is not null)
            {
                line += $"|{exception}";
            }

            lock (_gate)
            {
                File.AppendAllText(_path, line + Environment.NewLine);
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose()
            {
            }
        }
    }
}

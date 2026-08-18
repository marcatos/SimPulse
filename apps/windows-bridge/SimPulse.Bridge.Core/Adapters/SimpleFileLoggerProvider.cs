using Microsoft.Extensions.Logging;

using SimPulse.Bridge.Core.Ports;

namespace SimPulse.Bridge.Core.Adapters;

public sealed class SimpleFileLoggerProvider : ILoggerProvider
{
    private readonly string _directory;
    private readonly IClock _clock;
    private readonly LogLevel _minLevel;
    private readonly object _sync = new();
    private bool _disposed;
    private bool _disabled;

    public SimpleFileLoggerProvider(string directory, IClock clock, LogLevel minLevel = LogLevel.Trace)
    {
        _directory = directory;
        _clock = clock;
        _minLevel = minLevel;
        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception ex) when (IsFileLogFailure(ex))
        {
            _disabled = true;
        }
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new SimpleFileLogger(categoryName, this);
    }

    public void Dispose()
    {
        _disposed = true;
    }

    internal bool IsEnabled(LogLevel logLevel)
    {
        return !_disposed && !_disabled && logLevel != LogLevel.None && logLevel >= _minLevel;
    }

    internal void Write(LogLevel logLevel, string categoryName, string message, Exception? exception)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        DateTimeOffset timestamp = _clock.UtcNow;
        string fileName = $"bridge-{timestamp.UtcDateTime:yyyyMMdd}.log";
        string path = Path.Combine(_directory, fileName);
        string line = exception is null
            ? $"{timestamp:o} {logLevel} {categoryName} {message}{Environment.NewLine}"
            : $"{timestamp:o} {logLevel} {categoryName} {message} {exception}{Environment.NewLine}";

        lock (_sync)
        {
            if (_disabled)
            {
                return;
            }

            try
            {
                File.AppendAllText(path, line);
            }
            catch (Exception ex) when (IsFileLogFailure(ex))
            {
                _disabled = true;
            }
        }
    }

    private static bool IsFileLogFailure(Exception ex)
    {
        return ex is IOException or UnauthorizedAccessException or NotSupportedException;
    }

    private sealed class SimpleFileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly SimpleFileLoggerProvider _provider;

        public SimpleFileLogger(string categoryName, SimpleFileLoggerProvider provider)
        {
            _categoryName = categoryName;
            _provider = provider;
        }

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return _provider.IsEnabled(logLevel);
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            _provider.Write(logLevel, _categoryName, formatter(state, exception), exception);
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

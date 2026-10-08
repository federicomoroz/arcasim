using Microsoft.Extensions.Logging;

namespace ArcaSim.Tests.Support;

/// <summary>An error ArcaSim logged while a test ran.</summary>
public sealed record LoggedError(string Category, string Message, Exception? Exception);

/// <summary>
/// Keeps what ArcaSim logs at Error or above. A rule that fails on a request nobody foresaw is
/// answered with the service's fault and logged: this is how a test tells that apart from the
/// fault a rule meant to give.
/// </summary>
internal sealed class CapturedLogs : ILoggerProvider
{
    private readonly List<LoggedError> _errors = [];

    public IReadOnlyList<LoggedError> Errors
    {
        get
        {
            lock (_errors) return _errors.ToList();
        }
    }

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, this);

    public void Dispose()
    {
    }

    private sealed class Logger(string category, CapturedLogs logs) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var error = new LoggedError(category, formatter(state, exception), exception);
            lock (logs._errors) logs._errors.Add(error);
        }
    }
}

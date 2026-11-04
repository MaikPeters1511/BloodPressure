using Microsoft.Extensions.Logging;

namespace CleanArchitecture.Tests;

/// <summary>Sammelt Logeinträge, damit Tests auf Log-Level prüfen können.</summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly List<(LogLevel Level, string Category, string Message)> _entries = [];

    public IReadOnlyList<(LogLevel Level, string Category, string Message)> Entries
    {
        get { lock (_entries) { return _entries.ToList(); } }
    }

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void Dispose()
    {
    }

    private sealed class Logger(CapturingLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (owner._entries)
            {
                owner._entries.Add((logLevel, category, formatter(state, exception)));
            }
        }
    }
}

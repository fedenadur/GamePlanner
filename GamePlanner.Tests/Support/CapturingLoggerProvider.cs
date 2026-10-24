using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace GamePlanner.Tests;

/// <summary>
/// Captures every log call, including exception text and active scopes, so tests can assert
/// what the host actually emitted after level filtering.
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    public sealed record Entry(string Category, LogLevel Level, EventId EventId, string Text);

    private readonly ConcurrentQueue<Entry> _entries = new();
    private IExternalScopeProvider? _scopeProvider;

    public IReadOnlyCollection<Entry> Entries => _entries;

    public void Clear() => _entries.Clear();

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, this);

    public void Dispose() { }

    private sealed class CapturingLogger(string category, CapturingLoggerProvider owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
            owner._scopeProvider?.Push(state);

        // Level filtering is applied by the logging configuration, which is what tests must observe.
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var parts = new List<string> { formatter(state, exception) };
            if (exception is not null)
                parts.Add(exception.ToString());

            owner._scopeProvider?.ForEachScope((scope, list) => list.Add(scope?.ToString() ?? string.Empty), parts);

            owner._entries.Enqueue(new Entry(category, logLevel, eventId, string.Join("\n", parts)));
        }
    }
}

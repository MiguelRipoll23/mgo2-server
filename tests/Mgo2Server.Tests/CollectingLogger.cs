using Microsoft.Extensions.Logging;

namespace Mgo2Server.Tests;

/// <summary>
/// Logger that keeps the formatted lines it is given, so a test can assert on
/// what a handler reported rather than only on what it sent.
/// </summary>
/// <param name="sink">Receives every formatted line.</param>
public sealed class CollectingLogger(Action<string> sink) : ILogger
{
    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        sink(formatter(state, exception));
}

/// <summary>Factory for <see cref="CollectingLogger"/>, for typed loggers.</summary>
public sealed class CollectingLoggerProvider(Action<string> sink) : ILoggerProvider
{
    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new CollectingLogger(sink);

    /// <inheritdoc />
    public void Dispose()
    {
    }
}

/// <summary>Extension that builds a typed <see cref="CollectingLogger"/>.</summary>
public static class CollectingLoggerExtensions
{
    /// <summary>Builds a logger of the given category that appends to this list.</summary>
    /// <param name="lines">List every formatted line is appended to.</param>
    /// <typeparam name="TCategory">Logger category to build.</typeparam>
    /// <returns>The logger.</returns>
    public static ILogger<TCategory> Collecting<TCategory>(this List<string> lines) =>
        new TypedCollectingLogger<TCategory>(lines.Add);
}

/// <summary>Typed <see cref="CollectingLogger"/>.</summary>
/// <typeparam name="TCategory">Logger category.</typeparam>
/// <param name="sink">Receives every formatted line.</param>
public sealed class TypedCollectingLogger<TCategory>(Action<string> sink) : ILogger<TCategory>
{
    private readonly CollectingLogger inner = new(sink);

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => inner.BeginScope(state);

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        inner.Log(logLevel, eventId, state, exception, formatter);
}

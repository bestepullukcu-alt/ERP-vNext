using System.Collections;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Diten.Web.Services.Http;

/// <summary>
/// BL-517 — the query string never reaches the log.
///
/// <para><b>The measured leak.</b> ASP.NET writes every incoming request as <c>Request starting … GET
/// http://host/path?search=Ayşe</c> (category <c>Microsoft.AspNetCore.Hosting.Diagnostics</c>), and the HTTP client
/// factory writes every outgoing one as <c>Start processing HTTP request GET http://gateway/…/lookup?search=Ayşe</c>
/// (<c>System.Net.Http.HttpClient.*</c>). On the person-lookup endpoints the query is a fragment of somebody's name:
/// personal data, written to the log at Information on every keystroke.</para>
///
/// <para><b>The decision (CT, 2026-10-03).</b> Log levels are NOT lowered — the lines stay, so a request can still be
/// followed. Only the part after <c>?</c> is taken out of those two families of lines, in ONE place: this factory wraps
/// the application's <see cref="ILoggerFactory"/>, so every provider (console, debug, a test's capture) receives the
/// line already without its query, and no call site has to remember anything.</para>
///
/// <para>Only those two categories are touched. Everything else is passed through unchanged — a redaction that also
/// rewrote a business log line that happens to contain a question mark would be a new defect.</para>
/// </summary>
public sealed partial class QueryStringRedactingLoggerFactory : ILoggerFactory
{
    private static readonly string[] RedactedCategoryPrefixes =
    [
        "Microsoft.AspNetCore.Hosting.Diagnostics",
        "System.Net.Http.HttpClient."
    ];

    private readonly ILoggerFactory _inner;

    public QueryStringRedactingLoggerFactory(ILoggerFactory inner) => _inner = inner;

    public ILogger CreateLogger(string categoryName)
    {
        var logger = _inner.CreateLogger(categoryName);
        return RedactedCategoryPrefixes.Any(prefix => categoryName.StartsWith(prefix, StringComparison.Ordinal))
            ? new RedactingLogger(logger)
            : logger;
    }

    public void AddProvider(ILoggerProvider provider) => _inner.AddProvider(provider);

    public void Dispose() => _inner.Dispose();

    /// <summary>
    /// Wraps the registered logger factory. Called once in Program; providers added later (by a test, by a host
    /// extension) are still behind the redaction, because they are added to the factory this one wraps.
    /// </summary>
    public static IServiceCollection AddQueryStringRedaction(IServiceCollection services)
    {
        var registered = services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(ILoggerFactory))
            ?? throw new InvalidOperationException("BL-517: logging must be registered before the query-string redaction.");
        if (registered.ImplementationType is not { } implementation)
        {
            throw new InvalidOperationException("BL-517: the logger factory is expected to be registered by type.");
        }

        services.RemoveAll<ILoggerFactory>();
        services.AddSingleton(implementation);
        services.AddSingleton<ILoggerFactory>(provider =>
            new QueryStringRedactingLoggerFactory((ILoggerFactory)provider.GetRequiredService(implementation)));
        return services;
    }

    /// <summary>A URL followed by its query: the URL stays, the query goes.</summary>
    internal static string Redact(string value)
        => string.IsNullOrEmpty(value) || value.IndexOf('?') < 0 ? value : UrlWithQuery().Replace(value, "${url}");

    private static object? RedactValue(string key, object? value) => value switch
    {
        _ when string.Equals(key, "QueryString", StringComparison.Ordinal) => string.Empty,
        Uri uri when uri.IsAbsoluteUri => uri.GetLeftPart(UriPartial.Path),
        Uri uri => Redact(uri.OriginalString),
        string text when !string.Equals(key, "{OriginalFormat}", StringComparison.Ordinal) => Redact(text),
        _ => value
    };

    [GeneratedRegex(@"(?<url>(?:[A-Za-z][A-Za-z0-9+.\-]*://[^\s?#]*|/[^\s?#]*))\?[^\s#]*",
        RegexOptions.CultureInvariant)]
    private static partial Regex UrlWithQuery();

    private sealed class RedactingLogger(ILogger inner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => inner.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!inner.IsEnabled(logLevel))
            {
                return;
            }

            var message = Redact(formatter(state, exception));
            var values = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? pairs.Select(pair => new KeyValuePair<string, object?>(pair.Key, RedactValue(pair.Key, pair.Value))).ToList()
                : [];
            inner.Log(logLevel, eventId, new RedactedState(message, values), exception, static (s, _) => s.Message);
        }
    }

    /// <summary>The line as it will be written: its message and its structured values, both without the query.</summary>
    private sealed class RedactedState(string message, IReadOnlyList<KeyValuePair<string, object?>> values)
        : IReadOnlyList<KeyValuePair<string, object?>>
    {
        public string Message { get; } = message;
        public int Count => values.Count;
        public KeyValuePair<string, object?> this[int index] => values[index];
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => values.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public override string ToString() => Message;
    }
}

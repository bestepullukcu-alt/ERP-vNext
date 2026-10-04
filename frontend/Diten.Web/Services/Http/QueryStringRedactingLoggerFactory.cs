using System.Collections;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Diten.Web.Services.Http;

/// <summary>
/// BL-517 — a URL's query string does not reach the Web log: not in a line's message, not in its structured values,
/// not in a scope that is written beside it. The path stays.
///
/// <para><b>The measured leak.</b> ASP.NET writes every incoming request as <c>Request starting … GET
/// http://host/path?search=Ayşe</c> (<c>Microsoft.AspNetCore.Hosting.Diagnostics</c>); the HTTP client factory writes
/// every outgoing one as <c>Start processing HTTP request GET http://gateway/…/lookup?search=Ayşe</c> and opens the
/// scope <c>HTTP GET http://gateway/…?search=Ayşe</c> around it (net8.0 <c>LogicalHandler</c>; any provider that writes
/// scopes — console <c>IncludeScopes</c>, the JSON formatter, OpenTelemetry — puts that on every line inside it); MVC's
/// <c>RedirectResultExecutor</c> writes "redirecting to {Destination}" with the target's query at Information; and the
/// application's own lines carry <c>{Path}</c> / <c>{Url}</c> values built with <c>Request.QueryString</c> (about forty
/// call sites, e.g. the CRM proxies). On the person-lookup endpoints the query is a fragment of somebody's name.</para>
///
/// <para><b>The decision (CT, 2026-10-03; widened 2026-10-04, CT-SHELL-FIX1 item 5).</b> Log levels are NOT lowered and no
/// call site is edited. This factory wraps the application's <see cref="ILoggerFactory"/>, so EVERY category, through
/// every provider, receives the line already without its queries: the message, each structured value (a string, a
/// <see cref="Uri"/> — absolute or relative — the <c>QueryString</c> field, the <c>{OriginalFormat}</c> template, any
/// other value whose text carries one) and the scope state. Only the part from <c>?</c> on is removed from a URL
/// (several <c>&amp;</c>-joined parameters included); a question mark that does not follow a URL is left alone.</para>
///
/// <para><b>The cost on a line without a query</b> is a scan of its values for <c>?</c> — no formatting, no allocation;
/// the line is passed through untouched. Measured by <c>QueryStringRedactionTests</c> (budget 5 µs per line).</para>
///
/// <para><b>What this does not reach</b>: the text of an exception passed with the line (rendered by the provider from
/// the exception object itself), and a bare parameter value logged on its own, without the URL it came from.</para>
/// </summary>
public sealed partial class QueryStringRedactingLoggerFactory : ILoggerFactory
{
    private const string QueryStringKey = "QueryString";

    private readonly ILoggerFactory _inner;

    public QueryStringRedactingLoggerFactory(ILoggerFactory inner) => _inner = inner;

    public ILogger CreateLogger(string categoryName) => new RedactingLogger(_inner.CreateLogger(categoryName));

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

    // An absolute URL (scheme://…) or a rooted path (/…), then "?" and everything up to whitespace, a fragment or a quote.
    [GeneratedRegex(@"(?<url>(?:[A-Za-z][A-Za-z0-9+.\-]*://[^\s?#""'<>]*|/[^\s?#""'<>]*))\?[^\s#""'<>]*",
        RegexOptions.CultureInvariant)]
    private static partial Regex UrlWithQuery();

    // ── the cheap question, asked of every line ──────────────────────────────────────────────────────────────────

    /// <summary>Could this value's text carry a query? Strings and URIs are scanned; numbers, dates and ids cannot.</summary>
    private static bool MayCarryQuery(string key, object? value) => value switch
    {
        null => false,
        string text => text.Contains('?'),
        Uri uri => uri.OriginalString.Contains('?'),
        _ when string.Equals(key, QueryStringKey, StringComparison.Ordinal) => true,
        IConvertible or Guid or DateTimeOffset or TimeSpan => false,
        // A sequence is written item by item (the logging formatter joins them), so each item is asked.
        IEnumerable items => AnyItemMayCarryQuery(items),
        _ => value.ToString()?.Contains('?') == true
    };

    private static bool AnyItemMayCarryQuery(IEnumerable items)
    {
        foreach (var item in items)
        {
            if (MayCarryQuery(string.Empty, item))
            {
                return true;
            }
        }

        return false;
    }

    private static bool AnyValueMayCarryQuery<TState>(TState state, out bool structured)
    {
        structured = true;
        switch (state)
        {
            case IReadOnlyList<KeyValuePair<string, object?>> list:
                for (var i = 0; i < list.Count; i++)
                {
                    var pair = list[i];
                    if (MayCarryQuery(pair.Key, pair.Value))
                    {
                        return true;
                    }
                }

                return false;
            case IEnumerable<KeyValuePair<string, object?>> pairs:
                foreach (var pair in pairs)
                {
                    if (MayCarryQuery(pair.Key, pair.Value))
                    {
                        return true;
                    }
                }

                return false;
            default:
                structured = false;
                return false;
        }
    }

    // ── the rewrite, only for a line that may carry one ──────────────────────────────────────────────────────────

    private static object? RedactValue(string key, object? value) => value switch
    {
        null => null,
        _ when string.Equals(key, QueryStringKey, StringComparison.Ordinal) => string.Empty,
        string text => Redact(text),
        Uri uri => WithoutQuery(uri),
        IConvertible or Guid or DateTimeOffset or TimeSpan => value,
        IEnumerable items => AnyItemMayCarryQuery(items)
            ? items.Cast<object?>().Select(item => RedactValue(string.Empty, item)).ToList()
            : value,
        _ => value.ToString() is { } text && text.Contains('?') ? Redact(text) : value
    };

    /// <summary>A URI keeps everything before its "?" — absolute or relative alike (a relative one need not start with "/").</summary>
    private static string WithoutQuery(Uri uri)
    {
        var text = uri.OriginalString;
        var query = text.IndexOf('?');
        return query < 0 ? text : text[..query];
    }

    private static IReadOnlyList<KeyValuePair<string, object?>> RedactValues<TState>(TState state)
    {
        if (state is not IEnumerable<KeyValuePair<string, object?>> pairs)
        {
            return [];
        }

        var values = new List<KeyValuePair<string, object?>>();
        foreach (var pair in pairs)
        {
            values.Add(new KeyValuePair<string, object?>(pair.Key, RedactValue(pair.Key, pair.Value)));
        }

        return values;
    }

    private sealed class RedactingLogger(ILogger inner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            if (state is string text)
            {
                return text.Contains('?') ? inner.BeginScope(Redact(text)) : inner.BeginScope(state);
            }

            var mayCarry = AnyValueMayCarryQuery(state, out var structured);
            if (!structured)
            {
                // A scope with no structured values is written by its text alone.
                var rendered = state.ToString() ?? string.Empty;
                return rendered.Contains('?') ? inner.BeginScope(Redact(rendered)) : inner.BeginScope(state);
            }

            return mayCarry
                ? inner.BeginScope(new RedactedState(Redact(state.ToString() ?? string.Empty), RedactValues(state)))
                : inner.BeginScope(state);
        }

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

            // A structured line renders its message from its template and its values, so scanning those is enough
            // to know — without formatting it — that it carries no query: then it goes through untouched.
            var mayCarry = AnyValueMayCarryQuery(state, out var structured);
            string? message = null;
            if (!structured)
            {
                message = formatter(state, exception);
                mayCarry = message.Contains('?');
            }

            if (!mayCarry)
            {
                inner.Log(logLevel, eventId, state, exception, formatter);
                return;
            }

            message = Redact(message ?? formatter(state, exception));
            inner.Log(logLevel, eventId, new RedactedState(message, RedactValues(state)), exception, static (s, _) => s.Message);
        }
    }

    /// <summary>The line (or scope) as it will be written: its message and its structured values, without the query.</summary>
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

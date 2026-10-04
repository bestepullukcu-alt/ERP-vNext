using System.Collections;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Diten.Platform.Application.Features.Notifications;

/// <summary>
/// BL-454 — which notification variables are never stored and never shown. ONE place; every rule here is either about
/// a variable's NAME or about a value that has the SHAPE of a credential. Nothing guesses from ordinary text: the old
/// guess (a space or an '=' in the value) called every task title with a space a secret, and every retry of such a mail
/// lost its table and its button.
///
/// <para><b>A name is a secret in two ways.</b> A long word that means one thing (<c>password</c>, <c>token</c>,
/// <c>secret</c> …) counts wherever it appears, glued or not: <c>RESETTOKEN</c>, <c>temppassword</c>,
/// <c>SetpasswordUrl</c>. A short word that means many things (<c>code</c>, <c>key</c>, <c>pass</c>, <c>pin</c> …)
/// counts only as a WORD of the name, and not when a known neutral word stands next to it: <c>ActivationCode</c>,
/// <c>LicenseKey</c>, <c>SessionId</c> are secrets; <c>TenantCode</c>, <c>PostalCode</c>, <c>WeekKey</c>,
/// <c>KeyResult</c> are not, and "Shipping" never contains the word <c>pin</c>.</para>
/// </summary>
public static partial class NotificationSecrets
{
    /// <summary>Long words that mean one thing: a name that CONTAINS one of them, glued or not, carries a secret.</summary>
    public static IReadOnlyList<string> SecretNameParts { get; } =
    [
        "password", "passwd", "passcode", "passphrase", "secret", "token", "credential", "apikey", "connectionstring"
    ];

    /// <summary>Short words that are secrets on their own as a word of the name: no neutral neighbour can excuse them.</summary>
    public static IReadOnlyList<string> SecretNameWords { get; } = ["pwd", "jwt", "sig", "signature"];

    /// <summary>
    /// Short words that are secrets as a word of the name UNLESS a neutral word stands right before or right after
    /// them (<see cref="NeutralNameWords"/>): <c>InviteCode</c> is a secret, <c>CountryCode</c> is not.
    /// </summary>
    public static IReadOnlyList<string> AmbiguousSecretNameWords { get; } =
    [
        "code", "codes", "key", "keys", "pass", "pin", "pins", "otp", "nonce", "session"
    ];

    /// <summary>
    /// Words that turn an ambiguous word (<see cref="AmbiguousSecretNameWords"/>) next to them into ordinary data.
    /// The one list: add a word here, nowhere else.
    /// </summary>
    public static IReadOnlyList<string> NeutralNameWords { get; } =
    [
        "tenant", "module", "postal", "post", "zip", "country", "currency", "product", "week", "result", "type", "status",
        "language", "lang", "locale", "color", "colour", "region", "area", "city", "state", "item", "project", "task",
        "department", "cost", "center", "centre", "category", "event", "template", "error", "reason", "unit", "sort",
        "primary", "foreign", "lookup", "cache", "partition", "resource", "translation", "message", "menu", "page",
        "field", "group", "role", "idempotency", "bar", "qr", "material", "lot", "batch", "document", "doc"
    ];

    /// <summary>
    /// A link's QUERY parameter is a secret when its name is a secret NAME (<see cref="IsSecretName"/>: <c>token=</c>,
    /// <c>code=</c>, <c>key=</c>, <c>pass=</c>, <c>session=</c>, <c>nonce=</c> …) or one of these words, which carry
    /// credentials only in a query: <c>p=</c> (a meeting join passcode), <c>rlkey=</c>, <c>ticket=</c>, <c>auth=</c> …
    /// </summary>
    public static IReadOnlyList<string> SecretQueryWords { get; } =
    [
        "auth", "p", "rlkey", "ticket", "hash", "invite", "accesstoken"
    ];

    /// <summary>How deep a link inside a link's query value is followed (<c>?next=%2Freset%3Ftoken%3D…</c>).</summary>
    public const int MaxLinkDepth = 3;

    public static bool IsSecretName(string? name)
    {
        var compact = Compact(name);
        if (compact.Length == 0)
        {
            return false;
        }

        if (SecretNameParts.Any(part => compact.Contains(part, StringComparison.Ordinal)))
        {
            return true;
        }

        var words = Words(name);
        for (var i = 0; i < words.Count; i++)
        {
            if (SecretNameWords.Contains(words[i], StringComparer.Ordinal))
            {
                return true;
            }

            if (AmbiguousSecretNameWords.Contains(words[i], StringComparer.Ordinal)
                && !IsNeutral(words, i - 1)
                && !IsNeutral(words, i + 1))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The name says secret, or the value is one (a credential-bearing link, a secret-shaped token, a structure).</summary>
    public static bool IsSensitive(string? name, object? value) => IsSecretName(name) || IsSecretValue(value);

    public static bool IsSecretValue(object? value)
    {
        switch (value)
        {
            case null:
                return false;
            case JsonElement element:
                // A structure is never rendered as text by a template; stored, it would be another tenant's raw data.
                if (element.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                {
                    return true;
                }

                return element.ValueKind == JsonValueKind.String && IsSecretText(element.GetString());
            case string text:
                return IsSecretText(text);
            case IDictionary:
            case IEnumerable when value is not string:
                return true;
            default:
                return IsSecretText(Convert.ToString(value, CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// A text is a secret when any whitespace-separated piece of it is a credential-bearing link (absolute, relative,
    /// host-only, a query inside a fragment, parameters split by <c>&amp;</c> or <c>;</c>, names percent-encoded or not,
    /// a link encoded inside another link's query value) or carries the shape of a secret: a <c>sk-</c> /
    /// <c>sk_live_</c> / <c>SG.</c> / <c>ghp_</c> / <c>xoxb-</c> / <c>AKIA</c> key, or a three-part JWT — alone or with
    /// something stuck to it (<c>token:eyJ…</c>, <c>eyJ…;</c>, inside JSON). A sentence with a space or an '=' in it is
    /// NOT a secret.
    /// </summary>
    public static bool IsSecretText(string? text) => IsSecretText(text, depth: 0);

    public static bool IsCredentialBearingLink(string? piece) => IsCredentialBearingLink(piece, depth: 0);

    private static bool IsSecretText(string? text, int depth)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (SecretShape().IsMatch(text))
        {
            return true;
        }

        foreach (var raw in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var piece = raw.Trim('"', '\'', '(', ')', '<', '>', '[', ']', ',', '.');
            if (IsCredentialBearingLink(piece, depth))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCredentialBearingLink(string? piece, int depth)
    {
        if (string.IsNullOrEmpty(piece))
        {
            return false;
        }

        var start = piece.IndexOfAny(['?', '#']);
        if (start < 0)
        {
            return false;
        }

        // A link, not a sentence: something before the query that looks like a path, a scheme or a host.
        var head = piece[..start];
        if (head.Length > 0 && !head.Contains('/') && !head.Contains(':') && !head.Contains('.'))
        {
            return false;
        }

        foreach (var parameter in piece[(start + 1)..].Split(['&', ';', '?', '#'], StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = parameter.IndexOf('=');
            var name = Unescape(equals < 0 ? parameter : parameter[..equals]);
            if (IsSecretName(name) || Words(name).Any(word => SecretQueryWords.Contains(word, StringComparer.Ordinal)))
            {
                return true;
            }

            // A link carried inside a value (?next=%2Freset%3Ftoken%3D…, redirect_uri=…, a mailto body) is read too.
            if (equals >= 0 && depth < MaxLinkDepth && IsSecretText(Unescape(parameter[(equals + 1)..]), depth + 1))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNeutral(IReadOnlyList<string> words, int index) =>
        index >= 0 && index < words.Count && NeutralNameWords.Contains(words[index], StringComparer.Ordinal);

    private static string Unescape(string value)
    {
        try
        {
            return Uri.UnescapeDataString(value.Replace('+', ' '));
        }
        catch (UriFormatException)
        {
            return value;
        }
    }

    private static string Compact(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? string.Empty
            : new string(name.Where(char.IsAsciiLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    /// <summary>Lower-case words of a name: separators, PascalCase and letter/digit boundaries split it.</summary>
    public static IReadOnlyList<string> Words(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return [];
        }

        return WordBoundary().Replace(name, " ")
            .Split([' ', '_', '-', '.', '/', '\\', ':'], StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.ToLowerInvariant())
            .ToList();
    }

    // A secret's own shape, found anywhere in a text but never in the middle of a word: provider keys by their published
    // prefixes, and a three-part JWT.
    [GeneratedRegex(
        @"(?<![A-Za-z0-9_])(?:sk-[A-Za-z0-9_-]{9,}|sk_(?:live|test)_[A-Za-z0-9]{8,}|SG\.[A-Za-z0-9_.-]{9,}|gh[pousr]_[A-Za-z0-9]{20,}|xox[abprs]-[A-Za-z0-9-]{8,}|AKIA[0-9A-Z]{16}|eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]*)",
        RegexOptions.CultureInvariant)]
    private static partial Regex SecretShape();

    [GeneratedRegex(@"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])|(?<=[A-Za-z])(?=[0-9])|(?<=[0-9])(?=[A-Za-z])|[^A-Za-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex WordBoundary();
}

/// <summary>
/// BL-454 — a notification's variables as the template will SEE them, fixed once when the mail is queued. Every value
/// becomes the string the renderer would have drawn from it (invariant culture), and lookup ignores case. The first
/// send renders from this, and so does a retry read back from the dispatch row: a <c>DateTimeOffset</c>, a bool or an
/// enum can no longer render one way the first time and another way from stored JSON.
/// </summary>
public static class NotificationVariables
{
    public static Dictionary<string, object?> Normalize(IReadOnlyDictionary<string, object?>? variables)
    {
        var normalized = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in variables ?? new Dictionary<string, object?>())
        {
            normalized[pair.Key] = ToText(pair.Value);
        }

        return normalized;
    }

    /// <summary>Read back what <see cref="Normalize"/> stored (and any older row, whose values may be JSON numbers or bools).</summary>
    public static Dictionary<string, object?> FromJson(string? json)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
        {
            return values;
        }

        foreach (var pair in JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? [])
        {
            values[pair.Key] = ToText(pair.Value);
        }

        return values;
    }

    public static string? ToText(object? value) => value switch
    {
        null => null,
        string text => text,
        JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
        JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => null,
        // A bool is drawn the way the first render drew a CLR bool ("True"), not as JSON spells it ("true").
        JsonElement { ValueKind: JsonValueKind.True } => bool.TrueString,
        JsonElement { ValueKind: JsonValueKind.False } => bool.FalseString,
        JsonElement element => element.GetRawText(),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)
    };
}

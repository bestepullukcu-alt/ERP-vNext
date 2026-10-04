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
/// <para><b>Names are matched by WORD, not by substring.</b> A name is split into its words (PascalCase, digits,
/// separators): <c>TemporaryPassword</c> → temporary, password. A substring match would read <c>pin</c> into
/// "Shipping" and <c>code</c> into "TenantCode" / "ModuleCode"; whole words do not.</para>
/// </summary>
public static partial class NotificationSecrets
{
    /// <summary>A variable whose name contains one of these WORDS carries a secret.</summary>
    public static IReadOnlyList<string> SecretNameWords { get; } =
    [
        "secret", "secrets", "token", "tokens", "password", "passwords", "passwd", "pwd", "passcode", "otp", "pin",
        "credential", "credentials", "jwt", "signature", "sig", "apikey"
    ];

    /// <summary>Two-word names that are secrets although neither word alone is (<c>AccessKey</c>, <c>ResetCode</c>).</summary>
    public static IReadOnlyList<(string First, string Second)> SecretNamePairs { get; } =
    [
        ("access", "key"), ("api", "key"), ("secret", "key"), ("private", "key"), ("signing", "key"), ("session", "key"),
        ("client", "key"), ("auth", "key"),
        ("reset", "code"), ("verification", "code"), ("verify", "code"), ("auth", "code"), ("access", "code"),
        ("security", "code"), ("confirmation", "code"), ("otp", "code"), ("login", "code")
    ];

    /// <summary>
    /// In a link's QUERY a parameter is short and means one thing: <c>code=</c> (an OAuth or reset code), <c>key=</c>,
    /// <c>auth=</c> carry credentials there although "TenantCode" as a variable name does not.
    /// </summary>
    public static IReadOnlyList<string> SecretQueryWords { get; } = [.. SecretNameWords, "code", "key", "keys", "auth"];

    public static bool IsSecretName(string? name)
    {
        var words = Words(name);
        if (words.Any(word => SecretNameWords.Contains(word, StringComparer.Ordinal)))
        {
            return true;
        }

        for (var i = 0; i + 1 < words.Count; i++)
        {
            if (SecretNamePairs.Contains((words[i], words[i + 1])))
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
    /// a query inside a fragment, parameters split by <c>&amp;</c> or <c>;</c>, names percent-encoded or not) or has
    /// the shape of a secret: a <c>sk-</c> / <c>SG.</c> key, or a three-part JWT. A sentence with a space or an '='
    /// in it is NOT a secret.
    /// </summary>
    public static bool IsSecretText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        foreach (var raw in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var piece = raw.Trim('"', '\'', '(', ')', '<', '>', '[', ']', ',', '.');
            if (IsSecretShaped(piece) || IsCredentialBearingLink(piece))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsCredentialBearingLink(string? piece)
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

        // A link, not a sentence: something before the query that looks like a path or a scheme.
        var head = piece[..start];
        if (head.Length > 0 && !head.Contains('/') && !head.Contains(':'))
        {
            return false;
        }

        foreach (var parameter in piece[(start + 1)..].Split(['&', ';', '?', '#'], StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = parameter.IndexOf('=');
            var name = Unescape(equals < 0 ? parameter : parameter[..equals]);
            if (Words(name).Any(word => SecretQueryWords.Contains(word, StringComparer.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSecretShaped(string piece) =>
        (piece.StartsWith("sk-", StringComparison.Ordinal) && piece.Length >= 12)
        || (piece.StartsWith("SG.", StringComparison.Ordinal) && piece.Length >= 12)
        || Jwt().IsMatch(piece);

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

    [GeneratedRegex(@"^eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex Jwt();

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

    /// <summary>Read back what <see cref="Normalize"/> stored (and any older row, whose values may be JSON numbers).</summary>
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
        JsonElement element => element.GetRawText(),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)
    };
}

using System.Text.RegularExpressions;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Features.Notifications;

public static partial class NotificationParsing
{
    public static string NormalizeTemplateKey(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    public static string NormalizeLocale(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "en" : value.Trim().ToLowerInvariant();

    public static string NormalizeEmail(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    public static bool TryParseProvider(string? value, out MessagingProviderCode provider) =>
        Enum.TryParse(value, ignoreCase: true, out provider) && Enum.IsDefined(provider);

    public static bool TryParseChannel(string? value, out NotificationChannelCode channel) =>
        Enum.TryParse(value, ignoreCase: true, out channel) && Enum.IsDefined(channel);

    public static bool TryParseTemplateStatus(string? value, out NotificationTemplateStatus status) =>
        Enum.TryParse(value, ignoreCase: true, out status) && Enum.IsDefined(status);

    public static bool TryParseVariableType(string? value, out TemplateVariableType type) =>
        Enum.TryParse(value, ignoreCase: true, out type) && Enum.IsDefined(type);

    public static bool TryParseFallbackPolicy(string? value, out NotificationFallbackPolicy policy) =>
        Enum.TryParse(value, ignoreCase: true, out policy) && Enum.IsDefined(policy);

    public static bool IsValidTemplateKey(string? value) =>
        !string.IsNullOrWhiteSpace(value) && TemplateKeyRegex().IsMatch(value.Trim());

    public static bool IsValidVariableName(string? value) =>
        !string.IsNullOrWhiteSpace(value) && VariableNameRegex().IsMatch(value.Trim());

    /// <summary>
    /// BL-454 — the ONE list of variable-name fragments whose VALUE is a secret. A variable whose name contains one of
    /// these (case-insensitive) is never stored on a dispatch and never shown in a preview; everything else is stored
    /// as given so a retry can send exactly the e-mail the first attempt sent. The value is no longer guessed at: the
    /// old guess (a space or an '=' in the value) called every task title with a space a secret, and every retry of
    /// such a mail lost its table and its button.
    /// </summary>
    public static IReadOnlyList<string> SensitiveVariableNameParts { get; } = ["secret", "token", "password", "apikey", "api_key"];

    public static bool IsSensitiveVariableName(string? name) =>
        !string.IsNullOrWhiteSpace(name)
        && SensitiveVariableNameParts.Any(part => name.Contains(part, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// BL-454 — the query-parameter names that carry a credential in a link: everything a variable name is checked for,
    /// plus the short forms links use (<c>key</c>, <c>code</c>, <c>sig</c>). Defined here and nowhere else.
    /// </summary>
    public static IReadOnlyList<string> SensitiveQueryParameterParts { get; } =
        [.. SensitiveVariableNameParts, "key", "code", "sig"];

    /// <summary>
    /// BL-454 — the ONE value check, and a narrow one: an absolute http(s) link whose query carries a parameter named
    /// like a credential (<c>…/set-password?token=…</c>). Such a link is a secret whatever the variable is called. Nothing
    /// else about a value is guessed at — a task title with a space, an <c>=</c> in a sentence, a link to a task or a
    /// page number stay as they are.
    /// </summary>
    public static bool IsCredentialBearingLink(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrEmpty(uri.Query))
        {
            return false;
        }

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var name = Uri.UnescapeDataString(separator < 0 ? pair : pair[..separator]);
            if (SensitiveQueryParameterParts.Any(part => name.Contains(part, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A variable is never stored or shown when its NAME says secret, or its value is a credential-bearing link.</summary>
    public static bool IsSensitiveVariable(string? name, object? value) =>
        IsSensitiveVariableName(name) || IsCredentialBearingLink(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>
    /// BL-454 — what the dispatch monitoring answer shows of the stored variables: their NAMES, every value replaced
    /// with "•••". The values are another tenant's data; an operator diagnosing a send needs the names and the
    /// preview, not the content.
    /// </summary>
    public static string MaskVariableValues(string? variablesJson)
    {
        if (string.IsNullOrWhiteSpace(variablesJson))
        {
            return "{}";
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(variablesJson);
            if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                return "{}";
            }

            var names = document.RootElement.EnumerateObject().Select(p => p.Name).ToList();
            return System.Text.Json.JsonSerializer.Serialize(names.ToDictionary(name => name, _ => "•••"));
        }
        catch (System.Text.Json.JsonException)
        {
            return "{}";
        }
    }

    public static bool LooksLikeRawSecret(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        return candidate.Contains(' ')
               || candidate.Contains('=')
               || candidate.StartsWith("sk-", StringComparison.OrdinalIgnoreCase)
               || candidate.StartsWith("SG.", StringComparison.OrdinalIgnoreCase)
               || candidate.Contains("password", StringComparison.OrdinalIgnoreCase)
               || candidate.Contains("apikey", StringComparison.OrdinalIgnoreCase)
               || candidate.Contains("api_key", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex("^[a-z0-9]+(\\.[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex TemplateKeyRegex();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_.]*$", RegexOptions.CultureInvariant)]
    private static partial Regex VariableNameRegex();
}

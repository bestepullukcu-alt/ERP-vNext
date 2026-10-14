namespace Diten.AuthService.Domain.Entities;

/// <summary>
/// WP-EMAIL-LANG-01 — the languages a tenant user may choose for what is sent to them: the seven languages of tenant
/// modules. A user's choice outranks the tenant's language; no choice falls back to it, then to <see cref="Default"/>.
/// </summary>
public static class UserLanguages
{
    public const string Default = "en";

    public static readonly IReadOnlyList<string> Supported = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    /// <summary>The stored form of a choice: lower case, trimmed; blank is "no choice" (null). Null when not supported.</summary>
    public static bool TryNormalize(string? language, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(language))
        {
            return true;
        }

        var value = language.Trim().ToLowerInvariant();
        if (!Supported.Contains(value))
        {
            return false;
        }

        normalized = value;
        return true;
    }

    /// <summary>The language to write to a recipient: their own choice, else the tenant's, else <see cref="Default"/>.
    /// A value outside the supported set at either link is skipped, never sent as is.</summary>
    public static string ForRecipient(string? preferred, string? tenantLanguage)
    {
        foreach (var candidate in new[] { preferred, Primary(tenantLanguage) })
        {
            if (TryNormalize(candidate, out var value) && value is not null)
            {
                return value;
            }
        }

        return Default;
    }

    // "tr-TR" → "tr": a tenant may store a regional form.
    private static string? Primary(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        var value = language.Trim();
        var separator = value.IndexOfAny(['-', '_']);
        return separator > 0 ? value[..separator] : value;
    }
}

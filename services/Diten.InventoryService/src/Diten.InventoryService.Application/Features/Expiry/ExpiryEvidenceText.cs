using System.Collections.Concurrent;
using System.Globalization;
using System.Resources;

namespace Diten.InventoryService.Application.Features.Expiry;

/// <summary>
/// WP-MVP1-FIX-LIVE2 (CT decision "çeviri kaynağı B") — the words of the FEFO evidence pack (PDF + CSV) in the 7 languages from ONE source:
/// the service family <c>ExpiryEvidenceText_{lang}.resx</c> next to this file (embedded in the main assembly — no satellite assembly, no
/// culture lookup). Keys named like the Web's (<c>Expiry.Type.*</c>, <c>Expiry.Status.*</c>, <c>Expiry.Resolution.*</c>,
/// <c>Expiry.Policy.Field.*</c>, <c>Expiry.Policy.Mode.*</c>, the four section titles) are SHARED — same key, same value as the Web's
/// <c>Expiry.{lang}.resx</c> (a parity guard in the Web tests); <c>ExpiryPdf.*</c> are the files' own. A missing key answers the key itself.
/// 0176's own reader (the recall pack has its own — no Expiry ↔ Recall link).
/// </summary>
public static class ExpiryEvidenceText
{
    public static readonly IReadOnlyList<string> Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    /// <summary>The languages the PDF draws (the embedded Segoe WP glyphs, measured); zh / ar → an English PDF with a note (FU-0176-PDF-L10N).</summary>
    public static readonly IReadOnlyList<string> PdfLanguages = ["en", "tr", "fr", "es", "ru"];

    private static readonly ConcurrentDictionary<string, ResourceManager> Managers = new(StringComparer.Ordinal);

    /// <summary>The primary subtag of a language tag, if it is one of the 7; anything else (or none) → en (the recall pack's rule).</summary>
    public static string Normal(string? language)
    {
        var primary = (language ?? string.Empty).Split('-', '_')[0].Trim().ToLowerInvariant();
        return Languages.Contains(primary) ? primary : "en";
    }

    public static string PdfLanguage(string language) => PdfLanguages.Contains(language) ? language : "en";

    public static string? Find(string key, string language)
        => Managers.GetOrAdd(Normal(language), l => new ResourceManager($"{typeof(ExpiryEvidenceText).FullName}_{l}", typeof(ExpiryEvidenceText).Assembly))
            .GetString(key, CultureInfo.InvariantCulture);

    public static string Of(string key, string language) => Find(key, language) ?? key;

    public static string Format(string key, string language, params object?[] args)
        => string.Format(CultureInfo.InvariantCulture, Of(key, language), args);

    /// <summary>A stored code in the language — the screen's key (<paramref name="prefix"/> + code); unknown → the code.</summary>
    public static string Value(string prefix, string code, string language) => Find(prefix + code, language) ?? code;
}

using System.Collections.Concurrent;
using System.Globalization;
using System.Resources;

namespace Diten.InventoryService.Application.Features.Recall.Evidence;

/// <summary>
/// WP-MVP1-FIX-LIVE2 (CT decision "çeviri kaynağı B") — the words of the recall evidence pack (PDF, CSV and the preview the screen shows) in
/// the 7 languages, from ONE source: the service family <c>RecallEvidenceText_{lang}.resx</c> next to this file (embedded in the main
/// assembly — no satellite assembly, no culture lookup, so it reads the same under invariant globalization).
/// <list type="bullet">
/// <item>Keys named like the Web's (<c>Recall.Ev.W.*</c>, <c>Recall.Status.*</c>, <c>Recall.Event.*</c> …) are SHARED: the same key carries the
/// same value in the Web's <c>Recall.{lang}.resx</c> — a parity guard (Web tests) is red otherwise, both ways.</item>
/// <item><c>RecallPdf.*</c> keys are the PDF's own (title, footer, subject kinds) and live only here; they too are filled in 7 languages.</item>
/// <item>A missing key answers the key itself (never another language's text).</item>
/// </list>
/// </summary>
public static class RecallEvidenceText
{
    /// <summary>The 7 tenant-module languages (en first).</summary>
    public static readonly IReadOnlyList<string> Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    /// <summary>
    /// The languages the PDF draws (CT decision "yazı tipi A"): the embedded Segoe WP font has the glyphs of these five (measured, cmap);
    /// zh and ar are not covered and PDFsharp does no Arabic shaping / right-to-left — those PDFs are produced in English with a note
    /// (FU-0176-PDF-L10N). The screen and the CSV stay in the selected language.
    /// </summary>
    public static readonly IReadOnlyList<string> PdfLanguages = ["en", "tr", "fr", "es", "ru"];

    private static readonly ConcurrentDictionary<string, ResourceManager> Managers = new(StringComparer.Ordinal);

    /// <summary>The primary subtag of a language tag, if it is one of the 7; anything else (or none) → en. "tr-TR" → tr, "zh-Hans" → zh.</summary>
    public static string Normal(string? language)
    {
        var primary = (language ?? string.Empty).Split('-', '_')[0].Trim().ToLowerInvariant();
        return Languages.Contains(primary) ? primary : "en";
    }

    /// <summary>The language a PDF is drawn in for the selected one: itself when the font covers it, else en.</summary>
    public static string PdfLanguage(string language) => PdfLanguages.Contains(language) ? language : "en";

    /// <summary>The value of <paramref name="key"/> in <paramref name="language"/> (one of the 7), or null when the family has no such key.</summary>
    public static string? Find(string key, string language)
        => Managers.GetOrAdd(Normal(language), l => new ResourceManager($"{typeof(RecallEvidenceText).FullName}_{l}", typeof(RecallEvidenceText).Assembly))
            .GetString(key, CultureInfo.InvariantCulture);

    public static string Of(string key, string language) => Find(key, language) ?? key;

    public static string Format(string key, string language, params object?[] args)
        => string.Format(CultureInfo.InvariantCulture, Of(key, language), args);

    /// <summary>An evidence word — a section, table, column or summary-field key (<c>Recall.Ev.W.{key}</c>).</summary>
    public static string Word(string key, string language) => Of("Recall.Ev.W." + key, language);
}

using System.Collections.Concurrent;
using System.Globalization;
using System.Resources;

namespace Diten.InventoryService.Application.Features.CycleCounting;

/// <summary>
/// MOD-0181 DL-5 (pack §4.5; r5 SB1/F-2, r6 SB2/F-3) — the words of the Evidence Pack (PDF + CSV) in the 7 languages from ONE source: the service
/// family <c>CycleCountEvidenceText_{lang}.resx</c> next to this file (embedded in the main assembly — no satellite assembly, no culture lookup; the
/// MOD-0176 <c>ExpiryEvidenceText</c> pattern, an own copy). Keys named like the Web's (<c>CycleCount.Status.*</c>, <c>CycleCount.LineStatus.*</c>,
/// <c>CycleCount.Decision.*</c>, <c>CycleCount.Decision.Source.*</c> — see <see cref="CycleCountTextKeys.WebFamilies"/>) are SHARED: same key, same
/// value as the Web family (the parity guard <c>CycleCountEvidenceTextParityTests</c>); <c>CountPdf.*</c> are the files' own. A status, decision or
/// source is stored as a CODE and worded here at render time — never stored as text. A missing key answers the key itself.
/// </summary>
public static class CycleCountEvidenceText
{
    /// <summary>The manifest base name of the family: the folder (<c>Features/CycleCount</c>), not this class's namespace, names the embedded file.</summary>
    public const string ResourceBase = "Diten.InventoryService.Application.Features.CycleCount.CycleCountEvidenceText_";

    public static readonly IReadOnlyList<string> Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    /// <summary>The languages the PDF draws (the embedded Segoe WP glyphs — MOD-0176's measurement); zh / ar → an English PDF with a note.</summary>
    public static readonly IReadOnlyList<string> PdfLanguages = ["en", "tr", "fr", "es", "ru"];

    private static readonly ConcurrentDictionary<string, ResourceManager> Managers = new(StringComparer.Ordinal);

    /// <summary>The primary subtag of a language tag, if it is one of the 7; anything else (or none) → en (pack §4.5 "Dil kuralı").</summary>
    public static string Normal(string? language)
    {
        var primary = (language ?? string.Empty).Split('-', '_')[0].Trim().ToLowerInvariant();
        return Languages.Contains(primary) ? primary : "en";
    }

    /// <summary>The language a PDF is drawn in: the requested one when the font draws it, else en.</summary>
    public static string PdfLanguage(string language) => PdfLanguages.Contains(language) ? language : "en";

    public static string? Find(string key, string language)
        => Managers.GetOrAdd(Normal(language), l => new ResourceManager(ResourceBase + l, typeof(CycleCountEvidenceText).Assembly))
            .GetString(key, CultureInfo.InvariantCulture);

    public static string Of(string key, string language) => Find(key, language) ?? key;

    public static string Format(string key, string language, params object?[] args)
        => string.Format(CultureInfo.InvariantCulture, Of(key, language), args);

    /// <summary>A stored code in the language — the screen's key (<paramref name="prefix"/> + code); unknown → the code.</summary>
    public static string Value(string prefix, string code, string language) => Find(prefix + code, language) ?? code;
}

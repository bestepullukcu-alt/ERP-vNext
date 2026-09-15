using System.Globalization;

namespace Diten.Platform.Application.Features.Meetings.Services;

/// <summary>
/// S12 (pack §23.13/5, owner decision 2026-09-15) — the one sentence every downloaded report file starts
/// with: "as of {timestamp} — a snapshot, not a controlled copy." GxP-required, Quality-pending confirmation
/// on the WORDING only (not on whether the sentence exists); the file carries its own generation timestamp
/// either way.
///
/// <para><b>Why this is hardcoded text, not <c>IStringLocalizer</c>.</b> Measured (also recorded in
/// <c>WorkReportModels.cs</c>'s own header): Platform carries no localizer at all. The same seven-language
/// hardcoded-block pattern <c>NotificationTemplateSeed</c> already uses for its own Platform-authored,
/// user-facing text is reused here rather than inventing a general localization mechanism for one sentence.</para>
///
/// <para><b>ONE PLACE</b> (pack §23.13/5's "metin tek yerde") — every dataset's CSV/JSON export calls
/// <see cref="For"/>; no second copy of the sentence exists anywhere in this feature.</para>
/// </summary>
public static class MeetingReportExportDisclaimer
{
    private static readonly IReadOnlyDictionary<string, string> Sentences = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "Snapshot taken as of {0} — not a controlled copy.",
        ["tr"] = "{0} itibarıyla alınmış anlık görüntüdür, kontrollü kopya değildir.",
        ["fr"] = "Instantané pris le {0} — ce n'est pas une copie contrôlée.",
        ["es"] = "Instantánea tomada el {0} — no es una copia controlada.",
        ["zh"] = "本快照生成于 {0} — 非受控副本。",
        ["ar"] = "لقطة مأخوذة بتاريخ {0} — ليست نسخة خاضعة للرقابة.",
        ["ru"] = "Снимок сделан по состоянию на {0} — не является контролируемой копией."
    };

    /// <summary>
    /// <paramref name="locale"/> is whatever the caller sent (case-insensitive, may be null/unrecognised);
    /// falls back to English — never blank, and never a thrown exception over a display preference.
    /// <paramref name="generatedAtUtc"/> is rendered as an ISO 8601 UTC instant, invariant culture, the same
    /// convention every other machine-readable timestamp on this export uses.
    /// </summary>
    public static string For(string? locale, DateTimeOffset generatedAtUtc)
    {
        var key = string.IsNullOrWhiteSpace(locale) ? "en" : locale.Trim().ToLowerInvariant();
        var template = Sentences.TryGetValue(key, out var value) ? value : Sentences["en"];
        var timestamp = generatedAtUtc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        return string.Format(CultureInfo.InvariantCulture, template, timestamp);
    }
}

using Diten.Platform.Application.Features.Meetings.Services;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// WP-MG-MOD0357-S12-EXPORT-WORDING-01 — the exported file's own disclaimer sentence (§23.13/5, CT
/// recommendation "point-in-time, not a controlled copy") must say "controlled copy" in all seven
/// languages, never "controlled document" — the wording this WP exists to fix. Exercises the REAL
/// <see cref="MeetingReportExportDisclaimer.For"/>, not a copy of its dictionary.
/// </summary>
public sealed class MeetingReportExportDisclaimerTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

    public static IEnumerable<object[]> LocaleTerms => new List<object[]>
    {
        new object[] { "en", "controlled copy", "controlled document" },
        new object[] { "tr", "kontrollü kopya", "belge" },
        new object[] { "fr", "copie contrôlée", "document contrôlé" },
        new object[] { "es", "copia controlada", "documento controlado" },
        new object[] { "zh", "受控副本", "受控文档" },
        new object[] { "ar", "نسخة خاضعة للرقابة", "مستند" },
        new object[] { "ru", "контролируемой копией", "контролируемый документ" },
    };

    [Theory]
    [MemberData(nameof(LocaleTerms))]
    public void For_each_of_the_seven_locales_says_controlled_copy_never_controlled_document(
        string locale, string requiredTerm, string forbiddenTerm)
    {
        var sentence = MeetingReportExportDisclaimer.For(locale, Timestamp);

        Assert.Contains(requiredTerm, sentence);
        Assert.DoesNotContain(forbiddenTerm, sentence);
    }

    [Fact]
    public void An_unrecognised_locale_falls_back_to_English_never_throws_never_blank()
    {
        var sentence = MeetingReportExportDisclaimer.For("xx-not-a-locale", Timestamp);

        Assert.False(string.IsNullOrWhiteSpace(sentence));
        Assert.Contains("controlled copy", sentence);
    }

    [Fact]
    public void The_timestamp_is_embedded_as_an_invariant_ISO_8601_UTC_instant()
    {
        var sentence = MeetingReportExportDisclaimer.For("en", Timestamp);

        Assert.Contains("2026-09-15T12:00:00Z", sentence);
    }
}

using System.Xml.Linq;
using Diten.Web.Tests.Traceability;
using Xunit;

namespace Diten.Web.Tests.Expiry;

/// <summary>
/// WP-MVP1-FIX-LIVE3 (live finding 4: the FEFO panel said "Bugün 19.10.2026" at 00:30 local on the 20th) — the pack decides the day:
/// MOD-0176 ASSUMPTION-0176-05 "today = UTC date" (and MOD-0174 "ExpiryDate &lt; today (UTC)"); no tenant / company time zone is readable by
/// the Inventory service. So the behaviour is kept and the screens SAY it: the "today" of the FEFO panel and of the recall readiness panel is
/// labelled "(UTC)" in all seven languages, and both scripts format that date in UTC (the service's day), never in the browser's zone.
/// </summary>
public sealed class TodayIsUtcLabelTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    private static string Value(string folder, string lang, string key)
        => XDocument.Load(Path.Combine(TraceViewRenderer.WebRoot, "Resources", "Views", folder, $"{folder}.{lang}.resx")).Root!
            .Elements("data").Single(d => (string)d.Attribute("name")! == key).Element("value")!.Value;

    [Theory]
    [InlineData("Expiry", "Expiry.Dashboard.Subtitle")]
    [InlineData("Expiry", "Expiry.Dashboard.SubtitleToday")]
    [InlineData("Recall", "Recall.Dashboard.Subtitle")]
    public void The_panel_today_is_labelled_UTC_in_seven_languages(string folder, string key)
    {
        foreach (var lang in Languages)
        {
            var text = Value(folder, lang, key);
            Assert.Contains("(UTC) {0}", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_panels_format_the_service_day_in_UTC()
    {
        var js = Path.Combine(TraceViewRenderer.WebRoot, "wwwroot", "assets", "js");
        foreach (var file in new[] { Path.Combine(js, "Expiry", "expiry-ui.js"), Path.Combine(js, "Recall", "recall-ui.js") })
            Assert.Contains("String(wire).slice(0, 10) + 'T00:00:00Z'", File.ReadAllText(file), StringComparison.Ordinal);
        Assert.Contains("t('Expiry.Dashboard.SubtitleToday', ui.date(panel.asOf))", File.ReadAllText(Path.Combine(js, "Expiry", "dashboard.js")), StringComparison.Ordinal);
        Assert.Contains("t('Recall.Dashboard.Subtitle', fmt.date(panel.asOfUtc)", File.ReadAllText(Path.Combine(js, "Recall", "dashboard.js")), StringComparison.Ordinal);
    }
}

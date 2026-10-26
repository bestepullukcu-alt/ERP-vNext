using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Tests.Traceability;
using Xunit;

namespace Diten.Web.Tests.Recall;

/// <summary>
/// WP-MVP1-FIX-LIVE2 (CT decision "çeviri kaynağı B") — the PARITY GUARD between the evidence packs' service resx families and the screens'
/// resx families. The PDF / CSV are service output, so their words live in the service (<c>RecallEvidenceText_{lang}.resx</c>,
/// <c>ExpiryEvidenceText_{lang}.resx</c>); the rule "no second list" is kept by this guard instead:
/// <list type="bullet">
/// <item>every SHARED service key exists in the Web family with the SAME value, in all 7 languages;</item>
/// <item>every Web key of a shared group (a status, level, direction, node, event, gap, channel … value) exists in the service — a value the
/// screen knows and the pack does not is red;</item>
/// <item>a service-only key is a file's own word (<c>RecallPdf.*</c> / <c>ExpiryPdf.*</c>) and is filled in all 7 languages with its
/// placeholders, copying English only where listed by name.</item>
/// </list>
/// The shared groups are named here, in code (sabotage i: change one shared value in a service file → red).
/// </summary>
public sealed class EvidenceTextParityTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    public sealed record Family(string Name, string ServiceFolder, string ServiceBase, string WebFamily, string[] SharedPrefixes, string[] SharedKeys,
        string ServiceOnlyPrefix, string[] SameAsEnglishAllowed);

    public static readonly Family RecallFamily = new("Recall", Path.Combine("Features", "Recall", "Evidence"), "RecallEvidenceText", "Recall",
        ["Recall.Ev.W.", "Recall.Status.", "Recall.Level.", "Recall.Direction.", "Recall.Kind.", "Recall.Event.", "Recall.ReqStatus.", "Recall.NotNeeded.",
            "Recall.Channel.", "Recall.Gap.", "Recall.DrillStatus."],
        ["Recall.Class.N", "Recall.Drills.ModeAuto", "Recall.Drills.ModeManual"],
        "RecallPdf.",
        // fr "Page" is French
        ["RecallPdf.Page"]);

    public static readonly Family ExpiryFamily = new("Expiry", Path.Combine("Features", "Expiry"), "ExpiryEvidenceText", "Expiry",
        ["Expiry.Type.", "Expiry.Status.", "Expiry.Resolution.", "Expiry.Policy.Field.", "Expiry.Policy.Mode."],
        ["Expiry.Evidence.Summary", "Expiry.Evidence.Exceptions", "Expiry.Evidence.PolicyChanges", "Expiry.Evidence.Bypasses"],
        "ExpiryPdf.",
        // fr "Page" / "Types" / "Type" / "Lot", tr "Lot" / "No" are those languages' own words; fr / es "h" the hour symbol
        ["ExpiryPdf.Page", "ExpiryPdf.Types", "ExpiryPdf.Type", "ExpiryPdf.Lot", "ExpiryPdf.No", "ExpiryPdf.Hours"]);

    public static TheoryData<string> Families => new() { "Recall", "Expiry" };

    private static Family Of(string name) => name == "Recall" ? RecallFamily : ExpiryFamily;

    public static Dictionary<string, string> Resx(string path)
        => XDocument.Load(path).Root!.Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);

    public static string ServicePath(Family f, string lang)
        => Path.Combine(TraceViewRenderer.RepoRoot(), "services", "Diten.InventoryService", "src", "Diten.InventoryService.Application", f.ServiceFolder,
            $"{f.ServiceBase}_{lang}.resx");

    private static string WebPath(Family f, string lang) => Path.Combine(TraceViewRenderer.WebRoot, "Resources", "Views", f.WebFamily, $"{f.WebFamily}.{lang}.resx");

    private static bool Shared(Family f, string key) => f.SharedPrefixes.Any(p => key.StartsWith(p, StringComparison.Ordinal)) || f.SharedKeys.Contains(key);

    private static string Placeholders(string value) => string.Join(",", Regex.Matches(value, @"\{\d\}").Select(m => m.Value).OrderBy(v => v, StringComparer.Ordinal));

    [Theory]
    [MemberData(nameof(Families))]
    public void The_service_family_is_seven_files_with_the_same_keys_and_real_text(string family)
    {
        var f = Of(family);
        var folder = Path.GetDirectoryName(ServicePath(f, "en"))!;
        var files = Directory.GetFiles(folder, f.ServiceBase + "*.resx").Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.Equal(Languages.Select(l => $"{f.ServiceBase}_{l}.resx").OrderBy(n => n, StringComparer.Ordinal), files);
        var en = Resx(ServicePath(f, "en"));
        Assert.True(en.Count > 40, $"{family}: {en.Count} keys");
        foreach (var lang in Languages)
        {
            var values = Resx(ServicePath(f, lang));
            Assert.Equal(en.Keys.OrderBy(k => k, StringComparer.Ordinal), values.Keys.OrderBy(k => k, StringComparer.Ordinal));
            foreach (var (key, value) in values)
            {
                Assert.False(string.IsNullOrWhiteSpace(value), $"{lang} {key} is empty");
                Assert.Equal(Placeholders(en[key]), Placeholders(value));
                Assert.True(Shared(f, key) || key.StartsWith(f.ServiceOnlyPrefix, StringComparison.Ordinal), $"{key}: neither shared nor {f.ServiceOnlyPrefix}*");
                if (lang != "en" && !Shared(f, key) && value == en[key]) Assert.Contains(key, f.SameAsEnglishAllowed);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Families))]
    public void Every_shared_word_is_the_screens_word_both_ways_in_seven_languages(string family)
    {
        var f = Of(family);
        foreach (var lang in Languages)
        {
            var service = Resx(ServicePath(f, lang));
            var web = Resx(WebPath(f, lang));
            foreach (var (key, value) in service.Where(p => Shared(f, p.Key)))
            {
                Assert.True(web.TryGetValue(key, out var screen), $"{lang} {key}: the screen has no such key");
                Assert.True(screen == value, $"{lang} {key}: pack \"{value}\" ≠ screen \"{screen}\"");
            }

            // a value the screen knows (a status, an event, a node kind …) that the pack cannot word is red
            foreach (var key in web.Keys.Where(k => Shared(f, k))) Assert.True(service.ContainsKey(key), $"{lang} {key}: missing in the service family");
        }
    }
}

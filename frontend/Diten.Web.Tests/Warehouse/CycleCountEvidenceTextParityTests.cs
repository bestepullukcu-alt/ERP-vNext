using System.Xml.Linq;
using Diten.Web.Tests.Traceability;
using Xunit;

namespace Diten.Web.Tests.Warehouse;

/// <summary>
/// MOD-0181 DL-5 (pack §4.5, §5 r6 SB2/F-3; AC-43) — the parity guard between the Evidence Pack's ONE text source in the service
/// (<c>services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/CycleCount/CycleCountEvidenceText_{lang}.resx</c>) and the Web
/// screens: the service family is 7 files with one key set; every SHARED key — <c>CycleCount.Status.*</c> (the dashboard's family),
/// <c>CycleCount.LineStatus.*</c>, <c>CycleCount.Decision.*</c>, <c>CycleCount.Decision.Source.*</c> (the Variance Trace's family) — has the same
/// name AND value on both sides, in BOTH directions, in all 7 languages. The prefix → family map is the service's
/// <c>CycleCountTextKeys.WebFamilies</c> (read here from its source, so a moved family turns this red). The MOD-0177 recall guard is not touched.
/// </summary>
public sealed class CycleCountEvidenceTextParityTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    private static readonly string Repo = Path.GetFullPath(Path.Combine(TraceViewRenderer.WebRoot, "..", ".."));

    private static readonly string ServiceFolder = Path.Combine(Repo, "services", "Diten.InventoryService", "src", "Diten.InventoryService.Application", "Features", "CycleCount");

    /// <summary>The service's prefix → Web family map (<c>CycleCountTextKeys.WebFamilies</c>), the values the guard measures.</summary>
    public static readonly TheoryData<string, string> Shared = new()
    {
        { "CycleCount.Status.", "Views/Warehouse/CycleCounts/CycleCountsIndex" },
        { "CycleCount.LineStatus.", "Views/Warehouse/CountVariances/CountVariancesIndex" },
        { "CycleCount.Decision.", "Views/Warehouse/CountVariances/CountVariancesIndex" },
        { "CycleCount.Decision.Source.", "Views/Warehouse/CountVariances/CountVariancesIndex" }
    };

    private static Dictionary<string, string> Rows(string path) =>
        XDocument.Load(path).Root!.Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value ?? string.Empty, StringComparer.Ordinal);

    private static Dictionary<string, string> Service(string lang) => Rows(Path.Combine(ServiceFolder, $"CycleCountEvidenceText_{lang}.resx"));

    private static Dictionary<string, string> Web(string family, string lang) =>
        Rows(Path.Combine([TraceViewRenderer.WebRoot, "Resources", .. family.Split('/')]) + $".{lang}.resx");

    [Fact]
    public void The_service_family_is_seven_files_with_one_key_set()
    {
        var en = Service("en");
        Assert.True(en.Count > 80, $"only {en.Count} keys");
        foreach (var lang in Languages)
        {
            var rows = Service(lang);
            Assert.Equal(en.Keys.Order(StringComparer.Ordinal), rows.Keys.Order(StringComparer.Ordinal));
            Assert.All(rows, kv => Assert.False(string.IsNullOrWhiteSpace(kv.Value), $"{lang}: {kv.Key} is empty"));
        }

        Assert.Equal(7, Directory.GetFiles(ServiceFolder, "CycleCountEvidenceText_*.resx").Length);
    }

    [Theory]
    [MemberData(nameof(Shared))]
    public void A_shared_key_has_the_same_name_and_value_in_the_service_and_the_web_in_both_directions(string prefix, string family)
    {
        bool Owns(string key) => key.StartsWith(prefix, StringComparison.Ordinal)
                                 && !(prefix == "CycleCount.Decision." && key.StartsWith("CycleCount.Decision.Source.", StringComparison.Ordinal));
        foreach (var lang in Languages)
        {
            var service = Service(lang).Where(kv => Owns(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
            var web = Web(family, lang).Where(kv => Owns(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
            Assert.True(service.Count > 0, $"{lang}: the service has no {prefix} key");
            Assert.Equal(web.Keys.Order(StringComparer.Ordinal), service.Keys.Order(StringComparer.Ordinal));
            foreach (var (key, value) in web) Assert.True(value == service[key], $"{lang}: {key} — web \"{value}\" ≠ service \"{service[key]}\"");
        }
    }

    [Fact]
    public void The_map_is_the_services_own_CycleCountTextKeys_WebFamilies()
    {
        var source = File.ReadAllText(Path.Combine(ServiceFolder, "CycleCountTextKeys.cs"));
        Assert.Contains("[StatusPrefix] = \"Views/Warehouse/CycleCounts/CycleCountsIndex\"", source, StringComparison.Ordinal);
        Assert.Contains("[LineStatusPrefix] = \"Views/Warehouse/CountVariances/CountVariancesIndex\"", source, StringComparison.Ordinal);
        Assert.Contains("[DecisionPrefix] = \"Views/Warehouse/CountVariances/CountVariancesIndex\"", source, StringComparison.Ordinal);
        Assert.Contains("[DecisionSourcePrefix] = \"Views/Warehouse/CountVariances/CountVariancesIndex\"", source, StringComparison.Ordinal);
    }
}

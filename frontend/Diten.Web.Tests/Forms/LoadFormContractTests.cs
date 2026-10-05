using System.Xml.Linq;
using Xunit;

namespace Diten.Web.Tests.Forms;

// MOD-0185 Loads resources and bridge (R-4b): seven tenant languages with identical non-empty key sets, every published
// createLoadPlan error code localized, and a Dictionary bridge (MODULE-RECIPE 5.1, 5.3).
public sealed class LoadFormContractTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
    private static readonly string[] ErrorKeys =
    [
        "ErrCarrierNotFound", "ErrCarrierModeUnsupported", "ErrShipmentNotFound", "ErrShipmentNotEligible", "ErrShipmentCarrierMismatch",
        "ErrShipmentAlreadyAssigned", "ErrDuplicateShipment", "ErrInvalidLoadStops", "ErrIdempotencyKeyReused", "ErrCorrelationRootMismatch",
        "ErrDependencyUnavailable", "ErrDependencyResponseInvalid", "ErrReferenceStateUnavailable", "ErrPersistenceUnavailable", "ErrInternalError"
    ];

    [Fact]
    public void SevenLanguagesHaveIdenticalNonEmptyKeySets()
    {
        var english = Resx("en");
        foreach (var language in Languages)
        {
            var resx = Resx(language);
            Assert.Equal(english.Keys.OrderBy(k => k, StringComparer.Ordinal), resx.Keys.OrderBy(k => k, StringComparer.Ordinal));
            Assert.All(resx, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Value), $"{language}:{entry.Key}"));
            if (language != "en") Assert.NotEqual(english["LoadsTitle"], resx["LoadsTitle"]);
        }
    }

    [Fact]
    public void EveryPublishedCreateCodeAndEveryStatusIsLocalized()
    {
        var english = Resx("en");
        Assert.All(ErrorKeys, key => Assert.True(english.ContainsKey(key), key));
        Assert.All(new[] { "Draft", "Planned", "Tendered", "Accepted", "Dispatched", "Completed", "Cancelled" }, s => Assert.True(english.ContainsKey("Status" + s), s));
    }

    [Fact]
    public void BridgeIsADictionaryAndViewsCarryNoLayoutInPartials()
    {
        var bridge = File.ReadAllText(Path.Combine(Root(), "frontend/Diten.Web/Views/SupplyChain/Loads/_IndexL10n.cshtml"));
        Assert.Contains("@Json.Serialize(new Dictionary<string, string>", bridge);
        Assert.DoesNotContain("@Json.Serialize(new\n", bridge);
        foreach (var partial in Directory.GetFiles(Path.Combine(Root(), "frontend/Diten.Web/Views/SupplyChain/Loads"), "_*.cshtml"))
            Assert.DoesNotContain("Layout =", File.ReadAllText(partial));
        Assert.Contains("Layout = \"_LayoutTenantShell\";", File.ReadAllText(Path.Combine(Root(), "frontend/Diten.Web/Views/SupplyChain/Loads/Index.cshtml")));
    }

    private static Dictionary<string, string> Resx(string language) =>
        XDocument.Load(Path.Combine(Root(), $"frontend/Diten.Web/Resources/Views/SupplyChain/Loads/LoadsIndex.{language}.resx")).Root!
            .Elements("data").ToDictionary(e => e.Attribute("name")!.Value, e => e.Element("value")?.Value ?? string.Empty, StringComparer.Ordinal);

    private static string Root() { var dir = new DirectoryInfo(AppContext.BaseDirectory); while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent; return dir?.FullName ?? throw new DirectoryNotFoundException(); }
}

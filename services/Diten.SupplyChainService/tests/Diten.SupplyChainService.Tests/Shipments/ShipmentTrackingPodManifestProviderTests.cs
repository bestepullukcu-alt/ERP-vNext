using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.SupplyChainService.Api.ModuleRegistration;
using Xunit;

namespace Diten.SupplyChainService.Tests.Shipments;

// Q288 group 3 (F-Q245-6, Q247's remaining half). This test used to pin the provider against the provider's own
// values, so it stayed green while the provider disagreed with the module pack. It now reads MOD-0183 pack §22 —
// the authority for this manifest — on every run and compares the provider with it, field by field. A red result
// here is a disagreement between the provider and the pack; the expectation must not be edited to make it pass.
public sealed class ShipmentTrackingPodManifestProviderTests
{
    private const string PackPath = "execution/domains/supply-chain-execution/module-packs/MOD-0183-shipment-tracking-pod.md";
    private static readonly ModuleManifestDocument Manifest = new ShipmentTrackingPodManifestProvider().GetManifest();

    [Fact]
    public void Identity_WhenComparedWithPack22_MatchesEveryField()
    {
        var rows = Table("### Identity").ToDictionary(row => row[0], row => row[1], StringComparer.Ordinal);
        var names = Split(rows["ModuleCode / ModuleName / DisplayName"]);
        var domain = Split(rows["Domain / Service"]);
        var version = Split(rows["ModuleVersion / IsTenantAssignable / IsBaseline"]);
        var sort = Split(rows["SortOrder / Icon (SOFT, seed-once)"]);
        var differences = new List<string>();

        Compare(differences, "ModuleCode", names[0], Manifest.ModuleCode);
        Compare(differences, "ModuleName", names[1], Manifest.ModuleName);
        Compare(differences, "DisplayName", names[2], Manifest.DisplayName);
        Compare(differences, "Domain", domain[0], Manifest.Domain);
        Compare(differences, "Service", domain[1], Manifest.Service);
        Compare(differences, "ModuleVersion", version[0], Manifest.ModuleVersion);
        Compare(differences, "IsTenantAssignable", version[1], Flag(Manifest.IsTenantAssignable));
        Compare(differences, "IsBaseline", version[2], Flag(Manifest.IsBaseline));
        Compare(differences, "SortOrder", sort[0], Manifest.SortOrder.ToString());
        Compare(differences, "Icon", sort[1], Manifest.Icon ?? "");

        Assert.True(differences.Count == 0, "Provider differs from pack §22 identity: " + string.Join("; ", differences));
    }

    [Fact]
    public void Pages_WhenComparedWithPack22_MatchEveryColumn()
    {
        var expected = Table("### Pages")
            .Select(row => string.Join(" | ", row[0], row[1], row[2], row[3], row[4] == "—" ? "" : row[4], row[5], row[6], row[7]))
            .ToArray();
        var actual = Manifest.Pages
            .Select(page => string.Join(" | ", page.PageCode, page.DisplayName, page.RoutePath, page.RequiredPermission,
                page.ParentPageCode ?? "", Flag(page.IsNavigationVisible), page.PageType, page.SortOrder))
            .ToArray();

        Assert.NotEmpty(expected);
        Assert.True(expected.SequenceEqual(actual), Difference("pages", expected, actual));
    }

    [Fact]
    public void Actions_WhenComparedWithPack22_MatchPerPageCodeKeyPlacementAndDangerousFlag()
    {
        // M-05: the action table equals the manifest actions per page (code, key, placement, dangerous flag).
        var expected = Table("### Actions")
            .Select(row => string.Join(" | ", row[0], row[1], row[3], row[4], Flag(row[5] == "yes")))
            .OrderBy(line => line, StringComparer.Ordinal).ToArray();
        var actual = Manifest.Pages
            .SelectMany(page => page.Actions.Select(action => string.Join(" | ", page.PageCode, action.ActionCode,
                action.PermissionKey, action.ActionType, Flag(action.IsDangerous))))
            .OrderBy(line => line, StringComparer.Ordinal).ToArray();

        Assert.NotEmpty(expected);
        Assert.True(expected.SequenceEqual(actual), Difference("actions", expected, actual));
    }

    // Rows of the first markdown table under the given §22 subheading, with backticks and bold removed.
    private static List<string[]> Table(string heading)
    {
        var lines = File.ReadAllLines(RepositoryFile.Locate(PackPath));
        var start = Array.FindIndex(lines, line => line.StartsWith("## 22. Self-registration", StringComparison.Ordinal));
        Assert.True(start >= 0, "Pack §22 not found.");
        var at = Array.FindIndex(lines, start, line => line.StartsWith(heading, StringComparison.Ordinal));
        Assert.True(at >= 0, $"Pack §22 has no '{heading}' subsection.");
        var rows = new List<string[]>();
        for (var i = at + 1; i < lines.Length && !lines[i].StartsWith("#", StringComparison.Ordinal); i++)
        {
            if (!lines[i].StartsWith("|", StringComparison.Ordinal)) { if (rows.Count > 0) break; continue; }
            var cells = lines[i].Trim().Trim('|').Split('|').Select(cell => cell.Replace("`", "").Replace("**", "").Trim()).ToArray();
            if (cells.All(cell => cell.Trim('-', ':').Length == 0)) continue;
            rows.Add(cells);
        }
        return rows.Skip(1).ToList();
    }

    private static string[] Split(string cell) => cell.Split(" / ").Select(part => part.Trim()).ToArray();
    private static string Flag(bool value) => value ? "true" : "false";

    private static void Compare(List<string> differences, string field, string pack, string provider)
    {
        if (!string.Equals(pack, provider, StringComparison.Ordinal)) differences.Add($"{field}: pack '{pack}', provider '{provider}'");
    }

    private static string Difference(string what, string[] pack, string[] provider) =>
        $"Provider {what} differ from pack §22.\nOnly in pack:\n  " + string.Join("\n  ", pack.Except(provider)) +
        "\nOnly in provider:\n  " + string.Join("\n  ", provider.Except(pack));
}

using System.Reflection;
using System.Text.RegularExpressions;
using Diten.InventoryService.Api.Controllers.Traceability;
using Diten.InventoryService.Api.ModuleRegistration;
using Diten.InventoryService.Infrastructure.Authorization;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Trace;

/// <summary>
/// MOD-0174 self-registration completeness (module-self-registration-standard §3), BOTH directions, measured against the
/// sources rather than a list typed here:
/// <list type="number">
/// <item>the manifest equals the pack §14 tables, parsed from the pack file itself: every page (code, route, permission,
/// nav-visible, parent) and, per page, every action (code, key, toolbar / row, dangerous) — nothing more, nothing less;</item>
/// <item>every key is a real <see cref="TracePermissions"/> / <see cref="InventoryPermissions"/> constant;</item>
/// <item>every key the TRACE controller enforces (<c>[HasPermission]</c> + the two PATCH keys) is reachable from the
/// manifest (page, action or <c>ApiPermissions</c>), and every <see cref="TracePermissions"/> key is surfaced by it;</item>
/// <item><c>trace.genealogy.link</c> is API-only (standard §2d), Tenant-scoped; <c>NEWLOTQUICK</c> is not here (slice P3,
/// INVENTORY-LEDGER manifest).</item>
/// </list>
/// The screens are drawn by slice P3 — the frontend direction of the completeness rule is P3's.
/// </summary>
public sealed class LotSerialTrackingManifestCompletenessTests
{
    private static readonly string PackPath = Path.Combine(RepoPaths.Root, "execution", "domains", "supply-chain-execution", "module-packs", "MOD-0174-lot-serial-tracking.md");

    /// <summary>Pack §14 screen names → page codes (the "Screen" column of the button table).</summary>
    private static readonly Dictionary<string, string> PageOfScreen = new(StringComparer.Ordinal)
    {
        ["Lot Gezgini"] = "LOTS",
        ["Lot Detayı"] = "LOTDETAILS",
        ["Seri Gezgini"] = "SERIALS",
        ["Lot Hareket Geçmişi"] = "LOTMOVEMENTS",
        ["Lot Hareket Geçmişi (fiş)"] = "LOTMOVEMENTS",
        ["İzlenebilirlik"] = "LOTTRACE",
        ["Aktarma"] = "LOTMIGRATION"
    };

    private static readonly Regex PageRow = new(@"^\| `(?<code>[A-Z]+)` \| `(?<route>/[^`]+)` \| `(?<perm>[a-z.\-]+)` \| (?<nav>\*\*true\*\*|false) \| (?<parent>`[A-Z]+`|—) \|$", RegexOptions.Compiled);

    private static readonly Regex ActionRow = new(@"^\| (?<screens>[^|]+?) \| [^|]+ \| `(?<code>[A-Z]+)`(?<rest>[^|]*) \| (?<placement>[^|]+?) \| `(?<perm>[a-z.\-]+)` \| (?<danger>[^|]+?) \|$", RegexOptions.Compiled);

    private static readonly Lazy<string[]> Section14 = new(() =>
    {
        var lines = File.ReadAllLines(PackPath);
        var start = Array.FindIndex(lines, l => l.StartsWith("## 14.", StringComparison.Ordinal));
        var end = Array.FindIndex(lines, start + 1, l => l.StartsWith("## 15.", StringComparison.Ordinal));
        Assert.True(start >= 0 && end > start, "pack §14 not found");
        return lines[start..end];
    });

    private static readonly IModuleManifestProvider Provider = new LotSerialTrackingManifestProvider();

    [Fact]
    public void The_pages_are_exactly_the_pack_14_page_table()
    {
        var packPages = Section14.Value.Select(l => PageRow.Match(l)).Where(m => m.Success)
            .Select(m => $"{m.Groups["code"].Value}|{m.Groups["route"].Value}|{m.Groups["perm"].Value}|{m.Groups["nav"].Value == "**true**"}|{m.Groups["parent"].Value.Trim('`').Replace("—", "")}")
            .OrderBy(x => x).ToList();
        Assert.Equal(8, packPages.Count); // non-vacuous: the parser found the table

        var manifest = Provider.GetManifest();
        var pages = manifest.Pages
            .Select(p => $"{p.PageCode}|{p.RoutePath}|{p.RequiredPermission}|{p.IsNavigationVisible}|{p.ParentPageCode ?? ""}")
            .OrderBy(x => x).ToList();
        Assert.Equal(packPages, pages);
        Assert.Equal("LOT-SERIAL-TRACKING", manifest.ModuleCode);
        Assert.True(manifest.IsTenantAssignable);
        Assert.All(manifest.Pages, p => Assert.False(p.RoutePath.StartsWith("/Platform", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void The_actions_of_every_page_are_exactly_the_pack_14_button_table()
    {
        var expected = new List<string>();
        var crossModule = new List<string>();
        foreach (var match in Section14.Value.Select(l => ActionRow.Match(l)).Where(m => m.Success))
        {
            var code = match.Groups["code"].Value;
            var screens = match.Groups["screens"].Value.Split(" · ").Select(x => x.Trim()).ToList();
            if (screens.Any(x => !PageOfScreen.ContainsKey(x)))
            {
                crossModule.Add(code);
                continue;
            }

            var toolbar = match.Groups["placement"].Value.Trim() == "Toolbar";
            var dangerous = match.Groups["danger"].Value.Contains("**yes**", StringComparison.Ordinal);
            expected.AddRange(screens.Select(x => $"{PageOfScreen[x]}|{code}|{OwnNamespaceKey(match.Groups["perm"].Value)}|{toolbar}|{dangerous}"));
        }

        Assert.Equal(["NEWLOTQUICK"], crossModule); // the one cross-module action (INVENTORY-LEDGER pages, slice P3)
        Assert.True(expected.Count >= 25, "the button table parser found too few rows: " + expected.Count);

        var actual = Provider.GetManifest().Pages
            .SelectMany(p => p.Actions.Select(a => $"{p.PageCode}|{a.ActionCode}|{a.PermissionKey}|{a.IsToolbarAction}|{a.IsDangerous}"))
            .ToList();
        Assert.Equal(expected.Distinct().OrderBy(x => x), actual.OrderBy(x => x));
        Assert.All(Provider.GetManifest().Pages.SelectMany(p => p.Actions), a =>
            Assert.Equal(a.IsToolbarAction ? "Toolbar" : "RowAction", a.ActionType));
        Assert.DoesNotContain(Provider.GetManifest().Pages.SelectMany(p => p.Actions), a => a.ActionCode == "NEWLOTQUICK");
    }

    [Fact]
    public void Every_key_is_a_real_constant_and_genealogy_link_is_the_one_api_only_permission()
    {
        var real = Constants(typeof(TracePermissions)).Concat(Constants(typeof(InventoryPermissions))).ToHashSet(StringComparer.Ordinal);
        var manifest = Provider.GetManifest();
        Assert.All(manifest.Pages, p => Assert.Contains(p.RequiredPermission, real));
        Assert.All(manifest.Pages.SelectMany(p => p.Actions), a => Assert.Contains(a.PermissionKey, real));

        var api = Assert.Single(manifest.ApiPermissions ?? []);
        Assert.Equal(TracePermissions.GenealogyLink, api.PermissionKey);
        Assert.Equal("Tenant", api.Scope);
        Assert.DoesNotContain(manifest.Pages.SelectMany(p => p.Actions), a => a.PermissionKey == api.PermissionKey);
    }

    [Fact]
    public void Every_enforced_key_is_reachable_and_every_trace_key_is_surfaced()
    {
        var enforced = typeof(TraceController).GetCustomAttributes<HasPermissionAttribute>(inherit: true)
            .Concat(typeof(TraceController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .SelectMany(m => m.GetCustomAttributes<HasPermissionAttribute>(inherit: true)))
            .Select(a => a.Policy!["Permission:".Length..])
            .Concat([TracePermissions.LotsUpdate, TracePermissions.LotsUpdateReleased]) // PATCH: the key follows the lot status
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains(TracePermissions.GenealogyLink, enforced); // the API-only key, so the scan is not vacuous

        var manifest = Provider.GetManifest();
        var reachable = manifest.Pages.Select(p => p.RequiredPermission)
            .Concat(manifest.Pages.SelectMany(p => p.Actions).Select(a => a.PermissionKey))
            .Concat((manifest.ApiPermissions ?? []).Select(p => p.PermissionKey))
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(enforced, key => Assert.True(reachable.Contains(key), $"[HasPermission({key})] is enforced but the manifest never declares it"));
        Assert.All(Constants(typeof(TracePermissions)), key => Assert.True(reachable.Contains(key), $"{key} is never surfaced by the manifest"));
        Assert.All(Constants(typeof(TracePermissions)), key => Assert.Matches(@"^trace\.[a-z]+\.[a-z\-]+$", key)); // PKS-001 shape, own namespace
    }

    /// <summary>
    /// FU-0174-CAT-01 (WP-0174-P2): a pack §14 button whose key belongs to another module (<c>inventory.stock.read</c> on the
    /// "open in Stock Ledger" buttons) is declared here with <c>trace.lots.read</c> — the target page enforces its own key, and
    /// a foreign key in this manifest moved its catalog attribution to this module.
    /// </summary>
    private static string OwnNamespaceKey(string packKey)
        => packKey.StartsWith("trace.", StringComparison.Ordinal) ? packKey : TracePermissions.LotsRead;

    private static IEnumerable<string> Constants(Type type)
        => type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!);
}

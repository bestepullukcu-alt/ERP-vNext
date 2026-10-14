using System.Reflection;
using System.Text.RegularExpressions;
using Diten.InventoryService.Api.Controllers.Quarantine;
using Diten.InventoryService.Api.ModuleRegistration;
using Diten.InventoryService.Infrastructure.Authorization;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Diten.InventoryService.Tests.Quarantine;

/// <summary>
/// MOD-0175 self-registration (pack §14, §16 Q1 #6), measured against the PACK FILE, not a list typed here:
/// <list type="bullet">
/// <item>the manifest pages and per-page actions equal the pack §14 tables (code, route, key, nav-visible, parent; action
/// code, placement, key, dangerous);</item>
/// <item>every key is in the module's own namespace <c>quarantine.*</c> — no <c>inventory.*</c> / <c>trace.*</c> key
/// (FU-0174-CAT-01; K3 sabotage (d): add <c>inventory.stock.read</c> → red);</item>
/// <item>every route is a tenant route — none under <c>/Platform</c> (K3 sabotage (c): <c>/Platform/Quarantine</c> → red);</item>
/// <item>every key the quarantine controller enforces is reachable from the manifest, and every <c>quarantine.*</c> key is surfaced.</item>
/// </list>
/// </summary>
public sealed class QuarantineManifestTests
{
    private static readonly string PackPath = Path.Combine(RepoPaths.Root, "execution", "domains", "supply-chain-execution", "module-packs",
        "MOD-0175-quarantine-blocked-stock.md");

    private static readonly Dictionary<string, string> PageOfScreen = new(StringComparer.Ordinal)
    {
        ["Karantina Kuyruğu"] = "QUARANTINEQUEUE",
        ["Detay"] = "QUARANTINEDETAILS",
        ["Karar Ver"] = "QUARANTINEDECIDE",
        ["Bloke Stok"] = "BLOCKEDSTOCK",
        ["Disposition"] = "BLOCKEDDISPOSITION",
        ["Yeniden Test"] = "RETESTQUEUE",
        ["Ayarlar"] = "QUARANTINESETTINGS"
    };

    private static readonly Regex PageRow = new(@"^\| `(?<code>[A-Z]+)` \| `(?<route>/[^`]+)` \| `(?<perm>[a-z.\-]+)` \| (?<nav>true|false) \| (?<parent>`[A-Z]+`|—) \|$", RegexOptions.Compiled);

    private static readonly Regex ActionRow = new(@"^\| (?<screen>[^|]+?) \| [^|]+ \| `(?<code>[A-Z]+)` \| (?<placement>RowAction|Toolbar|form) \| `(?<perm>[a-z.\-]+)`[^|]* \| (?<danger>[^|]+?) \|$", RegexOptions.Compiled);

    private static readonly Lazy<string[]> Section14 = new(() =>
    {
        var lines = File.ReadAllLines(PackPath);
        var start = Array.FindIndex(lines, l => l.StartsWith("## 14.", StringComparison.Ordinal));
        var end = Array.FindIndex(lines, start + 1, l => l.StartsWith("## 15.", StringComparison.Ordinal));
        Assert.True(start >= 0 && end > start, "pack §14 not found");
        return lines[start..end];
    });

    private static readonly IModuleManifestProvider Provider = new QuarantineBlockedStockManifestProvider();

    [Fact]
    public void The_pages_are_exactly_the_pack_14_page_table_and_all_are_tenant_routes()
    {
        var packPages = Section14.Value.Select(l => PageRow.Match(l)).Where(m => m.Success)
            .Select(m => $"{m.Groups["code"].Value}|{m.Groups["route"].Value}|{m.Groups["perm"].Value}|{m.Groups["nav"].Value == "true"}|{m.Groups["parent"].Value.Trim('`').Replace("—", "")}")
            .OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.Equal(8, packPages.Count);

        var manifest = Provider.GetManifest();
        Assert.Equal(packPages, manifest.Pages.Select(p => $"{p.PageCode}|{p.RoutePath}|{p.RequiredPermission}|{p.IsNavigationVisible}|{p.ParentPageCode ?? ""}")
            .OrderBy(x => x, StringComparer.Ordinal).ToList());
        Assert.Equal("QUARANTINE-BLOCKED-STOCK", manifest.ModuleCode);
        Assert.True(manifest.IsTenantAssignable);
        // ScopeFromRoute (Platform): "/Platform" or "/Platform/…" → PlatformAdmin; everything else Tenant.
        Assert.All(manifest.Pages, p => Assert.False(
            p.RoutePath.Equals("/Platform", StringComparison.OrdinalIgnoreCase) || p.RoutePath.StartsWith("/Platform/", StringComparison.OrdinalIgnoreCase), p.RoutePath));
        Assert.All(manifest.Pages, p => Assert.StartsWith("/Quarantine", p.RoutePath, StringComparison.Ordinal));
    }

    [Fact]
    public void The_actions_of_every_page_are_exactly_the_pack_14_button_table()
    {
        var expected = Section14.Value.Select(l => ActionRow.Match(l)).Where(m => m.Success)
            .Select(m => $"{PageOfScreen[m.Groups["screen"].Value.Trim()]}|{m.Groups["code"].Value}|{(m.Groups["placement"].Value == "form" ? "Form" : m.Groups["placement"].Value)}|{m.Groups["perm"].Value}|{m.Groups["danger"].Value.Trim().StartsWith("yes", StringComparison.Ordinal)}")
            .Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.True(expected.Count == 17, $"parsed {expected.Count} coded actions from pack §14 (expected the 17 rows with an ActionCode)");

        var actual = Provider.GetManifest().Pages
            .SelectMany(p => p.Actions.Select(a => $"{p.PageCode}|{a.ActionCode}|{a.ActionType}|{a.PermissionKey}|{a.IsDangerous}"))
            .OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Every_key_is_in_the_quarantine_namespace_and_every_enforced_key_is_reachable()
    {
        var manifest = Provider.GetManifest();
        var keys = manifest.Pages.Select(p => p.RequiredPermission)
            .Concat(manifest.Pages.SelectMany(p => p.Actions).Select(a => a.PermissionKey))
            .Concat((manifest.ApiPermissions ?? []).Select(a => a.PermissionKey)).ToList();
        Assert.True(keys.Count >= 20);
        Assert.All(keys, k => Assert.StartsWith("quarantine.", k, StringComparison.Ordinal));
        Assert.Empty(manifest.ApiPermissions ?? []);

        var reachable = keys.ToHashSet(StringComparer.Ordinal);
        Assert.All(QuarantinePermissions.All, k => Assert.Contains(k, reachable));

        var enforced = typeof(QuarantineController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(m => m.GetCustomAttributes<HasPermissionAttribute>()).Select(a => a.Policy!["Permission:".Length..]).ToHashSet(StringComparer.Ordinal);
        Assert.True(enforced.Count >= 6);
        Assert.All(enforced, k => Assert.Contains(k, reachable));
    }

    [Fact]
    public void The_quarantine_routes_are_under_api_quarantine_and_every_action_carries_a_key()
    {
        var route = typeof(QuarantineController).GetCustomAttribute<RouteAttribute>()!.Template;
        Assert.Equal("api/quarantine", route);
        var actions = typeof(QuarantineController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).ToList();
        // createDecision / submitDecision check the kind's key in the action (three possible keys, like TRACE updateLot)
        Assert.All(actions.Where(a => a.Name is not ("CreateDecision" or "SubmitDecision")),
            a => Assert.NotNull(a.GetCustomAttribute<HasPermissionAttribute>()));
    }
}

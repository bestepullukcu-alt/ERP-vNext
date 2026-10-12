using System.Reflection;
using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.SupplyChainService.Api.ModuleRegistration;
using Diten.SupplyChainService.Domain.Features.Returns;
using Diten.SupplyChainService.Infrastructure.Features.Returns;
using Xunit;

namespace Diten.SupplyChainService.Tests.ModuleRegistration;

// MOD-0186 Reverse Logistics — manifest completeness (pack §33 tests M-01…M-08; TEST-PLAN.md §2). DRAFT overlay — not built.
// Asserted in BOTH directions (D5 = A): every manifest key is a reflected ReturnPermissions constant or a non-null value of
// ReturnPermissions.ForTarget over the frozen ReturnStatus enum, and every such key is modeled or allow-listed with a reason.
// The frontend view-route set is fixed here from pack §32.4 and cross-checked on the frontend side by
// Diten.Web.Tests.Controllers.SupplyChainReturnsControllerTests.View_routes_are_exactly_the_manifest_page (and by the
// shared guard W-01 once the integration owner lands it).
public sealed class ReverseLogisticsManifestProviderTests
{
    private static readonly ModuleManifestDocument Manifest = new ReverseLogisticsManifestProvider().GetManifest();

    // Reflected public const string fields of ReturnPermissions (Read, Create, Transition) + every non-null ForTarget value.
    private static readonly HashSet<string> EnforcedPermissions = typeof(ReturnPermissions)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .Concat(Enum.GetNames<ReturnStatus>().Select(ReturnPermissions.ForTarget).OfType<string>())
        .ToHashSet(StringComparer.Ordinal);

    // Keys enforced by the API but deliberately not modeled, each with a reason (pack §33 M-03). Empty since R-2: the one
    // entry, supplychain.returns.transition, never reached Auth (the sync carries only declared keys), so no role could hold
    // a key every transition requires. It is now the CHANGE_STATUS action.
    private static readonly IReadOnlyDictionary<string, string> ApiOnlyAllowList = new Dictionary<string, string>(StringComparer.Ordinal);

    // Frontend view-routes of SupplyChainReturnsController (pack §32.4): the list page only. The api/* adapter routes are
    // JSON endpoints, not pages; there is no Create/Details/Edit page (QuickView and offcanvas only).
    private static readonly HashSet<string> FrontendViewRoutes = new(StringComparer.Ordinal) { "/SupplyChain/Returns" };

    private static readonly (string Code, string Key, string Placement, bool Dangerous, string Name)[] ExpectedActions =
    [
        ("CREATE", "supplychain.returns.create", "Toolbar", false, "Create Return"),
        ("CHANGE_STATUS", "supplychain.returns.transition", "RowAction", false, "Change Return Status"),
        ("AUTHORIZE", "supplychain.returns.authorize", "RowAction", false, "Authorize"),
        ("REJECT", "supplychain.returns.authorize", "RowAction", true, "Reject"),
        ("MARK_IN_TRANSIT", "supplychain.returns.transit", "RowAction", false, "Mark In Transit"),
        ("CANCEL", "supplychain.returns.cancel", "RowAction", true, "Cancel Return"),
        ("RECEIVE", "supplychain.returns.receive", "RowAction", false, "Receive (manual assertion)"),
        ("DISPOSITION", "supplychain.returns.disposition", "RowAction", false, "Disposition"),
        ("CLOSE", "supplychain.returns.close", "RowAction", false, "Close")
    ];

    [Fact] // M-01
    public void M01_Identity_is_exactly_the_pack_values()
    {
        Assert.Equal("reverse-logistics", Manifest.ModuleCode);
        Assert.Equal("ReverseLogistics", Manifest.ModuleName);
        Assert.Equal("Reverse Logistics", Manifest.DisplayName);
        Assert.Equal("SupplyChainExecution", Manifest.Domain);
        Assert.Equal("DitenSupplyChainService", Manifest.Service);
        Assert.Equal("1.0.0", Manifest.ModuleVersion);
        Assert.True(Manifest.IsTenantAssignable);
        Assert.False(Manifest.IsBaseline);
        Assert.Equal(420, Manifest.SortOrder);
        Assert.Equal("bx-undo", Manifest.Icon);
    }

    [Fact] // M-02
    public void M02_Every_manifest_key_is_a_reflected_constant_or_a_ForTarget_value()
    {
        Assert.Equal(9, EnforcedPermissions.Count); // 3 constants + 6 target keys
        foreach (var page in Manifest.Pages)
        {
            Assert.Contains(page.RequiredPermission, EnforcedPermissions);
            Assert.All(page.Actions, action => Assert.Contains(action.PermissionKey, EnforcedPermissions));
        }
    }

    [Fact] // M-03
    public void M03_Every_reflected_key_is_modeled_or_allow_listed_with_a_reason()
    {
        var modeled = Manifest.Pages
            .SelectMany(p => p.Actions.Select(a => a.PermissionKey).Append(p.RequiredPermission))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var key in EnforcedPermissions)
        {
            Assert.True(modeled.Contains(key) || ApiOnlyAllowList.ContainsKey(key),
                $"'{key}' is enforced by the API but neither modeled nor allow-listed with a reason.");
        }

        Assert.All(ApiOnlyAllowList, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Value)));
        Assert.Empty(ApiOnlyAllowList);
    }

    [Fact] // M-04
    public void M04_Manifest_routes_equal_the_frontend_view_routes_both_directions()
    {
        var manifestRoutes = Manifest.Pages.Select(p => p.RoutePath).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(FrontendViewRoutes.Count, Manifest.Pages.Count);
        Assert.True(manifestRoutes.SetEquals(FrontendViewRoutes),
            "Manifest pages must mirror the Returns frontend view-routes exactly (both directions).");
    }

    [Fact] // M-05
    public void M05_Actions_equal_the_pack_action_table()
    {
        var page = Assert.Single(Manifest.Pages);
        Assert.Equal("RETURNS", page.PageCode);
        Assert.Equal("Returns", page.DisplayName);
        Assert.Equal("supplychain.returns.read", page.RequiredPermission);
        Assert.Equal("List", page.PageType);
        Assert.Equal(10, page.SortOrder);
        Assert.Equal(ExpectedActions.Length, page.Actions.Count);
        foreach (var expected in ExpectedActions)
        {
            var action = Assert.Single(page.Actions, a => a.ActionCode == expected.Code);
            Assert.Equal(expected.Key, action.PermissionKey);
            Assert.Equal(expected.Placement, action.ActionType);
            Assert.Equal(expected.Dangerous, action.IsDangerous);
            Assert.Equal(expected.Name, action.DisplayName);
            Assert.Equal(expected.Placement == "Toolbar", action.IsToolbarAction);
            Assert.Equal(expected.Placement == "RowAction", action.IsRowAction);
        }
    }

    [Fact] // M-06
    public void M06_Page_codes_routes_and_action_codes_are_unique_case_insensitive()
    {
        AssertUnique(Manifest.Pages.Select(p => p.PageCode));
        AssertUnique(Manifest.Pages.Select(p => p.RoutePath));
        foreach (var page in Manifest.Pages)
        {
            AssertUnique(page.Actions.Select(a => a.ActionCode));
        }
    }

    [Fact] // M-07
    public void M07_Exactly_one_nav_visible_page_with_null_parent_and_others_parented()
    {
        var nav = Assert.Single(Manifest.Pages, p => p.IsNavigationVisible);
        Assert.Equal("RETURNS", nav.PageCode);
        Assert.Null(nav.ParentPageCode);
        Assert.All(Manifest.Pages.Where(p => !p.IsNavigationVisible), p => Assert.NotNull(p.ParentPageCode));
    }

    [Fact] // M-08
    public void M08_Tenant_scope_every_route_under_SupplyChain_and_none_under_Platform()
    {
        Assert.All(Manifest.Pages, p =>
        {
            Assert.StartsWith("/SupplyChain/", p.RoutePath, StringComparison.Ordinal);
            Assert.False(p.RoutePath.StartsWith("/Platform/", StringComparison.OrdinalIgnoreCase));
        });
    }

    // R-2: Platform syncs to Auth only the keys a manifest declares (Q353 §A; R-2 measured supplychain.returns.transition
    // absent from Auth while every transition required it). An enforced key that is not declared can never be granted.
    [Fact]
    public void Every_enforced_key_is_declared_so_the_permission_sync_can_carry_it()
    {
        var page = Assert.Single(new ReverseLogisticsManifestProvider().GetManifest().Pages);
        var declared = page.Actions.Select(a => a.PermissionKey).Append(page.RequiredPermission).ToHashSet(StringComparer.Ordinal);
        Assert.All(EnforcedPermissions, key => Assert.True(declared.Contains(key), $"'{key}' is enforced but never declared, so no role can hold it."));
    }

    [Fact]
    public void Key_grammar_is_lowercase_with_at_least_three_segments()
    {
        Assert.All(EnforcedPermissions, key =>
        {
            Assert.Equal(key.ToLowerInvariant(), key);
            Assert.True(key.Split('.').Length >= 3, $"'{key}' must have >= 3 segments.");
            Assert.StartsWith("supplychain.returns.", key, StringComparison.Ordinal);
        });
    }

    private static void AssertUnique(IEnumerable<string> values)
    {
        var list = values.ToList();
        Assert.Equal(list.Count, list.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}

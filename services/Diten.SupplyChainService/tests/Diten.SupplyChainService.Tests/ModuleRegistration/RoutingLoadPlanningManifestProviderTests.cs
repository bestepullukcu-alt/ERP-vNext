using System.Reflection;
using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.SupplyChainService.Api.ModuleRegistration;
using Diten.SupplyChainService.Infrastructure.Features.Loads;
using Xunit;

namespace Diten.SupplyChainService.Tests.ModuleRegistration;

// MOD-0185 Routing & Load Planning — manifest completeness (pack §29 tests M-01…M-08), written by R-4b with the Loads UI.
// Both directions: every manifest key is a reflected LoadPermissions constant, and every constant is declared. The
// transition key is declared (CHANGE_STATUS) rather than allow-listed: an enforced key that is not declared can never be
// granted (R-2 F-R2-1; R-3 guard).
public sealed class RoutingLoadPlanningManifestProviderTests
{
    private static readonly ModuleManifestDocument Manifest = new RoutingLoadPlanningManifestProvider().GetManifest();

    private static readonly HashSet<string> EnforcedPermissions = typeof(LoadPermissions)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToHashSet(StringComparer.Ordinal);

    // The frontend view-route set of SupplyChainLoadsController: the list page only (create is an offcanvas, api/* are JSON).
    private static readonly HashSet<string> FrontendViewRoutes = new(StringComparer.Ordinal) { "/SupplyChain/Loads" };

    private static readonly (string Code, string Key, string Placement, bool Dangerous, string Name)[] ExpectedActions =
    [
        ("CREATE", "supplychain.loads.create", "Toolbar", false, "Create"),
        ("CHANGE_STATUS", "supplychain.loads.transition", "RowAction", false, "Change Load Status")
    ];

    [Fact] // M-01
    public void M01_Identity_is_exactly_the_pack_values()
    {
        Assert.Equal("routing-load-planning", Manifest.ModuleCode);
        Assert.Equal("RoutingLoadPlanning", Manifest.ModuleName);
        Assert.Equal("Routing & Load Planning", Manifest.DisplayName);
        Assert.Equal("SupplyChainExecution", Manifest.Domain);
        Assert.Equal("DitenSupplyChainService", Manifest.Service);
        Assert.Equal("1.0.0", Manifest.ModuleVersion);
        Assert.True(Manifest.IsTenantAssignable);
        Assert.False(Manifest.IsBaseline);
        Assert.Equal(410, Manifest.SortOrder);
        Assert.Equal("bx-map-alt", Manifest.Icon);
    }

    [Fact] // M-02
    public void M02_Every_manifest_key_is_a_reflected_constant()
    {
        Assert.Equal(3, EnforcedPermissions.Count);
        foreach (var page in Manifest.Pages)
        {
            Assert.Contains(page.RequiredPermission, EnforcedPermissions);
            Assert.All(page.Actions, action => Assert.Contains(action.PermissionKey, EnforcedPermissions));
        }
    }

    [Fact] // M-03
    public void M03_Every_reflected_key_is_declared()
    {
        var declared = Manifest.Pages.SelectMany(p => p.Actions.Select(a => a.PermissionKey).Append(p.RequiredPermission))
            .ToHashSet(StringComparer.Ordinal);
        Assert.All(EnforcedPermissions, key => Assert.True(declared.Contains(key), $"'{key}' is enforced but never declared, so no role can hold it."));
    }

    [Fact] // M-04
    public void M04_Manifest_routes_equal_the_frontend_view_routes_both_directions()
    {
        Assert.Equal(FrontendViewRoutes.Count, Manifest.Pages.Count);
        Assert.True(Manifest.Pages.Select(p => p.RoutePath).ToHashSet(StringComparer.Ordinal).SetEquals(FrontendViewRoutes));
    }

    [Fact] // M-05
    public void M05_Actions_equal_the_pack_action_table()
    {
        var page = Assert.Single(Manifest.Pages);
        Assert.Equal("LOADS", page.PageCode);
        Assert.Equal("Loads", page.DisplayName);
        Assert.Equal("supplychain.loads.read", page.RequiredPermission);
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
        foreach (var page in Manifest.Pages) AssertUnique(page.Actions.Select(a => a.ActionCode));
    }

    [Fact] // M-07
    public void M07_Exactly_one_nav_visible_page_with_null_parent_and_others_parented()
    {
        var nav = Assert.Single(Manifest.Pages, p => p.IsNavigationVisible);
        Assert.Equal("LOADS", nav.PageCode);
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

    private static void AssertUnique(IEnumerable<string> values)
    {
        var list = values.ToList();
        Assert.Equal(list.Count, list.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}

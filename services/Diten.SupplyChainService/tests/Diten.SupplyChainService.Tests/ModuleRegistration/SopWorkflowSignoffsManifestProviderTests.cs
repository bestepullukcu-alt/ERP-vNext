using System.Reflection;
using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.SupplyChainService.Api.ModuleRegistration;
using Diten.SupplyChainService.Infrastructure.Features.SandopPlans;
using Xunit;

namespace Diten.SupplyChainService.Tests.ModuleRegistration;

// MOD-0190 S&OP — manifest completeness (pack §24 via mvp6-ui-pack-patch-190-192-01, tests M-01…M-08). G6, 2026-10-18.
// Asserted in BOTH directions: every manifest key is a real SandopPermissions constant, and every constant is modeled.
// The frontend view-route set is fixed here from UI-SCOPE-0190 §3 and cross-checked on the frontend side by
// Diten.Web.Tests.Controllers.SupplyChainSandopPlansControllerTests.View_routes_are_exactly_the_manifest_pages.
public sealed class SopWorkflowSignoffsManifestProviderTests
{
    private static readonly ModuleManifestDocument Manifest = new SopWorkflowSignoffsManifestProvider().GetManifest();

    private static readonly HashSet<string> EnforcedPermissions = typeof(SandopPermissions)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToHashSet(StringComparer.Ordinal);

    private static readonly HashSet<string> FrontendViewRoutes = new(StringComparer.Ordinal)
    {
        "/SupplyChain/SandopPlans", "/SupplyChain/SandopPlans/Details/{sandopPlanId:guid}"
    };

    private static readonly (string Page, string Code, string Key, string Placement, bool Dangerous)[] ExpectedActions =
    [
        ("SANDOP_PLANS", "CREATE", "supplychain.sandop-plans.create", "Toolbar", false),
        ("SANDOP_PLAN_DETAILS", "CAPTURE_SNAPSHOT", "supplychain.sandop-plans.snapshot.capture", "Toolbar", false),
        ("SANDOP_PLAN_DETAILS", "RECORD_SIGN_OFF", "supplychain.sandop-plans.sign-off.record", "Toolbar", false)
    ];

    [Fact] // M-01
    public void M01_Identity_is_exactly_the_pack_values()
    {
        Assert.Equal("sop-workflow-signoffs", Manifest.ModuleCode);
        Assert.Equal("SopWorkflowSignoffs", Manifest.ModuleName);
        Assert.Equal("S&OP Workflow & Sign-offs", Manifest.DisplayName);
        Assert.Equal("SupplyChainExecution", Manifest.Domain);
        Assert.Equal("DitenSupplyChainService", Manifest.Service);
        Assert.Equal("1.0.0", Manifest.ModuleVersion);
        Assert.True(Manifest.IsTenantAssignable);
        Assert.False(Manifest.IsBaseline);
        Assert.Equal(440, Manifest.SortOrder);
        Assert.Equal("bx-check-double", Manifest.Icon);
    }

    [Fact] // M-02
    public void M02_Every_manifest_key_is_a_reflected_SandopPermissions_constant()
    {
        Assert.Equal(4, EnforcedPermissions.Count);
        foreach (var page in Manifest.Pages)
        {
            Assert.Contains(page.RequiredPermission, EnforcedPermissions);
            Assert.All(page.Actions, action => Assert.Contains(action.PermissionKey, EnforcedPermissions));
        }
    }

    [Fact] // M-03 — none on an API-only allow-list: all four constants are modeled
    public void M03_Every_reflected_key_is_modeled()
    {
        var modeled = Manifest.Pages
            .SelectMany(p => p.Actions.Select(a => a.PermissionKey).Append(p.RequiredPermission))
            .ToHashSet(StringComparer.Ordinal);
        Assert.All(EnforcedPermissions, key => Assert.True(modeled.Contains(key), $"'{key}' is enforced by the API but not modeled."));
    }

    [Fact] // M-04
    public void M04_Manifest_routes_equal_the_frontend_view_routes_both_directions()
    {
        Assert.Equal(FrontendViewRoutes.Count, Manifest.Pages.Count);
        Assert.True(Manifest.Pages.Select(p => p.RoutePath).ToHashSet(StringComparer.Ordinal).SetEquals(FrontendViewRoutes));
    }

    [Fact] // M-05
    public void M05_Pages_and_actions_equal_the_pack_tables()
    {
        var entry = Assert.Single(Manifest.Pages, p => p.PageCode == "SANDOP_PLANS");
        Assert.Equal(("S&OP Plans", "List", 10, "supplychain.sandop-plans.read"), (entry.DisplayName, entry.PageType, entry.SortOrder, entry.RequiredPermission));
        var detail = Assert.Single(Manifest.Pages, p => p.PageCode == "SANDOP_PLAN_DETAILS");
        Assert.Equal(("S&OP Plan", "Detail", 11, "SANDOP_PLANS"), (detail.DisplayName, detail.PageType, detail.SortOrder, detail.ParentPageCode));
        Assert.Equal(ExpectedActions.Length, Manifest.Pages.Sum(p => p.Actions.Count));
        foreach (var expected in ExpectedActions)
        {
            var page = Assert.Single(Manifest.Pages, p => p.PageCode == expected.Page);
            var action = Assert.Single(page.Actions, a => a.ActionCode == expected.Code);
            Assert.Equal(expected.Key, action.PermissionKey);
            Assert.Equal(expected.Placement, action.ActionType);
            Assert.Equal(expected.Dangerous, action.IsDangerous);
            Assert.True(action.IsToolbarAction);
            Assert.False(action.IsRowAction);
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

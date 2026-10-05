using System.Reflection;
using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.SupplyChainService.Api.ModuleRegistration;
using Diten.SupplyChainService.Infrastructure.Features.Claims;
using Xunit;

namespace Diten.SupplyChainService.Tests.ModuleRegistration;

// MOD-0187 Claims — manifest completeness (pack §33 tests M-01…M-08; TEST-PLAN.md §2). Built and run by R-4c.
// Asserted in BOTH directions: every manifest key is a real ClaimPermissions constant, and every constant is modeled.
// The frontend view-route set is fixed here from pack §32.4 and cross-checked on the frontend side by
// Diten.Web.Tests.Controllers.SupplyChainClaimsControllerTests.View_routes_are_exactly_the_manifest_page (and by the
// shared guard W-01 once the integration owner lands it).
public sealed class ClaimsManagementManifestProviderTests
{
    private static readonly ModuleManifestDocument Manifest = new ClaimsManagementManifestProvider().GetManifest();

    // Reflected public const string fields of ClaimPermissions (Read, Create, Investigate, Decide, Settle).
    private static readonly HashSet<string> EnforcedPermissions = typeof(ClaimPermissions)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToHashSet(StringComparer.Ordinal);

    // Keys enforced by the API but deliberately not modeled as a catalog page/action, each with a reason. None expected.
    private static readonly IReadOnlyDictionary<string, string> ApiOnlyAllowList = new Dictionary<string, string>(StringComparer.Ordinal);

    // Frontend view-routes of SupplyChainClaimsController (pack §32.4): the list page only. The api/* adapter routes are
    // JSON endpoints, not pages; there is no Create/Details/Edit page (QuickView and offcanvas only).
    private static readonly HashSet<string> FrontendViewRoutes = new(StringComparer.Ordinal) { "/SupplyChain/Claims" };

    private static readonly (string Code, string Key, string Placement, bool Dangerous)[] ExpectedActions =
    [
        ("CREATE", "supplychain.claims.create", "Toolbar", false),
        ("INVESTIGATE", "supplychain.claims.investigate", "RowAction", false),
        ("WITHDRAW", "supplychain.claims.investigate", "RowAction", true),
        ("APPROVE", "supplychain.claims.decide", "RowAction", false),
        ("REJECT", "supplychain.claims.decide", "RowAction", true),
        ("SETTLE", "supplychain.claims.settle", "RowAction", false),
        ("CLOSE", "supplychain.claims.decide", "RowAction", false)
    ];

    [Fact] // M-01
    public void M01_Identity_is_exactly_the_pack_values()
    {
        Assert.Equal("claims-management", Manifest.ModuleCode);
        Assert.Equal("ClaimsManagement", Manifest.ModuleName);
        Assert.Equal("Claims Management", Manifest.DisplayName);
        Assert.Equal("SupplyChainExecution", Manifest.Domain);
        Assert.Equal("DitenSupplyChainService", Manifest.Service);
        Assert.Equal("1.0.0", Manifest.ModuleVersion);
        Assert.True(Manifest.IsTenantAssignable);
        Assert.False(Manifest.IsBaseline);
        Assert.Equal(430, Manifest.SortOrder);
        Assert.Equal("bx-receipt", Manifest.Icon);
    }

    [Fact] // M-02
    public void M02_Every_manifest_key_is_a_reflected_ClaimPermissions_constant()
    {
        Assert.Equal(5, EnforcedPermissions.Count);
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
        Assert.Empty(ApiOnlyAllowList); // pack §33: none expected
    }

    [Fact] // M-04
    public void M04_Manifest_routes_equal_the_frontend_view_routes_both_directions()
    {
        var manifestRoutes = Manifest.Pages.Select(p => p.RoutePath).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(FrontendViewRoutes.Count, Manifest.Pages.Count);
        Assert.True(manifestRoutes.SetEquals(FrontendViewRoutes),
            "Manifest pages must mirror the Claims frontend view-routes exactly (both directions).");
    }

    [Fact] // M-05
    public void M05_Actions_equal_the_pack_action_table()
    {
        var page = Assert.Single(Manifest.Pages);
        Assert.Equal("CLAIMS", page.PageCode);
        Assert.Equal("Claims", page.DisplayName);
        Assert.Equal("supplychain.claims.read", page.RequiredPermission);
        Assert.Equal("List", page.PageType);
        Assert.Equal(10, page.SortOrder);
        Assert.Equal(ExpectedActions.Length, page.Actions.Count);
        foreach (var expected in ExpectedActions)
        {
            var action = Assert.Single(page.Actions, a => a.ActionCode == expected.Code);
            Assert.Equal(expected.Key, action.PermissionKey);
            Assert.Equal(expected.Placement, action.ActionType);
            Assert.Equal(expected.Dangerous, action.IsDangerous);
            Assert.Equal(expected.Placement == "Toolbar", action.IsToolbarAction);
            Assert.Equal(expected.Placement == "RowAction", action.IsRowAction);
        }

        Assert.Equal("Settle (no payment posted)", Assert.Single(page.Actions, a => a.ActionCode == "SETTLE").DisplayName);
        Assert.Equal("Create Claim", Assert.Single(page.Actions, a => a.ActionCode == "CREATE").DisplayName);
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

    [Fact]
    public void Key_grammar_is_lowercase_with_at_least_three_segments()
    {
        Assert.All(EnforcedPermissions, key =>
        {
            Assert.Equal(key.ToLowerInvariant(), key);
            Assert.True(key.Split('.').Length >= 3, $"'{key}' must have >= 3 segments.");
            Assert.StartsWith("supplychain.claims.", key, StringComparison.Ordinal);
        });
    }

    private static void AssertUnique(IEnumerable<string> values)
    {
        var list = values.ToList();
        Assert.Equal(list.Count, list.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}

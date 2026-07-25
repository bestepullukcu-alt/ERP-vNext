using System.Reflection;
using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.SelfRegistration;
using Xunit;

namespace Diten.Platform.Application.Tests.WorkAggregation;

// WC-1b (DCP-004 §8 row 1b) — the Görev Merkezi manifest must mirror the REAL tenant route and carry the verbatim
// WorkAggregationPermissions constant the WC-1 controller enforces. Asserted in both directions so a missing/extra
// page or a drifted permission breaks the build. The permission oracle is reflected off the constants class, so a
// hand-typed string literal in the manifest fails here.
public sealed class WorkAggregationManifestProviderTests
{
    private static readonly ModuleManifestDocument Manifest = new WorkAggregationManifestProvider().GetManifest();

    // The real tenant route, with the exact permission it is gated on.
    private static readonly Dictionary<string, string> ExpectedRoutePermissions = new(StringComparer.Ordinal)
    {
        ["/WorkCenterNext"] = "platform.work-aggregation.inbox.view"
    };

    // Zero-drift oracle: every permission constant declared by WC-1.
    private static readonly HashSet<string> KnownPermissionKeys = typeof(WorkAggregationPermissions)
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
        .Where(f => f is { IsLiteral: true, IsInitOnly: false } && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void Declares_a_clean_slug_entitlement_gated_module_identity()
    {
        Assert.Equal("work-aggregation", Manifest.ModuleCode);
        Assert.Equal("Work Aggregation", Manifest.ModuleName);
        Assert.Equal("Görev Merkezi / Task Center", Manifest.DisplayName);
        Assert.Equal("Workspace", Manifest.Domain);       // pack DEC-5
        Assert.Equal("DitenPlatform", Manifest.Service);
        Assert.True(Manifest.IsTenantAssignable);
        Assert.False(Manifest.IsBaseline);                // pack DEC-4 — entitlement-gated, NOT baseline
        Assert.Equal("bx-been-here", Manifest.Icon);
        Assert.Equal(10, Manifest.SortOrder);
        Assert.NotEmpty(Manifest.Pages);
    }

    [Fact]
    public void Governance_identity_never_leaks_into_the_manifest()
    {
        // The governance identity is documentation-only; it must never appear in runtime values.
        var values = new[] { Manifest.ModuleCode, Manifest.ModuleName, Manifest.DisplayName, Manifest.Domain, Manifest.Service }
            .Concat(Manifest.Pages.Select(p => p.PageCode))
            .Concat(Manifest.Pages.Select(p => p.RoutePath));

        Assert.All(values, v =>
        {
            Assert.DoesNotContain("CAND-CAP", v, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("MOD-", v, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Pages_mirror_the_tenant_route_and_permission_exactly_both_directions()
    {
        Assert.Equal(ExpectedRoutePermissions.Count, Manifest.Pages.Count);
        var manifestRoutes = Manifest.Pages.Select(p => p.RoutePath).ToHashSet(StringComparer.Ordinal);
        Assert.True(manifestRoutes.SetEquals(ExpectedRoutePermissions.Keys),
            "Manifest pages must mirror the real tenant routes exactly (both directions).");

        foreach (var page in Manifest.Pages)
        {
            Assert.Equal(ExpectedRoutePermissions[page.RoutePath], page.RequiredPermission);
        }
    }

    [Fact]
    public void Every_declared_permission_is_a_real_WorkAggregationPermissions_constant()
    {
        Assert.NotEmpty(KnownPermissionKeys);

        foreach (var page in Manifest.Pages)
        {
            Assert.Contains(page.RequiredPermission, KnownPermissionKeys);
        }

        foreach (var action in Manifest.Pages.SelectMany(p => p.Actions))
        {
            Assert.Contains(action.PermissionKey, KnownPermissionKeys);
        }
    }

    [Fact]
    public void Page_is_a_visible_top_level_nav_entry_with_no_commands()
    {
        var page = Assert.Single(Manifest.Pages);
        Assert.Equal("WORKCENTER", page.PageCode);
        Assert.True(page.IsNavigationVisible);
        Assert.Null(page.ParentPageCode);
        Assert.Equal("List", page.PageType);
        // Read-only slice: approve/reject/delegate stay on the MOD-0023 endpoints, so no actions are projected.
        Assert.Empty(page.Actions);
    }

    [Fact]
    public void Page_codes_and_routes_are_unique_so_the_reconcile_cannot_skip_one()
    {
        var codes = Manifest.Pages.Select(p => p.PageCode).ToList();
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        var routes = Manifest.Pages.Select(p => p.RoutePath).ToList();
        Assert.Equal(routes.Count, routes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}

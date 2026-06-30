using System.Reflection;
using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.API.Controllers.Platform;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.Organization.SelfRegistration;
using Xunit;

namespace Diten.Platform.Application.Tests.Organization;

// MC-3b-expand — the organization manifest must mirror the real OrganizationUnits/Positions/PositionAssignments
// frontend slim pages and the verbatim platform.* permission keys the API controllers enforce (zero drift),
// asserted in BOTH directions (§3) so a missing/extra page or a fabricated permission breaks the build.
public sealed class OrganizationManifestProviderTests
{
    private static readonly ModuleManifestDocument Manifest = new OrganizationManifestProvider().GetManifest();

    // The REAL enforced permission keys, reflected off the API controllers' [HasPermission] attributes (the
    // single source of truth) — so the manifest cannot drift from what the backend actually gates.
    private static readonly HashSet<string> EnforcedPermissions = ReflectEnforcedPermissions(
        typeof(OrganizationUnitsController),
        typeof(PositionsController),
        typeof(PositionAssignmentsController));

    // The real frontend view-routes (Diten.Web). Hard-coded because the Platform test assembly cannot reference
    // the frontend project; a dropped/added route breaks the SetEquals below (both directions).
    private static readonly HashSet<string> FrontendViewRoutes = new(StringComparer.Ordinal)
    {
        "/OrganizationUnits",
        "/Positions",
        "/PositionAssignments"
    };

    [Fact]
    public void Declares_clean_slug_module_identity()
    {
        Assert.Equal("organization", Manifest.ModuleCode);
        Assert.Equal("PlatformSharedServices", Manifest.Domain);
        Assert.Equal("DitenPlatform", Manifest.Service);
        Assert.True(Manifest.IsTenantAssignable);
        Assert.NotEmpty(Manifest.Pages);
    }

    [Fact]
    public void Every_permission_is_a_real_enforced_key()
    {
        Assert.NotEmpty(EnforcedPermissions);
        foreach (var page in Manifest.Pages)
        {
            Assert.Contains(page.RequiredPermission, EnforcedPermissions);
            foreach (var action in page.Actions)
            {
                Assert.Contains(action.PermissionKey, EnforcedPermissions);
            }
        }
    }

    [Fact]
    public void Page_routes_and_codes_and_action_codes_are_unique()
    {
        AssertUnique(Manifest.Pages.Select(p => p.RoutePath));
        AssertUnique(Manifest.Pages.Select(p => p.PageCode));
        foreach (var page in Manifest.Pages)
        {
            AssertUnique(page.Actions.Select(a => a.ActionCode));
        }
    }

    [Fact]
    public void Every_frontend_view_route_has_exactly_one_manifest_page_and_vice_versa()
    {
        var manifestRoutes = Manifest.Pages.Select(p => p.RoutePath).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(FrontendViewRoutes.Count, Manifest.Pages.Count);
        Assert.True(manifestRoutes.SetEquals(FrontendViewRoutes),
            "Manifest pages must mirror the frontend view-routes exactly (both directions).");
    }

    [Fact]
    public void All_three_pages_are_co_equal_top_level_nav_entries()
    {
        Assert.All(Manifest.Pages, p => Assert.True(p.IsNavigationVisible));
        Assert.All(Manifest.Pages, p => Assert.Null(p.ParentPageCode));
    }

    [Fact]
    public void Position_assignments_has_no_archive_action()
    {
        var assignments = Assert.Single(Manifest.Pages, p => p.PageCode == "POSITION_ASSIGNMENTS");
        Assert.DoesNotContain(assignments.Actions, a => a.ActionCode == "ARCHIVE");
        // The sibling lists DO expose archive (proving the difference is intentional, not an omission).
        var units = Assert.Single(Manifest.Pages, p => p.PageCode == "ORGANIZATION_UNITS");
        Assert.Contains(units.Actions, a => a.ActionCode == "ARCHIVE");
    }

    private static void AssertUnique(IEnumerable<string> values)
    {
        var list = values.ToList();
        Assert.Equal(list.Count, list.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    private static HashSet<string> ReflectEnforcedPermissions(params Type[] controllers) =>
        controllers
            .SelectMany(t => t.GetCustomAttributes<HasPermissionAttribute>()
                .Concat(t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .SelectMany(m => m.GetCustomAttributes<HasPermissionAttribute>())))
            .Select(a => a.Permission)
            .ToHashSet(StringComparer.Ordinal);
}

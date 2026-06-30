using System.Reflection;
using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.DevEnablementService.Api.Controllers;
using Diten.DevEnablementService.Api.ModuleRegistration;
using Diten.DevEnablementService.Infrastructure.Authorization;
using Xunit;

namespace Diten.DevEnablementService.Api.Tests.ModuleRegistration;

// Golden Compact — the manifest must mirror the real GoldenReferenceCompactController frontend view-routes and the
// verbatim goldencompact.* keys the API controller enforces (zero drift), asserted in BOTH directions (§3).
public sealed class GoldenCompactManifestProviderTests
{
    private static readonly ModuleManifestDocument Manifest = new GoldenCompactManifestProvider().GetManifest();

    // Real enforced permission keys, reflected off the API controller's [HasPermission] policies ("Permission:<key>").
    private static readonly HashSet<string> EnforcedPermissions =
        ReflectEnforcedPermissions(typeof(GoldenReferenceCompactController));

    // Real frontend view-routes (Diten.Web GoldenReferenceCompactController) — List + Create + Edit + Details.
    private static readonly HashSet<string> FrontendViewRoutes = new(StringComparer.Ordinal)
    {
        "/GoldenReferenceCompact",
        "/GoldenReferenceCompact/Create",
        "/GoldenReferenceCompact/Edit/{id}",
        "/GoldenReferenceCompact/Details/{id}"
    };

    [Fact]
    public void Declares_clean_slug_module_identity()
    {
        Assert.Equal("goldencompact", Manifest.ModuleCode);
        Assert.Equal("Golden Compact", Manifest.DisplayName);
        Assert.Equal("DevEnablement", Manifest.Domain);
        Assert.Equal("DevEnablement", Manifest.Service);
        Assert.True(Manifest.IsTenantAssignable);
    }

    [Fact]
    public void Every_manifest_permission_is_a_real_enforced_key()
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
    public void Controller_enforces_the_full_goldencompact_namespace()
    {
        // The task's declared namespace: CRUD (parallel to GoldenSlim) + export + reports.view.
        foreach (var key in new[]
                 {
                     "goldencompact.records.read",
                     "goldencompact.records.create",
                     "goldencompact.records.update",
                     "goldencompact.records.delete",
                     "goldencompact.records.export",
                     "goldencompact.reports.view"
                 })
        {
            Assert.Contains(key, EnforcedPermissions);
        }

        // Grammar: lowercase, >= 3 dot-segments.
        Assert.All(EnforcedPermissions, key =>
        {
            Assert.Equal(key.ToLowerInvariant(), key);
            Assert.True(key.Split('.').Length >= 3, $"'{key}' must have >= 3 segments.");
        });
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
    public void Records_is_the_single_top_level_nav_entry_with_export_and_delete()
    {
        var records = Assert.Single(Manifest.Pages, p => p.IsNavigationVisible);
        Assert.Equal("RECORDS", records.PageCode);
        Assert.Equal("/GoldenReferenceCompact", records.RoutePath);
        Assert.Null(records.ParentPageCode);

        var keys = records.Actions.Select(a => a.PermissionKey).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("goldencompact.records.export", keys);
        Assert.Contains("goldencompact.records.delete", keys);

        Assert.All(Manifest.Pages.Where(p => !p.IsNavigationVisible), p => Assert.NotNull(p.ParentPageCode));
    }

    [Fact]
    public void Reports_view_is_api_only_and_not_a_catalog_page()
    {
        // reports.view is a real enforced permission (above) but has no frontend route/button yet, so — per the
        // self-registration standard — it is NOT modeled as a catalog page/action.
        Assert.Contains("goldencompact.reports.view", EnforcedPermissions);
        var manifestKeys = Manifest.Pages
            .SelectMany(p => p.Actions.Select(a => a.PermissionKey).Append(p.RequiredPermission))
            .ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain("goldencompact.reports.view", manifestKeys);
    }

    private static void AssertUnique(IEnumerable<string> values)
    {
        var list = values.ToList();
        Assert.Equal(list.Count, list.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    // DevEnablement's HasPermissionAttribute derives from AuthorizeAttribute and stores the key as
    // Policy = "Permission:<key>". Read Policy via reflection (no compile-time AuthorizeAttribute dependency).
    private static HashSet<string> ReflectEnforcedPermissions(Type controller)
    {
        const string prefix = "Permission:";
        var policyProp = typeof(HasPermissionAttribute).GetProperty("Policy");
        return controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(m => m.GetCustomAttributes<HasPermissionAttribute>())
            .Select(a => policyProp?.GetValue(a) as string ?? string.Empty)
            .Where(p => p.StartsWith(prefix, StringComparison.Ordinal))
            .Select(p => p[prefix.Length..])
            .ToHashSet(StringComparer.Ordinal);
    }
}

using System.Reflection;
using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.MdmService.Api.Controllers;
using Diten.MdmService.Api.ModuleRegistration;
using Diten.MdmService.Infrastructure.Authorization;
using Xunit;

namespace Diten.MdmService.Application.Tests.ModuleRegistration;

// MC-3b-expand (Part B) — the legal-entity manifest must mirror the real LegalEntitiesController frontend view-routes
// and the verbatim mdm.legal-entities.* keys the MDM API enforces (zero drift), asserted in BOTH directions (§3).
public sealed class LegalEntityManifestProviderTests
{
    private static readonly ModuleManifestDocument Manifest = new LegalEntityManifestProvider().GetManifest();

    // Real enforced permission keys, reflected off the API controller's [HasPermission] policies
    // ("Permission:<key>") — the single source of truth, so the manifest cannot drift from what the backend gates.
    private static readonly HashSet<string> EnforcedPermissions =
        ReflectEnforcedPermissions(typeof(LegalEntitiesController));

    // Real frontend view-routes (Diten.Web LegalEntitiesController). §2a: the 8-step wizard is one page per route
    // (steps are presentation); create and edit are distinct routes, so two wizard pages.
    private static readonly HashSet<string> FrontendViewRoutes = new(StringComparer.Ordinal)
    {
        "/LegalEntities",
        "/LegalEntities/Wizard",
        "/LegalEntities/Wizard/{id}",
        "/LegalEntities/Details/{id}"
    };

    [Fact]
    public void Declares_clean_slug_module_identity()
    {
        Assert.Equal("legal-entity", Manifest.ModuleCode);
        Assert.Equal("MasterDataManagement", Manifest.Domain);
        Assert.Equal("DitenMdmService", Manifest.Service);
        Assert.True(Manifest.IsTenantAssignable);
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
    public void List_is_the_single_top_level_nav_entry_at_the_real_route()
    {
        var list = Assert.Single(Manifest.Pages, p => p.IsNavigationVisible);
        Assert.Equal("LEGAL_ENTITIES", list.PageCode);
        Assert.Equal("/LegalEntities", list.RoutePath);
        Assert.Null(list.ParentPageCode);
        Assert.All(Manifest.Pages.Where(p => !p.IsNavigationVisible), p => Assert.NotNull(p.ParentPageCode));
    }

    [Fact]
    public void Lifecycle_actions_gate_on_the_real_update_permission()
    {
        var list = Assert.Single(Manifest.Pages, p => p.PageCode == "LEGAL_ENTITIES");
        foreach (var code in new[] { "ACTIVATE", "SUSPEND", "ARCHIVE" })
        {
            var action = Assert.Single(list.Actions, a => a.ActionCode == code);
            Assert.Equal("mdm.legal-entities.update", action.PermissionKey);
        }
        // Delete uses the distinct delete permission.
        var delete = Assert.Single(list.Actions, a => a.ActionCode == "DELETE");
        Assert.Equal("mdm.legal-entities.delete", delete.PermissionKey);
    }

    private static void AssertUnique(IEnumerable<string> values)
    {
        var list = values.ToList();
        Assert.Equal(list.Count, list.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    // MDM's HasPermissionAttribute derives from AuthorizeAttribute and stores the key as Policy = "Permission:<key>".
    // Read Policy via reflection (no compile-time dependency on AuthorizeAttribute) and strip the prefix.
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

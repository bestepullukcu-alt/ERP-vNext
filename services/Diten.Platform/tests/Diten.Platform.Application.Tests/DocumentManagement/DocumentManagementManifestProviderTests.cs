using System.Reflection;
using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.DocumentManagement.SelfRegistration;
using Diten.Platform.Application.Features.DocumentManagementInstantiation;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

// MC-3b-expand — the document-management manifest must mirror the real QmsBaselines/Instantiations frontend
// view-routes and the verbatim platform.document-management.* keys the API controllers enforce (zero drift),
// asserted in BOTH directions (§3).
public sealed class DocumentManagementManifestProviderTests
{
    private static readonly ModuleManifestDocument Manifest = new DocumentManagementManifestProvider().GetManifest();

    private static readonly HashSet<string> EnforcedPermissions = ReflectEnforcedPermissions(
        typeof(DocumentManagementController),
        typeof(DocumentManagementQmsBaselinesController),
        typeof(DocumentManagementInstantiationsController));

    // Real frontend view-routes (Diten.Web QmsBaselines + Instantiations controllers), hard-coded.
    private static readonly HashSet<string> FrontendViewRoutes = new(StringComparer.Ordinal)
    {
        "/DocumentManagementQmsBaselines",
        "/DocumentManagementQmsBaselines/Import",
        "/DocumentManagementQmsBaselines/CreateManual",
        "/DocumentManagementQmsBaselines/Details/{id}",
        "/DocumentManagementQmsBaselines/Designer/{id}",
        "/DocumentManagementInstantiations",
        "/DocumentManagementInstantiations/Details/{id}"
    };

    [Fact]
    public void Declares_clean_slug_module_identity()
    {
        Assert.Equal("document-management", Manifest.ModuleCode);
        Assert.Equal("DocumentManagement", Manifest.Domain);
        Assert.Equal("DitenPlatform", Manifest.Service);
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
    public void Two_list_pages_are_the_top_level_nav_entries()
    {
        var navPages = Manifest.Pages.Where(p => p.IsNavigationVisible).Select(p => p.PageCode).ToHashSet();
        Assert.True(navPages.SetEquals(new[] { "QMS_BASELINES", "INSTANCES" }));
        // Top-level entries have no parent; every sub-page does.
        Assert.All(Manifest.Pages, p =>
            Assert.Equal(p.IsNavigationVisible, p.ParentPageCode is null));
    }

    [Fact]
    public void Designer_exposes_the_real_definition_crud_actions()
    {
        var designer = Assert.Single(Manifest.Pages, p => p.PageCode == "QMS_DESIGNER");
        var keys = designer.Actions.Select(a => a.PermissionKey).ToHashSet(StringComparer.Ordinal);
        Assert.Contains(QmsBaselinePermissions.CollectionDefinitionsCreate, keys);
        Assert.Contains(QmsBaselinePermissions.CollectionDefinitionsEdit, keys);
        Assert.Contains(QmsBaselinePermissions.CollectionDefinitionsMove, keys);
        Assert.Contains(QmsBaselinePermissions.CollectionDefinitionsDelete, keys);
    }

    [Fact]
    public void Instances_archive_and_restore_reuse_the_execute_permission()
    {
        var instances = Assert.Single(Manifest.Pages, p => p.PageCode == "INSTANCES");
        var archive = Assert.Single(instances.Actions, a => a.ActionCode == "ARCHIVE");
        var restore = Assert.Single(instances.Actions, a => a.ActionCode == "RESTORE");
        Assert.Equal(DocumentManagementInstantiationPermissions.InstantiationsExecute, archive.PermissionKey);
        Assert.Equal(DocumentManagementInstantiationPermissions.InstantiationsExecute, restore.PermissionKey);
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

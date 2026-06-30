using System.Reflection;
using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.ReferenceData.SelfRegistration;
using Xunit;

namespace Diten.Platform.Application.Tests.ReferenceData;

// MC-3b-expand — the reference-data manifest must mirror the real ReferenceDataController frontend view-routes and
// the verbatim Platform.BusinessReferenceData.* keys the API controller enforces (zero drift), asserted in BOTH
// directions (§3).
public sealed class ReferenceDataManifestProviderTests
{
    private static readonly ModuleManifestDocument Manifest = new ReferenceDataManifestProvider().GetManifest();

    private static readonly HashSet<string> EnforcedPermissions =
        ReflectEnforcedPermissions(typeof(BusinessReferenceDataController));

    // Real frontend view-routes (Diten.Web ReferenceDataController), hard-coded.
    private static readonly HashSet<string> FrontendViewRoutes = new(StringComparer.Ordinal)
    {
        "/Platform/ReferenceData",
        "/Platform/ReferenceData/Sets/{setId}",
        "/Platform/ReferenceData/Sets/{setId}/DraftWizard",
        "/Platform/ReferenceData/Versions/{versionId}",
        "/Platform/ReferenceData/Hierarchy/{setCode}",
        "/Platform/ReferenceData/Attributes/{setCode}",
        "/Platform/ReferenceData/Mappings/{setCode}",
        "/Platform/ReferenceData/PublishReview/{versionId}",
        "/Platform/ReferenceData/Usage/{setCode}",
        "/Platform/ReferenceData/Usage/{setCode}/Create",
        "/Platform/ReferenceData/Usage/{setCode}/Edit/{usageRegistrationId}",
        "/Platform/ReferenceData/Usage/{setCode}/Details/{usageRegistrationId}",
        "/Platform/ReferenceData/ImportPreview"
    };

    [Fact]
    public void Declares_clean_slug_module_identity()
    {
        Assert.Equal("reference-data", Manifest.ModuleCode);
        Assert.Equal("MasterDataManagement", Manifest.Domain);
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
    public void Sets_is_the_single_top_level_nav_entry_at_the_real_route()
    {
        var sets = Assert.Single(Manifest.Pages, p => p.IsNavigationVisible);
        Assert.Equal("RD_SETS", sets.PageCode);
        Assert.Equal("/Platform/ReferenceData", sets.RoutePath);
        Assert.Null(sets.ParentPageCode);
        // Every non-nav page is a sub-page with a parent.
        Assert.All(Manifest.Pages.Where(p => !p.IsNavigationVisible),
            p => Assert.NotNull(p.ParentPageCode));
    }

    [Fact]
    public void Publish_review_exposes_the_full_approval_chain()
    {
        var review = Assert.Single(Manifest.Pages, p => p.PageCode == "RD_PUBLISH_REVIEW");
        var keys = review.Actions.Select(a => a.PermissionKey).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("Platform.BusinessReferenceData.Version.Approve", keys);
        Assert.Contains("Platform.BusinessReferenceData.Version.Publish", keys);
        Assert.Contains("Platform.BusinessReferenceData.Version.PublishOverride", keys);
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

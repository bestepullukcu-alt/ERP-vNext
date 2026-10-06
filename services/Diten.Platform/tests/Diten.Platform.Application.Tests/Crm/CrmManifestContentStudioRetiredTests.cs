using Diten.Platform.Application.Features.Crm.SelfRegistration;
using Xunit;

namespace Diten.Platform.Application.Tests.Crm;

// WP-SB-1R (bridge-decision §7) retired the ContentScope and WP-KP-4 (bridge-decision §8, DESIGN-KP-STUDIO §7) the
// ContentSet: the CRM manifest registers neither the Content Scopes nor the Content Sets page (the sidebar entry comes only
// from this descriptor, and a re-registration prunes pages the manifest no longer declares — MC-6). The Knowledge Path
// Studio page that took the content set's job stays.
public sealed class CrmManifestContentStudioRetiredTests
{
    private static readonly Diten.BuildingBlocks.ModuleRegistration.Abstractions.ModuleManifestDocument Manifest =
        new CrmManifestProvider().GetManifest();

    [Fact]
    public void The_content_scopes_page_is_not_in_the_crm_manifest()
    {
        Assert.DoesNotContain(Manifest.Pages, p => p.PageCode == "CONTENT_SCOPES");
        Assert.DoesNotContain(Manifest.Pages, p => p.RoutePath.Contains("/CRM/ContentScopes", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(Manifest.Pages.SelectMany(p => p.Actions),
            a => a.PermissionKey.StartsWith("crm.content-scope.", StringComparison.Ordinal));
    }

    [Fact]
    public void The_content_sets_page_is_not_in_the_crm_manifest()
    {
        Assert.DoesNotContain(Manifest.Pages, p => p.PageCode == "CONTENT_SETS");
        Assert.DoesNotContain(Manifest.Pages, p => p.RoutePath.Contains("/CRM/ContentSets", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(Manifest.Pages, p => p.RequiredPermission.StartsWith("crm.content-set.", StringComparison.Ordinal));
        Assert.DoesNotContain(Manifest.Pages.SelectMany(p => p.Actions),
            a => a.PermissionKey.StartsWith("crm.content-set.", StringComparison.Ordinal));
    }

    [Fact]
    public void The_knowledge_path_studio_page_stays()
    {
        var page = Assert.Single(Manifest.Pages, p => p.RoutePath == "/CRM/KnowledgePaths");
        Assert.Equal("crm.knowledge.path.read", page.RequiredPermission);
    }
}

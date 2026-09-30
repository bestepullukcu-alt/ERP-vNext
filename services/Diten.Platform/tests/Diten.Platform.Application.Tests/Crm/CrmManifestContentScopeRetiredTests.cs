using Diten.Platform.Application.Features.Crm.SelfRegistration;
using Xunit;

namespace Diten.Platform.Application.Tests.Crm;

// WP-SB-1R (bridge-decision §7) — the ContentScope is retired: the CRM manifest no longer registers the Content Scopes
// page (the sidebar entry comes only from this descriptor), while the Content Sets page stays.
public sealed class CrmManifestContentScopeRetiredTests
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
    public void The_content_sets_page_stays()
    {
        var page = Assert.Single(Manifest.Pages, p => p.PageCode == "CONTENT_SETS");
        Assert.Equal("/CRM/ContentSets", page.RoutePath);
        Assert.Equal("crm.content-set.read", page.RequiredPermission);
    }
}

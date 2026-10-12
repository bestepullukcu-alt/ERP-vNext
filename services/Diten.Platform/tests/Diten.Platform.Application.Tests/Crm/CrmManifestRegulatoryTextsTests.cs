using Diten.Platform.Application.Features.Crm.SelfRegistration;
using Xunit;

namespace Diten.Platform.Application.Tests.Crm;

// WP-KP-5a-UI — the CRM manifest registers the two Regulatory master-data screens: Safety Texts (sort 150) and Legal
// Profiles (sort 160), nav-visible List pages gated by their read keys, each with a MANAGE toolbar action and a SUBMIT
// row action. The module identity (code CRM) is unchanged, so plan / entitlement keys are not touched.
public sealed class CrmManifestRegulatoryTextsTests
{
    private static readonly Diten.BuildingBlocks.ModuleRegistration.Abstractions.ModuleManifestDocument Manifest =
        new CrmManifestProvider().GetManifest();

    [Theory]
    [InlineData("SAFETY_TEXTS", "/CRM/SafetyTexts", "crm.safety-text", 150)]
    [InlineData("LEGAL_PROFILES", "/CRM/LegalProfiles", "crm.country-legal-profile", 160)]
    public void The_regulatory_text_pages_are_registered_with_their_actions(string code, string route, string key, int sort)
    {
        var page = Assert.Single(Manifest.Pages, p => p.PageCode == code);
        Assert.Equal(route, page.RoutePath);
        Assert.Equal($"{key}.read", page.RequiredPermission);
        Assert.True(page.IsNavigationVisible);
        Assert.Equal("List", page.PageType);
        Assert.Equal(sort, page.SortOrder);

        var manage = Assert.Single(page.Actions, a => a.ActionCode == "MANAGE");
        Assert.Equal($"{key}.manage", manage.PermissionKey);
        Assert.Equal("Toolbar", manage.ActionType);
        Assert.True(manage.IsToolbarAction);
        var submit = Assert.Single(page.Actions, a => a.ActionCode == "SUBMIT");
        Assert.Equal($"{key}.submit", submit.PermissionKey);
        Assert.Equal("RowAction", submit.ActionType);
        Assert.True(submit.IsRowAction);
    }

    [Fact]
    public void The_module_identity_and_the_existing_pages_are_unchanged()
    {
        Assert.Equal("CRM", Manifest.ModuleCode);
        Assert.Single(Manifest.Pages, p => p.PageCode == "CLAIMS");
        Assert.Single(Manifest.Pages, p => p.PageCode == "KNOWLEDGE_PATHS");
        Assert.Equal(Manifest.Pages.Count, Manifest.Pages.Select(p => p.PageCode).Distinct().Count());
        Assert.Single(Manifest.Pages, p => p.SortOrder == 150);
        Assert.Single(Manifest.Pages, p => p.SortOrder == 160);
    }
}

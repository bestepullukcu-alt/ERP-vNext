using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-UX-COMMON-01 (BL-495, BL-438, BL-586) — the tenant shell, rendered with a real page, carries the shortcut layer,
/// the help button and panel, the tour layer and the side-panel Enter rule ONCE; and on the unauthorized face it draws
/// no help (UAS-001). Measured with the real view + layout (LayoutRenderer), not a source search.
/// </summary>
public sealed class PageHelpShellTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string View = "/Views/MasterDataManagement/Lskus/Index.cshtml";

    private static int Count(string haystack, string needle)
    {
        var count = 0;
        for (var at = haystack.IndexOf(needle, StringComparison.Ordinal); at >= 0;
             at = haystack.IndexOf(needle, at + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    [Fact]
    public async Task A_reader_gets_one_help_button_one_panel_and_the_shared_keyboard_layer_once()
    {
        var page = await LayoutRenderer.RenderAsync(factory, View, "Lskus", "mdm.lskus.read");

        Assert.Equal(1, Count(page, "id=\"diten-help-toggle\""));
        Assert.Equal(1, Count(page, "id=\"diten-help-panel\""));
        Assert.Equal(1, Count(page, "id=\"diten-shortcuts-l10n\""));
        Assert.Equal(1, Count(page, "assets/js/shared/diten-shortcuts.js"));
        Assert.Equal(1, Count(page, "assets/js/shared/diten-help.js"));
        Assert.Equal(1, Count(page, "assets/js/shared/diten-tour.js"));
        Assert.Equal(1, Count(page, "assets/js/shared/diten-enter-primary.js"));
        Assert.Equal(1, Count(page, "id=\"diten-tour-l10n\""));

        // The shortcut layer and the help scripts come before the page's own script, which registers its keys.
        var layer = page.IndexOf("assets/js/shared/diten-shortcuts.js", StringComparison.Ordinal);
        var help = page.IndexOf("assets/js/shared/diten-help.js", StringComparison.Ordinal);
        var own = page.IndexOf("Lskus/index.js", StringComparison.Ordinal);
        Assert.True(layer < help && help < own, "layer → help → page script");

        // The help button sits in the top bar before the language menu.
        Assert.True(page.IndexOf("id=\"diten-help-toggle\"", StringComparison.Ordinal)
            < page.IndexOf("dropdown-language", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_page_without_help_text_says_it_is_being_prepared()
    {
        var page = await LayoutRenderer.RenderAsync(factory, View, "Lskus", "mdm.lskus.read",
            new Dictionary<string, object?> { ["PageCode"] = "LSKUS" });

        Assert.Contains("data-page-code=\"LSKUS\"", page);
        Assert.Contains("data-diten-help-preparing", page);
        Assert.Contains("Help text for this page is being prepared.", page);
        Assert.DoesNotContain("data-diten-help-purpose", page);
    }

    [Fact]
    public async Task A_page_with_help_text_shows_its_purpose_and_steps_from_the_resource()
    {
        var page = await LayoutRenderer.RenderAsync(factory, View, "Lskus", "mdm.lskus.read",
            new Dictionary<string, object?> { ["PageCode"] = "USERS" });

        Assert.Contains("data-diten-help-purpose", page);
        Assert.Contains("Manage everyone who can sign in to your tenant", page);
        Assert.Contains("data-diten-help-steps", page);
        Assert.Equal(5, Count(page[page.IndexOf("data-diten-help-steps", StringComparison.Ordinal)..
            page.IndexOf("</ol>", page.IndexOf("data-diten-help-steps", StringComparison.Ordinal), StringComparison.Ordinal)], "<li>"));
        Assert.DoesNotContain("data-diten-help-preparing", page);
    }

    [Fact]
    public async Task A_platform_admin_page_is_coded_by_its_view_folder_in_the_platform_shell()
    {
        // U2 — the platform admin layout hands the help partials ForPlatformView(ViewContext.View.Path).
        var page = await LayoutRenderer.RenderAsync(factory, "/Views/Platform/DomainManagement/Index.cshtml",
            "DomainManagement", "platform.domains.read");

        Assert.Contains("data-shell=\"platform-admin\"", page);
        Assert.Contains("data-page-code=\"PLATFORMDOMAINMANAGEMENT\"", page);
        Assert.Contains("id=\"diten-help-toggle\"", page);
    }

    [Fact]
    public async Task The_unauthorized_face_draws_no_help_button_and_no_help_panel()
    {
        var denied = await LayoutRenderer.RenderAsync(factory, View, "Lskus", "mdm.lskus.create",
            new Dictionary<string, object?> { ["PageCode"] = "LSKUS" });

        Assert.Contains("diten-access-denied", denied);
        Assert.DoesNotContain("diten-help-toggle", denied);
        Assert.DoesNotContain("diten-help-panel", denied);
        Assert.DoesNotContain("diten-help.js", denied);
    }
}

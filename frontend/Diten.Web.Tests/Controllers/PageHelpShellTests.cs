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
        // WP-UX-HELP-PANEL-02 — one card per step (Step1..5), the first one open.
        Assert.Equal(5, Count(page, "data-diten-help-step=\""));
        Assert.Contains("class=\"collapse show\" id=\"diten-help-step-1\"", page);
        Assert.Contains("class=\"collapse\" id=\"diten-help-step-2\"", page);
        Assert.DoesNotContain("data-diten-help-preparing", page);
    }

    // ---- WP-UX-HELP-PANEL-02 — the owner's design (2026-10-13): header, three underlined tabs, cards, footer ----

    private static Task<string> RenderHelpAsync(WebApplicationFactory<Program> factory, string code) =>
        LayoutRenderer.RenderAsync(factory, View, "Lskus", "mdm.lskus.read",
            new Dictionary<string, object?> { ["PageCode"] = code });

    private static string Panel(string page)
    {
        var start = page.IndexOf("id=\"diten-help-panel\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "no help panel");
        return page[start..];
    }

    [Fact]
    public async Task The_panel_draws_three_tabs_with_their_roles_and_opens_on_overview()
    {
        var panel = Panel(await RenderHelpAsync(factory, "USERS"));

        Assert.Equal(1, Count(panel, "role=\"tablist\""));
        Assert.Equal(3, Count(panel, "role=\"tab\""));
        Assert.Equal(3, Count(panel, "role=\"tabpanel\""));
        foreach (var tab in new[] { "overview", "steps", "keys" })
        {
            Assert.Contains($"id=\"diten-help-tab-{tab}\" role=\"tab\"", panel);
            Assert.Contains($"aria-controls=\"diten-help-pane-{tab}\"", panel);
            Assert.Contains($"id=\"diten-help-pane-{tab}\"", panel);
        }

        // Overview is the selected tab and the shown pane; the others are out of the tab order.
        Assert.Equal(1, Count(panel, "aria-selected=\"true\""));
        Assert.Contains("aria-selected=\"true\"\n                            data-diten-help-tab=\"overview\"", panel.ReplaceLineEndings("\n"));
        Assert.Contains("class=\"tab-pane fade show active\" id=\"diten-help-pane-overview\"", panel);
        Assert.Equal(2, Count(panel, "tabindex=\"-1\" data-diten-help-tab="));

        // Header: the page's menu name under the "Page help" eyebrow; footer: Close and the (hidden) tour button.
        Assert.Contains(">Page help</div>", panel);
        Assert.Contains("class=\"diten-help-footer\"", panel);
        Assert.Contains("data-diten-help-start-tour", panel);
    }

    [Fact]
    public async Task Overview_draws_the_general_card_with_purpose_audience_and_immediate()
    {
        var panel = Panel(await RenderHelpAsync(factory, "USERS"));

        Assert.Contains("data-diten-help-general", panel);
        Assert.Contains("data-diten-help-field=\"purpose\"", panel);
        Assert.Contains("data-diten-help-field=\"audience\"", panel);
        Assert.Contains(">Administrators</div>", panel);
        Assert.Contains("data-diten-help-field=\"immediate\"", panel);
        Assert.Contains(">Disabling, deleting, password reset</div>", panel);
    }

    [Fact]
    public async Task Nothing_empty_is_drawn_a_page_without_immediate_actions_or_related_pages_has_neither()
    {
        // ITEMS carries Purpose and Audience, no Immediate, and the catalog lists no related page for it.
        var panel = Panel(await RenderHelpAsync(factory, "ITEMS"));

        Assert.Contains("data-diten-help-field=\"audience\"", panel);
        Assert.DoesNotContain("data-diten-help-field=\"immediate\"", panel);
        Assert.DoesNotContain("data-diten-help-related", panel);
    }

    [Fact]
    public async Task Steps_carry_their_icon_title_and_quoted_names_as_labels()
    {
        var panel = Panel(await RenderHelpAsync(factory, "USERS"));

        Assert.Contains("<i class=\"bx bx-user-plus\" aria-hidden=\"true\"></i>", panel); // USERS step 2
        Assert.Contains(">Invite a user</span>", panel);
        Assert.Contains(">Step 2</span>", panel);
        Assert.Contains("<span class=\"diten-help-ui\">Add User</span>", panel);
        Assert.Contains("<span class=\"diten-help-ui\">Save</span>", panel);
    }

    [Fact]
    public async Task Keyboard_tab_carries_the_keys_of_every_page_and_an_empty_card_for_this_page()
    {
        var panel = Panel(await RenderHelpAsync(factory, "USERS"));

        Assert.Contains("data-diten-help-everywhere", panel);
        foreach (var text in new[] { "Opens this panel", "Saves in a side panel", "Runs an action that cannot be taken back",
                     "Closes the open panel", "On a Mac, use &#x2318; instead of Ctrl." }) // Razor writes ⌘ encoded
        {
            Assert.Contains(text, panel);
        }

        // "On this page" is filled by diten-help.js from DitenShortcuts.list(); the server draws it hidden.
        Assert.Contains("data-diten-help-shortcuts hidden", panel);
    }

    [Fact]
    public async Task A_page_without_help_text_keeps_its_old_face_no_tabs_and_the_keys()
    {
        var panel = Panel(await RenderHelpAsync(factory, "LSKUS"));

        Assert.DoesNotContain("role=\"tablist\"", panel);
        Assert.Contains("data-diten-help-preparing", panel);
        Assert.Contains("class=\"diten-help-keys-only\"", panel);
        Assert.Contains("data-diten-help-everywhere", panel);
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

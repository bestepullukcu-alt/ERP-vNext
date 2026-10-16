using System.Text.Encodings.Web;
using Diten.Web.Services.Help;
using Diten.Web.Services.Navigation;
using Microsoft.AspNetCore.Html;
using Xunit;

namespace Diten.Web.Tests.Services;

/// <summary>
/// WP-UX-HELP-PANEL-02 — what the "?" panel takes from <see cref="PageHelpCatalog"/>: a quoted button name becomes a
/// small label while every piece of the text stays encoded; each step's icon; the related pages the reader can open.
/// </summary>
public sealed class PageHelpCatalogTests
{
    private static string Html(IHtmlContent content)
    {
        using var writer = new StringWriter();
        content.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    [Theory]
    [InlineData("Click “Add User”, then “Save”.", "Click <span class=\"diten-help-ui\">Add User</span>, then <span class=\"diten-help-ui\">Save</span>.")]
    [InlineData("Cliquez sur « Enregistrer ».", "Cliquez sur <span class=\"diten-help-ui\">Enregistrer</span>.")]
    [InlineData("انقر «حفظ» الآن", "انقر <span class=\"diten-help-ui\">حفظ</span> الآن")]
    [InlineData("No quoted name here.", "No quoted name here.")]
    public void A_quoted_name_becomes_a_label(string text, string expected)
    {
        // The encoder writes non-ASCII as character references; decoding leaves the label's own markup as it is.
        Assert.Equal(expected, System.Net.WebUtility.HtmlDecode(Html(PageHelpCatalog.Labelled(text))));
    }

    [Fact]
    public void A_script_in_the_text_stays_text_inside_and_outside_a_label()
    {
        var html = Html(PageHelpCatalog.Labelled("<script>alert(1)</script> then “<img src=x onerror=alert(2)>”"));

        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<img", html);
        Assert.StartsWith("&lt;script&gt;", html);
        Assert.Contains("<span class=\"diten-help-ui\">&lt;img", html);
        Assert.EndsWith("&gt;</span>", html);
    }

    [Fact]
    public void Each_step_has_its_icon_and_an_unknown_step_the_default()
    {
        Assert.Equal("bx-filter-alt", PageHelpCatalog.StepIcon("USERS", 1));
        Assert.Equal("bx-user-plus", PageHelpCatalog.StepIcon("USERS", 2));
        Assert.Equal("bx-send", PageHelpCatalog.StepIcon("ITEMS", 3));
        Assert.Equal(PageHelpCatalog.DefaultStepIcon, PageHelpCatalog.StepIcon("ROLES", 5));
        Assert.Equal(PageHelpCatalog.DefaultStepIcon, PageHelpCatalog.StepIcon("NOPAGE", 1));
        Assert.Equal(PageHelpCatalog.DefaultStepIcon, PageHelpCatalog.StepIcon(null, 1));
        Assert.Equal(PageHelpCatalog.DefaultStepIcon, PageHelpCatalog.StepIcon("USERS", 0));
    }

    [Fact]
    public void Every_page_with_help_steps_has_an_icon_for_each_step_it_names()
    {
        foreach (var code in new[] { "USERS", "ROLES", "ROLEPERMISSIONS", "USERROLES", "ITEMS" })
        {
            Assert.NotEqual(PageHelpCatalog.DefaultStepIcon, PageHelpCatalog.StepIcon(code, 1));
        }
    }

    private static readonly IReadOnlyDictionary<string, MenuPage> Menu = new Dictionary<string, MenuPage>
    {
        ["USERROLES"] = new("USERROLES", "/Governance/UserRoleAssignments", "platform.userroles.read"),
        ["ROLES"] = new("ROLES", "/Governance/Roles", null),
    };

    [Fact]
    public void Related_pages_link_where_the_menu_links_in_catalog_order()
    {
        var related = PageHelpCatalog.RelatedFor("USERS", Menu, _ => true);

        Assert.Equal(["USERROLES", "ROLES"], related.Select(r => r.Code));
        Assert.Equal("/Governance/UserRoleAssignments", related[0].RoutePath);
        Assert.Equal("/Governance/Roles", related[1].RoutePath);
    }

    [Fact]
    public void A_related_page_the_reader_cannot_open_is_not_listed()
    {
        var related = PageHelpCatalog.RelatedFor("USERS", Menu, permission => permission != "platform.userroles.read");

        Assert.Equal(["ROLES"], related.Select(r => r.Code));
    }

    [Fact]
    public void A_related_page_missing_from_the_menu_is_not_listed_and_an_unlisted_page_has_none()
    {
        Assert.Empty(PageHelpCatalog.RelatedFor("USERS", new Dictionary<string, MenuPage>(), _ => true));
        Assert.Empty(PageHelpCatalog.RelatedFor("ITEMS", Menu, _ => true));
        Assert.Empty(PageHelpCatalog.RelatedFor(null, Menu, _ => true));
    }
}

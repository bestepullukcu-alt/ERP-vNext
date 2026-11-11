using System.Text.RegularExpressions;
using Diten.Web.Tests.Traceability;
using Xunit;

namespace Diten.Web.Tests.Quarantine;

/// <summary>
/// WP-MVP1-FU-CLEAN1 — FU-0175-UI-THEME-01 (O-F4-7, mod-0176-f4 evidence): the Quarantine screens had the two defects the Expiry screens fixed in
/// F4 — inactive tabs painted <c>bg-white</c> (a white pill in the dark theme) and list filters without <c>data-placeholder</c> (select2 shows an
/// empty box for the "all" choice). Read from the SHIPPED views.
/// </summary>
public sealed class QuarantineThemeTests
{
    private static readonly string Views = Path.Combine(TraceViewRenderer.WebRoot, "Views", "Quarantine");

    [Fact]
    public void An_inactive_tab_takes_the_themes_paper_colour_not_white()
    {
        var header = File.ReadAllText(Path.Combine(Views, "_Header.cshtml"));
        var link = Regex.Match(header, @"<a class=""nav-link[^>]*data-qr-tab").Value;
        Assert.NotEmpty(link);
        Assert.DoesNotContain("bg-white shadow-sm", link, StringComparison.Ordinal);
        Assert.Contains("background-color: var(--bs-paper-bg)", link, StringComparison.Ordinal);
    }

    public static TheoryData<string> ListPages => new() { "Index.cshtml", "Blocked.cshtml" };

    [Theory]
    [MemberData(nameof(ListPages))]
    public void Every_list_filter_names_its_empty_choice(string page)
    {
        var view = File.ReadAllText(Path.Combine(Views, page));
        var filters = Regex.Matches(view, @"<select id=""filter\w+""[^>]*>").Select(m => m.Value).ToList();
        Assert.True(filters.Count >= 3, $"{page}: {filters.Count} filters");
        Assert.All(filters, f => Assert.Matches(@"data-placeholder=""@(Shared)?Localizer\[""[^""]+""\]""", f));
    }
}

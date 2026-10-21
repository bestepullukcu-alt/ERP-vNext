using System.Text.RegularExpressions;
using Diten.Web.Tests.Traceability;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Diten.Web.Tests.Inventory;

/// <summary>
/// WP-MVP1-FIX-LIVE3 (live finding 3: "Raf Ömrü Kuralları Ve Ayarlar" — the resx says "ve") — the vendor theme capitalizes every word of a tab
/// (<c>.nav-tabs .nav-link, .nav-pills .nav-link { text-transform: capitalize }</c>, core.css, a shared file not changed here). Every tab /
/// pill link of the MVP-1 views carries the theme's own <c>.text-none</c> (<c>text-transform: none !important</c>), so a tab shows its resx text
/// as written ("ve", "ile", "de", "by product" …). Measured on the rendered pages and on the view sources.
/// </summary>
public sealed class MvpOneTabCaseTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string[] Views =
    [
        "Inventory/Locations/Index.cshtml", "Inventory/LocationTree/Index.cshtml", "Inventory/StockLedger/Index.cshtml", "Inventory/StockMovements/Index.cshtml",
        "Inventory/StockMovements/_Form.cshtml", "Traceability/LotMigration/Index.cshtml", "Quarantine/_Header.cshtml", "Expiry/Exceptions.cshtml",
        "Expiry/Policy.cshtml", "Expiry/_Header.cshtml", "Recall/CaseDetails.cshtml", "Recall/_Header.cshtml", "ESignature/MySignature.cshtml"
    ];

    [Fact]
    public void The_theme_capitalizes_tabs_and_its_text_none_class_undoes_it()
    {
        var css = File.ReadAllText(Path.Combine(TraceViewRenderer.WebRoot, "wwwroot", "assets", "vendor", "css", "core.css")).ReplaceLineEndings("\n");
        Assert.Matches(new Regex(@"\.nav-tabs \.nav-link,\n\.nav-pills \.nav-link \{[^}]*text-transform: capitalize;"), css);
        Assert.Matches(new Regex(@"\.text-none \{\n  text-transform: none !important;\n\}"), css);
    }

    [Fact]
    public void Every_tab_link_of_the_MVP1_views_carries_text_none()
    {
        foreach (var view in Views)
        {
            var source = File.ReadAllText(Path.Combine([TraceViewRenderer.WebRoot, "Views", .. view.Split('/')]));
            var links = Regex.Matches(source, @"class=""nav-link(?![-\w])[^""]*""");
            Assert.True(links.Count > 0, $"{view}: no tab link measured");
            Assert.All(links, m => Assert.Matches(@"^class=""nav-link text-none\b", m.Value));
        }
    }

    [Fact]
    public async Task The_rendered_expiry_tabs_draw_their_text_untransformed()
    {
        var html = await TraceViewRenderer.RenderAsync(factory, "/Views/Expiry/Policy.cshtml", null, "expiry.dashboard.read", "expiry.evidence.export", "expiry.exceptions.read", "expiry.fefo-order.read", "expiry.policies.read");
        var tabs = Regex.Matches(html, @"<a class=""nav-link[^""]*""[^>]*href=""/Expiry[^""]*""");
        Assert.True(tabs.Count >= 6, $"expected the module tabs, got {tabs.Count}");
        Assert.All(tabs, m => Assert.Contains("nav-link text-none", m.Value, StringComparison.Ordinal));
        Assert.All(Regex.Matches(html, @"<button type=""button"" class=""nav-link[^""]*"" data-bs-toggle=""tab"""), m => Assert.Contains("nav-link text-none", m.Value, StringComparison.Ordinal));
    }
}

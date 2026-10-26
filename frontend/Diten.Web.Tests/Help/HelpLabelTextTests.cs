using System.Text.RegularExpressions;
using Diten.Web.Tests.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Diten.Web.Tests.Help;

/// <summary>
/// WP-MVP1-FIX-LIVE4 (FU-0175-HELP-CHIPS, G1 live run: an accessibility-tree read of Bloke Stok's step 1 came back as "filtre düğmesine ()
/// tıklayıp bloke nedeni, veya seçin ve ya tıklayın") — MEASURED, not changed: the shared panel (PageHelpCatalog.Labelled, _PageHelpPanel)
/// draws every quoted name as a REAL text node inside <c>&lt;span class="diten-help-ui"&gt;</c>; the shared CSS only frames it (no
/// <c>content</c>, no hiding), diten-help.js never touches it. The words reach the page; the dropped words are the reader tool's collapsing of
/// the role-less inline spans. This pins what we rely on, on the real shell render.
/// </summary>
public sealed class HelpLabelTextTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Blocked_stock_step_one_draws_every_quoted_name_as_text()
    {
        var page = await LayoutRenderer.RenderAsync(factory, "/Views/MasterDataManagement/Lskus/Index.cshtml", "Lskus", "mdm.lskus.read",
            new Dictionary<string, object?> { ["PageCode"] = "BLOCKEDSTOCK" });
        var panel = page[page.IndexOf("id=\"diten-help-panel\"", StringComparison.Ordinal)..];
        var labels = Regex.Matches(panel, @"<span class=""diten-help-ui"">([^<]+)</span>").Select(m => System.Net.WebUtility.HtmlDecode(m.Groups[1].Value)).ToList();
        foreach (var name in new[] { "Filter", "Apply" })                                   // the render's culture is en
            Assert.Contains(name, labels);
        Assert.DoesNotContain(labels, string.IsNullOrWhiteSpace);

        var css = File.ReadAllText(Path.Combine(Diten.Web.Tests.Traceability.TraceViewRenderer.WebRoot, "wwwroot", "assets", "css", "backbone-custom.css"));
        var rule = Regex.Match(css, @"\.diten-help-ui \{([^}]*)\}");
        Assert.True(rule.Success);
        Assert.DoesNotMatch(@"content:|font-size:\s*0|visibility:\s*hidden|display:\s*none", rule.Groups[1].Value);
    }
}

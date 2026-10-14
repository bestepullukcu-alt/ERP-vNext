using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Web.Controllers;
using Diten.Web.Controllers.CRM;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W4 · W4-FIX-d — the Web halves: (G-1) the GSKU form's global product picker pages and searches the create-options
/// endpoint (the proxy forwards a bounded search / page / size; the pure picker builds the URL and the Select2 page);
/// (G-9) the pharmacy page reads the competitors of an added GSKU's product through the MDM lookup proxy with its own key,
/// and the products of the stock / sales GSKUs come in the server's order.
/// </summary>
public sealed class VwW4FixDWebTests
{
    private const string GatewayUrl = "http://gateway.test";
    private static readonly Guid TenantId = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
    private const string Product = "11111111-1111-1111-1111-111111111111";

    // ═══ G-1 · the GSKU product picker ════════════════════════════════════════════════════════════════════

    [Fact]
    public void G1_the_create_options_proxy_forwards_a_bounded_search_page_and_size()
    {
        Assert.Equal("?pageNumber=1&pageSize=50", GskusController.CreateOptionsQuery(null, null, null));
        Assert.Equal("?pageNumber=3&pageSize=50&search=KONSET", GskusController.CreateOptionsQuery("  KONSET ", 3, null));
        Assert.Equal("?pageNumber=1&pageSize=50&search=k%C4%B1nset%20%26%20x", GskusController.CreateOptionsQuery("kınset & x", 0, 500)); // escaped, clamped
        Assert.Equal("?pageNumber=1&pageSize=1", GskusController.CreateOptionsQuery("   ", -4, 0));
        Assert.Equal(100, GskusController.CreateOptionsQuery(new string('a', 300), 1, 50).Split("&search=")[1].Length);

        var action = typeof(GskusController).GetMethod(nameof(GskusController.CreateOptions))!;
        Assert.Equal(new[] { "search", "pageNumber", "pageSize", "cancellationToken" }, action.GetParameters().Select(p => p.Name).ToArray());

        var view = File.ReadAllText(Path.Combine(WebRoot(), "Views", "MasterDataManagement", "Gskus", "Index.cshtml"));
        Assert.True(view.IndexOf("gsku-product-picker.js", StringComparison.Ordinal) < view.IndexOf("Gskus/index.js", StringComparison.Ordinal));
        var page = File.ReadAllText(Path.Combine(GskuScripts(), "index.js"));
        Assert.Contains("picker.url(endpoint, term, page)", page);
        Assert.Contains("processResults: (data, params) => picker.results(data, params.page || 1)", page);
    }

    [Fact]
    public void G1_the_picker_reads_every_product_page_by_page_with_the_typed_text()
    {
        var node = RunNode("const P = require('" + Path.Combine(GskuScripts(), "gsku-product-picker.js").Replace("\\", "/") + "');\n" + """
            const full = { globalProducts: Array.from({ length: 50 }, (_, i) => ({ id: 'p' + i, canonicalCode: 'GP-' + i, globalProductName: 'ÜRÜN ' + i })), uoms: [] };
            const last = { GlobalProducts: [{ Id: 'k', CanonicalCode: 'GP-177', GlobalProductName: 'KONSET' }] };
            console.log(JSON.stringify({
              url: [P.url('/api', 'konset', 2), P.url('/api', '', undefined), P.url('/api', ' kınset ', 0), P.url('/api', 'x'.repeat(150), 1).length],
              full: P.results(full, 1), last: P.results(last, 4), empty: P.results({}, 1)
            }));
            """);
        if (node is null) return;
        var r = JsonDocument.Parse(node).RootElement;
        Assert.Equal("/api/create-options?pageNumber=2&pageSize=50&search=konset", r.GetProperty("url")[0].GetString());
        Assert.Equal("/api/create-options?pageNumber=1&pageSize=50", r.GetProperty("url")[1].GetString());
        Assert.Equal("/api/create-options?pageNumber=1&pageSize=50&search=k%C4%B1nset", r.GetProperty("url")[2].GetString());
        Assert.Equal("/api/create-options?pageNumber=1&pageSize=50&search=".Length + 100, r.GetProperty("url")[3].GetInt32());
        Assert.Equal(50, r.GetProperty("full").GetProperty("results").GetArrayLength());
        Assert.True(r.GetProperty("full").GetProperty("pagination").GetProperty("more").GetBoolean());   // a full page ⇒ more
        var konset = r.GetProperty("last").GetProperty("results")[0];
        Assert.Equal(("k", "GP-177 — KONSET"), (konset.GetProperty("id").GetString(), konset.GetProperty("text").GetString()));
        Assert.False(r.GetProperty("last").GetProperty("pagination").GetProperty("more").GetBoolean());
        Assert.Equal(0, r.GetProperty("empty").GetProperty("results").GetArrayLength());
    }

    // ═══ G-9 · the competitors of an added GSKU's product ═════════════════════════════════════════════════════

    [Fact]
    public async Task G9_the_pharmacy_competitor_lookup_proxy_reads_mdm_with_its_own_key()
    {
        var (gateway, controller) = Arrange("crm.visit-report.read", "mdm.competitor-products.read");
        await controller.Competitors(Guid.Parse(Product), CancellationToken.None);
        var call = Assert.Single(gateway.Requests);
        Assert.Equal(("GET", GatewayUrl + "/api/mdm/competitor-products/lookup?productId=" + Product), (call.Method, call.Uri));

        foreach (var keys in new[] { new[] { "crm.visit-report.read" }, new[] { "mdm.competitor-products.read" } })
        {
            var (g, c) = Arrange(keys);
            Assert.Equal(403, ((ObjectResult)await c.Competitors(Guid.Parse(Product), CancellationToken.None)).StatusCode);
            Assert.Empty(g.Requests);
        }

        var (g2, c2) = Arrange("crm.visit-report.read", "mdm.competitor-products.read");
        Assert.Equal(403, ((ObjectResult)await c2.Competitors(Guid.Empty, CancellationToken.None)).StatusCode);
        Assert.Empty(g2.Requests);

        // the page flag + the view attribute
        Assert.True(Assert.IsType<Diten.Web.Models.CRM.PharmacyReportViewModel>(Assert.IsType<ViewResult>(
            Arrange("crm.visit-report.read", "mdm.competitor-products.read").Controller.Index(Guid.NewGuid())).Model).CanReadCompetitors);
        Assert.Contains("data-can-read-competitors=", File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "PharmacyReport", "Index.cshtml")));
    }

    [Fact]
    public void G9_the_gsku_products_come_stock_first_then_sales_once_each_and_the_page_reads_the_missing_ones()
    {
        var node = RunNode("const C = require('" + Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "PharmacyReport", "report-pharmacy-core.js").Replace("\\", "/") + "');\n" + """
            const pharmacy = { stock: [{ gskuId: 'g2' }, { gskuId: 'g1' }, { gskuId: 'gX' }], sales: [{ gskuId: 'g3' }, { gskuId: 'g1' }] };
            const map = { g1: 'P1', g2: 'P2', g3: 'P1', g4: 'P9' };
            console.log(JSON.stringify({ ids: C.gskuProductIds(pharmacy, map), none: C.gskuProductIds(null, map), nomap: C.gskuProductIds(pharmacy, null) }));
            """);
        if (node is null) return;
        var r = JsonDocument.Parse(node).RootElement;
        Assert.Equal(["P2", "P1"], r.GetProperty("ids").EnumerateArray().Select(x => x.GetString()!).ToArray()); // the server's order
        Assert.Equal(0, r.GetProperty("none").GetArrayLength());
        Assert.Equal(0, r.GetProperty("nomap").GetArrayLength());

        var page = File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "PharmacyReport", "pharmacy-report.js"));
        Assert.Contains("function ensureCompetitors()", page);
        Assert.Contains("'/lookups/competitors?productId='", page);
        Assert.Contains("case 'gsku-add': b.pharmacy = C.addGsku(p, t.getAttribute('data-id')); state.gskuResults = []; ensureCompetitors(); break;", page);
        Assert.Contains("return ensureCompetitors();", page);                                    // after the seeding
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    private static (Gateway Gateway, PharmacyReportController Controller) Arrange(params string[] permissions)
    {
        var gateway = new Gateway();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl }).Build();
        var controller = new PharmacyReportController(new HttpClient(gateway), configuration, NullLogger<PharmacyReportController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()), new("sub", "user-1") };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) } };
        return (gateway, controller);
    }

    private static string? RunNode(string code)
    {
        var temp = Path.Combine(Path.GetTempPath(), "w4fd-" + Guid.NewGuid().ToString("N") + ".js");
        File.WriteAllText(temp, code, new UTF8Encoding(false));
        try
        {
            using var process = Process.Start(new ProcessStartInfo("node", "\"" + temp + "\"")
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, StandardOutputEncoding = Encoding.UTF8
            });
            if (process is null) return null;
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(20000);
            Assert.True(process.ExitCode == 0, error);
            return output;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
        finally
        {
            File.Delete(temp);
        }
    }

    private static string GskuScripts() => Path.Combine(WebRoot(), "wwwroot", "assets", "js", "MasterDataManagement", "Gskus");

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }

    private sealed class Gateway : HttpMessageHandler
    {
        public List<(string Method, string Uri)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.Method.Method, request.RequestUri!.ToString()));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"data\":[]}", Encoding.UTF8, "application/json") });
        }
    }
}

using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.Web.Controllers.CRM;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W3E · W3E-E4R2-FIX-c (U-5, K-STD, K-TABS) on the REAL views, scripts, resx and controller: the potential
/// thresholds page wears the Golden Compact header (no page crumb), its two tables sit on the shared tab card, the
/// thresholds are the Golden Compact list in CLIENT mode (one bounded read) and the change history the second table in
/// SERVER mode (the proxy turns the list contract into the CRM's page / pageSize), with an export of every change.
/// </summary>
public sealed partial class VwW4PharmacyThresholdsWebTests
{
    private static string PtView(string file) => File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "PotentialThresholds", file)).Replace("\r\n", "\n");

    [Fact]
    public void FixC4_the_header_is_the_golden_compact_one_and_the_page_crumb_is_gone()
    {
        var view = View("PotentialThresholds");
        Assert.Matches(@"<div class=""mb-3"">\s*<h5 class=""mb-0"">@Localizer\[""PageTitle""\]</h5>\s*<p class=""mb-0 text-muted"">@Localizer\[""PageDescription""\]</p>\s*</div>", view);
        Assert.DoesNotContain("pt-crumb", view);
        Assert.DoesNotContain("<h4", view);
        Assert.DoesNotContain("Crumb", PotentialThresholdsIndexKeys());
        Assert.DoesNotContain(".pt-crumb", Script("PotentialThresholds", "potential-thresholds.css"));
        // the page action "add a product" is the toolbar's Add New (the standard place), not a header button
        Assert.DoesNotContain("id=\"pt-add\"", view);
    }

    [Fact]
    public void FixC4_the_two_tables_sit_on_the_shared_tab_card()
    {
        var view = View("PotentialThresholds");
        Assert.Contains("<partial name=\"~/Views/Shared/Components/Tabs/_TabCard.cshtml\" model=\"tabCard\" />", view);
        Assert.Contains("Key = \"thresholds\", Id = \"pt-tab-thresholds\", Icon = \"bx-slider-alt\", Text = Localizer[\"ThresholdsTitle\"].Value, Active = true, Controls = \"pt-pane-thresholds\"", view);
        Assert.Contains("Key = \"history\", Id = \"pt-tab-history\", Icon = \"bx-history\", Text = Localizer[\"HistoryTitle\"].Value, Controls = \"pt-pane-history\"", view);
        Assert.Contains("<div class=\"tab-pane fade show active\" id=\"pt-pane-thresholds\" role=\"tabpanel\" aria-labelledby=\"pt-tab-thresholds\">", view);
        Assert.Contains("<div class=\"tab-pane fade\" id=\"pt-pane-history\" role=\"tabpanel\" aria-labelledby=\"pt-tab-history\">", view);
        Assert.Contains("~/assets/js/diten-tabs.js", view);
        Assert.DoesNotContain("pt-card", view);
        // a table drawn while its tab was hidden measures itself when the tab shows
        Assert.Contains("document.addEventListener('diten:tab-change'", Script("PotentialThresholds", "index.js"));
    }

    [Fact]
    public void FixC4_the_thresholds_are_the_v2_client_list_and_the_history_the_v2_server_table()
    {
        var thresholds = PtView("_DataTable.cshtml");
        Assert.Contains("TableId = \"dt-potentialthresholds\"", thresholds);
        Assert.Contains("DataMode = \"client\"", thresholds);
        Assert.Contains("HasSelection = false", thresholds);
        var historyShell = PtView("_HistoryTable.cshtml");
        Assert.Contains("TableId = \"dt-pt-history\"", historyShell);
        Assert.Contains("DataMode = \"server\"", historyShell);
        foreach (var file in new[] { thresholds, historyShell })
        {
            Assert.Contains("<partial name=\"~/Views/Shared/Components/DataTable/_ListShell.cshtml\" model=", file);
        }

        // both tables ARE the shell's: the v2 marker + the data mode on the <table>
        var shell = File.ReadAllText(Path.Combine(WebRoot(), "Views", "Shared", "Components", "DataTable", "_ListShell.cshtml"));
        var table = Regex.Match(shell, "<table [^>]*>").Value;
        Assert.Contains("data-dt-standard=\"v2\"", table);
        Assert.Contains("data-dt-data-mode=\"@Model.DataMode\"", table);

        var index = Script("PotentialThresholds", "index.js");
        Assert.Contains("window.DitenDataTable.createList({", index);
        Assert.Contains("dataMode: 'client'", index);
        Assert.Contains("const endpoint = '/CRM/PotentialThresholds/api';", index);
        Assert.DoesNotMatch(@"serverSide\s*:\s*true", index);
        Assert.DoesNotMatch(@"pageSize=\d", index);
        // the history is built AFTER the primary list (the shared placeholder the list removes is its own)
        Assert.True(index.IndexOf("createList({", StringComparison.Ordinal) < index.IndexOf("PotentialThresholdsHistory?.create(", StringComparison.Ordinal));

        var history = Script("PotentialThresholds", "history.js");
        Assert.Contains("D.createCrudTable({", history);
        Assert.Contains("dataMode: 'server'", history);
        Assert.Contains("serverSide: true", history);
        Assert.Contains("D.toServerQuery(request, [], {})", history);                                      // the factory's own wire pieces
        Assert.Contains("D.toDataTablesResponse(json, drawOf(this, lastDraw))", history);
        Assert.Contains("serverExport: (format) => exportToServer(format)", history);
        Assert.DoesNotMatch(@"pageSize=\d", history);
        var view = View("PotentialThresholds");
        Assert.True(view.IndexOf("PotentialThresholds/history.js", StringComparison.Ordinal) < view.IndexOf("PotentialThresholds/index.js", StringComparison.Ordinal));
    }

    [Fact]
    public void FixC4_the_smoke_list_client_history_server_on_the_wire_and_the_edit_opens_the_drawer()
    {
        var r = Smoke("potential-thresholds-smoke.js", PotentialThresholdsIndexAllKeys(), "PotentialThresholds", "PotentialThresholdsIndex", "manage");
        if (r is null) { return; }
        var o = r.Value;
        Assert.Equal("/CRM/PotentialThresholds/api/thresholds/history?start=0&length=10&draw=1", o.GetProperty("historyUrl").GetString());
        Assert.True(o.GetProperty("historyServerExport").GetBoolean());
        Assert.Equal("Ürün ekle", o.GetProperty("addNewText").GetString());
        Assert.True(o.GetProperty("drawerShown").GetBoolean());
        Assert.Contains("Ayşe Demir", o.GetProperty("historyRows").GetString());
    }

    [Fact]
    public async Task FixC4_the_history_proxy_turns_start_length_into_the_crm_page_and_page_size()
    {
        const string Answer = "{\"data\":{\"items\":[{\"at\":\"2026-10-11T09:00:00Z\",\"by\":\"u2\",\"productId\":null,\"field\":\"lowBelow\",\"oldValue\":\"8\",\"newValue\":\"10\"}],\"total\":37,\"page\":3,\"pageSize\":10,\"displayNames\":{\"u2\":\"Ayşe Demir\"}}}";
        var (gateway, controller) = ThresholdArrange([InsightRead], "", Answer);
        var ok = Assert.IsType<OkObjectResult>(await controller.History(20, 10, null, null, null, CancellationToken.None));
        Assert.Equal(GatewayUrl + "/api/crm/doctor-insights/thresholds/history?page=3&pageSize=10", Assert.Single(gateway.Requests).Uri);
        var data = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value)).RootElement.GetProperty("data");
        Assert.Equal((37, 37, 1), (data.GetProperty("total").GetInt64(), data.GetProperty("filteredTotal").GetInt64(), data.GetProperty("items").GetArrayLength()));
        Assert.Equal("Ayşe Demir", data.GetProperty("displayNames").GetProperty("u2").GetString());

        var (g2, c2) = ThresholdArrange([InsightRead], "", Answer);
        await c2.History(0, 25, null, null, null, CancellationToken.None);
        await c2.History(100, 100, null, null, null, CancellationToken.None);
        Assert.Equal(new[] { GatewayUrl + "/api/crm/doctor-insights/thresholds/history?page=1&pageSize=25", GatewayUrl + "/api/crm/doctor-insights/thresholds/history?page=2&pageSize=100" },
            g2.Requests.Select(x => x.Uri).ToArray());

        // the contract's refusals never reach the CRM: a page size outside 1–100, a search, a sort key, a bad direction
        var (g3, c3) = ThresholdArrange([InsightRead], "", Answer);
        Assert.Equal(400, Status(await c3.History(0, 0, null, null, null, CancellationToken.None)));
        Assert.Equal(400, Status(await c3.History(0, 101, null, null, null, CancellationToken.None)));
        Assert.Equal(400, Status(await c3.History(0, 10, "x", null, null, CancellationToken.None)));
        Assert.Equal(400, Status(await c3.History(0, 10, null, "at", "asc", CancellationToken.None)));
        Assert.Equal(400, Status(await c3.History(0, 10, null, null, "up", CancellationToken.None)));
        Assert.Empty(g3.Requests);
        Assert.Equal("/api/crm/doctor-insights/thresholds/history?page=1&pageSize=200", PotentialThresholdsController.HistoryPath(0, 500));
    }

    [Fact]
    public async Task FixC4_the_history_export_writes_every_change_page_by_page_with_names_and_refuses_over_50000()
    {
        static string Page(int from, int count, long total) => "{\"data\":{\"total\":" + total + ",\"displayNames\":{\"u2\":\"Ayşe Demir\"},\"items\":[" + string.Join(",", Enumerable.Range(from, count).Select(i =>
            "{\"at\":\"2026-10-11T09:00:00Z\",\"by\":\"u2\",\"productId\":" + (i == 0 ? "null" : "\"P1\"") + ",\"field\":\"lowBelow\",\"oldValue\":\"8\",\"newValue\":\"" + i + "\"}")) + "]}}";
        var (gateway, controller) = RoutedThresholds([InsightRead, ProductRead], path =>
            path.Contains("/global-products/selector", StringComparison.Ordinal) ? "{\"data\":{\"items\":[{\"id\":\"P1\",\"globalProductName\":\"Ürofleks\"}],\"totalCount\":1}}"
            : path.Contains("page=1&", StringComparison.Ordinal) ? Page(0, 200, 201) : Page(200, 1, 201), "tr");
        var file = Assert.IsType<FileContentResult>(await controller.HistoryExport("csv", ["at,by,productId,field,newValue"], CancellationToken.None));
        var csv = Encoding.UTF8.GetString(file.FileContents).TrimStart('﻿').Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        Assert.Equal(202, csv.Length);                                                                         // the header + EVERY change
        Assert.Equal("Tarih,Kişi,Satır,Alan,Değişiklik", csv[0]);
        Assert.EndsWith(",Ayşe Demir,Kiracı varsayılanı,Az sınırı,8 → 0", csv[1]);                           // the person by name, the tenant row
        Assert.Contains(",Ürofleks,", csv[2]);                                                                 // the product by name
        Assert.DoesNotContain("u2", string.Join("\n", csv));                                                  // never the id
        Assert.StartsWith("potential-threshold-history-", file.FileDownloadName);

        var (g2, c2) = RoutedThresholds([InsightRead], _ => Page(0, 1, 50_001));
        var big = Assert.IsType<ObjectResult>(await c2.HistoryExport("xlsx", null, CancellationToken.None));
        Assert.Equal(413, big.StatusCode);
        Assert.Contains("EXPORT_TOO_LARGE", JsonSerializer.Serialize(big.Value));
        Assert.Single(g2.Requests);

        var (g3, c3) = RoutedThresholds([InsightRead], _ => Page(0, 1, 1));
        Assert.Equal(400, Status(await c3.HistoryExport("csv", ["at,by,secret"], CancellationToken.None)));
        Assert.Equal(400, Status(await c3.HistoryExport("pdf", null, CancellationToken.None)));
        Assert.Empty(g3.Requests);
        Assert.Equal(403, Status(await RoutedThresholds([ProductRead], _ => "{}").Controller.HistoryExport("csv", null, CancellationToken.None)));
    }

    // ── helpers (FIX-c) ────────────────────────────────────────────────────────────────────────────────────────

    private static string[] PotentialThresholdsIndexKeys()
        => Diten.Web.Views.CRM.PotentialThresholds.PotentialThresholdsIndex.ScriptKeys.Concat(Diten.Web.Views.CRM.PotentialThresholds.PotentialThresholdsIndex.ViewKeys).ToArray();

    private static string[] PotentialThresholdsIndexAllKeys() => PotentialThresholdsIndexKeys().Distinct().ToArray();

    private static (RoutedGateway Gateway, PotentialThresholdsController Controller) RoutedThresholds(string[] permissions, Func<string, string> answer, string? acceptLanguage = null)
    {
        var gateway = new RoutedGateway(answer);
        var controller = new PotentialThresholdsController(new HttpClient(gateway), Config(), NullLogger<PotentialThresholdsController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()), new("sub", "user-1") };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization(o => o.ResourcesPath = "Resources");
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), RequestServices = services.BuildServiceProvider() };
        if (acceptLanguage is not null) http.Request.Headers.AcceptLanguage = acceptLanguage;
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return (gateway, controller);
    }

    private sealed class RoutedGateway(Func<string, string> answer) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!.OriginalString;
            Requests.Add(uri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer(uri[GatewayUrl.Length..]), Encoding.UTF8, "application/json") });
        }
    }
}

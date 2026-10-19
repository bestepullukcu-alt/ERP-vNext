using System.Net;
using System.Text;
using System.Text.Json;
using Diten.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Xunit;
using static Diten.Web.Tests.Recall.RecallDataGateTests;

namespace Diten.Web.Tests.Recall;

/// <summary>
/// WP-0177-R6 — what the doors send and give back: the Vakalar filters reach listCases as they are, the union answer (items + drillRows) is
/// laid out in ONE list by position with the drill rows keeping their own shape (no class, no level — D-25), the trace CSV is the service's
/// bytes, "Vaka aç" forwards its body with the body's Idempotency-Key, and a malformed trace query never reaches the gateway.
/// </summary>
public sealed class RecallDoorTests
{
    private static readonly string[] All = [RecallKeys.DashboardRead, RecallKeys.TraceRun, RecallKeys.TraceExport, RecallKeys.CasesRead, RecallKeys.CasesCreate];

    private static JsonElement Body(IActionResult result) => result switch
    {
        ContentResult content => JsonDocument.Parse(content.Content!).RootElement,
        _ => throw new Xunit.Sdk.XunitException("unexpected result " + result.GetType().Name)
    };

    [Fact]
    public async Task The_case_filters_reach_listCases_as_they_are()
    {
        var gateway = new FakeGateway();
        await Invoke("ListCases", gateway, All,
            "?start=20&length=10&search=RC-2026&type=Drill&status=Draft&status=Active&drillStatus=Completed&class=2&skuId=" + Guid.Empty + "&openedFrom=2026-10-01&sort=number");
        var sent = Assert.Single(gateway.Requests, r => r.StartsWith("/api/recall/cases", StringComparison.Ordinal));
        var query = sent.Split('?')[1].Split('&').ToList();
        Assert.Equal(["page=3", "pageSize=10", "q=RC-2026", "type=Drill", "class=2", "skuId=" + Guid.Empty, "openedFrom=2026-10-01", "sort=number",
            "status=Draft", "status=Active", "drillStatus=Completed"], query);
    }

    [Fact]
    public async Task The_union_answer_becomes_one_list_by_position_and_a_drill_row_keeps_no_class_or_level()
    {
        var gateway = new FakeGateway
        {
            Answer = r => Json(r.RequestUri!.AbsolutePath == "/api/recall/cases" ? """
                {"items":[{"caseId":"00000001-0000-4000-8000-000000000001","number":"RC-2026-0002","type":"Real","status":"Active","class":2,"level":"Pharmacy","item":{"itemId":"00000002-0000-4000-8000-000000000002","skuId":"00000003-0000-4000-8000-000000000003","skuLevel":"Lsku"},
                           "rootLot":{"lotId":"l1","lotNumber":"L-1"},"openedAtUtc":"2026-10-02T08:00:00Z","owner":{"userId":"00000004-0000-4000-8000-000000000004"},"affectedQuantity":[],"progress":[],"version":1,
                           "sortAt":"2026-10-02T08:00:00Z","position":1}],
                 "drillRows":[{"runId":"00000005-0000-4000-8000-000000000005","number":"RC-2026-D001","type":"Drill","status":"Completed","lot":{"lotId":"l2","lotNumber":"L-2"},"startedAtUtc":"2026-10-03T08:00:00Z",
                               "owner":{"userId":"00000006-0000-4000-8000-000000000006"},"durationMinutes":100,"targetHours":4,"passed":true,"sortAt":"2026-10-03T08:00:00Z","position":0,"version":3}],
                 "total":2,"page":1,"pageSize":10,"statusCounts":{"Active":1},"drillStatusCounts":{"Completed":1}}
                """ : "{}")
        };
        var body = Body(await Invoke("ListCases", gateway, All, "?start=0&length=10"));
        var rows = body.GetProperty("data").GetProperty("items").EnumerateArray().ToList();
        Assert.Equal([("Drill", "RC-2026-D001"), ("Case", "RC-2026-0002")], rows.Select(r => (r.GetProperty("rowKind").GetString(), r.GetProperty("number").GetString())));
        Assert.False(rows[0].TryGetProperty("class", out _));
        Assert.False(rows[0].TryGetProperty("level", out _));
        Assert.Equal(2, rows[1].GetProperty("class").GetInt32());
        Assert.Equal(2, body.GetProperty("data").GetProperty("total").GetInt32());
        Assert.Equal(1, body.GetProperty("drillStatusCounts").GetProperty("Completed").GetInt32());
    }

    [Fact]
    public async Task The_trace_CSV_is_the_services_bytes_unchanged()
    {
        var csv = "﻿\"Düğüm türü\";\"SKU\";\"Lot\";\"Depo / Müşteri\";\"Miktar\";\"Birim\";\"Hareket / Sevkiyat no\";\"Tarih\";\"Boşluk\"\r\n\"FinishedGoodLot\";\"s\";\"L-1\";\"\";\"\";\"\";\"\";\"\";\"\"\r\n";
        var bytes = Encoding.UTF8.GetBytes(csv);
        var gateway = new FakeGateway
        {
            Answer = _ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
                response.Content.Headers.ContentType = new("text/csv");
                response.Content.Headers.ContentDisposition = new("attachment") { FileName = "izleme_L-1.csv" };
                return response;
            }
        };
        var result = Assert.IsType<FileContentResult>(await Invoke("ExportTrace", gateway, All));
        Assert.Equal(bytes, result.FileContents);
        Assert.Equal("izleme_L-1.csv", result.FileDownloadName);
        var lines = Encoding.UTF8.GetString(result.FileContents).TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("\"Düğüm türü\";\"SKU\";\"Lot\"", lines[0], StringComparison.Ordinal);
        Assert.Contains("/api/recall/trace/export?rootLotId=0b6c2f1e-3d4a-4c5b-9e8f-7a6b5c4d3e2f&direction=Both&format=csv", Assert.Single(gateway.Requests));
    }

    [Fact]
    public async Task Open_case_forwards_the_body_with_its_own_idempotency_key()
    {
        string? key = null, sentBody = null;
        var gateway = new FakeGateway
        {
            Answer = r =>
            {
                key = r.Headers.TryGetValues("Idempotency-Key", out var v) ? v.Single() : null;
                sentBody = r.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                // FU-CLEAN2 (FU-WEB-CONTRACT-FAKES-RECALL): a whole CaseDetail of RECALL 1.9.0 (was a 3-member fragment with a non-uuid id)
                return Json("""
                    {"caseId":"00000009-0000-4000-8000-000000000009","number":"RC-2026-0009","type":"Real","status":"Draft","class":2,"level":"Pharmacy",
                     "item":{"itemId":"00000002-0000-4000-8000-000000000002","skuId":"00000003-0000-4000-8000-000000000003","skuLevel":"Lsku"},"rootLot":{"lotId":"0b6c2f1e-3d4a-4c5b-9e8f-7a6b5c4d3e2f","lotNumber":"L-1"},
                     "openedAtUtc":"2026-10-02T08:00:00Z","owner":{"userId":"00000004-0000-4000-8000-000000000004"},"affectedQuantity":[],"progress":[],"version":1,"reason":"Kalite şikâyeti",
                     "direction":"Both","scopeRevision":1,"counts":{"scopeItems":0,"excludedItems":0,"customers":0,"notified":0,"consumptionDeclarations":0,"segregations":0}}
                    """, HttpStatusCode.Created);
            }
        };
        var result = await Invoke("OpenCase", gateway, All);
        Assert.Equal(201, Status(result));
        Assert.Equal("r6-key-0001", key);
        var forwarded = JsonDocument.Parse(sentBody!).RootElement;
        Assert.Equal(("0b6c2f1e-3d4a-4c5b-9e8f-7a6b5c4d3e2f", "Both", 2, "Pharmacy"), (forwarded.GetProperty("rootLotId").GetString(), forwarded.GetProperty("direction").GetString(),
            forwarded.GetProperty("class").GetInt32(), forwarded.GetProperty("level").GetString()));
        Assert.Equal("/api/recall/cases", Assert.Single(gateway.Requests));

        var none = new FakeGateway();
        Assert.Equal(400, Status(await Invoke("OpenCase", none, All, body: "{\"rootLotId\":\"x\"}")));
        Assert.Empty(none.Requests);
    }

    [Theory]
    [InlineData("not-a-guid", "Both")]
    [InlineData("0b6c2f1e-3d4a-4c5b-9e8f-7a6b5c4d3e2f", "Sideways")]
    public async Task A_malformed_trace_query_never_reaches_the_gateway(string rootLotId, string direction)
    {
        var gateway = new FakeGateway();
        var result = await InvokeTrace(gateway, rootLotId, direction);
        Assert.Equal(400, Status(result));
        Assert.Empty(gateway.Requests);
    }

    private static Task<IActionResult> InvokeTrace(FakeGateway gateway, string rootLotId, string direction)
    {
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = "http://gateway.test" }).Build();
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext
        {
            User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                [new("tenantId", Guid.NewGuid().ToString()), new("legal_entity_id", Guid.NewGuid().ToString())], "test"))
        };
        http.Request.Headers.Cookie = "access_token=caller-token";
        var keys = new TraceKeys([RecallKeys.TraceRun]);
        var controller = new RecallController(new GatewayFactory(gateway), new HttpClient(gateway), configuration, keys,
            new Localizer(), Microsoft.Extensions.Logging.Abstractions.NullLogger<RecallController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
        return controller.TraceData(rootLotId, direction, CancellationToken.None);
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class GatewayFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class TraceKeys(string[] keys) : Diten.Web.Services.IPermissionSnapshot
    {
        public bool Has(string permissionKey) => keys.Contains(permissionKey, StringComparer.OrdinalIgnoreCase);
        public IReadOnlyCollection<string> Keys => keys;
    }

    private sealed class Localizer : Microsoft.Extensions.Localization.IStringLocalizer<Diten.Web.Views.Recall.Recall>
    {
        public Microsoft.Extensions.Localization.LocalizedString this[string name] => new(name, name);
        public Microsoft.Extensions.Localization.LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<Microsoft.Extensions.Localization.LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}

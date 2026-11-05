using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Diten.Web.Controllers;
using Diten.Web.Models.SupplyChain.Shipments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using static Diten.Web.Tests.Controllers.SupplyChainShipmentsControllerTests;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// MOD-0183 r9 DL-4 (§24.3, §24.4) MVC adapters on production code: the server-mode list, read-time names, the details lines
/// and the server export. Each test names its SB row.
/// </summary>
public sealed class SupplyChainShipmentsListTests
{
    private const string Other = "14900000-0000-4000-8000-000000000002";
    private static readonly string[] ReadAndCrm = ["supplychain.shipments.read", "crm.account.read"];

    private static string Row(string number, string? customerRef, string warehouse = "wh-01", string shipTo = "TR, Istanbul") =>
        $"{{\"shipmentId\":\"{Guid.NewGuid()}\",\"shipmentNumber\":\"{number}\",\"sourceDocumentId\":\"SO-{number}\",\"warehouseReferenceId\":\"{warehouse}\"," +
        $"\"shipToReference\":\"{shipTo}\",\"customerRef\":{(customerRef is null ? "null" : $"\"{customerRef}\"")},\"status\":\"Draft\",\"plannedShipAt\":\"2026-09-20T08:00:00Z\"}}";

    private static string Page(params string[] rows) => $"{{\"items\":[{string.Join(',', rows)}],\"page\":1,\"pageSize\":50,\"total\":{rows.Length + 5},\"filteredTotal\":{rows.Length},\"contractVersion\":\"v1\"}}";

    private static HttpResponseMessage Route(HttpRequestMessage r, string list, Func<string, HttpResponseMessage>? crm = null)
    {
        var path = r.RequestUri!.AbsolutePath;
        if (path.StartsWith("/api/crm/accounts/", StringComparison.Ordinal))
            return crm?.Invoke(path[18..]) ?? Response(HttpStatusCode.OK, """{"data":{"accountName":"Acme","accountCode":"C-1","responsiblePersonEmail":"buyer@example.test"}}""");
        if (path.StartsWith("/api/locations/", StringComparison.Ordinal))
            return path.EndsWith("/wh-01", StringComparison.Ordinal) ? Response(HttpStatusCode.OK, """{"locationId":"wh-01","code":"WH-1","name":"Main"}""") : Response(HttpStatusCode.NotFound, "{}");
        return Response(HttpStatusCode.OK, list);
    }

    private static JsonObject Json(IActionResult result) => JsonNode.Parse(Assert.IsType<ContentResult>(result).Content!)!.AsObject();

    // SB-9 / SB-14: the factory's server request becomes queryShipments 3.5.0 (page from start/length, search, orderBy, orderDir)
    // and the answer is { data: { items, total, filteredTotal } }.
    [Fact]
    public async Task List_TranslatesTheFactoryRequest_AndAnswersTheFactoryEnvelope()
    {
        var handler = new CaptureHandler(r => Route(r, Page(Row("SHP-1", Customer))));
        var result = Json(await CreateController(handler, ReadAndCrm).List("InTransit", " SO 1 ", 100, 50, " acme ", "plannedShipAt", "desc", 7, CancellationToken.None));
        Assert.Equal("http://localhost:5000/api/shipment-bundle/shipments?page=3&pageSize=50&status=InTransit&sourceDocumentId=SO 1&search=acme&orderBy=plannedShipAt&orderDir=desc", handler.Uris[0]);
        Assert.Equal(6, result["data"]!["total"]!.GetValue<int>());
        Assert.Equal(1, result["data"]!["filteredTotal"]!.GetValue<int>());
        Assert.Equal(7, result["draw"]!.GetValue<int>());
        Assert.Single(result["data"]!["items"]!.AsArray());
    }

    [Theory]
    [InlineData("customer", null)]
    [InlineData("shipmentNumber", "DESC")]
    public async Task List_RefusesAnUnknownOrderColumnOrDirection(string orderBy, string? orderDir)
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("no call"));
        var result = await CreateController(handler, ReadAndCrm).List(null, null, 0, 50, null, orderBy, orderDir, 0, CancellationToken.None);
        Assert.Equal(400, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Equal(0, handler.CallCount);
    }

    // SB-9 / SB-20: customerRef never reaches the browser; a resolved row carries the name and code only.
    [Fact]
    public async Task List_ResolvesTheCustomerAndWarehouseNames_AndDropsCustomerRef()
    {
        var handler = new CaptureHandler(r => Route(r, Page(Row("SHP-1", Customer))));
        var body = Json(await CreateController(handler, ReadAndCrm).List(null, null, 0, 50, null, null, null, 0, CancellationToken.None));
        var item = body["data"]!["items"]![0]!.AsObject();
        Assert.False(item.ContainsKey("customerRef"));
        Assert.DoesNotContain(Customer, body.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(("resolved", "Acme", "C-1"), (item["customer"]!["state"]!.GetValue<string>(), item["customer"]!["name"]!.GetValue<string>(), item["customer"]!["code"]!.GetValue<string>()));
        Assert.Equal(["code", "name", "state"], item["customer"]!.AsObject().Select(p => p.Key).Order().ToArray());
        Assert.Equal("WH-1 · Main", item["warehouse"]!["label"]!.GetValue<string>());
    }

    // SB-11a / SB-19 / SB-25: an old record is marked and keeps its shipToReference as stored; a stored value that is not a
    // lower-case Guid is "not found"; neither is sent to CRM. A deleted account (CRM 404) is "not found", never its id.
    [Fact]
    public async Task List_OldRecordsAndMalformedRefsMakeNoCrmCall_AndADeletedAccountIsNotFound()
    {
        var handler = new CaptureHandler(r => Route(r, Page(Row("SHP-1", null, shipTo: "warehouse-outbound:7:ship-to"), Row("SHP-2", "ACME"), Row("SHP-3", Other)),
            crm: _ => Response(HttpStatusCode.NotFound, "{}")));
        var items = Json(await CreateController(handler, ReadAndCrm).List(null, null, 0, 50, null, null, null, 0, CancellationToken.None))["data"]!["items"]!.AsArray();
        Assert.Equal(("oldRecord", "warehouse-outbound:7:ship-to"), (items[0]!["customer"]!["state"]!.GetValue<string>(), items[0]!["customer"]!["reference"]!.GetValue<string>()));
        Assert.Equal("notFound", items[1]!["customer"]!["state"]!.GetValue<string>());
        Assert.Equal("notFound", items[2]!["customer"]!["state"]!.GetValue<string>());
        Assert.Equal([$"/api/crm/accounts/{Other}"], handler.Uris.Select(u => new Uri(u).AbsolutePath).Where(p => p.StartsWith("/api/crm/", StringComparison.Ordinal)).ToArray());
    }

    // SB-10: a failed or denied name read never fails the page — "unavailable", with the reference for the warehouse and no id
    // for the customer; without crm.account.read no CRM request is made at all.
    [Fact]
    public async Task List_FailedReadsShowUnavailable_AndThePageStillLoads()
    {
        var handler = new CaptureHandler(r => Route(r, Page(Row("SHP-1", Customer, warehouse: "wh-gone")), crm: _ => Response(HttpStatusCode.InternalServerError, "")));
        var item = Json(await CreateController(handler, ReadAndCrm).List(null, null, 0, 50, null, null, null, 0, CancellationToken.None))["data"]!["items"]![0]!.AsObject();
        Assert.Equal(["state"], item["customer"]!.AsObject().Select(p => p.Key).ToArray());
        Assert.Equal("unavailable", item["customer"]!["state"]!.GetValue<string>());
        Assert.Equal(("unavailable", "wh-gone"), (item["warehouse"]!["state"]!.GetValue<string>(), item["warehouse"]!["reference"]!.GetValue<string>()));

        var noCrm = new CaptureHandler(r => Route(r, Page(Row("SHP-1", Customer))));
        var denied = Json(await CreateController(noCrm, ["supplychain.shipments.read"]).List(null, null, 0, 50, null, null, null, 0, CancellationToken.None))["data"]!["items"]![0]!;
        Assert.Equal("unavailable", denied["customer"]!["state"]!.GetValue<string>());
        Assert.DoesNotContain(noCrm.Uris, u => u.Contains("/api/crm/", StringComparison.Ordinal));
    }

    // SB-15: names are read once per request (two rows, one customer → one read) and never cached across requests.
    [Fact]
    public async Task List_DeduplicatesWithinARequest_AndDoesNotCacheAcrossRequests()
    {
        var handler = new CaptureHandler(r => Route(r, Page(Row("SHP-1", Customer), Row("SHP-2", Customer))));
        var controller = CreateController(handler, ReadAndCrm);
        await controller.List(null, null, 0, 50, null, null, null, 0, CancellationToken.None);
        Assert.Equal(1, handler.Uris.Count(u => u.Contains("/api/crm/", StringComparison.Ordinal)));
        Assert.Equal(1, handler.Uris.Count(u => u.Contains("/api/locations/", StringComparison.Ordinal)));
        await CreateController(handler, ReadAndCrm).List(null, null, 0, 50, null, null, null, 0, CancellationToken.None);
        Assert.Equal(2, handler.Uris.Count(u => u.Contains("/api/crm/", StringComparison.Ordinal)));
    }

    // SB-21: nothing read from CRM reaches the log on the list adapter.
    [Fact]
    public async Task List_LogsNothingReadFromCrm()
    {
        var log = new SupplyChainShipmentsCustomerTests.CapturingLogger();
        var handler = new CaptureHandler(r => Route(r, Page(Row("SHP-1", Customer))));
        await CreateController(handler, ReadAndCrm, logger: log).List(null, null, 0, 50, null, null, null, 0, CancellationToken.None);
        Assert.NotEmpty(log.Lines);
        Assert.DoesNotContain(log.Lines, l => l.Contains("Acme", StringComparison.Ordinal) || l.Contains("C-1", StringComparison.Ordinal) || l.Contains("buyer@", StringComparison.Ordinal));
    }

    // SB-12: the details lines carry product / SKU name + code (one bulk read), lot number and location name; a reference that
    // cannot be read is shown as stored; customerRef is dropped.
    [Fact]
    public async Task Detail_ResolvesLineNames_WithOneBulkSkuRead()
    {
        var sku = Guid.NewGuid(); var other = Guid.NewGuid();
        var detail = $"{{\"shipmentId\":\"{Shipment}\",\"shipmentNumber\":\"SHP-1\",\"warehouseReferenceId\":\"wh-01\",\"shipToReference\":\"TR\",\"customerRef\":\"{Customer}\",\"status\":\"Draft\"," +
            $"\"lines\":[{{\"lineNumber\":\"1\",\"skuId\":\"{sku}\",\"lotId\":\"lot-1\",\"fromLocationId\":\"wh-01\"}},{{\"lineNumber\":\"2\",\"skuId\":\"{other}\",\"fromLocationId\":\"bin-gone\"}}],\"contractVersion\":\"v1\"}}";
        var bulkBodies = new List<string>();
        var handler = new CaptureHandler(r =>
        {
            var path = r.RequestUri!.AbsolutePath;
            if (path == "/api/product-master/skus/bulk-read")
            {
                bulkBodies.Add(r.Content!.ReadAsStringAsync().Result);
                return Response(HttpStatusCode.OK, $"{{\"items\":[{{\"skuId\":\"{sku}\",\"skuLevel\":\"Lsku\",\"canonicalCode\":\"LS-1\",\"name\":\"Tablet 16\",\"lifecycleStatus\":\"Active\"}}],\"notFound\":[\"{other}\"],\"contractVersion\":\"v1\"}}");
            }
            if (path == "/api/trace/lots/lot-1") return Response(HttpStatusCode.OK, """{"lotId":"lot-1","lotNumber":"L-0042"}""");
            return Route(r, detail);
        });
        var body = Json(await CreateController(handler, ReadAndCrm).Detail(Shipment, CancellationToken.None));
        Assert.False(body.ContainsKey("customerRef"));
        Assert.Equal("Acme", body["customer"]!["name"]!.GetValue<string>());
        var lines = body["lines"]!.AsArray();
        Assert.Equal("LS-1 · Tablet 16", lines[0]!["sku"]!["label"]!.GetValue<string>());
        Assert.Equal("L-0042", lines[0]!["lot"]!["label"]!.GetValue<string>());
        Assert.Equal("WH-1 · Main", lines[0]!["fromLocation"]!["label"]!.GetValue<string>());
        Assert.Equal(("unavailable", other.ToString()), (lines[1]!["sku"]!["state"]!.GetValue<string>(), lines[1]!["sku"]!["reference"]!.GetValue<string>()));
        Assert.Null(lines[1]!["lot"]);
        Assert.Equal(("unavailable", "bin-gone"), (lines[1]!["fromLocation"]!["state"]!.GetValue<string>(), lines[1]!["fromLocation"]!["reference"]!.GetValue<string>()));
        Assert.Single(bulkBodies);
        Assert.Contains(sku.ToString(), bulkBodies[0]); Assert.Contains(other.ToString(), bulkBodies[0]);
    }

    // BL-452 (owner decision 2026-11-05): the export writes every matching row with names, guards formula cells, and refuses
    // more than ShipmentExport.MaxRows with 413.
    [Fact]
    public async Task Export_WritesEveryMatchingRowWithNames_AsCsvAndXlsx()
    {
        var handler = new CaptureHandler(r => Route(r, Page(Row("SHP-1", Customer), Row("=HYPERLINK(1)", null, shipTo: "free text"))));
        var csv = Assert.IsType<FileContentResult>(await CreateController(handler, ReadAndCrm).Export("csv", null, null, null, null, null, null, CancellationToken.None));
        var text = Encoding.UTF8.GetString(csv.FileContents);
        Assert.StartsWith("﻿", text);
        Assert.Contains("\"Acme · C-1\"", text);
        Assert.Contains("\"'=HYPERLINK(1)\"", text);
        Assert.Contains("free text", text);
        Assert.DoesNotContain(Customer, text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);

        var xlsx = Assert.IsType<FileContentResult>(await CreateController(handler, ReadAndCrm).Export("xlsx", "shipmentNumber,customer", null, null, null, null, null, CancellationToken.None));
        using var zip = new ZipArchive(new MemoryStream(xlsx.FileContents));
        using var sheet = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var xml = sheet.ReadToEnd();
        Assert.Contains("Acme · C-1", xml);
        Assert.Contains("&apos;=HYPERLINK(1)", xml); // the apostrophe guard, XML-escaped
        Assert.NotNull(zip.GetEntry("[Content_Types].xml"));
    }

    // SB-10: Gateway down on a location read → the warehouse keeps its reference and the page still loads. The gateway client
    // turns the transport error into a failed reply, so this measures that path; the Gate catch (VER F-4) is defence in depth
    // that no location or lot read reaches today (measured 2026-11-05: removing it keeps this test green).
    [Fact]
    public async Task List_ALocationReadThatThrows_KeepsItsReference()
    {
        var handler = new CaptureHandler(r => r.RequestUri!.AbsolutePath.StartsWith("/api/locations/", StringComparison.Ordinal)
            ? throw new HttpRequestException("down") : Route(r, Page(Row("SHP-1", null))));
        var item = Json(await CreateController(handler, ReadAndCrm).List(null, null, 0, 50, null, null, null, 0, CancellationToken.None))["data"]!["items"]![0]!;
        Assert.Equal(("unavailable", "wh-01"), (item["warehouse"]!["state"]!.GetValue<string>(), item["warehouse"]!["reference"]!.GetValue<string>()));
    }

    // VER F-7: characters XML 1.0 forbids are dropped from the workbook, so a stored free text cannot corrupt it.
    [Fact]
    public void Xlsx_DropsXmlIllegalControlCharacters()
    {
        var bytes = ShipmentExport.Xlsx(["A"], [new string?[] { "a\u0001b\u000Bc\td" }]);
        using var zip = new ZipArchive(new MemoryStream(bytes));
        using var sheet = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var xml = sheet.ReadToEnd();
        System.Xml.Linq.XDocument.Parse(xml);
        Assert.Contains("abc\td", xml);
    }

    [Fact]
    public async Task Export_RefusesMoreThanTheCap_AndUnknownColumnsOrFormats()
    {
        var huge = $"{{\"items\":[{Row("SHP-1", null)}],\"total\":20000,\"filteredTotal\":{ShipmentExport.MaxRows + 1}}}";
        var handler = new CaptureHandler(r => Route(r, huge));
        Assert.Equal(413, Assert.IsType<ObjectResult>(await CreateController(handler, ReadAndCrm).Export("csv", null, null, null, null, null, null, CancellationToken.None)).StatusCode);
        Assert.Equal(400, Assert.IsType<ObjectResult>(await CreateController(handler, ReadAndCrm).Export("pdf", null, null, null, null, null, null, CancellationToken.None)).StatusCode);
        Assert.Equal(400, Assert.IsType<ObjectResult>(await CreateController(handler, ReadAndCrm).Export("csv", "customerRef", null, null, null, null, null, CancellationToken.None)).StatusCode);
    }
}

using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.Web.Controllers;
using Diten.Web.Models.SupplyChain.Shipments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;
using static Diten.Web.Tests.Controllers.SupplyChainShipmentsControllerTests;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// MOD-0183 r9 DL-3 (§24.1, §24.2) MVC adapters on production code: the create adapter's account check and destination text,
/// the customer picker and address projections, the location layer and the log rule. Each test names its SB row.
/// </summary>
public sealed class SupplyChainShipmentsCustomerTests
{
    private static readonly string[] CreateAndCrm = ["supplychain.shipments.create", "crm.account.read"];

    private static string Account(string? country, string? city, string? district, string? line) =>
        JsonSerializer.Serialize(new { data = new { id = Customer, accountName = "Acme", accountCode = "C-1", countryRef = country, cityRef = city,
            districtRef = district, addressLine = line, responsiblePersonName = "Ayse", responsiblePersonPhone = "+90 555", responsiblePersonEmail = "buyer@example.test",
            notes = "vip", latitude = 41.0, longitude = 29.0, logoDataUri = "data:image/png;base64,AA==" }, isSuccessful = true, statusCode = 200 });

    // SB-2 / SB-23: the account id goes to customerRef (lower-case "D"), never to shipToReference; shipToReference is the CRM
    // address in CountryRef, CityRef, DistrictRef, AddressLine order, trimmed, empty dropped, joined with ", ". SB-5 / SB-6:
    // MOD-0183 / MANUAL, lines numbered 1, 2, 3 in order, no inventoryReferenceId. The body is a 3.5.0 createShipment body.
    [Fact]
    public async Task Create_SendsCustomerRefAndTheDestinationText_WithAdapterSetFields()
    {
        var handler = new CaptureHandler(r => IsCrm(r) ? Response(HttpStatusCode.OK, Account("TR", " Istanbul ", "", "Ataturk Cad. No 12"))
            : Response(HttpStatusCode.Created, "{\"shipmentId\":\"44444444-4444-4444-8444-444444444444\"}"));
        var controller = CreateController(handler, CreateAndCrm, true);
        var model = NewShipment("2");
        model.Lines!.Add(new() { ItemId = Guid.NewGuid(), SkuId = Guid.NewGuid(), SkuLevel = "Item", Quantity = "1", UomId = "KGM", FromLocationId = "loc-2", LotId = "lot-9" });
        Assert.IsType<ContentResult>(await controller.Create(model, CancellationToken.None));

        Assert.Equal(2, handler.CallCount);
        Assert.Equal($"http://localhost:5000/api/crm/accounts/{Customer}", handler.Uris[0]);
        Assert.Equal("http://localhost:5000/api/shipment-bundle/shipments", handler.Uris[1]);
        var body = JsonNode.Parse(handler.Body)!.AsObject();
        Assert.Equal(Customer, body["customerRef"]!.GetValue<string>());
        Assert.Equal("TR, Istanbul, Ataturk Cad. No 12", body["shipToReference"]!.GetValue<string>());
        Assert.Equal(("MOD-0183", "MANUAL"), (body["sourceModule"]!.GetValue<string>(), body["sourceType"]!.GetValue<string>()));
        var lines = body["lines"]!.AsArray();
        Assert.Equal(["1", "2"], lines.Select(l => l!["lineNumber"]!.GetValue<string>()).ToArray());
        Assert.All(lines, l => Assert.False(l!.AsObject().ContainsKey("inventoryReferenceId")));
        Assert.False(lines[0]!.AsObject().ContainsKey("lotId"));
        Assert.Equal("lot-9", lines[1]!["lotId"]!.GetValue<string>());
        Assert.DoesNotContain("legalEntityId", handler.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Acme", handler.Body, StringComparison.Ordinal);
    }

    // SB-23: all four address fields empty → 400 CUSTOMER_ADDRESS_MISSING, no createShipment call.
    [Fact]
    public async Task Create_AccountWithoutAnyAddress_Is400CustomerAddressMissing_AndSendsNothing()
    {
        var handler = new CaptureHandler(r => IsCrm(r) ? Response(HttpStatusCode.OK, Account(" ", null, "", null)) : throw new InvalidOperationException("no create"));
        var result = await CreateController(handler, CreateAndCrm, true).Create(NewShipment("1"), CancellationToken.None);
        AssertCode(result, 400, ShipmentCustomer.AddressMissingCode);
        Assert.Equal(1, handler.CallCount);
    }

    // SB-2a: missing, free text, upper case, braces → 400 INVALID_CUSTOMER_ACCOUNT and no call at all.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Acme Corp")]
    [InlineData("14900000-0000-4000-8000-00000000000A")]
    [InlineData("{14900000-0000-4000-8000-000000000001}")]
    [InlineData("14900000-0000-4000-8000-000000000001\n")]
    public async Task Create_MalformedCustomerRef_Is400InvalidCustomerAccount_WithNoCall(string? customerRef)
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("no call"));
        var result = await CreateController(handler, CreateAndCrm, true).Create(NewShipment("1", customerRef), CancellationToken.None);
        AssertCode(result, 400, ShipmentCustomer.InvalidAccountCode);
        Assert.Equal(0, handler.CallCount);
    }

    // SB-2a: CRM 404 (also another tenant's id) → 400 INVALID_CUSTOMER_ACCOUNT; 403 → 403; 503 / unreachable → 503; no create.
    [Theory]
    [InlineData(HttpStatusCode.NotFound, 400)]
    [InlineData(HttpStatusCode.Forbidden, 403)]
    [InlineData(HttpStatusCode.ServiceUnavailable, 503)]
    [InlineData(HttpStatusCode.InternalServerError, 503)]
    public async Task Create_CrmAnswer_MapsAndSendsNoCreate(HttpStatusCode crm, int expected)
    {
        var handler = new CaptureHandler(r => IsCrm(r) ? Response(crm, "{}") : throw new InvalidOperationException("no create"));
        var result = await CreateController(handler, CreateAndCrm, true).Create(NewShipment("1"), CancellationToken.None);
        Assert.Equal(expected, Assert.IsType<ObjectResult>(result).StatusCode);
        if (expected == 400) AssertCode(result, 400, ShipmentCustomer.InvalidAccountCode);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Create_CrmUnreachable_Is503_AndSendsNoCreate()
    {
        var handler = new CaptureHandler(r => IsCrm(r) ? throw new HttpRequestException("down") : throw new InvalidOperationException("no create"));
        var result = await CreateController(handler, CreateAndCrm, true).Create(NewShipment("1"), CancellationToken.None);
        Assert.Equal(503, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Equal(1, handler.CallCount);
    }

    // SB-3: without crm.account.read nothing is asked of CRM and nothing is created; the picker and address adapters refuse too.
    [Fact]
    public async Task WithoutCrmAccountRead_NoCrmRequest_AndNoCreate()
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("no call"));
        var controller = CreateController(handler, ["supplychain.shipments.create"], true);
        Assert.Equal(403, Assert.IsType<ObjectResult>(await controller.Create(NewShipment("1"), CancellationToken.None)).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>(await controller.Customers("ac", 1, CancellationToken.None)).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>(await controller.Customer(Guid.Parse(Customer), CancellationToken.None)).StatusCode);
        Assert.Equal(0, handler.CallCount);
    }

    // SB-4: the address adapter returns name, code and the four address fields only — no person, phone, e-mail, notes,
    // coordinates or logo; and no id.
    [Fact]
    public async Task CustomerAddress_ProjectsNameCodeAndFourAddressFieldsOnly()
    {
        var handler = new CaptureHandler(_ => Response(HttpStatusCode.OK, Account("TR", "Istanbul", "Kadikoy", "Ataturk Cad. No 12")));
        var result = Assert.IsType<OkObjectResult>(await CreateController(handler, CreateAndCrm).Customer(Guid.Parse(Customer), CancellationToken.None));
        var json = JsonSerializer.SerializeToNode(result.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
        Assert.Equal(["addressLine", "cityRef", "code", "countryRef", "districtRef", "name"], json.Select(p => p.Key).Order().ToArray());
        Assert.Equal($"http://localhost:5000/api/crm/accounts/{Customer}", handler.Uri);
    }

    // SB-2 / SB-20: the picker returns id (for the value), name and code; no other CRM field.
    [Fact]
    public async Task CustomerPicker_ReturnsIdNameAndCodeOnly()
    {
        const string list = """{"data":{"items":[{"id":"14900000-0000-4000-8000-000000000001","accountName":"Acme","accountCode":"C-1","accountType":"Customer","status":"Active","countryRef":"TR","activeContactCount":3}],"total":41,"page":1,"pageSize":20},"isSuccessful":true}""";
        var handler = new CaptureHandler(_ => Response(HttpStatusCode.OK, list));
        var result = Assert.IsType<OkObjectResult>(await CreateController(handler, CreateAndCrm).Customers(" ac me ", 2, CancellationToken.None));
        Assert.Equal("http://localhost:5000/api/crm/accounts?page=2&pageSize=20&search=ac me", handler.Uri); // Uri.ToString() shows the escaped space unescaped
        var json = JsonSerializer.SerializeToNode(result.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
        var row = Assert.Single(json["items"]!.AsArray())!.AsObject();
        Assert.Equal(["code", "id", "name"], row.Select(p => p.Key).Order().ToArray());
        Assert.True(json["hasMore"]!.GetValue<bool>());
    }

    // SB-15: every CRM read uses the caller's token and tenant; no shared cache — two calls, two reads.
    [Fact]
    public async Task CustomerReads_UseTheCallersTokenAndTenant_AndAreNotCached()
    {
        var handler = new CaptureHandler(_ => Response(HttpStatusCode.OK, Account("TR", null, null, "x")));
        var controller = CreateController(handler, CreateAndCrm);
        await controller.Customer(Guid.Parse(Customer), CancellationToken.None);
        await controller.Customer(Guid.Parse(Customer), CancellationToken.None);
        Assert.Equal(2, handler.CallCount);
        Assert.Equal("Bearer test-token", handler.Headers["Authorization"]);
        Assert.Equal("11111111-1111-4111-8111-111111111111", handler.Headers["X-Tenant-Id"]);
    }

    // SB-21: with a capturing logger on the picker, address and create adapters no log line holds the account name, code,
    // an address field, person, phone or e-mail.
    [Fact]
    public async Task NothingReadFromCrmReachesTheLog()
    {
        var log = new CapturingLogger();
        var handler = new CaptureHandler(r => IsCrm(r)
            ? Response(HttpStatusCode.OK, r.RequestUri!.Query.Length > 0
                ? """{"data":{"items":[{"id":"14900000-0000-4000-8000-000000000001","accountName":"Acme","accountCode":"C-1"}],"total":1}}"""
                : Account("TR", "Istanbul", "Kadikoy", "Ataturk Cad. No 12"))
            : Response(HttpStatusCode.Created, "{\"shipmentId\":\"44444444-4444-4444-8444-444444444444\"}"));
        var controller = CreateController(handler, CreateAndCrm, true, log);
        await controller.Customers("acme", 1, CancellationToken.None);
        await controller.Customer(Guid.Parse(Customer), CancellationToken.None);
        await controller.Create(NewShipment("1"), CancellationToken.None);
        Assert.NotEmpty(log.Lines);
        foreach (var secret in new[] { "Acme", "C-1", "Istanbul", "Kadikoy", "Ataturk", "Ayse", "+90 555", "buyer@example.test" })
            Assert.DoesNotContain(log.Lines, line => line.Contains(secret, StringComparison.Ordinal));
    }

    // SB-7: one layer at a time — parentId = the opened node, Active, and never an isStockable filter on the layer.
    [Fact]
    public async Task LocationLayer_AsksDirectChildrenOfTheOpenedNode_WithoutIsStockable()
    {
        const string layer = """{"items":[{"locationId":"11111111-0000-4000-8000-000000000001","code":"Z-1","name":"Zone 1","type":"Zone","isStockable":false},{"locationId":"11111111-0000-4000-8000-000000000002","code":"B-1","name":"B-1","type":"Bin","isStockable":true}],"total":2}""";
        var handler = new CaptureHandler(_ => Response(HttpStatusCode.OK, layer));
        var parent = Guid.NewGuid();
        var result = Assert.IsType<OkObjectResult>(await CreateController(handler, ["supplychain.shipments.create"]).Locations(parent.ToString(), "b", CancellationToken.None));
        Assert.Equal($"http://localhost:5000/api/locations/search?parentId={parent:D}&status=Active&page=1&pageSize=200&search=b", handler.Uri);
        Assert.DoesNotContain("isStockable", handler.Uri, StringComparison.OrdinalIgnoreCase);
        var items = JsonSerializer.SerializeToNode(result.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web))!["items"]!.AsArray();
        Assert.Equal([false, true], items.Select(i => i!["isStockable"]!.GetValue<bool>()).ToArray());
        Assert.Equal(400, Assert.IsType<ObjectResult>(await CreateController(handler, ["supplychain.shipments.create"]).Locations("not-a-guid", null, CancellationToken.None)).StatusCode);
    }

    // SB-7: the warehouse picker asks Active warehouses only.
    [Fact]
    public async Task WarehousePicker_AsksActiveWarehouses()
    {
        var handler = new CaptureHandler(_ => Response(HttpStatusCode.OK, """{"items":[]}"""));
        await CreateController(handler, ["supplychain.shipments.create"]).Warehouses(null, CancellationToken.None);
        Assert.Equal("http://localhost:5000/api/locations/search?type=Warehouse&status=Active&page=1&pageSize=50", handler.Uri);
    }

    [Fact]
    public void Destination_FollowsTheCrmOrderTrimsDropsEmptyAndCutsNothing()
    {
        Assert.Equal("TR, Istanbul, Ataturk Cad. No 12", ShipmentCustomer.Destination(new(null, null, "TR", " Istanbul ", "", "Ataturk Cad. No 12")));
        var longLine = new string('x', 600);
        Assert.Equal("TR, " + longLine, ShipmentCustomer.Destination(new(null, null, "TR", null, null, longLine)));
        Assert.Null(ShipmentCustomer.Destination(new("n", "c", " ", "", null, "\t")));
    }

    private static void AssertCode(IActionResult result, int status, string code)
    {
        var failure = Assert.IsType<ObjectResult>(result);
        Assert.Equal(status, failure.StatusCode);
        Assert.Equal(code, JsonSerializer.SerializeToNode(failure.Value)!["error"]!["code"]!.GetValue<string>());
    }

    internal sealed class CapturingLogger : ILogger<SupplyChainShipmentsController>
    {
        public List<string> Lines { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Lines.Add(formatter(state, exception) + (exception is null ? string.Empty : " " + exception));
    }
}

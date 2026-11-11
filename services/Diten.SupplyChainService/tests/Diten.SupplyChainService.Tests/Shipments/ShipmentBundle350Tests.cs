using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.SupplyChainService.Application.Features.Shipments;
using Diten.SupplyChainService.Persistence.Features.Shipments;
using Diten.SupplyChainService.Tests.Common;
using Diten.SupplyChainService.Tests.Shipments.Dispatch;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;

namespace Diten.SupplyChainService.Tests.Shipments;

/// <summary>
/// MOD-0183 r9 DL-3, the service side of SHIPMENT-BUNDLE 3.5.0 (§24.7 items 1–3): SB-22 (applied exactly: customerRef stored
/// as given and returned on summary and detail with shipToReference and warehouseReferenceId; search / orderBy / orderDir /
/// filteredTotal as the contract text; a 3.4.0 client still creates) and SB-25 (the pattern check on the service). Real host,
/// real Mongo replica set (MOD0183_TEST_MONGO), fake INVENTORY.
/// </summary>
public sealed class ShipmentBundle350Tests(ITestOutputHelper output)
{
    private const string TestSecret = "mod0183-test-signing-secret-not-for-production-123456";
    private const string Database = "diten_mod0183_tests";
    private const string Customer = "14900000-0000-4000-8000-000000000001";
    private static string Connection => Environment.GetEnvironmentVariable("MOD0183_TEST_MONGO")
     ?? throw new InvalidOperationException("MOD0183_TEST_MONGO must point to an isolated replica set.");
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _le = Guid.NewGuid();
    private readonly Guid _actor = Guid.NewGuid();
    private readonly Guid _correlation = Guid.NewGuid();

    private sealed class Factory : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(c => c.AddInMemoryCollection(Settings()));
            return base.CreateHost(builder);
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(Settings()));
            builder.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<IShipmentCommitProbe>(new NoOpShipmentCommitProbe()))
                .StubLegalEntityValidation().UseFakeInventory(new FakeInventory()));
        }
        private static Dictionary<string, string?> Settings() => new(AuthServiceToken.JwtSettings(TestSecret))
        {
            ["Mongo:ConnectionString"] = Connection,
            ["Mongo:DatabaseName"] = Database,
            ["Inventory:ReferenceBaseUrl"] = DispatchTestHost.InventoryBaseUrl
        };
    }

    private static JsonObject Body(string sourceDocumentId = "SO-900", string? customerRef = Customer, string shipTo = "TR, Istanbul, Kadikoy, Ataturk Cad. No 12",
        string plannedShipAt = "2026-09-20T08:00:00Z")
    {
        var body = JsonNode.Parse($$"""
 {"sourceModule":"MOD-0183","sourceType":"MANUAL","sourceDocumentId":"{{sourceDocumentId}}","warehouseReferenceId":"wh-01",
 "shipToReference":"{{shipTo}}","plannedShipAt":"{{plannedShipAt}}",
 "lines":[{"lineNumber":"1","itemId":"b1f2c3d4-0000-0000-0000-000000000001","skuId":"c3d4e5f6-0000-0000-0000-000000000002",
 "quantity":"3.000","uomId":"EA","inventoryReferenceId":null,"skuLevel":"Lsku","fromLocationId":"loc-01"}]}
 """)!.AsObject();
        if (customerRef is not null) body["customerRef"] = customerRef;
        return body;
    }

    private async Task<(int Status, JsonObject Body)> Send(HttpClient client, string method, string path, JsonObject? body = null, string? key = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/shipment-bundle/shipments" + path);
        request.Headers.Add("Authorization", "Bearer " + AuthServiceToken.Mint(TestSecret, _tenant, _actor,
            new[] { "read", "create" }.Select(p => "supplychain.shipments." + p)));
        request.Headers.Add("X-Tenant-Id", _tenant.ToString()); request.Headers.Add("X-Legal-Entity-Id", _le.ToString());
        request.Headers.Add("X-Correlation-Id", _correlation.ToString());
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        output.WriteLine($"{method} {path} => {(int)response.StatusCode} {text}");
        return ((int)response.StatusCode, string.IsNullOrEmpty(text) ? new JsonObject() : JsonNode.Parse(text)!.AsObject());
    }

    private Task<long> StoredShipments(Factory app) => app.Services.GetRequiredService<IMongoDatabase>().GetCollection<BsonDocument>("sce_shipments")
        .CountDocumentsAsync(new BsonDocument { { "TenantId", _tenant.ToString() }, { "LegalEntityId", _le.ToString() } });

    private static void AssertContract(string path, int status, JsonObject body)
    {
        var errors = ShipmentBundleContract.ValidateResponse(path, "GET", status, body);
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }

    [Fact]
    public async Task CustomerRef_IsStoredAsGiven_AndReturnedOnSummaryAndDetail()
    {
        using var app = new Factory(); using var c = app.CreateClient();
        var created = await Send(c, "POST", "", Body(), "sb22-create");
        Assert.Equal(201, created.Status);
        var id = created.Body["shipmentId"]!.GetValue<string>();

        var detail = await Send(c, "GET", "/" + id);
        Assert.Equal(200, detail.Status);
        Assert.Equal(Customer, detail.Body["customerRef"]!.GetValue<string>());
        Assert.Equal("TR, Istanbul, Kadikoy, Ataturk Cad. No 12", detail.Body["shipToReference"]!.GetValue<string>());
        Assert.Equal("wh-01", detail.Body["warehouseReferenceId"]!.GetValue<string>());
        AssertContract("/shipments/{shipmentId}", 200, detail.Body);

        var list = await Send(c, "GET", "");
        Assert.Equal(200, list.Status);
        var row = Assert.Single(list.Body["items"]!.AsArray())!.AsObject();
        Assert.Equal(Customer, row["customerRef"]!.GetValue<string>());
        Assert.Equal("TR, Istanbul, Kadikoy, Ataturk Cad. No 12", row["shipToReference"]!.GetValue<string>());
        Assert.Equal("wh-01", row["warehouseReferenceId"]!.GetValue<string>());
        Assert.Equal(1, list.Body["filteredTotal"]!.GetValue<int>());
        AssertContract("/shipments", 200, list.Body);
    }

    [Fact]
    public async Task A340ClientWithoutCustomerRef_StillCreates_AndReplaysWithItsOldFingerprint()
    {
        using var app = new Factory(); using var c = app.CreateClient();
        var created = await Send(c, "POST", "", Body(customerRef: null), "sb22-340");
        Assert.Equal(201, created.Status);
        var detail = await Send(c, "GET", "/" + created.Body["shipmentId"]!.GetValue<string>());
        Assert.Null(detail.Body["customerRef"]);
        AssertContract("/shipments/{shipmentId}", 200, detail.Body);
        Assert.Equal(200, (await Send(c, "POST", "", Body(customerRef: null), "sb22-340")).Status);
        // The same key with a customerRef is a different request: 409, not a silent replay.
        Assert.Equal(409, (await Send(c, "POST", "", Body(), "sb22-340")).Status);
    }

    [Fact]
    public void FingerprintOfARequestWithoutCustomerRef_IsUnchangedBy350()
    {
        var withoutField = JsonSerializer.Deserialize<ShipmentModels.Create>(Body(customerRef: null).ToJsonString(), ShipmentProjection.Options)!;
        Assert.DoesNotContain("customerRef", JsonSerializer.Serialize(withoutField, ShipmentProjection.Options), StringComparison.OrdinalIgnoreCase);
        var withField = withoutField with { CustomerRef = Customer };
        Assert.NotEqual(RequestFingerprint.For(withoutField), RequestFingerprint.For(withField));
    }

    // SB-25: the service checks the 3.5.0 pattern and stores nothing on a mismatch.
    [Theory]
    [InlineData("14900000-0000-4000-8000-00000000000A")]
    [InlineData("{14900000-0000-4000-8000-000000000001}")]
    [InlineData("14900000000040008000000000000001")]
    [InlineData("ACME Corp")]
    [InlineData("buyer@example.test")]
    [InlineData("14900000-0000-4000-8000-000000000001\n")]
    [InlineData("")]
    public async Task MalformedCustomerRef_Is400InvalidRequest_AndStoresNothing(string customerRef)
    {
        using var app = new Factory(); using var c = app.CreateClient();
        var r = await Send(c, "POST", "", Body(customerRef: customerRef), "sb25-" + Guid.NewGuid());
        Assert.Equal(400, r.Status);
        Assert.Equal("INVALID_REQUEST", r.Body["error"]!["code"]!.GetValue<string>());
        Assert.Equal(0, await StoredShipments(app));
    }

    [Fact]
    public async Task Search_OrderBy_OrderDir_AndFilteredTotal_BehaveAsTheContractText()
    {
        using var app = new Factory(); using var c = app.CreateClient();
        Assert.Equal(201, (await Send(c, "POST", "", Body("SO-ALPHA-1", plannedShipAt: "2026-09-22T08:00:00Z"), "s1")).Status);
        Assert.Equal(201, (await Send(c, "POST", "", Body("so-alpha-2", plannedShipAt: "2026-09-20T08:00:00Z"), "s2")).Status);
        Assert.Equal(201, (await Send(c, "POST", "", Body("SO-BETA-3", plannedShipAt: "2026-09-21T08:00:00Z"), "s3")).Status);

        // Case-insensitive substring on sourceDocumentId; total keeps its 3.4.0 meaning, filteredTotal counts the search.
        var alpha = await Send(c, "GET", "?search=Alpha");
        Assert.Equal(3, alpha.Body["total"]!.GetValue<int>());
        Assert.Equal(2, alpha.Body["filteredTotal"]!.GetValue<int>());
        Assert.Equal(2, alpha.Body["items"]!.AsArray().Count);
        AssertContract("/shipments", 200, alpha.Body);

        // Substring on shipmentNumber too.
        var all = (await Send(c, "GET", "")).Body["items"]!.AsArray().Select(x => x!["shipmentNumber"]!.GetValue<string>()).ToArray();
        var number = all[0];
        var byNumber = await Send(c, "GET", "?search=" + Uri.EscapeDataString(number[4..16].ToUpperInvariant()));
        Assert.Equal(number, Assert.Single(byNumber.Body["items"]!.AsArray())!["shipmentNumber"]!.GetValue<string>());

        // The term is a literal, not a pattern; names are not searchable.
        Assert.Equal(0, (await Send(c, "GET", "?search=" + Uri.EscapeDataString("SO-.*"))).Body["filteredTotal"]!.GetValue<int>());
        Assert.Equal(0, (await Send(c, "GET", "?search=Istanbul")).Body["filteredTotal"]!.GetValue<int>());
        // Empty search is no text filter.
        Assert.Equal(3, (await Send(c, "GET", "?search=")).Body["filteredTotal"]!.GetValue<int>());

        var desc = (await Send(c, "GET", "?orderBy=plannedShipAt&orderDir=desc")).Body["items"]!.AsArray()
            .Select(x => x!["sourceDocumentId"]!.GetValue<string>()).ToArray();
        Assert.Equal(["SO-ALPHA-1", "SO-BETA-3", "so-alpha-2"], desc);
        var asc = (await Send(c, "GET", "?orderBy=plannedShipAt")).Body["items"]!.AsArray()
            .Select(x => x!["sourceDocumentId"]!.GetValue<string>()).ToArray();
        Assert.Equal(["so-alpha-2", "SO-BETA-3", "SO-ALPHA-1"], asc);
        var paged = await Send(c, "GET", "?orderBy=plannedShipAt&pageSize=1&page=2");
        Assert.Equal("SO-BETA-3", Assert.Single(paged.Body["items"]!.AsArray())!["sourceDocumentId"]!.GetValue<string>());

        Assert.Equal(400, (await Send(c, "GET", "?orderBy=customerRef")).Status);
        Assert.Equal(400, (await Send(c, "GET", "?orderBy=plannedShipAt&orderDir=DESC")).Status);
        Assert.Equal(400, (await Send(c, "GET", "?search=" + new string('x', 101))).Status);
        Assert.Equal(200, (await Send(c, "GET", "?search=" + new string('x', 100))).Status);
    }
}

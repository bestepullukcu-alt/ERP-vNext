using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Diten.SupplyChainService.Application.Features.Returns;
using Diten.SupplyChainService.Infrastructure.Features.Returns;
using Diten.SupplyChainService.Tests.Common;
using Diten.SupplyChainService.Tests.Shipments.Dispatch;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;

namespace Diten.SupplyChainService.Tests.Returns.Receipt;

/// <summary>
/// The shipment seam behind <see cref="ReturnReferenceReader"/>: answers the GET of a shipment detail with <see cref="Detail"/>,
/// or forwards to <see cref="Forward"/> (the real Shipments API of the same host, for the joint scenario R34-8).
/// </summary>
public sealed class FakeShipmentDetail : HttpMessageHandler
{
    private int _calls;
    public JsonObject Detail { get; set; } = new();
    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? Forward { get; set; }
    public int Calls => Volatile.Read(ref _calls);
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Interlocked.Increment(ref _calls);
        if (Forward is not null) return await Forward(request, ct);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Detail.ToJsonString(), Encoding.UTF8, "application/json") };
    }
}

/// <summary>
/// MOD-0186 §34 test host: the real Program, coordinator, repository and INVENTORY client on the owned replica set
/// (<c>RETURNS_MONGO_URI</c>, no fallback port), <see cref="FakeInventory"/> and <see cref="FakeShipmentDetail"/> at the two
/// HTTP seams, MDM legal-entity validation stubbed and a test clock for the lease (request timeout 30 s → lease 60 s).
/// </summary>
public sealed class ReturnReceiptHost : WebApplicationFactory<Program>
{
    public const string Secret = "mod0186-dl1-test-signing-secret-not-for-production-123456";
    public static readonly TimeSpan PastLease = TimeSpan.FromSeconds(61);
    public readonly FakeInventory Inventory = new();
    public readonly FakeShipmentDetail Shipments = new();
    public readonly ManualClock Clock = new();
    private static string Connection => Environment.GetEnvironmentVariable("RETURNS_MONGO_URI")
        ?? throw new InvalidOperationException("RETURNS_MONGO_URI must point to an isolated replica set; there is no fallback port.");
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(c => c.AddInMemoryCollection(Settings()));
        return base.CreateHost(builder);
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(Settings()));
        builder.ConfigureServices(s =>
        {
            s.StubLegalEntityValidation().UseFakeInventory(Inventory, Clock);
            s.AddHttpClient<IReturnReferenceReader, ReturnReferenceReader>().ConfigurePrimaryHttpMessageHandler(() => Shipments);
        });
    }
    private static Dictionary<string, string?> Settings() => new(AuthServiceToken.JwtSettings(Secret))
    {
        ["Mongo:ConnectionString"] = Connection,
        ["Mongo:DatabaseName"] = "diten_mod0186_receipt_tests",
        ["Inventory:ReferenceBaseUrl"] = DispatchTestHost.InventoryBaseUrl,
        ["Inventory:RequestTimeoutSeconds"] = "30",
        ["Returns:ReferenceBaseUrl"] = "http://shipment.test/"
    };
    public IMongoDatabase Db => Services.GetRequiredService<IMongoDatabase>();
}

/// <summary>One tenant, legal entity, actor and correlation root per test, and the HTTP helpers of the return lifecycle.</summary>
public sealed class ReceiptScenario(ReturnReceiptHost app, ITestOutputHelper output)
{
    public Guid Tenant { get; } = Guid.NewGuid();
    public Guid LegalEntity { get; } = Guid.NewGuid();
    public Guid Actor { get; } = Guid.NewGuid();
    public Guid Correlation { get; } = Guid.NewGuid();
    public Guid ShipmentId { get; } = Guid.NewGuid();
    public static readonly string[] ReturnPermissionsAll = [ReturnPermissions.Read, ReturnPermissions.Create, ReturnPermissions.Transition,
        "supplychain.returns.authorize", "supplychain.returns.transit", "supplychain.returns.receive"];

    public static string Item(string line) => "b1f2c3d4-0000-4000-8000-00000000000" + line;
    public static string Sku(string line) => "c3d4e5f6-0000-4000-8000-00000000000" + line;

    /// <summary>A Delivered shipment detail with the 3.4.0 issue fields; <paramref name="stockIssued"/> null = pre-WP-P1.</summary>
    public JsonObject Shipment(bool? stockIssued, params (string Line, string Quantity, string? Issue)[] lines) => new()
    {
        ["shipmentId"] = ShipmentId.ToString(), ["status"] = "Delivered", ["sourceModule"] = "MOD-0141", ["sourceType"] = "SALES_ORDER",
        ["warehouseReferenceId"] = "wh-01", ["shipToReference"] = "CUST-1", ["contractVersion"] = "v1", ["lifecycleCorrelationId"] = Correlation.ToString(),
        ["dispatchState"] = "None", ["stockIssued"] = stockIssued,
        ["lines"] = new JsonArray(lines.Select(l => (JsonNode)new JsonObject
        {
            ["lineNumber"] = l.Line, ["itemId"] = Item(l.Line), ["skuId"] = Sku(l.Line), ["quantity"] = l.Quantity, ["uomId"] = "EA",
            ["skuLevel"] = "Lsku", ["lotId"] = "lot-" + l.Line, ["fromLocationId"] = "loc-" + l.Line, ["issueTransactionId"] = l.Issue
        }).ToArray())
    };

    public string Token(Guid? tenant = null, IEnumerable<string>? permissions = null) =>
        AuthServiceToken.Mint(ReturnReceiptHost.Secret, tenant ?? Tenant, Actor, permissions ?? ReturnPermissionsAll);

    public async Task<(int Status, JsonObject Body)> Send(string method, string path, JsonObject? body = null, string? key = null, Guid? tenant = null, Guid? le = null)
    {
        using var client = app.CreateClient();
        var t = tenant ?? Tenant;
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/shipment-bundle/returns" + path);
        request.Headers.Add("Authorization", "Bearer " + Token(t));
        request.Headers.Add("X-Tenant-Id", t.ToString()); request.Headers.Add("X-Legal-Entity-Id", (le ?? LegalEntity).ToString());
        request.Headers.Add("X-Correlation-Id", Correlation.ToString());
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        output.WriteLine($"{method} {path} => {(int)response.StatusCode} {text}");
        return ((int)response.StatusCode, string.IsNullOrEmpty(text) ? new JsonObject() : JsonNode.Parse(text)!.AsObject());
    }

    /// <summary>Creates a return of <paramref name="lines"/> and moves it to InTransit.</summary>
    public async Task<Guid> InTransit(params (string Line, string Quantity)[] lines)
    {
        var created = await Send("POST", "", new JsonObject
        {
            ["shipmentId"] = ShipmentId.ToString(), ["reasonCode"] = "damaged",
            ["lines"] = new JsonArray(lines.Select(l => (JsonNode)new JsonObject { ["shipmentLineNumber"] = l.Line, ["quantity"] = l.Quantity, ["uomId"] = "EA" }).ToArray())
        }, "create-" + Guid.NewGuid());
        Assert.Equal(201, created.Status);
        var id = Guid.Parse(created.Body["returnId"]!.GetValue<string>());
        foreach (var target in new[] { "Authorized", "InTransit" })
            Assert.Equal(200, (await Send("POST", $"/{id}/transition", new JsonObject { ["targetStatus"] = target, ["occurredAt"] = "2026-10-29T07:00:00Z" }, target + "-" + id)).Status);
        return id;
    }

    public static JsonObject Received(string warehouse = "wh-q", string? location = "loc-q1", string at = "2026-10-29T08:00:00Z", JsonArray? unposted = null)
    {
        var body = new JsonObject { ["targetStatus"] = "Received", ["occurredAt"] = at, ["toWarehouseId"] = warehouse, ["toLocationId"] = location };
        if (unposted is not null) body["unpostedLines"] = unposted;
        return body;
    }
    public static JsonArray Unposted(string line, string reason) => new(new JsonObject { ["shipmentLineNumber"] = line, ["reasonCode"] = reason });

    public Task<(int Status, JsonObject Body)> Receive(Guid id, string key, JsonObject? body = null) => Send("POST", $"/{id}/transition", body ?? Received(), key);

    public async Task<BsonDocument> Stored(Guid id) =>
        await app.Db.GetCollection<BsonDocument>("returns").Find(new BsonDocument("_id", id.ToString())).SingleAsync();
    public async Task<long> Events(Guid id, string type) =>
        await app.Db.GetCollection<BsonDocument>("returns_outbox").CountDocumentsAsync(new BsonDocument { { "ReturnId", id.ToString() }, { "Envelope.eventType", type } });
    public async Task<BsonDocument?> ReceivedAudit(Guid id) =>
        await app.Db.GetCollection<BsonDocument>("returns_audit").Find(new BsonDocument { { "ReturnId", id.ToString() }, { "ToStatus", "Received" } }).FirstOrDefaultAsync();
    public static string Code(JsonObject body) => body["error"]!["code"]!.GetValue<string>();
    public static JsonObject? Details(JsonObject body) => body["error"]!["details"]?.AsObject();
    public static JsonObject Line(JsonObject body, string line) => body["lines"]!.AsArray().Single(l => l!["shipmentLineNumber"]!.GetValue<string>() == line)!.AsObject();
}

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// One isolated world per test: a fresh tenant + legal entity + user, a fresh item / sku registered in the product
/// stub, and bins in two warehouses registered in the location stub under THIS legal entity.
/// </summary>
public sealed class InventoryScenario
{
    public const string PostingDate = "2026-09-30T10:00:00Z";

    private readonly InventoryApiFactory _factory;
    private readonly HttpClient _client;

    public InventoryScenario(
        InventoryApiFactory factory,
        bool lotControlled = false,
        bool serialControlled = false,
        params UomConversionRule[] conversions)
    {
        _factory = factory;
        _client = factory.CreateContractClient();
        factory.TrackTenant(Tenant);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        Warehouse = "wh-" + suffix;
        Warehouse2 = "wh2-" + suffix;
        LocationA = "bin-a-" + suffix;
        LocationB = "bin-b-" + suffix;
        LocationC = "bin-c-" + suffix;
        LotControlled = lotControlled;

        factory.Products.AddItem(Item, lotControlled, serialControlled);
        factory.Products.AddSku(Sku, SkuLevel.Lsku, Item, "EA", conversions);
        factory.Locations.AddBin(LocationA, Warehouse, LegalEntity.ToString());
        factory.Locations.AddBin(LocationB, Warehouse, LegalEntity.ToString());
        factory.Locations.AddBin(LocationC, Warehouse2, LegalEntity.ToString());
        Token = TestTokens.Mint(Tenant, LegalEntity, User, TestTokens.AllPermissions);
    }

    public Guid Tenant { get; } = Guid.NewGuid();
    public Guid LegalEntity { get; } = Guid.NewGuid();
    public Guid User { get; } = Guid.NewGuid();
    public Guid Item { get; } = Guid.NewGuid();
    public Guid Sku { get; } = Guid.NewGuid();
    public string Warehouse { get; }
    public string Warehouse2 { get; }
    public string LocationA { get; }
    public string LocationB { get; }
    public string LocationC { get; }
    public bool LotControlled { get; }
    public string Token { get; }

    public InventoryApiFactory Factory => _factory;

    // ── request bodies ───────────────────────────────────────────────────────────────────────────────────

    public Dictionary<string, object?> Movement(string movementType, string quantity, string uomId = "EA")
    {
        var body = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = "key-" + Guid.NewGuid().ToString("N"),
            ["movementType"] = movementType,
            ["itemId"] = Item.ToString(),
            ["skuId"] = Sku.ToString(),
            ["skuLevel"] = "Lsku",
            ["quantity"] = quantity,
            ["uomId"] = uomId,
            ["sourceModule"] = "MOD-TEST",
            ["sourceType"] = "TEST",
            ["transactionDate"] = PostingDate,
            ["postingDate"] = PostingDate
        };
        if (LotControlled) body["lotId"] = "lot-1";
        return body;
    }

    public Dictionary<string, object?> Receipt(string quantity, string movementType = "OPENING_BALANCE", string? toStatus = null, string? location = null, string uomId = "EA")
    {
        var body = Movement(movementType, quantity, uomId);
        body["toWarehouseId"] = location == LocationC ? Warehouse2 : Warehouse;
        body["toLocationId"] = location ?? LocationA;
        if (toStatus is not null) body["toStockStatus"] = toStatus;
        return body;
    }

    public Dictionary<string, object?> Issue(string quantity, string movementType = "GOODS_ISSUE_SALES", string? fromStatus = null, string? location = null)
    {
        var body = Movement(movementType, quantity);
        body["fromWarehouseId"] = location == LocationC ? Warehouse2 : Warehouse;
        body["fromLocationId"] = location ?? LocationA;
        if (fromStatus is not null) body["fromStockStatus"] = fromStatus;
        return body;
    }

    public Dictionary<string, object?> Reversal(Dictionary<string, object?> original, string originalTransactionId)
    {
        var body = Movement("REVERSAL", (string)original["quantity"]!, (string)original["uomId"]!);
        body.Remove("lotId");
        body["sourceModule"] = ReversalRules.SourceModule;
        body["sourceType"] = ReversalRules.SourceType;
        body["sourceDocumentId"] = originalTransactionId;
        return body;
    }

    // ── HTTP ─────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>POST /movements. The Idempotency-Key header defaults to the body key (as a well-behaved caller sends it).</summary>
    public Task<HttpResponseMessage> PostAsync(
        Dictionary<string, object?> body,
        string? idempotencyKeyHeader = null,
        string? token = null,
        Action<HttpRequestMessage>? configure = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/inventory/movements")
        {
            Content = JsonContent.Create(body)
        };
        var header = idempotencyKeyHeader ?? body.GetValueOrDefault("idempotencyKey") as string;
        if (header is not null) request.Headers.TryAddWithoutValidation("Idempotency-Key", header);
        Authorize(request, token);
        configure?.Invoke(request);
        return _client.SendAsync(request);
    }

    public Task<HttpResponseMessage> GetAsync(string pathAndQuery, string? token = null, Action<HttpRequestMessage>? configure = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, pathAndQuery);
        Authorize(request, token);
        configure?.Invoke(request);
        return _client.SendAsync(request);
    }

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request) => _client.SendAsync(request);

    public async Task<JsonElement> ReadAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    /// <summary>Posts and returns the body, asserting the expected status.</summary>
    public async Task<JsonElement> PostExpectingAsync(Dictionary<string, object?> body, int expectedStatus)
    {
        using var response = await PostAsync(body);
        var json = await ReadAsync(response);
        Xunit.Assert.True((int)response.StatusCode == expectedStatus,
            $"expected {expectedStatus}, got {(int)response.StatusCode}: {json}");
        return json;
    }

    public async Task<decimal> OnHandAsync(string? location = null, string? status = null)
    {
        var query = $"/api/inventory/balance?itemId={Item}&locationId={location ?? LocationA}" + (status is null ? "" : $"&stockStatus={status}");
        using var response = await GetAsync(query);
        var json = await ReadAsync(response);
        return json.GetProperty("rows").EnumerateArray().Sum(r => decimal.Parse(r.GetProperty("onHand").GetString()!, System.Globalization.CultureInfo.InvariantCulture));
    }

    // ── persisted state ──────────────────────────────────────────────────────────────────────────────────

    public List<BsonDocument> Persisted(string collection)
        => _factory.Collection(collection).Find(new BsonDocument("TenantId", Tenant.ToString())).ToList();

    private void Authorize(HttpRequestMessage request, string? token)
    {
        var effective = token ?? Token;
        if (effective.Length > 0)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", effective);
        }
    }
}

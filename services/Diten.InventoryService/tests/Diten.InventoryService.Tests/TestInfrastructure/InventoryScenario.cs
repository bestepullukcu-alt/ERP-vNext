using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// One isolated world per test: a fresh tenant + legal entity + user, a fresh item / sku registered in the product
/// stub, and a REAL location tree under THIS legal entity (MOD-0173-FU02 AC-22): Site → Warehouse → StorageLocation →
/// bins A, B and Site → Warehouse2 → StorageLocation2 → bin C, written as <c>inventory_locations</c> documents with
/// fresh UUIDs. The ledger resolves them through the production in-process directory. The same ids are also put in
/// the pre-FU02 LOCATION stub, which only <see cref="StubLocationMasterApiFactory"/> registers.
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
        : this(factory, SkuLevel.Lsku, lotControlled, serialControlled, conversions)
    {
    }

    /// <summary>
    /// WP-0174-ITEM: the scenario's SKU at <paramref name="level"/>. <see cref="SkuLevel.Item"/> is a material item stocked as its
    /// own SKU (<c>skuId == itemId</c>, PRODUCT-MASTER 1.1 §8.5 Q-1, never serial-tracked).
    /// </summary>
    public InventoryScenario(
        InventoryApiFactory factory,
        SkuLevel level,
        bool lotControlled,
        bool serialControlled = false,
        params UomConversionRule[] conversions)
    {
        Level = level;
        Sku = level == SkuLevel.Item ? Item : Guid.NewGuid();
        _factory = factory;
        _client = factory.CreateContractClient();
        factory.TrackTenant(Tenant);

        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        SiteNode = LocationSeed.Node(Tenant, LegalEntity, LocationType.Site, "SITE-" + suffix, null);
        WarehouseNode = LocationSeed.Node(Tenant, LegalEntity, LocationType.Warehouse, "WH-" + suffix, SiteNode);
        StorageNode = LocationSeed.Node(Tenant, LegalEntity, LocationType.StorageLocation, "SL-" + suffix, WarehouseNode);
        var binA = LocationSeed.Node(Tenant, LegalEntity, LocationType.Bin, "A-" + suffix, StorageNode);
        var binB = LocationSeed.Node(Tenant, LegalEntity, LocationType.Bin, "B-" + suffix, StorageNode);
        Warehouse2Node = LocationSeed.Node(Tenant, LegalEntity, LocationType.Warehouse, "WH2-" + suffix, SiteNode);
        Storage2Node = LocationSeed.Node(Tenant, LegalEntity, LocationType.StorageLocation, "SL2-" + suffix, Warehouse2Node);
        var binC = LocationSeed.Node(Tenant, LegalEntity, LocationType.Bin, "C-" + suffix, Storage2Node);
        LocationSeed.Insert(factory.Database, SiteNode, WarehouseNode, StorageNode, binA, binB, Warehouse2Node, Storage2Node, binC);

        Warehouse = WarehouseNode.Id.ToString("D");
        Warehouse2 = Warehouse2Node.Id.ToString("D");
        LocationA = binA.Id.ToString("D");
        LocationB = binB.Id.ToString("D");
        LocationC = binC.Id.ToString("D");
        LotControlled = lotControlled;

        factory.Products.AddItem(Item, lotControlled, level != SkuLevel.Item && serialControlled);
        factory.Products.AddSku(Sku, level, Item, "EA", conversions);
        factory.Locations.AddBin(LocationA, Warehouse, LegalEntity.ToString());
        factory.Locations.AddBin(LocationB, Warehouse, LegalEntity.ToString());
        factory.Locations.AddBin(LocationC, Warehouse2, LegalEntity.ToString());
        Token = TestTokens.Mint(Tenant, LegalEntity, User, TestTokens.AllPermissions);
    }

    public Guid Tenant { get; } = Guid.NewGuid();
    public Guid LegalEntity { get; } = Guid.NewGuid();
    public Guid User { get; } = Guid.NewGuid();
    public Guid Item { get; } = Guid.NewGuid();
    /// <summary>The scenario SKU — equal to <see cref="Item"/> at level Item.</summary>
    public Guid Sku { get; }

    public SkuLevel Level { get; }
    public string Warehouse { get; }
    public string Warehouse2 { get; }
    public string LocationA { get; }
    public string LocationB { get; }
    public string LocationC { get; }
    public Location SiteNode { get; }
    public Location WarehouseNode { get; }
    public Location StorageNode { get; }
    public Location Warehouse2Node { get; }
    public Location Storage2Node { get; }
    public bool LotControlled { get; }
    public string Token { get; }

    public InventoryApiFactory Factory => _factory;

    /// <summary>One more REAL location of this scenario's company (default: a bin under the first storage location).</summary>
    public string AddLocation(LocationType type = LocationType.Bin, Location? parent = null, bool? stockable = null,
        LocationStatus status = LocationStatus.Active)
    {
        var node = LocationSeed.Node(Tenant, LegalEntity, type, type + "-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            parent ?? StorageNode, stockable, status);
        LocationSeed.Insert(_factory.Database, node);
        return node.Id.ToString("D");
    }

    /// <summary>
    /// A complete Site → Warehouse → StorageLocation → Bin chain of ANOTHER legal entity of the same tenant; returns
    /// (warehouseId, binId). For this scenario's company those ids do not exist.
    /// </summary>
    public (string WarehouseId, string BinId) AddForeignCompanyBin()
    {
        var other = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var site = LocationSeed.Node(Tenant, other, LocationType.Site, "FS-" + suffix, null);
        var warehouse = LocationSeed.Node(Tenant, other, LocationType.Warehouse, "FW-" + suffix, site);
        var storage = LocationSeed.Node(Tenant, other, LocationType.StorageLocation, "FL-" + suffix, warehouse);
        var bin = LocationSeed.Node(Tenant, other, LocationType.Bin, "FB-" + suffix, storage);
        LocationSeed.Insert(_factory.Database, site, warehouse, storage, bin);
        return (warehouse.Id.ToString("D"), bin.Id.ToString("D"));
    }

    // ── request bodies ───────────────────────────────────────────────────────────────────────────────────

    public Dictionary<string, object?> Movement(string movementType, string quantity, string uomId = "EA")
    {
        var body = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = "key-" + Guid.NewGuid().ToString("N"),
            ["movementType"] = movementType,
            ["itemId"] = Item.ToString(),
            ["skuId"] = Sku.ToString(),
            ["skuLevel"] = WireNames.ToWire(Level),
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

    /// <summary>A contract <c>ReservationRequest</c> for this scenario's SKU (quantity in base UoM).</summary>
    public Dictionary<string, object?> Reservation(
        string quantity, string? sourceDocumentId = null, string? sourceLineId = null, string sourceModule = "MOD-0172", string? expiresAt = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = "rsv-" + Guid.NewGuid().ToString("N"),
            ["itemId"] = Item.ToString(),
            ["skuId"] = Sku.ToString(),
            ["skuLevel"] = WireNames.ToWire(Level),
            ["quantity"] = quantity,
            ["reservationType"] = "SALES_ORDER",
            ["sourceModule"] = sourceModule,
            ["priority"] = 10
        };
        if (sourceDocumentId is not null) body["sourceDocumentId"] = sourceDocumentId;
        if (sourceLineId is not null) body["sourceLineId"] = sourceLineId;
        if (expiresAt is not null) body["expiresAt"] = expiresAt;
        return body;
    }

    public Task<HttpResponseMessage> ReserveAsync(Dictionary<string, object?> body, string? token = null, Action<HttpRequestMessage>? configure = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/inventory/reservations") { Content = JsonContent.Create(body) };
        if (body.GetValueOrDefault("idempotencyKey") is string key) request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        Authorize(request, token);
        configure?.Invoke(request);
        return _client.SendAsync(request);
    }

    public async Task<System.Text.Json.JsonElement> ReserveExpectingAsync(Dictionary<string, object?> body, int expectedStatus)
    {
        using var response = await ReserveAsync(body);
        var json = await ReadAsync(response);
        Xunit.Assert.True((int)response.StatusCode == expectedStatus, $"expected {expectedStatus}, got {(int)response.StatusCode}: {json}");
        return json;
    }

    public Task<HttpResponseMessage> ReleaseAsync(string reservationId, string? ifMatch, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/inventory/reservations/{reservationId}/release");
        if (ifMatch is not null) request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        Authorize(request, token);
        return _client.SendAsync(request);
    }

    /// <summary>Issue with a source reference (consumption of a matching reservation, pack ASSUMPTION-0173-15).</summary>
    public Dictionary<string, object?> IssueFor(string quantity, string sourceModule, string sourceDocumentId, string? sourceLineId)
    {
        var body = Issue(quantity);
        body["sourceModule"] = sourceModule;
        body["sourceDocumentId"] = sourceDocumentId;
        if (sourceLineId is not null) body["sourceLineId"] = sourceLineId;
        return body;
    }

    public async Task<(decimal OnHand, decimal Reserved, decimal Available)> AvailabilityAsync()
    {
        var json = await ReadAsync(await GetAsync($"/api/inventory/availability?skuId={Sku}"));
        static decimal D(System.Text.Json.JsonElement e, string name)
            => decimal.Parse(e.GetProperty(name).GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        return (D(json, "onHand"), D(json, "reserved"), D(json, "available"));
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

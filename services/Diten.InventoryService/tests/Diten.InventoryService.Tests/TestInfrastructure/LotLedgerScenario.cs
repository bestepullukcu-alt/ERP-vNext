using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Persistence.Quarantine;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// WP-0174-P2 world: the MOD-0173 <see cref="InventoryScenario"/> (fresh tenant + company, real location tree, the item /
/// Lsku in the product stub) with the item also known to the MOD-0174 SKU directory, and ONE token holding the ledger keys
/// and every <c>trace.*</c> key. Lots are created / released / blocked through the real TRACE API; movements go through the
/// real ledger API; every response is checked against its frozen contract (contract-validating client).
/// </summary>
public sealed class LotLedgerScenario
{
    public const string Reason = "QA karari - sapma DEV-2026-021";

    public LotLedgerScenario(TraceApiFactory factory, bool lotControlled = true, bool serialControlled = false)
    {
        Factory = factory;
        Ledger = new InventoryScenario(factory, lotControlled, serialControlled);
        factory.TrackTraceTenant(Ledger.Tenant);
        factory.Skus.Add(Ledger.Sku, SkuLevel.Lsku, Ledger.Item, TraceScenario.Gtin);
        Token = TestTokens.Mint(Ledger.Tenant, Ledger.LegalEntity, Ledger.User, TestTokens.AllPermissions.Concat(TestTokens.TracePermissions));
        Client = factory.CreateContractClient();
        LotGate.Off(factory.Database, Ledger.Tenant, Ledger.LegalEntity); // MOD-0175 Q4: not a gate world (gate tests switch it on)
    }

    public TraceApiFactory Factory { get; }
    public InventoryScenario Ledger { get; }
    public string Token { get; }
    public HttpClient Client { get; }

    // ── lots (TRACE API) ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>A second Gsku of the same item (lot of another SKU — LOT_SKU_MISMATCH).</summary>
    public Guid AddSecondSku()
    {
        var sku = Guid.NewGuid();
        Factory.Products.AddSku(sku, SkuLevel.Gsku, Ledger.Item);
        Factory.Skus.Add(sku, SkuLevel.Gsku, Ledger.Item, "08699512340209");
        return sku;
    }

    public async Task<string> CreateLotAsync(
        string? expiryDate = null, Guid? sku = null, SkuLevel level = SkuLevel.Lsku, string? manufacturingDate = null, string? retestDate = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = "lot-" + Guid.NewGuid().ToString("N"),
            ["itemId"] = Ledger.Item.ToString("D"),
            ["lotNumber"] = TraceScenario.LotNo("P2"),
            ["skuId"] = (sku ?? Ledger.Sku).ToString("D"),
            ["skuLevel"] = WireNames.ToWire(level)
        };
        if (expiryDate is not null) body["expiryDate"] = expiryDate;
        if (manufacturingDate is not null) body["manufacturingDate"] = manufacturingDate;
        if (retestDate is not null) body["retestDate"] = retestDate;
        var json = await ExpectAsync(Send(HttpMethod.Post, "/api/trace/lots", body, r => r.Headers.TryAddWithoutValidation("Idempotency-Key", (string)body["idempotencyKey"]!)), 201);
        return json.GetProperty("lotId").GetString()!;
    }

    public async Task<JsonElement> SetStatusAsync(string lotId, string toStatus, int expected = 200)
    {
        using var get = await Send(HttpMethod.Get, $"/api/trace/lots/{lotId}", null);
        Assert.Equal(200, (int)get.StatusCode);
        var etag = get.Headers.ETag!.ToString();
        var body = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = "st-" + Guid.NewGuid().ToString("N"),
            ["toStatus"] = toStatus,
            ["reason"] = Reason,
            ["acknowledgeImpact"] = true
        };
        return await ExpectAsync(Send(HttpMethod.Post, $"/api/trace/lots/{lotId}/status", body, r =>
        {
            r.Headers.TryAddWithoutValidation("If-Match", etag);
            r.Headers.TryAddWithoutValidation("Idempotency-Key", (string)body["idempotencyKey"]!);
        }), expected);
    }

    public Task<JsonElement> ReleaseAsync(string lotId) => SetStatusAsync(lotId, "Released");

    /// <summary>MOD-0175 Q4: PATCH the lot's retest date (TRACE updateLot, with a reason — the lot may be Released).</summary>
    public async Task<JsonElement> SetRetestDateAsync(string lotId, DateOnly day, int expected = 200)
    {
        using var get = await Send(HttpMethod.Get, $"/api/trace/lots/{lotId}", null);
        Assert.Equal(200, (int)get.StatusCode);
        var etag = get.Headers.ETag!.ToString();
        var body = new Dictionary<string, object?> { ["retestDate"] = Midnight(day), ["reason"] = Reason };
        return await ExpectAsync(Send(HttpMethod.Patch, $"/api/trace/lots/{lotId}", body, r => r.Headers.TryAddWithoutValidation("If-Match", etag)), expected);
    }

    public static string Midnight(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T00:00:00Z";

    /// <summary>MOD-0175 Q4: one retest-hold sweep of THIS tenant (the hosted sweeper is off in tests).</summary>
    public async Task<RetestHoldSweeper.SweepResult> SweepRetestAsync(DateTimeOffset? now = null)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<RetestHoldSweeper>().SweepAsync(now ?? DateTimeOffset.UtcNow, CancellationToken.None, Ledger.Tenant);
    }

    /// <summary>
    /// CT-Q16 (4): the live SKU position must equal what the PRODUCTION <see cref="ISkuPositionRebuilder"/> computes from the sources.
    /// Reads the live <c>AvailableOnHand</c>, runs the rebuilder's own per-SKU step (<c>RebuildOneAsync</c> — the body of
    /// <c>RebuildAllAsync</c> for one key, in its own snapshot transaction) for THIS scenario's SKU, reads again: (live, rebuilt).
    /// One key instead of the whole shared test database keeps the measurement local (RebuildAllAsync walks every tenant).
    /// </summary>
    public async Task<(decimal Live, decimal Rebuilt)> LiveAndRebuiltAsync()
    {
        var live = SkuPositionAvailableOnHand();
        using var scope = Factory.Services.CreateScope();
        var rebuilder = scope.ServiceProvider.GetRequiredService<ISkuPositionRebuilder>();
        var one = rebuilder.GetType().GetMethod("RebuildOneAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                  ?? throw new InvalidOperationException("SkuPositionRebuilder.RebuildOneAsync not found");
        var key = (Ledger.Tenant.ToString(), Ledger.LegalEntity.ToString(), Ledger.Item.ToString(), Ledger.Sku.ToString(), nameof(SkuLevel.Lsku));
        using var session = await scope.ServiceProvider.GetRequiredService<IMongoClient>().StartSessionAsync();
        await session.WithTransactionAsync((s, ct) => (Task<bool>)one.Invoke(rebuilder, [s, key, ct])!,
            new TransactionOptions(ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority));
        return (live, SkuPositionAvailableOnHand());
    }

    public async Task AssertConsistentAsync(string because)
    {
        var (live, rebuilt) = await LiveAndRebuiltAsync();
        Assert.True(live == rebuilt, $"{because}: live AvailableOnHand {live} != rebuilder {rebuilt}");
        Assert.True(live == ExpectedSkuAvailableOnHand(), $"{because}: live AvailableOnHand {live} != oracle {ExpectedSkuAvailableOnHand()}");
    }

    public Task<JsonElement> BlockAsync(string lotId, int expected = 200) => SetStatusAsync(lotId, "Blocked", expected);

    public async Task<JsonElement> GetTraceAsync(string pathAndQuery, int expected = 200)
        => await ExpectAsync(Send(HttpMethod.Get, pathAndQuery, null), expected);

    // ── movements (ledger API) ───────────────────────────────────────────────────────────────────────────

    public Dictionary<string, object?> Receipt(string quantity, string? lotId, string? location = null, string movementType = "GOODS_RECEIPT_PO",
        string? toStatus = null, string[]? serials = null, string? postingDate = null)
        => WithLot(Ledger.Receipt(quantity, movementType, toStatus, location), lotId, serials, postingDate);

    public Dictionary<string, object?> Issue(string quantity, string? lotId, string? location = null, string movementType = "GOODS_ISSUE_SALES",
        string? fromStatus = null, string[]? serials = null, string? postingDate = null)
        => WithLot(Ledger.Issue(quantity, movementType, fromStatus, location), lotId, serials, postingDate);

    /// <summary>LOCATION_TRANSFER A → B (same warehouse).</summary>
    public Dictionary<string, object?> Transfer(string quantity, string? lotId, string[]? serials = null)
    {
        var body = Ledger.Movement("LOCATION_TRANSFER", quantity);
        body["fromWarehouseId"] = Ledger.Warehouse;
        body["fromLocationId"] = Ledger.LocationA;
        body["toWarehouseId"] = Ledger.Warehouse;
        body["toLocationId"] = Ledger.LocationB;
        return WithLot(body, lotId, serials, null);
    }

    /// <summary>STATUS_TRANSFER AVAILABLE → QUALITY_INSPECTION at A.</summary>
    public Dictionary<string, object?> StatusTransfer(string quantity, string? lotId)
    {
        var body = Ledger.Movement("STATUS_TRANSFER", quantity);
        body["fromWarehouseId"] = Ledger.Warehouse;
        body["fromLocationId"] = Ledger.LocationA;
        body["fromStockStatus"] = "AVAILABLE";
        body["toWarehouseId"] = Ledger.Warehouse;
        body["toLocationId"] = Ledger.LocationA;
        body["toStockStatus"] = "QUALITY_INSPECTION";
        return WithLot(body, lotId, null, null);
    }

    public Dictionary<string, object?> Reversal(Dictionary<string, object?> original, string originalTransactionId)
        => Ledger.Reversal(original, originalTransactionId);

    public async Task<JsonElement> PostAsync(Dictionary<string, object?> body, int expected)
    {
        using var response = await Ledger.PostAsync(body, token: Token);
        var json = await Ledger.ReadAsync(response);
        Assert.True((int)response.StatusCode == expected, $"expected {expected}, got {(int)response.StatusCode}: {json}");
        return json;
    }

    public async Task<string> PostOkAsync(Dictionary<string, object?> body)
        => (await PostAsync(body, 201)).GetProperty("transactionId").GetString()!;

    public async Task<string> RefusedAsync(Dictionary<string, object?> body, string code)
    {
        var json = await PostAsync(body, 422);
        Assert.Equal(code, json.GetProperty("error").GetProperty("code").GetString());
        return json.GetProperty("error").GetProperty("message").GetString()!;
    }

    public async Task<decimal> AvailableAsync() => (await Ledger.AvailabilityAsync()).Available;

    public async Task<JsonElement> ReserveAsync(string quantity, int expected)
    {
        using var response = await Ledger.ReserveAsync(Ledger.Reservation(quantity), token: Token);
        var json = await Ledger.ReadAsync(response);
        Assert.True((int)response.StatusCode == expected, $"expected {expected}, got {(int)response.StatusCode}: {json}");
        return json;
    }

    // ── persisted state (read-only) ──────────────────────────────────────────────────────────────────────

    public List<BsonDocument> Persisted(string collection) => Ledger.Persisted(collection);

    public decimal LotPositionOnHand(string lotId)
        => Persisted("inventory_lot_positions").Where(d => d["LotId"].AsString == lotId).Select(d => d["OnHand"].ToDecimal()).SingleOrDefault();

    public decimal BalanceOnHandOfLot(string lotId)
        => Persisted("inventory_balances").Where(d => d["LotId"].IsString && d["LotId"].AsString == lotId).Sum(d => d["OnHand"].ToDecimal());

    public decimal SkuPositionAvailableOnHand()
        => Persisted("inventory_sku_positions").Where(d => d["SkuId"].AsString == Ledger.Sku.ToString()).Select(d => d["AvailableOnHand"].ToDecimal()).SingleOrDefault();

    /// <summary>
    /// The CT-D1 (a) oracle: Σ OnHand of the SKU's AVAILABLE rows whose lot is not Blocked / Expired — and, since MOD-0175 Q4 (CT-Q16),
    /// carries no stored retest hold (<c>RetestHeldAtUtc</c>).
    /// </summary>
    public decimal ExpectedSkuAvailableOnHand()
    {
        var held = Persisted("trace_lots").Where(d => d["Status"].AsString is "Blocked" or "Expired" || RetestHeld(d)).Select(d => d["_id"].AsString).ToHashSet();
        return Persisted("inventory_balances")
            .Where(d => d["SkuId"].AsString == Ledger.Sku.ToString() && d["StockStatus"].AsString == "Available")
            .Where(d => !d["LotId"].IsString || !held.Contains(d["LotId"].AsString))
            .Sum(d => d["OnHand"].ToDecimal());
    }

    public static bool RetestHeld(BsonDocument lot) => lot.Contains("RetestHeldAtUtc") && !lot["RetestHeldAtUtc"].IsBsonNull;

    public BsonDocument LotDocument(string lotId) => Assert.Single(Persisted("trace_lots"), d => d["_id"].AsString == lotId);

    public List<BsonDocument> Serials() => Persisted("trace_serials");

    public BsonDocument Serial(string number) => Assert.Single(Serials(), d => d["Number"].AsString == number);

    public static decimal D(string wire) => decimal.Parse(wire, CultureInfo.InvariantCulture);

    // ── plumbing ─────────────────────────────────────────────────────────────────────────────────────────

    private static Dictionary<string, object?> WithLot(Dictionary<string, object?> body, string? lotId, string[]? serials, string? postingDate)
    {
        if (lotId is null) body.Remove("lotId");
        else body["lotId"] = lotId;
        if (serials is not null) body["serialIds"] = serials;
        if (postingDate is not null)
        {
            body["postingDate"] = postingDate;
            body["transactionDate"] = postingDate;
        }

        return body;
    }

    private Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body, Action<HttpRequestMessage>? configure = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);
        configure?.Invoke(request);
        return Client.SendAsync(request);
    }

    private static async Task<JsonElement> ExpectAsync(Task<HttpResponseMessage> call, int status)
    {
        using var response = await call;
        var json = await TraceScenario.Read(response);
        Assert.True((int)response.StatusCode == status, $"expected {status}, got {(int)response.StatusCode}: {json}");
        return json;
    }
}

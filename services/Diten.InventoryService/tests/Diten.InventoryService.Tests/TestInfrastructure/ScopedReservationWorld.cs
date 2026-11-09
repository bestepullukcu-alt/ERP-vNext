using System.Globalization;
using System.Text.Json;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Traceability.Rules;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// MOD-0173-FU03 DL-1 world: the WP-0174-P2 <see cref="LotLedgerScenario"/> (fresh tenant + company, the real location tree — warehouse W with
/// bins A / B, warehouse W2 with bin C — registered lots through the real TRACE API, every response checked against its contract) plus the
/// scoped reservation calls and an INDEPENDENT oracle of the three projection levels computed from the raw documents (balance rows, lots,
/// reservations), never through the service.
/// </summary>
public sealed class ScopedReservationWorld
{
    public ScopedReservationWorld(TraceApiFactory factory, bool lotControlled = true)
        : this(new LotLedgerScenario(factory, lotControlled))
    {
    }

    private ScopedReservationWorld(LotLedgerScenario lots) => Lots = lots;

    /// <summary>The same calls and oracle over an existing lot world (e.g. a <see cref="MigrationScenario"/>'s).</summary>
    public static ScopedReservationWorld Of(LotLedgerScenario lots) => new(lots);

    public LotLedgerScenario Lots { get; }
    public InventoryScenario Ledger => Lots.Ledger;
    public TraceApiFactory Factory => Lots.Factory;
    public string W => Ledger.Warehouse;
    public string W2 => Ledger.Warehouse2;

    // ── stock ────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A registered lot of the scenario SKU (Draft — accepted by reservations, ASSUMPTION-0174-07; no expiry date, so no FEFO order).</summary>
    public Task<string> LotAsync() => Lots.CreateLotAsync();

    /// <summary>Receipt into W (bin A by default) or, with <see cref="InventoryScenario.LocationC"/>, into W2.</summary>
    public Task<string> ReceiveAsync(string quantity, string? lotId, string? location = null)
        => Lots.PostOkAsync(Lots.Receipt(quantity, lotId, location));

    public Dictionary<string, object?> Issue(string quantity, string? lotId, string? location = null, string sourceModule = "MOD-0183",
        string? sourceDocumentId = null, string? sourceLineId = null)
    {
        var body = Lots.Issue(quantity, lotId, location);
        body["sourceModule"] = sourceModule;
        if (sourceDocumentId is not null) body["sourceDocumentId"] = sourceDocumentId;
        if (sourceLineId is not null) body["sourceLineId"] = sourceLineId;
        return body;
    }

    /// <summary>One more Site → Warehouse → StorageLocation → Bin of the world's company (W3); returns (warehouseId, binId).</summary>
    public (string WarehouseId, string BinId) AddWarehouse()
    {
        var s = Ledger;
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var warehouse = LocationSeed.Node(s.Tenant, s.LegalEntity, LocationType.Warehouse, "WH3-" + suffix, s.SiteNode);
        var storage = LocationSeed.Node(s.Tenant, s.LegalEntity, LocationType.StorageLocation, "SL3-" + suffix, warehouse);
        var bin = LocationSeed.Node(s.Tenant, s.LegalEntity, LocationType.Bin, "D-" + suffix, storage);
        LocationSeed.Insert(Factory.Database, warehouse, storage, bin);
        return (warehouse.Id.ToString("D"), bin.Id.ToString("D"));
    }

    /// <summary>A receipt into a bin of a warehouse the scenario does not name (e.g. <see cref="AddWarehouse"/>'s).</summary>
    public Task<string> ReceiveIntoAsync(string quantity, string? lotId, string warehouseId, string binId)
    {
        var body = Lots.Receipt(quantity, lotId);
        body["toWarehouseId"] = warehouseId;
        body["toLocationId"] = binId;
        return Lots.PostOkAsync(body);
    }

    /// <summary>WAREHOUSE_TRANSFER W (bin A) → W2 (bin C).</summary>
    public Dictionary<string, object?> WarehouseTransfer(string quantity, string? lotId)
    {
        var body = Ledger.Movement("WAREHOUSE_TRANSFER", quantity);
        body["fromWarehouseId"] = W;
        body["fromLocationId"] = Ledger.LocationA;
        body["toWarehouseId"] = W2;
        body["toLocationId"] = Ledger.LocationC;
        if (lotId is null) body.Remove("lotId");
        else body["lotId"] = lotId;
        return body;
    }

    public Task<JsonElement> PostAsync(Dictionary<string, object?> body, int expected) => Lots.PostAsync(body, expected);

    /// <summary>INVENTORY 1.7.0: the same movement naming a reservation by <c>reservationId</c>.</summary>
    public static Dictionary<string, object?> Naming(Dictionary<string, object?> body, string reservationId)
    {
        body["reservationId"] = reservationId;
        return body;
    }

    /// <summary>The REVERSAL of a posted movement; <paramref name="restore"/> null = no <c>restoreReservation</c> member at all.</summary>
    public Dictionary<string, object?> Reversal(Dictionary<string, object?> original, string transactionId, bool? restore = null)
    {
        var body = Ledger.Reversal(original, transactionId);
        if (restore is { } flag) body["restoreReservation"] = flag;
        return body;
    }

    // ── reservations ─────────────────────────────────────────────────────────────────────────────────────

    public Dictionary<string, object?> Reservation(
        string quantity, string? warehouseId = null, string? lotId = null, string sourceModule = "MOD-0178", string? sourceDocumentId = null,
        string? sourceLineId = null, string? expiresAt = null)
    {
        var body = Ledger.Reservation(quantity, sourceDocumentId, sourceLineId, sourceModule, expiresAt);
        if (warehouseId is not null) body["warehouseId"] = warehouseId;
        if (lotId is not null) body["lotId"] = lotId;
        return body;
    }

    public async Task<JsonElement> ReserveAsync(Dictionary<string, object?> body, int expected)
    {
        using var response = await Ledger.ReserveAsync(body, token: Lots.Token);
        var json = await Ledger.ReadAsync(response);
        Assert.True((int)response.StatusCode == expected, $"expected {expected}, got {(int)response.StatusCode}: {json}");
        return json;
    }

    public async Task<string> ReserveOkAsync(string quantity, string? warehouseId = null, string? lotId = null, string sourceModule = "MOD-0178",
        string? sourceDocumentId = null, string? sourceLineId = null, string? expiresAt = null)
        => (await ReserveAsync(Reservation(quantity, warehouseId, lotId, sourceModule, sourceDocumentId, sourceLineId, expiresAt), 201))
            .GetProperty("reservationId").GetString()!;

    public async Task<JsonElement> GetReservationAsync(string id, int expected, string? token = null)
    {
        using var response = await Ledger.GetAsync($"/api/inventory/reservations/{id}", token ?? Lots.Token);
        var json = await Ledger.ReadAsync(response);
        Assert.True((int)response.StatusCode == expected, $"expected {expected}, got {(int)response.StatusCode}: {json}");
        return json;
    }

    public async Task<JsonElement> ReleaseAsync(string id, string etag, int expected)
    {
        using var response = await Ledger.ReleaseAsync(id, etag, Lots.Token);
        var json = await Ledger.ReadAsync(response);
        Assert.True((int)response.StatusCode == expected, $"expected {expected}, got {(int)response.StatusCode}: {json}");
        return json;
    }

    public static string Code(JsonElement error) => error.GetProperty("error").GetProperty("code").GetString()!;

    public static string Detail(JsonElement error, string name) => error.GetProperty("error").GetProperty("details").GetProperty(name).GetString()!;

    // ── persisted state (read-only) ──────────────────────────────────────────────────────────────────────

    /// <summary>This tenant's documents of <paramref name="collection"/> — of THIS company where the document carries one.</summary>
    public List<BsonDocument> Persisted(string collection)
        => Ledger.Persisted(collection)
            .Where(d => !d.Contains("LegalEntityId") || d["LegalEntityId"].IsBsonNull || d["LegalEntityId"].AsString == Ledger.LegalEntity.ToString())
            .ToList();

    public List<BsonDocument> Reservations() => Persisted("inventory_reservations");

    public BsonDocument ReservationDocument(string id) => Assert.Single(Reservations(), d => d["_id"].AsString == id);

    public (decimal OnHand, decimal Reserved) SkuPosition()
    {
        var doc = Persisted("inventory_sku_positions").SingleOrDefault(d => d["SkuId"].AsString == Ledger.Sku.ToString());
        return doc is null ? (0m, 0m) : (doc["AvailableOnHand"].ToDecimal(), doc["Reserved"].ToDecimal());
    }

    /// <summary>The warehouse (<paramref name="lotId"/> null) or warehouse + lot position; (0, 0) when none exists.</summary>
    public (decimal OnHand, decimal Reserved) Scope(string warehouseId, string? lotId = null)
    {
        var level = lotId is null ? "Warehouse" : "Lot";
        var lot = lotId ?? Guid.Empty.ToString();
        var doc = Persisted("inventory_reservation_positions")
            .SingleOrDefault(d => d["Level"].AsString == level && d["WarehouseId"].AsString == warehouseId && d["LotId"].AsString == lot);
        return doc is null ? (0m, 0m) : (doc["AvailableOnHand"].ToDecimal(), doc["Reserved"].ToDecimal());
    }

    /// <summary>The raw position documents of this tenant (SKU and scope), for byte-equality checks.</summary>
    public string PositionsSnapshot()
        => string.Join("\n", Persisted("inventory_sku_positions").Concat(Persisted("inventory_reservation_positions"))
            .Select(d => d.ToJson()).OrderBy(x => x, StringComparer.Ordinal));

    public int Count(string collection, Func<BsonDocument, bool>? where = null) => Persisted(collection).Count(where ?? (_ => true));

    /// <summary>
    /// FU03 §8.2 invariant on EVERY level, from the raw documents: Σ AVAILABLE row OnHand (held lots excluded) == AvailableOnHand and
    /// Σ ACTIVE containing remaining == Reserved — the SKU position and every warehouse / warehouse + lot position (a position no source
    /// names must read 0 / 0).
    /// </summary>
    public void AssertInvariant(string because = "")
    {
        var held = Persisted("trace_lots").Where(d => d["Status"].AsString is "Blocked" or "Expired" || LotLedgerScenario.RetestHeld(d))
            .Select(d => d["_id"].AsString).ToHashSet(StringComparer.Ordinal);
        var rows = Persisted("inventory_balances")
            .Where(d => d["SkuId"].AsString == Ledger.Sku.ToString() && d["StockStatus"].AsString == "Available" && !d["IsDeleted"].AsBoolean)
            .Where(d => !d["LotId"].IsString || !held.Contains(d["LotId"].AsString))
            .ToList();
        var active = Reservations().Where(d => d["SkuId"].AsString == Ledger.Sku.ToString() && d["Status"].AsString == "Active").ToList();

        var expected = new Dictionary<(string Level, string Warehouse, string Lot), (decimal OnHand, decimal Reserved)>();
        void Add((string, string, string) key, decimal onHand, decimal reserved)
        {
            var c = expected.TryGetValue(key, out var v) ? v : (0m, 0m);
            expected[key] = (c.Item1 + onHand, c.Item2 + reserved);
        }

        foreach (var row in rows.Where(r => r["WarehouseId"].IsString))
        {
            Add(("Warehouse", row["WarehouseId"].AsString, Guid.Empty.ToString()), row["OnHand"].ToDecimal(), 0m);
            if (row["LotId"].IsString && LotPostingRules.IsRegisteredLotForm(row["LotId"].AsString, out _))
            {
                Add(("Lot", row["WarehouseId"].AsString, row["LotId"].AsString), row["OnHand"].ToDecimal(), 0m);
            }
        }

        foreach (var r in active.Where(r => r.Contains("WarehouseId") && r["WarehouseId"].IsString))
        {
            Add(("Warehouse", r["WarehouseId"].AsString, Guid.Empty.ToString()), 0m, r["RemainingQuantity"].ToDecimal());
            if (r.Contains("LotId") && r["LotId"].IsString)
            {
                Add(("Lot", r["WarehouseId"].AsString, r["LotId"].AsString), 0m, r["RemainingQuantity"].ToDecimal());
            }
        }

        var sku = SkuPosition();
        Assert.True(sku == (rows.Sum(r => r["OnHand"].ToDecimal()), active.Sum(r => r["RemainingQuantity"].ToDecimal())),
            $"{because}: SKU position {sku} != oracle ({rows.Sum(r => r["OnHand"].ToDecimal())}, {active.Sum(r => r["RemainingQuantity"].ToDecimal())})");

        var positions = Persisted("inventory_reservation_positions").Where(d => d["SkuId"].AsString == Ledger.Sku.ToString())
            .ToDictionary(d => (d["Level"].AsString, d["WarehouseId"].AsString, d["LotId"].AsString), d => (d["AvailableOnHand"].ToDecimal(), d["Reserved"].ToDecimal()));
        foreach (var key in expected.Keys.Union(positions.Keys))
        {
            var want = expected.TryGetValue(key, out var e) ? e : (0m, 0m);
            var have = positions.TryGetValue(key, out var p) ? p : (0m, 0m);
            Assert.True(want == have, $"{because}: position {key} is {have}, the sources say {want}");
        }
    }

    public static decimal D(string wire) => decimal.Parse(wire, CultureInfo.InvariantCulture);

    public static string Later(TimeSpan by) => DateTimeOffset.UtcNow.Add(by).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
}

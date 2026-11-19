using System.Globalization;
using System.Text.Json;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Expiry.Entities;
using Diten.InventoryService.Domain.Expiry.Enums;
using Diten.InventoryService.Domain.Expiry.Rules;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Persistence.Warehouse;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// One isolated S2 outbound world (DL-4) on top of <see cref="PutawayScenario"/> (fresh tenant + company + user, the REAL location tree — bins
/// <see cref="A01"/> / <see cref="A02"/> are pickable, the Receiving zone with bin <see cref="R01"/>, company code <c>GMG</c>) plus a Staging zone
/// with bin <see cref="S01"/> (the role from the REAL location-role API), registered lots with expiry dates (TRACE <c>trace_lots</c> documents,
/// Released — the same documents the TRACE API writes), stock put in by the REAL ledger API, other outbounds' reservations through the REAL
/// INVENTORY reservation API, the company's FEFO policy row, and calls to the REAL outbound API with a planner token.
/// </summary>
public sealed class OutboundScenario
{
    public const string OutboundRead = "warehouse.outbound-shipments.read";
    public const string OutboundCreate = "warehouse.outbound-shipments.create";
    public const string OutboundUpdate = "warehouse.outbound-shipments.update";
    public const string Reason10 = "Musteri-01";

    public OutboundScenario(WarehouseApiFactory factory, bool lotControlled = true, bool serialControlled = false, SkuLevel level = SkuLevel.Lsku)
    {
        P = new PutawayScenario(factory, lotControlled, serialControlled, level);
        _serial = serialControlled;
        StagingZone = P.W.Node(LocationType.Zone);
        S01Node = P.W.Node(LocationType.Bin, StagingZone);
        Planner = P.W.TokenFor(User, OutboundRead, OutboundCreate, OutboundUpdate, PutawayScenario.TaskRead, Read);
    }

    private readonly bool _serial;

    public PutawayScenario P { get; }

    public WarehouseApiFactory Factory => P.Factory;

    public Guid Tenant => P.Tenant;

    public Guid LegalEntity => P.LegalEntity;

    public Guid User => P.User;

    public Guid Item => P.Item;

    public Guid Sku => P.Sku;

    public Guid Warehouse => P.Warehouse;

    public Guid A01 => P.A01;

    /// <summary>Bin B of the scenario — code <c>B-…</c>, so it comes after <see cref="A01"/> in the plan's location-code order.</summary>
    public Guid A02 => P.B01;

    public Guid R01 => P.R01;

    public Location StagingZone { get; }

    public Location S01Node { get; }

    public Guid S01 => S01Node.Id;

    public string Planner { get; }

    public string TokenFor(params string[] permissions) => P.W.TokenFor(User, permissions);

    /// <summary>Receiving on the Receiving zone, Staging on the Staging zone (S-01 inherits it).</summary>
    public async Task<OutboundScenario> ReadyAsync()
    {
        await P.ReadyAsync();
        await P.W.CreatedAsync(StagingZone.Id, ["Staging"]);
        return this;
    }

    // ── lots, stock, other reservations, policy ─────────────────────────────────────────────────────────

    /// <summary>A registered, Released lot of the scenario SKU with this expiry (TRACE's document, written directly).</summary>
    public string Lot(string expiry, LotStatus status = LotStatus.Released, Guid? sku = null)
    {
        var number = "L-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var lot = new Lot
        {
            Id = Guid.NewGuid(), TenantId = Tenant, LegalEntityId = LegalEntity, CreatedAt = DateTimeOffset.UtcNow, Version = 1,
            ItemId = Item, SkuId = sku ?? Sku, SkuLevel = P.Inventory.Level, LotNumber = number, IdentityKey = $"{Tenant:N}|{sku ?? Sku:N}|{number}",
            ExpiryDate = DateTimeOffset.Parse(expiry + "T00:00:00Z", CultureInfo.InvariantCulture), Status = status, TrackingMode = _serial ? LotTrackingMode.LotAndSerial : LotTrackingMode.Lot,
            CreationSource = "TEST", CreatedBy = User
        };
        Factory.Database.GetCollection<Lot>("trace_lots").InsertOne(lot);
        return lot.Id.ToString("D");
    }

    /// <summary>OPENING_BALANCE of <paramref name="quantity"/> into <paramref name="location"/> (lot as given; null = no lot member).</summary>
    public async Task ReceiveAsync(decimal quantity, Guid location, string? lotId)
    {
        var body = P.Inventory.Receipt(PutawayScenario.Q(quantity), location: location.ToString("D"));
        if (lotId is null) body.Remove("lotId");
        else body["lotId"] = lotId;
        await P.Inventory.PostExpectingAsync(body, 201);
    }

    /// <summary>Another outbound's FU03 reservation of <paramref name="quantity"/> on the warehouse (+ lot) through the REAL reservation API.</summary>
    public async Task<string> ReserveOtherAsync(decimal quantity, string? lotId)
    {
        var body = P.Inventory.Reservation(PutawayScenario.Q(quantity), "OB-OTHER-" + Guid.NewGuid().ToString("N")[..6], "1", "MOD-0178");
        body["warehouseId"] = Warehouse.ToString("D");
        if (lotId is not null) body["lotId"] = lotId;
        using var response = await P.Inventory.ReserveAsync(body);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True((int)response.StatusCode == 201, $"other reservation: {(int)response.StatusCode} {text}");
        return JsonDocument.Parse(text).RootElement.GetProperty("reservationId").GetString()!;
    }

    /// <summary>The company's FEFO outbound mode (MOD-0176 policy row; none = the defaults, RequireReason).</summary>
    public void Policy(FefoOutboundMode mode)
    {
        var policy = ExpiryPolicyRules.Defaults();
        policy.Id = Guid.NewGuid();
        policy.TenantId = Tenant;
        policy.LegalEntityId = LegalEntity;
        policy.Version = 1;
        policy.CreatedAt = DateTimeOffset.UtcNow;
        policy.FefoOutboundMode = mode;
        Factory.Database.GetCollection<ExpiryPolicy>("expiry_policies").InsertOne(policy);
    }

    // ── outbound API ────────────────────────────────────────────────────────────────────────────────────

    public static Dictionary<string, object?> Line(string orderLineId, decimal quantity, Guid item, Guid? sku, SkuLevel level, string? lotOverride = null,
        string? reason = null, string uom = "EA")
    {
        var itemBody = new Dictionary<string, object?> { ["itemId"] = item.ToString("D"), ["skuLevel"] = WireNames.ToWire(level) };
        if (sku is { } s) itemBody["skuId"] = s.ToString("D");
        var line = new Dictionary<string, object?>
        {
            ["orderLineId"] = orderLineId, ["item"] = itemBody, ["uomId"] = uom, ["quantity"] = PutawayScenario.Q(quantity)
        };
        if (lotOverride is not null)
        {
            var o = new Dictionary<string, object?> { ["lotId"] = lotOverride };
            if (reason is not null) o["fefoBypassReason"] = reason;
            line["lotOverride"] = o;
        }

        return line;
    }

    /// <summary>A line of the scenario SKU.</summary>
    public Dictionary<string, object?> Line(decimal quantity, string? lotOverride = null, string? reason = null, string? orderLineId = null)
        => Line(orderLineId ?? "SO-1/" + Guid.NewGuid().ToString("N")[..4], quantity, Item, P.Inventory.Level == SkuLevel.Item ? null : Sku, P.Inventory.Level,
            lotOverride, reason);

    public Dictionary<string, object?> Body(params Dictionary<string, object?>[] lines)
        => new()
        {
            ["idempotencyKey"] = NewKey(),
            ["warehouseId"] = Warehouse.ToString("D"),
            ["orderRef"] = "SO-2026-0001",
            ["shipTo"] = new Dictionary<string, object?> { ["name"] = "Ada Hastanesi", ["country"] = "tr", ["city"] = "Ankara", ["line1"] = "Atatürk Blv. 1" },
            ["note"] = "DL-4",
            ["lines"] = lines
        };

    public Task<HttpResponseMessage> CreateAsync(Dictionary<string, object?> body, string? token = null)
        => P.W.Send(HttpMethod.Post, "/api/warehouse/outbound-shipments", body, (string)body["idempotencyKey"]!, null, token ?? Planner);

    public async Task<JsonElement> CreatedAsync(params Dictionary<string, object?>[] lines) => await Expect(CreateAsync(Body(lines)), 201);

    public Task<HttpResponseMessage> PreviewAsync(Dictionary<string, object?> body, string? token = null)
    {
        var copy = new Dictionary<string, object?>(body);
        copy.Remove("idempotencyKey");
        return P.W.Send(HttpMethod.Post, "/api/warehouse/outbound-shipments/allocation-preview", copy, null, null, token ?? Planner);
    }

    public Task<HttpResponseMessage> UpdateAsync(string outboundId, Dictionary<string, object?> change, string ifMatch, string? key = null, string? token = null)
    {
        var body = new Dictionary<string, object?>(change) { ["idempotencyKey"] = key ?? NewKey() };
        return P.W.Send(HttpMethod.Put, $"/api/warehouse/outbound-shipments/{outboundId}", body, (string)body["idempotencyKey"]!, ifMatch, token ?? Planner);
    }

    public Task<HttpResponseMessage> ViewAsync(string outboundId, string? token = null)
        => P.W.GetAsync($"/api/warehouse/outbound-shipments/{outboundId}/view", token ?? Planner);

    public Task<HttpResponseMessage> SearchAsync(string query = "", string? token = null)
        => P.W.GetAsync($"/api/warehouse/outbound-shipments/search?pageSize=200{query}", token ?? Planner);

    public Task<HttpResponseMessage> EventsAsync(string outboundId, string? token = null)
        => P.W.GetAsync($"/api/warehouse/outbound-shipments/{outboundId}/events?pageSize=200", token ?? Planner);

    /// <summary>One maintenance tick for THIS tenant at the host's clock.</summary>
    public async Task<OutboundMaintenanceTick> MaintainAsync()
        => await Factory.Services.GetRequiredService<OutboundMaintenance>().RunOnceAsync(Factory.Clock.GetUtcNow(), CancellationToken.None, [Tenant]);

    // ── stored rows / readers ───────────────────────────────────────────────────────────────────────────

    public static string Id(JsonElement outbound) => outbound.GetProperty("outboundId").GetString()!;

    public static List<JsonElement> Allocations(JsonElement outbound)
        => outbound.GetProperty("lines").EnumerateArray().SelectMany(l => l.GetProperty("allocations").EnumerateArray()).ToList();

    public static List<(string? Lot, decimal Quantity, int? Rank)> AllocationSummary(JsonElement outbound)
        => Allocations(outbound).Select(a => (a.TryGetProperty("lotId", out var l) ? l.GetString() : null, PutawayScenario.D(a, "quantity"),
            a.TryGetProperty("fefoRank", out var r) ? r.GetInt32() : (int?)null)).ToList();

    public static List<(Guid From, decimal Planned)> Tasks(JsonElement outbound)
        => outbound.GetProperty("pickTasks").EnumerateArray()
            .Select(t => (Guid.Parse(t.GetProperty("fromLocationId").GetString()!), PutawayScenario.D(t, "plannedQuantity"))).ToList();

    public List<BsonDocument> StoredOutbounds() => P.W.Stored(WarehouseCollections.OutboundShipments);

    public BsonDocument StoredOutbound(string outboundId) => Assert.Single(StoredOutbounds(), o => o["_id"].ToString() == outboundId);

    public List<BsonDocument> OutboundEvents(string? outboundId = null)
        => P.W.Stored(WarehouseCollections.OutboundEvents).Where(e => outboundId is null || e["OutboundId"].ToString() == outboundId)
            .OrderBy(e => e["Seq"].AsInt32).ToList();

    public List<BsonDocument> PickTasks(string? outboundId = null)
        => P.W.Stored(WarehouseCollections.Tasks).Where(t => t["Kind"].AsString == "Pick" && (outboundId is null || t["OutboundId"].ToString() == outboundId)).ToList();

    /// <summary>The FU03 reservations 0178 made (source module MOD-0178, type WAREHOUSE_OUTBOUND) — of one outbound when given.</summary>
    public List<BsonDocument> Reservations(string? outboundId = null)
        => P.Inventory.Persisted("inventory_reservations")
            .Where(r => r["ReservationType"].AsString == "WAREHOUSE_OUTBOUND" && (outboundId is null || (r.Contains("SourceDocumentId") && r["SourceDocumentId"].ToString() == outboundId)))
            .ToList();

    /// <summary>FU03 <c>GetAsync</c> through the REAL in-process port in this company's scope (what MOD-0178 itself would read).</summary>
    public async Task<Application.Features.Inventory.ReservationViewDto?> ReservationViewAsync(string reservationId)
    {
        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetScope(Tenant, LegalEntity, User);
        scope.ServiceProvider.GetRequiredService<ICorrelationContext>().Set(Guid.NewGuid());
        return await scope.ServiceProvider.GetRequiredService<Application.Features.Inventory.Ports.IInventoryReservationPort>().GetAsync(Guid.Parse(reservationId));
    }

    public (int Forwarded, int Abandoned) Central(string requestType) => P.Central(requestType);

    public List<BsonDocument> CentralRows(string requestType) => P.W.CentralRows(requestType);

    public static string Code(JsonElement error) => error.GetProperty("error").GetProperty("code").GetString()!;

    public static JsonElement Details(JsonElement error) => error.GetProperty("error").GetProperty("details");
}

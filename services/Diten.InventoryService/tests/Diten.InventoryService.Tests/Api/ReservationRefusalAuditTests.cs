using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// MOD-0173-FU03 AC-15 / AC-40 (AUD-001 §3, §4.4, §21) — every refusal of a create and every reservation-guard refusal of an issue (no
/// <c>reservationId</c>, DL-1) leaves exactly ONE row in <c>inventory_audit</c> with <c>Outcome Failed</c>, the code, the actor (and the
/// actor's name only when the actor is the token user), the scope and the numbers; no reservation, no movement, no central event. A request
/// whose body cannot be read never reaches the handler: no row. A row-level INSUFFICIENT_STOCK (negative-stock guard, no level) writes none.
/// </summary>
public sealed class ReservationRefusalAuditTests : IClassFixture<TraceApiFactory>
{
    private const string ActorName = "Ayşe Rezervasyon";
    private readonly TraceApiFactory _factory;

    public ReservationRefusalAuditTests(TraceApiFactory factory) => _factory = factory;

    /// <summary>AC-15 — sabotage: the refusal write removed → a code leaves no row, red.</summary>
    [Fact]
    public async Task Create_refusal_writes_one_row_per_code()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        await w.ReceiveAsync("10", lot);
        var named = Named(w);
        var recalled = await w.LotAsync();
        await RecallBlock.AddAsync(_factory, w.Ledger.Tenant, w.Ledger.LegalEntity, recalled);
        var reused = w.Reservation("1", w.W, lot);
        await w.ReserveAsync(reused, 201);
        var (foreignWarehouse, _) = w.Ledger.AddForeignCompanyBin();

        var cases = new (Dictionary<string, object?> Body, int Status, string Code, string? Level)[]
        {
            (w.Reservation("11", w.W, lot), 422, "INSUFFICIENT_AVAILABLE", "LOT"),
            (w.Reservation("1", w.W, Guid.NewGuid().ToString("D")), 422, "UNKNOWN_LOT", null),
            (w.Reservation("1", w.W, recalled), 422, "LOT_UNDER_RECALL", null),
            (w.Reservation("1", foreignWarehouse), 422, "UNKNOWN_LOCATION", null),
            (w.Reservation("1", lotId: lot), 400, "INVALID_REQUEST", null),
            (new Dictionary<string, object?>(reused) { ["quantity"] = "2" }, 400, "IDEMPOTENCY_KEY_REUSED", null)
        };

        foreach (var (body, status, code, level) in cases)
        {
            var before = Refusals(w).Count;
            using var response = await w.Ledger.ReserveAsync(body, named);
            Assert.Equal(status, (int)response.StatusCode);
            var rows = Refusals(w).Skip(before).ToList();
            var row = Assert.Single(rows);
            Assert.Equal((code, "Failed"), (row["ErrorCode"].AsString, row["Outcome"].AsString));
            Assert.Equal((Guid.Empty.ToString(), 0m, 0m), (row["ReservationId"].AsString, row["ChangedQuantity"].ToDecimal(), row["RemainingQuantity"].ToDecimal()));
            Assert.True(row["Status"].IsBsonNull, code + ": a refusal row has no reservation status");
            Assert.Equal((w.Ledger.User.ToString(), ActorName), (row["ActorUserId"].AsString, row["ActorDisplayName"].AsString));
            Assert.Equal((w.Ledger.Tenant.ToString(), w.Ledger.LegalEntity.ToString(), "token"), (row["TenantId"].AsString, row["LegalEntityId"].AsString, row["LeSource"].AsString));
            Assert.Equal(Guid.Parse(response.Headers.GetValues("X-Correlation-Id").Single()).ToString(), row["CorrelationId"].AsString);
            Assert.Equal(level, row["Level"].IsBsonNull ? null : row["Level"].AsString);
            if (code == "INSUFFICIENT_AVAILABLE")
            {
                Assert.Equal((11m, 9m, w.W, lot), (row["RequestedQuantity"].ToDecimal(), row["UncoveredQuantity"].ToDecimal(), row["WarehouseId"].AsString, row["LotId"].AsString));
            }
        }

        Assert.Single(w.Reservations());
        Assert.DoesNotContain(CentralRows(w), r => r.RequestType == "CreateReservationCommand" && r.EntityId != Guid.Parse(w.Reservations()[0]["_id"].AsString));

        // a body that cannot be read is refused before the handler: no row
        var before400 = Refusals(w).Count;
        using var unreadable = await RawAsync(w, "{ not json", named);
        Assert.Equal(400, (int)unreadable.StatusCode);
        Assert.Equal(before400, Refusals(w).Count);
        await RecallBlock.RemoveAsync(_factory, w.Ledger.Tenant);
    }

    /// <summary>AC-15 — sabotage: the outcome written as "Refused" → this vocabulary test is red.</summary>
    [Fact]
    public async Task Outcome_is_Failed_from_AUD_001_vocabulary()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        await w.ReceiveAsync("5", lot);
        await w.ReserveAsync(w.Reservation("5", w.W, lot), 201);
        await w.ReserveAsync(w.Reservation("1", w.W, lot), 422);
        await w.PostAsync(w.Issue("1", lot), 422);

        var outcomes = w.Persisted("inventory_audit").Where(d => d.Contains("Outcome") && !d["Outcome"].IsBsonNull).Select(d => d["Outcome"].AsString).ToList();
        Assert.Contains("Failed", outcomes);
        Assert.Contains("Succeeded", outcomes);
        Assert.All(outcomes, o => Assert.Contains(o, new[] { "Succeeded", "Failed", "Denied" }));
    }

    /// <summary>AC-15: LOCATION unreadable for the warehouse scope → 503 LOCATION_MASTER_UNAVAILABLE and one row.</summary>
    [Fact]
    public async Task Location_master_unavailable_is_503_with_one_row()
    {
        using var factory = new StubLocationMasterApiFactory();
        var s = new InventoryScenario(factory);
        factory.Locations.MarkUnavailable(s.Warehouse);
        var body = s.Reservation("1");
        body["warehouseId"] = s.Warehouse;

        var refused = await s.ReserveExpectingAsync(body, 503);
        Assert.Equal("LOCATION_MASTER_UNAVAILABLE", ScopedReservationWorld.Code(refused));
        var row = Assert.Single(s.Persisted("inventory_audit"), d => d.GetValue("Action", "").AsString == "InventoryReservationRefused");
        Assert.Equal(("LOCATION_MASTER_UNAVAILABLE", "Failed", s.Warehouse), (row["ErrorCode"].AsString, row["Outcome"].AsString, row["WarehouseId"].AsString));
        Assert.Empty(s.Persisted("inventory_reservations"));
    }

    /// <summary>
    /// AC-40 — every level its own fixture: (a) LOT, (b) WAREHOUSE, (c) SKU, (d) WAREHOUSE_TRANSFER. S-43: the ConsumptionRefused write removed
    /// from the guard refusal → no row, red.
    /// </summary>
    [Fact]
    public async Task Unnamed_issue_blocked_by_reservation_writes_one_row()
    {
        // (a) LOT — AC-4 (a)
        var a = new ScopedReservationWorld(_factory);
        var lot = await a.LotAsync();
        var lot2 = await a.LotAsync();
        await a.ReceiveAsync("50", lot);
        await a.ReceiveAsync("50", lot2);
        await a.ReceiveAsync("50", lot2, a.Ledger.LocationC);
        await a.ReserveOkAsync("30", a.W, lot);
        await AssertGuardRowAsync(a, a.Issue("21", lot, sourceModule: "MOD-0183"), "LOT", "GOODS_ISSUE_SALES", a.W, lot);

        // (b) WAREHOUSE — AC-4 (b)
        var b = new ScopedReservationWorld(_factory, lotControlled: false);
        await b.ReceiveAsync("50", null);
        await b.ReceiveAsync("50", null, b.Ledger.LocationC);
        await b.ReserveOkAsync("30", b.W);
        await AssertGuardRowAsync(b, b.Issue("21", null, sourceModule: "MOD-0183"), "WAREHOUSE", "GOODS_ISSUE_SALES", b.W, null);

        // (c) SKU — an unscoped reservation of 30 on 50, the issue's source triple differs from the reservation's
        var c = new ScopedReservationWorld(_factory, lotControlled: false);
        await c.ReceiveAsync("50", null);
        await c.ReserveOkAsync("30", sourceModule: "MOD-0172", sourceDocumentId: "SO-1", sourceLineId: "1");
        await AssertGuardRowAsync(c, c.Issue("21", null, sourceModule: "MOD-0183", sourceDocumentId: "SH-1", sourceLineId: "1"), "SKU", "GOODS_ISSUE_SALES", c.W, null);

        // (d) WAREHOUSE_TRANSFER — AC-37's 11
        var d = new ScopedReservationWorld(_factory, lotControlled: false);
        await d.ReceiveAsync("50", null);
        var (w3, w3Bin) = d.AddWarehouse();
        await d.ReceiveIntoAsync("50", null, w3, w3Bin);
        await d.ReserveOkAsync("40", d.W);
        await AssertGuardRowAsync(d, d.WarehouseTransfer("11", null), "WAREHOUSE", "WAREHOUSE_TRANSFER", d.W, null);

        // a row-level INSUFFICIENT_STOCK (the negative-stock guard, no level) writes none
        var e = new ScopedReservationWorld(_factory, lotControlled: false);
        await e.ReceiveAsync("5", null);
        var before = Refusals(e, "InventoryReservationConsumptionRefused").Count;
        var refused = await e.PostAsync(e.Issue("6", null), 422);
        Assert.Equal("INSUFFICIENT_STOCK", ScopedReservationWorld.Code(refused));
        Assert.False(refused.GetProperty("error").GetProperty("details").TryGetProperty("level", out _));
        Assert.Equal(before, Refusals(e, "InventoryReservationConsumptionRefused").Count);
    }

    private async Task AssertGuardRowAsync(ScopedReservationWorld w, Dictionary<string, object?> movement, string level, string movementType, string warehouse, string? lot)
    {
        var named = Named(w);
        var movements = w.Count("inventory_transactions");
        using var response = await w.Ledger.PostAsync(movement, token: named);
        var json = await w.Ledger.ReadAsync(response);
        Assert.True((int)response.StatusCode == 422, json.ToString());
        Assert.Equal(("INSUFFICIENT_STOCK", level), (ScopedReservationWorld.Code(json), ScopedReservationWorld.Detail(json, "level")));

        var row = Assert.Single(Refusals(w, "InventoryReservationConsumptionRefused"));
        Assert.Equal(("Failed", "INSUFFICIENT_STOCK", level, movementType), (row["Outcome"].AsString, row["ErrorCode"].AsString, row["Level"].AsString, row["MovementType"].AsString));
        Assert.Equal(Guid.Empty.ToString(), row["ReservationId"].AsString);
        Assert.True(row["ReservationIdText"].IsBsonNull);
        Assert.Equal((w.Ledger.User.ToString(), ActorName, (string)movement["sourceModule"]!), (row["ActorUserId"].AsString, row["ActorDisplayName"].AsString, row["SourceModule"].AsString));
        Assert.Equal((w.Ledger.Tenant.ToString(), w.Ledger.LegalEntity.ToString()), (row["TenantId"].AsString, row["LegalEntityId"].AsString));
        Assert.Equal(Guid.Parse(response.Headers.GetValues("X-Correlation-Id").Single()).ToString(), row["CorrelationId"].AsString);
        Assert.Equal(warehouse, row["WarehouseId"].AsString);
        Assert.Equal(lot, row["LotId"].IsBsonNull ? null : row["LotId"].AsString);
        Assert.Equal(movements, w.Count("inventory_transactions"));
        Assert.DoesNotContain(CentralRows(w), r => r.RequestType == "PostMovementCommand" && r.CorrelationId == Guid.Parse(row["CorrelationId"].AsString));
    }

    /// <summary>A token of the world's user that carries a display name (the name the central audit queue records).</summary>
    private static string Named(ScopedReservationWorld w)
        => TestTokens.Mint(w.Ledger.Tenant, w.Ledger.LegalEntity, w.Ledger.User, TestTokens.AllPermissions, [new Claim("name", ActorName)]);

    private static List<BsonDocument> Refusals(ScopedReservationWorld w, string action = "InventoryReservationRefused")
        => w.Persisted("inventory_audit").Where(d => d.GetValue("Action", "").AsString == action).OrderBy(d => d["OccurredAt"].ToUniversalTime()).ToList();

    private List<CentralAuditOutboxRecord> CentralRows(ScopedReservationWorld w)
        => CentralAudit.CentralAuditInventoryTests.Outbox(_factory).Find(x => x.TenantId == w.Ledger.Tenant).ToList();

    private async Task<HttpResponseMessage> RawAsync(ScopedReservationWorld w, string body, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/inventory/reservations") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", "rsv-raw");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await w.Ledger.SendAsync(request);
    }
}

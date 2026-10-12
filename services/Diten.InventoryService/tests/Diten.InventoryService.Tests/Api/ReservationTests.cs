using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// WP-0173-S2 AC-R1..R6, R8, R14 (pack ASSUMPTION-0173-12..17). GUARDS UNDER TEST (D4 sabotage targets):
/// (a) the create guard <c>AvailableOnHand − Reserved ≥ q</c>, (b) the SKU-level guard on decreases, (c) the ETag check.
/// </summary>
public sealed class ReservationTests : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;

    public ReservationTests(InventoryApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AC_R1_reserve_30_of_100_answers_201_available_after_70_and_availability_shows_it()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("100"), 201);

        var created = await s.ReserveExpectingAsync(s.Reservation("30"), 201);

        Assert.Equal("ACTIVE", created.GetProperty("status").GetString());
        Assert.Equal("30.000", created.GetProperty("reservedQuantity").GetString());
        Assert.Equal("70.000", created.GetProperty("availableAfter").GetString());
        Assert.Equal("W/\"1\"", created.GetProperty("etag").GetString());
        Assert.Equal((100m, 30m, 70m), await s.AvailabilityAsync());

        // ASSUMPTION-0173-12: reservations are SKU-level — balance rows keep reserved 0.
        var balance = await s.ReadAsync(await s.GetAsync($"/api/inventory/balance?skuId={s.Sku}"));
        var row = Assert.Single(balance.GetProperty("rows").EnumerateArray().ToList());
        Assert.Equal("0.000", row.GetProperty("reserved").GetString());
        Assert.Equal("100.000", row.GetProperty("available").GetString());

        var position = Assert.Single(s.Persisted("inventory_sku_positions"));
        Assert.Equal(100m, position["AvailableOnHand"].AsDecimal);
        Assert.Equal(30m, position["Reserved"].AsDecimal);
    }

    [Fact]
    public async Task AC_R2_over_reservation_is_422_INSUFFICIENT_AVAILABLE_with_details_and_writes_nothing()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("100"), 201);
        await s.ReserveExpectingAsync(s.Reservation("30"), 201);

        var refused = await s.ReserveExpectingAsync(s.Reservation("80"), 422);

        var error = refused.GetProperty("error");
        Assert.Equal("INSUFFICIENT_AVAILABLE", error.GetProperty("code").GetString());
        Assert.Equal("70.000", error.GetProperty("details").GetProperty("available").GetString());
        Assert.Equal("80.000", error.GetProperty("details").GetProperty("requested").GetString());
        Assert.Single(s.Persisted("inventory_reservations"));
        Assert.Equal((100m, 30m, 70m), await s.AvailabilityAsync());
    }

    [Fact]
    public async Task Reserving_a_sku_without_stock_is_422_with_available_zero()
    {
        var s = new InventoryScenario(_factory);

        var refused = await s.ReserveExpectingAsync(s.Reservation("1"), 422);

        Assert.Equal("0.000", refused.GetProperty("error").GetProperty("details").GetProperty("available").GetString());
        Assert.Empty(s.Persisted("inventory_reservations"));
    }

    [Fact]
    public async Task AC_R3_two_parallel_reserves_of_60_on_100_commit_once_and_reserved_is_60_never_120()
    {
        for (var round = 0; round < 5; round++)
        {
            var s = new InventoryScenario(_factory);
            await s.PostExpectingAsync(s.Receipt("100"), 201);

            var results = await Task.WhenAll(s.ReserveAsync(s.Reservation("60")), s.ReserveAsync(s.Reservation("60")));

            Assert.Equal(new[] { 201, 422 }, results.Select(r => (int)r.StatusCode).OrderBy(x => x).ToArray());
            Assert.Equal((100m, 60m, 40m), await s.AvailabilityAsync());
            Assert.Equal(60m, Assert.Single(s.Persisted("inventory_sku_positions"))["Reserved"].AsDecimal);
            Assert.Single(s.Persisted("inventory_reservations"));
        }
    }

    [Fact]
    public async Task AC_R4_reserved_stock_cannot_be_issued_to_someone_else()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("100"), 201);
        await s.ReserveExpectingAsync(s.Reservation("70", "SO-900", "1"), 201);

        var refused = await s.PostExpectingAsync(s.IssueFor("40", "MOD-OTHER", "DO-1", "1"), 422);

        Assert.Equal("INSUFFICIENT_STOCK", refused.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("30.000", refused.GetProperty("error").GetProperty("details").GetProperty("available").GetString());
        Assert.Equal("40.000", refused.GetProperty("error").GetProperty("details").GetProperty("requested").GetString());
        Assert.Single(s.Persisted("inventory_transactions"));
        Assert.Equal((100m, 70m, 30m), await s.AvailabilityAsync());

        // Control: the unreserved 30 can still be issued.
        await s.PostExpectingAsync(s.IssueFor("30", "MOD-OTHER", "DO-2", "1"), 201);
        Assert.Equal((70m, 70m, 0m), await s.AvailabilityAsync());
    }

    [Fact]
    public async Task Moving_reserved_stock_between_available_bins_is_allowed_reservations_are_not_located()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("100"), 201);
        await s.ReserveExpectingAsync(s.Reservation("100"), 201);
        var move = s.Movement("LOCATION_TRANSFER", "100");
        move["fromWarehouseId"] = s.Warehouse;
        move["fromLocationId"] = s.LocationA;
        move["toWarehouseId"] = s.Warehouse;
        move["toLocationId"] = s.LocationB;

        await s.PostExpectingAsync(move, 201);

        Assert.Equal((100m, 100m, 0m), await s.AvailabilityAsync());
    }

    [Fact]
    public async Task Moving_reserved_stock_out_of_AVAILABLE_status_is_refused()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("100"), 201);
        await s.ReserveExpectingAsync(s.Reservation("80"), 201);
        var block = s.Movement("STATUS_TRANSFER", "30");
        block["fromWarehouseId"] = s.Warehouse;
        block["fromLocationId"] = s.LocationA;
        block["toWarehouseId"] = s.Warehouse;
        block["toLocationId"] = s.LocationA;
        block["toStockStatus"] = "BLOCKED";

        var refused = await s.PostExpectingAsync(block, 422);

        Assert.Equal("INSUFFICIENT_STOCK", refused.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task AC_R5_a_movement_with_the_reservation_source_consumes_it_partially_then_fully()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("100"), 201);
        var created = await s.ReserveExpectingAsync(s.Reservation("30", "SO-900", "1"), 201);
        var id = created.GetProperty("reservationId").GetString()!;

        await s.PostExpectingAsync(s.IssueFor("20", "MOD-0172", "SO-900", "1"), 201);
        var afterFirst = Reservation(s, id);
        Assert.Equal("Active", afterFirst["Status"].AsString);
        Assert.Equal(10m, afterFirst["RemainingQuantity"].AsDecimal);
        Assert.Equal((80m, 10m, 70m), await s.AvailabilityAsync());

        await s.PostExpectingAsync(s.IssueFor("10", "MOD-0172", "SO-900", "1"), 201);
        var afterSecond = Reservation(s, id);
        Assert.Equal("Released", afterSecond["Status"].AsString);
        Assert.Equal(0m, afterSecond["RemainingQuantity"].AsDecimal);
        Assert.Equal((70m, 0m, 70m), await s.AvailabilityAsync());

        var actions = s.Persisted("inventory_audit").Where(a => a.Contains("ReservationId")).Select(a => a["Action"].AsString).ToList();
        Assert.Contains("InventoryReservationPartiallyConsumed", actions);
        Assert.Contains("InventoryReservationConsumed", actions);
    }

    [Fact]
    public async Task A_holder_can_issue_its_own_reservation_even_when_everything_is_reserved()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("100"), 201);
        await s.ReserveExpectingAsync(s.Reservation("30", "SO-1", "1"), 201);
        await s.ReserveExpectingAsync(s.Reservation("70", "SO-2", "1"), 201);

        await s.PostExpectingAsync(s.IssueFor("30", "MOD-0172", "SO-1", "1"), 201);
        await s.PostExpectingAsync(s.IssueFor("1", "MOD-0172", "SO-3", "1"), 422);

        Assert.Equal((70m, 70m, 0m), await s.AvailabilityAsync());
    }

    [Fact]
    public async Task AC_R6_release_with_the_right_ETag_restores_available_stale_ETag_409_missing_400_other_tenant_404()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("100"), 201);
        var created = await s.ReserveExpectingAsync(s.Reservation("30"), 201);
        var id = created.GetProperty("reservationId").GetString()!;
        var etag = created.GetProperty("etag").GetString()!;

        using (var missing = await s.ReleaseAsync(id, ifMatch: null))
        {
            Assert.Equal(400, (int)missing.StatusCode);
        }

        using (var stale = await s.ReleaseAsync(id, "W/\"7\""))
        {
            Assert.Equal(409, (int)stale.StatusCode);
            Assert.Equal("RESERVATION_CONFLICT", (await s.ReadAsync(stale)).GetProperty("error").GetProperty("code").GetString());
        }

        var other = new InventoryScenario(_factory);
        using (var foreign = await other.ReleaseAsync(id, etag))
        {
            Assert.Equal(404, (int)foreign.StatusCode);
        }

        using (var unknown = await s.ReleaseAsync(Guid.NewGuid().ToString(), etag))
        {
            Assert.Equal(404, (int)unknown.StatusCode);
        }

        Assert.Equal((100m, 30m, 70m), await s.AvailabilityAsync());

        using var released = await s.ReleaseAsync(id, etag);
        var body = await s.ReadAsync(released);
        Assert.Equal(200, (int)released.StatusCode);
        Assert.Equal("RELEASED", body.GetProperty("status").GetString());
        Assert.Equal("100.000", body.GetProperty("availableAfter").GetString());
        Assert.Equal((100m, 0m, 100m), await s.AvailabilityAsync());

        // The old ETag is now stale and the reservation is no longer ACTIVE.
        using var again = await s.ReleaseAsync(id, etag);
        Assert.Equal(409, (int)again.StatusCode);
    }

    [Fact]
    public async Task A_consumed_reservation_changes_version_so_its_creation_ETag_is_stale()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("100"), 201);
        var created = await s.ReserveExpectingAsync(s.Reservation("30", "SO-5", "1"), 201);
        await s.PostExpectingAsync(s.IssueFor("10", "MOD-0172", "SO-5", "1"), 201);

        using var stale = await s.ReleaseAsync(created.GetProperty("reservationId").GetString()!, created.GetProperty("etag").GetString());
        using var current = await s.ReleaseAsync(created.GetProperty("reservationId").GetString()!, "W/\"2\"");

        Assert.Equal(409, (int)stale.StatusCode);
        Assert.Equal(200, (int)current.StatusCode);
        Assert.Equal((90m, 0m, 90m), await s.AvailabilityAsync());
    }

    [Fact]
    public async Task AC_R8_replay_returns_the_original_result_once_and_a_reused_key_is_400()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("100"), 201);
        var body = s.Reservation("30");

        var first = await s.ReserveExpectingAsync(body, 201);
        await s.ReserveExpectingAsync(s.Reservation("10"), 201);
        var replay = await s.ReserveExpectingAsync(body, 200);

        Assert.Equal(first.GetProperty("reservationId").GetString(), replay.GetProperty("reservationId").GetString());
        Assert.Equal("70.000", replay.GetProperty("availableAfter").GetString());
        Assert.Equal(2, s.Persisted("inventory_reservations").Count);
        Assert.Equal((100m, 40m, 60m), await s.AvailabilityAsync());

        var reused = new Dictionary<string, object?>(body) { ["quantity"] = "31" };
        var refused = await s.ReserveExpectingAsync(reused, 400);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", refused.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(2, s.Persisted("inventory_reservations").Count);
    }

    [Fact]
    public async Task Concurrent_duplicates_of_one_reservation_key_commit_once()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("100"), 201);
        var body = s.Reservation("10");

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => s.ReserveAsync(body)));

        Assert.Equal(1, results.Count(r => (int)r.StatusCode == 201));
        Assert.Equal(4, results.Count(r => (int)r.StatusCode == 200));
        Assert.Single(s.Persisted("inventory_reservations"));
        Assert.Equal((100m, 10m, 90m), await s.AvailabilityAsync());
    }

    [Theory]
    [InlineData("quantity", "0")]
    [InlineData("quantity", "1e2")]
    [InlineData("skuLevel", "lsku")]
    [InlineData("itemId", "item-1")]
    [InlineData("reservationType", "")]
    [InlineData("sourceModule", null)]
    [InlineData("baseQuantity", "31")]
    [InlineData("expiresAt", "2020-01-01T00:00:00Z")]
    [InlineData("expiresAt", "2099-01-01T00:00:00+03:00")]
    [InlineData("tenantId", "x")]
    public async Task Malformed_reservation_requests_are_400_INVALID_REQUEST(string field, string? value)
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("100"), 201);
        var body = s.Reservation("30");
        body[field] = value;

        var refused = await s.ReserveExpectingAsync(body, 400);

        Assert.Equal("INVALID_REQUEST", refused.GetProperty("error").GetProperty("code").GetString());
        Assert.Empty(s.Persisted("inventory_reservations"));
    }

    [Fact]
    public async Task A_non_integer_priority_is_400()
    {
        var s = new InventoryScenario(_factory);
        var body = s.Reservation("1");
        body["priority"] = "high";

        await s.ReserveExpectingAsync(body, 400);
    }

    [Fact]
    public async Task AC_R14_create_and_release_write_audit_and_Pending_outbox_with_the_response_correlation()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("100"), 201);

        using var create = await s.ReserveAsync(s.Reservation("30"));
        var createdBody = await s.ReadAsync(create);
        var createCorrelation = create.Headers.GetValues("X-Correlation-Id").Single();
        var id = createdBody.GetProperty("reservationId").GetString()!;

        using var release = await s.ReleaseAsync(id, createdBody.GetProperty("etag").GetString());
        var releaseCorrelation = release.Headers.GetValues("X-Correlation-Id").Single();

        var audit = s.Persisted("inventory_audit").Where(a => a.Contains("ReservationId")).ToList();
        var outbox = s.Persisted("inventory_outbox").Where(o => o["EventName"].AsString == "InventoryReservationChanged").ToList();

        var createdAudit = Assert.Single(audit, a => a["Action"].AsString == "InventoryReservationCreated");
        var releasedAudit = Assert.Single(audit, a => a["Action"].AsString == "InventoryReservationReleased");
        Assert.Equal(createCorrelation, createdAudit["CorrelationId"].AsString);
        Assert.Equal(releaseCorrelation, releasedAudit["CorrelationId"].AsString);
        Assert.Equal(s.User.ToString(), releasedAudit["ActorUserId"].AsString);

        Assert.Equal(2, outbox.Count);
        Assert.All(outbox, o => Assert.Equal("Pending", o["Status"].AsString));
        Assert.Contains(outbox, o => o["CorrelationId"].AsString == createCorrelation && o["Payload"]["Change"].AsString == "Created");
        Assert.Contains(outbox, o => o["CorrelationId"].AsString == releaseCorrelation && o["Payload"]["Change"].AsString == "Released");
        Assert.All(outbox, o => Assert.Equal(id, o["AggregateId"].AsString));
    }

    [Fact]
    public async Task Consumption_is_audited_with_the_movement_correlation_and_transaction()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("100"), 201);
        await s.ReserveExpectingAsync(s.Reservation("30", "SO-7", "2"), 201);

        using var issue = await s.PostAsync(s.IssueFor("30", "MOD-0172", "SO-7", "2"));
        var correlation = issue.Headers.GetValues("X-Correlation-Id").Single();
        var transactionId = (await s.ReadAsync(issue)).GetProperty("transactionId").GetString();

        var consumed = Assert.Single(s.Persisted("inventory_audit"), a => a.Contains("ReservationId") && a["Action"].AsString == "InventoryReservationConsumed");
        Assert.Equal(correlation, consumed["CorrelationId"].AsString);
        Assert.Equal(transactionId, consumed["TransactionId"].AsString);
        Assert.Contains(s.Persisted("inventory_outbox"),
            o => o["EventName"].AsString == "InventoryReservationChanged" && o["Payload"]["Change"].AsString == "Consumed"
                 && o["CorrelationId"].AsString == correlation);
    }

    private static BsonDocument Reservation(InventoryScenario s, string id)
        => s.Factory.Collection("inventory_reservations").Find(new BsonDocument("_id", id)).Single();
}

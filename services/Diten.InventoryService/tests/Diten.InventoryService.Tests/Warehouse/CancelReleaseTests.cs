using System.Text.Json;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Inventory.Services;
using Diten.InventoryService.Application.Features.OutboundShipments.Services;
using Diten.InventoryService.Persistence.Warehouse;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using static Diten.InventoryService.Tests.Warehouse.OutboundScenario;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-5 — the cancel and the ONE reservation release rule (pack §8.22, §8.17 step 6; AC-32, AC-71 (vii) release part, AC-35 golden flow) on
/// the REAL host: transaction A (Cancelled, tasks / packages Cancelled, every allocation Pending, the Cancellation put-back), then the release per
/// allocation read-first (GetAsync / FindByIdempotencyKeysAsync), a failure left Pending for the maintenance, which releases with the creator as actor.
/// </summary>
public sealed class CancelReleaseTests : IClassFixture<WarehouseApiFactory>
{
    private readonly WarehouseApiFactory _factory;

    public CancelReleaseTests(WarehouseApiFactory factory) => _factory = factory;

    private Task<(OutboundScenario S, OutboundFlow F, string L1, string L2)> FS2Async() => PickTaskTests.FS2Async(_factory);

    private static string Rid(JsonElement view, int seq = 1) => OutboundFlow.Alloc(view, seq).GetProperty("reservationId").GetString()!;

    /// <summary>AC-32: an Allocated outbound cancelled → 200, outbound and pick tasks Cancelled, FU03 RELEASED, no put-back task.</summary>
    [Fact]
    public async Task AC32_an_allocated_cancel_releases_every_reservation_and_opens_no_putaway()
    {
        var (s, f, _, _) = await FS2Async();
        var outbound = await s.CreatedAsync(s.Line(60));
        var id = OutboundScenario.Id(outbound);
        Assert.Equal("REASON_REQUIRED", Code(await Expect(f.CancelAsync(id, "kisa"), 400)));

        var cancelled = await Expect(f.CancelAsync(id), 200);
        Assert.Equal("Cancelled", cancelled.GetProperty("status").GetString());
        Assert.All(s.PickTasks(id), t => Assert.Equal("Cancelled", t["Status"].AsString));
        foreach (var a in OutboundFlow.Allocs(cancelled))
        {
            Assert.Equal("Released", a.GetProperty("reservationReleaseState").GetString());
            Assert.Equal("RELEASED", (await s.ReservationViewAsync(a.GetProperty("reservationId").GetString()!))!.Status);
        }

        Assert.Empty(f.PutawayTasks("Cancellation"));
        Assert.Equal(1, s.Central("CancelOutboundShipmentCommand").Forwarded);
        Assert.Equal("WMS_INVALID_TRANSITION", Code(await Expect(f.CancelAsync(id), 409)));
    }

    /// <summary>
    /// AC-32: a Picked outbound (L1 30 on S-01) cancelled → a Cancellation putaway (S-01, L1, 30), put away by S1; a manual S1 task from Staging stays 422.
    /// Sabotage (co): no Cancellation task → red.
    /// </summary>
    [Fact]
    public async Task AC32_a_picked_cancel_puts_the_staged_stock_back_with_a_Cancellation_task()
    {
        var (s, f, l1, _) = await FS2Async();
        var id = OutboundScenario.Id(await s.CreatedAsync(s.Line(30)));
        await f.PickAllAsync(id);
        await Expect(f.CancelAsync(id), 200);

        var task = Assert.Single(f.PutawayTasks("Cancellation"));
        Assert.Equal((s.S01.ToString("D"), l1, 30m), (task["FromLocationId"].ToString(), task["LotId"].AsString, task["PlannedQuantity"].ToDecimal()));
        var putaway = await s.P.GetTaskAsync(task["_id"].ToString()!, f.Supervisor);
        await Expect(s.P.ConfirmAsync(putaway, 30, s.A01, token: f.Supervisor), 201);
        Assert.Equal(0m, await s.P.OnHandAsync(s.S01));

        // S1 rule unchanged: a manual task from the Staging node is refused
        var manual = s.P.TaskBody(1, from: s.A01, lotId: l1);
        manual["fromLocationId"] = s.S01.ToString("D");
        Assert.Equal("WMS_SOURCE_NOT_RECEIVING", Code(await Expect(s.P.CreateTaskAsync(manual, f.Supervisor), 422)));
    }

    /// <summary>AC-32: ReadyToShip → 409 WMS_OUTBOUND_CANCEL_NOT_ALLOWED; a Pending pick confirmation → 409 WMS_CONFIRMATION_PENDING.</summary>
    [Fact]
    public async Task AC32_ready_to_ship_and_a_pending_pick_refuse_the_cancel()
    {
        var (s, f, _, _) = await FS2Async();
        var ready = await f.ReadyAsync(s.Line(10));
        Assert.Equal("WMS_OUTBOUND_CANCEL_NOT_ALLOWED", Code(await Expect(f.CancelAsync(ready), 409)));

        var pending = OutboundScenario.Id(await s.CreatedAsync(s.Line(10)));
        var task = (await f.PickTasksAsync(pending)).Single();
        s.P.LedgerAnswersBefore(503);
        await Expect(f.ConfirmAsync(PutawayScenario.TaskId(task), 10, PutawayScenario.ETagOf(task)), 503);
        s.P.LedgerHealthy();
        Assert.Equal("WMS_CONFIRMATION_PENDING", Code(await Expect(f.CancelAsync(pending), 409)));
        Assert.Equal("Allocated", (await f.ViewAsync(pending)).GetProperty("status").GetString());
    }

    /// <summary>
    /// AC-32 release failure: FU03 ReleaseAsync throws → 200, Cancelled, the allocation Pending + ReservationReleaseFailed; the maintenance tick → Released,
    /// FU03 RELEASED, the actor = the outbound's creator; an already RELEASED reservation → no ReleaseAsync call. Sabotage (cn): allocations not marked
    /// Pending at cancel → the reservation stays ACTIVE (red).
    /// </summary>
    [Fact]
    public async Task AC32_a_failed_release_stays_pending_and_the_maintenance_releases_it_as_the_creator()
    {
        var (s, f, _, _) = await FS2Async();
        var outbound = await s.CreatedAsync(s.Line(30));
        var id = OutboundScenario.Id(outbound);
        _factory.BeforeRelease[s.Tenant] = _ => throw new InvalidOperationException("test: FU03 release down");
        var cancelled = await Expect(f.CancelAsync(id), 200);
        _factory.BeforeRelease.TryRemove(s.Tenant, out _);
        Assert.Equal("Cancelled", cancelled.GetProperty("status").GetString());
        Assert.Equal("Pending", OutboundFlow.Alloc(cancelled, 1).GetProperty("reservationReleaseState").GetString());
        Assert.Contains("ReservationReleaseFailed", f.OutboundEventKinds(id));
        Assert.Equal("ACTIVE", (await s.ReservationViewAsync(Rid(outbound)))!.Status);

        _factory.ReleaseActors.TryRemove(s.Tenant, out _);
        var tick = await s.MaintainAsync();
        Assert.True(tick.CommandsSent >= 1);
        Assert.Equal("Released", OutboundFlow.Alloc(await f.ViewAsync(id), 1).GetProperty("reservationReleaseState").GetString());
        Assert.Equal("RELEASED", (await s.ReservationViewAsync(Rid(outbound)))!.Status);
        Assert.All(_factory.ReleaseActors[s.Tenant], actor => Assert.Equal(s.User, actor));
        Assert.Equal(1, s.Central("ReleaseOutboundReservationsCommand").Forwarded);

        // an already RELEASED reservation: the rule reads first and makes no ReleaseAsync call
        var second = await s.CreatedAsync(s.Line(10));
        var secondId = OutboundScenario.Id(second);
        var view = await s.ReservationViewAsync(Rid(second));
        await PickTaskTests.ReleaseDirectAsync(s, Rid(second), view!.Etag);
        _factory.ReleaseCalls[s.Tenant] = 0;
        var done = await Expect(f.CancelAsync(secondId), 200);
        Assert.Equal(0, _factory.ReleaseCalls[s.Tenant]);
        Assert.Equal("Released", OutboundFlow.Alloc(done, 1).GetProperty("reservationReleaseState").GetString());
    }

    private async Task BumpReservationVersionAsync(OutboundScenario s, string reservationId)
        => await _factory.Database.GetCollection<BsonDocument>("inventory_reservations")
            .UpdateOneAsync(new BsonDocument("_id", reservationId), new BsonDocument("$inc", new BsonDocument("Version", 1)));

    /// <summary>
    /// AC-32 version (r21 F-7): the reservation's version moved after the allocation → the cancel releases with the version it READS (Released); a version
    /// moved between the read and the release → 409 → Pending + ReservationReleaseFailed (Conflict), the maintenance re-reads → Released. Sabotage (dl): the
    /// release with a stored version (the creation version W/"1") → the first case red.
    /// </summary>
    [Fact]
    public async Task AC32_the_release_uses_the_version_it_reads_and_a_race_is_retried_by_the_maintenance()
    {
        var (s, f, _, _) = await FS2Async();
        var first = await s.CreatedAsync(s.Line(10));
        await BumpReservationVersionAsync(s, Rid(first));
        var done = await Expect(f.CancelAsync(OutboundScenario.Id(first)), 200);
        Assert.Equal("Released", OutboundFlow.Alloc(done, 1).GetProperty("reservationReleaseState").GetString());

        var second = await s.CreatedAsync(s.Line(10));
        var once = 0;
        _factory.BeforeRelease[s.Tenant] = async rid =>
        {
            if (Interlocked.Increment(ref once) == 1) await BumpReservationVersionAsync(s, rid.ToString("D"));
        };
        var raced = await Expect(f.CancelAsync(OutboundScenario.Id(second)), 200);
        _factory.BeforeRelease.TryRemove(s.Tenant, out _);
        Assert.Equal("Pending", OutboundFlow.Alloc(raced, 1).GetProperty("reservationReleaseState").GetString());
        var failed = s.OutboundEvents(OutboundScenario.Id(second)).Last(e => e["Kind"].AsString == "ReservationReleaseFailed");
        Assert.Equal("Conflict", failed["Data"]["reason"].AsString);
        await s.MaintainAsync();
        Assert.Equal("Released", OutboundFlow.Alloc(await f.ViewAsync(OutboundScenario.Id(second)), 1).GetProperty("reservationReleaseState").GetString());
        Assert.Equal("RELEASED", (await s.ReservationViewAsync(Rid(second)))!.Status);
    }

    /// <summary>
    /// AC-71 (vii) release part: the FU03 400 fixture (two of three keys made a reservation, one released, one ACTIVE) → the maintenance calls
    /// FindByIdempotencyKeysAsync ONCE with the outbound's tenant / company and the three keys, releases the ACTIVE one with its etag, calls no release for the
    /// RELEASED one nor the missing key; all three Released, central 1. Sabotage (ea): the keyed read skipped → the ACTIVE reservation stays (red).
    /// </summary>
    [Fact]
    public async Task AC71_vii_the_maintenance_releases_the_key_reused_allocations_by_key()
    {
        var s = await new OutboundScenario(_factory).ReadyAsync();
        foreach (var (expiry, bin) in new[] { ("2027-01-31", s.A01), ("2027-02-28", s.A02), ("2027-03-31", s.A01) }) await s.ReceiveAsync(10, bin, s.Lot(expiry));
        string? activeId = null;
        _factory.AroundBatch[s.Tenant] = async (lines, real) =>
        {
            using var scope = _factory.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetScope(s.Tenant, s.LegalEntity, s.User);
            scope.ServiceProvider.GetRequiredService<ICorrelationContext>().Set(Guid.NewGuid());
            var port = scope.ServiceProvider.GetRequiredService<InventoryReservationPort>();
            var first = await port.CreateAsync(lines[0], s.LegalEntity, s.User);
            var second = await port.CreateAsync(lines[1], s.LegalEntity, s.User);
            Assert.True((await port.ReleaseAsync(Guid.Parse(first.Data!.ReservationId), first.Data.Etag, s.User)).IsSuccessful);
            activeId = second.Data!.ReservationId;
            return await real();
        };
        await Expect(s.CreateAsync(s.Body(s.Line(30))), 422);
        _factory.AroundBatch.TryRemove(s.Tenant, out _);
        var stored = s.StoredOutbounds().Single();
        Assert.Equal("AllocationFailed", stored["Status"].AsString);

        _factory.KeyedReads[s.Tenant] = 0;
        _factory.ReleaseCalls[s.Tenant] = 0;
        await s.MaintainAsync();

        Assert.Equal(1, _factory.KeyedReads[s.Tenant]);
        Assert.Equal(1, _factory.ReleaseCalls[s.Tenant]);                                 // only the ACTIVE one
        Assert.Equal("RELEASED", (await s.ReservationViewAsync(activeId!))!.Status);
        var states = s.StoredOutbounds().Single()["Lines"].AsBsonArray.SelectMany(l => l["Allocations"].AsBsonArray).Select(a => a["ReservationReleaseState"].AsString);
        Assert.Equal(["Released", "Released", "Released"], states);
        Assert.Equal(1, s.Central("ReleaseOutboundReservationsCommand").Forwarded);
        Assert.Equal(0, (await s.MaintainAsync()).CommandsSent);                          // nothing pending any more
    }

    /// <summary>AC-71 (vii): the keyed read in a scope that is not the outbound's company → FU03 fail-closed → Pending, ReservationReleaseFailed, still ACTIVE.</summary>
    [Fact]
    public async Task AC71_vii_a_scope_mismatch_is_refused_fail_closed_and_stays_pending()
    {
        var s = await new OutboundScenario(_factory).ReadyAsync();
        foreach (var (expiry, bin) in new[] { ("2027-01-31", s.A01), ("2027-02-28", s.A02) }) await s.ReceiveAsync(10, bin, s.Lot(expiry));
        string? activeId = null;
        _factory.AroundBatch[s.Tenant] = async (lines, real) =>
        {
            using var scope = _factory.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetScope(s.Tenant, s.LegalEntity, s.User);
            scope.ServiceProvider.GetRequiredService<ICorrelationContext>().Set(Guid.NewGuid());
            activeId = (await scope.ServiceProvider.GetRequiredService<InventoryReservationPort>().CreateAsync(lines[0], s.LegalEntity, s.User)).Data!.ReservationId;
            return await real();
        };
        await Expect(s.CreateAsync(s.Body(s.Line(20))), 422);
        _factory.AroundBatch.TryRemove(s.Tenant, out _);

        using (var scope = _factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetScope(s.Tenant, s.LegalEntity, s.User);
            scope.ServiceProvider.GetRequiredService<ICorrelationContext>().Set(Guid.NewGuid());
            var outbound = (await scope.ServiceProvider.GetRequiredService<OutboundShipmentRepository>().FindAsync(Guid.Parse(s.StoredOutbounds().Single()["_id"].ToString()!)))!;
            var shifted = outbound.Copy();
            shifted.LegalEntityId = Guid.NewGuid();                                          // another company than the resolved scope
            var (_, outcomes) = await scope.ServiceProvider.GetRequiredService<OutboundReservationReleaser>().ReleasePendingAsync(shifted, s.User, CancellationToken.None);
            Assert.All(outcomes, o => Assert.Equal(("Pending", "ScopeRefused"), (o.State.ToString(), o.FailureReason)));
        }

        Assert.Contains(s.OutboundEvents(), e => e["Kind"].AsString == "ReservationReleaseFailed");
        Assert.Equal("ACTIVE", (await s.ReservationViewAsync(activeId!))!.Status);
    }

    /// <summary>
    /// AC-35 golden flow: receipt → S1 putaway → outbound → pick → pack → ReadyToShip → the MVP-6 step (GOODS_ISSUE_SALES from S-01 with the reservation —
    /// fixture until MOD-0183's revision) → "Sevkiyatı kontrol et" → Shipped; S-01 0, the reservation RELEASED (none ACTIVE), 0178's movements only
    /// LOCATION_TRANSFER; a restarted host reads the same outbound, tasks, packages and balance.
    /// </summary>
    [Fact]
    public async Task AC35_the_golden_S2_flow_from_receipt_to_shipped()
    {
        var s = await new OutboundScenario(_factory).ReadyAsync();
        var f = new OutboundFlow(s);
        var lot = s.Lot("2027-06-30");
        await s.ReceiveAsync(20, s.R01, lot);
        var putawayBody = s.P.TaskBody(20, lotId: lot);
        var putaway = await Expect(s.P.CreateTaskAsync(putawayBody, f.Supervisor), 201);
        await Expect(s.P.ConfirmAsync(putaway, 20, s.A01, token: f.Supervisor), 201);

        var outbound = await s.CreatedAsync(s.Line(20));
        var id = OutboundScenario.Id(outbound);
        await f.PickAllAsync(id);
        await f.PackAllAsync(id);
        await Expect(f.ReleaseAsync(id), 200);
        var reservationId = Rid(outbound);
        await f.IssueAsync(20, s.S01, lot, reservationId);
        f.Dispatched((s.Sku, lot, s.S01, 20m));
        var synced = await Expect(f.SyncAsync(id), 200);
        Assert.Equal("Shipped", synced.GetProperty("outbound").GetProperty("status").GetString());

        Assert.Equal(0m, await s.P.OnHandAsync(s.S01));
        Assert.Equal("RELEASED", (await s.ReservationViewAsync(reservationId))!.Status);
        Assert.DoesNotContain(s.Reservations(id), r => r["Status"].AsString == "ACTIVE");
        Assert.All(f.Movements().Where(m => m["SourceModule"].AsString == "MOD-0178"), m => Assert.Equal("LocationTransfer", m["MovementType"].ToString()));

        // a restarted host (a new server on the same database) reads the same state
        using var restarted = _factory.WithWebHostBuilder(_ => { });
        var client = restarted.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/warehouse/outbound-shipments/{id}/view");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", f.Supervisor);
        request.Headers.Add("X-Legal-Entity-Id", s.LegalEntity.ToString("D"));
        using var response = await client.SendAsync(request);
        var view = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("Shipped", view.GetProperty("status").GetString());
        Assert.Equal(OutboundFlow.Allocs(synced.GetProperty("outbound")).Count, OutboundFlow.Allocs(view).Count);
        Assert.Single(view.GetProperty("packages").EnumerateArray());
        Assert.All(view.GetProperty("pickTasks").EnumerateArray(), t => Assert.Equal("Completed", t.GetProperty("status").GetString()));
        Assert.Equal(0m, await s.P.OnHandAsync(s.S01));
    }
}

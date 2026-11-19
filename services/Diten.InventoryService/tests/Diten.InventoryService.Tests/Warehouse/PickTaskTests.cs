using System.Text.Json;
using Diten.InventoryService.Domain.Enums;
using Xunit;
using static Diten.InventoryService.Tests.Warehouse.OutboundScenario;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-5 — the S2 pick task on the REAL host (pack §8.19): S1's confirmation machine with the pick movement (bin → Staging
/// LOCATION_TRANSFER, PICK_TASK, documentGroupId = outbound number, reservationId = the allocation's — validated, never consumed), the pick
/// pre-checks (recall, the Staging storage-condition fit, the serial bucket), no target in the body, short close (also with 0 picked), and the
/// outbound becoming <c>Picked</c> in the last terminal write. Fixture F-S2: L1 (2027-01-31) 30 EA on A-02, L2 (2027-03-31) 50 EA on A-01, S-01.
/// </summary>
public sealed class PickTaskTests : IClassFixture<WarehouseApiFactory>
{
    private readonly WarehouseApiFactory _factory;

    public PickTaskTests(WarehouseApiFactory factory) => _factory = factory;

    internal static async Task<(OutboundScenario S, OutboundFlow F, string L1, string L2)> FS2Async(WarehouseApiFactory factory, IReadOnlyList<string>? conditions = null,
        bool lotControlled = true, SkuLevel level = SkuLevel.Lsku)
    {
        var s = new OutboundScenario(factory, lotControlled, level: level);
        if (conditions is not null) factory.StorageConditions[s.Sku] = conditions;
        await s.ReadyAsync();
        var f = new OutboundFlow(s);
        if (!lotControlled) return (s, f, string.Empty, string.Empty);
        var l1 = s.Lot("2027-01-31");
        var l2 = s.Lot("2027-03-31");
        await s.ReceiveAsync(30, s.A02, l1);
        await s.ReceiveAsync(50, s.A01, l2);
        return (s, f, l1, l2);
    }

    /// <summary>Sabotage (cf): the pick movement without <c>reservationId</c> → the reservation and "Refused" assertions are red.</summary>
    [Fact]
    public async Task AC27_the_L1_pick_moves_A02_to_S01_with_the_reservation_which_stays_ACTIVE_and_the_task_completes()
    {
        var (s, f, l1, _) = await FS2Async(_factory);
        var outbound = await s.CreatedAsync(s.Line(60));
        var id = OutboundScenario.Id(outbound);
        var allocation = OutboundFlow.Allocs(outbound).Single(a => a.GetProperty("lotId").GetString() == l1);
        var reservationId = allocation.GetProperty("reservationId").GetString()!;
        var task = (await f.PickTasksAsync(id)).Single(t => t.GetProperty("lotId").GetString() == l1);

        var done = await f.ConfirmedAsync(task, 30);

        Assert.Equal("Completed", PutawayScenario.Status(done));
        var taskId = PutawayScenario.TaskId(task);
        var movement = Assert.Single(s.P.TaskMovements(taskId));
        Assert.Equal("LocationTransfer", movement["MovementType"].ToString());
        Assert.Equal(s.A02.ToString("D"), movement["FromLocationId"].ToString());
        Assert.Equal(s.S01.ToString("D"), movement["ToLocationId"].ToString());
        Assert.Equal("Available", movement["ToStockStatus"].ToString());
        Assert.Equal("MOD-0178", movement["SourceModule"].AsString);
        Assert.Equal("PICK_TASK", movement["SourceType"].AsString);
        Assert.Equal("1", movement["SourceLineId"].AsString);
        Assert.Equal(outbound.GetProperty("outboundNumber").GetString(), movement["DocumentGroupId"].AsString);
        Assert.Equal(reservationId, movement["ReservationId"].ToString());
        Assert.Equal($"wms:pck:{taskId}:1", movement["IdempotencyKey"].AsString);

        var reservation = await s.ReservationViewAsync(reservationId);
        Assert.Equal("ACTIVE", reservation!.Status);                                  // validated, not consumed (FU03 §8.4)
        Assert.Equal("30.000", reservation.Quantity);
        Assert.Equal(30m, await s.P.OnHandAsync(s.S01));
        Assert.Equal(1, s.Central("ConfirmPickTaskCommand").Forwarded);
        Assert.Equal("Allocated", (await f.ViewAsync(id)).GetProperty("status").GetString()); // one task still open
    }

    /// <summary>
    /// AC-27: a recalled lot → 422 LOT_UNDER_RECALL (LOCATION_TRANSFER), no movement; a reservation released in FU03 → INVENTORY's
    /// RESERVATION_NOT_ACTIVE → the confirmation Refused, the same code 422, no movement. Sabotage (cf) turns the second red.
    /// </summary>
    [Fact]
    public async Task AC27_recall_refuses_before_the_ledger_and_a_released_reservation_is_refused_by_INVENTORY()
    {
        var (s, f, l1, l2) = await FS2Async(_factory);
        var outbound = await s.CreatedAsync(s.Line(60));
        var id = OutboundScenario.Id(outbound);
        var tasks = await f.PickTasksAsync(id);
        var l2Task = tasks.Single(t => t.GetProperty("lotId").GetString() == l2);
        var l1Task = tasks.Single(t => t.GetProperty("lotId").GetString() == l1);

        f.Recall(l2);
        var recalled = await Expect(f.ConfirmAsync(PutawayScenario.TaskId(l2Task), 30, PutawayScenario.ETagOf(l2Task)), 422);
        Assert.Equal("LOT_UNDER_RECALL", Code(recalled));
        Assert.Equal("LOCATION_TRANSFER", Details(recalled).GetProperty("movementType").GetString());
        Assert.Empty(s.P.TaskMovements(PutawayScenario.TaskId(l2Task)));

        // the L1 reservation released behind 0178's back (FU03 HTTP release through the REAL port)
        var l1Reservation = OutboundFlow.Allocs(outbound).Single(a => a.GetProperty("lotId").GetString() == l1).GetProperty("reservationId").GetString()!;
        var view = await s.ReservationViewAsync(l1Reservation);
        await ReleaseDirectAsync(s, l1Reservation, view!.Etag);
        var refused = await Expect(f.ConfirmAsync(PutawayScenario.TaskId(l1Task), 30, PutawayScenario.ETagOf(l1Task)), 422);
        Assert.Equal("RESERVATION_NOT_ACTIVE", Code(refused));
        Assert.Equal("Refused", PutawayScenario.ConfirmationStatus(await f.PickTaskAsync(PutawayScenario.TaskId(l1Task)), 1));
        Assert.Empty(s.P.TaskMovements(PutawayScenario.TaskId(l1Task)));
    }

    internal static async Task ReleaseDirectAsync(OutboundScenario s, string reservationId, string etag)
    {
        using var scope = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.CreateScope(s.Factory.Services);
        var sp = scope.ServiceProvider;
        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Application.Common.ITenantContext>(sp).SetScope(s.Tenant, s.LegalEntity, s.User);
        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Application.Common.ICorrelationContext>(sp).Set(Guid.NewGuid());
        var port = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Application.Features.Inventory.Services.InventoryReservationPort>(sp);
        var answer = await port.ReleaseAsync(Guid.Parse(reservationId), etag, s.User);
        Assert.True(answer.IsSuccessful, $"direct release: {answer.ErrorCode}");
    }

    /// <summary>Sabotage (cg): accept a target member in the pick body → "body with a target → 400" is red.</summary>
    [Fact]
    public async Task AC27_a_target_or_a_source_in_the_body_is_400()
    {
        var (s, f, _, _) = await FS2Async(_factory);
        var id = OutboundScenario.Id(await s.CreatedAsync(s.Line(30)));
        var task = (await f.PickTasksAsync(id)).Single();
        foreach (var extra in new[] { "toLocationId", "fromLocationId", "deviationReason" })
        {
            var error = await Expect(f.ConfirmAsync(PutawayScenario.TaskId(task), 30, PutawayScenario.ETagOf(task),
                extra: new Dictionary<string, object?> { [extra] = extra == "deviationReason" ? "Hedef degisti 2026" : s.A01.ToString("D") }), 400);
            Assert.Equal("INVALID_REQUEST", Code(error));
        }

        Assert.Empty(s.P.TaskMovements(PutawayScenario.TaskId(task)));
    }

    /// <summary>AC-27: INVENTORY 503 → Pending + 503; "Yeniden dene" with the SAME posting key → Posted, one movement in the ledger.</summary>
    [Fact]
    public async Task AC27_the_confirmation_machine_on_the_pick_path_pending_then_retry_posts_once()
    {
        var (s, f, _, _) = await FS2Async(_factory);
        var id = OutboundScenario.Id(await s.CreatedAsync(s.Line(30)));
        var task = (await f.PickTasksAsync(id)).Single();
        var taskId = PutawayScenario.TaskId(task);

        s.P.LedgerAnswersBefore(503);
        await Expect(f.ConfirmAsync(taskId, 30, PutawayScenario.ETagOf(task)), 503);
        Assert.Equal("Pending", PutawayScenario.ConfirmationStatus(await f.PickTaskAsync(taskId), 1));
        s.P.LedgerHealthy();
        _factory.Clock.Advance(TimeSpan.FromMinutes(5)); // the flight lock passed

        var retried = await Expect(f.RetryAsync(taskId, 1), 200);
        Assert.Equal("Posted", PutawayScenario.ConfirmationStatus(retried, 1));
        var movement = Assert.Single(s.P.TaskMovements(taskId));
        Assert.Equal($"wms:pck:{taskId}:1", movement["IdempotencyKey"].AsString);
        Assert.Equal("Picked", (await f.ViewAsync(id)).GetProperty("status").GetString()); // the retry's terminal write moved the outbound
    }

    /// <summary>AC-73 (reconciler): the S1 sweeper writes a pick confirmation whose answer was lost as Posted — one movement.</summary>
    [Fact]
    public async Task AC73_the_S1_reconciler_posts_a_lost_pick_answer_once()
    {
        var (s, f, _, _) = await FS2Async(_factory);
        var id = OutboundScenario.Id(await s.CreatedAsync(s.Line(30)));
        var task = (await f.PickTasksAsync(id)).Single();
        var taskId = PutawayScenario.TaskId(task);

        s.P.LedgerCommitsThenFails();
        await Expect(f.ConfirmAsync(taskId, 30, PutawayScenario.ETagOf(task)), 503);
        s.P.LedgerHealthy();
        _factory.Clock.Advance(TimeSpan.FromMinutes(5));
        await s.P.ReconcileAsync();

        var after = await f.PickTaskAsync(taskId);
        Assert.Equal("Posted", PutawayScenario.ConfirmationStatus(after, 1));
        Assert.Single(s.P.TaskMovements(taskId));
        Assert.Equal("Picked", (await f.ViewAsync(id)).GetProperty("status").GetString());
    }

    /// <summary>
    /// AC-33 (ii): the Item line's pick → LOCATION_TRANSFER itemId = skuId = K, skuLevel Item, lotId KL1, reservationId.
    /// </summary>
    [Fact]
    public async Task AC33_ii_the_item_pick_moves_the_item_itself_with_its_lot_and_reservation()
    {
        var s = await new OutboundScenario(_factory, level: SkuLevel.Item).ReadyAsync();
        var f = new OutboundFlow(s);
        var kl1 = s.Lot("2027-01-31");
        await s.ReceiveAsync(20, s.A01, kl1);
        var outbound = await s.CreatedAsync(s.Line(20));
        var task = (await f.PickTasksAsync(OutboundScenario.Id(outbound))).Single();
        await f.ConfirmedAsync(task, 20);

        var movement = Assert.Single(s.P.TaskMovements(PutawayScenario.TaskId(task)));
        Assert.Equal(s.Item.ToString(), movement["ItemId"].ToString());
        Assert.Equal(s.Item.ToString(), movement["SkuId"].ToString());
        Assert.Equal("Item", movement["SkuLevel"].ToString());
        Assert.Equal(kl1, movement["LotId"].AsString);
        Assert.Equal(OutboundFlow.Allocs(outbound).Single().GetProperty("reservationId").GetString(), movement["ReservationId"].ToString());
    }

    /// <summary>Sabotage (cx): no Staging storage-condition check in the pick → the COOL_2_8 case is 201 instead of 422.</summary>
    [Fact]
    public async Task AC75_a_cool_SKU_is_not_picked_to_an_untagged_Staging_until_the_tag_is_added()
    {
        var (s, f, _, _) = await FS2Async(_factory, conditions: ["COOL_2_8"]);
        // the pickable bins accept COOL_2_8 (their own role rows), Staging does not yet
        await s.P.W.CreatedAsync(s.A01, [], ["COOL_2_8"]);
        await s.P.W.CreatedAsync(s.A02, [], ["COOL_2_8"]);
        var id = OutboundScenario.Id(await s.CreatedAsync(s.Line(30)));
        var task = (await f.PickTasksAsync(id)).Single();

        var refused = await Expect(f.ConfirmAsync(PutawayScenario.TaskId(task), 30, PutawayScenario.ETagOf(task)), 422);
        Assert.Equal("WMS_STORAGE_CONDITION_MISMATCH", Code(refused));
        Assert.Empty(s.P.TaskMovements(PutawayScenario.TaskId(task)));

        await s.P.W.CreatedAsync(s.S01, [], ["COOL_2_8"]); // S-01's own row (nearest tagged node wins)
        await f.ConfirmedAsync(await f.PickTaskAsync(PutawayScenario.TaskId(task)), 30);
        Assert.Single(s.P.TaskMovements(PutawayScenario.TaskId(task)));
    }

    /// <summary>
    /// AC-72: 20 of 30 + "Kalanı kapat" → Completed (short), outbound Picked, the FU03 reservation still 30 and ACTIVE. Every task closed with 0 →
    /// the outbound stays Allocated and release is 409 WMS_INVALID_TRANSITION (sabotage (cs) makes it Picked).
    /// </summary>
    [Fact]
    public async Task AC72_short_close_keeps_the_reservation_and_all_zero_keeps_the_outbound_Allocated()
    {
        var (s, f, _, _) = await FS2Async(_factory);
        var outbound = await s.CreatedAsync(s.Line(30));
        var id = OutboundScenario.Id(outbound);
        var task = (await f.PickTasksAsync(id)).Single();
        var after = await f.ConfirmedAsync(task, 20);
        var closed = await Expect(f.CloseShortAsync(after), 200);
        Assert.Equal("Completed", PutawayScenario.Status(closed));
        Assert.Equal("Picked", (await f.ViewAsync(id)).GetProperty("status").GetString());
        var reservation = await s.ReservationViewAsync(OutboundFlow.Allocs(outbound).Single().GetProperty("reservationId").GetString()!);
        Assert.Equal(("ACTIVE", "30.000"), (reservation!.Status, reservation.Quantity));

        var zero = OutboundScenario.Id(await s.CreatedAsync(s.Line(20)));
        foreach (var t in await f.PickTasksAsync(zero)) await Expect(f.CloseShortAsync(t), 200);
        var view = await f.ViewAsync(zero);
        Assert.Equal("Allocated", view.GetProperty("status").GetString());
        Assert.Equal("WMS_INVALID_TRANSITION", Code(await Expect(f.ReleaseAsync(zero), 409)));
        await Expect(f.CancelAsync(zero), 200);
    }

    /// <summary>AC-72: two concurrent last confirmations (barrier) → the outbound Picked once, one PickCompleted row.</summary>
    [Fact]
    public async Task AC72_two_concurrent_last_confirmations_make_the_outbound_Picked_once()
    {
        var (s, f, _, _) = await FS2Async(_factory);
        var id = OutboundScenario.Id(await s.CreatedAsync(s.Line(60)));
        var tasks = await f.PickTasksAsync(id);
        Assert.Equal(2, tasks.Count);
        // both postings wait at the ledger gate (their transaction-1 writes done); then their two RESULT writes — the terminal ones — meet at a barrier
        var gate = s.P.LedgerWaitsFor();
        var barrier = new AsyncBarrier(2);
        var writes = 0;
        _factory.BeforeTaskWrite[s.Tenant] = async () =>
        {
            if (Interlocked.Increment(ref writes) > 2) await barrier.SignalAndWaitAsync().WaitAsync(TimeSpan.FromSeconds(20));
        };
        var first = Task.Run(() => f.ConfirmAsync(PutawayScenario.TaskId(tasks[0]), 30, PutawayScenario.ETagOf(tasks[0])));
        var second = Task.Run(() => f.ConfirmAsync(PutawayScenario.TaskId(tasks[1]), 30, PutawayScenario.ETagOf(tasks[1])));
        for (var i = 0; i < 200 && Volatile.Read(ref writes) < 2; i++) await Task.Delay(50);
        gate.SetResult();
        var answers = await Task.WhenAll(first, second);
        _factory.BeforeTaskWrite.TryRemove(s.Tenant, out _);
        Assert.All(answers, a => Assert.Equal(201, (int)a.StatusCode));

        Assert.Equal("Picked", (await f.ViewAsync(id)).GetProperty("status").GetString());
        Assert.Single(f.OutboundEventKinds(id), k => k == "PickCompleted");
    }

    /// <summary>
    /// AC-73 (pick): replay of confirm / claim / close-short with the same key and body → 200 and no new row; another body → 400; stale If-Match →
    /// 409 WMS_CONFLICT; an assigned task is not confirmed by another operator (403 WMS_NOT_ASSIGNEE); "Uzlaştır" needs pick-tasks.resolve (operator 403).
    /// </summary>
    [Fact]
    public async Task AC73_pick_commands_replay_keep_the_assignee_rule_and_the_resolve_key()
    {
        var (s, f, _, _) = await FS2Async(_factory);
        var id = OutboundScenario.Id(await s.CreatedAsync(s.Line(60)));
        var tasks = await f.PickTasksAsync(id);
        var a = tasks[0];
        var aId = PutawayScenario.TaskId(a);

        var claimKey = NewKey();
        var claimed = await Expect(f.ClaimAsync(a, claimKey, f.Operator), 200);
        var replay = await Expect(f.ClaimAsync(a, claimKey, f.Operator), 200);
        Assert.Equal(claimed.GetProperty("version").GetInt32(), replay.GetProperty("version").GetInt32());
        Assert.Single(s.P.EventKinds(aId), k => k == "Claimed");

        var stranger = s.P.TokenOf(Guid.NewGuid(), OutboundFlow.PickRead, OutboundFlow.PickConfirm);
        Assert.Equal("WMS_NOT_ASSIGNEE", Code(await Expect(f.ConfirmAsync(aId, 10, PutawayScenario.ETagOf(claimed), token: stranger), 403)));
        Assert.Equal("WMS_CONFLICT", Code(await Expect(f.ConfirmAsync(aId, 10, "W/\"1\"", token: f.Operator), 409)));

        var key = NewKey();
        var confirmed = await Expect(f.ConfirmAsync(aId, 10, PutawayScenario.ETagOf(claimed), key, f.Operator), 201);
        var again = await Expect(f.ConfirmAsync(aId, 10, PutawayScenario.ETagOf(claimed), key, f.Operator), 200);
        Assert.Equal(confirmed.GetProperty("confirmationSeq").GetInt32(), again.GetProperty("confirmationSeq").GetInt32());
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", Code(await Expect(f.ConfirmAsync(aId, 11, PutawayScenario.ETagOf(claimed), key, f.Operator), 400)));
        Assert.Single(s.P.TaskMovements(aId));
        Assert.Equal(1, s.Central("ConfirmPickTaskCommand").Forwarded);

        var shortKey = NewKey();
        var current = await f.PickTaskAsync(aId);
        await Expect(f.CloseShortAsync(current, key: shortKey, token: f.Operator), 200);
        await Expect(f.CloseShortAsync(current, key: shortKey, token: f.Operator), 200);
        Assert.Single(s.P.EventKinds(aId), k => k == "ClosedShort");
        Assert.Equal(1, s.Central("ClosePickTaskShortCommand").Forwarded);

        await Expect(f.ReasonedAsync($"/api/warehouse/pick-tasks/{aId}/confirmations/1/resolve-integrity", PutawayScenario.Reason,
            PutawayScenario.ETagOf(await f.PickTaskAsync(aId)), token: f.Operator), 403); // the operator has no warehouse.pick-tasks.resolve
    }

    /// <summary>AC-73: a pick task of another company is 404; a putaway task id is unknown on the pick paths (and vice versa).</summary>
    [Fact]
    public async Task AC73_pick_paths_are_company_bound_and_kind_bound()
    {
        var (s, f, _, _) = await FS2Async(_factory);
        var id = OutboundScenario.Id(await s.CreatedAsync(s.Line(30)));
        var task = (await f.PickTasksAsync(id)).Single();
        var taskId = PutawayScenario.TaskId(task);

        var foreign = await new OutboundScenario(_factory).ReadyAsync();
        var foreignFlow = new OutboundFlow(foreign);
        Assert.Equal("WMS_TASK_NOT_FOUND", Code(await Expect(foreign.P.W.GetAsync($"/api/warehouse/pick-tasks/{taskId}", foreignFlow.Supervisor), 404)));
        Assert.Equal("WMS_TASK_NOT_FOUND", Code(await Expect(foreignFlow.ConfirmAsync(taskId, 1, PutawayScenario.ETagOf(task)), 404)));
        Assert.Equal("WMS_TASK_NOT_FOUND", Code(await Expect(s.P.W.GetAsync($"/api/warehouse/putaway-tasks/{taskId}", f.Supervisor), 404)));
        Assert.DoesNotContain("shipTo", (await f.PickTaskAsync(taskId)).ToString());
        Assert.Equal(id, (await f.PickTaskAsync(taskId)).GetProperty("outbound").GetProperty("outboundId").GetString());
        Assert.Equal("Allocated", (await f.PickTaskAsync(taskId)).GetProperty("outbound").GetProperty("status").GetString());
    }
}

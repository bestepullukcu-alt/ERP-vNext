using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Inventory;
using Diten.InventoryService.Application.Features.Inventory.Services;
using Diten.InventoryService.Tests.CentralAudit;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;
using static Diten.InventoryService.Tests.Warehouse.OutboundScenario;
using static Diten.InventoryService.Tests.Warehouse.PutawayRecoveryTests;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-4 AC-71 (pack §8.17 steps 5–6) — an interrupted allocation: after transaction A the record is <c>Allocating</c> with its WHOLE plan;
/// the maintenance service touches it only after the recovery lock and re-sends the SAME FU03 batch with the SAME keys (one reservation set, never
/// two; the plan is never recomputed); an unknown FU03 outcome is never read as "not reserved"; the user's repeat answers 409 / 200 / 422 / 400;
/// FU03's 400 <c>IDEMPOTENCY_KEY_REUSED</c> → <c>AllocationFailed (KeyReused)</c> with every allocation <c>Pending</c>. (The keyed release of
/// those allocations is DL-5; the S3 <c>AllocationStuck</c> / <c>AllocationOrphanSuspected</c> reads are DL-7.)
/// </summary>
public sealed class OutboundRecoveryTests : IClassFixture<WarehouseApiFactory>
{
    private readonly WarehouseApiFactory _factory;

    public OutboundRecoveryTests(WarehouseApiFactory factory) => _factory = factory;

    internal static async Task<(OutboundScenario S, string L1, string L2)> WorldAsync(WarehouseApiFactory factory)
    {
        var s = await new OutboundScenario(factory).ReadyAsync();
        var l1 = s.Lot("2027-01-31");
        var l2 = s.Lot("2027-03-31");
        await s.ReceiveAsync(30, s.A02, l1);
        await s.ReceiveAsync(50, s.A01, l2);
        return (s, l1, l2);
    }

    private void PastLease() => _factory.Clock.Advance(TimeSpan.FromSeconds(121));

    /// <summary>
    /// (ii) the batch committed, the process stopped before transaction B → the maintenance re-sends the same keys, FU03 answers its replay, one
    /// reservation set. Sabotage (cq): an attempt number added to the keys on the maintenance's repeat → two sets (red).
    /// </summary>
    [Fact]
    public async Task AC71_ii_a_batch_committed_before_transaction_B_is_completed_with_the_same_keys_and_one_reservation_set()
    {
        var (s, l1, l2) = await WorldAsync(_factory);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        _factory.AroundBatch[s.Tenant] = async (_, real) =>
        {
            var answer = await real();
            if (Interlocked.Increment(ref calls) > 1) return answer;              // the maintenance's call passes through
            await stopped.Task.WaitAsync(TimeSpan.FromSeconds(60));              // the request's process "stopped" after the commit
            throw new InvalidOperationException("test: the process is gone");
        };
        Task<HttpResponseMessage> request;
        try
        {
            request = s.CreateAsync(s.Body(s.Line(60)));
            await WaitForAsync(() => s.Reservations().Count == 2);
            var id = s.StoredOutbounds().Single()["_id"].ToString()!;
            Assert.Equal("Allocating", s.StoredOutbound(id)["Status"].AsString);

            PastLease();
            var tick = await s.MaintainAsync();

            Assert.Equal(1, tick.CommandsSent);
            Assert.Equal("Allocated", s.StoredOutbound(id)["Status"].AsString);
            Assert.Equal(2, s.Reservations(id).Count);                           // no second set
            Assert.Equal(2, s.PickTasks(id).Count);
            Assert.Equal([(l1, 30m, 1), (l2, 30m, 2)], AllocationSummary(await Expect(s.ViewAsync(id), 200)));
            Assert.Equal((1, 0), s.Central("CompleteOutboundAllocationCommand"));
        }
        finally
        {
            stopped.TrySetResult();
        }

        await Expect(request, 503);
        _factory.AroundBatch.TryRemove(s.Tenant, out _);
    }

    /// <summary>
    /// (vii) the persisted plan: between the stop and the maintenance an earlier-expiry lot arrives; the maintenance sends transaction A's plan,
    /// FU03 replays, the lots stay. Sabotage (dv): the maintenance recomputes the plan → FU03 400 → <c>AllocationFailed</c> (red).
    /// </summary>
    [Fact]
    public async Task AC71_vii_the_maintenance_sends_the_persisted_plan_even_when_the_FEFO_order_changed()
    {
        var (s, l1, l2) = await WorldAsync(_factory);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        _factory.AroundBatch[s.Tenant] = async (_, real) =>
        {
            var answer = await real();
            if (Interlocked.Increment(ref calls) > 1) return answer;
            await stopped.Task.WaitAsync(TimeSpan.FromSeconds(60));
            throw new InvalidOperationException("test: the process is gone");
        };
        Task<HttpResponseMessage> request;
        try
        {
            request = s.CreateAsync(s.Body(s.Line(60)));
            await WaitForAsync(() => s.Reservations().Count == 2);
            var id = s.StoredOutbounds().Single()["_id"].ToString()!;
            var l0 = s.Lot("2026-12-31");                                        // an earlier expiry arrives in between
            await s.ReceiveAsync(60, s.A01, l0);

            PastLease();
            await s.MaintainAsync();

            Assert.Equal("Allocated", s.StoredOutbound(id)["Status"].AsString);
            Assert.Equal([(l1, 30m, 1), (l2, 30m, 2)], AllocationSummary(await Expect(s.ViewAsync(id), 200)));
            Assert.Equal(2, s.Reservations(id).Count);
        }
        finally
        {
            stopped.TrySetResult();
        }

        await Expect(request, 503);
        _factory.AroundBatch.TryRemove(s.Tenant, out _);
    }

    /// <summary>
    /// (vi) an exception AFTER the FU03 commit → 503 <c>INVENTORY_UNAVAILABLE</c> / <c>OutcomeUnknown</c>, the record stays <c>Allocating</c>, no
    /// <c>AllocationFailed</c>; the maintenance completes it with the one reservation set. Sabotage (dc): the exception mapped to
    /// <c>AllocationFailed</c> → the record fails and the reservations are left ownerless (red).
    /// </summary>
    [Fact]
    public async Task AC71_vi_an_unknown_batch_outcome_is_never_read_as_not_reserved()
    {
        var (s, _, _) = await WorldAsync(_factory);
        var calls = 0;
        _factory.AroundBatch[s.Tenant] = async (_, real) =>
        {
            var answer = await real();
            if (Interlocked.Increment(ref calls) == 1) throw new TimeoutException("test: the answer was lost after the commit");
            return answer;
        };

        var unknown = await Expect(s.CreateAsync(s.Body(s.Line(60))), 503);

        Assert.Equal("INVENTORY_UNAVAILABLE", Code(unknown));
        Assert.Equal("OutcomeUnknown", Details(unknown).GetProperty("reason").GetString());
        var stored = s.StoredOutbounds().Single();
        var id = stored["_id"].ToString()!;
        Assert.Equal("Allocating", stored["Status"].AsString);
        Assert.False(stored.Contains("AllocationFailure") && !stored["AllocationFailure"].IsBsonNull);
        Assert.Equal(2, s.Reservations(id).Count);
        Assert.Equal((0, 1), s.Central("CreateOutboundShipmentCommand"));      // transaction A committed, the command failed → Abandoned

        PastLease();
        await s.MaintainAsync();

        Assert.Equal("Allocated", s.StoredOutbound(id)["Status"].AsString);
        Assert.Equal(2, s.Reservations(id).Count);
        Assert.Equal((1, 0), s.Central("CompleteOutboundAllocationCommand"));
        _factory.AroundBatch.TryRemove(s.Tenant, out _);
    }

    /// <summary>
    /// (iii) the user's repeat with the same key: 409 while <c>Allocating</c> (no write), 200 replay once <c>Allocated</c>, 400 for another body;
    /// a failed allocation's repeat answers its stored 422 with the FU03 line results.
    /// </summary>
    [Fact]
    public async Task AC71_iii_the_same_key_answers_409_while_allocating_then_200_and_another_body_400()
    {
        var (s, _, l2) = await WorldAsync(_factory);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _factory.AroundBatch[s.Tenant] = async (_, real) =>
        {
            await gate.Task.WaitAsync(TimeSpan.FromSeconds(60));
            return await real();
        };
        var body = s.Body(s.Line(60));
        Task<HttpResponseMessage> first;
        try
        {
            first = s.CreateAsync(body);
            await WaitForAsync(() => s.StoredOutbounds().Count == 1);
            var events = s.OutboundEvents().Count;

            Assert.Equal("WMS_OUTBOUND_ALLOCATION_IN_PROGRESS", await ErrorCode(s.CreateAsync(body), 409));
            Assert.Equal(events, s.OutboundEvents().Count);
        }
        finally
        {
            gate.TrySetResult();
        }

        var created = await Expect(first, 201);
        _factory.AroundBatch.TryRemove(s.Tenant, out _);
        var replay = await Expect(s.CreateAsync(body), 200);
        Assert.Equal(OutboundScenario.Id(created), OutboundScenario.Id(replay));
        Assert.Equal(2, s.OutboundEvents().Count);
        var other = new Dictionary<string, object?>(body) { ["note"] = "another body" };
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", await ErrorCode(s.CreateAsync(other), 400));

        // a failed allocation: its repeat answers the stored refusal (L1 is wholly this outbound's now; the 20 come from L2's remaining 20,
        // which another exit takes between the plan and the batch)
        _factory.AroundBatch[s.Tenant] = async (_, real) =>
        {
            await s.ReserveOtherAsync(20, l2);
            return await real();
        };
        var failedBody = s.Body(s.Line(20));
        var failed = await Expect(s.CreateAsync(failedBody), 422);
        _factory.AroundBatch.TryRemove(s.Tenant, out _);
        var failedReplay = await Expect(s.CreateAsync(failedBody), 422);
        Assert.Equal("WMS_ALLOCATION_FAILED", Code(failedReplay));
        Assert.Equal(Details(failed).GetProperty("lines").GetRawText(), Details(failedReplay).GetProperty("lines").GetRawText());
    }

    /// <summary>
    /// FU03 400 branch (r24): the hook between transaction A and the batch opens reservations with two of the outbound's three keys (one is then
    /// released), the third not → FU03 400 → <c>AllocationFailed</c> + <c>{ KeyReused, lines: [] }</c>, every allocation <c>Pending</c>, event
    /// <c>Data.kind = KeyReused</c>; the user's repeat answers 422 <c>ReservationKeyConflict</c>. Sabotage (ea): the Pending marking removed (red).
    /// </summary>
    [Fact]
    public async Task AC71_vii_FU03_400_marks_the_outbound_KeyReused_with_every_allocation_pending_release()
    {
        var s = await new OutboundScenario(_factory).ReadyAsync();
        foreach (var (expiry, bin) in new[] { ("2027-01-31", s.A01), ("2027-02-28", s.A02), ("2027-03-31", s.A01) })
        {
            await s.ReceiveAsync(10, bin, s.Lot(expiry));
        }

        _factory.AroundBatch[s.Tenant] = async (lines, real) =>
        {
            using var scope = _factory.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetScope(s.Tenant, s.LegalEntity, s.User);
            scope.ServiceProvider.GetRequiredService<ICorrelationContext>().Set(Guid.NewGuid());
            var port = scope.ServiceProvider.GetRequiredService<InventoryReservationPort>();
            var first = await port.CreateAsync(lines[0], s.LegalEntity, s.User);
            Assert.True(first.IsSuccessful, first.ErrorCode);
            var second = await port.CreateAsync(lines[1], s.LegalEntity, s.User);
            Assert.True(second.IsSuccessful, second.ErrorCode);
            Assert.True((await port.ReleaseAsync(Guid.Parse(first.Data!.ReservationId), first.Data.Etag, s.User)).IsSuccessful);
            return await real();
        };
        var body = s.Body(s.Line(30));

        var refused = await Expect(s.CreateAsync(body), 422);

        Assert.Equal("WMS_ALLOCATION_FAILED", Code(refused));
        Assert.Equal("ReservationKeyConflict", Details(refused).GetProperty("reason").GetString());
        Assert.Empty(Details(refused).GetProperty("lines").EnumerateArray());
        var stored = s.StoredOutbounds().Single();
        Assert.Equal("AllocationFailed", stored["Status"].AsString);
        Assert.Equal("KeyReused", stored["AllocationFailure"]["Kind"].AsString);
        Assert.Empty(stored["AllocationFailure"]["Lines"].AsBsonArray);
        var states = stored["Lines"].AsBsonArray.SelectMany(l => l["Allocations"].AsBsonArray).Select(a => a["ReservationReleaseState"].AsString).ToList();
        Assert.Equal(["Pending", "Pending", "Pending"], states);
        var failedEvent = Assert.Single(s.OutboundEvents(), e => e["Kind"].AsString == "AllocationFailed");
        Assert.Equal("KeyReused", failedEvent["Data"]["kind"].AsString);
        Assert.Equal(0, (await Expect(s.SearchAsync(), 200)).GetProperty("total").GetInt64());
        _factory.AroundBatch.TryRemove(s.Tenant, out _);

        var replay = await Expect(s.CreateAsync(body), 422);
        Assert.Equal("ReservationKeyConflict", Details(replay).GetProperty("reason").GetString());
    }
}

/// <summary>
/// AC-71 (i) + (iv) — the process stopped right after transaction A (held inside the FU03 call): <c>Allocating</c>, 0 reservations; before the lock
/// passes the maintenance does not touch it; the dispatcher releases the create row as <c>Unconfirmed</c> after <c>OrphanAfter</c>; after the
/// lock the maintenance completes it (<c>Allocated</c>, as many reservations as allocations) with ONE <c>CompleteOutboundAllocationCommand</c>
/// row whose actor is the outbound's creator. Runs alone (the dispatcher's orphan release touches the whole shared outbox).
/// Sabotage (cr): the lock condition removed from the maintenance's selection (and the command's own check) → touched at once (red).
/// </summary>
[Collection(PutawayCentralAuditAloneCollection.Name)]
public sealed class OutboundAllocationCrashTests : IClassFixture<WarehouseApiFactory>
{
    private readonly WarehouseApiFactory _factory;

    public OutboundAllocationCrashTests(WarehouseApiFactory factory) => _factory = factory;

    private List<CentralAuditOutboxRecord> Rows(OutboundScenario s, string requestType)
        => CentralAuditInventoryTests.Outbox(_factory).Find(x => x.TenantId == s.Tenant && x.RequestType == requestType).ToList();

    [Fact]
    public async Task AC71_i_iv_a_command_stopped_after_transaction_A_is_completed_by_the_maintenance_after_the_lock()
    {
        var (s, _, _) = await OutboundRecoveryTests.WorldAsync(_factory);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        _factory.AroundBatch[s.Tenant] = async (_, real) =>
        {
            if (Interlocked.Increment(ref calls) > 1) return await real();     // the maintenance's call
            await stopped.Task.WaitAsync(TimeSpan.FromSeconds(60));            // the request "stopped" before the batch
            throw new InvalidOperationException("test: the process is gone");
        };
        Task<HttpResponseMessage> request;
        try
        {
            request = s.CreateAsync(s.Body(s.Line(60)));
            await WaitForAsync(() => Rows(s, "CreateOutboundShipmentCommand") is [{ Status: CentralAuditOutboxStatus.AwaitingCommand }]);
            var id = s.StoredOutbounds().Single()["_id"].ToString()!;
            Assert.Equal("Allocating", s.StoredOutbound(id)["Status"].AsString);
            Assert.Empty(s.Reservations(id));

            var early = await s.MaintainAsync();                               // the lock has not passed: untouched
            Assert.Equal(0, early.CommandsSent);
            Assert.Equal("Allocating", s.StoredOutbound(id)["Status"].AsString);
            Assert.Empty(s.Reservations(id));

            _factory.Clock.Advance(TimeSpan.FromMinutes(11));                  // past the lock (120 s) and OrphanAfter (10 min)
            await _factory.Services.GetRequiredService<CentralAuditDispatcher>().RunOnceAsync(CancellationToken.None);
            var released = Assert.Single(Rows(s, "CreateOutboundShipmentCommand"));
            Assert.Equal(CentralAuditOutboxStatus.Pending, released.Status);
            Assert.False(released.ConfirmedByCommand);                         // Platform: Unconfirmed
            Assert.Equal(Guid.Parse(id), released.EntityId);

            var tick = await s.MaintainAsync();
            Assert.Equal(1, tick.CommandsSent);
            Assert.Equal("Allocated", s.StoredOutbound(id)["Status"].AsString);
            Assert.Equal(2, s.Reservations(id).Count);
            var completed = Assert.Single(Rows(s, "CompleteOutboundAllocationCommand"));
            Assert.Equal(s.User, completed.ActorId);                           // the actor is the outbound's creator
            Assert.Equal("Execute", completed.Operation);
            Assert.Equal(Guid.Parse(id), completed.EntityId);
        }
        finally
        {
            stopped.TrySetResult();
        }

        await Expect(request, 503);
        var after = Assert.Single(Rows(s, "CreateOutboundShipmentCommand"));
        Assert.False(after.ConfirmedByCommand);
        _factory.AroundBatch.TryRemove(s.Tenant, out _);
    }
}

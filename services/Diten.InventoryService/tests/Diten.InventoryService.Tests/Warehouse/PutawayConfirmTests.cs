using System.Text.Json;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Inventory;
using Xunit;
using static Diten.InventoryService.Tests.Warehouse.PutawayScenario;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-2 — confirmations and the task state machine on the REAL host (pack §8.3, §8.13, §8.15; AC-4, AC-9, AC-11, AC-12, AC-39, AC-47,
/// AC-49, AC-55, AC-56): replays, the ledger's refusals passed on, the transitions, If-Match, the Pending lock, the assignee rule, the
/// Refused → new confirmation / "returned" flows and the retry pre-checks.
/// </summary>
public sealed class PutawayConfirmTests : IClassFixture<WarehouseApiFactory>
{
    private readonly WarehouseApiFactory _factory;

    public PutawayConfirmTests(WarehouseApiFactory factory) => _factory = factory;

    private async Task<(PutawayScenario S, JsonElement Task)> OpenAsync(decimal stock = 100, decimal planned = 60)
    {
        var s = await new PutawayScenario(_factory).ReadyAsync();
        await s.ReceiveAsync(stock);
        return (s, await s.TaskAsync(planned));
    }

    /// <summary>The first confirmation refused by the REAL ledger (the stock was issued away).</summary>
    private static async Task<JsonElement> RefusedFirstAsync(PutawayScenario s, JsonElement task, decimal quantity = 60)
    {
        await s.PostLedgerAsync(s.Issue(99)); // 1 left of the 100
        var refused = await Expect(s.ConfirmAsync(task, quantity, s.A01), 422);
        Assert.Equal("INSUFFICIENT_STOCK", Code(refused));
        return DetailsTask(refused);
    }

    // ── AC-4 replays ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC4_create_replay_is_200_same_task_other_body_400_and_keys_are_per_user()
    {
        var s = await new PutawayScenario(_factory).ReadyAsync();
        await s.ReceiveAsync(100);
        var body = s.TaskBody(10);
        var first = await Expect(s.CreateTaskAsync(body), 201);
        var replay = await Expect(s.CreateTaskAsync(body), 200);
        Assert.Equal(TaskId(first), TaskId(replay));
        Assert.Equal(1, replay.GetProperty("version").GetInt32());

        var changed = new Dictionary<string, object?>(body) { ["plannedQuantity"] = "11" };
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", await ErrorCode(s.CreateTaskAsync(changed), 400));
        Assert.Single(s.W.Stored("wms_tasks"));
        Assert.Equal((1, 0), s.Central("CreatePutawayTaskCommand"));

        var other = s.TokenOf(Guid.NewGuid(), TaskRead, TaskCreate);
        var second = await Expect(s.CreateTaskAsync(body, other), 201);
        Assert.NotEqual(TaskId(first), TaskId(second));
    }

    [Fact]
    public async Task AC4_confirm_replay_with_the_first_if_match_is_200_one_movement_no_new_event()
    {
        var (s, task) = await OpenAsync();
        var key = NewKey();
        var first = await Expect(s.ConfirmAsync(task, 20, s.A01, key: key), 201);
        var events = s.TaskEvents(TaskId(task)).Count;

        var replay = await Expect(s.ConfirmAsync(task, 20, s.A01, key: key), 200);
        Assert.Equal(first.GetProperty("version").GetInt32(), replay.GetProperty("version").GetInt32());
        Assert.Equal(1, replay.GetProperty("confirmationSeq").GetInt32());
        Assert.Single(s.TaskMovements(TaskId(task)));
        Assert.Equal(events, s.TaskEvents(TaskId(task)).Count);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", await ErrorCode(s.ConfirmAsync(task, 21, s.A01, key: key), 400));
        Assert.Equal((1, 0), s.Central("ConfirmPutawayTaskCommand"));
    }

    [Fact]
    public async Task AC4_a_refused_first_confirmation_replays_200_with_its_own_seq_after_a_later_posted_one()
    {
        var (s, task) = await OpenAsync(planned: 60);
        await s.PostLedgerAsync(s.Issue(50));
        var key = NewKey();
        var refused = await Expect(s.ConfirmAsync(task, 60, s.A01, key: key), 422);
        Assert.Equal(1, refused.GetProperty("error").GetProperty("details").GetProperty("seq").GetInt32());
        var after = DetailsTask(refused);
        await Expect(s.ConfirmAsync(after, 50, s.A01), 201);

        var replay = await Expect(s.ConfirmAsync(task, 60, s.A01, key: key), 200);
        Assert.Equal(1, replay.GetProperty("confirmationSeq").GetInt32());
        Assert.Equal("Refused", ConfirmationStatus(replay, 1));
        Assert.Equal("Posted", ConfirmationStatus(replay, 2));
    }

    [Fact]
    public async Task AC4_two_identical_concurrent_confirmations_write_once_and_the_twin_replays()
    {
        var (s, task) = await OpenAsync();
        var key = NewKey();
        var barrier = new AsyncBarrier(2);
        _factory.BeforeTaskWrite[s.Tenant] = barrier.SignalAndWaitAsync;
        HttpResponseMessage[] answers;
        try
        {
            answers = await Task.WhenAll(s.ConfirmAsync(task, 20, s.A01, key: key), s.ConfirmAsync(task, 20, s.A01, key: key));
        }
        finally
        {
            _factory.BeforeTaskWrite.TryRemove(s.Tenant, out _);
        }

        Assert.Equal([200, 201], answers.Select(a => (int)a.StatusCode).Order());
        Assert.Single(s.TaskMovements(TaskId(task)));
        Assert.Single((await s.GetTaskAsync(TaskId(task))).GetProperty("confirmations").EnumerateArray());
        // the loser's write touched 0 records: it enlists nothing, so the replaying command forwards no second row (sabotage (bk))
        Assert.Equal((1, 0), s.Central("ConfirmPutawayTaskCommand"));
        Assert.Single(s.TaskEvents(TaskId(task)), e => e["Kind"].AsString == "ConfirmationRecorded");
    }

    [Fact]
    public async Task AC4_claim_update_cancel_replays_are_200_without_a_new_version()
    {
        var (s, task) = await OpenAsync();
        var claimKey = NewKey();
        var claimed = await Expect(s.ClaimAsync(TaskId(task), ETagOf(task), claimKey), 200);
        Assert.Equal(claimed.GetProperty("version").GetInt32(), (await Expect(s.ClaimAsync(TaskId(task), ETagOf(task), claimKey), 200)).GetProperty("version").GetInt32());

        var updateKey = NewKey();
        var updated = await Expect(s.UpdateTaskAsync(TaskId(task), new() { ["note"] = "ilk raf" }, ETagOf(claimed), updateKey), 200);
        Assert.Equal(updated.GetProperty("version").GetInt32(),
            (await Expect(s.UpdateTaskAsync(TaskId(task), new() { ["note"] = "ilk raf" }, ETagOf(claimed), updateKey), 200)).GetProperty("version").GetInt32());
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", await ErrorCode(s.UpdateTaskAsync(TaskId(task), new() { ["note"] = "baska" }, ETagOf(claimed), updateKey), 400));

        var cancelKey = NewKey();
        var cancelled = await Expect(s.CancelAsync(TaskId(task), ETagOf(updated), key: cancelKey), 200);
        var replay = await Expect(s.CancelAsync(TaskId(task), ETagOf(updated), key: cancelKey), 200);
        Assert.Equal("Cancelled", Status(replay));
        Assert.Equal(cancelled.GetProperty("version").GetInt32(), replay.GetProperty("version").GetInt32());
        Assert.Equal(["Created", "Claimed", "Updated", "Cancelled"], s.EventKinds(TaskId(task)));
        Assert.Equal((1, 0), s.Central("CancelPutawayTaskCommand"));
    }

    [Fact]
    public async Task AC4_returned_and_close_short_replays_are_200()
    {
        var (s, task) = await OpenAsync(planned: 60);
        var refusedTask = await RefusedFirstAsync(s, task);
        var returnKey = NewKey();
        var returned = await Expect(s.ReturnedAsync(TaskId(task), 1, ETagOf(refusedTask), key: returnKey), 200);
        Assert.Equal(returned.GetProperty("version").GetInt32(),
            (await Expect(s.ReturnedAsync(TaskId(task), 1, ETagOf(refusedTask), key: returnKey), 200)).GetProperty("version").GetInt32());

        var posted = await Expect(s.ConfirmAsync(returned, 1, s.A01), 201);
        var closeKey = NewKey();
        var closed = await Expect(s.CloseShortAsync(TaskId(task), ETagOf(posted), key: closeKey), 200);
        Assert.Equal("Completed", Status(closed));
        Assert.Equal(closed.GetProperty("version").GetInt32(),
            (await Expect(s.CloseShortAsync(TaskId(task), ETagOf(posted), key: closeKey), 200)).GetProperty("version").GetInt32());
        Assert.DoesNotContain("Completed", s.EventKinds(TaskId(task)));
        Assert.Contains("ClosedShort", s.EventKinds(TaskId(task)));
    }

    // ── AC-9 / AC-49 ledger refusals ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC9_insufficient_stock_is_passed_on_and_the_task_quantity_does_not_move()
    {
        var (s, task) = await OpenAsync(planned: 60);
        var refusedTask = await RefusedFirstAsync(s, task);
        Assert.Equal("Refused", ConfirmationStatus(refusedTask, 1));
        Assert.Equal(0m, D(refusedTask, "confirmedQuantity"));
        Assert.Equal("PostingRefused", Flag(refusedTask));
        Assert.Equal("INSUFFICIENT_STOCK", Confirmation(refusedTask, 1).GetProperty("error").GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("LOT_BLOCKED")]
    [InlineData("LOT_EXPIRED")]
    [InlineData("LOT_RETEST_DUE")]
    public async Task AC9_lot_refusals_of_the_ledger_keep_their_code_and_details(string code)
    {
        var (s, task) = await OpenAsync();
        _factory.AroundPosting[s.Tenant] = (_, _) => Task.FromResult(Response<MovementResponseDto>.Fail(code, "test: lot refused", 422,
            new Dictionary<string, object?> { ["lotId"] = Lot }));
        try
        {
            var refused = await Expect(s.ConfirmAsync(task, 20, s.A01), 422);
            Assert.Equal(code, Code(refused));
            Assert.Equal(Lot, refused.GetProperty("error").GetProperty("details").GetProperty("lotId").GetString());
            Assert.Equal("Refused", ConfirmationStatus(DetailsTask(refused), 1));
            Assert.Equal(0m, D(DetailsTask(refused), "confirmedQuantity"));
        }
        finally
        {
            s.LedgerHealthy();
        }
    }

    [Fact]
    public async Task AC49_held_stock_after_the_task_opened_refuses_the_confirmation_and_the_task_stays_open()
    {
        var (s, task) = await OpenAsync(stock: 60, planned: 60);
        await s.PostLedgerAsync(s.StatusTransfer(60, "AVAILABLE", "QUALITY_INSPECTION"));

        var refused = await Expect(s.ConfirmAsync(task, 60, s.A01), 422);
        Assert.Equal("WMS_STOCK_NOT_PUTAWAY_ELIGIBLE", Code(refused));
        var current = await s.GetTaskAsync(TaskId(task));
        Assert.Equal("Open", Status(current));
        Assert.Empty(s.TaskMovements(TaskId(task)));
    }

    // ── AC-11 / AC-12 / AC-39 state, If-Match and the Pending lock ───────────────────────────────────────

    [Fact]
    public async Task AC11_transitions_cancel_only_without_posted_close_short_only_with_posted_terminal_refuses()
    {
        var (s, task) = await OpenAsync();
        Assert.Equal("WMS_INVALID_TRANSITION", await ErrorCode(s.CloseShortAsync(TaskId(task), ETagOf(task)), 409));
        var posted = await Expect(s.ConfirmAsync(task, 20, s.A01), 201);
        Assert.Equal("WMS_INVALID_TRANSITION", await ErrorCode(s.CancelAsync(TaskId(task), ETagOf(posted)), 409));
        var closed = await Expect(s.CloseShortAsync(TaskId(task), ETagOf(posted)), 200);
        Assert.Equal("Completed", Status(closed));
        Assert.Equal(20m, await s.OnHandAsync(s.A01));
        Assert.Equal("WMS_INVALID_TRANSITION", await ErrorCode(s.ConfirmAsync(closed, 1, s.A01), 409));
        Assert.Equal("WMS_INVALID_TRANSITION", await ErrorCode(s.CancelAsync(TaskId(task), ETagOf(closed)), 409));
        Assert.Equal("REASON_REQUIRED", await ErrorCode(s.CancelAsync(TaskId(task), ETagOf(closed), "kisa"), 400));
    }

    [Fact]
    public async Task AC12_a_stale_if_match_is_409_and_a_missing_one_400()
    {
        var (s, task) = await OpenAsync();
        await Expect(s.ConfirmAsync(task, 10, s.A01), 201);
        Assert.Equal("WMS_CONFLICT", await ErrorCode(s.ConfirmAsync(task, 10, s.A01), 409));
        Assert.Equal("WMS_CONFLICT", await ErrorCode(s.UpdateTaskAsync(TaskId(task), new() { ["note"] = "x" }, ETagOf(task)), 409));
        var body = s.ConfirmBody(10, s.A01);
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.ConfirmRawAsync(TaskId(task), body, null), 400));
        Assert.Single(s.TaskMovements(TaskId(task)));
    }

    [Fact]
    public async Task AC39_a_pending_confirmation_locks_everything_but_retry()
    {
        var (s, task) = await OpenAsync();
        s.LedgerAnswersBefore(503);
        var pending = DetailsTask(await Expect(s.ConfirmAsync(task, 20, s.A01), 503));
        s.LedgerHealthy();
        Assert.Equal("Pending", ConfirmationStatus(pending, 1));
        Assert.Equal("PostingPending", Flag(pending));
        Assert.Equal(["Retry"], Strings(pending.GetProperty("allowedActions")));

        var etag = ETagOf(pending);
        Assert.Equal("WMS_CONFIRMATION_PENDING", await ErrorCode(s.ConfirmAsync(pending, 10, s.A01), 409));
        Assert.Equal("WMS_CONFIRMATION_PENDING", await ErrorCode(s.UpdateTaskAsync(TaskId(task), new() { ["note"] = "x" }, etag), 409));
        Assert.Equal("WMS_CONFIRMATION_PENDING", await ErrorCode(s.ClaimAsync(TaskId(task), etag), 409));
        Assert.Equal("WMS_CONFIRMATION_PENDING", await ErrorCode(s.CancelAsync(TaskId(task), etag), 409));
        Assert.Equal("WMS_CONFIRMATION_PENDING", await ErrorCode(s.CloseShortAsync(TaskId(task), etag), 409));
        Assert.Equal(1, (await s.GetTaskAsync(TaskId(task))).GetProperty("confirmations").GetArrayLength());

        _factory.Clock.Advance(TimeSpan.FromMinutes(3));
        var retried = await Expect(s.RetryAsync(TaskId(task), 1), 200);
        Assert.Equal("Posted", ConfirmationStatus(retried, 1));
        Assert.Single(s.TaskMovements(TaskId(task)));
    }

    // ── AC-47 the assignee rule ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC47_claim_then_only_the_assignee_or_a_supervisor_confirms()
    {
        var (s, task) = await OpenAsync();
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var aliceToken = s.TokenOf(alice, TaskRead, TaskConfirm);
        var bobToken = s.TokenOf(bob, TaskRead, TaskConfirm);

        Assert.Equal("WMS_NOT_ASSIGNEE", await ErrorCode(s.ConfirmAsync(task, 5, s.A01, token: aliceToken), 403));
        var claimed = await Expect(s.ClaimAsync(TaskId(task), ETagOf(task), token: aliceToken), 200);
        Assert.Equal(alice.ToString("D"), claimed.GetProperty("assignee").GetProperty("userId").GetString());
        Assert.Equal("WMS_TASK_ALREADY_ASSIGNED", await ErrorCode(s.ClaimAsync(TaskId(task), ETagOf(claimed), token: bobToken), 409));
        Assert.Equal("WMS_NOT_ASSIGNEE", await ErrorCode(s.ConfirmAsync(claimed, 5, s.A01, token: bobToken), 403));

        var mine = await Expect(s.ConfirmAsync(claimed, 5, s.A01, token: aliceToken), 201);
        await Expect(s.ConfirmAsync(mine, 5, s.A01, token: s.Supervisor), 201);
        Assert.Equal(2, s.TaskMovements(TaskId(task)).Count);
    }

    // ── AC-55 / AC-56 after a refusal ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC55_a_new_posted_confirmation_supersedes_the_refused_one_and_clears_the_flag()
    {
        var (s, task) = await OpenAsync(planned: 60);
        var refusedTask = await RefusedFirstAsync(s, task, 60);
        await s.ReceiveAsync(59);

        var posted = await Expect(s.ConfirmAsync(refusedTask, 60, s.B01), 201);
        Assert.Equal("SupersededByNewConfirmation", Confirmation(posted, 1).GetProperty("resolution").GetString());
        Assert.Equal("None", Flag(posted));
        Assert.Equal("Completed", Status(posted));
        var superseded = Assert.Single(s.TaskEvents(TaskId(task)), e => e["Kind"].AsString == "ConfirmationSuperseded");
        Assert.Equal("1", superseded["Data"]["seq"].AsString);
        Assert.Equal("1", Assert.Single(s.TaskEvents(TaskId(task)), e => e["Kind"].AsString == "ConfirmationPosted")["Data"]["superseded"].AsString);
        Assert.Equal((1, 1), s.Central("ConfirmPutawayTaskCommand"));
    }

    [Fact]
    public async Task AC55_returned_closes_the_refusal_without_a_movement_then_cancel_works()
    {
        var (s, task) = await OpenAsync(planned: 60);
        var refusedTask = await RefusedFirstAsync(s, task, 60);

        var returned = await Expect(s.ReturnedAsync(TaskId(task), 1, ETagOf(refusedTask)), 200);
        Assert.Equal("ReturnedToSource", Confirmation(returned, 1).GetProperty("resolution").GetString());
        Assert.Equal("None", Flag(returned));
        Assert.Equal("Open", Status(returned));
        Assert.Contains("ConfirmationReturned", s.EventKinds(TaskId(task)));
        Assert.Empty(s.TaskMovements(TaskId(task)));
        Assert.Equal("WMS_CONFIRMATION_NOT_RESOLVABLE", await ErrorCode(s.ReturnedAsync(TaskId(task), 1, ETagOf(returned)), 409));

        Assert.Equal("Cancelled", Status(await Expect(s.CancelAsync(TaskId(task), ETagOf(returned)), 200)));
    }

    [Fact]
    public async Task AC55_returned_on_a_posted_confirmation_is_not_resolvable()
    {
        var (s, task) = await OpenAsync();
        var posted = await Expect(s.ConfirmAsync(task, 10, s.A01), 201);
        Assert.Equal("WMS_CONFIRMATION_NOT_RESOLVABLE", await ErrorCode(s.ReturnedAsync(TaskId(task), 1, ETagOf(posted)), 409));
    }

    [Theory]
    [InlineData("recall")]
    [InlineData("held")]
    [InlineData("condition")]
    public async Task AC56_a_retry_whose_pre_check_fails_refuses_without_calling_the_ledger(string change)
    {
        var (s, task) = await OpenAsync(stock: 60, planned: 60);
        if (change == "condition") _factory.StorageConditions[s.Sku] = ["AMBIENT_15_25"];
        s.LedgerAnswersBefore(503);
        await Expect(s.ConfirmAsync(task, 60, s.A01), 503);
        s.LedgerHealthy();

        switch (change)
        {
            case "recall": s.Recall(); break;
            case "held": await s.PostLedgerAsync(s.StatusTransfer(60, "AVAILABLE", "QUALITY_INSPECTION")); break;
            default: await s.W.CreatedAsync(s.A01, [], ["FROZEN_BELOW_MINUS_20"]); break;
        }

        var calls = 0;
        _factory.AroundPosting[s.Tenant] = (_, real) => { Interlocked.Increment(ref calls); return real(); };
        _factory.Clock.Advance(TimeSpan.FromMinutes(3));
        try
        {
            var refused = await Expect(s.RetryAsync(TaskId(task), 1), 422);
            Assert.Equal(change switch { "recall" => "LOT_UNDER_RECALL", "held" => "WMS_STOCK_NOT_PUTAWAY_ELIGIBLE", _ => "WMS_STORAGE_CONDITION_MISMATCH" }, Code(refused));
            var c = Confirmation(DetailsTask(refused), 1);
            Assert.Equal("Refused", c.GetProperty("status").GetString());
            Assert.Equal("PreCheck", c.GetProperty("refusalSource").GetString());
            Assert.True(c.GetProperty("refusedOnRetry").GetBoolean());
            Assert.Equal(0, calls);
            Assert.Empty(s.TaskMovements(TaskId(task)));
        }
        finally
        {
            s.LedgerHealthy();
        }
    }
}

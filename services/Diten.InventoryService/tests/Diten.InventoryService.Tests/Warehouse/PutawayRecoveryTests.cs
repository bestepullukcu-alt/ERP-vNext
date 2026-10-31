using System.Text.Json;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Inventory;
using Diten.InventoryService.Domain.Warehouse.Entities;

using Xunit;
using static Diten.InventoryService.Tests.Warehouse.PutawayScenario;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-2 — when the stock answer is lost (pack §8.3 Pending recovery; AC-5, AC-46, AC-54, AC-63, AC-64, AC-65, AC-67 (6), AC-69): the
/// fake ledger answers before writing, commits and then loses the answer, throws, or waits; the host's clock moves the flight lock. One
/// movement per confirmation whatever happens, never a Refused for an unknown outcome, and the ledger key decides before any pre-check.
/// </summary>
public sealed class PutawayRecoveryTests : IClassFixture<WarehouseApiFactory>
{
    private readonly WarehouseApiFactory _factory;

    public PutawayRecoveryTests(WarehouseApiFactory factory) => _factory = factory;

    private void LockExpires() => _factory.Clock.Advance(TimeSpan.FromMinutes(3));

    private async Task<(PutawayScenario S, JsonElement Task)> OpenAsync(decimal stock = 100, decimal planned = 60)
    {
        var s = await new PutawayScenario(_factory).ReadyAsync();
        await s.ReceiveAsync(stock);
        return (s, await s.TaskAsync(planned));
    }

    /// <summary>A confirmation left Pending by an unknown outcome (503 / OutcomeUnknown), with the ledger healthy again afterwards.</summary>
    private static async Task<JsonElement> PendingAsync(PutawayScenario s, JsonElement task, decimal q, bool committed)
    {
        if (committed) s.LedgerCommitsThenFails();
        else s.LedgerAnswersBefore(500, "INTERNAL");
        var pending = await Expect(s.ConfirmAsync(task, q, s.A01), 503);
        s.LedgerHealthy();
        Assert.Equal(1, pending.GetProperty("error").GetProperty("details").GetProperty("seq").GetInt32());
        return DetailsTask(pending);
    }

    // ── AC-5 ─────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(504)]
    [InlineData(0)] // an exception
    [InlineData(-1)] // a cancellation
    public async Task AC5_an_unknown_outcome_leaves_the_confirmation_pending_with_503_outcome_unknown(int status)
    {
        var (s, task) = await OpenAsync();
        if (status > 0) s.LedgerAnswersBefore(status, "INTERNAL");
        else s.LedgerThrows(status == 0 ? new InvalidOperationException("test: boom") : new OperationCanceledException("test: cancelled"));
        try
        {
            var answer = await Expect(s.ConfirmAsync(task, 20, s.A01), 503);
            var details = answer.GetProperty("error").GetProperty("details");
            Assert.Equal("OutcomeUnknown", details.GetProperty("reason").GetString());
            Assert.Equal(1, details.GetProperty("seq").GetInt32());
            Assert.Equal("Pending", ConfirmationStatus(DetailsTask(answer), 1));
            Assert.Equal(["Created", "ConfirmationRecorded"], s.EventKinds(TaskId(task)));
            Assert.Equal((0, 1), s.Central("ConfirmPutawayTaskCommand"));
        }
        finally
        {
            s.LedgerHealthy();
        }
    }

    [Fact]
    public async Task AC5_a_503_keeps_the_confirmation_pending()
    {
        var (s, task) = await OpenAsync();
        s.LedgerAnswersBefore(503);
        var answer = await Expect(s.ConfirmAsync(task, 20, s.A01), 503);
        s.LedgerHealthy();
        Assert.Equal("Pending", ConfirmationStatus(DetailsTask(answer), 1));
    }

    [Fact]
    public async Task AC5_500_before_commit_the_retry_finds_nothing_and_posts_with_the_same_key_and_dates()
    {
        var (s, task) = await OpenAsync();
        var pending = await PendingAsync(s, task, 20, committed: false);
        var dates = Confirmation(pending, 1).GetProperty("transactionDate").GetString();
        LockExpires();

        var posted = await Expect(s.RetryAsync(TaskId(task), 1), 200);
        Assert.Equal("Posted", ConfirmationStatus(posted, 1));
        Assert.Equal(dates, Confirmation(posted, 1).GetProperty("transactionDate").GetString());
        var movement = Assert.Single(s.TaskMovements(TaskId(task)));
        Assert.Equal(PostingKey(TaskId(task), 1), movement["IdempotencyKey"].AsString);
        Assert.Equal(2, Confirmation(posted, 1).GetProperty("attempts").GetInt32());
        Assert.Equal(["Created", "ConfirmationRecorded", "ConfirmationRetried", "ConfirmationPosted"], s.EventKinds(TaskId(task)));
        Assert.Equal((1, 0), s.Central("RetryPutawayConfirmationCommand"));
    }

    [Fact]
    public async Task AC5_500_after_commit_the_retry_finds_the_key_posts_without_pre_checks_or_a_second_movement()
    {
        var (s, task) = await OpenAsync(stock: 60);
        await PendingAsync(s, task, 20, committed: true);
        Assert.Single(s.TaskMovements(TaskId(task)));

        // the conditions changed after the commit: recall + the rest of the bucket held — the key decides first (review 02 F-1)
        s.Recall();
        await s.PostLedgerAsync(s.StatusTransfer(40, "AVAILABLE", "QUALITY_INSPECTION"));
        var productCalls = _factory.ProductCalls.GetValueOrDefault(s.Sku);
        LockExpires();

        var posted = await Expect(s.RetryAsync(TaskId(task), 1), 200);
        Assert.Equal("Posted", ConfirmationStatus(posted, 1));
        Assert.Single(s.TaskMovements(TaskId(task)));
        Assert.Equal(productCalls, _factory.ProductCalls.GetValueOrDefault(s.Sku));
    }

    [Fact]
    public async Task AC5_the_sweeper_posts_a_committed_key_and_leaves_a_not_committed_one_without_upstream_calls()
    {
        var (s, task) = await OpenAsync();
        await PendingAsync(s, task, 20, committed: true);
        var other = await s.TaskAsync(20);
        await PendingAsync(s, other, 20, committed: false);
        var productCalls = _factory.ProductCalls.GetValueOrDefault(s.Sku);
        LockExpires();

        var tick = await s.ReconcileAsync();
        Assert.Equal(1, tick.CommandsSent);
        Assert.Equal("Posted", ConfirmationStatus(await s.GetTaskAsync(TaskId(task)), 1));
        var untouched = await s.GetTaskAsync(TaskId(other));
        Assert.Equal("Pending", ConfirmationStatus(untouched, 1));
        Assert.Equal(1, Confirmation(untouched, 1).GetProperty("attempts").GetInt32());
        Assert.Equal(productCalls, _factory.ProductCalls.GetValueOrDefault(s.Sku));
        Assert.Contains("ConfirmationReconciled", s.EventKinds(TaskId(task)));
        Assert.Equal(["Created", "ConfirmationRecorded"], s.EventKinds(TaskId(other)));
        Assert.Equal((1, 0), s.Central("ReconcilePendingConfirmationCommand"));
    }

    [Theory]
    [InlineData(400)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(422)]
    public async Task AC5_a_definite_4xx_is_a_stored_refusal_not_retried(int status)
    {
        var (s, task) = await OpenAsync();
        s.LedgerAnswersBefore(status, "SOME_REFUSAL");
        try
        {
            var refused = await Expect(s.ConfirmAsync(task, 20, s.A01), 422);
            Assert.Equal("SOME_REFUSAL", Code(refused));
            var c = Confirmation(DetailsTask(refused), 1);
            Assert.Equal("Refused", c.GetProperty("status").GetString());
            Assert.Equal(status, c.GetProperty("error").GetProperty("status").GetInt32());
            Assert.Equal("WMS_CONFIRMATION_NOT_RETRYABLE", await ErrorCode(s.RetryAsync(TaskId(task), 1), 409));
            Assert.Equal(0m, D(DetailsTask(refused), "confirmedQuantity"));
        }
        finally
        {
            s.LedgerHealthy();
        }
    }

    [Fact]
    public async Task AC5_key_reused_is_an_integrity_error_not_a_refusal()
    {
        var (s, task) = await OpenAsync();
        await s.ForeignUnderKeyAsync(PostingKey(TaskId(task), 1));
        var error = await Expect(s.ConfirmAsync(task, 20, s.A01), 409);
        Assert.Equal("WMS_POSTING_INTEGRITY_ERROR", Code(error));
        Assert.Equal("IntegrityError", ConfirmationStatus(DetailsTask(error), 1));
        Assert.Equal("KeyReused", error.GetProperty("error").GetProperty("details").GetProperty("reason").GetString());
    }

    [Fact]
    public async Task AC5_no_retry_limit_six_failures_then_the_same_key_posts_once()
    {
        var (s, task) = await OpenAsync();
        await PendingAsync(s, task, 20, committed: false);
        s.LedgerAnswersBefore(503);
        for (var i = 0; i < 5; i++)
        {
            LockExpires();
            await Expect(s.RetryAsync(TaskId(task), 1), 503);
        }

        s.LedgerHealthy();
        LockExpires();
        var posted = await Expect(s.RetryAsync(TaskId(task), 1), 200);
        Assert.Equal(7, Confirmation(posted, 1).GetProperty("attempts").GetInt32());
        Assert.False(string.IsNullOrEmpty(Confirmation(posted, 1).GetProperty("lastAttemptAtUtc").GetString()));
        Assert.Single(s.TaskMovements(TaskId(task)));
    }

    // ── AC-46 long Pending ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC46_long_pending_flag_after_the_configured_minutes_then_a_healthy_retry_clears_it()
    {
        var (s, task) = await OpenAsync();
        await PendingAsync(s, task, 20, committed: false);
        Assert.Equal("PostingPending", Flag(await s.GetTaskAsync(TaskId(task))));

        _factory.Clock.Advance(TimeSpan.FromMinutes(29));
        var stillShort = await s.GetTaskAsync(TaskId(task));
        Assert.Equal("PostingPending", Flag(stillShort));
        _factory.Clock.Advance(TimeSpan.FromMinutes(2));
        var longPending = await s.GetTaskAsync(TaskId(task));
        Assert.Equal("PostingPendingLong", Flag(longPending));
        Assert.Equal("WMS_CONFIRMATION_PENDING", await ErrorCode(s.ConfirmAsync(longPending, 5, s.A01), 409));
        Assert.Equal("WMS_CONFIRMATION_PENDING", await ErrorCode(s.CancelAsync(TaskId(task), ETagOf(longPending)), 409));
        Assert.Equal("WMS_CONFIRMATION_PENDING", await ErrorCode(s.CloseShortAsync(TaskId(task), ETagOf(longPending)), 409));

        var posted = await Expect(s.RetryAsync(TaskId(task), 1), 200);
        Assert.Equal("None", Flag(posted));
    }

    // ── AC-54 two retries ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC54_two_retries_both_find_the_committed_key_one_writes_the_other_sees_it_posted()
    {
        var (s, task) = await OpenAsync(planned: 60);
        await PendingAsync(s, task, 20, committed: true);
        LockExpires();

        var firstLookup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lookups = 0;
        _factory.BeforeLookup[s.Tenant] = async () =>
        {
            if (Interlocked.Increment(ref lookups) != 1) return;
            firstLookup.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(30));
        };
        try
        {
            var a = s.RetryAsync(TaskId(task), 1, token: s.TokenOf(s.User, TaskRead, TaskConfirm, TaskUpdate));
            await firstLookup.Task.WaitAsync(TimeSpan.FromSeconds(20));
            LockExpires(); // A's lock runs out while A waits; B takes it
            var b = await Expect(s.RetryAsync(TaskId(task), 1), 200);
            Assert.Equal("Posted", ConfirmationStatus(b, 1));
            release.TrySetResult();
            var aAnswer = await Expect(a, 200);
            Assert.Equal("Posted", ConfirmationStatus(aAnswer, 1));
        }
        finally
        {
            _factory.BeforeLookup.TryRemove(s.Tenant, out _);
        }

        var final = await s.GetTaskAsync(TaskId(task));
        Assert.Equal(20m, D(final, "confirmedQuantity"));
        Assert.Single(s.TaskMovements(TaskId(task)));
        var kinds = s.EventKinds(TaskId(task));
        Assert.Equal(2, kinds.Count(k => k == "ConfirmationRetried"));
        Assert.Single(kinds, k => k == "ConfirmationPosted");
        Assert.Equal((2, 0), s.Central("RetryPutawayConfirmationCommand"));
    }

    // ── AC-63 / AC-65 integrity and its resolve ──────────────────────────────────────────────────────────

    [Fact]
    public async Task AC63_an_integrity_error_locks_the_task_until_resolved()
    {
        var (s, task) = await OpenAsync();
        await s.ForeignUnderKeyAsync(PostingKey(TaskId(task), 1));
        var locked = DetailsTask(await Expect(s.ConfirmAsync(task, 20, s.A01), 409));
        Assert.Equal("PostingIntegrityError", Flag(locked));
        Assert.Equal(["ResolveIntegrity"], Strings(locked.GetProperty("allowedActions")));

        var etag = ETagOf(locked);
        Assert.Equal("WMS_POSTING_INTEGRITY_ERROR", await ErrorCode(s.ConfirmAsync(locked, 5, s.A01), 409));
        Assert.Equal("WMS_CONFIRMATION_NOT_RESOLVABLE", await ErrorCode(s.ReturnedAsync(TaskId(task), 1, etag), 409));
        Assert.Equal("WMS_POSTING_INTEGRITY_ERROR", await ErrorCode(s.CancelAsync(TaskId(task), etag), 409));
        Assert.Equal("WMS_POSTING_INTEGRITY_ERROR", await ErrorCode(s.CloseShortAsync(TaskId(task), etag), 409));
        var listed = await Expect(s.W.GetAsync("/api/warehouse/putaway-tasks?pageSize=50", s.Supervisor), 200);
        Assert.Contains(listed.GetProperty("items").EnumerateArray(), t => TaskId(t) == TaskId(task) && Flag(t) == "PostingIntegrityError");
    }

    [Fact]
    public async Task AC65_resolve_a_foreign_holder_back_to_an_unresolved_refusal_and_the_task_stays_open()
    {
        var (s, task) = await OpenAsync();
        var foreign = await s.ForeignUnderKeyAsync(PostingKey(TaskId(task), 1));
        var locked = DetailsTask(await Expect(s.ConfirmAsync(task, 20, s.A01), 409));

        Assert.Equal(403, (int)(await s.ResolveAsync(TaskId(task), 1, ETagOf(locked), token: s.Operator)).StatusCode);
        Assert.Equal("REASON_REQUIRED", await ErrorCode(s.ResolveAsync(TaskId(task), 1, ETagOf(locked), "kisa"), 400));

        var resolved = await Expect(s.ResolveAsync(TaskId(task), 1, ETagOf(locked)), 200);
        var c = Confirmation(resolved, 1);
        Assert.Equal("Refused", c.GetProperty("status").GetString());
        Assert.Equal("Resolve", c.GetProperty("refusalSource").GetString());
        Assert.Equal("Open", Status(resolved));
        var e = Assert.Single(s.TaskEvents(TaskId(task)), x => x["Kind"].AsString == "IntegrityResolved");
        Assert.Equal(foreign.GetProperty("transactionId").GetString(), e["Data"]["holderTransactionId"].AsString);
        Assert.Equal((1, 0), s.Central("ResolvePutawayIntegrityCommand"));

        // the way on: a new confirmation
        Assert.Equal("Posted", ConfirmationStatus(await Expect(s.ConfirmAsync(resolved, 20, s.A01), 201), 2));
        Assert.Equal("WMS_CONFIRMATION_NOT_RESOLVABLE", await ErrorCode(s.ResolveAsync(TaskId(task), 2, ETagOf(await s.GetTaskAsync(TaskId(task)))), 409));
    }

    [Fact]
    public async Task AC65_resolve_its_own_movement_takes_the_ledger_values_and_records_the_deviation()
    {
        var (s, task) = await OpenAsync();
        s.LedgerAnswersBefore(503);
        await Expect(s.ConfirmAsync(task, 40, s.A01), 503);
        s.LedgerHealthy();
        // the ledger holds a 0178 movement of this task / seq under the key, but with another date and quantity
        await s.LateCommitAsync(TaskId(task), 1, r => r with { Quantity = "30", TransactionDate = "2026-09-01T08:00:00Z", PostingDate = "2026-09-01T08:00:00Z" });
        LockExpires();
        var locked = DetailsTask(await Expect(s.RetryAsync(TaskId(task), 1), 409));
        Assert.Equal("IntegrityError", ConfirmationStatus(locked, 1));

        var resolved = await Expect(s.ResolveAsync(TaskId(task), 1, ETagOf(locked)), 200);
        var c = Confirmation(resolved, 1);
        Assert.Equal("Posted", c.GetProperty("status").GetString());
        Assert.Equal(30m, D(c, "quantity"));
        Assert.Equal(30m, D(resolved, "confirmedQuantity"));
        Assert.Equal("InProgress", Status(resolved));
        Assert.Contains("ConfirmationDeviation", s.EventKinds(TaskId(task)));
        Assert.Single(s.TaskMovements(TaskId(task)));
    }

    [Fact]
    public async Task AC65_resolve_while_a_confirmation_is_pending_is_409()
    {
        var (s, task) = await OpenAsync();
        await s.ForeignUnderKeyAsync(PostingKey(TaskId(task), 1));
        var locked = DetailsTask(await Expect(s.ConfirmAsync(task, 20, s.A01), 409));
        await s.MutateStoredTaskAsync(TaskId(task), t => t.Confirmations.Add(new TaskConfirmation
        {
            Seq = 2, Quantity = 1, ToLocationId = s.A01, Status = Diten.InventoryService.Domain.Warehouse.Enums.ConfirmationStatus.Pending, PostingKey = PostingKey(TaskId(task), 2),
            ConfirmedBy = s.User, ConfirmedAtUtc = DateTimeOffset.UtcNow
        }));
        var current = await s.GetTaskAsync(TaskId(task));
        Assert.Equal("WMS_CONFIRMATION_PENDING", await ErrorCode(s.ResolveAsync(TaskId(task), 1, ETagOf(current)), 409));
    }

    // ── AC-69 own movement reversed ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC69_a_reversed_own_movement_is_never_posted_retry_and_sweeper_mark_holder_reversed()
    {
        var (s, task) = await OpenAsync();
        await PendingAsync(s, task, 20, committed: true);
        var movement = Assert.Single(s.TaskMovements(TaskId(task)));
        await s.PostLedgerAsync(s.Inventory.Reversal(new() { ["quantity"] = "20", ["uomId"] = "EA" }, movement["_id"].ToString()));
        LockExpires();

        var error = await Expect(s.RetryAsync(TaskId(task), 1), 409);
        Assert.Equal("HolderReversed", error.GetProperty("error").GetProperty("details").GetProperty("reason").GetString());
        var locked = DetailsTask(error);
        Assert.False(string.IsNullOrEmpty(Confirmation(locked, 1).GetProperty("holderMovement").GetProperty("reversedByTransactionId").GetString()));

        var resolved = await Expect(s.ResolveAsync(TaskId(task), 1, ETagOf(locked)), 200);
        Assert.Equal("Refused", ConfirmationStatus(resolved, 1));
        Assert.Equal(0m, D(resolved, "confirmedQuantity"));
        Assert.Equal("Open", Status(resolved));

        // the sweeper path on a second task
        var other = await s.TaskAsync(20);
        await PendingAsync(s, other, 20, committed: true);
        var second = Assert.Single(s.TaskMovements(TaskId(other)));
        await s.PostLedgerAsync(s.Inventory.Reversal(new() { ["quantity"] = "20", ["uomId"] = "EA" }, second["_id"].ToString()));
        LockExpires();
        await s.ReconcileAsync();
        var swept = await s.GetTaskAsync(TaskId(other));
        Assert.Equal("IntegrityError", ConfirmationStatus(swept, 1));
        Assert.Equal("HolderReversed", Confirmation(swept, 1).GetProperty("error").GetProperty("reason").GetString());
        Assert.Equal(0, s.Central("ReconcilePendingConfirmationCommand").Forwarded);
    }

    // ── AC-64 the flight lock ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC64_a_retry_while_the_first_attempt_is_in_flight_is_409_and_writes_nothing()
    {
        var (s, task) = await OpenAsync();
        var gate = s.LedgerWaitsFor();
        var confirm = s.ConfirmAsync(task, 20, s.A01);
        await WaitForAsync(() => s.StoredTask(TaskId(task))["Confirmations"].AsBsonArray.Count == 1);
        var before = s.StoredTask(TaskId(task))["Version"].AsInt32;

        Assert.Equal("WMS_CONFIRMATION_IN_FLIGHT", await ErrorCode(s.RetryAsync(TaskId(task), 1), 409));
        Assert.Equal(0, (await s.ReconcileAsync()).CommandsSent);
        Assert.Equal(before, s.StoredTask(TaskId(task))["Version"].AsInt32);

        gate.TrySetResult();
        s.LedgerHealthy();
        Assert.Equal("Posted", ConfirmationStatus(await Expect(confirm, 201), 1));
        Assert.Single(s.TaskMovements(TaskId(task)));
    }

    [Fact]
    public async Task AC64_C1_the_expired_attempt_answers_in_flight_when_a_retry_owns_the_lock_and_one_movement_follows()
    {
        var (s, task) = await OpenAsync();
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        _factory.AroundPosting[s.Tenant] = async (_, real) =>
        {
            await (Interlocked.Increment(ref calls) == 1 ? first.Task : second.Task).WaitAsync(TimeSpan.FromSeconds(30));
            return await real();
        };
        try
        {
            var x = s.ConfirmAsync(task, 20, s.A01);
            await WaitForAsync(() => calls == 1);
            LockExpires();
            var y = s.RetryAsync(TaskId(task), 1);
            await WaitForAsync(() => calls == 2);

            first.TrySetResult();
            var xAnswer = await Expect(x, 409);
            Assert.Equal("WMS_CONFIRMATION_IN_FLIGHT", Code(xAnswer));
            Assert.Equal(1, xAnswer.GetProperty("error").GetProperty("details").GetProperty("seq").GetInt32());
            Assert.Equal("Pending", ConfirmationStatus(await s.GetTaskAsync(TaskId(task)), 1));

            second.TrySetResult();
            Assert.Equal("Posted", ConfirmationStatus(await Expect(y, 200), 1));
        }
        finally
        {
            s.LedgerHealthy();
        }

        Assert.Single(s.TaskMovements(TaskId(task)));
        Assert.Equal((0, 1), s.Central("ConfirmPutawayTaskCommand"));
        Assert.DoesNotContain("ConfirmationIntegrityError", s.EventKinds(TaskId(task)));
    }

    [Fact]
    public async Task AC64_D_the_expired_attempt_posted_while_a_retry_refused_marks_the_confirmation_diverged()
    {
        var (s, task) = await OpenAsync();
        var gate = s.LedgerWaitsFor();
        var x = s.ConfirmAsync(task, 20, s.A01);
        await WaitForAsync(() => s.StoredTask(TaskId(task))["Confirmations"].AsBsonArray.Count == 1);
        LockExpires();
        s.Recall();
        var y = await Expect(s.RetryAsync(TaskId(task), 1), 422); // the pre-check refuses (recall)
        Assert.Equal("Refused", ConfirmationStatus(DetailsTask(y), 1));

        gate.TrySetResult(); // X's posting commits (LOCATION_TRANSFER is exempt from the recall block)
        var xAnswer = await Expect(x, 409);
        s.LedgerHealthy();
        Assert.Equal("WMS_POSTING_INTEGRITY_ERROR", Code(xAnswer));
        var c = Confirmation(DetailsTask(xAnswer), 1);
        Assert.Equal("IntegrityError", c.GetProperty("status").GetString());
        Assert.Equal("StatusDiverged", c.GetProperty("error").GetProperty("reason").GetString());
        Assert.Single(s.TaskMovements(TaskId(task)));
    }

    // ── AC-67 (6) the overshoot on the write path ───────────────────────────────────────────────────────

    [Fact]
    public async Task AC67_6_a_confirmed_quantity_raised_before_transaction_2_makes_the_confirmation_over_planned()
    {
        var (s, task) = await OpenAsync(planned: 60);
        var writes = 0;
        _factory.BeforeTaskWrite[s.Tenant] = async () =>
        {
            if (Interlocked.Increment(ref writes) == 2) await s.MutateStoredTaskAsync(TaskId(task), t => t.ConfirmedQuantity = 50);
        };
        try
        {
            var error = await Expect(s.ConfirmAsync(task, 20, s.A01), 409);
            Assert.Equal("WMS_POSTING_INTEGRITY_ERROR", Code(error));
            Assert.Equal("OverPlanned", error.GetProperty("error").GetProperty("details").GetProperty("reason").GetString());
        }
        finally
        {
            _factory.BeforeTaskWrite.TryRemove(s.Tenant, out _);
        }
    }

    internal static async Task WaitForAsync(Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(20);
        while (!condition())
        {
            if (DateTime.UtcNow > until) throw new TimeoutException("condition not reached");
            await Task.Delay(25);
        }
    }
}

using System.Text.Json;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Inventory;
using Diten.InventoryService.Persistence.Warehouse;
using MongoDB.Bson;
using Xunit;
using static Diten.InventoryService.Tests.Warehouse.PutawayRecoveryTests;
using static Diten.InventoryService.Tests.Warehouse.PutawayScenario;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-2 — the reconciler and the late-commit defence (pack §8.3 "Sweeper", flow rows 18 / 20; AC-66, AC-68, AC-70): per-row scope,
/// the ledger key only, KeyReused / HolderReversed / the defensive branch (refused on a retry, PreCheck and Ledger4xx) → IntegrityError,
/// first-confirmation and Resolve refusals never looked up, the two cursors with their own budgets, one row's failure isolated; the closing
/// commands' pre-lookup and the race of "returned" with the defensive branch.
/// </summary>
public sealed class PutawaySweeperTests : IClassFixture<WarehouseApiFactory>
{
    private readonly WarehouseApiFactory _factory;

    public PutawaySweeperTests(WarehouseApiFactory factory) => _factory = factory;

    private void LockExpires() => _factory.Clock.Advance(TimeSpan.FromMinutes(3));

    private async Task<(PutawayScenario S, JsonElement Task)> OpenAsync(decimal stock = 100, decimal planned = 60)
    {
        var s = await new PutawayScenario(_factory).ReadyAsync();
        await s.ReceiveAsync(stock);
        return (s, await s.TaskAsync(planned));
    }

    /// <summary>A Pending confirmation (nothing committed) refused on its retry — by a pre-check (recall) or by the ledger (a 4xx).</summary>
    private async Task<JsonElement> RefusedOnRetryAsync(PutawayScenario s, JsonElement task, decimal q, bool preCheck)
    {
        s.LedgerAnswersBefore(503);
        await Expect(s.ConfirmAsync(task, q, s.A01), 503);
        if (preCheck)
        {
            s.LedgerHealthy();
            s.Recall();
        }
        else
        {
            s.LedgerAnswersBefore(422, "INSUFFICIENT_STOCK");
        }

        LockExpires();
        var refused = await Expect(s.RetryAsync(TaskId(task), 1), 422);
        s.LedgerHealthy();
        var c = Confirmation(DetailsTask(refused), 1);
        Assert.True(c.GetProperty("refusedOnRetry").GetBoolean());
        Assert.Equal(preCheck ? "PreCheck" : "Ledger4xx", c.GetProperty("refusalSource").GetString());
        return DetailsTask(refused);
    }

    private static void RemoveRecall(PutawayScenario s)
        => s.Factory.Collection("recall_lot_blocks").DeleteMany(new BsonDocument("TenantId", s.Tenant.ToString("D")));

    // ── AC-66 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC66_key_reused_under_a_pending_confirmation_becomes_an_integrity_error_without_a_central_row()
    {
        var (s, task) = await OpenAsync();
        s.LedgerAnswersBefore(503);
        await Expect(s.ConfirmAsync(task, 20, s.A01), 503);
        s.LedgerHealthy();
        await s.ForeignUnderKeyAsync(PostingKey(TaskId(task), 1));
        LockExpires();

        await s.ReconcileAsync();
        var swept = await s.GetTaskAsync(TaskId(task));
        Assert.Equal("IntegrityError", ConfirmationStatus(swept, 1));
        Assert.Equal("KeyReused", Confirmation(swept, 1).GetProperty("error").GetProperty("reason").GetString());
        Assert.Equal(["Created", "ConfirmationRecorded", "ConfirmationIntegrityError"], s.EventKinds(TaskId(task)));
        Assert.Equal((0, 1), s.Central("ReconcilePendingConfirmationCommand"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AC66_defensive_branch_a_retry_refusal_whose_key_committed_late_becomes_diverged(bool preCheck)
    {
        var (s, task) = await OpenAsync();
        await RefusedOnRetryAsync(s, task, 20, preCheck);
        RemoveRecall(s);
        await s.LateCommitAsync(TaskId(task), 1);

        await s.ReconcileAsync();
        var swept = await s.GetTaskAsync(TaskId(task));
        Assert.Equal("IntegrityError", ConfirmationStatus(swept, 1));
        Assert.Equal("StatusDiverged", Confirmation(swept, 1).GetProperty("error").GetProperty("reason").GetString());
        Assert.Single(s.TaskEvents(TaskId(task)), e => e["Kind"].AsString == "ConfirmationIntegrityError" && e["System"].AsBoolean);
        Assert.Equal(0, s.Central("ReconcilePendingConfirmationCommand").Forwarded);
    }

    [Fact]
    public async Task AC66_first_confirmation_refusals_and_resolve_refusals_are_never_looked_up()
    {
        var (s, task) = await OpenAsync();
        s.LedgerAnswersBefore(422, "INSUFFICIENT_STOCK");
        await Expect(s.ConfirmAsync(task, 20, s.A01), 422);
        s.LedgerHealthy();
        var other = await s.TaskAsync(20);
        await s.ForeignUnderKeyAsync(PostingKey(TaskId(other), 1));
        var locked = DetailsTask(await Expect(s.ConfirmAsync(other, 20, s.A01), 409));
        Assert.Equal("Refused", ConfirmationStatus(await Expect(s.ResolveAsync(TaskId(other), 1, ETagOf(locked)), 200), 1));
        LockExpires();

        var before = s.Lookups;
        var tick = await s.ReconcileAsync();
        Assert.Equal(0, tick.NormalScanned + tick.DefensiveScanned);
        Assert.Equal(before, s.Lookups);
    }

    [Fact]
    public async Task AC66_a_not_committed_pending_is_left_untouched_and_a_locked_one_is_skipped()
    {
        var (s, task) = await OpenAsync();
        var gate = s.LedgerWaitsFor();
        var inFlight = s.ConfirmAsync(task, 20, s.A01);
        await WaitForAsync(() => s.StoredTask(TaskId(task))["Confirmations"].AsBsonArray.Count == 1);
        var before = s.Lookups;
        var tick = await s.ReconcileAsync();
        Assert.Equal(0, tick.CommandsSent);
        Assert.Equal(before, s.Lookups); // younger than the reconcile age and locked: not even read
        gate.TrySetResult();
        s.LedgerHealthy();
        await Expect(inFlight, 201);
    }

    [Fact]
    public async Task AC66_the_201st_committed_row_is_reconciled_on_the_next_tick_and_the_defensive_branch_has_its_own_budget()
    {
        var (s, task) = await OpenAsync(stock: 300, planned: 20);
        await PendingCommittedAsync(s, task);
        // a defensive candidate of the same tenant
        var defensive = await s.TaskAsync(20);
        await RefusedOnRetryAsync(s, defensive, 20, preCheck: false);
        await s.LateCommitAsync(TaskId(defensive), 1);
        // 200 earlier Pending rows (ids sort first) whose keys are NOT committed — clones of the stored task
        var stored = s.StoredTask(TaskId(task));
        var clones = Enumerable.Range(1, 200).Select(i =>
        {
            var clone = stored.DeepClone().AsBsonDocument;
            var id = Guid.Parse($"00000000-0000-0000-0000-{i:D12}");
            clone["_id"] = stored["_id"].IsString ? id.ToString("D") : new BsonBinaryData(id, GuidRepresentation.Standard);
            clone["Number"] = $"GMG-PUT-9{i:D5}";
            clone["IdempotencyKey"] = "clone-" + i;
            clone["Confirmations"][0]["PostingKey"] = $"wms:put:{id:D}:1";
            return clone;
        }).ToList();
        _factory.Collection(WarehouseCollections.Tasks).InsertMany(clones);
        LockExpires();

        var first = await s.ReconcileAsync();
        Assert.Equal(200, first.NormalScanned);
        Assert.Equal(1, first.DefensiveScanned);
        Assert.Equal("Pending", ConfirmationStatus(await s.GetTaskAsync(TaskId(task)), 1));
        Assert.Equal("IntegrityError", ConfirmationStatus(await s.GetTaskAsync(TaskId(defensive)), 1));

        var second = await s.ReconcileAsync();
        Assert.True(second.NormalScanned >= 1);
        Assert.Equal("Posted", ConfirmationStatus(await s.GetTaskAsync(TaskId(task)), 1));
    }

    [Fact]
    public async Task AC66_one_rows_failure_does_not_stop_the_others()
    {
        var (s, task) = await OpenAsync();
        await PendingCommittedAsync(s, task);
        var other = await s.TaskAsync(20);
        await PendingCommittedAsync(s, other);
        LockExpires();
        var lookups = 0;
        _factory.BeforeLookup[s.Tenant] = () => Interlocked.Increment(ref lookups) == 1 ? throw new InvalidOperationException("test: one row fails") : Task.CompletedTask;
        try
        {
            var tick = await s.ReconcileAsync();
            Assert.Equal(1, tick.Failures);
            Assert.Equal(1, tick.CommandsSent);
        }
        finally
        {
            _factory.BeforeLookup.TryRemove(s.Tenant, out _);
        }

        var posted = new[] { await s.GetTaskAsync(TaskId(task)), await s.GetTaskAsync(TaskId(other)) }.Count(t => ConfirmationStatus(t, 1) == "Posted");
        Assert.Equal(1, posted);
        await s.ReconcileAsync();
        Assert.Equal("Posted", ConfirmationStatus(await s.GetTaskAsync(TaskId(task)), 1));
        Assert.Equal("Posted", ConfirmationStatus(await s.GetTaskAsync(TaskId(other)), 1));
    }

    private static async Task PendingCommittedAsync(PutawayScenario s, JsonElement task)
    {
        s.LedgerCommitsThenFails();
        await Expect(s.ConfirmAsync(task, 20, s.A01), 503);
        s.LedgerHealthy();
    }

    // ── AC-68 the closing commands' pre-lookup and the terminal task ────────────────────────────────────

    /// <summary>A first confirmation refused by the ledger whose movement then commits late (the answer was a lie of a slow ledger).</summary>
    private async Task<JsonElement> LateRefusedAsync(PutawayScenario s, JsonElement task, decimal q = 20)
    {
        s.LedgerAnswersBefore(422, "INSUFFICIENT_STOCK");
        var refused = DetailsTask(await Expect(s.ConfirmAsync(task, q, s.A01), 422));
        s.LedgerHealthy();
        await s.LateCommitAsync(TaskId(task), 1);
        return refused;
    }

    [Theory]
    [InlineData("confirm")]
    [InlineData("returned")]
    [InlineData("cancel")]
    [InlineData("close-short")]
    public async Task AC68_i_a_late_commit_under_a_refusal_stops_the_closing_command_and_marks_it_diverged(string command)
    {
        var (s, task) = await OpenAsync();
        JsonElement current;
        if (command == "close-short")
        {
            var posted = await Expect(s.ConfirmAsync(task, 10, s.A01), 201);
            s.LedgerAnswersBefore(422, "INSUFFICIENT_STOCK");
            current = DetailsTask(await Expect(s.ConfirmAsync(posted, 20, s.A01), 422));
            s.LedgerHealthy();
            await s.LateCommitAsync(TaskId(task), 2);
        }
        else
        {
            current = await LateRefusedAsync(s, task);
        }

        var seq = command == "close-short" ? 2 : 1;
        var events = s.TaskEvents(TaskId(task)).Count;
        var answer = command switch
        {
            "confirm" => s.ConfirmAsync(current, 5, s.B01),
            "returned" => s.ReturnedAsync(TaskId(task), 1, ETagOf(current)),
            "cancel" => s.CancelAsync(TaskId(task), ETagOf(current)),
            _ => s.CloseShortAsync(TaskId(task), ETagOf(current))
        };
        var error = await Expect(answer, 409);
        Assert.Equal("WMS_POSTING_INTEGRITY_ERROR", Code(error));
        var after = await s.GetTaskAsync(TaskId(task));
        Assert.Equal("IntegrityError", ConfirmationStatus(after, seq));
        Assert.Equal("StatusDiverged", Confirmation(after, seq).GetProperty("error").GetProperty("reason").GetString());
        Assert.Equal(Status(current), Status(after));
        Assert.Equal(events + 1, s.TaskEvents(TaskId(task)).Count); // only the IntegrityError row
    }

    [Theory]
    [InlineData("returned")]
    [InlineData("cancel")]
    public async Task AC68_ii_iii_a_commit_after_returned_or_cancel_is_caught_by_the_defensive_branch_then_resolved(string closing)
    {
        var (s, task) = await OpenAsync();
        var refused = await RefusedOnRetryAsync(s, task, 20, preCheck: false);
        var closed = closing == "returned"
            ? await Expect(s.ReturnedAsync(TaskId(task), 1, ETagOf(refused)), 200)
            : await Expect(s.CancelAsync(TaskId(task), ETagOf(refused)), 200);
        await s.LateCommitAsync(TaskId(task), 1);

        await s.ReconcileAsync();
        var caught = await s.GetTaskAsync(TaskId(task));
        var c = Confirmation(caught, 1);
        Assert.Equal("IntegrityError", c.GetProperty("status").GetString());
        if (closing == "returned") Assert.Equal("ReturnedToSource", c.GetProperty("priorResolution").GetString());
        Assert.Equal(Status(closed), Status(caught));

        var resolved = await Expect(s.ResolveAsync(TaskId(task), 1, ETagOf(caught)), 200);
        Assert.Equal("Posted", ConfirmationStatus(resolved, 1));
        Assert.Equal(closing == "cancel" ? "Cancelled" : "InProgress", Status(resolved)); // a Cancelled task stays Cancelled; an open one moves on
        if (closing == "cancel")
        {
            var deviation = Assert.Single(s.TaskEvents(TaskId(task)), e => e["Kind"].AsString == "ConfirmationDeviation");
            Assert.Equal("true", deviation["Data"]["postedAfterCancel"].AsString);
        }
    }

    // ── AC-70 returned racing the defensive branch ─────────────────────────────────────────────────────

    [Fact]
    public async Task AC70_returned_racing_the_defensive_write_never_leaves_resolution_and_integrity_error_together()
    {
        var (s, task) = await OpenAsync();
        var refused = await RefusedOnRetryAsync(s, task, 20, preCheck: false);
        await s.LateCommitAsync(TaskId(task), 1);

        var busy = 0;
        var returnedStatus = 0;
        _factory.BeforeTaskWrite[s.Tenant] = async () =>
        {
            if (Interlocked.Exchange(ref busy, 1) == 1) return;
            using var r = await s.ReturnedAsync(TaskId(task), 1, ETagOf(refused));
            returnedStatus = (int)r.StatusCode;
        };
        try
        {
            await s.ReconcileAsync();
        }
        finally
        {
            _factory.BeforeTaskWrite.TryRemove(s.Tenant, out _);
        }

        // either "returned" wrote first (200; its resolution survives as PriorResolution) or its own pre-lookup found the commit (409)
        Assert.Contains(returnedStatus, new[] { 200, 409 });
        await s.ReconcileAsync();
        var final = await s.GetTaskAsync(TaskId(task));
        var c = Confirmation(final, 1);
        Assert.Equal("IntegrityError", c.GetProperty("status").GetString());
        if (returnedStatus == 200) Assert.Equal("ReturnedToSource", c.GetProperty("priorResolution").GetString());
        Assert.Single(s.TaskEvents(TaskId(task)), e => e["Kind"].AsString == "ConfirmationIntegrityError");
        Assert.False(c.TryGetProperty("resolution", out var resolution) && resolution.ValueKind == JsonValueKind.String);
        Assert.Equal("WMS_CONFLICT", await ErrorCode(s.CancelAsync(TaskId(task), ETagOf(refused)), 409));
    }
    // ── AC-67 (3) the late-commit path on a task that completed meanwhile ──────────────────────────────

    [Fact]
    public async Task AC67_3_a_late_commit_of_a_superseded_confirmation_is_caught_and_resolved_over_planned_the_task_stays_completed()
    {
        var (s, task) = await OpenAsync(stock: 120, planned: 60);
        s.LedgerAnswersBefore(503);
        await Expect(s.ConfirmAsync(task, 60, s.A01), 503);   // N: the slow ledger has not committed yet
        s.LedgerHealthy();
        LockExpires();
        await s.PostLedgerAsync(s.StatusTransfer(120, "AVAILABLE", "QUALITY_INSPECTION"));
        var refused = DetailsTask(await Expect(s.RetryAsync(TaskId(task), 1), 422)); // NotCommitted → the pre-check refuses N
        await s.PostLedgerAsync(s.StatusTransfer(120, "QUALITY_INSPECTION", "AVAILABLE"));

        var completed = await Expect(s.ConfirmAsync(refused, 60, s.A01), 201);    // P closes N
        Assert.Equal("Completed", Status(completed));
        Assert.Equal("SupersededByNewConfirmation", Confirmation(completed, 1).GetProperty("resolution").GetString());

        await s.LateCommitAsync(TaskId(task), 1);                                  // N's movement appears
        await s.ReconcileAsync();
        var caught = await s.GetTaskAsync(TaskId(task));
        Assert.Equal("IntegrityError", ConfirmationStatus(caught, 1));
        Assert.Equal("SupersededByNewConfirmation", Confirmation(caught, 1).GetProperty("priorResolution").GetString());

        var resolved = await Expect(s.ResolveAsync(TaskId(task), 1, ETagOf(caught)), 200);
        Assert.Equal("Posted", ConfirmationStatus(resolved, 1));
        Assert.Equal(120m, D(resolved, "confirmedQuantity"));
        Assert.Equal("Completed", Status(resolved));
        var deviation = Assert.Single(s.TaskEvents(TaskId(task)), e => e["Kind"].AsString == "ConfirmationDeviation");
        Assert.Equal("true", deviation["Data"]["overPlanned"].AsString);
    }
}

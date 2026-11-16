using System.Text.Json;
using MongoDB.Bson;
using Xunit;
using static Diten.InventoryService.Tests.Warehouse.PutawayRecoveryTests;
using static Diten.InventoryService.Tests.Warehouse.PutawayScenario;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-2 — the orderings of pack §8.3 step 4 that need two writers held at a chosen point (AC-64 (C2), (E), (D)-(ii), (D)-(iii), the
/// sibling case of review 07 F-1; AC-70 the barrier sequence for "returned", cancel and close short). The holds are the fixture's seams:
/// a slow ledger (<see cref="WarehouseApiFactory.AroundPosting"/>), a hook before a task write (<see cref="WarehouseApiFactory.BeforeTaskWrite"/>),
/// the host's clock and a late commit of the confirmation's stored request. Every hold is bounded (30 s), so a broken order fails, never hangs.
/// </summary>
public sealed class PutawayRaceTests : IClassFixture<WarehouseApiFactory>
{
    private static readonly TimeSpan Hold = TimeSpan.FromSeconds(30);

    private readonly WarehouseApiFactory _factory;

    public PutawayRaceTests(WarehouseApiFactory factory) => _factory = factory;

    private void LockExpires() => _factory.Clock.Advance(TimeSpan.FromMinutes(3));

    private async Task<(PutawayScenario S, JsonElement Task)> OpenAsync(decimal stock = 100, decimal planned = 60)
    {
        var s = await new PutawayScenario(_factory).ReadyAsync();
        await s.ReceiveAsync(stock);
        return (s, await s.TaskAsync(planned));
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The n-th task write of the tenant from now on waits until released (the others pass).</summary>
    private (TaskCompletionSource Reached, TaskCompletionSource Release) PauseWrite(PutawayScenario s, int nth)
    {
        var reached = Signal();
        var release = Signal();
        var writes = 0;
        _factory.BeforeTaskWrite[s.Tenant] = async () =>
        {
            if (Interlocked.Increment(ref writes) != nth) return;
            reached.TrySetResult();
            await release.Task.WaitAsync(Hold);
        };
        return (reached, release);
    }

    private void NoHooks(PutawayScenario s)
    {
        _factory.BeforeTaskWrite.TryRemove(s.Tenant, out _);
        s.LedgerHealthy();
    }

    /// <summary>A confirmation whose first attempt (X) is held inside the ledger call; returns X's pending answer and the gate that frees it.</summary>
    private static async Task<(Task<HttpResponseMessage> X, TaskCompletionSource Gate)> InFlightAsync(PutawayScenario s, JsonElement task, decimal q)
    {
        var gate = s.LedgerWaitsFor();
        var x = s.ConfirmAsync(task, q, s.A01);
        await WaitForAsync(() => s.StoredTask(TaskId(task))["Confirmations"].AsBsonArray.Count == 1);
        return (x, gate);
    }

    // ── AC-64 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC64_C2_both_locks_expired_the_first_attempt_writes_nothing_and_the_sweeper_posts_once()
    {
        var (s, task) = await OpenAsync();
        var id = TaskId(task);
        var first = Signal();
        var calls = 0;
        _factory.AroundPosting[s.Tenant] = async (_, real) =>
        {
            if (Interlocked.Increment(ref calls) != 1) throw new InvalidOperationException("test: Y is cut off inside the ledger call");
            await first.Task.WaitAsync(Hold);
            return await real();
        };
        try
        {
            var x = s.ConfirmAsync(task, 20, s.A01);
            await WaitForAsync(() => calls == 1);
            LockExpires();                                               // X's lock runs out while X waits in the ledger
            await Expect(s.RetryAsync(id, 1), 503);                       // Y takes the lock and is cut off: Pending, Y's lock
            var yLock = s.StoredTask(id)["Confirmations"][0]["AttemptId"].ToString();
            LockExpires();                                               // … and Y's lock runs out too

            first.TrySetResult();                                        // X's ledger call commits; X's result write finds Y's expired lock
            var xAnswer = await Expect(x, 409);
            Assert.Equal("WMS_CONFLICT", Code(xAnswer));
            Assert.Equal("Pending", ConfirmationStatus(DetailsTask(xAnswer), 1));
            Assert.Equal(yLock, s.StoredTask(id)["Confirmations"][0]["AttemptId"].ToString());
        }
        finally
        {
            NoHooks(s);
        }

        await s.ReconcileAsync();
        var swept = await s.GetTaskAsync(id);
        Assert.Equal("Posted", ConfirmationStatus(swept, 1));
        Assert.Single(s.TaskMovements(id));
        Assert.DoesNotContain("ConfirmationIntegrityError", s.EventKinds(id));
    }

    [Fact]
    public async Task AC64_E_a_retry_refusal_meeting_a_confirmation_the_sweeper_posted_writes_nothing()
    {
        var (s, task) = await OpenAsync();
        var id = TaskId(task);
        s.LedgerAnswersBefore(503);
        await Expect(s.ConfirmAsync(task, 20, s.A01), 503);
        s.LedgerHealthy();
        LockExpires();
        s.Recall();                                                      // Y's pre-check will refuse

        var (reached, release) = PauseWrite(s, 2);                       // write 1 = Y's lock, write 2 = Y's Refused result
        try
        {
            var y = s.RetryAsync(id, 1);
            await reached.Task.WaitAsync(Hold);
            LockExpires();                                               // Y's lock runs out while Y is held
            await s.LateCommitAsync(id, 1);                              // the ledger holds the confirmation's movement
            await s.ReconcileAsync();                                    // the sweeper writes Posted
            Assert.Equal("Posted", ConfirmationStatus(await s.GetTaskAsync(id), 1));

            release.TrySetResult();
            var answer = await Expect(y, 409);
            Assert.Equal("WMS_CONFLICT", Code(answer));
        }
        finally
        {
            NoHooks(s);
        }

        var final = await s.GetTaskAsync(id);
        Assert.Equal("Posted", ConfirmationStatus(final, 1));
        Assert.DoesNotContain("ConfirmationRefused", s.EventKinds(id));
        Assert.Single(s.TaskMovements(id));
    }

    [Fact]
    public async Task AC64_D_ii_the_defensive_branch_marked_it_first_the_posted_attempt_writes_nothing()
    {
        var (s, task) = await OpenAsync();
        var id = TaskId(task);
        var (x, gate) = await InFlightAsync(s, task, 20);
        LockExpires();
        s.Recall();
        await Expect(s.RetryAsync(id, 1), 422);                          // Y refuses on its pre-check: Refused, RefusedOnRetry

        var (reached, release) = PauseWrite(s, 1);                       // X's next write is its result write
        try
        {
            gate.TrySetResult();                                         // X's movement commits
            await reached.Task.WaitAsync(Hold);
            await s.ReconcileAsync();                                    // the defensive branch: IntegrityError (StatusDiverged)
            var marked = s.StoredTask(id);
            var version = marked["Version"].AsInt32;
            var events = s.TaskEvents(id).Count;

            release.TrySetResult();
            var answer = await Expect(x, 409);
            Assert.Equal("WMS_POSTING_INTEGRITY_ERROR", Code(answer));
            Assert.Equal(version, s.StoredTask(id)["Version"].AsInt32);  // X wrote nothing
            Assert.Equal(events, s.TaskEvents(id).Count);
            Assert.Equal(marked["Confirmations"][0].ToJson(), s.StoredTask(id)["Confirmations"][0].ToJson());
        }
        finally
        {
            NoHooks(s);
        }

        Assert.Single(s.TaskEvents(id), e => e["Kind"].AsString == "ConfirmationIntegrityError");
        Assert.Single(s.TaskMovements(id));
    }

    [Fact]
    public async Task AC64_D_iii_three_lost_diverge_writes_answer_conflict_the_refusal_stays_and_the_defensive_branch_marks_it_later()
    {
        var (s, task) = await OpenAsync();
        var id = TaskId(task);
        var (x, gate) = await InFlightAsync(s, task, 20);
        LockExpires();
        s.Recall();
        await Expect(s.RetryAsync(id, 1), 422);
        var events = s.TaskEvents(id).Count;

        var bumps = 0;
        _factory.BeforeTaskWrite[s.Tenant] = async () =>
        {
            Interlocked.Increment(ref bumps);
            await s.MutateStoredTaskAsync(id, t => t.Note = "baska bir yazim " + bumps); // a write that leaves the Refused confirmation as it is
        };
        try
        {
            gate.TrySetResult();
            var answer = await Expect(x, 409);
            Assert.Equal("WMS_CONFLICT", Code(answer));
            Assert.Equal(4, bumps);                                       // the first result write + exactly three (D) attempts
        }
        finally
        {
            NoHooks(s);
        }

        Assert.Equal("Refused", ConfirmationStatus(await s.GetTaskAsync(id), 1));
        Assert.Equal(events, s.TaskEvents(id).Count);                    // X left no event
        await s.ReconcileAsync();
        Assert.Equal("IntegrityError", ConfirmationStatus(await s.GetTaskAsync(id), 1));
    }

    [Fact]
    public async Task AC64_sibling_the_defensive_branch_marks_N_while_N_plus_1_is_in_flight_N_plus_1_still_posts()
    {
        var (s, task) = await OpenAsync();
        var id = TaskId(task);
        s.LedgerAnswersBefore(503);
        await Expect(s.ConfirmAsync(task, 20, s.A01), 503);
        s.LedgerAnswersBefore(422, "INSUFFICIENT_STOCK");
        LockExpires();
        await Expect(s.RetryAsync(id, 1), 422);                          // N refused by the ledger on a retry
        s.LedgerHealthy();

        var current = await s.GetTaskAsync(id);
        var gate = s.LedgerWaitsFor();
        try
        {
            var next = s.ConfirmAsync(current, 20, s.B01);               // N+1: its pre-lookup of N finds nothing yet
            await WaitForAsync(() => s.StoredTask(id)["Confirmations"].AsBsonArray.Count == 2);
            await s.LateCommitAsync(id, 1);                              // N's movement appears
            await s.ReconcileAsync();                                    // the defensive branch: N IntegrityError, Version + 1
            Assert.Equal("IntegrityError", ConfirmationStatus(await s.GetTaskAsync(id), 1));

            gate.TrySetResult();
            var posted = await Expect(next, 201);                        // N+1 lost its CAS, re-read, still its own Pending → wrote again
            Assert.Equal("Posted", ConfirmationStatus(posted, 2));
            Assert.Equal("IntegrityError", ConfirmationStatus(posted, 1));
            Assert.False(Confirmation(posted, 1).TryGetProperty("resolution", out _)); // not Superseded
        }
        finally
        {
            NoHooks(s);
        }

        Assert.Single(s.TaskEvents(id), e => e["Kind"].AsString == "ConfirmationIntegrityError");
    }

    // ── AC-70 the barrier sequence ─────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("returned")]
    [InlineData("cancel")]
    [InlineData("close-short")]
    public async Task AC70_the_user_command_writes_between_the_defensive_read_and_its_write_and_both_records_survive(string command)
    {
        var (s, task) = await OpenAsync();
        var id = TaskId(task);
        var current = task;
        if (command == "close-short") current = await Expect(s.ConfirmAsync(task, 10, s.A01), 201);
        var seq = command == "close-short" ? 2 : 1;

        s.LedgerAnswersBefore(503);
        await Expect(s.ConfirmAsync(current, 20, s.A01), 503);
        s.LedgerAnswersBefore(422, "INSUFFICIENT_STOCK");
        LockExpires();
        var refused = DetailsTask(await Expect(s.RetryAsync(id, seq), 422)); // N: Refused on a retry, Resolution empty, version v
        s.LedgerHealthy();
        var v = refused.GetProperty("version").GetInt32();

        var userAtWrite = Signal();
        var userGo = Signal();
        var sweepAtWrite = Signal();
        var sweepGo = Signal();
        var writes = 0;
        _factory.BeforeTaskWrite[s.Tenant] = async () =>
        {
            switch (Interlocked.Increment(ref writes))
            {
                case 1:
                    userAtWrite.TrySetResult();
                    await userGo.Task.WaitAsync(Hold);
                    break;
                case 2:
                    sweepAtWrite.TrySetResult();
                    await sweepGo.Task.WaitAsync(Hold);
                    break;
            }
        };
        try
        {
            var user = command switch                                    // its pre-lookup finds nothing yet; it stops before its write
            {
                "returned" => s.ReturnedAsync(id, seq, ETagOf(refused)),
                "cancel" => s.CancelAsync(id, ETagOf(refused)),
                _ => s.CloseShortAsync(id, ETagOf(refused))
            };
            await userAtWrite.Task.WaitAsync(Hold);
            await s.LateCommitAsync(id, seq);                            // N's movement appears
            var sweep = Task.Run(s.ReconcileAsync);                      // the defensive branch reads N (Refused, version v) and stops before its write
            await sweepAtWrite.Task.WaitAsync(Hold);
            var read = s.StoredTask(id);
            Assert.Equal(v, read["Version"].AsInt32);
            Assert.Equal("Refused", read["Confirmations"][seq - 1]["Status"].AsString);

            userGo.TrySetResult();                                       // the user command writes first: 200, version v + 1
            await Expect(user, 200);
            var afterUser = s.StoredTask(id);
            Assert.Equal(v + 1, afterUser["Version"].AsInt32);
            Assert.Equal("Refused", afterUser["Confirmations"][seq - 1]["Status"].AsString);

            sweepGo.TrySetResult();                                      // the defensive write at version v touches 0 records, reads again, marks it
            await sweep;
        }
        finally
        {
            NoHooks(s);
        }

        var final = await s.GetTaskAsync(id);
        var c = Confirmation(final, seq);
        Assert.Equal("IntegrityError", c.GetProperty("status").GetString());
        Assert.False(c.TryGetProperty("resolution", out _));             // never IntegrityError and a resolution together
        Assert.Equal(v + 2, final.GetProperty("version").GetInt32());
        switch (command)
        {
            case "returned":
                Assert.Equal("ReturnedToSource", c.GetProperty("priorResolution").GetString()); // the return is not lost
                break;
            case "cancel":
                Assert.Equal("Cancelled", Status(final));
                break;
            default:
                Assert.Equal("Completed", Status(final));
                break;
        }

        Assert.Equal("WMS_CONFLICT", await ErrorCode(s.CancelAsync(id, ETagOf(refused)), 409)); // an old If-Match after the system write
    }
}

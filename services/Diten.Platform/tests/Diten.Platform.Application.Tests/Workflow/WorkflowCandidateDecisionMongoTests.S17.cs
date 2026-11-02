using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using Answer = Diten.Platform.Application.Common.Response<Diten.Platform.Application.Features.Workflow.WorkflowTaskTransitionResponse>;

namespace Diten.Platform.Application.Tests.Workflow;

/*
 * MOD-0023 §17 DL-2 — THE SAME KEY, ANSWERED (AC-W6, W7, W8, W10, W14, W18 and the Delegate exception), over the real
 * repositories and a real replica-set transaction, every call through its production handler. Two clocks are forced, never
 * hoped for: a held read (the late twin: its key lookup ran before the twin committed, its task read after) and the
 * transaction barrier / injected "unavailable" executors of the earlier decision tests.
 *
 * Existing tests reused as evidence, NOT rewritten (their names, for the report):
 *   AC-W8 (c) approve vs approve, final / intermediate .. Two_candidates_approving_the_FINAL_step_at_once_write_one_decision_and_one_completion_event,
 *                                                         Two_candidates_approving_an_INTERMEDIATE_step_at_once_create_exactly_one_next_task
 *   AC-W8 (c) approve vs reject, final step ............. (WorkflowCandidateDecisionMongoTests.Fix1.cs:356-376)
 *   AC-W8 (c) unavailable, somebody else decided / nothing moved (Fix1.cs:141-172)
 *   same-key different body, the 409 that stays ......... (D2.cs:265-276, D2Fix1.cs:368-378, unchanged)
 *   identical twin 200 Replayed ......................... (D2Fix1.cs:235-259, :380-387)
 */
public sealed partial class WorkflowCandidateDecisionMongoTests
{
    private const string Principal = "target-principal-1";
    private static readonly (string Comment, string? Target) BodyA = ("not", Principal);               // stores "not target:…"
    private static readonly (string Comment, string? Target) BodyB = ("not target:" + Principal, null); // stores the same text

    // ── helpers ────────────────────────────────────────────────────────────────────────────────────────────────

    private static void Measure(string name, string value)
    {
        if (Environment.GetEnvironmentVariable("DL2_MEASURE_FILE") is { Length: > 0 } path)
        {
            File.AppendAllText(path, $"{name}\t{value}{Environment.NewLine}");
        }
    }

    private async Task<List<WorkflowTransitionLog>> RowsAsync(Guid instanceId, WorkflowTransitionAction action, string key) =>
        (await _store.Logs.ListByInstanceIdAsync(instanceId)).Where(l => l.Action == action && l.IdempotencyKey == key).ToList();

    /// <summary>One decision row under the key and — for an intermediate approval — exactly one next task and one next-Start row.</summary>
    private async Task<WorkflowTransitionLog> OneDecisionAsync(
        Seeded seeded, WorkflowTransitionAction action, string key, bool intermediateApproval)
    {
        var row = Assert.Single(await RowsAsync(seeded.Instance.Id, action, key));
        var tasks = await _store.Tasks.ListByInstanceIdAsync(seeded.Instance.Id);
        Assert.Equal(intermediateApproval ? 2 : 1, tasks.Count);
        var nextStarts = (await _store.Logs.ListByInstanceIdAsync(seeded.Instance.Id))
            .Where(l => l.Action == WorkflowTransitionAction.Start && l.IdempotencyKey!.StartsWith(key + ":next:", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(intermediateApproval ? 1 : 0, nextStarts.Count);
        return row;
    }

    private static void AssertCode(Answer answer, int status, string reasonCode)
    {
        Assert.False(answer.IsSuccessful, "expected a refusal, got a success");
        Assert.Equal(status, answer.StatusCode);
        Assert.Equal(reasonCode, answer.ReasonCode);
    }

    private static void AssertReplay(Answer answer, Guid transitionLogId)
    {
        Assert.True(answer.IsSuccessful, answer.ReasonCode);
        Assert.True(answer.Data!.IsIdempotent);
        Assert.Equal(transitionLogId, answer.Data.TransitionLogId);
    }

    private Task<Answer> ByAsync(string action, Guid taskId, string actor, string key, IPlatformTransactionExecutor? executor = null,
        IApprovalTaskRepository? tasks = null, int? expectedVersion = null, string? reason = null, string? comment = null,
        string? evidence = null) => action switch
    {
        "approve" => _store.ApproveAsync(taskId, actor, key, executor, tasks, expectedVersion, reason ?? "APPROVED", comment, evidence),
        "reject" => _store.RejectAsync(taskId, actor, key, executor, tasks, expectedVersion, reason ?? "REJECTED", comment ?? "no", evidence),
        "requestInfo" => _store.RequestInfoAsync(taskId, actor, key, executor, tasks, expectedVersion, reason ?? "INFO", comment ?? "please add", evidence),
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
    };

    private static WorkflowTransitionAction ActionOf(string action) => action switch
    {
        "approve" => WorkflowTransitionAction.Approve,
        "reject" => WorkflowTransitionAction.Reject,
        _ => WorkflowTransitionAction.RequestInfo
    };

    private Task<Answer> InfoAsync(Guid taskId, string actor, string key, (string Comment, string? Target) body,
        IPlatformTransactionExecutor? executor = null, IApprovalTaskRepository? tasks = null, int? expectedVersion = null) =>
        _store.RequestInfoAsync(taskId, actor, key, executor, tasks, expectedVersion, comment: body.Comment, target: body.Target);

    /// <summary>The late twin: <paramref name="late"/> runs with a task repository that holds its first task read; the winner
    /// then commits; the held call is released and answers. Returns both answers.</summary>
    private async Task<(Answer Winner, Answer Late)> LateTwinAsync(
        Func<IApprovalTaskRepository, Task<Answer>> late, Func<Task<Answer>> winner, Func<Task>? beforeRelease = null)
    {
        var hold = new S17Hold();
        var lateCall = Task.Run(() => late(new LateTaskReadHold(_store.Tasks, hold)));
        await hold.ReachedAsync();
        var won = await winner();
        Assert.True(won.IsSuccessful, won.ReasonCode);
        if (beforeRelease is not null)
        {
            await beforeRelease();
        }

        hold.Release();
        return (won, await lateCall);
    }

    private async Task UnsetTargetAsync(Guid logId)
    {
        var rows = _store.Context.Database.GetCollection<WorkflowTransitionLog>(PlatformCollections.WorkflowTransitionLogs);
        await rows.UpdateOneAsync(l => l.Id == logId, Builders<WorkflowTransitionLog>.Update.Unset(l => l.TargetPrincipalId));
        var raw = await rows.Find(l => l.Id == logId).As<BsonDocument>().FirstAsync();
        Assert.DoesNotContain(raw.Names, name => string.Equals(name, "TargetPrincipalId", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>An unavailable executor whose "before" writes a copy of <paramref name="written"/> (a row the handler wrote on a
    /// sibling instance) as the twin on <paramref name="target"/>; the copy keeps the handler-written Comment and TargetPrincipalId.</summary>
    private ReportsUnavailableAfter TwinExecutor(Seeded target, WorkflowTransitionLog written, string key, bool versionMoves,
        bool legacy = false) =>
        new(async () =>
        {
            await _store.Logs.CreateAsync(new WorkflowTransitionLog
            {
                TenantId = _store.TenantContext.TenantId,
                WorkflowInstanceId = target.Instance.Id,
                ApprovalTaskId = target.Task.Id,
                Action = written.Action,
                FromState = written.FromState,
                ToState = written.ToState,
                FromStatus = written.FromStatus,
                ToStatus = written.ToStatus,
                ActorId = written.ActorId,
                ActorRef = written.ActorRef,
                ReasonCode = written.ReasonCode,
                Comment = written.Comment,
                EvidenceRef = written.EvidenceRef,
                TargetPrincipalId = legacy ? null : written.TargetPrincipalId,
                IdempotencyKey = key,
                SequenceNo = 1
            });
            if (versionMoves)
            {
                await _store.Context.Database.GetCollection<ApprovalTask>(PlatformCollections.ApprovalTasks).UpdateOneAsync(
                    t => t.Id == target.Task.Id, Builders<ApprovalTask>.Update.Inc(t => t.Version, 1));
            }
        });

    private async Task<WorkflowTransitionLog> WrittenByHandlerAsync(
        string key, string actor, (string Comment, string? Target) body)
    {
        var model = await _store.SeedAsync([D(First), D(Second)]);
        var done = await InfoAsync(model.Task.Id, actor, key + "-model", body);
        Assert.True(done.IsSuccessful, done.ReasonCode);
        return Assert.Single(await _store.Logs.ListByInstanceIdAsync(model.Instance.Id), l => l.Action == WorkflowTransitionAction.RequestInfo);
    }

    // ── AC-W6 · Approve ────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AC_W6_a_replay_on_mongo(bool intermediate)
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)], twoSteps: intermediate);
        var key = "w6a-" + intermediate;
        var first = await _store.ApproveAsync(seeded.Task.Id, D(First), key);
        Assert.True(first.IsSuccessful, first.ReasonCode);
        Assert.False(first.Data!.IsIdempotent);

        // a stale expectedVersion changes nothing: the lookup answers before any task, version or decidable check
        var again = await _store.ApproveAsync(seeded.Task.Id, D(First), key, expectedVersion: seeded.Task.Version);

        AssertReplay(again, first.Data.TransitionLogId);
        Assert.Equal(first.Data.TransitionLogId, (await OneDecisionAsync(seeded, WorkflowTransitionAction.Approve, key, intermediate)).Id);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task AC_W6_b_late_twin_is_200(bool intermediate, bool staleVersion)
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)], twoSteps: intermediate);
        var key = "w6b-" + intermediate + staleVersion;
        int? version = staleVersion ? seeded.Task.Version : null;

        var (won, late) = await LateTwinAsync(
            tasks => _store.ApproveAsync(seeded.Task.Id, D(First), key, tasks: tasks, expectedVersion: version),
            () => _store.ApproveAsync(seeded.Task.Id, D(First), key));

        AssertReplay(late, won.Data!.TransitionLogId);
        Assert.Equal(won.Data.TransitionLogId, (await OneDecisionAsync(seeded, WorkflowTransitionAction.Approve, key, intermediate)).Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AC_W6_c_barrier_identical_overlap_writes_one_row(bool intermediate)
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)], twoSteps: intermediate);
        var key = "w6c-" + intermediate;
        var barrier = new TwoCallerBarrier(new PlatformTransactionExecutor(_store.Context));

        var answers = await Task.WhenAll(
            _store.ApproveAsync(seeded.Task.Id, D(First), key, barrier),
            _store.ApproveAsync(seeded.Task.Id, D(First), key, barrier));

        var winner = Assert.Single(answers, a => a.IsSuccessful && !a.Data!.IsIdempotent);
        var loser = answers.Single(a => !ReferenceEquals(a, winner));
        if (loser.IsSuccessful)
        {
            AssertReplay(loser, winner.Data!.TransitionLogId); // 200 Replayed: its unavailable re-read found the twin's row
        }
        else
        {
            AssertCode(loser, 409, WorkflowReasonCodes.WorkflowTransitionConflict);
        }

        Measure("AC_W6_c_barrier(" + intermediate + ")", loser.IsSuccessful ? "200 Replayed" : "409 WORKFLOW_TRANSITION_CONFLICT");
        Assert.Equal(winner.Data!.TransitionLogId, (await OneDecisionAsync(seeded, WorkflowTransitionAction.Approve, key, intermediate)).Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AC_W6_c_zero_row_503_then_first_write(bool intermediate)
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)], twoSteps: intermediate);
        var key = "w6c503-" + intermediate;

        var unavailable = await _store.ApproveAsync(seeded.Task.Id, D(First), key, new ReportsUnavailableAfter(() => Task.CompletedTask));

        AssertCode(unavailable, 503, WorkflowReasonCodes.WorkflowTransactionUnavailable);
        Assert.Empty(await RowsAsync(seeded.Instance.Id, WorkflowTransitionAction.Approve, key)); // the body never ran: zero rows
        var task = (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!;
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, task.Status);
        Assert.Equal(seeded.Task.Version, task.Version);

        // no row under the key: the next attempt is a FIRST write, not a replay
        var first = await _store.ApproveAsync(seeded.Task.Id, D(First), key);
        Assert.True(first.IsSuccessful, first.ReasonCode);
        Assert.False(first.Data!.IsIdempotent);
        Assert.Equal(first.Data.TransitionLogId, (await OneDecisionAsync(seeded, WorkflowTransitionAction.Approve, key, intermediate)).Id);
    }

    // ── AC-W7 · Reject and RequestInfo ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC_W7_a_replay_on_mongo()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        var first = await _store.RejectAsync(seeded.Task.Id, D(First), "w7a");
        Assert.True(first.IsSuccessful, first.ReasonCode);

        var again = await _store.RejectAsync(seeded.Task.Id, D(First), "w7a", expectedVersion: seeded.Task.Version);

        AssertReplay(again, first.Data!.TransitionLogId);
        Assert.Equal(first.Data.TransitionLogId, (await OneDecisionAsync(seeded, WorkflowTransitionAction.Reject, "w7a", false)).Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AC_W7_b_reject_late_twin_is_200(bool staleVersion)
    {
        // RejectAsync sends a non-blank comment and wires no version repository: the handler's own pre-check (which reads
        // the task before the support's lookup) does not run, so the first task read IS the support's.
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        var key = "w7b-" + staleVersion;
        int? version = staleVersion ? seeded.Task.Version : null;

        var (won, late) = await LateTwinAsync(
            tasks => _store.RejectAsync(seeded.Task.Id, D(First), key, tasks: tasks, expectedVersion: version),
            () => _store.RejectAsync(seeded.Task.Id, D(First), key));

        AssertReplay(late, won.Data!.TransitionLogId);
        Assert.Equal(won.Data.TransitionLogId, (await OneDecisionAsync(seeded, WorkflowTransitionAction.Reject, key, false)).Id);
    }

    [Fact]
    public async Task AC_W7_c_reject_barrier_identical_overlap()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        var barrier = new TwoCallerBarrier(new PlatformTransactionExecutor(_store.Context));

        var answers = await Task.WhenAll(
            _store.RejectAsync(seeded.Task.Id, D(First), "w7c", barrier),
            _store.RejectAsync(seeded.Task.Id, D(First), "w7c", barrier));

        var winner = Assert.Single(answers, a => a.IsSuccessful && !a.Data!.IsIdempotent);
        var loser = answers.Single(a => !ReferenceEquals(a, winner));
        if (loser.IsSuccessful) AssertReplay(loser, winner.Data!.TransitionLogId);
        else AssertCode(loser, 409, WorkflowReasonCodes.WorkflowTransitionConflict);
        Measure("AC_W7_c_reject_barrier", loser.IsSuccessful ? "200 Replayed" : "409 WORKFLOW_TRANSITION_CONFLICT");
        Assert.Equal(winner.Data!.TransitionLogId, (await OneDecisionAsync(seeded, WorkflowTransitionAction.Reject, "w7c", false)).Id);
    }

    [Fact]
    public async Task AC_W7_requestinfo_a_replay()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        var first = await _store.RequestInfoAsync(seeded.Task.Id, D(First), "w7ri-a");
        Assert.True(first.IsSuccessful, first.ReasonCode);

        var again = await _store.RequestInfoAsync(seeded.Task.Id, D(First), "w7ri-a", expectedVersion: seeded.Task.Version);

        AssertReplay(again, first.Data!.TransitionLogId);
        Assert.Single(await RowsAsync(seeded.Instance.Id, WorkflowTransitionAction.RequestInfo, "w7ri-a"));
    }

    [Fact]
    public async Task AC_W7_requestinfo_b_stale_version_is_conflict_then_replay_200()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);

        var (won, late) = await LateTwinAsync(
            tasks => _store.RequestInfoAsync(seeded.Task.Id, D(First), "w7ri-b", tasks: tasks, expectedVersion: seeded.Task.Version),
            () => _store.RequestInfoAsync(seeded.Task.Id, D(First), "w7ri-b"));

        AssertCode(late, 409, WorkflowReasonCodes.WorkflowTransitionConflict); // unchanged under both options: Unknown for the consumer
        var replay = await _store.RequestInfoAsync(seeded.Task.Id, D(First), "w7ri-b");
        AssertReplay(replay, won.Data!.TransitionLogId); // and a replay of the key is case (a)
        Assert.Single(await RowsAsync(seeded.Instance.Id, WorkflowTransitionAction.RequestInfo, "w7ri-b"));
    }

    [Fact]
    public async Task AC_W7_requestinfo_b_duplicate_key_is_replay()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        var recorder = new RecordsUnavailable(new PlatformTransactionExecutor(_store.Context));

        var (won, late) = await LateTwinAsync(
            tasks => _store.RequestInfoAsync(seeded.Task.Id, D(First), "w7ri-dup", recorder, tasks),
            () => _store.RequestInfoAsync(seeded.Task.Id, D(First), "w7ri-dup"));

        // the unique index refused the late twin's log; the executor wrapped the driver's exception
        var write = Assert.IsType<MongoWriteException>(recorder.Inner);
        Assert.Equal(ServerErrorCategory.DuplicateKey, write.WriteError.Category);
        Assert.Contains("ux_workflow_transition_logs_task_action_idempotency_active", write.WriteError.Message, StringComparison.Ordinal);
        Measure("duplicate-key exception", write.GetType().FullName + " / " + write.WriteError.Category + " (wrapped in " + nameof(PlatformTransactionUnavailableException) + ")");

        AssertReplay(late, won.Data!.TransitionLogId);
        var row = Assert.Single(await RowsAsync(seeded.Instance.Id, WorkflowTransitionAction.RequestInfo, "w7ri-dup"));
        // only the mapping gives the twin's own transition; the re-read of the late task would say WaitingEvidence
        Assert.Equal(row.FromState, late.Data!.PreviousTaskStatus);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval.ToString(), late.Data.PreviousTaskStatus);
    }

    [Fact]
    public async Task AC_W7_requestinfo_c_barrier_identical_overlap()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        var recorder = new RecordsUnavailable(new TwoCallerBarrier(new PlatformTransactionExecutor(_store.Context)));

        var answers = await Task.WhenAll(
            _store.RequestInfoAsync(seeded.Task.Id, D(First), "w7ri-c", recorder),
            _store.RequestInfoAsync(seeded.Task.Id, D(First), "w7ri-c", recorder));

        var winner = Assert.Single(answers, a => a.IsSuccessful && !a.Data!.IsIdempotent);
        var loser = answers.Single(a => !ReferenceEquals(a, winner));
        if (loser.IsSuccessful) AssertReplay(loser, winner.Data!.TransitionLogId);
        else AssertCode(loser, 409, WorkflowReasonCodes.WorkflowTransitionConflict);
        Measure("AC_W7_requestinfo_c_barrier",
            (loser.IsSuccessful ? "200 Replayed" : "409 WORKFLOW_TRANSITION_CONFLICT")
            + "; executor reported unavailable: " + (recorder.Inner?.GetType().Name ?? "no"));
        Assert.Equal(winner.Data!.TransitionLogId,
            Assert.Single(await RowsAsync(seeded.Instance.Id, WorkflowTransitionAction.RequestInfo, "w7ri-c")).Id);
    }

    [Fact]
    public async Task AC_W7_requestinfo_c_injected_identical_twin_is_replayed()
    {
        var written = await WrittenByHandlerAsync("w7ri-twin", D(First), ("please add", null));
        Assert.Equal(string.Empty, written.TargetPrincipalId); // a request without a target stores "", never null
        var target = await _store.SeedAsync([D(First), D(Second)]);

        var answer = await _store.RequestInfoAsync(target.Task.Id, D(First), "w7ri-twin",
            TwinExecutor(target, written, "w7ri-twin", versionMoves: true));

        Assert.True(answer.IsSuccessful, answer.ReasonCode);
        Assert.True(answer.Data!.IsIdempotent);
        var row = Assert.Single(await RowsAsync(target.Instance.Id, WorkflowTransitionAction.RequestInfo, "w7ri-twin"));
        Assert.Equal(row.Id, answer.Data.TransitionLogId);
        Assert.Equal(string.Empty, row.TargetPrincipalId);
    }

    [Fact]
    public async Task AC_W7_requestinfo_c_zero_row_503_then_first_write()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);

        var unavailable = await _store.RequestInfoAsync(seeded.Task.Id, D(First), "w7ri-503",
            new ReportsUnavailableAfter(() => Task.CompletedTask));

        AssertCode(unavailable, 503, WorkflowReasonCodes.WorkflowTransactionUnavailable);
        Assert.Empty(await RowsAsync(seeded.Instance.Id, WorkflowTransitionAction.RequestInfo, "w7ri-503"));
        var task = (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!;
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, task.Status);
        Assert.Equal(seeded.Task.Version, task.Version);

        var first = await _store.RequestInfoAsync(seeded.Task.Id, D(First), "w7ri-503");
        Assert.True(first.IsSuccessful, first.ReasonCode);
        Assert.False(first.Data!.IsIdempotent);
        Assert.Single(await RowsAsync(seeded.Instance.Id, WorkflowTransitionAction.RequestInfo, "w7ri-503"));
    }

    // ── AC-W8 · two different decisions on one task ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AC_W8_b_late_loser_is_invalid_state_or_conflict(bool intermediate)
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)], twoSteps: intermediate);
        var winner = await _store.ApproveAsync(seeded.Task.Id, D(First), "w8-win-" + intermediate);
        Assert.True(winner.IsSuccessful, winner.ReasonCode);

        var withoutVersion = await _store.RejectAsync(seeded.Task.Id, D(Second), "w8-lose-" + intermediate);
        var withStaleVersion = await _store.RejectAsync(seeded.Task.Id, D(Second), "w8-lose-v-" + intermediate, expectedVersion: seeded.Task.Version);

        AssertCode(withoutVersion, 409, WorkflowReasonCodes.WorkflowTaskInvalidState);
        AssertCode(withStaleVersion, 409, WorkflowReasonCodes.WorkflowTransitionConflict);
        Assert.Empty(await RowsAsync(seeded.Instance.Id, WorkflowTransitionAction.Reject, "w8-lose-" + intermediate));
        Assert.Empty(await RowsAsync(seeded.Instance.Id, WorkflowTransitionAction.Reject, "w8-lose-v-" + intermediate));
        await OneDecisionAsync(seeded, WorkflowTransitionAction.Approve, "w8-win-" + intermediate, intermediate);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AC_W8_b_loser_replay_gets_the_same_answer(bool intermediate)
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)], twoSteps: intermediate);
        Assert.True((await _store.ApproveAsync(seeded.Task.Id, D(First), "w8r-win-" + intermediate)).IsSuccessful);

        // the re-read added for O-S17-3 (i) finds no row under the loser's key, so the refusal repeats
        for (var round = 0; round < 2; round++)
        {
            AssertCode(await _store.RejectAsync(seeded.Task.Id, D(Second), "w8r-lose"), 409, WorkflowReasonCodes.WorkflowTaskInvalidState);
            AssertCode(await _store.RejectAsync(seeded.Task.Id, D(Second), "w8r-lose-v", expectedVersion: seeded.Task.Version),
                409, WorkflowReasonCodes.WorkflowTransitionConflict);
        }

        Assert.Empty(await RowsAsync(seeded.Instance.Id, WorkflowTransitionAction.Reject, "w8r-lose"));
        Assert.Empty(await RowsAsync(seeded.Instance.Id, WorkflowTransitionAction.Reject, "w8r-lose-v"));
    }

    [Fact]
    public async Task AC_W8_c_approve_vs_reject_intermediate_step()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)], twoSteps: true);
        var barrier = new TwoCallerBarrier(new PlatformTransactionExecutor(_store.Context));

        var answers = await Task.WhenAll(
            _store.ApproveAsync(seeded.Task.Id, D(First), "w8c-approve", barrier),
            _store.RejectAsync(seeded.Task.Id, D(Second), "w8c-reject", barrier));

        var winner = Assert.Single(answers, a => a.IsSuccessful);
        AssertCode(Assert.Single(answers, a => !a.IsSuccessful), 409, WorkflowReasonCodes.WorkflowTransitionConflict);
        var logs = await _store.Logs.ListByInstanceIdAsync(seeded.Instance.Id);
        var decision = Assert.Single(logs, l => l.Action is WorkflowTransitionAction.Approve or WorkflowTransitionAction.Reject);
        Assert.Equal(winner.Data!.TransitionLogId, decision.Id);
        var tasks = await _store.Tasks.ListByInstanceIdAsync(seeded.Instance.Id);
        if (decision.Action == WorkflowTransitionAction.Approve)
        {
            Assert.Equal(2, tasks.Count); // exactly one next task and one next-Start row
            Assert.Single(logs, l => l.Action == WorkflowTransitionAction.Start && l.ApprovalTaskId != seeded.Task.Id);
        }
        else
        {
            Assert.Single(tasks);
        }
    }

    [Fact]
    public async Task AC_W8_c_unavailable_with_nothing_moved_writes_zero_rows()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);

        var answer = await _store.ApproveAsync(seeded.Task.Id, D(Second), "w8c-503", new ReportsUnavailableAfter(() => Task.CompletedTask));

        AssertCode(answer, 503, WorkflowReasonCodes.WorkflowTransactionUnavailable);
        Assert.Empty(await _store.Logs.ListByInstanceIdAsync(seeded.Instance.Id));
        var task = (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!;
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, task.Status);
        Assert.Equal(seeded.Task.Version, task.Version);
    }

    // ── AC-W10 · a replay after the task moved on ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("approve-intermediate")]
    [InlineData("approve-final")]
    [InlineData("reject")]
    [InlineData("request-info")]
    public async Task AC_W10_replay_after_the_task_moved_on(string kind)
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)], twoSteps: kind == "approve-intermediate");
        var key = "w10-" + kind;
        var first = kind switch
        {
            "reject" => await _store.RejectAsync(seeded.Task.Id, D(First), key),
            "request-info" => await _store.RequestInfoAsync(seeded.Task.Id, D(First), key),
            _ => await _store.ApproveAsync(seeded.Task.Id, D(First), key)
        };
        Assert.True(first.IsSuccessful, first.ReasonCode);
        if (kind == "request-info")
        {
            // WaitingEvidence is still open: the task moves on only when somebody decides it under another key
            Assert.True((await _store.ApproveAsync(seeded.Task.Id, D(Second), key + "-closes")).IsSuccessful);
        }

        var before = await _store.Logs.ListByInstanceIdAsync(seeded.Instance.Id);
        var statusBefore = (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!.Status;
        Assert.False((await _store.Tasks.GetByIdAsync(seeded.Task.Id))! is { Status: ApprovalTaskStatus.WaitingApproval or ApprovalTaskStatus.WaitingEvidence });

        var replay = kind switch
        {
            "reject" => await _store.RejectAsync(seeded.Task.Id, D(First), key),
            "request-info" => await _store.RequestInfoAsync(seeded.Task.Id, D(First), key),
            _ => await _store.ApproveAsync(seeded.Task.Id, D(First), key)
        };

        AssertReplay(replay, first.Data!.TransitionLogId);
        var after = await _store.Logs.ListByInstanceIdAsync(seeded.Instance.Id);
        Assert.Equal(before.Count, after.Count);
        Assert.Equal(before.Single(l => l.Id == first.Data.TransitionLogId).SequenceNo, after.Single(l => l.Id == first.Data.TransitionLogId).SequenceNo);
        Assert.Equal(statusBefore, (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!.Status);
    }

    // ── AC-W14 · a different actor ─────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    [InlineData("requestInfo")]
    public async Task AC_W14_a_other_actor_is_403(string action)
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        var key = "w14a-" + action;
        var first = await ByAsync(action, seeded.Task.Id, D(First), key);
        Assert.True(first.IsSuccessful, first.ReasonCode);

        var other = await ByAsync(action, seeded.Task.Id, D(Second), key);

        AssertCode(other, 403, WorkflowReasonCodes.WorkflowActorDenied);
        Assert.Equal(first.Data!.TransitionLogId, Assert.Single(await RowsAsync(seeded.Instance.Id, ActionOf(action), key)).Id);
    }

    [Theory]
    [InlineData("approve", false)]
    [InlineData("approve", true)]
    [InlineData("reject", false)]
    [InlineData("reject", true)]
    public async Task AC_W14_b_other_actor_late_is_403(string action, bool staleVersion)
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        var key = "w14b-" + action + staleVersion;
        int? version = staleVersion ? seeded.Task.Version : null;

        var (won, late) = await LateTwinAsync(
            tasks => ByAsync(action, seeded.Task.Id, D(Second), key, tasks: tasks, expectedVersion: version),
            () => ByAsync(action, seeded.Task.Id, D(First), key));

        AssertCode(late, 403, WorkflowReasonCodes.WorkflowActorDenied); // the re-read routes it to the actor-bound replay
        Assert.Equal(won.Data!.TransitionLogId, Assert.Single(await RowsAsync(seeded.Instance.Id, ActionOf(action), key)).Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AC_W14_requestinfo_b_other_actor(bool staleVersion)
    {
        // the other actor is a CANDIDATE ([First, Second]): a non-candidate returns at the candidate check, never reaching the mapping
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        var key = "w14ri-b-" + staleVersion;
        int? version = staleVersion ? seeded.Task.Version : null;

        var (won, late) = await LateTwinAsync(
            tasks => _store.RequestInfoAsync(seeded.Task.Id, D(Second), key, tasks: tasks, expectedVersion: version),
            () => _store.RequestInfoAsync(seeded.Task.Id, D(First), key));

        if (staleVersion) AssertCode(late, 409, WorkflowReasonCodes.WorkflowTransitionConflict);
        else AssertCode(late, 403, WorkflowReasonCodes.WorkflowActorDenied); // through the duplicate-key mapping
        Assert.Equal(won.Data!.TransitionLogId, Assert.Single(await RowsAsync(seeded.Instance.Id, WorkflowTransitionAction.RequestInfo, key)).Id);
    }

    [Fact]
    public async Task AC_W14_c_other_actor_overlap_is_409_or_503()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        var barrier = new TwoCallerBarrier(new PlatformTransactionExecutor(_store.Context));

        var answers = await Task.WhenAll(
            _store.ApproveAsync(seeded.Task.Id, D(First), "w14c", barrier),
            _store.ApproveAsync(seeded.Task.Id, D(Second), "w14c", barrier));

        var winner = Assert.Single(answers, a => a.IsSuccessful);
        var loser = Assert.Single(answers, a => !a.IsSuccessful);
        Assert.True(loser.StatusCode == 503 && loser.ReasonCode == WorkflowReasonCodes.WorkflowTransactionUnavailable
                    || loser.StatusCode == 409 && loser.ReasonCode == WorkflowReasonCodes.WorkflowTransitionConflict,
            $"{loser.StatusCode} {loser.ReasonCode}");
        Measure("AC_W14_c_overlap", loser.StatusCode + " " + loser.ReasonCode);
        Assert.Equal(winner.Data!.TransitionLogId, Assert.Single(await RowsAsync(seeded.Instance.Id, WorkflowTransitionAction.Approve, "w14c")).Id);
    }

    // ── AC-W14 · the same actor, a different body ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("approve", "reason")]
    [InlineData("approve", "comment")]
    [InlineData("approve", "evidence")]
    [InlineData("reject", "reason")]
    [InlineData("reject", "comment")]
    [InlineData("reject", "evidence")]
    [InlineData("requestInfo", "reason")]
    [InlineData("requestInfo", "comment")]
    [InlineData("requestInfo", "evidence")]
    public async Task AC_W14_a_different_body_is_409(string action, string field)
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        var key = "w14body-" + action + field;
        var first = await ByAsync(action, seeded.Task.Id, D(First), key, reason: "R1", comment: "c1", evidence: "e1");
        Assert.True(first.IsSuccessful, first.ReasonCode);

        var changed = await ByAsync(action, seeded.Task.Id, D(First), key,
            reason: field == "reason" ? "R2" : "R1", comment: field == "comment" ? "c2" : "c1", evidence: field == "evidence" ? "e2" : "e1");

        AssertCode(changed, 409, WorkflowReasonCodes.WorkflowTransitionIdempotencyConflict);
        var row = Assert.Single(await RowsAsync(seeded.Instance.Id, ActionOf(action), key));
        Assert.Equal(first.Data!.TransitionLogId, row.Id);
        Assert.Equal("R1", row.ReasonCode);
        Assert.Equal("e1", row.EvidenceRef);
        var same = await ByAsync(action, seeded.Task.Id, D(First), key, reason: "R1", comment: "c1", evidence: "e1");
        AssertReplay(same, first.Data.TransitionLogId); // the identical body still replays
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    [InlineData("requestInfo")]
    public async Task AC_W14_b_different_body_late_is_409(string action)
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        var key = "w14bbody-" + action;

        var (won, late) = await LateTwinAsync(
            tasks => ByAsync(action, seeded.Task.Id, D(First), key, tasks: tasks, comment: "late body"),
            () => ByAsync(action, seeded.Task.Id, D(First), key, comment: "first body"));

        AssertCode(late, 409, WorkflowReasonCodes.WorkflowTransitionIdempotencyConflict);
        Assert.Equal(won.Data!.TransitionLogId, Assert.Single(await RowsAsync(seeded.Instance.Id, ActionOf(action), key)).Id);
    }

    // ── AC-W14 · the target collision: (target P, comment "not") and (no target, comment "not target:P") ───────

    [Theory]
    [InlineData("AB")]
    [InlineData("BA")]
    public async Task AC_W14_target_collision_sequential(string direction)
    {
        var (firstBody, secondBody) = direction == "AB" ? (BodyA, BodyB) : (BodyB, BodyA);
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        var key = "w14tc-seq-" + direction;
        var first = await InfoAsync(seeded.Task.Id, D(First), key, firstBody);
        Assert.True(first.IsSuccessful, first.ReasonCode);

        var second = await InfoAsync(seeded.Task.Id, D(First), key, secondBody);

        AssertCode(second, 409, WorkflowReasonCodes.WorkflowTransitionIdempotencyConflict);
        var row = Assert.Single(await RowsAsync(seeded.Instance.Id, WorkflowTransitionAction.RequestInfo, key));
        Assert.Equal("not target:" + Principal, row.Comment); // non-vacuity: both bodies store this very comment
        Assert.Equal(first.Data!.TransitionLogId, row.Id);
        Assert.Equal(direction == "AB" ? Principal : string.Empty, row.TargetPrincipalId);
        AssertReplay(await InfoAsync(seeded.Task.Id, D(First), key, firstBody), row.Id);
    }

    [Theory]
    [InlineData("AB", false)]
    [InlineData("BA", false)]
    [InlineData("AB", true)]
    [InlineData("BA", true)]
    public async Task AC_W14_target_collision_duplicate_key(string direction, bool identical)
    {
        var (firstBody, secondBody) = direction == "AB" ? (BodyA, BodyB) : (BodyB, BodyA);
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        var key = "w14tc-dup-" + direction + identical;

        var (won, late) = await LateTwinAsync(
            tasks => InfoAsync(seeded.Task.Id, D(First), key, identical ? firstBody : secondBody, tasks: tasks),
            () => InfoAsync(seeded.Task.Id, D(First), key, firstBody));

        var row = Assert.Single(await RowsAsync(seeded.Instance.Id, WorkflowTransitionAction.RequestInfo, key));
        Assert.Equal(won.Data!.TransitionLogId, row.Id);
        Assert.Equal(direction == "AB" ? Principal : string.Empty, row.TargetPrincipalId);
        if (identical) AssertReplay(late, row.Id);
        else AssertCode(late, 409, WorkflowReasonCodes.WorkflowTransitionIdempotencyConflict);
    }

    [Theory]
    [InlineData("AB", true)]
    [InlineData("BA", true)]
    [InlineData("AB", false)]
    [InlineData("BA", false)]
    public async Task AC_W14_target_collision_unavailable_reread(string direction, bool versionMoves)
    {
        var (firstBody, secondBody) = direction == "AB" ? (BodyA, BodyB) : (BodyB, BodyA);
        var key = "w14tc-re-" + direction + versionMoves;
        var written = await WrittenByHandlerAsync(key, D(First), firstBody); // the twin, written by the handler's real call
        Assert.Equal(direction == "AB" ? Principal : string.Empty, written.TargetPrincipalId);
        Assert.Equal("not target:" + Principal, written.Comment);
        var target = await _store.SeedAsync([D(First), D(Second)]);

        var answer = await InfoAsync(target.Task.Id, D(First), key, secondBody, TwinExecutor(target, written, key, versionMoves));

        // not 200 Replayed: the twin is somebody else's request under my key
        if (versionMoves) AssertCode(answer, 409, WorkflowReasonCodes.WorkflowTransitionConflict);
        else AssertCode(answer, 503, WorkflowReasonCodes.WorkflowTransactionUnavailable);
    }

    // ── AC-W14 · a row written before the field existed ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("sequential", "no-target")]
    [InlineData("sequential", "target")]
    [InlineData("duplicate-key", "no-target")]
    [InlineData("duplicate-key", "target")]
    [InlineData("unavailable-reread", "no-target")]
    [InlineData("unavailable-reread", "target")]
    public async Task AC_W14_legacy_row_identical_retry(string path, string row)
    {
        var body = row == "target" ? BodyA : ("not", (string?)null);
        var expectedComment = row == "target" ? "not target:" + Principal : "not";
        var key = "w14legacy-" + path + row;
        var seeded = await _store.SeedAsync([D(First), D(Second)]);

        switch (path)
        {
            case "sequential":
            {
                var first = await InfoAsync(seeded.Task.Id, D(First), key, body);
                Assert.True(first.IsSuccessful, first.ReasonCode);
                await UnsetTargetAsync(first.Data!.TransitionLogId);
                var stored = Assert.Single(await RowsAsync(seeded.Instance.Id, WorkflowTransitionAction.RequestInfo, key));
                Assert.Equal(expectedComment, stored.Comment);
                AssertReplay(await InfoAsync(seeded.Task.Id, D(First), key, body), stored.Id);
                Assert.Single(await RowsAsync(seeded.Instance.Id, WorkflowTransitionAction.RequestInfo, key));
                break;
            }
            case "duplicate-key":
            {
                Guid winnerId = Guid.Empty;
                var (won, late) = await LateTwinAsync(
                    tasks => InfoAsync(seeded.Task.Id, D(First), key, body, tasks: tasks),
                    () => InfoAsync(seeded.Task.Id, D(First), key, body),
                    async () =>
                    {
                        var stored = Assert.Single(await RowsAsync(seeded.Instance.Id, WorkflowTransitionAction.RequestInfo, key));
                        winnerId = stored.Id;
                        Assert.Equal(expectedComment, stored.Comment);
                        // the held call must still be in front of a MOVED task, or the two paths cannot be told apart
                        Assert.NotEqual(stored.FromState, (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!.Status.ToString());
                        await UnsetTargetAsync(stored.Id);
                    });
                AssertReplay(late, winnerId);
                Assert.Equal(ApprovalTaskStatus.WaitingApproval.ToString(), late.Data!.PreviousTaskStatus);
                Assert.Equal(won.Data!.TransitionLogId, Assert.Single(await RowsAsync(seeded.Instance.Id, WorkflowTransitionAction.RequestInfo, key)).Id);
                break;
            }
            default:
            {
                var written = await WrittenByHandlerAsync(key, D(First), body);
                Assert.Equal(expectedComment, written.Comment);
                var target = await _store.SeedAsync([D(First), D(Second)]);
                var answer = await InfoAsync(target.Task.Id, D(First), key, body,
                    TwinExecutor(target, written, key, versionMoves: true, legacy: true));
                Assert.True(answer.IsSuccessful, answer.ReasonCode);
                Assert.True(answer.Data!.IsIdempotent);
                var twin = Assert.Single(await RowsAsync(target.Instance.Id, WorkflowTransitionAction.RequestInfo, key));
                Assert.Null(twin.TargetPrincipalId);
                Assert.Equal(twin.Id, answer.Data.TransitionLogId);
                break;
            }
        }
    }

    // ── Delegate · the replay stays actor-bound only ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Delegate_replay_with_a_different_body_stays_idempotent()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        Task<Answer> Delegate(string reason, string? comment, Guid target) =>
            new DelegateWorkflowTaskHandler(_store.Tasks, _store.Instances, _store.Snapshots, _store.Logs,
                    new PlatformTransactionExecutor(_store.Context))
                .Handle(new DelegateWorkflowTaskCommand(seeded.Task.Id,
                    new DelegateWorkflowTaskRequest(D(First), D(target), reason, "delegate-k", comment), "corr-delegate"), default);

        var first = await Delegate("DELEGATED", "first", NextApprover);
        Assert.True(first.IsSuccessful, first.ReasonCode);

        var other = await Delegate("SOMETHING_ELSE", "another comment", Outsider);

        AssertReplay(other, first.Data!.TransitionLogId);
        var row = Assert.Single(await RowsAsync(seeded.Instance.Id, WorkflowTransitionAction.Delegate, "delegate-k"));
        Assert.Equal("DELEGATED", row.ReasonCode);
        Assert.Equal(D(NextApprover), (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!.AssigneeRef); // the first transition stands
    }

    // ── AC-W18 · Cancel ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC_W18_cancel_replay_other_actor_is_403()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        var first = await _store.CancelAsync(seeded.Task.Id, D(First), "w18-actor");
        Assert.True(first.IsSuccessful, first.ReasonCode);

        AssertCode(await _store.CancelAsync(seeded.Task.Id, D(Second), "w18-actor"), 403, WorkflowReasonCodes.WorkflowActorDenied);
        Assert.Equal(first.Data!.TransitionLogId, Assert.Single(await RowsAsync(seeded.Instance.Id, WorkflowTransitionAction.Cancel, "w18-actor")).Id);
    }

    [Fact]
    public async Task AC_W18_cancel_replay_different_body_is_409()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        var first = await _store.CancelAsync(seeded.Task.Id, D(First), "w18-body", comment: "withdrawn");
        Assert.True(first.IsSuccessful, first.ReasonCode);

        AssertCode(await _store.CancelAsync(seeded.Task.Id, D(First), "w18-body", comment: "another"),
            409, WorkflowReasonCodes.WorkflowTransitionIdempotencyConflict);
        AssertCode(await _store.CancelAsync(seeded.Task.Id, D(First), "w18-body", reason: "OTHER", comment: "withdrawn"),
            409, WorkflowReasonCodes.WorkflowTransitionIdempotencyConflict);
        Assert.Equal(first.Data!.TransitionLogId, Assert.Single(await RowsAsync(seeded.Instance.Id, WorkflowTransitionAction.Cancel, "w18-body")).Id);
    }

    [Fact]
    public async Task AC_W18_cancel_replay_same_actor_and_body_is_idempotent()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        var first = await _store.CancelAsync(seeded.Task.Id, D(First), "w18-same");
        Assert.True(first.IsSuccessful, first.ReasonCode);
        Assert.False(first.Data!.IsIdempotent);

        AssertReplay(await _store.CancelAsync(seeded.Task.Id, D(First), "w18-same"), first.Data.TransitionLogId);
        Assert.Single(await RowsAsync(seeded.Instance.Id, WorkflowTransitionAction.Cancel, "w18-same"));
    }

    // ── test seams ─────────────────────────────────────────────────────────────────────────────────────────────

    private sealed class S17Hold
    {
        private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _taken;

        public async Task PassAsync()
        {
            if (Interlocked.Exchange(ref _taken, 1) == 0)
            {
                _reached.TrySetResult();
                await _release.Task.WaitAsync(TimeSpan.FromSeconds(30));
            }
        }

        public Task ReachedAsync() => _reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
        public void Release() => _release.TrySetResult();
    }

    /// <summary>Holds the FIRST task read BEFORE it reads (so it sees what the winner committed meanwhile) and forwards every
    /// member to the real repository — the session-bound UpdateAsync / CreateAsync too, or the write would leave the transaction.</summary>
    private sealed class LateTaskReadHold(IApprovalTaskRepository inner, S17Hold hold) : IApprovalTaskRepository
    {
        public async Task<ApprovalTask?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            await hold.PassAsync();
            return await inner.GetByIdAsync(id, ct);
        }

        public Task<ApprovalTask> CreateAsync(ApprovalTask task, CancellationToken ct = default) => inner.CreateAsync(task, ct);
        public Task<ApprovalTask> CreateAsync(IPlatformTransactionSession session, ApprovalTask task, CancellationToken ct = default) =>
            inner.CreateAsync(session, task, ct);
        public Task<ApprovalTask> EnsureTrustedStartTaskAsync(ApprovalTask task, CancellationToken ct = default) =>
            inner.EnsureTrustedStartTaskAsync(task, ct);
        public Task<ApprovalTask?> GetFirstByInstanceIdAsync(Guid id, CancellationToken ct = default) => inner.GetFirstByInstanceIdAsync(id, ct);
        public Task<ApprovalTask?> GetActiveByInstanceIdAsync(Guid id, CancellationToken ct = default) => inner.GetActiveByInstanceIdAsync(id, ct);
        public Task<IReadOnlyList<ApprovalTask>> ListByInstanceIdAsync(Guid id, CancellationToken ct = default) => inner.ListByInstanceIdAsync(id, ct);
        public Task<IReadOnlyList<ApprovalTask>> GetAllForTenantAsync(CancellationToken ct = default) => inner.GetAllForTenantAsync(ct);
        public Task<bool> UpdateAsync(ApprovalTask task, int expectedVersion, CancellationToken ct = default) => inner.UpdateAsync(task, expectedVersion, ct);
        public Task<bool> UpdateAsync(IPlatformTransactionSession session, ApprovalTask task, int expectedVersion, CancellationToken ct = default) =>
            inner.UpdateAsync(session, task, expectedVersion, ct);
    }

    /// <summary>The real executor; remembers the driver's exception when the executor reports "unavailable".</summary>
    private sealed class RecordsUnavailable(IPlatformTransactionExecutor inner) : IPlatformTransactionExecutor
    {
        private Exception? _inner;
        public Exception? Inner => _inner;

        public async Task<T> ExecuteAsync<T>(
            Func<IPlatformTransactionSession, CancellationToken, Task<T>> body, CancellationToken cancellationToken = default)
        {
            try
            {
                return await inner.ExecuteAsync(body, cancellationToken);
            }
            catch (PlatformTransactionUnavailableException unavailable)
            {
                _inner = unavailable.InnerException;
                throw;
            }
        }
    }
}

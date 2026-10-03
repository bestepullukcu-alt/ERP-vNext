using System.Security.Cryptography;
using System.Text;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Workflow.Services;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

/// <summary>
/// WP-WF-ENGINE-CANDIDATES-01 · D1-FIX1 — what the independent review of D1 found missing, each as a rule with a test.
/// Same real replica set, same production handlers, same seeded shapes as the first half of this class.
/// </summary>
public sealed partial class WorkflowCandidateDecisionMongoTests
{
    private static readonly Guid CandidateA = Guid.Parse("1a000000-0000-4000-8000-00000000000a");
    private static readonly Guid CandidateB = Guid.Parse("1b000000-0000-4000-8000-00000000000b");

    // ── 1 · the starter does not choose their own approver ─────────────────────────────────────────────────────

    [Fact]
    public async Task The_starter_sitting_in_the_approving_seat_cannot_delegate_and_the_other_candidates_stay()
    {
        var seeded = await _store.SeedAsync([D(CandidateA), D(CandidateB), D(Starter)], startedBy: Starter);

        var response = await _store.DelegateAsync(seeded.Task.Id, D(Starter), D(Outsider), "starter-delegates");

        Assert.Equal(409, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.SodViolation, response.ReasonCode);
        var task = (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!;
        Assert.Equal(seeded.Snapshot.Id, task.AssignmentSnapshotId); // the snapshot did not change
        Assert.Single(await _store.Snapshots.ListByInstanceIdAsync(seeded.Instance.Id));
        Assert.DoesNotContain(await _store.Logs.ListByInstanceIdAsync(seeded.Instance.Id),
            log => log.Action == WorkflowTransitionAction.Delegate);
        // A and B are still candidates: one of them decides.
        Assert.Equal(403, (await _store.ApproveAsync(seeded.Task.Id, D(Outsider), "starter-x-cannot")).StatusCode);
        Assert.True((await _store.ApproveAsync(seeded.Task.Id, D(CandidateB), "starter-b-decides")).IsSuccessful);
    }

    [Fact]
    public async Task Somebody_who_is_not_a_candidate_is_not_told_that_the_actor_is_the_starter()
    {
        // The starter is NOT in the seat here: the answer is the plain 403, not the starter's 409.
        var seeded = await _store.SeedAsync([D(CandidateA)], startedBy: Starter);

        var response = await _store.DelegateAsync(seeded.Task.Id, D(Starter), D(Outsider), "starter-not-candidate");

        Assert.Equal(403, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowActorDenied, response.ReasonCode);
    }

    /// <summary>DECISION (CT, 2026-10-02): the starter may REJECT — it is withdrawing their own request and hands
    /// nobody an approval. Approve and delegate are what the starter may not do.</summary>
    [Fact]
    public async Task The_starter_in_the_seat_may_reject_their_own_request()
    {
        var seeded = await _store.SeedAsync([D(CandidateA), D(Starter)], startedBy: Starter);

        var response = await _store.RejectAsync(seeded.Task.Id, D(Starter), "starter-rejects");

        Assert.True(response.IsSuccessful, response.ReasonCode);
        Assert.Equal(WorkflowInstanceStatus.Rejected, (await _store.Instances.GetByIdAsync(seeded.Instance.Id))!.Status);
    }

    // ── 2 · a delegation names a user ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("the finance team")]
    [InlineData("position:0d000000-0000-4000-8000-000000000004")]
    [InlineData("user:not-an-id")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task A_delegation_to_anything_but_a_user_id_is_refused_and_locks_nobody_out(string target)
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);

        var response = await _store.DelegateAsync(seeded.Task.Id, D(Second), target, "bad-target-" + target.Length);

        Assert.Equal(400, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowDelegateTargetInvalid, response.ReasonCode);
        Assert.Equal(seeded.Snapshot.Id, (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!.AssignmentSnapshotId);
        Assert.True((await _store.ApproveAsync(seeded.Task.Id, D(First), "bad-target-first-" + target.Length)).IsSuccessful);
    }

    [Fact]
    public async Task A_delegation_target_is_stored_in_the_one_spelling_whatever_spelling_it_arrived_in()
    {
        var seeded = await _store.SeedAsync([D(First)]);

        var response = await _store.DelegateAsync(
            seeded.Task.Id, D(First), "user:" + NextApprover.ToString("B").ToUpperInvariant(), "prefixed-target");

        Assert.True(response.IsSuccessful, response.ReasonCode);
        var task = (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!;
        Assert.Equal(D(NextApprover), task.AssigneeRef);
        var snapshot = (await _store.Snapshots.GetByIdAsync(task.AssignmentSnapshotId!.Value))!;
        Assert.Equal([D(NextApprover)], snapshot.CandidatePrincipalIds);
    }

    [Fact]
    public async Task Delegating_to_yourself_is_refused_in_any_spelling_of_your_own_id()
    {
        var seeded = await _store.SeedAsync([D(First)]);

        // The actor arrives in one spelling, the target names the same person in another (and with the prefix).
        var response = await _store.DelegateAsync(
            seeded.Task.Id, First.ToString("D").ToUpperInvariant(), "user:" + First.ToString("B"), "self-delegate");

        Assert.Equal(409, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowDelegateSameActorInvalid, response.ReasonCode);
        Assert.Equal(seeded.Snapshot.Id, (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!.AssignmentSnapshotId);
    }

    // ── 3 · "transaction unavailable": mine, somebody else's, or really unavailable ────────────────────────────

    [Fact]
    public async Task A_commit_that_LANDED_but_was_reported_unavailable_is_a_success_not_a_lost_race()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        var executor = new CommitsThenReportsUnavailable(new PlatformTransactionExecutor(_store.Context));

        var response = await _store.ApproveAsync(seeded.Task.Id, D(Second), "landed-commit", executor);

        Assert.True(response.IsSuccessful, response.ReasonCode);
        Assert.Equal(1, executor.Calls);
        var log = Assert.Single(await _store.Logs.ListByInstanceIdAsync(seeded.Instance.Id),
            x => x.Action == WorkflowTransitionAction.Approve);
        Assert.Equal(log.Id, response.Data!.TransitionLogId);
        Assert.Equal(D(Second), (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!.ActionedBy);
    }

    [Fact]
    public async Task Unavailable_while_SOMEBODY_ELSE_decided_is_a_conflict()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        // Deterministic: the other candidate's decision commits, then this caller's transaction is "unavailable".
        var executor = new ReportsUnavailableAfter(
            () => _store.ApproveAsync(seeded.Task.Id, D(First), "the-other-decides"));

        var response = await _store.ApproveAsync(seeded.Task.Id, D(Second), "lost-to-the-other", executor);

        Assert.Equal(409, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowTransitionConflict, response.ReasonCode);
        Assert.Equal(D(First), (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!.ActionedBy);
    }

    [Fact]
    public async Task Unavailable_with_nothing_moved_stays_unavailable_and_so_does_a_failed_re_read()
    {
        var untouched = await _store.SeedAsync([D(First), D(Second)]);
        var nothing = await _store.ApproveAsync(untouched.Task.Id, D(Second), "nothing-moved",
            new ReportsUnavailableAfter(() => Task.CompletedTask));
        Assert.Equal(503, nothing.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowTransactionUnavailable, nothing.ReasonCode);

        // The re-read itself fails: the caller is not told "conflict" on a guess.
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        var tasks = new ReadsFailWhenArmed(_store.Tasks);
        var failed = await _store.ApproveAsync(seeded.Task.Id, D(Second), "re-read-fails",
            new ReportsUnavailableAfter(() => { tasks.Armed = true; return Task.CompletedTask; }), tasks);
        Assert.Equal(503, failed.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowTransactionUnavailable, failed.ReasonCode);
    }

    // ── 5 · a delegation, a request for information and an intermediate step are each ONE write ───────────────

    [Fact]
    public async Task A_delegation_whose_INSTANCE_write_loses_changes_nothing_and_the_retry_succeeds()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        var executor = new RunsBeforeTheBody(new PlatformTransactionExecutor(_store.Context),
            () => _store.BumpInstanceVersionAsync(seeded.Instance.Id));

        var lost = await _store.DelegateAsync(seeded.Task.Id, D(Second), D(NextApprover), "delegate-instance-lost", executor);

        Assert.Equal(409, lost.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowTransitionConflict, lost.ReasonCode);
        var task = (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!;
        Assert.Equal(seeded.Task.Version, task.Version);              // the task write was rolled back with it
        Assert.Equal(D(First), task.AssigneeRef);
        Assert.Equal(seeded.Snapshot.Id, task.AssignmentSnapshotId);
        Assert.Single(await _store.Snapshots.ListByInstanceIdAsync(seeded.Instance.Id));
        Assert.DoesNotContain(await _store.Logs.ListByInstanceIdAsync(seeded.Instance.Id),
            log => log.Action == WorkflowTransitionAction.Delegate);

        // Before D1-FIX1 the task WAS delegated here, without a log row, and this retry got 403.
        var retry = await _store.DelegateAsync(seeded.Task.Id, D(Second), D(NextApprover), "delegate-instance-retry");
        Assert.True(retry.IsSuccessful, retry.ReasonCode);
        Assert.Single(await _store.Logs.ListByInstanceIdAsync(seeded.Instance.Id),
            log => log.Action == WorkflowTransitionAction.Delegate);
    }

    [Theory]
    [InlineData("delegate")]
    [InlineData("requestInfo")]
    public async Task A_transition_whose_LOG_cannot_be_written_is_not_applied(string action)
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        var executor = new RunsBeforeTheBody(new PlatformTransactionExecutor(_store.Context),
            () => _store.TakeNextSequenceNumberAsync(seeded.Instance.Id));

        var response = action == "delegate"
            ? await _store.DelegateAsync(seeded.Task.Id, D(Second), D(NextApprover), "log-blocked-" + action, executor)
            : await _store.RequestInfoAsync(seeded.Task.Id, D(Second), "log-blocked-" + action, executor);

        Assert.False(response.IsSuccessful);
        var task = (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!;
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, task.Status);
        Assert.Equal(seeded.Task.Version, task.Version);
        Assert.Equal(seeded.Snapshot.Id, task.AssignmentSnapshotId);
        Assert.Single(await _store.Snapshots.ListByInstanceIdAsync(seeded.Instance.Id));
        Assert.Equal(seeded.Instance.Version, (await _store.Instances.GetByIdAsync(seeded.Instance.Id))!.Version);
    }

    [Fact]
    public async Task An_intermediate_approval_whose_log_cannot_be_written_leaves_no_next_task_and_no_next_snapshot()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)], twoSteps: true);
        var executor = new RunsBeforeTheBody(new PlatformTransactionExecutor(_store.Context),
            () => _store.TakeNextSequenceNumberAsync(seeded.Instance.Id));

        var response = await _store.ApproveAsync(seeded.Task.Id, D(Second), "step-log-blocked", executor);

        Assert.False(response.IsSuccessful);
        Assert.Single(await _store.Tasks.ListByInstanceIdAsync(seeded.Instance.Id));       // no next task
        Assert.Single(await _store.Snapshots.ListByInstanceIdAsync(seeded.Instance.Id));   // no next snapshot
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!.Status);
        Assert.Equal("step-a", (await _store.Instances.GetByIdAsync(seeded.Instance.Id))!.CurrentStep);
    }

    // ── 6 · a trusted start replays across the spelling change ─────────────────────────────────────────────────

    [Fact]
    public async Task A_trusted_start_from_one_spelling_ids_keeps_its_fingerprint_byte_for_byte()
    {
        var templateId = await _store.PublishedTemplateAsync();
        var request = TrustedRequest(templateId, "fingerprint-1", [D(Second), D(First)]);

        var started = await _store.Coordinator().StartAsync(request, TrustedClient, TrustedMaker, "corr-fp");

        Assert.True(started.IsSuccessful, started.ReasonCode);
        var instance = (await _store.Instances.GetByIdAsync(started.Data!.WorkflowInstanceId))!;
        // The formula as it stood before D1 (a frozen copy, not a call into production).
        Assert.Equal(
            FrozenFingerprint(_store.TenantContext.TenantId, templateId, instance.TemplateVersionId!.Value,
                "GlobalProduct", "GP-fingerprint-1", [D(First), D(Second)]),
            instance.StartRequestFingerprint);
        var replay = await _store.Coordinator().StartAsync(request, TrustedClient, TrustedMaker, "corr-fp-2");
        Assert.True(replay.IsSuccessful, replay.ReasonCode);
        Assert.True(replay.Data!.IsReplay);
    }

    /// <summary><paramref name="taskInOldSpellingToo"/> false = the snapshot is in the old spelling and the task
    /// names the same person in the new one: the stored proofs are compared as PEOPLE, not letter for letter.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_trusted_start_STORED_in_an_older_spelling_still_replays(bool taskInOldSpellingToo)
    {
        var templateId = await _store.PublishedTemplateAsync();
        // What a start before D1 stored for this request: the ids as the caller spelled them, ordinal-sorted.
        string[] asSent = [Second.ToString("N"), First.ToString("B").ToUpperInvariant()];
        var asStored = asSent.OrderBy(x => x, StringComparer.Ordinal).ToList();
        var started = await _store.Coordinator().StartAsync(
            TrustedRequest(templateId, "old-spelling-" + taskInOldSpellingToo, [D(First), D(Second)]), TrustedClient, TrustedMaker, "corr-old");
        Assert.True(started.IsSuccessful, started.ReasonCode);
        var instance = (await _store.Instances.GetByIdAsync(started.Data!.WorkflowInstanceId))!;
        await _store.RewriteAsStartedBeforeD1Async(started.Data, asStored,
            FrozenFingerprint(_store.TenantContext.TenantId, templateId, instance.TemplateVersionId!.Value,
                "GlobalProduct", "GP-old-spelling-" + taskInOldSpellingToo, asStored),
            assigneeRef: taskInOldSpellingToo ? asStored[0] : Guid.Parse(asStored[0]).ToString("D"));

        var replay = await _store.Coordinator().StartAsync(
            TrustedRequest(templateId, "old-spelling-" + taskInOldSpellingToo, asSent), TrustedClient, TrustedMaker, "corr-old-replay");

        Assert.True(replay.IsSuccessful, replay.ReasonCode);
        Assert.True(replay.Data!.IsReplay);
        Assert.Equal(started.Data.ApprovalTaskId, replay.Data.ApprovalTaskId);
        // …and the people stored in the old spelling can decide (the gate compares ids, not letters).
        Assert.True((await _store.ApproveAsync(started.Data.ApprovalTaskId, D(Second), "old-spelling-decides")).IsSuccessful);
    }

    // ── 7 · an unreadable snapshot: listed, but nothing to press ───────────────────────────────────────────────

    [Fact]
    public async Task A_task_whose_snapshot_cannot_be_read_is_listed_for_its_assignee_with_every_action_closed()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        await _store.PointTaskAtAMissingSnapshotAsync(seeded.Task.Id);

        var item = Assert.Single(await _store.TaskCenterItemsAsync(First), i => i.Id == seeded.Task.Id.ToString());

        Assert.All(item.Actions, action => Assert.False(action.Enabled, action.Code));
        // "is it with you" follows who may DECIDE: with a list given, AssigneeRef alone does not make it yours.
        Assert.False(item.Assignee!.IsCurrentUser);
        Assert.DoesNotContain(await _store.TaskCenterItemsAsync(Second), i => i.Id == seeded.Task.Id.ToString());
        Assert.Equal(404, (await _store.ApproveAsync(seeded.Task.Id, D(First), "dangling-snapshot")).StatusCode);
    }

    // ── 8 · a trusted start that has not finished ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    [InlineData("delegate")]
    [InlineData("requestInfo")]
    public async Task No_decision_is_taken_on_a_trusted_instance_whose_start_has_not_completed(string action)
    {
        var seeded = await _store.SeedAsync([D(First)],
            trustedCheckpoint: WorkflowStartCheckpoint.AssignmentSnapshotPersisted);

        var response = action switch
        {
            "approve" => await _store.ApproveAsync(seeded.Task.Id, D(First), "incomplete-" + action),
            "reject" => await _store.RejectAsync(seeded.Task.Id, D(First), "incomplete-" + action),
            "delegate" => await _store.DelegateAsync(seeded.Task.Id, D(First), D(NextApprover), "incomplete-" + action),
            _ => await _store.RequestInfoAsync(seeded.Task.Id, D(First), "incomplete-" + action)
        };

        Assert.Equal(409, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowStartNotCompleted, response.ReasonCode);
        Assert.Empty(await _store.Logs.ListByInstanceIdAsync(seeded.Instance.Id)); // sequence number 1 is still free

        // A COMPLETED trusted start decides as usual.
        var completed = await _store.SeedAsync([D(First)], trustedCheckpoint: WorkflowStartCheckpoint.Completed);
        Assert.True((await _store.ApproveAsync(completed.Task.Id, D(First), "completed-" + action)).IsSuccessful);
    }

    // ── 9 · the empty id is nobody ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_empty_id_matches_nothing_not_even_itself()
    {
        var empty = Guid.Empty.ToString("D");
        Assert.False(WorkflowTaskTransitionSupport.PrincipalEquals(empty, empty));
        Assert.False(WorkflowTaskTransitionSupport.PrincipalEquals(Guid.Empty.ToString("N"), empty));

        var seeded = await _store.SeedAsync([empty, D(First)]);
        Assert.Equal(403, (await _store.ApproveAsync(seeded.Task.Id, empty, "empty-actor")).StatusCode);
    }

    // ── 11 · races between different decisions ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_approval_and_a_rejection_at_once_exactly_one_wins()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        var barrier = new TwoCallerBarrier(new PlatformTransactionExecutor(_store.Context));

        var responses = await Task.WhenAll(
            _store.ApproveAsync(seeded.Task.Id, D(First), "race-approve", barrier),
            _store.RejectAsync(seeded.Task.Id, D(Second), "race-reject", barrier));

        Assert.Single(responses, r => r.IsSuccessful);
        var loser = Assert.Single(responses, r => !r.IsSuccessful);
        Assert.Equal(409, loser.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowTransitionConflict, loser.ReasonCode);
        Assert.Single(await _store.Logs.ListByInstanceIdAsync(seeded.Instance.Id),
            x => x.Action is WorkflowTransitionAction.Approve or WorkflowTransitionAction.Reject);
        Assert.Equal(1, await _store.CompletionEventsAsync(seeded.Instance.Id));
        var instance = (await _store.Instances.GetByIdAsync(seeded.Instance.Id))!;
        var task = (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!;
        Assert.Equal(instance.Status == WorkflowInstanceStatus.Completed ? ApprovalTaskStatus.Approved : ApprovalTaskStatus.Rejected,
            task.Status);
    }

    [Fact]
    public async Task A_decision_and_a_delegation_at_once_exactly_one_wins()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        var barrier = new TwoCallerBarrier(new PlatformTransactionExecutor(_store.Context));

        var responses = await Task.WhenAll(
            _store.ApproveAsync(seeded.Task.Id, D(First), "race-decide", barrier),
            _store.DelegateAsync(seeded.Task.Id, D(Second), D(NextApprover), "race-delegate", barrier));

        Assert.Single(responses, r => r.IsSuccessful);
        var loser = Assert.Single(responses, r => !r.IsSuccessful);
        Assert.Equal(409, loser.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowTransitionConflict, loser.ReasonCode);
        var task = (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!;
        var snapshots = await _store.Snapshots.ListByInstanceIdAsync(seeded.Instance.Id);
        if (task.Status == ApprovalTaskStatus.Approved)
        {
            Assert.Single(snapshots); // the losing delegation left no snapshot behind
        }
        else
        {
            Assert.Equal(D(NextApprover), task.AssigneeRef);
            Assert.Equal(2, snapshots.Count);
            Assert.Equal(0, await _store.CompletionEventsAsync(seeded.Instance.Id));
        }
    }

    [Fact]
    public async Task A_decision_and_an_escalation_at_once_exactly_one_wins()
    {
        var seeded = await _store.SeedAsync([D(First)], dueAt: DateTimeOffset.UtcNow.AddMinutes(-5));
        await _store.EscalationRuleAsync(seeded.Instance.TemplateId, [D(CandidateA), D(CandidateB)]);
        var barrier = new TwoCallerBarrier(new PlatformTransactionExecutor(_store.Context));

        var decision = _store.ApproveAsync(seeded.Task.Id, D(First), "race-vs-escalation", barrier);
        var escalation = _store.RunEscalationsAsync(barrier);
        await Task.WhenAll(decision, escalation);

        var escalated = (await escalation).Results.Single(r => r.ApprovalTaskId == seeded.Task.Id);
        var decided = (await decision).IsSuccessful;
        var escalationWon = escalated.ReasonCode == WorkflowReasonCodes.WorkflowEscalationProcessed;
        Assert.True(decided ^ escalationWon, $"decision={(await decision).ReasonCode} escalation={escalated.ReasonCode}");
        var task = (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!;
        var snapshots = await _store.Snapshots.ListByInstanceIdAsync(seeded.Instance.Id);
        if (decided)
        {
            Assert.Equal(ApprovalTaskStatus.Approved, task.Status);
            Assert.Equal(WorkflowReasonCodes.WorkflowTransitionConflict, escalated.ReasonCode);
            Assert.Single(snapshots); // the losing escalation left no orphan snapshot (it is in the transaction now)
        }
        else
        {
            Assert.Equal(ApprovalTaskStatus.Escalated, task.Status);
            Assert.Equal(409, (await decision).StatusCode);
            Assert.Equal(2, snapshots.Count);
        }
    }

    // ── 11 · after a REAL escalation ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task After_a_real_escalation_the_old_candidates_no_longer_see_the_task_and_the_new_ones_do()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)], dueAt: DateTimeOffset.UtcNow.AddMinutes(-5));
        await _store.EscalationRuleAsync(seeded.Instance.TemplateId, [D(CandidateA), D(CandidateB)]);

        var run = await _store.RunEscalationsAsync();
        Assert.Equal(WorkflowReasonCodes.WorkflowEscalationProcessed,
            run.Results.Single(r => r.ApprovalTaskId == seeded.Task.Id).ReasonCode);

        var taskId = seeded.Task.Id.ToString();
        foreach (var old in new[] { First, Second })
        {
            Assert.DoesNotContain(await _store.TaskCenterItemsAsync(old), i => i.Id == taskId);
            Assert.DoesNotContain(await _store.MyWorkflowTasksAsync(old), t => t.Id == seeded.Task.Id);
        }

        foreach (var now in new[] { CandidateA, CandidateB })
        {
            Assert.Contains(await _store.TaskCenterItemsAsync(now), i => i.Id == taskId);
            Assert.Contains(await _store.MyWorkflowTasksAsync(now), t => t.Id == seeded.Task.Id);
        }

        Assert.Equal(WorkflowInstanceStatus.Escalated, (await _store.Instances.GetByIdAsync(seeded.Instance.Id))!.Status);
        // The escalation log row is there, written with the escalation.
        Assert.Single(await _store.Logs.ListByInstanceIdAsync(seeded.Instance.Id), l => l.Action == WorkflowTransitionAction.Escalate);
    }

    [Theory]
    [InlineData("delegate")]
    [InlineData("requestInfo")]
    [InlineData("approveStep")]
    public async Task After_a_transition_on_an_escalated_task_the_INSTANCE_is_active_again(string action)
    {
        var seeded = await _store.SeedAsync([D(First)], twoSteps: action == "approveStep", escalatedTo: [D(Second)]);
        Assert.Equal(WorkflowInstanceStatus.Escalated, seeded.Instance.Status);

        var response = action switch
        {
            "delegate" => await _store.DelegateAsync(seeded.Task.Id, D(Second), D(NextApprover), "escalated-instance-" + action),
            "requestInfo" => await _store.RequestInfoAsync(seeded.Task.Id, D(Second), "escalated-instance-" + action),
            _ => await _store.ApproveAsync(seeded.Task.Id, D(Second), "escalated-instance-" + action)
        };

        Assert.True(response.IsSuccessful, response.ReasonCode);
        Assert.Equal(WorkflowInstanceStatus.Active, (await _store.Instances.GetByIdAsync(seeded.Instance.Id))!.Status);
    }

    // ── harness (FIX1) ─────────────────────────────────────────────────────────────────────────────────────────

    private static readonly Guid TrustedClient = Guid.Parse("7c000000-0000-4000-8000-000000000001");
    private static readonly Guid TrustedMaker = Guid.Parse("7d000000-0000-4000-8000-000000000002");

    private static TrustedWorkflowStartRequest TrustedRequest(Guid templateId, string key, IReadOnlyList<string> candidates) =>
        new(templateId, null, "GlobalProduct", "GP-" + key, null, candidates, "SUBMIT", key, false, false, null);

    /// <summary>The trusted-start fingerprint EXACTLY as computed before D1 (WorkflowInstanceStartCoordinator at
    /// 074c25cc5). A frozen copy on purpose: it is the contract in-flight idempotency keys were stored under.</summary>
    private static string FrozenFingerprint(
        Guid tenantId, Guid templateId, Guid versionId, string objectType, string objectId, IReadOnlyList<string> candidates)
    {
        static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        var canonical = string.Join('\n',
            tenantId.ToString("D"),
            TrustedClient.ToString("D"),
            TrustedMaker.ToString("D"),
            templateId.ToString("D"),
            versionId.ToString("D"),
            Encode(objectType),
            Encode(objectId),
            Encode($"{objectType}|{objectId}"),
            string.Join(',', candidates.Select(Encode)),
            Encode("SUBMIT"),
            "0",
            "0",
            string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private sealed partial class Store
    {
        public WorkflowInstanceStartCoordinator Coordinator() =>
            new(Templates, Versions, Instances, Tasks, Snapshots, Logs, TenantContext);

        public async Task<Guid> PublishedTemplateAsync()
        {
            var template = await Templates.CreateAsync(new WorkflowTemplate
            {
                TenantId = TenantContext.TenantId,
                TemplateCode = "D1F-" + Guid.NewGuid().ToString("N")[..10],
                Name = "Trusted start",
                Status = WorkflowTemplateStatus.Published
            });
            var version = await Versions.CreateAsync(new WorkflowTemplateVersion
            {
                TenantId = TenantContext.TenantId,
                TemplateId = template.Id,
                VersionNumber = 1,
                DefinitionJson = "{}",
                SchemaVersion = "1.0",
                ExpressionVersion = "1.0",
                Status = WorkflowTemplateVersionStatus.Published,
                IsImmutable = true
            });
            template.ActivePublishedVersionId = version.Id;
            template.CurrentVersionId = version.Id;
            Assert.True(await Templates.UpdateAsync(template, template.Version));
            return template.Id;
        }

        /// <summary>Rewrites a finished trusted start into what a start BEFORE the one-spelling rule stored: the
        /// candidate ids in the caller's spelling, and the fingerprint computed over them.</summary>
        public async Task RewriteAsStartedBeforeD1Async(
            TrustedWorkflowStartResult started, IReadOnlyList<string> storedCandidates, string storedFingerprint,
            string assigneeRef)
        {
            await Context.Database.GetCollection<RuntimeAssignmentSnapshot>(PlatformCollections.WorkflowRuntimeAssignmentSnapshots)
                .UpdateOneAsync(s => s.Id == started.AssignmentSnapshotId,
                    Builders<RuntimeAssignmentSnapshot>.Update
                        .Set(s => s.ResolvedPrincipalId, storedCandidates[0])
                        .Set(s => s.CandidatePrincipalIds, storedCandidates.ToList()));
            await Context.Database.GetCollection<ApprovalTask>(PlatformCollections.ApprovalTasks)
                .UpdateOneAsync(t => t.Id == started.ApprovalTaskId,
                    Builders<ApprovalTask>.Update.Set(t => t.AssigneeRef, assigneeRef));
            await Context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances)
                .UpdateOneAsync(i => i.Id == started.WorkflowInstanceId,
                    Builders<WorkflowInstance>.Update.Set(i => i.StartRequestFingerprint, storedFingerprint));
        }

        public Task BumpInstanceVersionAsync(Guid instanceId) =>
            Context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances)
                .UpdateOneAsync(i => i.Id == instanceId, Builders<WorkflowInstance>.Update.Inc(i => i.Version, 1));

        /// <summary>Somebody else takes the instance's next transition-log sequence number: the transition about to be
        /// written hits the unique (instance, SequenceNo) index.</summary>
        public async Task TakeNextSequenceNumberAsync(Guid instanceId)
        {
            await Logs.CreateAsync(new WorkflowTransitionLog
            {
                TenantId = TenantContext.TenantId,
                WorkflowInstanceId = instanceId,
                Action = WorkflowTransitionAction.Start,
                ActorId = "sequence-taker",
                ActorRef = "sequence-taker",
                IdempotencyKey = "taken-" + Guid.NewGuid().ToString("N"),
                SequenceNo = await Logs.GetLatestSequenceNoAsync(instanceId) + 1
            });
        }

        public Task PointTaskAtAMissingSnapshotAsync(Guid taskId) =>
            Context.Database.GetCollection<ApprovalTask>(PlatformCollections.ApprovalTasks)
                .UpdateOneAsync(t => t.Id == taskId,
                    Builders<ApprovalTask>.Update.Set(t => t.AssignmentSnapshotId, Guid.NewGuid()));

        public Task EscalationRuleAsync(Guid templateId, IReadOnlyList<string> escalateTo) =>
            new SlaEscalationRuleRepository(Context, TenantContext).CreateAsync(new SlaEscalationRule
            {
                TenantId = TenantContext.TenantId,
                TemplateId = templateId,
                StageCode = "stage-1",
                StepCode = "step-a",
                DueInMinutes = 10,
                EscalateAfterMinutes = 10, // overdue = escalated
                EscalationPrincipalIds = escalateTo.ToList()
            });

        public async Task<RunWorkflowEscalationsResponse> RunEscalationsAsync(IPlatformTransactionExecutor? executor = null)
        {
            var response = await new RunWorkflowEscalationsHandler(
                    Tasks, Instances, new SlaEscalationRuleRepository(Context, TenantContext), Logs, TimeProvider.System,
                    assignmentSnapshotRepository: Snapshots,
                    transactions: executor ?? new PlatformTransactionExecutor(Context),
                    events: new MongoIntentWriter(Context),
                    templates: Templates)
                .Handle(new RunWorkflowEscalationsCommand(new RunWorkflowEscalationsRequest(null, 500, null), "corr-escalation"), default);
            Assert.True(response.IsSuccessful, response.ReasonCode);
            return response.Data!;
        }
    }

    /// <summary>The body commits, then the caller is told the transaction was unavailable (an unknown commit result).</summary>
    private sealed class CommitsThenReportsUnavailable(IPlatformTransactionExecutor inner) : IPlatformTransactionExecutor
    {
        public int Calls { get; private set; }

        public async Task<T> ExecuteAsync<T>(
            Func<IPlatformTransactionSession, CancellationToken, Task<T>> body, CancellationToken cancellationToken = default)
        {
            Calls++;
            await inner.ExecuteAsync(body, cancellationToken);
            throw new PlatformTransactionUnavailableException("commit result unknown", new InvalidOperationException());
        }
    }

    /// <summary>The body never runs: something else happens first, then the transaction is "unavailable".</summary>
    private sealed class ReportsUnavailableAfter(Func<Task> before) : IPlatformTransactionExecutor
    {
        public async Task<T> ExecuteAsync<T>(
            Func<IPlatformTransactionSession, CancellationToken, Task<T>> body, CancellationToken cancellationToken = default)
        {
            await before();
            throw new PlatformTransactionUnavailableException("unavailable", new InvalidOperationException());
        }
    }

    private sealed class RunsBeforeTheBody(IPlatformTransactionExecutor inner, Func<Task> before) : IPlatformTransactionExecutor
    {
        public async Task<T> ExecuteAsync<T>(
            Func<IPlatformTransactionSession, CancellationToken, Task<T>> body, CancellationToken cancellationToken = default)
        {
            await before();
            return await inner.ExecuteAsync(body, cancellationToken);
        }
    }

    /// <summary>The real repository until armed; then every by-id read throws (the re-read after "unavailable").</summary>
    private sealed class ReadsFailWhenArmed(IApprovalTaskRepository inner) : IApprovalTaskRepository
    {
        public bool Armed { get; set; }

        public Task<ApprovalTask?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Armed ? throw new InvalidOperationException("the store cannot be read") : inner.GetByIdAsync(id, ct);

        public Task<ApprovalTask> CreateAsync(ApprovalTask task, CancellationToken ct = default) => inner.CreateAsync(task, ct);
        public Task<ApprovalTask?> GetFirstByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            inner.GetFirstByInstanceIdAsync(workflowInstanceId, ct);
        public Task<ApprovalTask?> GetActiveByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            inner.GetActiveByInstanceIdAsync(workflowInstanceId, ct);
        public Task<IReadOnlyList<ApprovalTask>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            inner.ListByInstanceIdAsync(workflowInstanceId, ct);
        public Task<IReadOnlyList<ApprovalTask>> GetAllForTenantAsync(CancellationToken ct = default) => inner.GetAllForTenantAsync(ct);
        public Task<bool> UpdateAsync(ApprovalTask task, int expectedVersion, CancellationToken ct = default) =>
            inner.UpdateAsync(task, expectedVersion, ct);
    }
}

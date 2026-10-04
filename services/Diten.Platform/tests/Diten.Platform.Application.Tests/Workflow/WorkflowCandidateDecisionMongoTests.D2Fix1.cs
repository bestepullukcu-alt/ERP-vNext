using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Workflow.Services;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

/// <summary>
/// WP-WF-ENGINE-CANDIDATES-01 · D2 fix round 1 (P-2026-10-03-D2-FIX1). Real replica set, production handlers.
/// </summary>
public sealed partial class WorkflowCandidateDecisionMongoTests
{
    // ── A1 · Tasks' own withdraw calls off an approval that was ever escalated ──────────────────────────────────

    [Fact]
    public async Task Tasks_withdraw_after_an_escalation_and_a_delegation_cancels_the_approval_and_leaves_no_orphan()
    {
        // The sequence the review found: escalated (level 1), delegated back to waiting, then the owner switches the
        // approval off. Before the fix the cancel was refused (ever escalated), nobody read the answer, and the task
        // dropped its link to an approval that stayed decidable.
        var seeded = await _store.SeedAsync([D(First)], escalatedTo: [D(Second)]);
        Assert.True((await _store.DelegateAsync(seeded.Task.Id, D(Second), D(NextApprover), "a1-delegate")).IsSuccessful);
        var before = (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!;
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, before.Status);
        Assert.True(before.EscalationLevel > 0); // non-vacuity: the public cancel refuses exactly this task

        var withdrawn = await _store.TaskApprovals().CancelApprovalAsync(
            TaskFor(seeded.Instance.Id),
            default);

        Assert.True(withdrawn);
        Assert.Equal(ApprovalTaskStatus.Cancelled, (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!.Status);
        Assert.Equal(WorkflowInstanceStatus.Cancelled, (await _store.Instances.GetByIdAsync(seeded.Instance.Id))!.Status);
        Assert.DoesNotContain(await _store.Tasks.ListByInstanceIdAsync(seeded.Instance.Id),
            t => t.Status is ApprovalTaskStatus.WaitingApproval or ApprovalTaskStatus.WaitingEvidence or ApprovalTaskStatus.Escalated);
    }

    [Fact]
    public async Task Tasks_withdraw_of_a_still_escalated_approval_cancels_it()
    {
        var seeded = await _store.SeedAsync([D(First)], escalatedTo: [D(Second)]);
        Assert.Equal(ApprovalTaskStatus.Escalated, seeded.Task.Status);

        var withdrawn = await _store.TaskApprovals().CancelApprovalAsync(
            TaskFor(seeded.Instance.Id),
            default);

        Assert.True(withdrawn);
        Assert.Equal(ApprovalTaskStatus.Cancelled, (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!.Status);
    }

    [Fact]
    public async Task Tasks_withdraw_of_an_approval_decided_meanwhile_reports_that_it_could_not_withdraw()
    {
        // Decided between the read and the cancel: the open list is read before, the task is closed when the cancel
        // runs — the engine refuses, and the answer is now READ (false), so the caller keeps its link.
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        var service = _store.TaskApprovals(beforeCancel: () => _store.ApproveAsync(seeded.Task.Id, D(Second), "a1-race"));

        var withdrawn = await service.CancelApprovalAsync(
            TaskFor(seeded.Instance.Id),
            default);

        Assert.False(withdrawn);
        // Decided (the engine closes a final approval as Completed), not cancelled: the withdraw did not happen.
        Assert.Equal(WorkflowInstanceStatus.Completed, (await _store.Instances.GetByIdAsync(seeded.Instance.Id))!.Status);
    }

    // ── A2 · a version-1 replay after the seat changed hands is the same request ───────────────────────────────

    [Fact]
    public async Task A_version_1_replay_after_the_seat_changed_hands_returns_the_same_instance()
    {
        var templateId = await _store.PublishedTemplateAsync();
        var seat = await _store.SeatHeldByAsync(First);
        string[] declared = ["position:" + seat.Position.ToString("D")];
        var started = await _store.Coordinator(withPositions: true).StartAsync(
            TrustedRequest(templateId, "v1-seat", declared), TrustedClient, TrustedMaker, "corr-v1");
        Assert.True(started.IsSuccessful, started.ReasonCode);
        var instance = (await _store.Instances.GetByIdAsync(started.Data!.WorkflowInstanceId))!;
        // What a start before D2 stored: the version-1 fingerprint over the list it RESOLVED (First, in the snapshot).
        await _store.RewriteAsStartedBeforeD1Async(started.Data, [D(First)],
            FrozenFingerprint(_store.TenantContext.TenantId, templateId, instance.TemplateVersionId!.Value,
                "GlobalProduct", "GP-v1-seat", [D(First)]),
            assigneeRef: D(First));

        await _store.HandSeatOverAsync(seat, Second);
        var replay = await _store.Coordinator(withPositions: true).StartAsync(
            TrustedRequest(templateId, "v1-seat", declared), TrustedClient, TrustedMaker, "corr-v1-2");

        Assert.True(replay.IsSuccessful, replay.ReasonCode);
        Assert.True(replay.Data!.IsReplay);
        Assert.Equal(started.Data.WorkflowInstanceId, replay.Data.WorkflowInstanceId);
    }

    // ── A3 · a reserved key without its snapshot trusts what its first call persisted ────────────────────────────

    [Fact]
    public async Task A_start_cut_before_its_snapshot_replays_after_the_seat_changed_hands()
    {
        var templateId = await _store.PublishedTemplateAsync();
        var seat = await _store.SeatHeldByAsync(First);
        string[] declared = ["position:" + seat.Position.ToString("D")];
        var started = await _store.Coordinator(withPositions: true).StartAsync(
            TrustedRequest(templateId, "cut-snap", declared), TrustedClient, TrustedMaker, "corr-cut");
        Assert.True(started.IsSuccessful, started.ReasonCode);
        await _store.CutStartAsync(started.Data!, WorkflowStartCheckpoint.TaskPersisted);

        await _store.HandSeatOverAsync(seat, Second);
        var replay = await _store.Coordinator(withPositions: true).StartAsync(
            TrustedRequest(templateId, "cut-snap", declared), TrustedClient, TrustedMaker, "corr-cut-2");

        Assert.True(replay.IsSuccessful, replay.ReasonCode);
        Assert.Equal(started.Data!.WorkflowInstanceId, replay.Data!.WorkflowInstanceId);
        // The task the first call wrote is kept, and the snapshot written now names its person first.
        Assert.Equal(D(First), (await _store.Tasks.GetByIdAsync(started.Data.ApprovalTaskId))!.AssigneeRef);
        Assert.Equal(D(First), (await _store.Snapshots.GetByIdAsync(started.Data.AssignmentSnapshotId))!.CandidatePrincipalIds[0]);
        Assert.Equal(WorkflowInstanceStatus.Active, (await _store.Instances.GetByIdAsync(started.Data.WorkflowInstanceId))!.Status);
    }

    [Fact]
    public async Task A_reservation_nobody_can_approve_any_more_is_closed_not_left_pending()
    {
        var templateId = await _store.PublishedTemplateAsync();
        var seat = await _store.SeatHeldByAsync(First);
        string[] declared = ["position:" + seat.Position.ToString("D")];
        var started = await _store.Coordinator(withPositions: true).StartAsync(
            TrustedRequest(templateId, "cut-res", declared), TrustedClient, TrustedMaker, "corr-res");
        Assert.True(started.IsSuccessful, started.ReasonCode);
        await _store.CutStartAsync(started.Data!, WorkflowStartCheckpoint.Reserved);

        // The seat now belongs to the maker: nobody but the person starting it could approve.
        await _store.HandSeatOverAsync(seat, TrustedMaker);
        var replay = await _store.Coordinator(withPositions: true).StartAsync(
            TrustedRequest(templateId, "cut-res", declared), TrustedClient, TrustedMaker, "corr-res-2");

        Assert.Equal(400, replay.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowNoEligibleApprover, replay.ReasonCode);
        var closed = (await _store.Instances.GetByIdAsync(started.Data!.WorkflowInstanceId))!;
        Assert.Equal(WorkflowInstanceStatus.Cancelled, closed.Status);   // not left Pending

        // The seat comes back: the key still answers the same way, and the closed reservation is never revived.
        await _store.HandSeatOverAsync(await _store.SeatAsync(seat.Position), First);
        var again = await _store.Coordinator(withPositions: true).StartAsync(
            TrustedRequest(templateId, "cut-res", declared), TrustedClient, TrustedMaker, "corr-res-3");
        Assert.Equal(WorkflowReasonCodes.WorkflowNoEligibleApprover, again.ReasonCode);
        Assert.Equal(WorkflowInstanceStatus.Cancelled, (await _store.Instances.GetByIdAsync(closed.Id))!.Status);
    }

    // ── A4 · a parked escalation: one warning, never read before the unparked ones ────────────────────────────────

    [Fact]
    public async Task Two_sweeps_over_an_escalation_nobody_may_take_warn_once_and_park_the_task()
    {
        var seeded = await _store.SeedAsync([D(First)], dueAt: DateTimeOffset.UtcNow.AddMinutes(-5), startedBy: CandidateA);
        await _store.EscalationRuleAsync(seeded.Instance.TemplateId, [D(CandidateA)]);
        var logger = new CountingLogger();

        var first = await _store.SweepAsync(logger: logger);
        var parkedAt = DateTime.UtcNow;
        var second = await _store.SweepAsync(logger: logger);
        // D2-FIX2 item 4 — the second sweep does not read it at all (it rests until its look time); looked at again
        // after that, it is still nobody's to take, and it is still not warned about twice.
        var later = await _store.SweepAtAsync(RunWorkflowEscalationsHandler.InconclusiveRecheckInterval + TimeSpan.FromMinutes(1), logger: logger);

        Assert.Equal(WorkflowReasonCodes.WorkflowNoEligibleApprover, first.Results.Single(r => r.ApprovalTaskId == seeded.Task.Id).ReasonCode);
        Assert.DoesNotContain(second.Results, r => r.ApprovalTaskId == seeded.Task.Id);
        Assert.Equal(WorkflowReasonCodes.WorkflowNoEligibleApprover, later.Results.Single(r => r.ApprovalTaskId == seeded.Task.Id).ReasonCode);
        Assert.Equal(1, logger.WarningsAbout(seeded.Task.Id));
        var task = (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!;
        Assert.Equal(WorkflowReasonCodes.WorkflowNoEligibleApprover, task.LastEscalationReasonCode);
        // Parking gives it its next look: a quarter of an hour on (this rule has no timeout to come sooner).
        Assert.NotNull(task.EscalationNextLookAtUtc);
        Assert.True(task.EscalationNextLookAtUtc > parkedAt.AddMinutes(14), task.EscalationNextLookAtUtc?.ToString("O"));
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, task.Status);
    }

    [Fact]
    public async Task More_than_a_page_of_timed_out_tasks_does_not_starve_a_newly_overdue_one()
    {
        await _store.InsertOverdueTasksAsync(101, ApprovalTaskStatus.TimedOut, parked: false, DateTimeOffset.UtcNow.AddDays(-30));
        var seeded = await _store.SeedAsync([D(First)], dueAt: DateTimeOffset.UtcNow.AddMinutes(-5));
        await _store.EscalationRuleAsync(seeded.Instance.TemplateId, [D(CandidateB)]);

        var run = await _store.SweepAsync(maxItems: 100);

        Assert.Contains(run.Results, r => r.ApprovalTaskId == seeded.Task.Id && r.ReasonCode == WorkflowReasonCodes.WorkflowEscalationProcessed);
        Assert.DoesNotContain(run.Results, r => r.NewTaskStatus == ApprovalTaskStatus.TimedOut.ToString() && r.ReasonCode == WorkflowReasonCodes.WorkflowTaskInvalidState);
    }

    [Fact]
    public async Task More_than_a_page_of_parked_tasks_does_not_starve_a_newly_overdue_one()
    {
        await _store.InsertOverdueTasksAsync(101, ApprovalTaskStatus.WaitingApproval, parked: true, DateTimeOffset.UtcNow.AddDays(-30));
        var seeded = await _store.SeedAsync([D(First)], dueAt: DateTimeOffset.UtcNow.AddMinutes(-5));
        await _store.EscalationRuleAsync(seeded.Instance.TemplateId, [D(CandidateB)]);

        var run = await _store.SweepAsync(maxItems: 100);

        Assert.Contains(run.Results, r => r.ApprovalTaskId == seeded.Task.Id && r.ReasonCode == WorkflowReasonCodes.WorkflowEscalationProcessed);
    }

    [Fact]
    public async Task The_overdue_read_is_oldest_first_whatever_order_the_tasks_were_written_in()
    {
        var now = DateTimeOffset.UtcNow;
        // Written newest first: an order the read must not follow.
        var newest = await _store.InsertOverdueTasksAsync(1, ApprovalTaskStatus.WaitingApproval, parked: false, now.AddHours(-1));
        var middle = await _store.InsertOverdueTasksAsync(1, ApprovalTaskStatus.WaitingApproval, parked: false, now.AddHours(-2));
        var oldest = await _store.InsertOverdueTasksAsync(1, ApprovalTaskStatus.WaitingApproval, parked: false, now.AddHours(-3));

        var read = await _store.Tasks.ListOverdueTasksAsync(now, 3);

        Assert.Equal([oldest[0], middle[0], newest[0]], read.Select(t => t.Id).ToList());
    }

    // ── A5 · small ones ───────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_twin_calls_terminal_commit_is_reported_as_a_replay()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        var executor = new ReportsUnavailableAfter(
            () => _store.RejectWithCommentAsync(seeded.Task.Id, D(Second), "twin-replay", "the reason"));

        var response = await _store.RejectWithCommentAsync(seeded.Task.Id, D(Second), "twin-replay", "the reason", executor);

        Assert.True(response.IsSuccessful, response.ReasonCode);
        Assert.True(response.Data!.IsIdempotent);
    }

    [Fact]
    public async Task A_twin_calls_next_step_commit_is_reported_as_a_replay()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)], twoSteps: true);
        var executor = new ReportsUnavailableAfter(() => _store.ApproveAsync(seeded.Task.Id, D(Second), "twin-step"));

        var response = await _store.ApproveAsync(seeded.Task.Id, D(Second), "twin-step", executor);

        Assert.True(response.IsSuccessful, response.ReasonCode);
        Assert.Equal(WorkflowInstanceStatus.Active.ToString(), response.Data!.NewInstanceStatus); // non-vacuity: the step path
        Assert.True(response.Data.IsIdempotent);
    }

    [Fact]
    public async Task A_stored_snapshot_with_nobody_in_it_is_a_recovery_conflict_not_a_crash()
    {
        var templateId = await _store.PublishedTemplateAsync();
        var started = await _store.Coordinator().StartAsync(
            TrustedRequest(templateId, "empty-snap", [D(First)]), TrustedClient, TrustedMaker, "corr-empty");
        Assert.True(started.IsSuccessful, started.ReasonCode);
        await _store.Context.Database.GetCollection<RuntimeAssignmentSnapshot>(PlatformCollections.WorkflowRuntimeAssignmentSnapshots)
            .UpdateOneAsync(x => x.Id == started.Data!.AssignmentSnapshotId,
                Builders<RuntimeAssignmentSnapshot>.Update.Set(x => x.CandidatePrincipalIds, new List<string>()));

        var replay = await _store.Coordinator().StartAsync(
            TrustedRequest(templateId, "empty-snap", [D(First)]), TrustedClient, TrustedMaker, "corr-empty-2");

        Assert.Equal(409, replay.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowStartRecoveryConflict, replay.ReasonCode);
    }

    [Fact]
    public async Task An_instance_from_before_D2_shows_its_starter_a_closed_approve_and_delegate()
    {
        // StartedByUserId missing; StartedBy is the starter's id as text — the gate's IsStarter reads it.
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        await _store.MakeLegacyStarterAsync(seeded.Instance.Id, First.ToString("D"));

        var item = Assert.Single(await _store.TaskCenterItemsAsync(First), i => i.Id == seeded.Task.Id.ToString());

        var approve = item.Actions.Single(a => a.Code == "approve");
        Assert.False(approve.Enabled);
        Assert.Equal(WorkAggregationReasonCodes.SelfApprovalNotAllowed, approve.DisabledReasonCode);
        Assert.False(item.Actions.Single(a => a.Code == "delegate").Enabled);
    }

    [Fact]
    public async Task An_instance_from_before_D2_keeps_its_starter_out_of_the_delegation_targets()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        await _store.MakeLegacyStarterAsync(seeded.Instance.Id, NextApprover.ToString("D"));

        var item = Assert.Single(await _store.TaskCenterItemsAsync(Second), i => i.Id == seeded.Task.Id.ToString());

        var delegation = item.Actions.Single(a => a.Code == "delegate");
        Assert.True(delegation.Enabled); // non-vacuity: Second is not the starter
        Assert.Contains(NextApprover.ToString(), delegation.ExcludedTargetPrincipalIds!);
    }

    // ── the rules that had no test of their own ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_trusted_cancel_refuses_an_approval_that_escalated_inside_its_transaction()
    {
        var (instance, task) = await _store.TrustedOpenApprovalAsync();
        // Escalated after the first read and before the transaction reads (no version change): only the in-transaction
        // check can see it.
        var executor = new RunsBeforeTheBody(new PlatformTransactionExecutor(_store.Context),
            () => _store.SetEscalationLevelAsync(task.Id, 1));

        var cancel = await _store.TrustedCancelAsync(instance, task, executor, "t-in-tx");

        Assert.Equal(409, cancel.StatusCode);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, (await _store.Tasks.GetByIdAsync(task.Id))!.Status);
    }

    [Fact]
    public async Task The_trusted_cancel_refuses_an_approval_that_was_escalated_before_the_call()
    {
        var (instance, task) = await _store.TrustedOpenApprovalAsync();
        await _store.SetEscalationLevelAsync(task.Id, 1);
        task = (await _store.Tasks.GetByIdAsync(task.Id))!;
        // The transaction's own read is made to see it un-escalated: only the check before the transaction can refuse.
        var executor = new RunsBeforeTheBody(new PlatformTransactionExecutor(_store.Context),
            () => _store.SetEscalationLevelAsync(task.Id, 0));

        var cancel = await _store.TrustedCancelAsync(instance, task, executor, "t-before");

        Assert.Equal(409, cancel.StatusCode);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, (await _store.Tasks.GetByIdAsync(task.Id))!.Status);
    }

    [Fact]
    public async Task The_trusted_cancel_of_an_approval_never_escalated_goes_through()
    {
        // Non-vacuity for the two tests above: the same call, nothing escalated.
        var (instance, task) = await _store.TrustedOpenApprovalAsync();

        var cancel = await _store.TrustedCancelAsync(instance, task, new PlatformTransactionExecutor(_store.Context), "t-plain");

        Assert.True(cancel.IsSuccessful, cancel.ReasonCode);
    }

    [Fact]
    public async Task Two_timeout_runs_sharing_the_system_actor_and_key_report_the_second_as_a_replay()
    {
        var seeded = await _store.SeedAsync([D(First)], dueAt: DateTimeOffset.UtcNow.AddMinutes(-60));
        await _store.EscalationRuleAsync(seeded.Instance.TemplateId, [D(CandidateA)], timeoutAfterMinutes: 10);
        RunWorkflowEscalationsResponse? other = null;
        var executor = new ReportsUnavailableAfter(async () => other = await _store.RunEscalationsAsync());

        var mine = await _store.RunEscalationsAsync(executor);

        Assert.NotNull(other);
        Assert.Equal(1, other!.TimedOutCount);
        var replayed = mine.Results.Single(r => r.ApprovalTaskId == seeded.Task.Id);
        Assert.Equal(WorkflowTransitionAction.Timeout.ToString(), replayed.Action);
        Assert.True(replayed.IsIdempotent);
    }

    [Theory]
    [InlineData("ToState")]
    [InlineData("ReasonCode")]
    [InlineData("EvidenceRef")]
    public async Task Same_key_and_actor_whose_stored_row_differs_in_one_field_is_not_my_commit(string field)
    {
        var response = await _store.ApproveAgainstACraftedTwinAsync("twin-" + field, field);

        Assert.Equal(409, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowTransitionConflict, response.ReasonCode);
    }

    [Fact]
    public async Task Same_key_and_actor_whose_stored_row_is_identical_is_my_commit()
    {
        // Non-vacuity for the theory above: the same crafted twin, nothing changed.
        var response = await _store.ApproveAgainstACraftedTwinAsync("twin-same", field: null);

        Assert.True(response.IsSuccessful, response.ReasonCode);
    }

    [Fact]
    public void The_declared_candidates_do_not_depend_on_the_order_they_were_sent_in()
    {
        // The raw list is sorted BEFORE the one spelling is applied: "{0a…}" sorts after "0b…" as written, before it once
        // canonical. The declared list is ordered again after canonicalisation, so both spellings give one order.
        IReadOnlyList<string> expected = [D(First), D(Second)];
        Assert.Equal(expected, WorkflowInstanceStartCoordinator.DeclaredCandidates([First.ToString("B"), D(Second)]));
        Assert.Equal(expected, WorkflowInstanceStartCoordinator.DeclaredCandidates([D(Second), D(First)]));
    }

    [Fact]
    public void The_declared_candidates_read_the_prefix_in_any_case()
    {
        Assert.Equal(
            WorkflowInstanceStartCoordinator.DeclaredCandidates(["user:" + First.ToString("D"), "position:" + Second.ToString("D")]),
            WorkflowInstanceStartCoordinator.DeclaredCandidates(["USER:" + First.ToString("N"), "Position:" + Second.ToString("D")]));
    }

    [Fact]
    public async Task An_escalation_of_an_instance_from_before_D2_drops_the_starter_named_only_in_StartedBy()
    {
        var seeded = await _store.SeedAsync([D(First)], dueAt: DateTimeOffset.UtcNow.AddMinutes(-5));
        await _store.MakeLegacyStarterAsync(seeded.Instance.Id, CandidateA.ToString("D"));
        // CandidateA sorts first: without the starter rule it would be the new assignee.
        await _store.EscalationRuleAsync(seeded.Instance.TemplateId, [D(CandidateA), D(CandidateB)]);

        var run = await _store.RunEscalationsAsync();

        Assert.Equal(WorkflowReasonCodes.WorkflowEscalationProcessed,
            run.Results.Single(r => r.ApprovalTaskId == seeded.Task.Id).ReasonCode);
        Assert.Equal(D(CandidateB), (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!.AssigneeRef);
    }

    // ── harness (D2-FIX1) ──────────────────────────────────────────────────────────────────────────────────────

    private static TaskItem TaskFor(Guid workflowInstanceId) => new()
    {
        TenantId = Guid.NewGuid(),
        Title = "D2-FIX1 task",
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        OrganizationUnitId = Guid.NewGuid(),
        WorkflowInstanceId = workflowInstanceId
    };

    private sealed partial class Store
    {
        public Task<Seat> SeatAsync(Guid position) =>
            Task.FromResult(new Seat(position, Guid.Empty));

        /// <summary>A finished trusted start, cut back to <paramref name="checkpoint"/> as if the first call crashed there:
        /// everything it would not have written yet is removed, and the instance is Pending again.</summary>
        public async Task CutStartAsync(TrustedWorkflowStartResult started, WorkflowStartCheckpoint checkpoint)
        {
            await Context.Database.GetCollection<WorkflowTransitionLog>(PlatformCollections.WorkflowTransitionLogs)
                .DeleteOneAsync(l => l.Id == started.StartTransitionLogId);
            await Context.Database.GetCollection<RuntimeAssignmentSnapshot>(PlatformCollections.WorkflowRuntimeAssignmentSnapshots)
                .DeleteOneAsync(x => x.Id == started.AssignmentSnapshotId);
            if (checkpoint < WorkflowStartCheckpoint.TaskPersisted)
            {
                await Context.Database.GetCollection<ApprovalTask>(PlatformCollections.ApprovalTasks)
                    .DeleteOneAsync(t => t.Id == started.ApprovalTaskId);
            }

            await Context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances).UpdateOneAsync(
                i => i.Id == started.WorkflowInstanceId,
                Builders<WorkflowInstance>.Update
                    .Set(i => i.StartCheckpoint, checkpoint)
                    .Set(i => i.Status, WorkflowInstanceStatus.Pending));
        }

        public async Task<IReadOnlyList<Guid>> InsertOverdueTasksAsync(
            int count, ApprovalTaskStatus status, bool parked, DateTimeOffset dueAt)
        {
            var tasks = Enumerable.Range(0, count).Select(_ => new ApprovalTask
            {
                TenantId = TenantContext.TenantId,
                WorkflowInstanceId = Guid.NewGuid(),
                Status = status,
                DueAt = dueAt,
                LastEscalationReasonCode = parked ? WorkflowReasonCodes.WorkflowNoEligibleApprover : null,
                // D2-FIX2 — a parked task rests until its next look time.
                EscalationNextLookAtUtc = parked ? DateTime.UtcNow.AddMinutes(10) : null
            }).ToList();
            await Context.Database.GetCollection<ApprovalTask>(PlatformCollections.ApprovalTasks).InsertManyAsync(tasks);
            return tasks.Select(t => t.Id).ToList();
        }

        /// <summary>An instance as started before D2: no StartedByUserId, the starter's id as text in StartedBy.</summary>
        public Task MakeLegacyStarterAsync(Guid instanceId, string startedBy) =>
            Context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances).UpdateOneAsync(
                i => i.Id == instanceId,
                Builders<WorkflowInstance>.Update.Set(i => i.StartedByUserId, null).Set(i => i.StartedBy, startedBy));

        /// <summary>Sets the level WITHOUT touching the version: the version checks cannot be what refuses.</summary>
        public Task SetEscalationLevelAsync(Guid taskId, int level) =>
            Context.Database.GetCollection<ApprovalTask>(PlatformCollections.ApprovalTasks).UpdateOneAsync(
                t => t.Id == taskId, Builders<ApprovalTask>.Update.Set(t => t.EscalationLevel, level));

        public async Task<(WorkflowInstance Instance, ApprovalTask Task)> TrustedOpenApprovalAsync()
        {
            var seeded = await SeedAsync([D(First)], trustedCheckpoint: WorkflowStartCheckpoint.Completed);
            await Context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances).UpdateOneAsync(
                i => i.Id == seeded.Instance.Id, Builders<WorkflowInstance>.Update.Set(i => i.DelegatedMakerUserId, TrustedMaker));
            return ((await Instances.GetByIdAsync(seeded.Instance.Id))!, (await Tasks.GetByIdAsync(seeded.Task.Id))!);
        }

        public Task<Diten.Platform.Application.Common.Response<TrustedWorkflowCancellationEvidence>> TrustedCancelAsync(
            WorkflowInstance instance, ApprovalTask task, IPlatformTransactionExecutor executor,
            string key) =>
            new TrustedWorkflowCancellationCoordinator(Instances, Tasks, Logs, executor).CancelAsync(
                instance.TrustedConsumerClientId!.Value, instance.DelegatedMakerUserId!.Value,
                new TrustedWorkflowCancellationRequest(instance.Id, task.Id, instance.ObjectType, instance.ObjectId,
                    instance.DelegatedMakerUserId!.Value, instance.Version, task.Version, "WITHDRAWN", null, key),
                "corr-" + key, default);

        /// <summary>
        /// An approval whose transaction reports "unavailable" after a TWIN row was written under the same key and actor:
        /// the twin is a copy of what a real approve writes (taken from a sibling instance), with <paramref name="field"/>
        /// changed — or nothing changed when it is null.
        /// </summary>
        public async Task<Diten.Platform.Application.Common.Response<WorkflowTaskTransitionResponse>> ApproveAgainstACraftedTwinAsync(
            string key, string? field)
        {
            var model = await SeedAsync([D(First), D(Second)]);
            Assert.True((await ApproveAsync(model.Task.Id, D(Second), key + "-model")).IsSuccessful);
            var written = Assert.Single(await Logs.ListByInstanceIdAsync(model.Instance.Id), l => l.Action == WorkflowTransitionAction.Approve);

            var target = await SeedAsync([D(First), D(Second)]);
            var executor = new ReportsUnavailableAfter(async () =>
            {
                var twin = new WorkflowTransitionLog
                {
                    TenantId = TenantContext.TenantId,
                    WorkflowInstanceId = target.Instance.Id,
                    ApprovalTaskId = target.Task.Id,
                    Action = written.Action,
                    FromState = written.FromState,
                    ToState = field == "ToState" ? ApprovalTaskStatus.Rejected.ToString() : written.ToState,
                    FromStatus = written.FromStatus,
                    ToStatus = written.ToStatus,
                    ActorId = written.ActorId,
                    ActorRef = written.ActorRef,
                    ReasonCode = field == "ReasonCode" ? "SOMETHING_ELSE" : written.ReasonCode,
                    Comment = written.Comment,
                    EvidenceRef = field == "EvidenceRef" ? "evidence:another" : written.EvidenceRef,
                    IdempotencyKey = key,
                    SequenceNo = 1
                };
                await Logs.CreateAsync(twin);
                await Context.Database.GetCollection<ApprovalTask>(PlatformCollections.ApprovalTasks).UpdateOneAsync(
                    t => t.Id == target.Task.Id, Builders<ApprovalTask>.Update.Inc(t => t.Version, 1));
            });

            return await ApproveAsync(target.Task.Id, D(Second), key, executor);
        }

        public Task EscalationRuleAsync(Guid templateId, IReadOnlyList<string> escalateTo, int? timeoutAfterMinutes) =>
            new SlaEscalationRuleRepository(Context, TenantContext).CreateAsync(new SlaEscalationRule
            {
                TenantId = TenantContext.TenantId,
                TemplateId = templateId,
                StageCode = "stage-1",
                StepCode = "step-a",
                DueInMinutes = 10,
                EscalateAfterMinutes = 10,
                TimeoutAfterMinutes = timeoutAfterMinutes,
                EscalationPrincipalIds = escalateTo.ToList()
            });

        public async Task<RunWorkflowEscalationsResponse> SweepAsync(
            int maxItems = 500, ILogger<RunWorkflowEscalationsHandler>? logger = null)
        {
            var response = await new RunWorkflowEscalationsHandler(
                    Tasks, Instances, new SlaEscalationRuleRepository(Context, TenantContext), Logs, TimeProvider.System,
                    assignmentSnapshotRepository: Snapshots,
                    transactions: new PlatformTransactionExecutor(Context),
                    events: new MongoIntentWriter(Context),
                    templates: Templates,
                    logger: logger)
                .Handle(new RunWorkflowEscalationsCommand(new RunWorkflowEscalationsRequest(null, maxItems, null), "corr-escalation"), default);
            Assert.True(response.IsSuccessful, response.ReasonCode);
            return response.Data!;
        }

        /// <summary>The REAL Tasks approval service, its cancel routed to MOD-0023's real cancel handler.</summary>
        public TaskApprovalService TaskApprovals(Func<Task>? beforeCancel = null) => new(
            new CancelOnlyMediator(
                new CancelWorkflowTaskHandler(Tasks, Instances, Snapshots, Logs,
                    new PlatformTransactionExecutor(Context), new MongoIntentWriter(Context), Templates),
                beforeCancel),
            Templates,
            Instances,
            Tasks,
            TenantContext,
            new CurrentUser(Outsider),
            Options.Create(new TaskApprovalOptions()),
            NullLogger<TaskApprovalService>.Instance);
    }

    private sealed class CountingLogger : ILogger<RunWorkflowEscalationsHandler>
    {
        private readonly List<string> _warnings = [];

        public int WarningsAbout(Guid taskId) => _warnings.Count(w => w.Contains(taskId.ToString(), StringComparison.Ordinal));

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                _warnings.Add(formatter(state, exception));
            }
        }
    }

    private sealed class CancelOnlyMediator(CancelWorkflowTaskHandler handler, Func<Task>? beforeCancel) : IMediator
    {
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            if (request is not CancelWorkflowTaskCommand command)
            {
                throw new InvalidOperationException($"Unexpected request {request.GetType().Name}.");
            }

            if (beforeCancel is not null)
            {
                await beforeCancel();
            }

            return (TResponse)(object)await handler.Handle(command, ct);
        }

        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();

        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest
            => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken ct = default) => throw new NotSupportedException();

        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
            where TNotification : INotification => throw new NotSupportedException();
    }
}

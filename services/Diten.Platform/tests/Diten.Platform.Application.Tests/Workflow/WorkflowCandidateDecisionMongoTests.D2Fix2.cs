using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Workflow.Services;
using Diten.Platform.Domain.Entities.Organization;
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
/// WP-WF-ENGINE-CANDIDATES-01 · D2 fix round 2 (CT-WF-D2-FIX2). Real replica set, production handlers and repositories.
/// </summary>
public sealed partial class WorkflowCandidateDecisionMongoTests
{
    // ── 3 · an empty seat keeps the reservation open; only "nobody but the starter" closes it ───────────────────

    [Fact]
    public async Task A_reservation_whose_seat_is_empty_stays_open_and_completes_once_the_seat_is_filled()
    {
        var templateId = await _store.PublishedTemplateAsync();
        var seat = await _store.SeatHeldByAsync(First);
        string[] declared = ["position:" + seat.Position.ToString("D")];
        var started = await _store.Coordinator(withPositions: true).StartAsync(
            TrustedRequest(templateId, "empty-seat", declared), TrustedClient, TrustedMaker, "corr-es");
        Assert.True(started.IsSuccessful, started.ReasonCode);
        await _store.CutStartAsync(started.Data!, WorkflowStartCheckpoint.Reserved);
        await _store.VacateSeatAsync(seat);

        var whileEmpty = await _store.Coordinator(withPositions: true).StartAsync(
            TrustedRequest(templateId, "empty-seat", declared), TrustedClient, TrustedMaker, "corr-es-2");
        var again = await _store.Coordinator(withPositions: true).StartAsync(
            TrustedRequest(templateId, "empty-seat", declared), TrustedClient, TrustedMaker, "corr-es-3");

        // Transient, and the same answer every time: nobody holds the seat yet, nothing is decided.
        foreach (var answer in new[] { whileEmpty, again })
        {
            Assert.Equal(503, answer.StatusCode);
            Assert.Equal(WorkflowReasonCodes.WorkflowAssignmentCandidatesRequired, answer.ReasonCode);
        }

        var open = (await _store.Instances.GetByIdAsync(started.Data!.WorkflowInstanceId))!;
        Assert.Equal(WorkflowInstanceStatus.Pending, open.Status);           // not closed
        Assert.Equal(WorkflowStartCheckpoint.Reserved, open.StartCheckpoint);
        Assert.Null(open.StartClosedReasonCode);

        await _store.FillSeatAsync(seat.Position, Second);
        var filled = await _store.Coordinator(withPositions: true).StartAsync(
            TrustedRequest(templateId, "empty-seat", declared), TrustedClient, TrustedMaker, "corr-es-4");

        Assert.True(filled.IsSuccessful, filled.ReasonCode);
        Assert.Equal(started.Data.WorkflowInstanceId, filled.Data!.WorkflowInstanceId);
        Assert.Equal(WorkflowInstanceStatus.Active, (await _store.Instances.GetByIdAsync(open.Id))!.Status);
        Assert.Equal(D(Second), (await _store.Tasks.GetByIdAsync(started.Data.ApprovalTaskId))!.AssigneeRef);
    }

    [Fact]
    public async Task A_reservation_closed_because_nobody_else_may_approve_stores_its_reason_and_repeats_it()
    {
        var templateId = await _store.PublishedTemplateAsync();
        var seat = await _store.SeatHeldByAsync(First);
        string[] declared = ["position:" + seat.Position.ToString("D")];
        var started = await _store.Coordinator(withPositions: true).StartAsync(
            TrustedRequest(templateId, "closed-reason", declared), TrustedClient, TrustedMaker, "corr-cr");
        Assert.True(started.IsSuccessful, started.ReasonCode);
        await _store.CutStartAsync(started.Data!, WorkflowStartCheckpoint.Reserved);
        await _store.HandSeatOverAsync(seat, TrustedMaker);

        var closing = await _store.Coordinator(withPositions: true).StartAsync(
            TrustedRequest(templateId, "closed-reason", declared), TrustedClient, TrustedMaker, "corr-cr-2");

        Assert.Equal(400, closing.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowNoEligibleApprover, closing.ReasonCode);
        var closed = (await _store.Instances.GetByIdAsync(started.Data!.WorkflowInstanceId))!;
        Assert.Equal(WorkflowInstanceStatus.Cancelled, closed.Status);
        Assert.Equal(WorkflowReasonCodes.WorkflowNoEligibleApprover, closed.StartClosedReasonCode);

        // The answer is the STORED one, not a constant: a reservation that names another reason replays that reason.
        await _store.SetClosedReasonAsync(closed.Id, WorkflowReasonCodes.WorkflowAssignmentCandidatesRequired);
        var replay = await _store.Coordinator(withPositions: true).StartAsync(
            TrustedRequest(templateId, "closed-reason", declared), TrustedClient, TrustedMaker, "corr-cr-3");
        Assert.Equal(400, replay.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowAssignmentCandidatesRequired, replay.ReasonCode);
    }

    [Fact]
    public async Task A_snapshot_that_appears_after_the_first_read_is_the_fact_the_replay_follows()
    {
        // The first call wrote its snapshot between this call's two reads (two first calls racing): the late snapshot
        // names the person, not the seat as it resolves now.
        var templateId = await _store.PublishedTemplateAsync();
        var seat = await _store.SeatHeldByAsync(First);
        string[] declared = ["position:" + seat.Position.ToString("D")];
        var started = await _store.Coordinator(withPositions: true).StartAsync(
            TrustedRequest(templateId, "late-snap", declared), TrustedClient, TrustedMaker, "corr-ls");
        Assert.True(started.IsSuccessful, started.ReasonCode);
        await _store.HandSeatOverAsync(seat, Second);

        var replay = await _store.CoordinatorWith(snapshots: new SnapshotsMissTheFirstRead(_store.Snapshots))
            .StartAsync(TrustedRequest(templateId, "late-snap", declared), TrustedClient, TrustedMaker, "corr-ls-2");

        Assert.True(replay.IsSuccessful, replay.ReasonCode);
        Assert.True(replay.Data!.IsReplay);
        Assert.Equal(started.Data!.WorkflowInstanceId, replay.Data.WorkflowInstanceId);
        Assert.Equal(D(First), (await _store.Tasks.GetByIdAsync(started.Data.ApprovalTaskId))!.AssigneeRef);
    }

    [Fact]
    public async Task A_late_snapshot_that_names_nobody_still_has_the_task_read_and_is_a_recovery_conflict()
    {
        // The late snapshot is empty; the task names First. Skipping the task read made this a no-eligible answer for a
        // start that DID write a task — it is a recovery conflict, the same answer an empty stored snapshot gets.
        var templateId = await _store.PublishedTemplateAsync();
        var seat = await _store.SeatHeldByAsync(First);
        string[] declared = ["position:" + seat.Position.ToString("D")];
        var started = await _store.Coordinator(withPositions: true).StartAsync(
            TrustedRequest(templateId, "late-empty", declared), TrustedClient, TrustedMaker, "corr-le");
        Assert.True(started.IsSuccessful, started.ReasonCode);
        await _store.Context.Database.GetCollection<RuntimeAssignmentSnapshot>(PlatformCollections.WorkflowRuntimeAssignmentSnapshots)
            .UpdateOneAsync(x => x.Id == started.Data!.AssignmentSnapshotId,
                Builders<RuntimeAssignmentSnapshot>.Update.Set(x => x.CandidatePrincipalIds, new List<string>()));
        await _store.HandSeatOverAsync(seat, TrustedMaker);

        var replay = await _store.CoordinatorWith(snapshots: new SnapshotsMissTheFirstRead(_store.Snapshots))
            .StartAsync(TrustedRequest(templateId, "late-empty", declared), TrustedClient, TrustedMaker, "corr-le-2");

        Assert.Equal(409, replay.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowStartRecoveryConflict, replay.ReasonCode);
    }

    [Fact]
    public async Task A_start_cut_before_its_snapshot_writes_the_tasks_person_first_and_keeps_the_seat_holder()
    {
        var templateId = await _store.PublishedTemplateAsync();
        var seat = await _store.SeatHeldByAsync(First);
        string[] declared = ["position:" + seat.Position.ToString("D")];
        var started = await _store.Coordinator(withPositions: true).StartAsync(
            TrustedRequest(templateId, "cut-list", declared), TrustedClient, TrustedMaker, "corr-cl");
        Assert.True(started.IsSuccessful, started.ReasonCode);
        await _store.CutStartAsync(started.Data!, WorkflowStartCheckpoint.TaskPersisted);
        await _store.HandSeatOverAsync(seat, Second);

        var replay = await _store.Coordinator(withPositions: true).StartAsync(
            TrustedRequest(templateId, "cut-list", declared), TrustedClient, TrustedMaker, "corr-cl-2");

        Assert.True(replay.IsSuccessful, replay.ReasonCode);
        // The WHOLE list: the task's person first, then who the seat resolves to now.
        Assert.Equal([D(First), D(Second)],
            (await _store.Snapshots.GetByIdAsync(started.Data!.AssignmentSnapshotId))!.CandidatePrincipalIds);
    }

    [Fact]
    public async Task A_close_whose_write_loses_its_race_is_a_recovery_conflict_and_closes_nothing()
    {
        var templateId = await _store.PublishedTemplateAsync();
        var seat = await _store.SeatHeldByAsync(First);
        string[] declared = ["position:" + seat.Position.ToString("D")];
        var started = await _store.Coordinator(withPositions: true).StartAsync(
            TrustedRequest(templateId, "close-race", declared), TrustedClient, TrustedMaker, "corr-clr");
        Assert.True(started.IsSuccessful, started.ReasonCode);
        await _store.CutStartAsync(started.Data!, WorkflowStartCheckpoint.Reserved);
        await _store.HandSeatOverAsync(seat, TrustedMaker);
        var instances = new InstancesThatMoveBeforeAnUpdate(_store.Instances, id => _store.BumpInstanceVersionAsync(id));

        var replay = await _store.CoordinatorWith(instances: instances).StartAsync(
            TrustedRequest(templateId, "close-race", declared), TrustedClient, TrustedMaker, "corr-clr-2");

        Assert.Equal(409, replay.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowStartRecoveryConflict, replay.ReasonCode);
        Assert.Equal(WorkflowInstanceStatus.Pending,
            (await _store.Instances.GetByIdAsync(started.Data!.WorkflowInstanceId))!.Status);
    }

    // ── 6(b) · a reservation closed under a first call's feet leaves no task waiting ────────────────────────────

    [Fact]
    public async Task A_first_call_whose_reservation_is_closed_while_it_writes_withdraws_the_task_it_wrote()
    {
        // C1: the first call reserves and is about to write its task; a twin call with the same key finds the seat now
        // held only by the maker and closes the reservation. The first call's task lands under a Cancelled instance.
        var templateId = await _store.PublishedTemplateAsync();
        var seat = await _store.SeatHeldByAsync(First);
        string[] declared = ["position:" + seat.Position.ToString("D")];
        Response<TrustedWorkflowStartResult>? twin = null;
        var tasks = new RunsBeforeTheStartTask(_store.Tasks, async () =>
        {
            await _store.HandSeatOverAsync(seat, TrustedMaker);
            twin = await _store.Coordinator(withPositions: true).StartAsync(
                TrustedRequest(templateId, "c1-race", declared), TrustedClient, TrustedMaker, "corr-c1-twin");
        });

        var first = await _store.CoordinatorWith(tasks: tasks).StartAsync(
            TrustedRequest(templateId, "c1-race", declared), TrustedClient, TrustedMaker, "corr-c1");

        Assert.NotNull(twin);
        Assert.Equal(WorkflowReasonCodes.WorkflowNoEligibleApprover, twin!.ReasonCode); // non-vacuity: the twin closed it
        // The first call answers what every replay of the key answers, and its task is not left waiting under the
        // closed instance (nobody could ever decide it, and nothing would ever read it).
        Assert.Equal(400, first.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowNoEligibleApprover, first.ReasonCode);
        var instance = (await _store.Instances.GetByIdempotencyKeyAsync("c1-race"))!;
        Assert.Equal(WorkflowInstanceStatus.Cancelled, instance.Status);
        var written = (await _store.Tasks.GetByIdAsync(instance.InitialApprovalTaskId!.Value))!;
        Assert.Equal(ApprovalTaskStatus.Cancelled, written.Status);
        Assert.Equal(WorkflowReasonCodes.WorkflowNoEligibleApprover, written.ActionReasonCode);
    }

    [Fact]
    public async Task A_replay_of_a_closed_reservation_withdraws_a_task_left_waiting_under_it()
    {
        // What the first call above leaves behind when it dies between writing its task and withdrawing it.
        var templateId = await _store.PublishedTemplateAsync();
        var started = await _store.Coordinator().StartAsync(
            TrustedRequest(templateId, "stray-task", [D(First)]), TrustedClient, TrustedMaker, "corr-st");
        Assert.True(started.IsSuccessful, started.ReasonCode);
        await _store.CloseAsIfRacedAsync(started.Data!);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, (await _store.Tasks.GetByIdAsync(started.Data!.ApprovalTaskId))!.Status);

        var replay = await _store.Coordinator().StartAsync(
            TrustedRequest(templateId, "stray-task", [D(First)]), TrustedClient, TrustedMaker, "corr-st-2");

        Assert.Equal(400, replay.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowNoEligibleApprover, replay.ReasonCode);
        Assert.Equal(ApprovalTaskStatus.Cancelled, (await _store.Tasks.GetByIdAsync(started.Data.ApprovalTaskId))!.Status);
    }

    // ── 4 · every task the sweep cannot act on gets a next look time; the read starts with the ones due ─────────

    [Fact]
    public async Task More_than_a_page_of_overdue_tasks_without_an_SLA_rule_does_not_starve_a_newly_overdue_one()
    {
        // No SLA rule for this template: every one of these is skipped, sweep after sweep. Before the look time they
        // filled the first page forever.
        var ruleless = await _store.SeedAsync([D(First)], dueAt: DateTimeOffset.UtcNow.AddDays(-30));
        await _store.InsertTasksOnAsync(ruleless.Instance.Id, 100, DateTimeOffset.UtcNow.AddDays(-30));
        var fresh = await _store.SeedAsync([D(First)], dueAt: DateTimeOffset.UtcNow.AddMinutes(-5));
        await _store.EscalationRuleAsync(fresh.Instance.TemplateId, [D(CandidateB)]);

        var first = await _store.SweepAsync(maxItems: 100);
        var second = await _store.SweepAsync(maxItems: 100);

        Assert.DoesNotContain(first.Results, r => r.ApprovalTaskId == fresh.Task.Id); // non-vacuity: the page was full
        Assert.All(first.Results, r => Assert.Equal(WorkflowReasonCodes.WorkflowSlaRuleNotFound, r.ReasonCode));
        Assert.Contains(second.Results, r => r.ApprovalTaskId == fresh.Task.Id
            && r.ReasonCode == WorkflowReasonCodes.WorkflowEscalationProcessed);
        Assert.NotNull((await _store.Tasks.GetByIdAsync(ruleless.Task.Id))!.EscalationNextLookAtUtc);
    }

    [Fact]
    public async Task A_parked_escalation_is_looked_at_again_when_its_time_comes_and_goes_to_the_new_seat_holder()
    {
        var seat = await _store.SeatHeldByAsync(CandidateA);
        var seeded = await _store.SeedAsync([D(First)], dueAt: DateTimeOffset.UtcNow.AddMinutes(-5), startedBy: CandidateA);
        await _store.EscalationRuleAsync(seeded.Instance.TemplateId, ["position:" + seat.Position.ToString("D")]);

        var parked = await _store.SweepAtAsync(TimeSpan.Zero);
        Assert.Equal(WorkflowReasonCodes.WorkflowNoEligibleApprover,
            parked.Results.Single(r => r.ApprovalTaskId == seeded.Task.Id).ReasonCode);
        await _store.HandSeatOverAsync(seat, CandidateB);

        var resting = await _store.SweepAtAsync(TimeSpan.Zero);
        var due = await _store.SweepAtAsync(RunWorkflowEscalationsHandler.InconclusiveRecheckInterval + TimeSpan.FromMinutes(1));

        Assert.DoesNotContain(resting.Results, r => r.ApprovalTaskId == seeded.Task.Id); // rests until its look time
        Assert.Equal(WorkflowReasonCodes.WorkflowEscalationProcessed,
            due.Results.Single(r => r.ApprovalTaskId == seeded.Task.Id).ReasonCode);
        var task = (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!;
        Assert.Equal(ApprovalTaskStatus.Escalated, task.Status);
        Assert.Equal(D(CandidateB), task.AssigneeRef);
        Assert.Null(task.EscalationNextLookAtUtc); // an escalation clears it
    }

    [Fact]
    public async Task A_look_time_is_never_later_than_the_next_SLA_deadline()
    {
        // Overdue but not yet at its escalation threshold (5 minutes after the due date): the look time is the threshold,
        // not a quarter of an hour away.
        var dueAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var seeded = await _store.SeedAsync([D(First)], dueAt: dueAt);
        await _store.EscalationRuleAfterAsync(seeded.Instance.TemplateId, [D(CandidateB)], escalateAfterMinutes: 15);

        var run = await _store.SweepAsync();

        Assert.Equal(WorkflowReasonCodes.WorkflowNoOverdueTasks, run.Results.Single(r => r.ApprovalTaskId == seeded.Task.Id).ReasonCode);
        var lookAt = (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!.EscalationNextLookAtUtc;
        Assert.NotNull(lookAt);
        Assert.InRange(lookAt!.Value, dueAt.UtcDateTime.AddMinutes(5).AddSeconds(-1), dueAt.UtcDateTime.AddMinutes(5).AddSeconds(1));
    }

    [Fact]
    public async Task A_timeout_clears_the_look_time()
    {
        var seeded = await _store.SeedAsync([D(First)], dueAt: DateTimeOffset.UtcNow.AddMinutes(-60));
        await _store.EscalationRuleAsync(seeded.Instance.TemplateId, [D(CandidateA)], timeoutAfterMinutes: 10);
        await _store.SetNextLookAsync(seeded.Task.Id, DateTime.UtcNow.AddMinutes(-1)); // looked at before, due again

        var run = await _store.SweepAsync();

        Assert.Equal(WorkflowReasonCodes.WorkflowTimeoutProcessed, run.Results.Single(r => r.ApprovalTaskId == seeded.Task.Id).ReasonCode);
        var task = (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!;
        Assert.Equal(ApprovalTaskStatus.TimedOut, task.Status);
        Assert.Null(task.EscalationNextLookAtUtc);
    }

    [Fact]
    public async Task The_overdue_read_skips_tasks_still_resting_and_reads_never_looked_ones_before_those_due_again()
    {
        var now = DateTimeOffset.UtcNow;
        var neverLooked = await _store.InsertOverdueAsync(now.AddHours(-1), lookAt: null);
        var dueLater = await _store.InsertOverdueAsync(now.AddHours(-3), lookAt: now.UtcDateTime.AddMinutes(-10));
        var dueEarlier = await _store.InsertOverdueAsync(now.AddHours(-2), lookAt: now.UtcDateTime.AddMinutes(-20));
        var resting = await _store.InsertOverdueAsync(now.AddHours(-4), lookAt: now.UtcDateTime.AddMinutes(10));

        var read = await _store.Tasks.ListOverdueTasksAsync(now, 10);

        Assert.Equal([neverLooked, dueEarlier, dueLater], read.Select(t => t.Id).ToList());
        Assert.DoesNotContain(resting, read.Select(t => t.Id));
    }

    [Fact]
    public async Task The_overdue_read_breaks_a_tie_on_the_due_date_by_id()
    {
        var dueAt = DateTimeOffset.UtcNow.AddHours(-1);
        var ids = new List<Guid>
        {
            await _store.InsertOverdueAsync(dueAt, lookAt: null),
            await _store.InsertOverdueAsync(dueAt, lookAt: null),
            await _store.InsertOverdueAsync(dueAt, lookAt: null)
        };

        var read = await _store.Tasks.ListOverdueTasksAsync(DateTimeOffset.UtcNow, 10);

        // A Standard-representation Guid is stored as its RFC bytes, so the store's order is the order of its hex text.
        Assert.Equal(ids.OrderBy(id => id.ToString("N"), StringComparer.Ordinal).ToList(), read.Select(t => t.Id).ToList());
    }

    [Fact]
    public void The_overdue_read_has_an_index_over_scalar_fields_only()
    {
        var approvalTasks = Assert.Single(PlatformSchemaManifest.For(SchemaProfile.WorkflowWorkCenter),
            c => c.Name == PlatformCollections.ApprovalTasks);
        var index = Assert.Single(approvalTasks.Indexes, i => i.Name == "ix_approval_tasks_tenant_status_next_look");

        Assert.Equal(["TenantId", "Status", nameof(ApprovalTask.EscalationNextLookAtUtc), "_id"],
            index.Key.Names.ToList());
        // BL-030: DueAt is stored as an array; the read's index carries none.
        Assert.DoesNotContain(index.Key.Names, name => name.StartsWith(nameof(ApprovalTask.DueAt), StringComparison.Ordinal));
    }

    // ── 6(c) · the real review withdraw, and both withdraws when MOD-0023 throws ─────────────────────────────────

    [Fact]
    public async Task Tasks_review_withdraw_of_a_still_escalated_review_cancels_it()
    {
        var seeded = await _store.SeedAsync([D(First)], escalatedTo: [D(Second)]);
        Assert.Equal(ApprovalTaskStatus.Escalated, seeded.Task.Status);

        var withdrawn = await _store.TaskReviews().CancelReviewAsync(ReviewTaskFor(seeded.Instance.Id), default);

        Assert.True(withdrawn);
        Assert.Equal(ApprovalTaskStatus.Cancelled, (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!.Status);
    }

    [Fact]
    public async Task Tasks_review_withdraw_of_a_review_decided_meanwhile_reports_that_it_could_not_withdraw()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        var service = _store.TaskReviews(beforeCancel: () => _store.ApproveAsync(seeded.Task.Id, D(Second), "review-race"));

        var withdrawn = await service.CancelReviewAsync(ReviewTaskFor(seeded.Instance.Id), default);

        Assert.False(withdrawn);
        Assert.Equal(WorkflowInstanceStatus.Completed, (await _store.Instances.GetByIdAsync(seeded.Instance.Id))!.Status);
    }

    [Theory]
    [InlineData("approval")]
    [InlineData("review")]
    public async Task A_withdraw_whose_engine_call_throws_reports_that_it_could_not_withdraw(string which)
    {
        var seeded = await _store.SeedAsync([D(First)]);
        var mediator = new ThrowingMediator();

        var withdrawn = which == "approval"
            ? await new TaskApprovalService(mediator, _store.Templates, _store.Instances, _store.Tasks, _store.TenantContext,
                    new CurrentUser(Outsider), Options.Create(new TaskApprovalOptions()), NullLogger<TaskApprovalService>.Instance)
                .CancelApprovalAsync(TaskFor(seeded.Instance.Id), default)
            : await new TaskReviewService(mediator, _store.Templates, _store.Instances, _store.Tasks, _store.TenantContext,
                    new CurrentUser(Outsider), Options.Create(new TaskReviewOptions()), NullLogger<TaskReviewService>.Instance)
                .CancelReviewAsync(ReviewTaskFor(seeded.Instance.Id), default);

        Assert.True(mediator.Calls > 0); // non-vacuity: an open task was found and the engine was asked
        Assert.False(withdrawn);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!.Status);
    }

    // ── harness (D2-FIX2) ──────────────────────────────────────────────────────────────────────────────────────

    private static TaskItem ReviewTaskFor(Guid reviewInstanceId) => new()
    {
        TenantId = Guid.NewGuid(),
        Title = "D2-FIX2 review",
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        OrganizationUnitId = Guid.NewGuid(),
        ReviewRequired = true,
        ReviewWorkflowInstanceId = reviewInstanceId
    };

    private sealed partial class Store
    {
        /// <summary>The position-resolving coordinator with one of its stores swapped for a test double.</summary>
        public WorkflowInstanceStartCoordinator CoordinatorWith(
            IWorkflowInstanceRepository? instances = null,
            IApprovalTaskRepository? tasks = null,
            IRuntimeAssignmentSnapshotRepository? snapshots = null,
            IWorkflowTransitionLogRepository? logs = null) =>
            new(Templates, Versions, instances ?? Instances, tasks ?? Tasks, snapshots ?? Snapshots, logs ?? Logs, TenantContext,
                new PositionAssignmentRepository(Context, TenantContext), null, new PositionRepository(Context, TenantContext));

        /// <summary>The holder leaves and nobody takes the seat.</summary>
        public Task VacateSeatAsync(Seat seat) =>
            Context.Database.GetCollection<PositionAssignment>(PlatformCollections.PositionAssignments).UpdateOneAsync(
                a => a.Id == seat.Assignment,
                Builders<PositionAssignment>.Update.Set(a => a.EffectiveTo, DateTimeOffset.UtcNow.AddMinutes(-1)));

        public Task FillSeatAsync(Guid position, Guid user) =>
            Context.Database.GetCollection<PositionAssignment>(PlatformCollections.PositionAssignments).InsertOneAsync(
                new PositionAssignment
                {
                    TenantId = TenantContext.TenantId,
                    PositionId = position,
                    UserId = user,
                    EffectiveFrom = DateTimeOffset.UtcNow.AddMinutes(-1)
                });

        public Task SetClosedReasonAsync(Guid instanceId, string reason) =>
            Context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances).UpdateOneAsync(
                i => i.Id == instanceId, Builders<WorkflowInstance>.Update.Set(i => i.StartClosedReasonCode, reason));

        /// <summary>A finished start rewritten into what a raced first call leaves when it dies after writing its task:
        /// the reservation closed (no-eligible) at Reserved, the task still waiting, no snapshot and no start log.</summary>
        public async Task CloseAsIfRacedAsync(TrustedWorkflowStartResult started)
        {
            await Context.Database.GetCollection<WorkflowTransitionLog>(PlatformCollections.WorkflowTransitionLogs)
                .DeleteOneAsync(l => l.Id == started.StartTransitionLogId);
            await Context.Database.GetCollection<RuntimeAssignmentSnapshot>(PlatformCollections.WorkflowRuntimeAssignmentSnapshots)
                .DeleteOneAsync(x => x.Id == started.AssignmentSnapshotId);
            await Context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances).UpdateOneAsync(
                i => i.Id == started.WorkflowInstanceId,
                Builders<WorkflowInstance>.Update
                    .Set(i => i.StartCheckpoint, WorkflowStartCheckpoint.Reserved)
                    .Set(i => i.Status, WorkflowInstanceStatus.Cancelled)
                    .Set(i => i.StartClosedReasonCode, WorkflowReasonCodes.WorkflowNoEligibleApprover));
        }

        /// <summary>Overdue waiting tasks on an existing instance (whose template has no SLA rule unless the test adds one).</summary>
        public Task InsertTasksOnAsync(Guid instanceId, int count, DateTimeOffset dueAt) =>
            Context.Database.GetCollection<ApprovalTask>(PlatformCollections.ApprovalTasks).InsertManyAsync(
                Enumerable.Range(0, count).Select(_ => new ApprovalTask
                {
                    TenantId = TenantContext.TenantId,
                    WorkflowInstanceId = instanceId,
                    StageCode = "stage-1",
                    StepCode = "step-a",
                    Status = ApprovalTaskStatus.WaitingApproval,
                    DueAt = dueAt
                }));

        public async Task<Guid> InsertOverdueAsync(DateTimeOffset dueAt, DateTime? lookAt)
        {
            var task = new ApprovalTask
            {
                TenantId = TenantContext.TenantId,
                WorkflowInstanceId = Guid.NewGuid(),
                Status = ApprovalTaskStatus.WaitingApproval,
                DueAt = dueAt,
                EscalationNextLookAtUtc = lookAt
            };
            await Context.Database.GetCollection<ApprovalTask>(PlatformCollections.ApprovalTasks).InsertOneAsync(task);
            return task.Id;
        }

        public Task SetNextLookAsync(Guid taskId, DateTime lookAt) =>
            Context.Database.GetCollection<ApprovalTask>(PlatformCollections.ApprovalTasks).UpdateOneAsync(
                t => t.Id == taskId, Builders<ApprovalTask>.Update.Set(t => t.EscalationNextLookAtUtc, lookAt));

        public Task EscalationRuleAfterAsync(Guid templateId, IReadOnlyList<string> escalateTo, int escalateAfterMinutes) =>
            new SlaEscalationRuleRepository(Context, TenantContext).CreateAsync(new SlaEscalationRule
            {
                TenantId = TenantContext.TenantId,
                TemplateId = templateId,
                StageCode = "stage-1",
                StepCode = "step-a",
                DueInMinutes = 10,
                EscalateAfterMinutes = escalateAfterMinutes,
                EscalationPrincipalIds = escalateTo.ToList()
            });

        /// <summary>A sweep on the server clock moved forward by <paramref name="shift"/>, resolving positions.</summary>
        public async Task<RunWorkflowEscalationsResponse> SweepAtAsync(
            TimeSpan shift, int maxItems = 500, ILogger<RunWorkflowEscalationsHandler>? logger = null)
        {
            var response = await new RunWorkflowEscalationsHandler(
                    Tasks, Instances, new SlaEscalationRuleRepository(Context, TenantContext), Logs, new ShiftedClock(shift),
                    positionAssignmentRepository: new PositionAssignmentRepository(Context, TenantContext),
                    assignmentSnapshotRepository: Snapshots,
                    positionRepository: new PositionRepository(Context, TenantContext),
                    transactions: new PlatformTransactionExecutor(Context),
                    events: new MongoIntentWriter(Context),
                    templates: Templates,
                    logger: logger)
                .Handle(new RunWorkflowEscalationsCommand(new RunWorkflowEscalationsRequest(null, maxItems, null), "corr-shifted"), default);
            Assert.True(response.IsSuccessful, response.ReasonCode);
            return response.Data!;
        }

        /// <summary>The REAL Tasks review service, its cancel routed to MOD-0023's real cancel handler.</summary>
        public TaskReviewService TaskReviews(Func<Task>? beforeCancel = null) => new(
            new CancelOnlyMediator(
                new CancelWorkflowTaskHandler(Tasks, Instances, Snapshots, Logs,
                    new PlatformTransactionExecutor(Context), new MongoIntentWriter(Context), Templates),
                beforeCancel),
            Templates,
            Instances,
            Tasks,
            TenantContext,
            new CurrentUser(Outsider),
            Options.Create(new TaskReviewOptions()),
            NullLogger<TaskReviewService>.Instance);
    }

    private sealed class ShiftedClock(TimeSpan shift) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TimeProvider.System.GetUtcNow() + shift;
    }

    /// <summary>The first by-id read finds nothing (the snapshot is not written yet); every later read is the store's.</summary>
    private sealed class SnapshotsMissTheFirstRead(IRuntimeAssignmentSnapshotRepository inner) : IRuntimeAssignmentSnapshotRepository
    {
        private int _reads;

        public Task<RuntimeAssignmentSnapshot?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Interlocked.Increment(ref _reads) == 1 ? Task.FromResult<RuntimeAssignmentSnapshot?>(null) : inner.GetByIdAsync(id, ct);

        public Task<RuntimeAssignmentSnapshot> CreateAsync(RuntimeAssignmentSnapshot snapshot, CancellationToken ct = default) =>
            inner.CreateAsync(snapshot, ct);

        public Task<IReadOnlyList<RuntimeAssignmentSnapshot>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            inner.ListByInstanceIdAsync(workflowInstanceId, ct);

        public Task<RuntimeAssignmentSnapshot> EnsureTrustedStartSnapshotAsync(RuntimeAssignmentSnapshot snapshot, CancellationToken ct = default) =>
            inner.EnsureTrustedStartSnapshotAsync(snapshot, ct);
    }

    /// <summary>Somebody else moves the instance just before each full write: every UpdateAsync loses its version check.</summary>
    private sealed class InstancesThatMoveBeforeAnUpdate(IWorkflowInstanceRepository inner, Func<Guid, Task> before)
        : IWorkflowInstanceRepository
    {
        public Task<WorkflowInstance> CreateAsync(WorkflowInstance instance, CancellationToken ct = default) => inner.CreateAsync(instance, ct);
        public Task<WorkflowInstance?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        public Task<WorkflowInstance?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default) =>
            inner.GetByIdempotencyKeyAsync(idempotencyKey, ct);
        public Task<WorkflowInstance?> GetLatestByObjectRefAsync(string objectRef, string objectType, string objectId, CancellationToken ct = default) =>
            inner.GetLatestByObjectRefAsync(objectRef, objectType, objectId, ct);
        public Task<IReadOnlyList<WorkflowInstance>> GetAllForTenantAsync(CancellationToken ct = default) => inner.GetAllForTenantAsync(ct);

        public async Task<bool> UpdateAsync(WorkflowInstance instance, int expectedVersion, CancellationToken ct = default)
        {
            await before(instance.Id);
            return await inner.UpdateAsync(instance, expectedVersion, ct);
        }

        public Task<(WorkflowInstance Instance, bool Created)> ReserveTrustedStartAsync(WorkflowInstance instance, CancellationToken ct = default) =>
            inner.ReserveTrustedStartAsync(instance, ct);

        public Task<bool> AdvanceStartCheckpointAsync(Guid instanceId, int expectedVersion, WorkflowStartCheckpoint expectedCheckpoint,
            WorkflowStartCheckpoint nextCheckpoint, CancellationToken ct = default) =>
            inner.AdvanceStartCheckpointAsync(instanceId, expectedVersion, expectedCheckpoint, nextCheckpoint, ct);
    }

    /// <summary>Runs <paramref name="before"/> just before the start task is written, then writes it through the store.</summary>
    private sealed class RunsBeforeTheStartTask(IApprovalTaskRepository inner, Func<Task> before) : IApprovalTaskRepository
    {
        public async Task<ApprovalTask> EnsureTrustedStartTaskAsync(ApprovalTask task, CancellationToken ct = default)
        {
            await before();
            return await inner.EnsureTrustedStartTaskAsync(task, ct);
        }

        public Task<ApprovalTask> CreateAsync(ApprovalTask task, CancellationToken ct = default) => inner.CreateAsync(task, ct);
        public Task<ApprovalTask?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
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

    /// <summary>MOD-0023 is down: every request throws.</summary>
    private sealed class ThrowingMediator : IMediator
    {
        public int Calls { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            Calls++;
            throw new InvalidOperationException("the workflow module is unavailable");
        }

        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
            where TNotification : INotification => throw new NotSupportedException();
    }
}

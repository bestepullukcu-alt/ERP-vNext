using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Workflow.Services;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

/// <summary>
/// WP-WF-ENGINE-CANDIDATES-01 · D2 (P-2026-10-03-D2) — the starter is never an approver, an empty list is an explicit
/// code, a trusted replay survives a seat change, and the D1 deferrals (C1, C4, C5, payload, 503 text). Real replica
/// set, production handlers.
/// </summary>
public sealed partial class WorkflowCandidateDecisionMongoTests
{
    // ── 1 · the starter is dropped from the list, everywhere a list is resolved ─────────────────────────────────

    [Fact]
    public async Task A_trusted_start_drops_the_maker_from_its_candidates()
    {
        var templateId = await _store.PublishedTemplateAsync();

        var started = await _store.Coordinator().StartAsync(
            TrustedRequest(templateId, "maker-dropped", [D(TrustedMaker), D(First)]), TrustedClient, TrustedMaker, "corr-md");

        Assert.True(started.IsSuccessful, started.ReasonCode);
        var snapshot = (await _store.Snapshots.GetByIdAsync(started.Data!.AssignmentSnapshotId))!;
        Assert.Equal([D(First)], snapshot.CandidatePrincipalIds);
    }

    [Fact]
    public async Task A_trusted_start_whose_only_candidate_is_the_maker_is_refused_and_writes_nothing()
    {
        var templateId = await _store.PublishedTemplateAsync();

        var started = await _store.Coordinator().StartAsync(
            TrustedRequest(templateId, "maker-only", [TrustedMaker.ToString("N").ToUpperInvariant()]), TrustedClient,
            TrustedMaker, "corr-mo");

        Assert.Equal(400, started.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowNoEligibleApprover, started.ReasonCode);
        Assert.Null(await _store.Instances.GetByIdempotencyKeyAsync("maker-only"));
    }

    [Fact]
    public async Task An_intermediate_approval_whose_next_step_names_only_the_starter_is_refused_and_does_not_advance()
    {
        // step-b's only candidate is NextApprover — who started this instance.
        var seeded = await _store.SeedAsync([D(First)], twoSteps: true, startedBy: NextApprover);

        var response = await _store.ApproveAsync(seeded.Task.Id, D(First), "next-is-starter");

        Assert.Equal(409, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowNoEligibleApprover, response.ReasonCode);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!.Status);
        Assert.Single(await _store.Tasks.ListByInstanceIdAsync(seeded.Instance.Id));
        Assert.Equal("step-a", (await _store.Instances.GetByIdAsync(seeded.Instance.Id))!.CurrentStep);
        Assert.Empty(await _store.Logs.ListByInstanceIdAsync(seeded.Instance.Id));
    }

    [Fact]
    public async Task An_escalation_to_the_starter_alone_is_not_written_and_the_task_stays()
    {
        var seeded = await _store.SeedAsync([D(First)], dueAt: DateTimeOffset.UtcNow.AddMinutes(-5), startedBy: CandidateA);
        await _store.EscalationRuleAsync(seeded.Instance.TemplateId, [D(CandidateA)]);

        var run = await _store.RunEscalationsAsync();

        var result = run.Results.Single(r => r.ApprovalTaskId == seeded.Task.Id);
        Assert.Equal(WorkflowReasonCodes.WorkflowNoEligibleApprover, result.ReasonCode);
        Assert.Equal(0, run.EscalatedCount);
        var task = (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!;
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, task.Status);
        Assert.Equal(seeded.Snapshot.Id, task.AssignmentSnapshotId);
        Assert.Single(await _store.Snapshots.ListByInstanceIdAsync(seeded.Instance.Id));
    }

    [Fact]
    public async Task An_escalation_drops_the_starter_and_goes_to_the_others()
    {
        var seeded = await _store.SeedAsync([D(First)], dueAt: DateTimeOffset.UtcNow.AddMinutes(-5), startedBy: CandidateA);
        await _store.EscalationRuleAsync(seeded.Instance.TemplateId, [D(CandidateA), D(CandidateB)]);

        var run = await _store.RunEscalationsAsync();

        Assert.Equal(WorkflowReasonCodes.WorkflowEscalationProcessed,
            run.Results.Single(r => r.ApprovalTaskId == seeded.Task.Id).ReasonCode);
        var task = (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!;
        Assert.Equal([D(CandidateB)], (await _store.Snapshots.GetByIdAsync(task.AssignmentSnapshotId!.Value))!.CandidatePrincipalIds);
    }

    // ── 3 · a trusted start records its starter as a user id; the starter check holds in any spelling ───────────

    [Fact]
    public async Task A_trusted_start_records_the_maker_as_its_starter_and_the_starter_check_matches_any_spelling()
    {
        var templateId = await _store.PublishedTemplateAsync();
        var started = await _store.Coordinator().StartAsync(
            TrustedRequest(templateId, "starter-id", [D(First)]), TrustedClient, TrustedMaker, "corr-sid");
        Assert.True(started.IsSuccessful, started.ReasonCode);

        var instance = (await _store.Instances.GetByIdAsync(started.Data!.WorkflowInstanceId))!;

        Assert.Equal(TrustedMaker, instance.StartedByUserId);
        Assert.True(WorkflowTaskTransitionSupport.IsStarter(instance, TrustedMaker.ToString("N").ToUpperInvariant()));
        // An instance from before D2 (no user id; StartedBy is the maker's id as text) — also any spelling now.
        instance.StartedByUserId = null;
        Assert.True(WorkflowTaskTransitionSupport.IsStarter(instance, TrustedMaker.ToString("B")));
    }

    // ── 4 · the fingerprint names the DECLARED candidates: a seat change does not turn a retry into a 409 ────────

    [Fact]
    public async Task A_trusted_replay_after_the_seat_changed_hands_returns_the_same_instance()
    {
        var templateId = await _store.PublishedTemplateAsync();
        var seat = await _store.SeatHeldByAsync(First);
        string[] declared = ["position:" + seat.Position.ToString("D")];

        var started = await _store.Coordinator(withPositions: true).StartAsync(
            TrustedRequest(templateId, "seat-change", declared), TrustedClient, TrustedMaker, "corr-seat");
        Assert.True(started.IsSuccessful, started.ReasonCode);
        Assert.Equal([D(First)], (await _store.Snapshots.GetByIdAsync(started.Data!.AssignmentSnapshotId))!.CandidatePrincipalIds);

        await _store.HandSeatOverAsync(seat, Second);
        var replay = await _store.Coordinator(withPositions: true).StartAsync(
            TrustedRequest(templateId, "seat-change", declared), TrustedClient, TrustedMaker, "corr-seat-2");

        Assert.True(replay.IsSuccessful, replay.ReasonCode);
        Assert.True(replay.Data!.IsReplay);
        Assert.Equal(started.Data.WorkflowInstanceId, replay.Data.WorkflowInstanceId);
        // The stored snapshot is the fact; nothing was resolved again.
        Assert.Equal([D(First)], (await _store.Snapshots.GetByIdAsync(started.Data.AssignmentSnapshotId))!.CandidatePrincipalIds);

        var different = await _store.Coordinator(withPositions: true).StartAsync(
            TrustedRequest(templateId, "seat-change", [D(Second)]), TrustedClient, TrustedMaker, "corr-seat-3");
        Assert.Equal(409, different.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowStartIdempotencyConflict, different.ReasonCode);
    }

    // ── 5 · an approval that was ever escalated stays out of reach of the cancel endpoints ──────────────────────

    [Fact]
    public async Task A_delegation_after_an_escalation_does_not_reopen_the_public_cancel()
    {
        var seeded = await _store.SeedAsync([D(First)], escalatedTo: [D(Second)]);
        Assert.True((await _store.DelegateAsync(seeded.Task.Id, D(Second), D(NextApprover), "after-escalation")).IsSuccessful);
        var task = (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!;
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, task.Status); // non-vacuity: the status says "open"
        Assert.True(task.EscalationLevel > 0);

        var cancelled = await _store.CancelAsync(seeded.Task.Id, "cancel-after-escalation");

        Assert.Equal(409, cancelled.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowTaskInvalidState, cancelled.ReasonCode);
        // The owning module's own withdraw (allowEscalated) still calls it off.
        Assert.True((await _store.CancelAsync(seeded.Task.Id, "owner-withdraw", allowEscalated: true)).IsSuccessful);
    }

    [Fact]
    public async Task A_delegation_after_an_escalation_does_not_reopen_the_trusted_cancel()
    {
        var seeded = await _store.SeedAsync([D(First)], escalatedTo: [D(Second)],
            trustedCheckpoint: WorkflowStartCheckpoint.Completed);
        Assert.True((await _store.DelegateAsync(seeded.Task.Id, D(Second), D(NextApprover), "after-escalation-t")).IsSuccessful);
        // A trusted instance names the person the consumer started it for (the seed leaves it out).
        await _store.Context.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances).UpdateOneAsync(
            i => i.Id == seeded.Instance.Id, Builders<WorkflowInstance>.Update.Set(i => i.DelegatedMakerUserId, TrustedMaker));
        var instance = (await _store.Instances.GetByIdAsync(seeded.Instance.Id))!;
        var task = (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!;
        Assert.Equal(WorkflowInstanceStatus.Active, instance.Status);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, task.Status);
        var coordinator = new TrustedWorkflowCancellationCoordinator(
            _store.Instances, _store.Tasks, _store.Logs, new PlatformTransactionExecutor(_store.Context));

        var preflight = await coordinator.PreflightAsync(instance.TrustedConsumerClientId!.Value, instance.Id, task.Id,
            instance.ObjectType, instance.ObjectId, instance.DelegatedMakerUserId!.Value, "corr-pf", default);
        var cancel = await coordinator.CancelAsync(instance.TrustedConsumerClientId!.Value, instance.DelegatedMakerUserId!.Value,
            new TrustedWorkflowCancellationRequest(instance.Id, task.Id, instance.ObjectType, instance.ObjectId,
                instance.DelegatedMakerUserId!.Value, instance.Version, task.Version, "WITHDRAWN", null, "t-cancel"),
            "corr-tc", default);

        Assert.Equal(404, preflight.StatusCode); // what an escalated task answers today
        Assert.Equal(409, cancel.StatusCode);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, (await _store.Tasks.GetByIdAsync(task.Id))!.Status);
    }

    // ── 6 (C5) · no cancel on a trusted start that has not finished ─────────────────────────────────────────────

    [Fact]
    public async Task The_public_cancel_of_an_unfinished_trusted_start_is_409_and_takes_no_sequence_number()
    {
        var seeded = await _store.SeedAsync([D(First)], trustedCheckpoint: WorkflowStartCheckpoint.AssignmentSnapshotPersisted);

        var cancelled = await _store.CancelAsync(seeded.Task.Id, "cancel-unfinished");

        Assert.Equal(409, cancelled.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowStartNotCompleted, cancelled.ReasonCode);
        Assert.Empty(await _store.Logs.ListByInstanceIdAsync(seeded.Instance.Id));
    }

    // ── 7 (C4) · a stored delegation is replayed before the target rule is asked ────────────────────────────────

    [Fact]
    public async Task A_replay_of_a_delegation_stored_before_the_target_rule_returns_its_stored_answer()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        // A delegation written before D1-FIX1 named a free-text target; its log row is what a retry finds.
        await _store.Logs.CreateAsync(new WorkflowTransitionLog
        {
            TenantId = _store.TenantContext.TenantId,
            WorkflowInstanceId = seeded.Instance.Id,
            ApprovalTaskId = seeded.Task.Id,
            Action = WorkflowTransitionAction.Delegate,
            FromState = ApprovalTaskStatus.WaitingApproval.ToString(),
            ToState = ApprovalTaskStatus.WaitingApproval.ToString(),
            FromStatus = WorkflowInstanceStatus.Active.ToString(),
            ToStatus = WorkflowInstanceStatus.Active.ToString(),
            ActorId = D(Second),
            ActorRef = D(Second),
            ReasonCode = "DELEGATED",
            IdempotencyKey = "legacy-free-text",
            SequenceNo = 1
        });

        var replay = await _store.DelegateAsync(seeded.Task.Id, D(Second), "the finance team", "legacy-free-text");

        Assert.True(replay.IsSuccessful, replay.ReasonCode);
        Assert.True(replay.Data!.IsIdempotent);
        // A NEW delegation with that target is still refused.
        Assert.Equal(400, (await _store.DelegateAsync(seeded.Task.Id, D(Second), "the finance team", "new-free-text")).StatusCode);
    }

    // ── 8 (C1) · two escalation runs at once count one escalation ───────────────────────────────────────────────

    [Fact]
    public async Task Two_escalation_runs_sharing_the_system_actor_and_key_count_the_escalation_once()
    {
        var seeded = await _store.SeedAsync([D(First)], dueAt: DateTimeOffset.UtcNow.AddMinutes(-5));
        await _store.EscalationRuleAsync(seeded.Instance.TemplateId, [D(CandidateA)]);
        RunWorkflowEscalationsResponse? other = null;
        // The other run escalates after this run's own replay check; this run is then told "unavailable".
        var executor = new ReportsUnavailableAfter(async () => other = await _store.RunEscalationsAsync());

        var mine = await _store.RunEscalationsAsync(executor);

        Assert.NotNull(other);
        Assert.Equal(1, other!.EscalatedCount + mine.EscalatedCount);
        var replayed = mine.Results.Single(r => r.ApprovalTaskId == seeded.Task.Id);
        Assert.True(replayed.IsIdempotent);
        Assert.Equal(WorkflowReasonCodes.WorkflowEscalationIdempotent, replayed.ReasonCode);
        Assert.Single(await _store.Logs.ListByInstanceIdAsync(seeded.Instance.Id), l => l.Action == WorkflowTransitionAction.Escalate);
    }

    // ── 9 · "my write" means the same request: same actor and key but another payload is not mine ──────────────

    [Fact]
    public async Task Same_key_and_actor_with_another_comment_is_not_my_commit()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        var executor = new ReportsUnavailableAfter(
            () => _store.RejectWithCommentAsync(seeded.Task.Id, D(Second), "same-key", "the first reason"));

        var response = await _store.RejectWithCommentAsync(seeded.Task.Id, D(Second), "same-key", "another reason", executor);

        Assert.Equal(409, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowTransitionConflict, response.ReasonCode);
    }

    [Fact]
    public async Task Same_key_and_actor_delegating_to_another_person_is_not_my_commit()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        var executor = new ReportsUnavailableAfter(
            () => _store.DelegateAsync(seeded.Task.Id, D(Second), D(CandidateA), "same-delegate-key"));

        var response = await _store.DelegateAsync(seeded.Task.Id, D(Second), D(NextApprover), "same-delegate-key", executor);

        Assert.Equal(409, response.StatusCode);
        Assert.Equal(D(CandidateA), (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!.AssigneeRef);
    }

    [Fact]
    public async Task Same_key_actor_and_payload_from_a_twin_call_is_a_success()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        var executor = new ReportsUnavailableAfter(
            () => _store.RejectWithCommentAsync(seeded.Task.Id, D(Second), "twin-key", "the reason"));

        var response = await _store.RejectWithCommentAsync(seeded.Task.Id, D(Second), "twin-key", "the reason", executor);

        Assert.True(response.IsSuccessful, response.ReasonCode);
    }

    // ── 11 · the 503 says what is known: the outcome is not ────────────────────────────────────────────────────

    [Fact]
    public async Task The_unavailable_answer_says_the_outcome_is_unknown_and_a_retry_with_the_same_key_is_safe()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);

        var response = await _store.ApproveAsync(seeded.Task.Id, D(Second), "unknown-outcome",
            new ReportsUnavailableAfter(() => Task.CompletedTask));

        Assert.Equal(503, response.StatusCode);
        var message = Assert.Single(response.Errors);
        Assert.Contains("unknown", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("same idempotency key is safe", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Nothing was written", message, StringComparison.OrdinalIgnoreCase);
    }

    // ── harness (D2) ───────────────────────────────────────────────────────────────────────────────────────────

    private sealed record Seat(Guid Position, Guid Assignment);

    private sealed partial class Store
    {
        public WorkflowInstanceStartCoordinator Coordinator(bool withPositions) =>
            withPositions
                ? new(Templates, Versions, Instances, Tasks, Snapshots, Logs, TenantContext,
                    new PositionAssignmentRepository(Context, TenantContext), null,
                    new PositionRepository(Context, TenantContext))
                : Coordinator();

        public async Task<Seat> SeatHeldByAsync(Guid user)
        {
            var position = new Position
            {
                TenantId = TenantContext.TenantId,
                Code = "SEAT-" + Guid.NewGuid().ToString("N")[..8],
                Name = "Approving seat",
                OrganizationUnitId = Guid.NewGuid(),
                Status = PositionStatus.Active
            };
            await Context.Database.GetCollection<Position>(PlatformCollections.Positions).InsertOneAsync(position);
            var assignment = new PositionAssignment
            {
                TenantId = TenantContext.TenantId,
                PositionId = position.Id,
                UserId = user,
                EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-30)
            };
            await Context.Database.GetCollection<PositionAssignment>(PlatformCollections.PositionAssignments).InsertOneAsync(assignment);
            return new Seat(position.Id, assignment.Id);
        }

        /// <summary>The holder leaves (the assignment ends) and <paramref name="next"/> takes the seat.</summary>
        public async Task HandSeatOverAsync(Seat seat, Guid next)
        {
            await Context.Database.GetCollection<PositionAssignment>(PlatformCollections.PositionAssignments).UpdateOneAsync(
                a => a.Id == seat.Assignment,
                Builders<PositionAssignment>.Update.Set(a => a.EffectiveTo, DateTimeOffset.UtcNow.AddMinutes(-1)));
            await Context.Database.GetCollection<PositionAssignment>(PlatformCollections.PositionAssignments).InsertOneAsync(
                new PositionAssignment
                {
                    TenantId = TenantContext.TenantId,
                    PositionId = seat.Position,
                    UserId = next,
                    EffectiveFrom = DateTimeOffset.UtcNow.AddMinutes(-1)
                });
        }

        public Task<Diten.Platform.Application.Common.Response<WorkflowTaskTransitionResponse>> CancelAsync(
            Guid taskId, string key, bool allowEscalated = false) =>
            new WorkflowTaskTransitionSupport(Tasks, Instances, Snapshots, Logs,
                    seams: new WorkflowTransitionSeams(null, new PlatformTransactionExecutor(Context),
                        new MongoIntentWriter(Context), Templates))
                .CancelAsync(taskId, D(Outsider), "WITHDRAWN", key, null, "corr-" + key, default, allowEscalated);

        public Task<Diten.Platform.Application.Common.Response<WorkflowTaskTransitionResponse>> RejectWithCommentAsync(
            Guid taskId, string actor, string key, string comment, IPlatformTransactionExecutor? executor = null) =>
            new RejectWorkflowTaskHandler(Tasks, Instances, Snapshots, Logs,
                    transactions: executor ?? new PlatformTransactionExecutor(Context),
                    events: new MongoIntentWriter(Context),
                    templates: Templates)
                .Handle(new RejectWorkflowTaskCommand(taskId,
                    new RejectWorkflowTaskRequest(actor, "REJECTED", key, comment, null), "corr-" + key), default);
    }
}

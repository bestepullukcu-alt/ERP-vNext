using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Services;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

/// <summary>
/// WP-WF-ENGINE-CANDIDATES-01 · D2 fix round 3 (CT-WF-D2-FIX3). Real replica set, production handlers and repositories.
/// </summary>
public sealed partial class WorkflowCandidateDecisionMongoTests
{
    // ── 4 · a realistic rule escalates at its threshold, whatever standing backlog the tenant has ────────────────

    [Fact]
    public async Task Behind_a_page_of_standing_backlog_a_realistic_rule_escalates_at_its_threshold()
    {
        // Escalate two hours AFTER the due date — the shape real rules have (the earlier helper put the threshold on the
        // due date, so the resting path was never taken). And 101 overdue tasks with no rule: more than a page.
        var dueAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var realistic = await _store.SeedAsync([D(First)], dueAt: dueAt);
        await _store.EscalationRuleAfterAsync(realistic.Instance.TemplateId, [D(CandidateB)], escalateAfterMinutes: 130);
        var ruleless = await _store.SeedAsync([D(First)], dueAt: DateTimeOffset.UtcNow.AddDays(-30));
        await _store.InsertTasksOnAsync(ruleless.Instance.Id, 100, DateTimeOffset.UtcNow.AddDays(-30));

        var first = await _store.SweepAtAsync(TimeSpan.Zero, maxItems: 100);
        var second = await _store.SweepAtAsync(TimeSpan.FromMinutes(5), maxItems: 100);
        var lookAt = (await _store.Tasks.GetByIdAsync(realistic.Task.Id))!.EscalationNextLookAtUtc;
        var atThreshold = await _store.SweepAtAsync(TimeSpan.FromMinutes(122), maxItems: 100);

        Assert.DoesNotContain(first.Results, r => r.ApprovalTaskId == realistic.Task.Id); // non-vacuity: the page was full
        Assert.Equal(WorkflowReasonCodes.WorkflowNoOverdueTasks,
            second.Results.Single(r => r.ApprovalTaskId == realistic.Task.Id).ReasonCode);
        // Rested until its threshold — not until a quarter of an hour later, to race the backlog's next return.
        Assert.NotNull(lookAt);
        Assert.InRange(lookAt!.Value, dueAt.UtcDateTime.AddMinutes(120).AddSeconds(-1), dueAt.UtcDateTime.AddMinutes(120).AddSeconds(1));
        Assert.Equal(WorkflowReasonCodes.WorkflowEscalationProcessed,
            atThreshold.Results.Single(r => r.ApprovalTaskId == realistic.Task.Id).ReasonCode);
        Assert.DoesNotContain(atThreshold.Results, r => r.ApprovalTaskId == ruleless.Task.Id); // the backlog still rests
    }

    // ── 8 · the overdue read's due-date order, deterministically (Repos:558-559) ────────────────────────────────

    [Fact]
    public async Task The_overdue_read_orders_tasks_with_the_same_look_time_by_due_date_whatever_their_ids()
    {
        // The ids are chosen to sort OPPOSITE to the due dates, so an order that fell back to the id fails every time.
        var now = DateTimeOffset.UtcNow;
        var dueLaterLowId = await _store.InsertOverdueWithIdAsync(IdStartingWith("00"), now.AddHours(-1), lookAt: null);
        var dueEarlierHighId = await _store.InsertOverdueWithIdAsync(IdStartingWith("ff"), now.AddHours(-2), lookAt: null);
        var lookedAt = now.UtcDateTime.AddMinutes(-5);
        var restedLaterLowId = await _store.InsertOverdueWithIdAsync(IdStartingWith("01"), now.AddHours(-3), lookedAt);
        var restedEarlierHighId = await _store.InsertOverdueWithIdAsync(IdStartingWith("fe"), now.AddHours(-4), lookedAt);

        var read = (await _store.Tasks.ListOverdueTasksAsync(now, 10)).Select(t => t.Id).ToList();

        Assert.Equal([dueEarlierHighId, dueLaterLowId, restedEarlierHighId, restedLaterLowId], read);
    }

    // ── 8 · a stray start task's withdraw reads its write and leaves a transition-log line (Coord:811) ──────────

    [Fact]
    public async Task Withdrawing_a_stray_start_task_records_it_once_in_the_transition_log()
    {
        var templateId = await _store.PublishedTemplateAsync();
        var started = await _store.Coordinator().StartAsync(
            TrustedRequest(templateId, "stray-log", [D(First)]), TrustedClient, TrustedMaker, "corr-sl");
        Assert.True(started.IsSuccessful, started.ReasonCode);
        await _store.CloseAsIfRacedAsync(started.Data!);

        await _store.Coordinator().StartAsync(
            TrustedRequest(templateId, "stray-log", [D(First)]), TrustedClient, TrustedMaker, "corr-sl-2");
        await _store.Coordinator().StartAsync(
            TrustedRequest(templateId, "stray-log", [D(First)]), TrustedClient, TrustedMaker, "corr-sl-3");

        Assert.Equal(ApprovalTaskStatus.Cancelled, (await _store.Tasks.GetByIdAsync(started.Data!.ApprovalTaskId))!.Status);
        var line = Assert.Single(await _store.Logs.ListByInstanceIdAsync(started.Data.WorkflowInstanceId),
            l => l.Action == WorkflowTransitionAction.Cancel);
        Assert.Equal(started.Data.ApprovalTaskId, line.ApprovalTaskId);
        Assert.Equal(WorkflowInstanceStartCoordinator.StartRecoveryActor, line.ActorId);
        Assert.Equal(WorkflowReasonCodes.WorkflowNoEligibleApprover, line.ReasonCode);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval.ToString(), line.FromState);
        Assert.Equal(ApprovalTaskStatus.Cancelled.ToString(), line.ToState);
        Assert.Equal("corr-sl-2", line.CorrelationId);
    }

    [Fact]
    public async Task A_stray_withdraw_whose_write_loses_its_race_records_nothing()
    {
        var templateId = await _store.PublishedTemplateAsync();
        var started = await _store.Coordinator().StartAsync(
            TrustedRequest(templateId, "stray-lost", [D(First)]), TrustedClient, TrustedMaker, "corr-slo");
        Assert.True(started.IsSuccessful, started.ReasonCode);
        await _store.CloseAsIfRacedAsync(started.Data!);

        await _store.CoordinatorWith(tasks: new TaskWritesLose(_store.Tasks)).StartAsync(
            TrustedRequest(templateId, "stray-lost", [D(First)]), TrustedClient, TrustedMaker, "corr-slo-2");

        Assert.Equal(ApprovalTaskStatus.WaitingApproval, (await _store.Tasks.GetByIdAsync(started.Data!.ApprovalTaskId))!.Status);
        Assert.DoesNotContain(await _store.Logs.ListByInstanceIdAsync(started.Data.WorkflowInstanceId),
            l => l.Action == WorkflowTransitionAction.Cancel);
    }

    // ── 7 · an empty seat is transient on the FIRST call too ────────────────────────────────────────────────────

    [Fact]
    public async Task A_first_call_that_finds_the_seat_empty_answers_503_reserves_nothing_and_its_retry_completes()
    {
        var templateId = await _store.PublishedTemplateAsync();
        var seat = await _store.SeatHeldByAsync(First);
        await _store.VacateSeatAsync(seat);
        string[] declared = ["position:" + seat.Position.ToString("D")];

        var first = await _store.Coordinator(withPositions: true).StartAsync(
            TrustedRequest(templateId, "first-empty", declared), TrustedClient, TrustedMaker, "corr-fe");

        // The answer a replay of a reserved key gives for the same condition: transient, same contract code.
        Assert.Equal(503, first.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowAssignmentCandidatesRequired, first.ReasonCode);
        Assert.Null(await _store.Instances.GetByIdempotencyKeyAsync("first-empty"));   // nothing reserved

        await _store.FillSeatAsync(seat.Position, Second);
        var retry = await _store.Coordinator(withPositions: true).StartAsync(
            TrustedRequest(templateId, "first-empty", declared), TrustedClient, TrustedMaker, "corr-fe-2");

        Assert.True(retry.IsSuccessful, retry.ReasonCode);
        Assert.Equal(201, retry.StatusCode);
        Assert.Equal(D(Second), (await _store.Tasks.GetByIdAsync(retry.Data!.ApprovalTaskId))!.AssigneeRef);
    }

    // ── harness (D2-FIX3) ──────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A fresh id whose stored (Standard, RFC-byte) order is set by its first hex byte.</summary>
    private static Guid IdStartingWith(string firstByte) => Guid.Parse(firstByte + Guid.NewGuid().ToString("N")[2..]);

    private sealed partial class Store
    {
        public async Task<Guid> InsertOverdueWithIdAsync(Guid id, DateTimeOffset dueAt, DateTime? lookAt)
        {
            var task = new ApprovalTask
            {
                Id = id,
                TenantId = TenantContext.TenantId,
                WorkflowInstanceId = Guid.NewGuid(),
                Status = ApprovalTaskStatus.WaitingApproval,
                DueAt = dueAt,
                EscalationNextLookAtUtc = lookAt
            };
            await Context.Database.GetCollection<ApprovalTask>(PlatformCollections.ApprovalTasks).InsertOneAsync(task);
            return task.Id;
        }
    }

    /// <summary>Every task write loses its version check (somebody moved the task first); reads go to the store.</summary>
    private sealed class TaskWritesLose(IApprovalTaskRepository inner) : IApprovalTaskRepository
    {
        public Task<ApprovalTask> CreateAsync(ApprovalTask task, CancellationToken ct = default) => inner.CreateAsync(task, ct);
        public Task<ApprovalTask?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        public Task<ApprovalTask?> GetFirstByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            inner.GetFirstByInstanceIdAsync(workflowInstanceId, ct);
        public Task<ApprovalTask?> GetActiveByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            inner.GetActiveByInstanceIdAsync(workflowInstanceId, ct);
        public Task<IReadOnlyList<ApprovalTask>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            inner.ListByInstanceIdAsync(workflowInstanceId, ct);
        public Task<IReadOnlyList<ApprovalTask>> GetAllForTenantAsync(CancellationToken ct = default) => inner.GetAllForTenantAsync(ct);
        public Task<bool> UpdateAsync(ApprovalTask task, int expectedVersion, CancellationToken ct = default) => Task.FromResult(false);
    }
}

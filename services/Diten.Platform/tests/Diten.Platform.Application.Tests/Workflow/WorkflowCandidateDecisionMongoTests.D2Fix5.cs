using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Services;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

/// <summary>
/// WP-WF-ENGINE-CANDIDATES-01 · D2 fix round 5 (CT-WF-D2-FIX5), item 6. Real replica set, production handlers and repositories.
/// </summary>
public sealed partial class WorkflowCandidateDecisionMongoTests
{
    // ── the stray-task withdraw's log append: five tries, and a twin that already wrote ──────────────────────────

    [Fact]
    public async Task A_stray_withdraws_log_append_that_keeps_failing_is_tried_five_times_and_then_fails_loudly()
    {
        var templateId = await _store.PublishedTemplateAsync();
        var started = await _store.Coordinator().StartAsync(
            TrustedRequest(templateId, "stray-five", [D(First)]), TrustedClient, TrustedMaker, "corr-s5");
        Assert.True(started.IsSuccessful, started.ReasonCode);
        await _store.CloseAsIfRacedAsync(started.Data!);
        var failing = new LogsWhoseCancelLineCannotBeWritten(_store.Logs);

        // The line's sequence number keeps being taken (or the store keeps failing) and no twin wrote it: after the bound the
        // failure is NOT hidden — the replay finds the task cancelled and would never write the line again.
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => _store.CoordinatorWith(logs: failing).StartAsync(
            TrustedRequest(templateId, "stray-five", [D(First)]), TrustedClient, TrustedMaker, "corr-s5-2"));

        Assert.Contains("log store", thrown.Message, StringComparison.Ordinal);
        Assert.Equal(5, failing.Attempts);                                                // the literal: the bound is part of the contract
        Assert.Equal(WorkflowInstanceStartCoordinator.MaxStrayLogAttempts, failing.Attempts);
        Assert.Equal(ApprovalTaskStatus.Cancelled, (await _store.Tasks.GetByIdAsync(started.Data!.ApprovalTaskId))!.Status);
        Assert.DoesNotContain(await _store.Logs.ListByInstanceIdAsync(started.Data.WorkflowInstanceId),
            l => l.Action == WorkflowTransitionAction.Cancel);
    }

    [Fact]
    public async Task A_stray_withdraws_log_append_that_meets_the_twins_line_stops_at_once_and_the_line_exists_once()
    {
        var templateId = await _store.PublishedTemplateAsync();
        var started = await _store.Coordinator().StartAsync(
            TrustedRequest(templateId, "stray-twin", [D(First)]), TrustedClient, TrustedMaker, "corr-tw");
        Assert.True(started.IsSuccessful, started.ReasonCode);
        await _store.CloseAsIfRacedAsync(started.Data!);
        var twin = new LogsWhoseTwinWroteTheCancelLineFirst(_store.Logs);

        var answer = await _store.CoordinatorWith(logs: twin).StartAsync(
            TrustedRequest(templateId, "stray-twin", [D(First)]), TrustedClient, TrustedMaker, "corr-tw-2");

        // The closed reservation's own answer — the twin's line is the record, and it is not written a second time.
        Assert.Equal(400, answer.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowNoEligibleApprover, answer.ReasonCode);
        Assert.Equal(1, twin.Attempts);                                                   // one failed append, no retry after finding the twin
        Assert.Single(await _store.Logs.ListByInstanceIdAsync(started.Data!.WorkflowInstanceId),
            l => l.Action == WorkflowTransitionAction.Cancel);
    }

    /// <summary>The transition log, except that the stray task's Cancel line can never be written (the store is down for it).</summary>
    private sealed class LogsWhoseCancelLineCannotBeWritten(IWorkflowTransitionLogRepository inner) : IWorkflowTransitionLogRepository
    {
        public int Attempts { get; private set; }

        public Task<WorkflowTransitionLog> CreateAsync(WorkflowTransitionLog log, CancellationToken ct = default)
        {
            if (log.Action == WorkflowTransitionAction.Cancel)
            {
                Attempts++;
                throw new InvalidOperationException("The log store could not be written.");
            }

            return inner.CreateAsync(log, ct);
        }

        public Task<WorkflowTransitionLog?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);

        public Task<WorkflowTransitionLog?> GetByTaskActionIdempotencyKeyAsync(
            Guid approvalTaskId, WorkflowTransitionAction action, string idempotencyKey, CancellationToken ct = default) =>
            inner.GetByTaskActionIdempotencyKeyAsync(approvalTaskId, action, idempotencyKey, ct);

        public Task<long> GetLatestSequenceNoAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            inner.GetLatestSequenceNoAsync(workflowInstanceId, ct);

        public Task<IReadOnlyList<WorkflowTransitionLog>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            inner.ListByInstanceIdAsync(workflowInstanceId, ct);
    }

    /// <summary>
    /// The transition log, except that a twin replay writes the stray task's Cancel line first: the first append arrives after the
    /// twin's line exists (it is written here, with the same key) and fails the way the unique index fails it.
    /// </summary>
    private sealed class LogsWhoseTwinWroteTheCancelLineFirst(IWorkflowTransitionLogRepository inner) : IWorkflowTransitionLogRepository
    {
        public int Attempts { get; private set; }

        public async Task<WorkflowTransitionLog> CreateAsync(WorkflowTransitionLog log, CancellationToken ct = default)
        {
            if (log.Action != WorkflowTransitionAction.Cancel)
            {
                return await inner.CreateAsync(log, ct);
            }

            Attempts++;
            await inner.CreateAsync(new WorkflowTransitionLog
            {
                TenantId = log.TenantId,
                WorkflowInstanceId = log.WorkflowInstanceId,
                ApprovalTaskId = log.ApprovalTaskId,
                Action = log.Action,
                FromState = log.FromState,
                ToState = log.ToState,
                ActorId = log.ActorId,
                ActorRef = log.ActorRef,
                ReasonCode = log.ReasonCode,
                IdempotencyKey = log.IdempotencyKey,
                CorrelationId = "the-twin",
                SequenceNo = log.SequenceNo
            }, ct);
            throw new InvalidOperationException("Duplicate key: the twin replay wrote this line first.");
        }

        public Task<WorkflowTransitionLog?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);

        public Task<WorkflowTransitionLog?> GetByTaskActionIdempotencyKeyAsync(
            Guid approvalTaskId, WorkflowTransitionAction action, string idempotencyKey, CancellationToken ct = default) =>
            inner.GetByTaskActionIdempotencyKeyAsync(approvalTaskId, action, idempotencyKey, ct);

        public Task<long> GetLatestSequenceNoAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            inner.GetLatestSequenceNoAsync(workflowInstanceId, ct);

        public Task<IReadOnlyList<WorkflowTransitionLog>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            inner.ListByInstanceIdAsync(workflowInstanceId, ct);
    }
}

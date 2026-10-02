using System.Security.Cryptography;
using System.Text;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Features.Workflow.Events;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;

internal enum WorkflowTerminalWriteOutcome
{
    Committed,
    Conflict,
    TransactionUnavailable
}

internal sealed record WorkflowTerminalWriteResult(WorkflowTerminalWriteOutcome Outcome, WorkflowTransitionLog? Log);

/// <summary>
/// WP-CL-BE-3 — the ONE place a workflow instance becomes terminal. The task update, the instance update, the
/// transition log and the <see cref="WorkflowInstanceCompletedV1"/> outbox intent run in one Platform transaction
/// (<see cref="IPlatformTransactionExecutor"/>): an optimistic-version conflict on either document aborts the whole
/// write, so a half-applied terminal transition — or a terminal state without its event — cannot be committed.
/// <para>
/// Without an executor and an event writer (in-memory test doubles that predate this WP) the writer keeps the
/// pre-existing sequential writes and emits no event; production DI always supplies both.
/// </para>
/// </summary>
internal sealed class WorkflowTerminalTransitionWriter
{
    private const string Producer = "Diten.Platform";

    private readonly IApprovalTaskRepository _tasks;
    private readonly IWorkflowInstanceRepository _instances;
    private readonly IWorkflowTransitionLogRepository _logs;
    private readonly IPlatformTransactionExecutor? _transactions;
    private readonly ITransactionalIntegrationEventWriter? _events;
    private readonly IWorkflowTemplateRepository? _templates;
    private readonly IRuntimeAssignmentSnapshotRepository? _snapshots;

    public WorkflowTerminalTransitionWriter(
        IApprovalTaskRepository tasks,
        IWorkflowInstanceRepository instances,
        IWorkflowTransitionLogRepository logs,
        IPlatformTransactionExecutor? transactions,
        ITransactionalIntegrationEventWriter? events,
        IWorkflowTemplateRepository? templates,
        IRuntimeAssignmentSnapshotRepository? snapshots = null)
    {
        _snapshots = snapshots;
        _tasks = tasks;
        _instances = instances;
        _logs = logs;
        _transactions = transactions;
        _events = events;
        _templates = templates;
    }

    public async Task<WorkflowTerminalWriteResult> CommitAsync(
        ApprovalTask task,
        int taskVersion,
        WorkflowInstance instance,
        int instanceVersion,
        WorkflowTransitionLog log,
        bool escalationWrite,
        string? completedBy,
        string? reasonCode,
        string correlationId,
        CancellationToken ct)
    {
        var outcome = WorkflowOutcomes.For(instance.Status)
            ?? throw new InvalidOperationException("Only a terminal instance status can be committed here.");

        if (_transactions is null || _events is null)
        {
            return await CommitSequentiallyAsync(task, taskVersion, instance, instanceVersion, log, escalationWrite, ct);
        }

        var completedEvent = await BuildEventAsync(instance, task, outcome, completedBy, reasonCode, correlationId, ct);
        try
        {
            var created = await _transactions.ExecuteAsync(async (session, tct) =>
            {
                if (!await _tasks.UpdateAsync(session, task, taskVersion, tct) ||
                    !await _instances.UpdateAsync(session, instance, instanceVersion, tct))
                {
                    throw new TerminalConflictException();
                }

                var createdLog = await _logs.CreateAsync(session, log, tct);
                await _events.EnqueueAsync(session, completedEvent, new EventPublishOptions
                {
                    EventId = completedEvent.EventId,
                    CorrelationId = completedEvent.CorrelationId,
                    TenantId = completedEvent.TenantId,
                    Producer = Producer,
                    OccurredAtUtc = completedEvent.OccurredAtUtc
                }, tct);
                return createdLog;
            }, ct);
            return new WorkflowTerminalWriteResult(WorkflowTerminalWriteOutcome.Committed, created);
        }
        catch (TerminalConflictException)
        {
            return new WorkflowTerminalWriteResult(WorkflowTerminalWriteOutcome.Conflict, null);
        }
        catch (PlatformTransactionUnavailableException)
        {
            return new WorkflowTerminalWriteResult(await LostToAnotherDecisionAsync(task.Id, taskVersion, ct)
                ? WorkflowTerminalWriteOutcome.Conflict
                : WorkflowTerminalWriteOutcome.TransactionUnavailable, null);
        }
    }

    /// <summary>
    /// D1 — an INTERMEDIATE approval: the decided task, the instance moved to the next step, the next step's task and
    /// assignment snapshot, and both log rows commit in ONE Platform transaction — the same executor and the same
    /// optimistic-version rule as <see cref="CommitAsync"/>. No completion event: the instance is not terminal.
    /// <para>
    /// The transaction body may be replayed after a transient write conflict (two candidates deciding at once): the
    /// replay finds the task's version already taken and ends as <see cref="WorkflowTerminalWriteOutcome.Conflict"/>.
    /// Without an executor (in-memory test doubles) the writes stay sequential, in the order they always had.
    /// </para>
    /// </summary>
    public async Task<WorkflowTerminalWriteResult> CommitStepAsync(
        ApprovalTask task,
        int taskVersion,
        WorkflowInstance instance,
        int instanceVersion,
        WorkflowTransitionLog log,
        ApprovalTask nextTask,
        RuntimeAssignmentSnapshot nextSnapshot,
        WorkflowTransitionLog nextTaskLog,
        CancellationToken ct)
    {
        if (WorkflowOutcomes.For(instance.Status) is not null)
        {
            throw new InvalidOperationException("A terminal instance status is committed by CommitAsync, with its event.");
        }

        if (_snapshots is null)
        {
            throw new InvalidOperationException("An intermediate step needs the assignment snapshot repository.");
        }

        if (_transactions is null)
        {
            if (!await _tasks.UpdateAsync(task, taskVersion, ct) ||
                !await _instances.UpdateAsync(instance, instanceVersion, ct))
            {
                return new WorkflowTerminalWriteResult(WorkflowTerminalWriteOutcome.Conflict, null);
            }

            await _tasks.CreateAsync(nextTask, ct);
            await _snapshots.CreateAsync(nextSnapshot, ct);
            var sequential = await _logs.CreateAsync(log, ct);
            await _logs.CreateAsync(nextTaskLog, ct);
            return new WorkflowTerminalWriteResult(WorkflowTerminalWriteOutcome.Committed, sequential);
        }

        try
        {
            var created = await _transactions.ExecuteAsync(async (session, tct) =>
            {
                if (!await _tasks.UpdateAsync(session, task, taskVersion, tct) ||
                    !await _instances.UpdateAsync(session, instance, instanceVersion, tct))
                {
                    throw new TerminalConflictException();
                }

                await _tasks.CreateAsync(session, nextTask, tct);
                await _snapshots.CreateAsync(session, nextSnapshot, tct);
                var createdLog = await _logs.CreateAsync(session, log, tct);
                await _logs.CreateAsync(session, nextTaskLog, tct);
                return createdLog;
            }, ct);
            return new WorkflowTerminalWriteResult(WorkflowTerminalWriteOutcome.Committed, created);
        }
        catch (TerminalConflictException)
        {
            return new WorkflowTerminalWriteResult(WorkflowTerminalWriteOutcome.Conflict, null);
        }
        catch (PlatformTransactionUnavailableException)
        {
            return new WorkflowTerminalWriteResult(await LostToAnotherDecisionAsync(task.Id, taskVersion, ct)
                ? WorkflowTerminalWriteOutcome.Conflict
                : WorkflowTerminalWriteOutcome.TransactionUnavailable, null);
        }
    }

    /// <summary>
    /// D1 — two candidates deciding at once. The loser's transaction is aborted by MongoDB's write conflict while the
    /// winner's is still open. The executor replays the body at once, three times, with no pause — measured
    /// 2026-10-02: all three replays can run into the same still-open winner, and the executor then reports
    /// "transaction unavailable" (the inner error is the labelled, transient WriteConflict 112, not an outage).
    /// Nothing was written either way — but "try again later" is the wrong answer when somebody else has just decided
    /// the task. So the task is read again, briefly: once its version has moved, this caller LOST (409). If it never
    /// moves, the transaction really was unavailable (503) — at the cost of a short wait on that rare path.
    /// </summary>
    private async Task<bool> LostToAnotherDecisionAsync(Guid taskId, int expectedVersion, CancellationToken ct)
    {
        try
        {
            for (var attempt = 0; attempt < LostRaceReadAttempts; attempt++)
            {
                var current = await _tasks.GetByIdAsync(taskId, ct);
                if (current is not null && current.Version != expectedVersion)
                {
                    return true;
                }

                await Task.Delay(LostRaceReadDelay, ct);
            }

            return false;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return false; // cannot tell → keep the honest "unavailable"
        }
    }

    private const int LostRaceReadAttempts = 6;
    private static readonly TimeSpan LostRaceReadDelay = TimeSpan.FromMilliseconds(100);

    /// <summary>The deterministic event id of an instance's completion: one instance, one id, one event.</summary>
    public static Guid CompletionEventId(Guid workflowInstanceId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{WorkflowInstanceCompletedV1.Name}:{workflowInstanceId:D}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    private async Task<WorkflowInstanceCompletedV1> BuildEventAsync(
        WorkflowInstance instance,
        ApprovalTask task,
        string outcome,
        string? completedBy,
        string? reasonCode,
        string correlationId,
        CancellationToken ct)
    {
        var templateCode = instance.TemplateCode;
        if (string.IsNullOrWhiteSpace(templateCode) && _templates is not null)
        {
            // Instances started before the TemplateCode snapshot existed: read the definition's key once.
            var templateId = instance.TemplateId == Guid.Empty ? instance.WorkflowTemplateId : instance.TemplateId;
            templateCode = (await _templates.GetByIdAsync(templateId, ct))?.TemplateCode;
        }

        var completedAt = instance.CompletedAt ?? DateTimeOffset.UtcNow;
        return new WorkflowInstanceCompletedV1(
            CompletionEventId(instance.Id),
            completedAt,
            instance.TenantId,
            Guid.TryParse(correlationId, out var correlation) ? correlation : Guid.NewGuid(),
            instance.Id,
            string.IsNullOrWhiteSpace(templateCode) ? null : templateCode,
            instance.TemplateVersionId,
            instance.ObjectType,
            instance.ObjectId,
            instance.ObjectRef,
            outcome,
            completedAt,
            string.IsNullOrWhiteSpace(completedBy) ? null : completedBy,
            task.StageCode,
            task.StepCode,
            string.IsNullOrWhiteSpace(reasonCode) ? null : reasonCode);
    }

    // Pre-WP-CL-BE-3 behaviour, kept byte-for-byte for callers without transaction/event seams.
    private async Task<WorkflowTerminalWriteResult> CommitSequentiallyAsync(
        ApprovalTask task,
        int taskVersion,
        WorkflowInstance instance,
        int instanceVersion,
        WorkflowTransitionLog log,
        bool escalationWrite,
        CancellationToken ct)
    {
        var updated = escalationWrite
            ? await _tasks.UpdateEscalationAsync(task, taskVersion, ct) &&
              await _instances.UpdateEscalationOrTimeoutAsync(instance, instanceVersion, ct)
            : await _tasks.UpdateAsync(task, taskVersion, ct) &&
              await _instances.UpdateAsync(instance, instanceVersion, ct);
        if (!updated)
        {
            return new WorkflowTerminalWriteResult(WorkflowTerminalWriteOutcome.Conflict, null);
        }

        var created = escalationWrite ? await _logs.AppendAsync(log, ct) : await _logs.CreateAsync(log, ct);
        return new WorkflowTerminalWriteResult(WorkflowTerminalWriteOutcome.Committed, created);
    }

    private sealed class TerminalConflictException : Exception;
}

/// <summary>
/// WP-CL-BE-3 — the optional seams a transition needs beyond its repositories: position status for candidate
/// resolution, and the transaction + outbox writer for terminal transitions. Handlers receive them from DI (all are
/// registered in production) and pass them through; null keeps the pre-existing behaviour for older test doubles.
/// </summary>
internal sealed record WorkflowTransitionSeams(
    IPositionRepository? Positions,
    IPlatformTransactionExecutor? Transactions,
    ITransactionalIntegrationEventWriter? Events,
    IWorkflowTemplateRepository? Templates);

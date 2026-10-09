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

/// <param name="Replayed">D2 (C1) — Committed, but by a TWIN of this call (same actor, key and payload, another log row),
/// found when the writer looked again after "unavailable". This call wrote nothing: a caller that counts writes (the
/// SLA runner) must not count it.</param>
internal sealed record WorkflowTerminalWriteResult(
    WorkflowTerminalWriteOutcome Outcome, WorkflowTransitionLog? Log, bool Replayed = false);

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
            return await AfterUnavailableAsync(task.Id, taskVersion, log, ct);
        }
    }

    /// <summary>
    /// BL-549 — an instance that has NO task becomes terminal: a generic start that crashed after writing its instance, closed
    /// by the abandoned-start recovery. The same one-transaction rule as <see cref="CommitAsync"/> — the instance update
    /// (fenced on <paramref name="instanceVersion"/>: a replay that claimed the instance meanwhile wins), the transition log
    /// and the <see cref="WorkflowInstanceCompletedV1"/> intent commit together — only without a task to update. The stage and
    /// step of the event are the instance's own (there is no task to take them from).
    /// </summary>
    public async Task<WorkflowTerminalWriteResult> CommitTasklessAsync(
        WorkflowInstance instance,
        int instanceVersion,
        WorkflowTransitionLog log,
        string? completedBy,
        string? reasonCode,
        string correlationId,
        CancellationToken ct)
    {
        var outcome = WorkflowOutcomes.For(instance.Status)
            ?? throw new InvalidOperationException("Only a terminal instance status can be committed here.");

        if (_transactions is null || _events is null)
        {
            if (!await _instances.UpdateAsync(instance, instanceVersion, ct))
            {
                return new WorkflowTerminalWriteResult(WorkflowTerminalWriteOutcome.Conflict, null);
            }

            return new WorkflowTerminalWriteResult(WorkflowTerminalWriteOutcome.Committed, await _logs.CreateAsync(log, ct));
        }

        var stageHolder = new ApprovalTask
        {
            TenantId = instance.TenantId,
            WorkflowInstanceId = instance.Id,
            StageCode = instance.CurrentStage,
            StepCode = instance.CurrentStep
        };
        var completedEvent = await BuildEventAsync(instance, stageHolder, outcome, completedBy, reasonCode, correlationId, ct);
        try
        {
            var created = await _transactions.ExecuteAsync(async (session, tct) =>
            {
                if (!await _instances.UpdateAsync(session, instance, instanceVersion, tct))
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
            return new WorkflowTerminalWriteResult(WorkflowTerminalWriteOutcome.TransactionUnavailable, null);
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
            return await AfterUnavailableAsync(task.Id, taskVersion, log, ct);
        }
    }

    /// <summary>True when writes run in a Platform transaction; false for in-memory doubles (sequential writes).</summary>
    public bool IsTransactional => _transactions is not null;

    /// <summary>
    /// D1-FIX1 — A TRANSITION THAT KEEPS THE INSTANCE OPEN (delegate, request information, escalate): the task, the
    /// instance, the new assignment snapshot (when the transition re-assigns) and the transition log commit in ONE
    /// Platform transaction. Before this they were separate writes: a delegation whose instance write lost left the
    /// task delegated WITHOUT its log row — an authority change with no trail (the transition log is an accepted
    /// audit trail, AUD-001 path c) — and the caller was told 409.
    /// <para><paramref name="escalationWrite"/> only chooses the sequential fallback's repository methods (the ones
    /// in-memory doubles implement for the SLA runner); inside a transaction the writes are the same.</para>
    /// </summary>
    public async Task<WorkflowTerminalWriteResult> CommitTransitionAsync(
        ApprovalTask task,
        int taskVersion,
        WorkflowInstance instance,
        int instanceVersion,
        WorkflowTransitionLog log,
        RuntimeAssignmentSnapshot? newSnapshot,
        bool escalationWrite,
        CancellationToken ct)
    {
        if (WorkflowOutcomes.For(instance.Status) is not null)
        {
            throw new InvalidOperationException("A terminal instance status is committed by CommitAsync, with its event.");
        }

        if (_transactions is null)
        {
            if (newSnapshot is not null && _snapshots is not null)
            {
                await _snapshots.CreateAsync(newSnapshot, ct);
            }

            var updated = escalationWrite
                ? await _tasks.UpdateEscalationAsync(task, taskVersion, ct) &&
                  await _instances.UpdateEscalationOrTimeoutAsync(instance, instanceVersion, ct)
                : await _tasks.UpdateAsync(task, taskVersion, ct) &&
                  await _instances.UpdateAsync(instance, instanceVersion, ct);
            if (!updated)
            {
                return new WorkflowTerminalWriteResult(WorkflowTerminalWriteOutcome.Conflict, null);
            }

            var sequential = escalationWrite ? await _logs.AppendAsync(log, ct) : await _logs.CreateAsync(log, ct);
            return new WorkflowTerminalWriteResult(WorkflowTerminalWriteOutcome.Committed, sequential);
        }

        if (newSnapshot is not null && _snapshots is null)
        {
            throw new InvalidOperationException("A re-assigning transition needs the assignment snapshot repository.");
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

                if (newSnapshot is not null)
                {
                    await _snapshots!.CreateAsync(session, newSnapshot, tct);
                }

                return await _logs.CreateAsync(session, log, tct);
            }, ct);
            return new WorkflowTerminalWriteResult(WorkflowTerminalWriteOutcome.Committed, created);
        }
        catch (TerminalConflictException)
        {
            return new WorkflowTerminalWriteResult(WorkflowTerminalWriteOutcome.Conflict, null);
        }
        catch (PlatformTransactionUnavailableException)
        {
            return await AfterUnavailableAsync(task.Id, taskVersion, log, ct, newSnapshot);
        }
    }

    /// <summary>
    /// WHAT "TRANSACTION UNAVAILABLE" REALLY WAS — asked before the caller is told anything.
    /// <list type="bullet">
    /// <item><b>It was MINE.</b> The executor also reports "unavailable" when the commit's outcome is unknown; the
    /// commit may have landed. Then the task's version moved by THIS caller's own write, and its log row is there
    /// (same task, action, idempotency key and actor). That is a success — the same answer an idempotent retry gets —
    /// not "somebody else decided". (The first version read only the version, and told such a caller 409.)</item>
    /// <item><b>Somebody else won.</b> Two candidates deciding at once: the loser's transaction is aborted by MongoDB's
    /// write conflict while the winner's is still open; the executor replays the body at once, three times, with no
    /// pause, and all three can meet the same open winner (measured 2026-10-02 — the inner error is the labelled,
    /// transient WriteConflict 112). The version moves a moment later, without a log row of mine: 409.</item>
    /// <item><b>Really unavailable.</b> Nothing moved within the short wait, or the re-read itself failed: 503.</item>
    /// </list>
    /// </summary>
    private async Task<WorkflowTerminalWriteResult> AfterUnavailableAsync(
        Guid taskId, int expectedVersion, WorkflowTransitionLog attempted, CancellationToken ct,
        RuntimeAssignmentSnapshot? attemptedSnapshot = null)
    {
        try
        {
            for (var attempt = 1; attempt <= LostRaceReadAttempts; attempt++)
            {
                if (await MyCommittedLogAsync(attempted, attemptedSnapshot, ct) is { } mine)
                {
                    return Mine(attempted, mine);
                }

                var current = await _tasks.GetByIdAsync(taskId, ct);
                if (current is not null && current.Version != expectedVersion)
                {
                    // The version moved between the two reads: once more, was it my own commit landing late?
                    return await MyCommittedLogAsync(attempted, attemptedSnapshot, ct) is { } late
                        ? Mine(attempted, late)
                        : new WorkflowTerminalWriteResult(WorkflowTerminalWriteOutcome.Conflict, null);
                }

                if (attempt < LostRaceReadAttempts)
                {
                    await Task.Delay(LostRaceReadDelay, ct); // no sleep after the last read
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // cannot tell → the honest answer is "unavailable"
        }

        return new WorkflowTerminalWriteResult(WorkflowTerminalWriteOutcome.TransactionUnavailable, null);
    }

    /// <summary>
    /// The log row that answers "was the version-advancing write mine": same task, action and idempotency key (the
    /// lookup), same actor — and, D2, the same PAYLOAD (target state, reason, comment, evidence, and for a re-assigning
    /// transition the same new candidate list). Same key and actor but another payload is somebody else's request
    /// under my key: not mine (409).
    /// </summary>
    private async Task<WorkflowTransitionLog?> MyCommittedLogAsync(
        WorkflowTransitionLog attempted, RuntimeAssignmentSnapshot? attemptedSnapshot, CancellationToken ct)
    {
        if (attempted.ApprovalTaskId is not { } taskId || string.IsNullOrWhiteSpace(attempted.IdempotencyKey))
        {
            return null;
        }

        var found = await _logs.GetByTaskActionIdempotencyKeyAsync(taskId, attempted.Action, attempted.IdempotencyKey, ct);
        if (found is null
            || !string.Equals(found.ActorId, attempted.ActorId, StringComparison.Ordinal)
            || !SamePayload(found, attempted))
        {
            return null;
        }

        if (attemptedSnapshot is not null && _snapshots is not null)
        {
            // The task's CURRENT assignment (an escalation's snapshot id is new on every run; a delegation's is
            // derived from its key): the transition that committed must have handed the task to the same people.
            var current = await _tasks.GetByIdAsync(taskId, ct);
            var stored = current?.AssignmentSnapshotId is { } currentSnapshotId
                ? await _snapshots.GetByIdAsync(currentSnapshotId, ct)
                : null;
            if (stored is null
                || !stored.CandidatePrincipalIds.SequenceEqual(attemptedSnapshot.CandidatePrincipalIds, StringComparer.Ordinal))
            {
                return null;
            }
        }

        return found;
    }

    private static bool SamePayload(WorkflowTransitionLog found, WorkflowTransitionLog attempted) =>
        string.Equals(found.ToState, attempted.ToState, StringComparison.Ordinal)
        && string.Equals(found.ToStatus, attempted.ToStatus, StringComparison.Ordinal)
        && string.Equals(found.ReasonCode, attempted.ReasonCode, StringComparison.Ordinal)
        && string.Equals(found.Comment, attempted.Comment, StringComparison.Ordinal)
        && string.Equals(found.EvidenceRef, attempted.EvidenceRef, StringComparison.Ordinal);

    /// <summary>D2 (C1) — my own row (this call's log id) is a write; a twin's row with the same payload is a replay.</summary>
    private static WorkflowTerminalWriteResult Mine(WorkflowTransitionLog attempted, WorkflowTransitionLog found) =>
        new(WorkflowTerminalWriteOutcome.Committed, found, Replayed: found.Id != attempted.Id);

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

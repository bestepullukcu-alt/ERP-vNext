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

    public WorkflowTerminalTransitionWriter(
        IApprovalTaskRepository tasks,
        IWorkflowInstanceRepository instances,
        IWorkflowTransitionLogRepository logs,
        IPlatformTransactionExecutor? transactions,
        ITransactionalIntegrationEventWriter? events,
        IWorkflowTemplateRepository? templates)
    {
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
            return new WorkflowTerminalWriteResult(WorkflowTerminalWriteOutcome.TransactionUnavailable, null);
        }
    }

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

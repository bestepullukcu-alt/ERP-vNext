using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;

/// <summary>
/// BL-549 — see <see cref="CloseAbandonedWorkflowStartCommand"/>. The close is the engine's terminal write
/// (<see cref="WorkflowTerminalTransitionWriter.CommitTasklessAsync"/>), fenced on the version this handler read: a replay
/// of the start that claimed the instance meanwhile (<c>ClaimStartCompletionAsync</c>) bumps the version, and the close
/// then writes nothing (409) — a start being completed is never closed under it.
/// </summary>
public sealed class CloseAbandonedWorkflowStartHandler
    : IRequestHandler<CloseAbandonedWorkflowStartCommand, Response<CloseAbandonedWorkflowStartResult>>
{
    private readonly IWorkflowInstanceRepository _instances;
    private readonly IApprovalTaskRepository _tasks;
    private readonly IWorkflowTransitionLogRepository _logs;
    private readonly WorkflowTerminalTransitionWriter _terminal;

    public CloseAbandonedWorkflowStartHandler(
        IWorkflowInstanceRepository instances,
        IApprovalTaskRepository tasks,
        IWorkflowTransitionLogRepository logs,
        IPlatformTransactionExecutor? transactions = null,
        ITransactionalIntegrationEventWriter? events = null,
        IWorkflowTemplateRepository? templates = null)
    {
        _instances = instances;
        _tasks = tasks;
        _logs = logs;
        _terminal = new WorkflowTerminalTransitionWriter(tasks, instances, logs, transactions, events, templates);
    }

    public async Task<Response<CloseAbandonedWorkflowStartResult>> Handle(
        CloseAbandonedWorkflowStartCommand request,
        CancellationToken ct)
    {
        var instance = await _instances.GetByIdAsync(request.WorkflowInstanceId, ct);
        if (instance is null)
        {
            return Response<CloseAbandonedWorkflowStartResult>.Fail(
                "Workflow instance not found.", 404, WorkflowReasonCodes.NotFoundNonLeakage, request.CorrelationId);
        }

        var key = IdempotencyKey(instance.Id);
        if (instance.Status == WorkflowInstanceStatus.Cancelled
            && string.Equals(instance.StartClosedReasonCode, WorkflowReasonCodes.WorkflowStartAbandoned, StringComparison.Ordinal))
        {
            var closing = (await _logs.ListByInstanceIdAsync(instance.Id, ct))
                .FirstOrDefault(log => log.Action == WorkflowTransitionAction.Cancel && log.IdempotencyKey == key);
            if (instance.TrustedConsumerClientId is not null)
            {
                // A close of a reservation that stopped after its fence (before the withdraw or the log) is finished here,
                // idempotently: the withdraw and the line are each written once.
                await Services.WorkflowInstanceStartCoordinator.WithdrawStrayStartTaskAsync(_tasks, _logs, instance, request.CorrelationId, ct);
                closing ??= await AppendTrustedCloseLogAsync(instance, request.CorrelationId, DateTimeOffset.UtcNow, ct);
            }

            return Response<CloseAbandonedWorkflowStartResult>.Success(
                new CloseAbandonedWorkflowStartResult(instance.Id, closing?.Id ?? Guid.Empty, AlreadyClosed: true),
                correlationId: request.CorrelationId);
        }

        var now = DateTimeOffset.UtcNow;
        // FIX1 item 7 — the floor holds HERE too, not only in the job: whoever sends the command cannot close a start
        // younger than the minimum (a replay still has that long to complete it).
        var floor = TimeSpan.FromMinutes(WorkflowStartRecoveryOptions.MinimumAbandonAfterMinutes);
        request = request with { MinimumAge = request.MinimumAge < floor ? floor : request.MinimumAge };
        if (instance.TrustedConsumerClientId is not null)
        {
            return await CloseIncompleteTrustedStartAsync(instance, request, now, ct);
        }

        var lastMoved = instance.LastTransitionAt ?? instance.StartedAt ?? instance.CreatedAt;
        if (instance.Status != WorkflowInstanceStatus.Active
            || lastMoved > now - request.MinimumAge
            // An instance with ANY task — open, decided, even deleted — is never closed here: somebody can decide it, or
            // it was decided, and its own transitions own it.
            || (await _tasks.ListByInstanceIdAsync(instance.Id, ct)).Count > 0)
        {
            return Response<CloseAbandonedWorkflowStartResult>.Fail(
                "The workflow instance is not an abandoned start; nothing was closed.",
                409,
                WorkflowReasonCodes.WorkflowStartNotAbandoned,
                request.CorrelationId);
        }

        var readVersion = instance.Version;
        var previousStatus = instance.Status;
        instance.Status = WorkflowInstanceStatus.Cancelled;
        instance.CompletedAt = now;
        instance.LastTransitionAt = now;
        instance.StartClosedReasonCode = WorkflowReasonCodes.WorkflowStartAbandoned;
        var log = new WorkflowTransitionLog
        {
            TenantId = instance.TenantId,
            WorkflowInstanceId = instance.Id,
            ApprovalTaskId = null,
            Action = WorkflowTransitionAction.Cancel,
            // The engine's log shape (WorkflowTaskTransitionSupport.NewLog): *State is the task's, *Status the instance's.
            // There is no task, so no task state.
            FromState = null,
            ToState = null,
            FromStatus = previousStatus.ToString(),
            ToStatus = WorkflowInstanceStatus.Cancelled.ToString(),
            ActorId = CloseAbandonedWorkflowStartResult.SystemActor,
            ActorRef = CloseAbandonedWorkflowStartResult.SystemActor,
            ReasonCode = WorkflowReasonCodes.WorkflowStartAbandoned,
            IdempotencyKey = key,
            CorrelationId = request.CorrelationId,
            SequenceNo = await _logs.GetLatestSequenceNoAsync(instance.Id, ct) + 1,
            CreatedAt = now
        };

        var written = await _terminal.CommitTasklessAsync(
            instance,
            readVersion,
            log,
            CloseAbandonedWorkflowStartResult.SystemActor,
            WorkflowReasonCodes.WorkflowStartAbandoned,
            request.CorrelationId,
            ct);
        return written.Outcome switch
        {
            WorkflowTerminalWriteOutcome.Committed => Response<CloseAbandonedWorkflowStartResult>.Success(
                new CloseAbandonedWorkflowStartResult(instance.Id, written.Log!.Id, AlreadyClosed: false),
                correlationId: request.CorrelationId),
            WorkflowTerminalWriteOutcome.TransactionUnavailable => Response<CloseAbandonedWorkflowStartResult>.Fail(
                "The close could not be written right now.", 503, WorkflowReasonCodes.WorkflowTransitionConflict, request.CorrelationId),
            _ => Response<CloseAbandonedWorkflowStartResult>.Fail(
                "The workflow instance changed meanwhile; nothing was closed.", 409,
                WorkflowReasonCodes.WorkflowTransitionConflict, request.CorrelationId)
        };
    }

    /// <summary>
    /// BL-539 (CT decision) — an INCOMPLETE trusted reservation past the age is closed through the engine's own path: Cancelled
    /// with the reason (the coordinator's own close), its stray start task withdrawn (the coordinator's own withdraw, with its
    /// log line), a closing log line, and the audit row of this command. Fenced: the close holds only while the reservation is
    /// still Pending, at the checkpoint and version read — a replay that advanced it meanwhile wins, and the close writes
    /// nothing. A start task somebody has moved (it is no longer waiting) is never withdrawn: the close loses and the
    /// reservation is left to its completion. Its consumer then reads the key as not found (start-result), which its accepted
    /// rule already ends on.
    /// </summary>
    private async Task<Response<CloseAbandonedWorkflowStartResult>> CloseIncompleteTrustedStartAsync(
        WorkflowInstance instance,
        CloseAbandonedWorkflowStartCommand request,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var lastMoved = instance.UpdatedAt ?? instance.CreatedAt;
        if (instance.Status != WorkflowInstanceStatus.Pending
            || instance.StartCheckpoint >= WorkflowStartCheckpoint.Completed
            || lastMoved > now - request.MinimumAge)
        {
            return NotAbandoned(request.CorrelationId);
        }

        if (instance.InitialApprovalTaskId is { } taskId
            && await _tasks.GetByIdAsync(taskId, ct) is { } written
            && written.Status is not (ApprovalTaskStatus.WaitingApproval or ApprovalTaskStatus.WaitingEvidence))
        {
            return NotAbandoned(request.CorrelationId); // somebody moved the start task: not this recovery's to close
        }

        if (!await _instances.CloseIncompleteTrustedStartAsync(
                instance.Id, instance.Version, instance.StartCheckpoint, WorkflowReasonCodes.WorkflowStartAbandoned, now, ct))
        {
            return Response<CloseAbandonedWorkflowStartResult>.Fail(
                "The reservation moved meanwhile (a replay is completing it); nothing was closed.", 409,
                WorkflowReasonCodes.WorkflowTransitionConflict, request.CorrelationId);
        }

        var closed = (await _instances.GetByIdAsync(instance.Id, ct))!;
        await Services.WorkflowInstanceStartCoordinator.WithdrawStrayStartTaskAsync(_tasks, _logs, closed, request.CorrelationId, ct);
        var log = await AppendTrustedCloseLogAsync(closed, request.CorrelationId, now, ct);
        return Response<CloseAbandonedWorkflowStartResult>.Success(
            new CloseAbandonedWorkflowStartResult(closed.Id, log.Id, AlreadyClosed: false),
            correlationId: request.CorrelationId);
    }

    private async Task<WorkflowTransitionLog> AppendTrustedCloseLogAsync(
        WorkflowInstance closed, string correlationId, DateTimeOffset now, CancellationToken ct) =>
        await _logs.AppendAsync(new WorkflowTransitionLog
        {
            TenantId = closed.TenantId,
            WorkflowInstanceId = closed.Id,
            ApprovalTaskId = null,
            Action = WorkflowTransitionAction.Cancel,
            FromState = null,
            ToState = null,
            FromStatus = WorkflowInstanceStatus.Pending.ToString(),
            ToStatus = WorkflowInstanceStatus.Cancelled.ToString(),
            ActorId = CloseAbandonedWorkflowStartResult.SystemActor,
            ActorRef = CloseAbandonedWorkflowStartResult.SystemActor,
            ReasonCode = WorkflowReasonCodes.WorkflowStartAbandoned,
            IdempotencyKey = IdempotencyKey(closed.Id),
            CorrelationId = correlationId,
            SequenceNo = await _logs.GetLatestSequenceNoAsync(closed.Id, ct) + 1,
            CreatedAt = now
        }, ct);

    private static Response<CloseAbandonedWorkflowStartResult> NotAbandoned(string correlationId) =>
        Response<CloseAbandonedWorkflowStartResult>.Fail(
            "The workflow instance is not an abandoned start; nothing was closed.",
            409,
            WorkflowReasonCodes.WorkflowStartNotAbandoned,
            correlationId);

    internal static string IdempotencyKey(Guid instanceId) => $"workflow-start-recovery-close:{instanceId:N}";
}

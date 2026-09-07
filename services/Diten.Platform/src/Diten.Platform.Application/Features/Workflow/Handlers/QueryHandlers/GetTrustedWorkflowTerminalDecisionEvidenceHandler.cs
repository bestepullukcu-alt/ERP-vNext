using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers;

public sealed class GetTrustedWorkflowTerminalDecisionEvidenceHandler
    : IRequestHandler<GetTrustedWorkflowTerminalDecisionEvidenceQuery, Response<TrustedWorkflowTerminalDecisionEvidence>>
{
    private readonly IWorkflowInstanceRepository _instances;
    private readonly IApprovalTaskRepository _tasks;
    private readonly IWorkflowTransitionLogRepository _logs;

    public GetTrustedWorkflowTerminalDecisionEvidenceHandler(
        IWorkflowInstanceRepository instances,
        IApprovalTaskRepository tasks,
        IWorkflowTransitionLogRepository logs)
    {
        _instances = instances;
        _tasks = tasks;
        _logs = logs;
    }

    public async Task<Response<TrustedWorkflowTerminalDecisionEvidence>> Handle(
        GetTrustedWorkflowTerminalDecisionEvidenceQuery request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var instance = await _instances.GetByIdAsync(request.WorkflowInstanceId, ct);
        if (instance is null ||
            instance.TrustedConsumerClientId != request.TrustedConsumerClientId ||
            !string.Equals(instance.ObjectType, request.ExpectedObjectType, StringComparison.Ordinal) ||
            !string.Equals(instance.ObjectId, request.ExpectedObjectId, StringComparison.Ordinal) ||
            !HasCoherentTrustedStartIdentity(instance))
        {
            return Response<TrustedWorkflowTerminalDecisionEvidence>.Fail(
                "Workflow instance not found.",
                404,
                WorkflowReasonCodes.NotFoundNonLeakage,
                request.CorrelationId);
        }

        var tasks = await _tasks.ListByInstanceIdAsync(instance.Id, ct);
        var logs = await _logs.ListByInstanceIdAsync(instance.Id, ct);
        if (!HasCoherentTrustedStartProof(instance, tasks, logs))
        {
            return Response<TrustedWorkflowTerminalDecisionEvidence>.Fail(
                "Workflow instance not found.",
                404,
                WorkflowReasonCodes.NotFoundNonLeakage,
                request.CorrelationId);
        }
        if (!HasStrictMonotonicSequence(logs))
        {
            return Inconsistent("Workflow transition history is not strictly monotonic.", request.CorrelationId);
        }

        var decisionLogs = logs
            .Where(x => x.Action is WorkflowTransitionAction.Approve or WorkflowTransitionAction.Reject)
            .OrderBy(x => x.SequenceNo)
            .ToList();
        var expectedAction = instance.Status switch
        {
            WorkflowInstanceStatus.Completed or WorkflowInstanceStatus.Approved =>
                WorkflowTransitionAction.Approve,
            WorkflowInstanceStatus.Rejected => WorkflowTransitionAction.Reject,
            _ => (WorkflowTransitionAction?)null
        };
        if (expectedAction is null)
        {
            return IsCoherentNonTerminal(instance.Status, tasks, decisionLogs)
                ? NotTerminal(request.CorrelationId)
                : Inconsistent("Workflow non-terminal facts are contradictory.", request.CorrelationId);
        }

        if (decisionLogs.Count == 0)
        {
            return Inconsistent("Workflow terminal transition proof is missing or contradictory.", request.CorrelationId);
        }

        var terminalLog = decisionLogs[^1];
        if (terminalLog.Action != expectedAction
            || terminalLog.SequenceNo != logs[^1].SequenceNo
            || decisionLogs.Take(decisionLogs.Count - 1).Any(x => x.Action != WorkflowTransitionAction.Approve)
            || !decisionLogs.Take(decisionLogs.Count - 1).All(x => IsCoherentPriorApproval(x, tasks)))
        {
            return Inconsistent("Workflow terminal transition proof is missing or contradictory.", request.CorrelationId);
        }

        var task = FindSingleTask(tasks, terminalLog.ApprovalTaskId);
        var expectedTaskStatus = expectedAction == WorkflowTransitionAction.Approve
            ? ApprovalTaskStatus.Approved
            : ApprovalTaskStatus.Rejected;
        if (task is null ||
            task.Status != expectedTaskStatus ||
            task.CompletedAt is null ||
            string.IsNullOrWhiteSpace(terminalLog.ActorId) ||
            !string.Equals(terminalLog.ActorRef, terminalLog.ActorId, StringComparison.Ordinal) ||
            instance.TemplateVersionId is null ||
            instance.TemplateVersionId == Guid.Empty ||
            instance.CompletedAt is null ||
            terminalLog.SequenceNo <= 0 ||
            !string.Equals(terminalLog.ToStatus, instance.Status.ToString(), StringComparison.Ordinal) ||
            !string.Equals(terminalLog.ToState, task.Status.ToString(), StringComparison.Ordinal) ||
            !string.Equals(task.ActionedBy, terminalLog.ActorId, StringComparison.Ordinal) ||
            !string.Equals(task.ActionReasonCode, terminalLog.ReasonCode, StringComparison.Ordinal))
        {
            return Inconsistent("Workflow terminal task, instance, and log facts are inconsistent.", request.CorrelationId);
        }

        var evidence = new TrustedWorkflowTerminalDecisionEvidence(
            instance.Id,
            task.Id,
            instance.TemplateId,
            instance.TemplateVersionId.Value,
            instance.ObjectType,
            instance.ObjectId,
            instance.ObjectRef,
            terminalLog.Action.ToString(),
            terminalLog.ActorId,
            terminalLog.ReasonCode,
            terminalLog.CreatedAt,
            terminalLog.SequenceNo,
            task.Status.ToString(),
            instance.Status.ToString(),
            terminalLog.CorrelationId ?? request.CorrelationId);
        return Response<TrustedWorkflowTerminalDecisionEvidence>.Success(evidence, correlationId: request.CorrelationId);
    }

    private static Response<TrustedWorkflowTerminalDecisionEvidence> Inconsistent(
        string message,
        string correlationId) =>
        Response<TrustedWorkflowTerminalDecisionEvidence>.Fail(
            message,
            409,
            WorkflowReasonCodes.WorkflowTerminalEvidenceInconsistent,
            correlationId);

    private static Response<TrustedWorkflowTerminalDecisionEvidence> NotTerminal(string correlationId) =>
        Response<TrustedWorkflowTerminalDecisionEvidence>.Fail(
            "Workflow decision is not terminal yet.",
            409,
            WorkflowReasonCodes.WorkflowDecisionNotTerminal,
            correlationId);

    private static bool HasStrictMonotonicSequence(IReadOnlyList<WorkflowTransitionLog> logs)
    {
        long previous = 0;
        foreach (var log in logs.OrderBy(x => x.SequenceNo))
        {
            if (log.SequenceNo != previous + 1) return false;
            previous = log.SequenceNo;
        }

        return true;
    }

    private static bool HasCoherentTrustedStartIdentity(WorkflowInstance instance) =>
        instance.TrustedConsumerClientId is { } clientId
        && clientId != Guid.Empty
        && instance.DelegatedMakerUserId is { } makerId
        && makerId != Guid.Empty
        && instance.StartCheckpoint == WorkflowStartCheckpoint.Completed
        && instance.InitialApprovalTaskId is { } taskId
        && taskId != Guid.Empty
        && instance.InitialAssignmentSnapshotId is { } snapshotId
        && snapshotId != Guid.Empty
        && instance.StartTransitionLogId is { } logId
        && logId != Guid.Empty
        && !string.IsNullOrWhiteSpace(instance.IdempotencyKey)
        && !string.IsNullOrWhiteSpace(instance.StartRequestFingerprint)
        && string.Equals(instance.StartedBy, makerId.ToString("D"), StringComparison.Ordinal);

    private static bool HasCoherentTrustedStartProof(
        WorkflowInstance instance,
        IReadOnlyList<ApprovalTask> tasks,
        IReadOnlyList<WorkflowTransitionLog> logs)
    {
        var initialTask = FindSingleTask(tasks, instance.InitialApprovalTaskId);
        var startLogs = logs.Where(x => x.Id == instance.StartTransitionLogId).Take(2).ToArray();
        if (initialTask is null || initialTask.WorkflowInstanceId != instance.Id || startLogs.Length != 1)
        {
            return false;
        }

        var startLog = startLogs[0];
        return startLog.WorkflowInstanceId == instance.Id
               && startLog.ApprovalTaskId == initialTask.Id
               && startLog.Action == WorkflowTransitionAction.Start
               && startLog.SequenceNo == 1
               && string.Equals(startLog.ActorId, instance.StartedBy, StringComparison.Ordinal)
               && string.Equals(startLog.ActorRef, instance.StartedBy, StringComparison.Ordinal)
               && startLog.FromStatus is null
               && string.Equals(startLog.ToState, WorkflowInstanceStatus.Active.ToString(), StringComparison.Ordinal)
               && string.Equals(startLog.ToStatus, WorkflowInstanceStatus.Active.ToString(), StringComparison.Ordinal);
    }

    private static bool IsCoherentNonTerminal(
        WorkflowInstanceStatus instanceStatus,
        IReadOnlyList<ApprovalTask> tasks,
        IReadOnlyList<WorkflowTransitionLog> decisionLogs)
    {
        if (decisionLogs.Any(x => x.Action != WorkflowTransitionAction.Approve)
            || !decisionLogs.All(x => IsCoherentPriorApproval(x, tasks)))
        {
            return false;
        }

        return instanceStatus switch
        {
            WorkflowInstanceStatus.Pending => decisionLogs.Count == 0 && tasks.Count == 0,
            WorkflowInstanceStatus.Active => tasks.Count(x => x.Status is
                ApprovalTaskStatus.WaitingApproval or ApprovalTaskStatus.WaitingEvidence) == 1,
            _ => false
        };
    }

    private static bool IsCoherentPriorApproval(
        WorkflowTransitionLog log,
        IReadOnlyList<ApprovalTask> tasks)
    {
        var task = FindSingleTask(tasks, log.ApprovalTaskId);
        return log.Action == WorkflowTransitionAction.Approve
               && task is not null
               && task.Status == ApprovalTaskStatus.Approved
               && task.CompletedAt is not null
               && !string.IsNullOrWhiteSpace(log.ActorId)
               && string.Equals(log.ActorRef, log.ActorId, StringComparison.Ordinal)
               && string.Equals(task.ActionedBy, log.ActorId, StringComparison.Ordinal)
               && string.Equals(task.ActionReasonCode, log.ReasonCode, StringComparison.Ordinal)
               && string.Equals(log.ToState, ApprovalTaskStatus.Approved.ToString(), StringComparison.Ordinal)
               && string.Equals(log.ToStatus, WorkflowInstanceStatus.Active.ToString(), StringComparison.Ordinal);
    }

    private static ApprovalTask? FindSingleTask(
        IReadOnlyList<ApprovalTask> tasks,
        Guid? taskId)
    {
        if (taskId is null || taskId == Guid.Empty) return null;
        var matches = tasks.Where(x => x.Id == taskId.Value).Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }
}

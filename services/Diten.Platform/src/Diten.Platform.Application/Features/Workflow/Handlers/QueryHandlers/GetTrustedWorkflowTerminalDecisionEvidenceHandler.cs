using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow.Queries;
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
            !string.Equals(instance.ObjectType, request.ExpectedObjectType, StringComparison.Ordinal) ||
            !string.Equals(instance.ObjectId, request.ExpectedObjectId, StringComparison.Ordinal))
        {
            return Response<TrustedWorkflowTerminalDecisionEvidence>.Fail(
                "Workflow instance not found.",
                404,
                WorkflowReasonCodes.NotFoundNonLeakage,
                request.CorrelationId);
        }

        var expectedAction = instance.Status switch
        {
            WorkflowInstanceStatus.Approved => WorkflowTransitionAction.Approve,
            WorkflowInstanceStatus.Rejected => WorkflowTransitionAction.Reject,
            _ => (WorkflowTransitionAction?)null
        };
        if (expectedAction is null)
        {
            return Inconsistent("Workflow instance is not in an approve/reject terminal state.", request.CorrelationId);
        }

        var tasks = await _tasks.ListByInstanceIdAsync(instance.Id, ct);
        var logs = await _logs.ListByInstanceIdAsync(instance.Id, ct);
        var terminalLogs = logs
            .Where(x => x.Action is WorkflowTransitionAction.Approve or WorkflowTransitionAction.Reject)
            .OrderBy(x => x.SequenceNo)
            .ToList();
        if (terminalLogs.Count != 1 || terminalLogs[0].Action != expectedAction)
        {
            return Inconsistent("Workflow terminal transition proof is missing or contradictory.", request.CorrelationId);
        }

        var terminalLog = terminalLogs[0];
        var task = terminalLog.ApprovalTaskId.HasValue
            ? tasks.SingleOrDefault(x => x.Id == terminalLog.ApprovalTaskId.Value)
            : null;
        var expectedTaskStatus = expectedAction == WorkflowTransitionAction.Approve
            ? ApprovalTaskStatus.Approved
            : ApprovalTaskStatus.Rejected;
        if (task is null ||
            task.Status != expectedTaskStatus ||
            string.IsNullOrWhiteSpace(terminalLog.ActorId) ||
            instance.TemplateVersionId is null ||
            instance.TemplateVersionId == Guid.Empty ||
            terminalLog.SequenceNo <= 0 ||
            !string.Equals(terminalLog.ToStatus, instance.Status.ToString(), StringComparison.Ordinal) ||
            !string.Equals(task.ActionedBy, terminalLog.ActorId, StringComparison.Ordinal))
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
}

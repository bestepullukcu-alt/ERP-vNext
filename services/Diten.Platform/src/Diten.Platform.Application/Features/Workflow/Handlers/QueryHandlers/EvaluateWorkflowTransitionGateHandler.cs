using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers;

public sealed class EvaluateWorkflowTransitionGateHandler
    : IRequestHandler<EvaluateWorkflowTransitionGateQuery, Response<EvaluateWorkflowTransitionGateResponse>>
{
    private readonly IWorkflowInstanceRepository _instances;
    private readonly IApprovalTaskRepository _tasks;

    public EvaluateWorkflowTransitionGateHandler(
        IWorkflowInstanceRepository instances,
        IApprovalTaskRepository tasks)
    {
        _instances = instances;
        _tasks = tasks;
    }

    public async Task<Response<EvaluateWorkflowTransitionGateResponse>> Handle(
        EvaluateWorkflowTransitionGateQuery request,
        CancellationToken ct)
    {
        var objectType = request.Request.ObjectType.Trim();
        var objectId = request.Request.ObjectId.Trim();
        var objectRef = request.Request.ObjectRef.Trim();

        var instance = await _instances.GetLatestByObjectRefAsync(objectRef, objectType, objectId, ct);
        if (instance is null)
        {
            return Response<EvaluateWorkflowTransitionGateResponse>.Success(
                BuildNoWorkflow(objectType, objectId, objectRef, request.CorrelationId),
                correlationId: request.CorrelationId);
        }

        var activeTask = instance.Status == WorkflowInstanceStatus.Active
            ? await _tasks.GetActiveByInstanceIdAsync(instance.Id, ct)
            : null;

        var response = instance.Status switch
        {
            WorkflowInstanceStatus.Active when activeTask?.Status == ApprovalTaskStatus.WaitingEvidence =>
                BuildBlocked(
                    instance,
                    activeTask,
                    WorkflowTransitionGateStatus.WaitingEvidence,
                    WorkflowReasonCodes.WorkflowWaitingEvidence,
                    "Workflow approval is waiting for evidence.",
                    request.CorrelationId),

            WorkflowInstanceStatus.Active =>
                BuildBlocked(
                    instance,
                    activeTask,
                    WorkflowTransitionGateStatus.PendingApproval,
                    WorkflowReasonCodes.WorkflowPendingApproval,
                    "Workflow approval is still pending.",
                    request.CorrelationId),

            WorkflowInstanceStatus.Completed or WorkflowInstanceStatus.Approved =>
                BuildAllowed(instance, request.CorrelationId),

            WorkflowInstanceStatus.Rejected =>
                BuildBlocked(
                    instance,
                    null,
                    WorkflowTransitionGateStatus.Rejected,
                    WorkflowReasonCodes.WorkflowRejected,
                    "Workflow was rejected.",
                    request.CorrelationId),

            WorkflowInstanceStatus.Cancelled =>
                BuildBlocked(
                    instance,
                    null,
                    WorkflowTransitionGateStatus.Cancelled,
                    WorkflowReasonCodes.WorkflowCancelled,
                    "Workflow was cancelled.",
                    request.CorrelationId),

            _ =>
                BuildBlocked(
                    instance,
                    activeTask,
                    WorkflowTransitionGateStatus.NotTerminalApproved,
                    WorkflowReasonCodes.WorkflowNotTerminalApproved,
                    "Workflow is not in an approved terminal state.",
                    request.CorrelationId)
        };

        return Response<EvaluateWorkflowTransitionGateResponse>.Success(response, correlationId: request.CorrelationId);
    }

    private static EvaluateWorkflowTransitionGateResponse BuildNoWorkflow(
        string objectType,
        string objectId,
        string objectRef,
        string correlationId) =>
        new(
            WorkflowTransitionGateDecision.NotApplicable,
            WorkflowTransitionGateStatus.NoWorkflow,
            objectType,
            objectId,
            objectRef,
            null,
            null,
            null,
            null,
            WorkflowReasonCodes.WorkflowNoInstance,
            "No workflow instance applies to this object.",
            correlationId);

    private static EvaluateWorkflowTransitionGateResponse BuildAllowed(
        WorkflowInstance instance,
        string correlationId) =>
        new(
            WorkflowTransitionGateDecision.Allowed,
            WorkflowTransitionGateStatus.Approved,
            instance.ObjectType,
            instance.ObjectId,
            instance.ObjectRef,
            instance.Id,
            instance.TemplateId == Guid.Empty ? instance.WorkflowTemplateId : instance.TemplateId,
            instance.TemplateVersionId,
            null,
            WorkflowReasonCodes.WorkflowApproved,
            null,
            correlationId);

    private static EvaluateWorkflowTransitionGateResponse BuildBlocked(
        WorkflowInstance instance,
        ApprovalTask? activeTask,
        WorkflowTransitionGateStatus gateStatus,
        string reasonCode,
        string message,
        string correlationId) =>
        new(
            WorkflowTransitionGateDecision.Blocked,
            gateStatus,
            instance.ObjectType,
            instance.ObjectId,
            instance.ObjectRef,
            instance.Id,
            instance.TemplateId == Guid.Empty ? instance.WorkflowTemplateId : instance.TemplateId,
            instance.TemplateVersionId,
            activeTask?.Id,
            reasonCode,
            message,
            correlationId);
}

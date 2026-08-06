using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Common.Tenancy;
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
    private readonly ITenantContext _tenantContext;

    public EvaluateWorkflowTransitionGateHandler(
        IWorkflowInstanceRepository instances,
        IApprovalTaskRepository tasks,
        ITenantContext tenantContext)
    {
        _instances = instances;
        _tasks = tasks;
        _tenantContext = tenantContext;
    }

    public async Task<Response<EvaluateWorkflowTransitionGateResponse>> Handle(
        EvaluateWorkflowTransitionGateQuery request,
        CancellationToken ct)
    {
        var objectType = request.Request.ObjectType.Trim();
        var objectId = request.Request.ObjectId.Trim();
        var objectRef = request.Request.ObjectRef.Trim();

        var scopeValidation = ValidateTargetScope(request.Request, request.CorrelationId);
        if (scopeValidation is not null)
        {
            return scopeValidation;
        }

        WorkflowInstance? instance;
        ApprovalTask? activeTask = null;

        try
        {
            var targetScope = request.Request.TargetScope ?? WorkflowTransitionGateTargetScope.CurrentTenant;
            if (targetScope == WorkflowTransitionGateTargetScope.Tenant)
            {
                using (TenantScope.Begin(_tenantContext, request.Request.TargetTenantId!.Value))
                {
                    instance = await _instances.GetLatestByObjectRefAsync(objectRef, objectType, objectId, ct);
                    activeTask = instance?.Status == WorkflowInstanceStatus.Active
                        ? await _tasks.GetActiveByInstanceIdAsync(instance.Id, ct)
                        : null;
                }
            }
            else
            {
                instance = await _instances.GetLatestByObjectRefAsync(objectRef, objectType, objectId, ct);
                activeTask = instance?.Status == WorkflowInstanceStatus.Active
                    ? await _tasks.GetActiveByInstanceIdAsync(instance.Id, ct)
                    : null;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return Response<EvaluateWorkflowTransitionGateResponse>.Fail(
                "Workflow gate evaluation is unavailable.",
                503,
                WorkflowReasonCodes.WorkflowEvaluationUnavailable,
                request.CorrelationId);
        }

        if (instance is null)
        {
            if (request.Request.RequiresWorkflowGate == true)
            {
                return Response<EvaluateWorkflowTransitionGateResponse>.Fail(
                    "Required workflow gate was not found.",
                    409,
                    WorkflowReasonCodes.WorkflowRequiredGateNotFound,
                    request.CorrelationId);
            }

            return Response<EvaluateWorkflowTransitionGateResponse>.Success(
                BuildNoWorkflow(objectType, objectId, objectRef, request.CorrelationId),
                correlationId: request.CorrelationId);
        }

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

    private Response<EvaluateWorkflowTransitionGateResponse>? ValidateTargetScope(
        EvaluateWorkflowTransitionGateRequest request,
        string correlationId)
    {
        var targetScope = request.TargetScope ?? WorkflowTransitionGateTargetScope.CurrentTenant;
        if (!Enum.IsDefined(targetScope))
        {
            return Response<EvaluateWorkflowTransitionGateResponse>.Fail(
                "TargetScope is invalid.",
                400,
                WorkflowReasonCodes.WorkflowInvalidTargetScope,
                correlationId);
        }

        if (targetScope == WorkflowTransitionGateTargetScope.CurrentTenant)
        {
            return request.TargetTenantId.HasValue
                ? Response<EvaluateWorkflowTransitionGateResponse>.Fail(
                    "TargetTenantId is valid only when TargetScope is Tenant.",
                    400,
                    WorkflowReasonCodes.WorkflowInvalidTargetScope,
                    correlationId)
                : null;
        }

        if (request.TargetTenantId is null || request.TargetTenantId == Guid.Empty)
        {
            return Response<EvaluateWorkflowTransitionGateResponse>.Fail(
                "TargetTenantId is required for tenant-scoped workflow gate evaluation.",
                400,
                WorkflowReasonCodes.WorkflowTargetTenantRequired,
                correlationId);
        }

        if (string.IsNullOrWhiteSpace(request.TargetTenantSource))
        {
            return Response<EvaluateWorkflowTransitionGateResponse>.Fail(
                "TargetTenantSource is required for tenant-scoped workflow gate evaluation.",
                400,
                WorkflowReasonCodes.WorkflowInvalidTargetScope,
                correlationId);
        }

        if (_tenantContext.IsResolved
            && !_tenantContext.IsPlatformContext
            && _tenantContext.TenantId != request.TargetTenantId.Value)
        {
            return Response<EvaluateWorkflowTransitionGateResponse>.Fail(
                "Actor is not authorized to evaluate a workflow gate for the requested target tenant.",
                403,
                WorkflowReasonCodes.WorkflowTargetTenantMismatch,
                correlationId);
        }

        return null;
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

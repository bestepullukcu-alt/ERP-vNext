using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;

public sealed class ApproveWorkflowTaskHandler
    : IRequestHandler<ApproveWorkflowTaskCommand, Response<WorkflowTaskTransitionResponse>>
{
    private readonly WorkflowTaskTransitionSupport _support;

    public ApproveWorkflowTaskHandler(
        IApprovalTaskRepository taskRepository,
        IWorkflowInstanceRepository instanceRepository,
        IRuntimeAssignmentSnapshotRepository snapshotRepository,
        IWorkflowTransitionLogRepository logRepository,
        IWorkflowTemplateVersionRepository? versionRepository = null,
        IPositionAssignmentRepository? positionAssignmentRepository = null)
    {
        _support = new WorkflowTaskTransitionSupport(
            taskRepository,
            instanceRepository,
            snapshotRepository,
            logRepository,
            versionRepository,
            positionAssignmentRepository);
    }

    public Task<Response<WorkflowTaskTransitionResponse>> Handle(ApproveWorkflowTaskCommand request, CancellationToken ct) =>
        _support.TransitionAsync(
            request.TaskId,
            WorkflowTransitionAction.Approve,
            request.Request.ActorId,
            request.Request.ReasonCode,
            request.Request.IdempotencyKey,
            request.Request.Comment,
            request.Request.EvidenceRef,
            request.CorrelationId,
            ct);
}

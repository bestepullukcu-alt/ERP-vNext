using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;

public sealed class RequestInfoWorkflowTaskHandler
    : IRequestHandler<RequestInfoWorkflowTaskCommand, Response<WorkflowTaskTransitionResponse>>
{
    private readonly WorkflowTaskTransitionSupport _support;

    public RequestInfoWorkflowTaskHandler(
        IApprovalTaskRepository taskRepository,
        IWorkflowInstanceRepository instanceRepository,
        IRuntimeAssignmentSnapshotRepository snapshotRepository,
        IWorkflowTransitionLogRepository logRepository)
    {
        _support = new WorkflowTaskTransitionSupport(taskRepository, instanceRepository, snapshotRepository, logRepository);
    }

    public Task<Response<WorkflowTaskTransitionResponse>> Handle(RequestInfoWorkflowTaskCommand request, CancellationToken ct) =>
        _support.RequestInfoAsync(
            request.TaskId,
            request.Request.ActorId,
            request.Request.TargetPrincipalId,
            request.Request.ReasonCode,
            request.Request.IdempotencyKey,
            request.Request.Comment,
            request.Request.EvidenceRef,
            request.CorrelationId,
            ct);
}

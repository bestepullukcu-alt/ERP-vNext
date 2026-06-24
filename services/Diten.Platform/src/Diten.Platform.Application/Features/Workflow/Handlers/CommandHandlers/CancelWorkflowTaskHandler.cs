using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;

public sealed class CancelWorkflowTaskHandler
    : IRequestHandler<CancelWorkflowTaskCommand, Response<WorkflowTaskTransitionResponse>>
{
    private readonly WorkflowTaskTransitionSupport _support;

    public CancelWorkflowTaskHandler(
        IApprovalTaskRepository taskRepository,
        IWorkflowInstanceRepository instanceRepository,
        IRuntimeAssignmentSnapshotRepository snapshotRepository,
        IWorkflowTransitionLogRepository logRepository)
    {
        _support = new WorkflowTaskTransitionSupport(taskRepository, instanceRepository, snapshotRepository, logRepository);
    }

    public Task<Response<WorkflowTaskTransitionResponse>> Handle(CancelWorkflowTaskCommand request, CancellationToken ct) =>
        _support.CancelAsync(
            request.TaskId,
            request.Request.ActorId,
            request.Request.ReasonCode,
            request.Request.IdempotencyKey,
            request.Request.Comment,
            request.CorrelationId,
            ct);
}

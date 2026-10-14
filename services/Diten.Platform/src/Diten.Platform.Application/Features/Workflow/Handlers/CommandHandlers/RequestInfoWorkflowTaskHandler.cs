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
        IWorkflowTransitionLogRepository logRepository,
        IPlatformTransactionExecutor? transactions = null)
    {
        // D1-FIX1 — the transition commits in one Platform transaction (the executor comes from DI; null keeps the
        // sequential writes for in-memory doubles).
        _support = new WorkflowTaskTransitionSupport(taskRepository, instanceRepository, snapshotRepository, logRepository,
            seams: new WorkflowTransitionSeams(null, transactions, null, null));
    }

    public Task<Response<WorkflowTaskTransitionResponse>> Handle(RequestInfoWorkflowTaskCommand request, CancellationToken ct) =>
        _support.RequestInfoAsync(
            request.TaskId,
            request.Request.ActorId ?? string.Empty,
            request.Request.TargetPrincipalId,
            request.Request.ReasonCode,
            request.Request.IdempotencyKey,
            request.Request.ExpectedVersion,
            request.Request.Comment,
            request.Request.EvidenceRef,
            request.CorrelationId,
            ct);
}

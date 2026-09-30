using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;

public sealed class RejectWorkflowTaskHandler
    : IRequestHandler<RejectWorkflowTaskCommand, Response<WorkflowTaskTransitionResponse>>
{
    private readonly WorkflowTaskTransitionSupport _support;

    public RejectWorkflowTaskHandler(
        IApprovalTaskRepository taskRepository,
        IWorkflowInstanceRepository instanceRepository,
        IRuntimeAssignmentSnapshotRepository snapshotRepository,
        IWorkflowTransitionLogRepository logRepository,
        IPlatformTransactionExecutor? transactions = null,
        ITransactionalIntegrationEventWriter? events = null,
        IWorkflowTemplateRepository? templates = null)
    {
        // WP-CL-BE-3 — terminal transitions commit with the completion event (seams from DI).
        _support = new WorkflowTaskTransitionSupport(taskRepository, instanceRepository, snapshotRepository, logRepository,
            seams: new WorkflowTransitionSeams(null, transactions, events, templates));
    }

    public Task<Response<WorkflowTaskTransitionResponse>> Handle(RejectWorkflowTaskCommand request, CancellationToken ct) =>
        _support.TransitionAsync(
            request.TaskId,
            WorkflowTransitionAction.Reject,
            request.Request.ActorId,
            request.Request.ReasonCode,
            request.Request.IdempotencyKey,
            request.Request.Comment,
            request.Request.EvidenceRef,
            request.CorrelationId,
            ct);
}

using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;

/// <summary>FIX2 item 5 — see <see cref="SupersedeWorkflowHalfStartCommand"/>.</summary>
public sealed class SupersedeWorkflowHalfStartHandler
    : IRequestHandler<SupersedeWorkflowHalfStartCommand, Response<CloseAbandonedWorkflowStartResult>>
{
    private readonly IWorkflowInstanceRepository _instances;
    private readonly IApprovalTaskRepository _tasks;
    private readonly IWorkflowTransitionLogRepository _logs;
    private readonly ICurrentUserContext _currentUser;
    private readonly WorkflowTerminalTransitionWriter _terminal;

    public SupersedeWorkflowHalfStartHandler(
        IWorkflowInstanceRepository instances,
        IApprovalTaskRepository tasks,
        IWorkflowTransitionLogRepository logs,
        ICurrentUserContext currentUser,
        IPlatformTransactionExecutor? transactions = null,
        ITransactionalIntegrationEventWriter? events = null,
        IWorkflowTemplateRepository? templates = null)
    {
        _instances = instances;
        _tasks = tasks;
        _logs = logs;
        _currentUser = currentUser;
        _terminal = new WorkflowTerminalTransitionWriter(tasks, instances, logs, transactions, events, templates);
    }

    public async Task<Response<CloseAbandonedWorkflowStartResult>> Handle(
        SupersedeWorkflowHalfStartCommand request,
        CancellationToken ct)
    {
        var instance = await _instances.GetByIdAsync(request.WorkflowInstanceId, ct);
        if (instance is null)
        {
            return Response<CloseAbandonedWorkflowStartResult>.Fail(
                "Workflow instance not found.", 404, WorkflowReasonCodes.NotFoundNonLeakage, request.CorrelationId);
        }

        // A half start already closed — by an earlier supersede, or by the abandoned-start recovery meanwhile (FIX3 K9) — is
        // the outcome asked for: "already closed", not a refusal.
        if (instance.Status == WorkflowInstanceStatus.Cancelled
            && (string.Equals(instance.StartClosedReasonCode, WorkflowReasonCodes.WorkflowStartSuperseded, StringComparison.Ordinal)
                || string.Equals(instance.StartClosedReasonCode, WorkflowReasonCodes.WorkflowStartAbandoned, StringComparison.Ordinal)))
        {
            return Response<CloseAbandonedWorkflowStartResult>.Success(
                new CloseAbandonedWorkflowStartResult(instance.Id, Guid.Empty, AlreadyClosed: true),
                correlationId: request.CorrelationId);
        }

        if (instance.TrustedConsumerClientId is not null
            || instance.Status != WorkflowInstanceStatus.Active
            // Any task this tenant's task store still lists — open or decided — and it is not a half start: its own transitions
            // own it. (FIX3 K13: a soft-deleted task is not listed here; the recovery's own scan counts it.)
            || (await _tasks.ListByInstanceIdAsync(instance.Id, ct)).Count > 0)
        {
            return Response<CloseAbandonedWorkflowStartResult>.Fail(
                "The workflow instance is not a half start; nothing was closed.",
                409,
                WorkflowReasonCodes.WorkflowStartNotAbandoned,
                request.CorrelationId);
        }

        var actor = _currentUser.UserId == Guid.Empty ? _currentUser.ActorName : _currentUser.UserId.ToString();
        return await CloseAbandonedWorkflowStartHandler.CloseTasklessAsync(
            _terminal, _logs, instance, WorkflowReasonCodes.WorkflowStartSuperseded, actor,
            $"workflow-half-start-supersede:{instance.Id:N}", request.CorrelationId, DateTimeOffset.UtcNow, ct);
    }
}

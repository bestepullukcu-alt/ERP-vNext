using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;

// BL-509 — the task request's own three writes. ACCEPTING one is AcceptTaskItemHandler (the same verb an assignee
// has always used); sending the first one is CreateTaskItemHandler. Every write here is an expected-version
// conditional write, so of two people answering at the same moment exactly one lands and the other reads 409.
//
// An admission transition, not an approval (CT decision, 2026-10-02): the person asked is saying whether they take
// the work on, which is the mirror of `accept`; nobody here rules on whether the work may proceed.

/// <summary>Decline a request addressed to me. The task goes back to its sender, unanswered and unheld.</summary>
public sealed class DeclineTaskOfferHandler : IRequestHandler<DeclineTaskOfferCommand, Response<NoContent>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskAssignmentRepository _assignments;
    private readonly ITaskRequestService _requests;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;

    public DeclineTaskOfferHandler(
        ITaskItemRepository tasks,
        ITaskAssignmentRepository assignments,
        ITaskRequestService requests,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext)
    {
        _tasks = tasks;
        _assignments = assignments;
        _requests = requests;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
    }

    public async Task<Response<NoContent>> Handle(DeclineTaskOfferCommand command, CancellationToken ct)
    {
        // The same rule, and the same code, a return carries: the sender is being told why.
        var reason = command.Request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Response<NoContent>.Fail(
                "Say why the request is being declined.",
                400, TaskReasonCodes.HandoverReasonRequired, command.CorrelationId);
        }

        // It is stored on the task and shown to the sender; the same ceiling a task's own description has.
        if (reason.Length > TaskFieldLimits.MaxDescriptionLength)
        {
            return Response<NoContent>.Fail(
                $"The reason may be at most {TaskFieldLimits.MaxDescriptionLength} characters.",
                400, TaskReasonCodes.RequestDeclineReasonTooLong, command.CorrelationId);
        }

        var task = await _tasks.GetByIdAsync(command.Id, ct);
        if (task is null)
        {
            return Response<NoContent>.Fail("Task not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        if (!TaskRequestPolicy.IsPendingOffer(task))
        {
            return Response<NoContent>.Fail(
                "This request is no longer waiting for an answer.",
                409, TaskReasonCodes.RequestNotPending, command.CorrelationId);
        }

        var me = _currentUser.UserId;
        if (!TaskRequestPolicy.IsOfferedTo(task, me))
        {
            return Response<NoContent>.Fail(
                "Only the person a request is addressed to can decline it.",
                403, TaskReasonCodes.RequestNotAddressee, command.CorrelationId);
        }

        task.OfferedToUserId = null;
        task.OfferDeclineReason = reason;
        task.UpdatedBy = _currentUser.ActorName;
        task.Declare(TaskTransitionKind.Declined, me, reason);

        if (!await _tasks.UpdateAsync(task, command.Request.ExpectedVersion, ct))
        {
            return Response<NoContent>.Fail(
                "The task changed meanwhile; reload and retry.",
                409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        await _assignments.CreateAsync(new TaskAssignment
        {
            TenantId = _tenantContext.TenantId,
            TaskItemId = task.Id,
            EventType = TaskAssignmentEventType.Declined,
            UserId = me,
            ActorUserId = me,
            ActorLegalEntityId = await _requests.LegalEntityOfAsync(me, ct),
            TargetLegalEntityId = task.CreatedByUserId is { } sender
                ? await _requests.LegalEntityOfAsync(sender, ct)
                : null,
            Note = reason,
            CreatedBy = _currentUser.ActorName
        }, ct);

        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}

/// <summary>The sender takes a request back before it is answered. After acceptance there is nothing to take back —
/// the task is somebody's work — and this refuses.</summary>
public sealed class WithdrawTaskOfferHandler : IRequestHandler<WithdrawTaskOfferCommand, Response<NoContent>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskAssignmentRepository _assignments;
    private readonly ITaskRequestService _requests;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;

    public WithdrawTaskOfferHandler(
        ITaskItemRepository tasks,
        ITaskAssignmentRepository assignments,
        ITaskRequestService requests,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext)
    {
        _tasks = tasks;
        _assignments = assignments;
        _requests = requests;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
    }

    public async Task<Response<NoContent>> Handle(WithdrawTaskOfferCommand command, CancellationToken ct)
    {
        var task = await _tasks.GetByIdAsync(command.Id, ct);
        if (task is null)
        {
            return Response<NoContent>.Fail("Task not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        var me = _currentUser.UserId;
        if (task.CreatedByUserId is null || task.CreatedByUserId != me)
        {
            return Response<NoContent>.Fail(
                "Only the person who sent a request can withdraw it.",
                403, TaskReasonCodes.RequestNotSender, command.CorrelationId);
        }

        // Before acceptance ONLY. An accepted request is a Person task with a holder; a declined or already
        // withdrawn one has nobody it is addressed to. Neither is pending, and both answer the same code.
        if (!TaskRequestPolicy.IsPendingOffer(task))
        {
            return Response<NoContent>.Fail(
                "This request is no longer waiting for an answer.",
                409, TaskReasonCodes.RequestNotPending, command.CorrelationId);
        }

        var askedOf = task.OfferedToUserId;
        var note = string.IsNullOrWhiteSpace(command.Request.Note) ? null : command.Request.Note.Trim();

        task.OfferedToUserId = null;
        task.OfferDeclineReason = null;
        task.UpdatedBy = _currentUser.ActorName;
        task.Declare(TaskTransitionKind.Withdrawn, me, note);

        if (!await _tasks.UpdateAsync(task, command.Request.ExpectedVersion, ct))
        {
            return Response<NoContent>.Fail(
                "The task changed meanwhile; reload and retry.",
                409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        await _assignments.CreateAsync(new TaskAssignment
        {
            TenantId = _tenantContext.TenantId,
            TaskItemId = task.Id,
            EventType = TaskAssignmentEventType.Withdrawn,
            UserId = askedOf,
            ActorUserId = me,
            ActorLegalEntityId = await _requests.LegalEntityOfAsync(me, ct),
            TargetLegalEntityId = askedOf is { } target ? await _requests.LegalEntityOfAsync(target, ct) : null,
            Note = note,
            CreatedBy = _currentUser.ActorName
        }, ct);

        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}

/// <summary>The sender addresses a declined or withdrawn request to somebody else — the same checks the first
/// send passed, asked again of the new addressee.</summary>
public sealed class OfferTaskItemHandler : IRequestHandler<OfferTaskItemCommand, Response<NoContent>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskAssignmentRepository _assignments;
    private readonly ITaskRequestService _requests;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;

    public OfferTaskItemHandler(
        ITaskItemRepository tasks,
        ITaskAssignmentRepository assignments,
        ITaskRequestService requests,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext)
    {
        _tasks = tasks;
        _assignments = assignments;
        _requests = requests;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
    }

    public async Task<Response<NoContent>> Handle(OfferTaskItemCommand command, CancellationToken ct)
    {
        var task = await _tasks.GetByIdAsync(command.Id, ct);
        if (task is null)
        {
            return Response<NoContent>.Fail("Task not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        var me = _currentUser.UserId;
        if (task.CreatedByUserId is null || task.CreatedByUserId != me)
        {
            return Response<NoContent>.Fail(
                "Only the person who sent a request can address it to somebody else.",
                403, TaskReasonCodes.RequestNotSender, command.CorrelationId);
        }

        // An open request with nobody it is addressed to: declined, or withdrawn. Anything else is not this verb's.
        if (!task.IsRequest
            || task.AssignmentTarget != TaskAssignmentTarget.Offered
            || task.AssigneeUserId is not null
            || task.Lifecycle is TaskLifecycle.Done or TaskLifecycle.Cancelled)
        {
            return Response<NoContent>.Fail(
                "This task is not an open request.",
                409, TaskReasonCodes.RequestNotPending, command.CorrelationId);
        }

        if (task.OfferedToUserId is not null)
        {
            return Response<NoContent>.Fail(
                "This request is still waiting for an answer. Withdraw it first.",
                409, TaskReasonCodes.RequestPending, command.CorrelationId);
        }

        var (refused, recipient) = await _requests.CheckSendAsync(command.Request.OfferedToUserId, ct);
        if (refused is not null)
        {
            return Response<NoContent>.Fail(
                refused.Message, refused.StatusCode, refused.ReasonCode, command.CorrelationId);
        }

        task.OfferedToUserId = recipient!.UserId;
        task.OfferDeclineReason = null;
        task.UpdatedBy = _currentUser.ActorName;
        task.Declare(TaskTransitionKind.Offered, me);

        if (!await _tasks.UpdateAsync(task, command.Request.ExpectedVersion, ct))
        {
            return Response<NoContent>.Fail(
                "The task changed meanwhile; reload and retry.",
                409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        await _assignments.CreateAsync(new TaskAssignment
        {
            TenantId = _tenantContext.TenantId,
            TaskItemId = task.Id,
            EventType = TaskAssignmentEventType.Offered,
            UserId = recipient.UserId,
            ActorUserId = me,
            ActorLegalEntityId = await _requests.LegalEntityOfAsync(me, ct),
            TargetLegalEntityId = recipient.LegalEntityId,
            CreatedBy = _currentUser.ActorName
        }, ct);

        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}

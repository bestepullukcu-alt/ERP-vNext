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
// conditional write, so of two people answering at the same moment exactly one lands and the other is refused.
//
// An admission transition, not an approval (CT decision, 2026-10-02): the person asked is saying whether they take
// the work on, which is the mirror of `accept`; nobody here rules on whether the work may proceed.
//
// THE ORDER OF THE QUESTIONS IS PART OF THE RULE (CT, D1 fix round 1). Each handler asks, in this order:
//   1. may the caller READ the task at all?     → 404, exactly what a task that does not exist answers;
//   2. is the caller the one this act belongs to → 403 (the person asked / the sender);
//   3. is the request in a state that allows it → 409.
// Asked the other way round, a caller who cannot even see the task would learn from a 409 that it is a request
// waiting for somebody's answer. And when an act names a NEW person (offer), whether that person can be reached is
// asked LAST, after every other check — see OfferTaskItemHandler.

/// <summary>Decline a request addressed to me. The task goes back to its sender, unanswered and unheld.</summary>
public sealed class DeclineTaskOfferHandler : IRequestHandler<DeclineTaskOfferCommand, Response<NoContent>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskAssignmentRepository _assignments;
    private readonly ITaskRequestService _requests;
    private readonly ITaskReadAccessPolicy _readAccess;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;

    public DeclineTaskOfferHandler(
        ITaskItemRepository tasks,
        ITaskAssignmentRepository assignments,
        ITaskRequestService requests,
        ITaskReadAccessPolicy readAccess,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext)
    {
        _tasks = tasks;
        _assignments = assignments;
        _requests = requests;
        _readAccess = readAccess;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
    }

    public async Task<Response<NoContent>> Handle(DeclineTaskOfferCommand command, CancellationToken ct)
    {
        var me = _currentUser.UserId;
        var task = await _tasks.GetByIdAsync(command.Id, ct);
        if (task is null || !await _readAccess.CanReadAsync(task, me, ct))
        {
            return Response<NoContent>.Fail("Task not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        if (task.OfferedToUserId != me)
        {
            return Response<NoContent>.Fail(
                "Only the person a request is addressed to can decline it.",
                403, TaskReasonCodes.RequestNotAddressee, command.CorrelationId);
        }

        if (!TaskRequestPolicy.IsOfferedTo(task, me))
        {
            return Response<NoContent>.Fail(
                "This request is no longer waiting for an answer.",
                409, TaskReasonCodes.RequestNotPending, command.CorrelationId);
        }

        // FIX2 (A5) — THE DECISION AND THE WRITE ARE ABOUT THE SAME DOCUMENT. The answer is decided on the task read
        // above; the write used to be conditioned on the CLIENT's version only, so a client one version ahead of that
        // read (a withdraw sent after the accept it raced with) replaced the newer document wholesale — the accept
        // silently gone. The read version must be the client's, and the write is conditioned on it.
        if (task.Version != command.Request.ExpectedVersion)
        {
            return Response<NoContent>.Fail(
                "The task changed meanwhile; reload and retry.",
                409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        // FIX2 — validation LAST: read (404) → addressee (403) → pending (409) → version (409) → the reason (400).
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

        // FIX2 note — both companies from ONE read of the seats, positions and units (it was two full reads).
        var entities = await _requests.LegalEntitiesOfAsync(
            task.CreatedByUserId is { } from ? [me, from] : [me], ct);
        await _assignments.CreateAsync(new TaskAssignment
        {
            TenantId = _tenantContext.TenantId,
            TaskItemId = task.Id,
            EventType = TaskAssignmentEventType.Declined,
            UserId = me,
            ActorUserId = me,
            ActorLegalEntityId = entities.GetValueOrDefault(me),
            TargetLegalEntityId = task.CreatedByUserId is { } sender ? entities.GetValueOrDefault(sender) : null,
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
    private readonly ITaskReadAccessPolicy _readAccess;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;

    public WithdrawTaskOfferHandler(
        ITaskItemRepository tasks,
        ITaskAssignmentRepository assignments,
        ITaskRequestService requests,
        ITaskReadAccessPolicy readAccess,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext)
    {
        _tasks = tasks;
        _assignments = assignments;
        _requests = requests;
        _readAccess = readAccess;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
    }

    public async Task<Response<NoContent>> Handle(WithdrawTaskOfferCommand command, CancellationToken ct)
    {
        var me = _currentUser.UserId;
        var task = await _tasks.GetByIdAsync(command.Id, ct);
        if (task is null || !await _readAccess.CanReadAsync(task, me, ct))
        {
            return Response<NoContent>.Fail("Task not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

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

        // FIX2 (A5) — THE DECISION AND THE WRITE ARE ABOUT THE SAME DOCUMENT. The answer is decided on the task read
        // above; the write used to be conditioned on the CLIENT's version only, so a client one version ahead of that
        // read (a withdraw sent after the accept it raced with) replaced the newer document wholesale — the accept
        // silently gone. The read version must be the client's, and the write is conditioned on it.
        if (task.Version != command.Request.ExpectedVersion)
        {
            return Response<NoContent>.Fail(
                "The task changed meanwhile; reload and retry.",
                409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
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

        // FIX2 note — both companies from ONE read of the seats, positions and units (it was two full reads).
        var entities = await _requests.LegalEntitiesOfAsync(askedOf is { } to ? [me, to] : [me], ct);
        await _assignments.CreateAsync(new TaskAssignment
        {
            TenantId = _tenantContext.TenantId,
            TaskItemId = task.Id,
            EventType = TaskAssignmentEventType.Withdrawn,
            UserId = askedOf,
            ActorUserId = me,
            ActorLegalEntityId = entities.GetValueOrDefault(me),
            TargetLegalEntityId = askedOf is { } target ? entities.GetValueOrDefault(target) : null,
            Note = note,
            CreatedBy = _currentUser.ActorName
        }, ct);

        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}

/// <summary>The sender addresses a declined or withdrawn request to somebody else — the same checks the first
/// send passed, asked again of the new addressee, and asked LAST.</summary>
public sealed class OfferTaskItemHandler : IRequestHandler<OfferTaskItemCommand, Response<NoContent>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskAssignmentRepository _assignments;
    private readonly ITaskRequestService _requests;
    private readonly ITaskReadAccessPolicy _readAccess;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;

    public OfferTaskItemHandler(
        ITaskItemRepository tasks,
        ITaskAssignmentRepository assignments,
        ITaskRequestService requests,
        ITaskReadAccessPolicy readAccess,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext)
    {
        _tasks = tasks;
        _assignments = assignments;
        _requests = requests;
        _readAccess = readAccess;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
    }

    public async Task<Response<NoContent>> Handle(OfferTaskItemCommand command, CancellationToken ct)
    {
        var me = _currentUser.UserId;
        var task = await _tasks.GetByIdAsync(command.Id, ct);
        if (task is null || !await _readAccess.CanReadAsync(task, me, ct))
        {
            return Response<NoContent>.Fail("Task not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

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

        if (await _requests.CheckSenderAsync(command.Request.OfferedToUserId, ct) is { } senderRefused)
        {
            return Response<NoContent>.Fail(
                senderRefused.Message, senderRefused.StatusCode, senderRefused.ReasonCode, command.CorrelationId);
        }

        /*
         * A stale version is refused HERE, before the addressee is asked, not by the write after it. Left to the
         * write, an out-of-date page would read 409 for a person who can be reached and 400 for one who cannot —
         * and the difference would say who exists. The write below still carries the version condition, so a race
         * between this read and that write is still decided by the database.
         */
        if (task.Version != command.Request.ExpectedVersion)
        {
            return Response<NoContent>.Fail(
                "The task changed meanwhile; reload and retry.",
                409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        // LAST: whether the new addressee can be reached — the one answer for every "no".
        var (refused, recipient) = await _requests.CheckRecipientAsync(command.Request.OfferedToUserId, ct);
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

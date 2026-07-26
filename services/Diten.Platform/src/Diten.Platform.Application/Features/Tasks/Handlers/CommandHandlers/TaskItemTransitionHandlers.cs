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

// MOD-0024 — ownership/admission and lifecycle transitions. Every one is an expected-version conditional write,
// so a concurrent change yields a controlled 409 instead of clobbering (pack §13).

/// <summary>Accept a task assigned to me — the Inbox acceptance gate closes and I become the holder.</summary>
public sealed class AcceptTaskItemHandler : IRequestHandler<AcceptTaskItemCommand, Response<NoContent>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskAssignmentRepository _assignments;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;

    public AcceptTaskItemHandler(
        ITaskItemRepository tasks,
        ITaskAssignmentRepository assignments,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext)
    {
        _tasks = tasks;
        _assignments = assignments;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
    }

    public async Task<Response<NoContent>> Handle(AcceptTaskItemCommand command, CancellationToken ct)
    {
        var task = await _tasks.GetByIdAsync(command.Id, ct);
        if (task is null)
        {
            return Response<NoContent>.Fail("Task not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        if (task.AssignmentTarget != TaskAssignmentTarget.Person || task.AssigneeUserId != _currentUser.UserId)
        {
            return Response<NoContent>.Fail(
                "Only the assignee can accept this task.", 403, TaskReasonCodes.InvalidState, command.CorrelationId);
        }

        // Acceptance is signalled by leaving the pre-acceptance lifecycle; Planned is preserved if already planned.
        if (task.Lifecycle == TaskLifecycle.Open)
        {
            task.Lifecycle = TaskLifecycle.InProgress;
        }

        task.UpdatedBy = _currentUser.ActorName;

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
            EventType = TaskAssignmentEventType.Accepted,
            UserId = _currentUser.UserId,
            ActorUserId = _currentUser.UserId,
            ReasonCode = command.Request.ReasonCode,
            Note = command.Request.Note,
            CreatedBy = _currentUser.ActorName
        }, ct);

        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}

/// <summary>Return a claimed pool task to its pool: ownership → unowned, admission → pendingClaim.</summary>
public sealed class ReleaseTaskItemHandler : IRequestHandler<ReleaseTaskItemCommand, Response<NoContent>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskAssignmentRepository _assignments;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;

    public ReleaseTaskItemHandler(
        ITaskItemRepository tasks,
        ITaskAssignmentRepository assignments,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext)
    {
        _tasks = tasks;
        _assignments = assignments;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
    }

    public async Task<Response<NoContent>> Handle(ReleaseTaskItemCommand command, CancellationToken ct)
    {
        var task = await _tasks.GetByIdAsync(command.Id, ct);
        if (task is null)
        {
            return Response<NoContent>.Fail("Task not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        if (task.AssignmentTarget != TaskAssignmentTarget.PositionPool || task.AssigneeUserId is null)
        {
            return Response<NoContent>.Fail(
                "Only a claimed pooled task can be released.",
                409, TaskReasonCodes.InvalidState, command.CorrelationId);
        }

        if (task.AssigneeUserId != _currentUser.UserId)
        {
            return Response<NoContent>.Fail(
                "Only the current holder can release this task.",
                403, TaskReasonCodes.InvalidState, command.CorrelationId);
        }

        var previousHolder = task.AssigneeUserId;

        // Back to the pool: no holder, and the lifecycle rewinds to Open so it reads as fresh pool work.
        task.AssigneeUserId = null;
        task.Lifecycle = TaskLifecycle.Open;
        task.UpdatedBy = _currentUser.ActorName;

        if (!await _tasks.UpdateAsync(task, command.Request.ExpectedVersion, ct))
        {
            return Response<NoContent>.Fail(
                "The task changed meanwhile; reload and retry.",
                409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        // "Released" is an activity EVENT, never an ownership state (pack boundary).
        await _assignments.CreateAsync(new TaskAssignment
        {
            TenantId = _tenantContext.TenantId,
            TaskItemId = task.Id,
            EventType = TaskAssignmentEventType.Released,
            UserId = previousHolder,
            PositionId = task.PoolPositionId,
            ActorUserId = _currentUser.UserId,
            ReasonCode = command.Request.ReasonCode,
            Note = command.Request.Note,
            CreatedBy = _currentUser.ActorName
        }, ct);

        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}

/// <summary>
/// Plan / start / complete / cancel. The permitted set comes from <see cref="ITaskLifecycleService"/> — this
/// handler never encodes its own rules, so an approval-gated task cannot be started here (pack §12 K2).
/// </summary>
public sealed class TransitionTaskItemHandler : IRequestHandler<TransitionTaskItemCommand, Response<NoContent>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskLifecycleService _lifecycle;
    private readonly ICurrentUserContext _currentUser;
    private readonly IChecklistRunRepository _checklists;
    private readonly ITaskChecklistService _checklistService;

    public TransitionTaskItemHandler(
        ITaskItemRepository tasks,
        ITaskLifecycleService lifecycle,
        ICurrentUserContext currentUser,
        IChecklistRunRepository checklists,
        ITaskChecklistService checklistService)
    {
        _tasks = tasks;
        _lifecycle = lifecycle;
        _currentUser = currentUser;
        _checklists = checklists;
        _checklistService = checklistService;
    }

    public async Task<Response<NoContent>> Handle(TransitionTaskItemCommand command, CancellationToken ct)
    {
        var task = await _tasks.GetByIdAsync(command.Id, ct);
        if (task is null)
        {
            return Response<NoContent>.Fail("Task not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        if (!_lifecycle.CanTransition(task, command.Target, out var reasonCode))
        {
            return Response<NoContent>.Fail(
                "This transition is not allowed in the task's current state.",
                409, reasonCode ?? TaskReasonCodes.InvalidState, command.CorrelationId);
        }

        // Checklist gate, enforced HERE and not only in the projection: the projection disables the button, but a
        // caller can post straight to this endpoint. Hiding a control is presentation; refusing the write is the
        // rule (pack §12 E1).
        //
        // Only Blocking items gate completion. An unfinished `Required` item is an expectation, not a barrier —
        // and open SUBTASKS never gate it at all, because two competing blocking mechanisms make "why can't I
        // finish this?" unanswerable.
        if (command.Target == TaskLifecycle.Done)
        {
            var checklist = await _checklists.GetByTaskIdAsync(task.Id, ct);
            if (_checklistService.BlocksCompletion(checklist))
            {
                return Response<NoContent>.Fail(
                    "A blocking checklist item is still open.",
                    409, TaskReasonCodes.ChecklistIncomplete, command.CorrelationId);
            }
        }

        task.Lifecycle = command.Target;
        task.UpdatedBy = _currentUser.ActorName;

        switch (command.Target)
        {
            case TaskLifecycle.Done:
                task.CompletedAt = DateTimeOffset.UtcNow;
                task.ClosureReasonCode = command.Request.ReasonCode;
                break;
            case TaskLifecycle.Cancelled:
                task.CancelledAt = DateTimeOffset.UtcNow;
                task.ClosureReasonCode = command.Request.ReasonCode;
                break;
            case TaskLifecycle.InProgress when task.StartAt is null:
                task.StartAt = DateTimeOffset.UtcNow;
                break;
        }

        if (!await _tasks.UpdateAsync(task, command.Request.ExpectedVersion, ct))
        {
            return Response<NoContent>.Fail(
                "The task changed meanwhile; reload and retry.",
                409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        if (command.Target == TaskLifecycle.Cancelled)
        {
            await CancelOpenSubtasksAsync(task, command, ct);
        }

        return Response<NoContent>.Success(204, command.CorrelationId);
    }

    /// <summary>
    /// Cancelling a parent cancels its still-open subtasks.
    ///
    /// <para>A subtask exists to serve its parent, so leaving it open would strand work in someone's İşlerim with
    /// no remaining reason to do it — and the person holding it has no way to discover the parent was called off.
    /// Subtasks already FINISHED (Done) or already Cancelled are left untouched: history is not rewritten.</para>
    ///
    /// <para>Best effort per child: one child losing an expected-version race must not fail the parent's
    /// cancellation, which already committed above.</para>
    /// </summary>
    private async Task CancelOpenSubtasksAsync(TaskItem parent, TransitionTaskItemCommand command, CancellationToken ct)
    {
        var children = await _tasks.ListByParentAsync(parent.Id, ct);
        foreach (var child in children)
        {
            if (child.Lifecycle is TaskLifecycle.Done or TaskLifecycle.Cancelled)
            {
                continue;
            }

            child.Lifecycle = TaskLifecycle.Cancelled;
            child.CancelledAt = DateTimeOffset.UtcNow;
            child.ClosureReasonCode = command.Request.ReasonCode;
            child.UpdatedBy = _currentUser.ActorName;

            await _tasks.UpdateAsync(child, child.Version, ct);
        }
    }
}

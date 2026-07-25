using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;

/// <summary>
/// MOD-0024 — take an unclaimed pool task.
///
/// <para><b>The race that matters.</b> Two people can press "Üzerime al" at the same instant. The claim is a
/// conditional write on the task's expected version, so exactly ONE update matches and the other caller gets a
/// controlled 409 <c>TASK_ALREADY_CLAIMED</c> with a refreshed projection — never two owners, never a silent
/// overwrite (pack §13).</para>
/// </summary>
public sealed class ClaimTaskItemHandler : IRequestHandler<ClaimTaskItemCommand, Response<NoContent>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskAssignmentRepository _assignments;
    private readonly IPositionAssignmentRepository _positionAssignments;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;

    public ClaimTaskItemHandler(
        ITaskItemRepository tasks,
        ITaskAssignmentRepository assignments,
        IPositionAssignmentRepository positionAssignments,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext)
    {
        _tasks = tasks;
        _assignments = assignments;
        _positionAssignments = positionAssignments;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
    }

    public async Task<Response<NoContent>> Handle(ClaimTaskItemCommand command, CancellationToken ct)
    {
        var task = await _tasks.GetByIdAsync(command.Id, ct);
        if (task is null)
        {
            return Response<NoContent>.Fail(
                "Task not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        if (task.AssignmentTarget != TaskAssignmentTarget.PositionPool)
        {
            return Response<NoContent>.Fail(
                "Only pooled tasks can be claimed.", 409, TaskReasonCodes.NotClaimable, command.CorrelationId);
        }

        if (task.Lifecycle is TaskLifecycle.Done or TaskLifecycle.Cancelled)
        {
            return Response<NoContent>.Fail(
                "A closed task cannot be claimed.", 409, TaskReasonCodes.InvalidState, command.CorrelationId);
        }

        // Fast path: already taken. The conditional write below is still the real guard — this only produces a
        // clearer error when the caller's view was stale before they even pressed the button.
        if (task.AssigneeUserId is not null)
        {
            return Response<NoContent>.Fail(
                "This task has already been claimed.", 409, TaskReasonCodes.AlreadyClaimed, command.CorrelationId);
        }

        var actorId = _currentUser.UserId;

        // Only an active holder of the offered position may claim it — otherwise a pool would be a public queue.
        if (!await HoldsPositionAsync(actorId, task.PoolPositionId, ct))
        {
            return Response<NoContent>.Fail(
                "You do not hold the position this task was offered to.",
                403, TaskReasonCodes.NotClaimable, command.CorrelationId);
        }

        task.AssigneeUserId = actorId;
        task.UpdatedBy = _currentUser.ActorName;

        var updated = await _tasks.UpdateAsync(task, command.Request.ExpectedVersion, ct);
        if (!updated)
        {
            // Someone else won the race (or the caller's version was stale). Both are the same story to the user.
            return Response<NoContent>.Fail(
                "This task has already been claimed.", 409, TaskReasonCodes.AlreadyClaimed, command.CorrelationId);
        }

        await _assignments.CreateAsync(new TaskAssignment
        {
            TenantId = _tenantContext.TenantId,
            TaskItemId = task.Id,
            EventType = TaskAssignmentEventType.Claimed,
            UserId = actorId,
            PositionId = task.PoolPositionId,
            ActorUserId = actorId,
            CreatedBy = _currentUser.ActorName
        }, ct);

        // OD-2: the other position holders are deliberately NOT notified that the work is gone.
        return Response<NoContent>.Success(204, command.CorrelationId);
    }

    private async Task<bool> HoldsPositionAsync(Guid userId, Guid? positionId, CancellationToken ct)
    {
        if (positionId is null)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        var assignments = await _positionAssignments.GetAllAsync(ct);
        return assignments.Any(a => a.PositionId == positionId
                                    && a.UserId == userId
                                    && !a.IsCancelled
                                    && a.EffectiveFrom <= now
                                    && (a.EffectiveTo is null || a.EffectiveTo > now));
    }
}

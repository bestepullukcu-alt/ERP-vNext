using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;

/// <summary>
/// MOD-0024 — the current actor's task list: work they hold, plus unclaimed pool work offered to a position they
/// occupy. Tenant isolation comes from the repository execution filter, so a cross-tenant task can never appear.
/// </summary>
public sealed class GetTaskItemListHandler
    : IRequestHandler<GetTaskItemListQuery, Response<IReadOnlyList<TaskItemListItemDto>>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly IPositionAssignmentRepository _positionAssignments;
    private readonly ITaskLifecycleService _lifecycle;
    private readonly ICurrentUserContext _currentUser;

    public GetTaskItemListHandler(
        ITaskItemRepository tasks,
        IPositionAssignmentRepository positionAssignments,
        ITaskLifecycleService lifecycle,
        ICurrentUserContext currentUser)
    {
        _tasks = tasks;
        _positionAssignments = positionAssignments;
        _lifecycle = lifecycle;
        _currentUser = currentUser;
    }

    public async Task<Response<IReadOnlyList<TaskItemListItemDto>>> Handle(
        GetTaskItemListQuery request,
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;

        var mine = await _tasks.ListByAssigneeAsync(userId, ct);

        // Pool work is offered to POSITIONS, so resolve the actor's currently-held positions first. Intervals are
        // half-open: EffectiveFrom <= now && (EffectiveTo == null || EffectiveTo > now).
        var positionIds = await ResolveActivePositionIdsAsync(userId, ct);
        var pooled = await _tasks.ListUnclaimedByPositionsAsync(positionIds, ct);

        IReadOnlyList<TaskItemListItemDto> result = mine
            .Concat(pooled)
            .DistinctBy(t => t.Id)
            .OrderBy(t => t.DueAt ?? DateTimeOffset.MaxValue)
            .Select(t => TaskItemMapper.ToListItem(t, _lifecycle))
            .ToList();

        return Response<IReadOnlyList<TaskItemListItemDto>>.Success(result, correlationId: request.CorrelationId);
    }

    private async Task<IReadOnlyList<Guid>> ResolveActivePositionIdsAsync(Guid userId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var assignments = await _positionAssignments.GetAllAsync(ct);
        return assignments
            .Where(a => a.UserId == userId
                        && !a.IsCancelled
                        && a.EffectiveFrom <= now
                        && (a.EffectiveTo is null || a.EffectiveTo > now))
            .Select(a => a.PositionId)
            .Distinct()
            .ToList();
    }
}

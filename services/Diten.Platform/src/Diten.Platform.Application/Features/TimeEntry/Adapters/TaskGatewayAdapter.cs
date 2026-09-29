using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.TimeEntry.Adapters;

/// <summary>
/// v1 <see cref="ITimeEntryTaskGateway"/> over MOD-0024's own read repositories and its OWN read-access rule
/// (<see cref="ITaskReadAccessPolicy"/>, BL-349) — the one rule every MOD-0024 read endpoint asks, so time can be
/// recorded against exactly the tasks the person can open. Batched reads; tenant-scoped by the repositories, so another
/// tenant's task simply does not exist here.
/// </summary>
public sealed class TaskGatewayAdapter : ITimeEntryTaskGateway
{
    /// <summary>The kinds that move a task away from its holder without necessarily leaving InProgress.</summary>
    private static readonly HashSet<TaskTransitionKind> HolderMoves =
    [
        TaskTransitionKind.Reassigned, TaskTransitionKind.Released, TaskTransitionKind.Returned,
        TaskTransitionKind.Claimed, TaskTransitionKind.Unknown
    ];

    private readonly ITaskItemRepository _tasks;
    private readonly ITaskTransitionRepository _transitions;
    private readonly ITaskReadAccessPolicy _readAccess;

    public TaskGatewayAdapter(ITaskItemRepository tasks, ITaskTransitionRepository transitions, ITaskReadAccessPolicy readAccess)
    {
        _tasks = tasks;
        _transitions = transitions;
        _readAccess = readAccess;
    }

    public async Task<IReadOnlySet<Guid>> ReadableTaskIdsAsync(
        Guid userId, IReadOnlyCollection<Guid> taskIds, CancellationToken ct = default)
    {
        var readable = new HashSet<Guid>();
        if (taskIds.Count == 0)
        {
            return readable;
        }

        foreach (var task in await _tasks.ListByIdsAsync(taskIds.Distinct().ToList(), ct))
        {
            if (await _readAccess.CanReadAsync(task, userId, ct))
            {
                readable.Add(task.Id);
            }
        }

        return readable;
    }

    public async Task<IReadOnlyDictionary<Guid, TimeEntryTaskFacts>> TaskFactsAsync(
        IReadOnlyCollection<Guid> taskIds, CancellationToken ct = default)
    {
        if (taskIds.Count == 0)
        {
            return new Dictionary<Guid, TimeEntryTaskFacts>();
        }

        return (await _tasks.ListByIdsAsync(taskIds.Distinct().ToList(), ct))
            .ToDictionary(
                task => task.Id,
                task => new TimeEntryTaskFacts(task.Id, task.Lifecycle == TaskLifecycle.InProgress, task.AssigneeUserId));
    }

    public async Task<DateTimeOffset?> InvalidatedAtAsync(
        Guid taskItemId, Guid holderUserId, DateTimeOffset since, CancellationToken ct = default)
    {
        var first = (await _transitions.ListByTaskIdAsync(taskItemId, ct))
            .Where(t => t.CreatedAt >= since)
            .Where(t => t.ToLifecycle != TaskLifecycle.InProgress || HolderMoves.Contains(t.Kind))
            .OrderBy(t => t.CreatedAt.UtcTicks)
            .FirstOrDefault();
        return first?.CreatedAt;
    }

    public async Task<IReadOnlyList<TimeEntryPlannedBlock>> PlannedBlocksAsync(
        Guid userId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default)
        => (await _tasks.ListByAssigneeAsync(userId, ct))
            .Where(task => task.PlannedStartAt is { } start && start >= fromUtc && start < toUtc
                           && task.PlannedDurationMinutes is > 0)
            .Select(task => new TimeEntryPlannedBlock(task.Id, task.PlannedStartAt!.Value, task.PlannedDurationMinutes!.Value))
            .ToList();
}

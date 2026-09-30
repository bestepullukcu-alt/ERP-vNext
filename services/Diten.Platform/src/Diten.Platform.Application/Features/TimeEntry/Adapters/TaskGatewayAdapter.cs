using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Tasks;
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
        => (await ReadableTaskSummariesAsync(userId, taskIds, ct)).Keys.ToHashSet();

    public async Task<IReadOnlyDictionary<Guid, TimeEntryTaskSummary>> ReadableTaskSummariesAsync(
        Guid userId, IReadOnlyCollection<Guid> taskIds, CancellationToken ct = default)
    {
        var readable = new Dictionary<Guid, TimeEntryTaskSummary>();
        if (taskIds.Count == 0)
        {
            return readable;
        }

        foreach (var task in await _tasks.ListByIdsAsync(taskIds.Distinct().ToList(), ct))
        {
            if (await _readAccess.CanReadAsync(task, userId, ct))
            {
                readable[task.Id] = Summary(task);
            }
        }

        return readable;
    }

    /// <summary>The lifecycles a person can still put time on from the picker: work not yet finished.</summary>
    private static readonly HashSet<TaskLifecycle> OpenLifecycles =
        [TaskLifecycle.Open, TaskLifecycle.Planned, TaskLifecycle.InProgress, TaskLifecycle.Waiting];

    public async Task<IReadOnlyList<TimeEntryTaskSummary>> OwnOpenTasksAsync(Guid userId, CancellationToken ct = default)
    {
        var own = new List<TimeEntryTaskSummary>();
        foreach (var task in (await _tasks.ListByAssigneeAsync(userId, ct)).Where(t => OpenLifecycles.Contains(t.Lifecycle)))
        {
            // The holder can normally read their own task; asked anyway, so the picker has exactly one read rule (F5).
            if (await _readAccess.CanReadAsync(task, userId, ct))
            {
                own.Add(Summary(task));
            }
        }

        return own;
    }

    private static TimeEntryTaskSummary Summary(TaskItem task) => new(task.Id, task.Title, task.Lifecycle.ToString());

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
        var holder = holderUserId.ToString();
        var first = (await _transitions.ListByTaskIdAsync(taskItemId, ct))
            .Where(t => t.CreatedAt >= since)
            .Where(t => t.ToLifecycle != TaskLifecycle.InProgress || MovedAwayFrom(t, holder))
            .OrderBy(t => t.CreatedAt.UtcTicks)
            .FirstOrDefault();
        return first?.CreatedAt;
    }

    /// <summary>
    /// v3 G6 — did this transition take the task AWAY from the person? Only a recorded assignee change whose new holder is
    /// someone else (or nobody) says so. A Reassigned/Claimed/Unknown entry that left the person holding the task — a
    /// re-save, a claim by themselves — ends nothing.
    /// </summary>
    private static bool MovedAwayFrom(TaskTransition transition, string holder)
        => transition.FieldChanges.Any(c => c.Field == TaskFieldChangeCodes.Assignee
                                            && !string.Equals(c.To, holder, StringComparison.OrdinalIgnoreCase));

    public async Task<IReadOnlyList<TimeEntryPlannedBlock>> PlannedBlocksAsync(
        Guid userId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default)
        => (await _tasks.ListByAssigneeAsync(userId, ct))
            .Where(task => task.PlannedStartAt is { } start && start >= fromUtc && start < toUtc
                           && task.PlannedDurationMinutes is > 0)
            .Select(task => new TimeEntryPlannedBlock(task.Id, task.PlannedStartAt!.Value, task.PlannedDurationMinutes!.Value))
            .ToList();
}

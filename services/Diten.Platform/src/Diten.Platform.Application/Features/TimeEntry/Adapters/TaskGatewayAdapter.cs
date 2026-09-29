using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.TimeEntry.Adapters;

/// <summary>
/// v1 <see cref="ITimeEntryTaskGateway"/> over MOD-0024's own read repository and its OWN read-access rule
/// (<see cref="ITaskReadAccessPolicy"/>, BL-349) — the one rule every MOD-0024 read endpoint asks, so time can be
/// recorded against exactly the tasks the person can open. One batched task read per call; tenant-scoped by the
/// repository, so another tenant's task simply does not exist here.
/// </summary>
public sealed class TaskGatewayAdapter : ITimeEntryTaskGateway
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskReadAccessPolicy _readAccess;

    public TaskGatewayAdapter(ITaskItemRepository tasks, ITaskReadAccessPolicy readAccess)
    {
        _tasks = tasks;
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
}

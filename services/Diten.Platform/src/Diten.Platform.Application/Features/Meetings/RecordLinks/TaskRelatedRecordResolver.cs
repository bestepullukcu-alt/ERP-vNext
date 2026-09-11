using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Meetings.RecordLinks;

/// <summary>
/// Resolves a <c>RecordLink</c>'s "tasks"-side id into a title/link — the ONE resolver this slice ships. It
/// lives under <c>Features/Meetings</c> (the resolver REGISTRY's home), not under <c>Features/Tasks</c>: it is
/// a consumer of <see cref="ITaskItemRepository"/>'s existing read surface, not a change to MOD-0024 itself,
/// and keeping every registered resolver in one folder is what makes "which module codes are covered" a
/// one-directory question instead of a repo-wide grep.
///
/// <para><b>Deep link.</b> The pack's own draft named `/WorkCenterNext?item=` as "whatever the existing deep
/// link pattern is" — measured, no such pattern exists anywhere in the backend today.
/// <c>TaskWorkItemProvider</c>'s own `Source.DeepLink` for a task is `/Tasks/{id}` (its real detail page), so
/// that is the pattern reused here; see this WP's report for the discrepancy, unresolved by this slice
/// (frontend is protected, out of scope to change).</para>
/// </summary>
public sealed class TaskRelatedRecordResolver : IRelatedRecordResolver
{
    private readonly ITaskItemRepository _tasks;

    public TaskRelatedRecordResolver(ITaskItemRepository tasks) => _tasks = tasks;

    public string ModuleCode => RecordLinkModuleCodes.Tasks;

    public async Task<IReadOnlyDictionary<Guid, RelatedRecordSummary>> ResolveAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, RelatedRecordSummary>();
        }

        var tasks = await _tasks.ListByIdsAsync(ids, ct);
        return tasks.ToDictionary(
            task => task.Id,
            task => new RelatedRecordSummary(task.Title, $"/Tasks/{task.Id}"));
    }
}

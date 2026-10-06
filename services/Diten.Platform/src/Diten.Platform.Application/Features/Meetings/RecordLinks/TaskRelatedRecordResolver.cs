using Diten.Platform.Application.Features.Tasks;
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
/// <para><b>Link.</b> <see cref="TaskLinks.Detail"/> — the Task Center detail, which opens for any reader the task
/// read rule admits (BL-414). It used to reuse the work item's own <c>Source.DeepLink</c>, the record page; that
/// link is the way OUT of the Task Center to the record and stays so (<see cref="TaskLinks.Record"/>), but a
/// meeting's related-task row sends the reader TO the task, which is the detail's job.</para>
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
            task => new RelatedRecordSummary(task.Title, TaskLinks.Detail(task.Id)));
    }
}

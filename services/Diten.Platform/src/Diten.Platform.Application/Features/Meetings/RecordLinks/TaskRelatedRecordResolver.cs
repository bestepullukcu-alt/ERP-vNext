using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Services;
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
///
/// <para><b>Title — only for a reader of the task</b> (WP-WCN-TASK-REQUEST-01 FIX3, item 4). A link outlives the reasons
/// somebody could read the task it points at, and every reader of the meeting is shown its rows: the title of a task
/// the CURRENT caller cannot read (MOD-0024's read rule, batched — <see cref="ITaskReadAccessPolicy.ReadableTaskIdsAsync"/>)
/// is replaced by <see cref="TaskLinks.InaccessibleTitleCode"/>, a code the client turns into its own words. The row
/// itself stays: that the meeting is linked to SOME task is the meeting's fact, and dropping it would make one
/// reader's list silently shorter than another's.</para>
/// </summary>
public sealed class TaskRelatedRecordResolver : IRelatedRecordResolver
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskReadAccessPolicy _readAccess;
    private readonly ICurrentUserContext _currentUser;

    public TaskRelatedRecordResolver(
        ITaskItemRepository tasks, ITaskReadAccessPolicy readAccess, ICurrentUserContext currentUser)
    {
        _tasks = tasks;
        _readAccess = readAccess;
        _currentUser = currentUser;
    }

    public string ModuleCode => RecordLinkModuleCodes.Tasks;

    public async Task<IReadOnlyDictionary<Guid, RelatedRecordSummary>> ResolveAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, RelatedRecordSummary>();
        }

        var tasks = await _tasks.ListByIdsAsync(ids, ct);
        var readable = await _readAccess.ReadableTaskIdsAsync(tasks, _currentUser.UserId, ct);
        return tasks.ToDictionary(
            task => task.Id,
            task => new RelatedRecordSummary(
                readable.Contains(task.Id) ? task.Title : TaskLinks.InaccessibleTitleCode,
                TaskLinks.Detail(task.Id)));
    }
}

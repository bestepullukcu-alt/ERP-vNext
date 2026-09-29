using Diten.Platform.Application.Contracts;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.TimeEntry.Services;

/// <summary>
/// MOD-0280-FU01 D7 — the v1 <see cref="ITaskSpentTimeSource"/>. Approved minutes come from
/// <c>time_entry_task_totals</c>, which only the approval finalizer writes (a recomputation from the in-force approved
/// revisions); submitted minutes are summed from revisions still waiting for a decision and are NEVER added to the approved
/// figure. The reader's own figures (running timer, draft minutes) are read for that reader only (D11).
/// </summary>
public sealed class TaskSpentTimeSource : ITaskSpentTimeSource
{
    private readonly ITaskTimeTotalRepository _totals;
    private readonly ITimeEntryRepository _entries;
    private readonly ITimesheetWeekRepository _weeks;
    private readonly ITimerSegmentRepository _segments;

    public TaskSpentTimeSource(
        ITaskTimeTotalRepository totals,
        ITimeEntryRepository entries,
        ITimesheetWeekRepository weeks,
        ITimerSegmentRepository segments)
    {
        _totals = totals;
        _entries = entries;
        _weeks = weeks;
        _segments = segments;
    }

    public async Task<IReadOnlyDictionary<Guid, int>> ApprovedMinutesAsync(
        IReadOnlyCollection<Guid> taskIds, CancellationToken ct = default)
    {
        if (taskIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        return (await _totals.ListByTaskIdsAsync(taskIds.Distinct().ToList(), ct))
            .Where(t => t.ApprovedMinutes > 0)
            .ToDictionary(t => t.TaskItemId, t => t.ApprovedMinutes);
    }

    public async Task<IReadOnlyDictionary<Guid, TaskSpentTime>> SpentTimeAsync(
        IReadOnlyCollection<Guid> taskIds, CancellationToken ct = default)
    {
        if (taskIds.Count == 0)
        {
            return new Dictionary<Guid, TaskSpentTime>();
        }

        var ids = taskIds.Distinct().ToList();
        var approved = await ApprovedMinutesAsync(ids, ct);

        var rows = await _entries.ListByTaskIdsAsync(ids, ct);
        var submittedWeeks = (await _weeks.ListByIdsAsync(rows.Select(r => r.TimesheetWeekId).Distinct().ToList(), ct))
            .Where(w => w.Status == TimesheetWeekStatus.Submitted)
            .Select(w => w.Id)
            .ToHashSet();
        var submitted = rows
            .Where(r => r.TaskItemId is not null && submittedWeeks.Contains(r.TimesheetWeekId))
            .GroupBy(r => r.TaskItemId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.DurationMinutes));

        return ids
            .Where(id => approved.ContainsKey(id) || submitted.ContainsKey(id))
            .ToDictionary(
                id => id,
                id => new TaskSpentTime(approved.GetValueOrDefault(id), submitted.GetValueOrDefault(id)));
    }

    public async Task<TaskReaderTime> ReaderTimeAsync(
        Guid readerUserId, IReadOnlyCollection<Guid> taskIds, CancellationToken ct = default)
    {
        if (taskIds.Count == 0)
        {
            return TaskReaderTime.None;
        }

        var ids = taskIds.Distinct().ToList();
        var running = await _segments.GetRunningAsync(readerUserId, ct);

        var mine = (await _entries.ListByTaskIdsAsync(ids, ct)).Where(r => r.UserId == readerUserId).ToList();
        var draftWeeks = (await _weeks.ListByIdsAsync(mine.Select(r => r.TimesheetWeekId).Distinct().ToList(), ct))
            .Where(w => w.Status == TimesheetWeekStatus.Draft && w.UserId == readerUserId)
            .Select(w => w.Id)
            .ToHashSet();
        var drafts = mine
            .Where(r => r.TaskItemId is not null && draftWeeks.Contains(r.TimesheetWeekId))
            .GroupBy(r => r.TaskItemId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.DurationMinutes));

        return new TaskReaderTime(
            running?.TaskItemId is { } runningTask && ids.Contains(runningTask) ? runningTask : null,
            drafts);
    }
}

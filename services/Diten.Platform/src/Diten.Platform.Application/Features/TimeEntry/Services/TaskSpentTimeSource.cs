using Diten.Platform.Application.Contracts;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Enums.TimeEntry;
using TimeEntryRow = Diten.Platform.Domain.Entities.TimeEntry.TimeEntry;
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
        var weeks = await _weeks.ListByIdsAsync(rows.Select(r => r.TimesheetWeekId).Distinct().ToList(), ct);
        var submitted = await ChangePerTaskAsync(
            rows, weeks.Where(w => w.Status == TimesheetWeekStatus.Submitted).ToList(), ids, ct);
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
            .ToList();
        var drafts = await ChangePerTaskAsync(mine, draftWeeks, ids, ct);

        return new TaskReaderTime(
            running?.TaskItemId is { } runningTask && ids.Contains(runningTask) ? runningTask : null,
            drafts);
    }

    /// <summary>
    /// Minutes per task in <paramref name="weeks"/> that are NOT already approved (v2 F7). A first revision counts all its
    /// minutes. A CORRECTION counts only its change against the revision in force for the same person and week — the
    /// approved figure is already in <see cref="ApprovedMinutesAsync"/>, and counting the corrected week whole would count
    /// it twice. The change can be negative (a correction that takes time away).
    /// </summary>
    private async Task<Dictionary<Guid, int>> ChangePerTaskAsync(
        IReadOnlyCollection<TimeEntryRow> rows, IReadOnlyCollection<TimesheetWeek> weeks, IReadOnlyCollection<Guid> taskIds,
        CancellationToken ct)
    {
        var result = new Dictionary<Guid, int>();
        foreach (var week in weeks)
        {
            var own = rows.Where(r => r.TimesheetWeekId == week.Id && r.TaskItemId is not null)
                .GroupBy(r => r.TaskItemId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.DurationMinutes));

            if (week.CorrectionOfRevision is not null)
            {
                var inForce = (await _weeks.ListRevisionsAsync(week.UserId, week.WeekKey, ct)).FirstOrDefault(r => r.InForce);
                if (inForce is not null)
                {
                    foreach (var approvedRow in (await _entries.ListByWeekAsync(inForce.Id, ct))
                                 .Where(r => r.TaskItemId is { } t && taskIds.Contains(t)))
                    {
                        own[approvedRow.TaskItemId!.Value] = own.GetValueOrDefault(approvedRow.TaskItemId!.Value) - approvedRow.DurationMinutes;
                    }
                }
            }

            foreach (var (task, minutes) in own)
            {
                result[task] = result.GetValueOrDefault(task) + minutes;
            }
        }

        return result.Where(pair => pair.Value != 0).ToDictionary(pair => pair.Key, pair => pair.Value);
    }
}

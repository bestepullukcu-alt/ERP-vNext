using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry.Queries;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Application.Features.WorkingHours;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.QueryHandlers;

/// <summary>
/// MOD-0280-FU01 T2a — the "+ Task row" picker. Two sources, one read rule:
/// <list type="number">
/// <item>open tasks (Open, Planned, InProgress, Waiting) the caller holds;</item>
/// <item>tasks the caller recorded time on in the current week and the <see cref="TimeEntryLimits.EditWindowPreviousWeeks"/>
/// weeks before it — offered only while the caller can still READ them.</item>
/// </list>
/// Both go through the task port's read rule, the same one a save asks (F5): a task the caller cannot read is never
/// listed, so the picker cannot be used to discover another team's work. The caller is always the server's user (D11).
/// </summary>
public sealed class GetTimeEntryTaskOptionsHandler
    : IRequestHandler<GetTimeEntryTaskOptionsQuery, Response<IReadOnlyList<TimeEntryTaskOptionDto>>>
{
    private readonly ITimeEntryTaskGateway _tasks;
    private readonly ITimesheetWeekRepository _weeks;
    private readonly ITimeEntryRepository _entries;
    private readonly IWorkingHoursProvider _workingHours;
    private readonly TimeProvider _clock;
    private readonly ICurrentUserContext _currentUser;

    public GetTimeEntryTaskOptionsHandler(
        ITimeEntryTaskGateway tasks,
        ITimesheetWeekRepository weeks,
        ITimeEntryRepository entries,
        IWorkingHoursProvider workingHours,
        TimeProvider clock,
        ICurrentUserContext currentUser)
    {
        _tasks = tasks;
        _weeks = weeks;
        _entries = entries;
        _workingHours = workingHours;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<Response<IReadOnlyList<TimeEntryTaskOptionDto>>> Handle(GetTimeEntryTaskOptionsQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userId = _currentUser.UserId;

        var own = await _tasks.OwnOpenTasksAsync(userId, ct);

        var recentIds = await RecentTaskIdsAsync(userId, ct);
        recentIds.ExceptWith(own.Select(t => t.TaskItemId));
        var recent = recentIds.Count == 0
            ? []
            : (await _tasks.ReadableTaskSummariesAsync(userId, recentIds, ct)).Values.ToList();

        var search = Normalise(request.Search);
        var options = own.OrderBy(t => t.Title, StringComparer.CurrentCultureIgnoreCase)
            .Concat(recent.OrderBy(t => t.Title, StringComparer.CurrentCultureIgnoreCase))
            .Where(t => search is null || t.Title.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            .Take(TimeEntryLimits.TaskOptionsMax)
            .Select(t => new TimeEntryTaskOptionDto(t.TaskItemId, t.Title, t.Status))
            .ToList();

        return Response<IReadOnlyList<TimeEntryTaskOptionDto>>.Success(options, correlationId: request.CorrelationId);
    }

    /// <summary>Task ids on any revision of the caller's weeks inside the edit window (tenant-local today).</summary>
    private async Task<HashSet<Guid>> RecentTaskIdsAsync(Guid userId, CancellationToken ct)
    {
        var now = _clock.GetUtcNow();
        var utcToday = DateOnly.FromDateTime(now.UtcDateTime);
        var hours = await _workingHours.GetWorkingWindowsAsync(userId, utcToday.AddDays(-1), utcToday.AddDays(1), ct);
        var thisMonday = WeekCalendar.MondayOf(WeekCalendar.LocalDateOf(now, hours.TimeZone));

        var ids = new HashSet<Guid>();
        for (var back = 0; back <= TimeEntryLimits.EditWindowPreviousWeeks; back++)
        {
            var weekKey = WeekCalendar.KeyOf(thisMonday.AddDays(-7 * back));
            foreach (var revision in await _weeks.ListRevisionsAsync(userId, weekKey, ct))
            {
                foreach (var entry in await _entries.ListByWeekAsync(revision.Id, ct))
                {
                    if (entry.TaskItemId is { } taskId)
                    {
                        ids.Add(taskId);
                    }
                }
            }
        }

        return ids;
    }

    private static string? Normalise(string? search)
    {
        var trimmed = search?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length > TimeEntryLimits.TaskOptionsSearchMaxLength
            ? trimmed[..TimeEntryLimits.TaskOptionsSearchMaxLength]
            : trimmed;
    }
}

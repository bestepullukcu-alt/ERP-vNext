using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry.Queries;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.QueryHandlers;

/// <summary>
/// MOD-0280-FU01 D9 — "fill from plan": ghost values from the caller's plan blocks. WRITES NOTHING — no reconcile, no
/// finalizer, no row; a plan is an intention, and letting it fill the sheet silently would make later estimates learn
/// from plans (Z-5). Ghost values appear only for (day, task) cells that have no timer segment, only for days up to the
/// caller's local today, rounded like the timer (A2). Accepting one is a save with <c>source: "Plan"</c>.
/// </summary>
public sealed class GetPlanFillInHandler : IRequestHandler<GetPlanFillInQuery, Response<PlanFillInDto>>
{
    private readonly ITimesheetWeekReader _reader;
    private readonly ITimeEntryTaskGateway _tasks;
    private readonly ITimerSegmentRepository _segments;
    private readonly ICurrentUserContext _currentUser;

    public GetPlanFillInHandler(
        ITimesheetWeekReader reader, ITimeEntryTaskGateway tasks, ITimerSegmentRepository segments, ICurrentUserContext currentUser)
    {
        _reader = reader;
        _tasks = tasks;
        _segments = segments;
        _currentUser = currentUser;
    }

    public async Task<Response<PlanFillInDto>> Handle(GetPlanFillInQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!WeekCalendar.TryParse(request.WeekKey, out var monday))
        {
            return Response<PlanFillInDto>.Fail(
                "Week key is not an ISO week (e.g. 2026-W40).", 400, TimeEntryReasonCodes.WeekKeyInvalid, request.CorrelationId);
        }

        var userId = _currentUser.UserId;
        var context = await _reader.LoadAsync(userId, monday, ct);
        var from = TimerRules.LocalMidnightAfter(context.Monday.AddDays(-1), context.Zone);
        var to = TimerRules.LocalMidnightAfter(context.Sunday, context.Zone);

        var timed = (await _segments.ListForWeekAsync(userId, context.WeekKey, ct))
            .Where(s => s.TaskItemId is not null)
            .Select(s => (s.LocalDate, TaskItemId: s.TaskItemId!.Value))
            .ToHashSet();

        var rows = (await _tasks.PlannedBlocksAsync(userId, from, to, ct))
            .Select(block => (block.TaskItemId, LocalDate: WeekCalendar.LocalDateOf(block.PlannedStartAt, context.Zone),
                Minutes: TimerRules.DraftMinutes(block.PlannedDurationMinutes * 60L)))
            .Where(x => x.LocalDate <= context.LocalToday && x.Minutes > 0 && !timed.Contains((x.LocalDate, x.TaskItemId)))
            .GroupBy(x => (x.LocalDate, x.TaskItemId))
            .Select(g => new PlanFillInRowDto(g.Key.LocalDate, g.Key.TaskItemId,
                Math.Min(TimeEntryLimits.MaxRowMinutes, g.Sum(x => x.Minutes))))
            .OrderBy(r => r.LocalDate)
            .ToList();

        // T2a — a ghost row for a task not yet on the sheet needs its name: one batched read, the person's read rule.
        if (rows.Count > 0)
        {
            var titles = await _tasks.ReadableTaskSummariesAsync(userId, rows.Select(r => r.TaskItemId).Distinct().ToList(), ct);
            rows = rows.Select(r => r with { TaskTitle = titles.TryGetValue(r.TaskItemId, out var task) ? task.Title : null }).ToList();
        }

        return Response<PlanFillInDto>.Success(
            new PlanFillInDto(context.WeekKey, context.LocalToday, rows), correlationId: request.CorrelationId);
    }
}

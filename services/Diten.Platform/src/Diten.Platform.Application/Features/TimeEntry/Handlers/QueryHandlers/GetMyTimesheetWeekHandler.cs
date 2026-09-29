using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry.Queries;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;
using MediatR;
using TimeEntryRow = Diten.Platform.Domain.Entities.TimeEntry.TimeEntry;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.QueryHandlers;

/// <summary>
/// MOD-0280-FU01 — the caller's OWN week, and only theirs: the user id is the server's, never the request's (D11).
///
/// <para><b>Reading writes nothing of its own (F4).</b> The one thing a read may cause is taking on board a decision
/// MOD-0023 has ALREADY made (the pull finalizer, D7, an audited command) — so the person never sees "Submitted" for a
/// week their manager decided. A week that was never written stays unwritten, inside the window or outside it; a time
/// admin reopens an old one by (person, week), which creates its Draft (<c>ReopenTimesheetWeek</c>).</para>
/// </summary>
public sealed class GetMyTimesheetWeekHandler : IRequestHandler<GetMyTimesheetWeekQuery, Response<TimesheetWeekDto>>
{
    private readonly ITimesheetWeekReader _reader;
    private readonly ITimeEntryRepository _entries;
    private readonly ITimesheetDecisionPuller _puller;
    private readonly ICurrentUserContext _currentUser;

    public GetMyTimesheetWeekHandler(
        ITimesheetWeekReader reader,
        ITimeEntryRepository entries,
        ITimesheetDecisionPuller puller,
        ICurrentUserContext currentUser)
    {
        _reader = reader;
        _entries = entries;
        _puller = puller;
        _currentUser = currentUser;
    }

    public async Task<Response<TimesheetWeekDto>> Handle(GetMyTimesheetWeekQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!WeekCalendar.TryParse(request.WeekKey, out var monday))
        {
            return Response<TimesheetWeekDto>.Fail(
                "Week key is not an ISO week (e.g. 2026-W40).", 400, TimeEntryReasonCodes.WeekKeyInvalid, request.CorrelationId);
        }

        var userId = _currentUser.UserId;
        var context = await _reader.LoadAsync(userId, monday, ct);

        if (await _puller.PullAsync(context.Revisions, request.CorrelationId, ct))
        {
            context = await _reader.LoadAsync(userId, monday, ct);
        }

        var current = context.Current;
        var rows = current is null ? Array.Empty<TimeEntryRow>() : (await _entries.ListByWeekAsync(current.Id, ct)).ToArray();
        var dayTotals = TimesheetRules.DayTotals(rows);
        var refusal = TimesheetRules.WriteRefusal(context);
        var inForce = context.InForce is { } approved && approved.Id != current?.Id ? approved : null;

        return Response<TimesheetWeekDto>.Success(new TimesheetWeekDto(
            WeekKey: context.WeekKey,
            WeekStartDate: context.Monday,
            TimeZoneId: context.Zone.Id,
            LocalToday: context.LocalToday,
            WeekId: current?.Id,
            RevisionNumber: current?.RevisionNumber,
            Status: (current?.Status ?? TimesheetWeekStatus.Draft).ToString(),
            Version: current?.Version ?? 0,
            Editable: refusal is null,
            NotEditableReason: refusal,
            InsideEditWindow: context.InsideEditWindow,
            ReopenActive: current?.ReopenActive ?? false,
            ReopenReason: current?.ReopenActive == true ? current.ReopenReason : null,
            CorrectionOfRevision: current?.CorrectionOfRevision,
            CorrectionReason: current?.CorrectionReason,
            InForce: inForce is null
                ? null
                : new InForceRevisionDto(inForce.Id, inForce.RevisionNumber, inForce.ApprovedAtUtc, inForce.TotalMinutes),
            SubmittedAtUtc: current?.SubmittedAtUtc,
            ApprovedAtUtc: current?.ApprovedAtUtc,
            LastRejectedAtUtc: current?.LastRejectedAtUtc,
            LastRejectionReason: current?.LastRejectionReason,
            FinalizationBlockedReason: current?.FinalizationBlockedReason,
            FlaggedDates: TimesheetRules.FlaggedDates(dayTotals),
            TotalMinutes: dayTotals.Values.Sum(),
            Days: TimesheetRules.Days(context.Days, context.Monday, dayTotals, context.LocalToday),
            Entries: rows.Select(TimesheetRules.ToDto).ToList()), correlationId: request.CorrelationId);
    }
}

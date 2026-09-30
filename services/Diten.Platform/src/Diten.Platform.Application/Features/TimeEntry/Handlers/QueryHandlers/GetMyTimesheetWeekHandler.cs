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
/// <para><b>Reading writes nothing of its own (F4).</b> The two things a read may cause are both audited system commands:
/// taking on board a decision MOD-0023 has ALREADY made (the pull finalizer, D7) — so the person never sees "Submitted"
/// for a week their manager decided — and closing the person's timer if its midnight passed or its task left them (D3,
/// §13), so the week never shows time a forgotten timer kept counting. A week that was never written stays unwritten; a
/// time admin reopens an old one by (person, week), which creates its Draft (<c>ReopenTimesheetWeek</c>).</para>
///
/// <para><b>T1b — what the timer and the meetings add.</b> Timer time too short to count (A2) and timer time that fell on
/// a locked week (§13) are reported, not written; meeting suggestions are derived (D8) — no suggestion row is written by
/// reading, the id is stable per (meeting, person).</para>
///
/// <para><b>T2a — what the screen names.</b> Task titles come from the task port in ONE batched read per week, under the
/// person's own read rule: a task they can no longer read has no title here. The approval history names the people on
/// the revision — who submitted, approved, last rejected, and the one person MOD-0023 assigned — through the same display
/// name resolver the approvals list uses.</para>
/// </summary>
public sealed class GetMyTimesheetWeekHandler : IRequestHandler<GetMyTimesheetWeekQuery, Response<TimesheetWeekDto>>
{
    private readonly ITimesheetWeekReader _reader;
    private readonly ITimeEntryRepository _entries;
    private readonly ITimesheetDecisionPuller _puller;
    private readonly ITimerReadModel _timer;
    private readonly ITimerSegmentRepository _segments;
    private readonly ITimeSuggestionReader _suggestions;
    private readonly ITimeEntryTaskGateway _tasks;
    private readonly IUserDisplayNameResolver _displayNames;
    private readonly ICurrentUserContext _currentUser;

    public GetMyTimesheetWeekHandler(
        ITimesheetWeekReader reader,
        ITimeEntryRepository entries,
        ITimesheetDecisionPuller puller,
        ITimerReadModel timer,
        ITimerSegmentRepository segments,
        ITimeSuggestionReader suggestions,
        ITimeEntryTaskGateway tasks,
        IUserDisplayNameResolver displayNames,
        ICurrentUserContext currentUser)
    {
        _reader = reader;
        _entries = entries;
        _puller = puller;
        _timer = timer;
        _segments = segments;
        _suggestions = suggestions;
        _tasks = tasks;
        _displayNames = displayNames;
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
        await _timer.ReconcileAsync(userId, request.CorrelationId, ct);
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

        var segments = (await _segments.ListForWeekAsync(userId, context.WeekKey, ct)).Where(x => !x.IsRunning).ToList();
        var suggestions = await _suggestions.ListForWeekAsync(context, ct);
        var conflictedRows = suggestions
            .Where(x => x.MinutesStatus == TimeSuggestionMinutesStatus.Conflict && x.Decision?.AcceptedEntryId is not null)
            .Select(x => x.Decision!.AcceptedEntryId!.Value)
            .ToHashSet();

        var tooShort = TimerWeekFacts.TooShort(segments);
        // v2 F3 — any week the timer cannot write to: submitted, approved, or outside the edit window.
        var outsideOpenWeek = refusal is not null ? TimerWeekFacts.OutsideOpenWeek(segments, rows) : [];
        // v2 F5 — computed, never written: where the draft does not (yet) say what the segments say.
        var draftPending = refusal is null ? TimerWeekFacts.DraftPending(segments, rows) : [];

        var taskIds = rows.Select(r => r.TaskItemId)
            .Concat(tooShort.Select(x => x.TaskItemId))
            .Concat(outsideOpenWeek.Select(x => x.TaskItemId))
            .Concat(draftPending.Select(x => x.TaskItemId))
            .OfType<Guid>()
            .Distinct()
            .ToList();
        var titles = taskIds.Count == 0
            ? new Dictionary<Guid, TimeEntryTaskSummary>()
            : await _tasks.ReadableTaskSummariesAsync(userId, taskIds, ct);
        string? TitleOf(Guid? taskId) => taskId is { } id && titles.TryGetValue(id, out var task) ? task.Title : null;

        var people = new[] { current?.SubmittedByUserId, current?.ApprovedByUserId, current?.LastRejectedByUserId, current?.AssignedApproverUserId }
            .OfType<Guid>()
            .Distinct()
            .ToList();
        var names = people.Count == 0 ? new Dictionary<Guid, string>() : await _displayNames.ResolveAsync(people, ct);
        string? NameOf(Guid? user) => user is { } id && names.TryGetValue(id, out var name) && !string.IsNullOrWhiteSpace(name) ? name : null;

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
            Entries: rows.Select(row => TimesheetRules.ToDto(row) with
            {
                OutsideWorkingMinutes = row.OutsideWorkingMinutes,
                EditedFromTimer = row.EditedFromTimer,
                MinutesConflict = conflictedRows.Contains(row.Id),
                SourceRef = row.SourceRef,
                TaskTitle = TitleOf(row.TaskItemId)
            }).ToList(),
            TooShortToCount: tooShort.Select(x => x with { TaskTitle = TitleOf(x.TaskItemId) }).ToList(),
            TimerOutsideOpenWeek: outsideOpenWeek.Select(x => x with { TaskTitle = TitleOf(x.TaskItemId) }).ToList(),
            TimerDraftPending: draftPending.Select(x => x with { TaskTitle = TitleOf(x.TaskItemId) }).ToList(),
            Suggestions: suggestions.Where(x => x.Offered).Select(x => x.ToDto()).ToList(),
            SubmittedByUserId: current?.SubmittedByUserId,
            SubmittedByDisplayName: NameOf(current?.SubmittedByUserId),
            ApprovedByUserId: current?.ApprovedByUserId,
            ApprovedByDisplayName: NameOf(current?.ApprovedByUserId),
            LastRejectedByUserId: current?.LastRejectedByUserId,
            LastRejectedByDisplayName: NameOf(current?.LastRejectedByUserId),
            AssignedApproverUserId: current?.AssignedApproverUserId,
            AssignedApproverDisplayName: NameOf(current?.AssignedApproverUserId),
            // T2b (E4) — the caller's OWN closed segments whose instants are still there (an approved week's were
            // minimised, D4, and are not listed). `segments` is already the caller's own, read by the server's user id.
            TimerSegments: segments
                .Where(s => s.MinimisedAtUtc is null && s.StartedAtUtc is not null)
                .OrderBy(s => s.StartedAtUtc)
                .Select(s => new TimerSegmentBreakdownDto(
                    s.Id, s.LocalDate, s.TaskItemId, s.CategoryCode, s.StartedAtUtc!.Value, s.StoppedAtUtc, s.DurationSeconds,
                    s.OutsideWorkingMinutes, s.StartSource.ToString(), s.StopReason?.ToString()))
                .ToList()), correlationId: request.CorrelationId);
    }
}

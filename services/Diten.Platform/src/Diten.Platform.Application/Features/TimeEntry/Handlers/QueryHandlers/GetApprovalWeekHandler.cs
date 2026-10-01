using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry.Queries;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.QueryHandlers;

/// <summary>
/// MOD-0280-FU01 D6 / D11 — one submitted week, read-only, for the ONE approver MOD-0023 assigned it to (F3: the other
/// candidates can neither see nor decide the approval in MOD-0023, so they do not see the week here either).
///
/// <para><b>404 for everything else, with no difference between the cases:</b> a draft (never shown to an approver),
/// a week routed to someone else, another tenant's id, an id that does not exist. An approver can tell none of them
/// apart, which is the point (§13 "Unauthorized").</para>
/// </summary>
public sealed class GetApprovalWeekHandler : IRequestHandler<GetApprovalWeekQuery, Response<ApprovalWeekDto>>
{
    private readonly ITimesheetWeekRepository _weeks;
    private readonly ITimeEntryRepository _entries;
    private readonly ITimesheetWeekReader _reader;
    private readonly ITimesheetDecisionPuller _puller;
    private readonly IUserDisplayNameResolver _displayNames;
    private readonly IApprovalWeekFacts _facts;
    private readonly ITimeEntryTaskGateway _tasks;
    private readonly ICurrentUserContext _currentUser;

    public GetApprovalWeekHandler(
        ITimesheetWeekRepository weeks,
        ITimeEntryRepository entries,
        ITimesheetWeekReader reader,
        ITimesheetDecisionPuller puller,
        IUserDisplayNameResolver displayNames,
        IApprovalWeekFacts facts,
        ITimeEntryTaskGateway tasks,
        ICurrentUserContext currentUser)
    {
        _weeks = weeks;
        _entries = entries;
        _reader = reader;
        _puller = puller;
        _displayNames = displayNames;
        _facts = facts;
        _tasks = tasks;
        _currentUser = currentUser;
    }

    public async Task<Response<ApprovalWeekDto>> Handle(GetApprovalWeekQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var week = await _weeks.GetByIdAsync(request.WeekId, ct);
        if (!IsRoutedToCaller(week))
        {
            return NotFound(request);
        }

        if (await _puller.PullAsync([week!], request.CorrelationId, ct))
        {
            week = await _weeks.GetByIdAsync(request.WeekId, ct);
            if (!IsRoutedToCaller(week))
            {
                return NotFound(request);
            }
        }

        var rows = await _entries.ListByWeekAsync(week!.Id, ct);
        var dayTotals = TimesheetRules.DayTotals(rows);
        var context = await _reader.LoadAsync(week.UserId, week.WeekStartDate, ct);
        var names = await _displayNames.ResolveAsync([week.UserId], ct);
        var marks = await _facts.MarksAsync(week, rows, context.Days, ct);

        // U5 — a correction shows what it changes against the approved revision still in force.
        var inForce = week.CorrectionOfRevision is not null ? context.InForce : null;
        var changes = inForce is null
            ? []
            : ApprovalWeekFacts.Changes(await _entries.ListByWeekAsync(inForce.Id, ct), rows);

        // Task titles as the APPROVER may read them (their own read rule, one batched read); null ⇒ neutral label.
        var taskIds = rows.Select(r => r.TaskItemId).Concat(changes.Select(c => c.TaskItemId)).OfType<Guid>().Distinct().ToList();
        var titles = taskIds.Count == 0
            ? new Dictionary<Guid, TimeEntryTaskSummary>()
            : await _tasks.ReadableTaskSummariesAsync(_currentUser.UserId, taskIds, ct);
        string? TitleOf(Guid? id) => id is { } t && titles.TryGetValue(t, out var s) ? s.Title : null;

        return Response<ApprovalWeekDto>.Success(new ApprovalWeekDto(
            week.Id,
            week.UserId,
            names.TryGetValue(week.UserId, out var name) && !string.IsNullOrWhiteSpace(name) ? name : null,
            week.WeekKey,
            week.WeekStartDate,
            week.RevisionNumber,
            week.CorrectionOfRevision,
            week.CorrectionReason,
            week.Status.ToString(),
            week.TotalMinutes,
            week.FlaggedDates,
            week.SubmittedAtUtc,
            week.WorkflowInstanceId,
            TimesheetRules.Days(context.Days, week.WeekStartDate, dayTotals, context.LocalToday),
            rows.Select(r => TimesheetRules.ToDto(r) with
            {
                OutsideWorkingMinutes = r.OutsideWorkingMinutes,
                EditedFromTimer = r.EditedFromTimer,
                SourceRef = r.SourceRef,
                TaskTitle = TitleOf(r.TaskItemId)
            }).ToList(),
            ApprovalTaskId: marks.ApprovalTaskId,
            ApprovalTaskVersion: marks.ApprovalTaskVersion,
            AutoClosedDates: marks.AutoClosedDates,
            OutsideWorkingMinutes: marks.OutsideWorkingMinutes,
            HolidayDates: marks.HolidayDates,
            InForceRevisionNumber: inForce?.RevisionNumber,
            CorrectionChanges: changes.Select(c => c with { TaskTitle = TitleOf(c.TaskItemId) }).ToList()),
            correlationId: request.CorrelationId);
    }

    private bool IsRoutedToCaller(TimesheetWeek? week)
        => week is { Status: TimesheetWeekStatus.Submitted }
           && week.UserId != _currentUser.UserId
           && week.AssignedApproverUserId == _currentUser.UserId;

    private static Response<ApprovalWeekDto> NotFound(GetApprovalWeekQuery request)
        => Response<ApprovalWeekDto>.Fail("Not found.", 404, TimeEntryReasonCodes.ApprovalWeekNotFound, request.CorrelationId);
}

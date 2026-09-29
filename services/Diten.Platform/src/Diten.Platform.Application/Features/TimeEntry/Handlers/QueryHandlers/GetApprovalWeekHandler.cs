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
    private readonly ICurrentUserContext _currentUser;

    public GetApprovalWeekHandler(
        ITimesheetWeekRepository weeks,
        ITimeEntryRepository entries,
        ITimesheetWeekReader reader,
        ITimesheetDecisionPuller puller,
        IUserDisplayNameResolver displayNames,
        ICurrentUserContext currentUser)
    {
        _weeks = weeks;
        _entries = entries;
        _reader = reader;
        _puller = puller;
        _displayNames = displayNames;
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
            rows.Select(TimesheetRules.ToDto).ToList()), correlationId: request.CorrelationId);
    }

    private bool IsRoutedToCaller(TimesheetWeek? week)
        => week is { Status: TimesheetWeekStatus.Submitted }
           && week.UserId != _currentUser.UserId
           && week.AssignedApproverUserId == _currentUser.UserId;

    private static Response<ApprovalWeekDto> NotFound(GetApprovalWeekQuery request)
        => Response<ApprovalWeekDto>.Fail("Not found.", 404, TimeEntryReasonCodes.ApprovalWeekNotFound, request.CorrelationId);
}

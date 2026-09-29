using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry.Queries;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.QueryHandlers;

/// <summary>
/// MOD-0280-FU01 D11 — the weeks waiting for THIS approver: submitted, and assigned to them by MOD-0023 (F3 — one
/// assignee, not the whole candidate list). A draft is never listed (it is not Submitted), and neither is a week
/// assigned to somebody else. Decisions already made in MOD-0023 are
/// taken on board first, so a week the approver just decided in the Task Center drops off the list (D7).
/// </summary>
public sealed class GetApprovalListHandler : IRequestHandler<GetApprovalListQuery, Response<ApprovalWeekListDto>>
{
    private readonly ITimesheetWeekRepository _weeks;
    private readonly ITimesheetDecisionPuller _puller;
    private readonly IUserDisplayNameResolver _displayNames;
    private readonly ICurrentUserContext _currentUser;

    public GetApprovalListHandler(
        ITimesheetWeekRepository weeks,
        ITimesheetDecisionPuller puller,
        IUserDisplayNameResolver displayNames,
        ICurrentUserContext currentUser)
    {
        _weeks = weeks;
        _puller = puller;
        _displayNames = displayNames;
        _currentUser = currentUser;
    }

    public async Task<Response<ApprovalWeekListDto>> Handle(GetApprovalListQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var approverId = _currentUser.UserId;
        var weeks = await _weeks.ListSubmittedForApproverAsync(approverId, ct);
        if (await _puller.PullAsync(weeks, request.CorrelationId, ct))
        {
            weeks = await _weeks.ListSubmittedForApproverAsync(approverId, ct);
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 25 : request.PageSize, 1, TimeEntryLimits.ApprovalsMaxPageSize);
        var slice = weeks.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var names = await _displayNames.ResolveAsync(slice.Select(w => w.UserId).Distinct().ToList(), ct);

        var items = slice.Select(w => new ApprovalWeekListItemDto(
            w.Id,
            w.UserId,
            names.TryGetValue(w.UserId, out var name) && !string.IsNullOrWhiteSpace(name) ? name : null,
            w.WeekKey,
            w.RevisionNumber,
            w.CorrectionOfRevision is not null,
            w.TotalMinutes,
            w.FlaggedDates,
            w.SubmittedAtUtc,
            w.ApproverResolution?.ToString())).ToList();

        return Response<ApprovalWeekListDto>.Success(
            new ApprovalWeekListDto(items, weeks.Count, page, pageSize), correlationId: request.CorrelationId);
    }
}

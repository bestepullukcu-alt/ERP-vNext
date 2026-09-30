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
    private readonly IApprovalWeekFacts _facts;
    private readonly ITimeEntryRepository _entries;
    private readonly ICurrentUserContext _currentUser;

    public GetApprovalListHandler(
        ITimesheetWeekRepository weeks,
        ITimesheetDecisionPuller puller,
        IUserDisplayNameResolver displayNames,
        IApprovalWeekFacts facts,
        ITimeEntryRepository entries,
        ICurrentUserContext currentUser)
    {
        _weeks = weeks;
        _puller = puller;
        _displayNames = displayNames;
        _facts = facts;
        _entries = entries;
        _currentUser = currentUser;
    }

    /// <summary>The columns the approvals list may be ordered by (Golden Reference server mode: a whitelist).</summary>
    private static readonly HashSet<string> OrderKeys = new(StringComparer.Ordinal)
    {
        "submittedAtUtc", "displayName", "weekKey", "totalMinutes"
    };

    public async Task<Response<ApprovalWeekListDto>> Handle(GetApprovalListQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var approverId = _currentUser.UserId;
        var weeks = await _weeks.ListSubmittedForApproverAsync(approverId, ct);
        if (await _puller.PullAsync(weeks, request.CorrelationId, ct))
        {
            weeks = await _weeks.ListSubmittedForApproverAsync(approverId, ct);
        }

        // T2b — the server-mode protocol: a whitelisted order, a length within bounds; anything else is refused.
        var orderKey = string.IsNullOrWhiteSpace(request.OrderBy) ? "submittedAtUtc" : request.OrderBy.Trim();
        if (!OrderKeys.Contains(orderKey)
            || request.Length is < 1 or > TimeEntryLimits.ApprovalsMaxServerLength
            || request.Start is < 0)
        {
            return Response<ApprovalWeekListDto>.Fail(
                "The approvals query is not valid (orderBy, length or start).", 400,
                TimeEntryReasonCodes.ApprovalsQueryInvalid, request.CorrelationId);
        }

        // Names for the whole queue (one batched read): the search matches them, and the order may sort by them.
        var names = await _displayNames.ResolveAsync(weeks.Select(w => w.UserId).Distinct().ToList(), ct);
        string? NameOf(Guid userId) => names.TryGetValue(userId, out var n) && !string.IsNullOrWhiteSpace(n) ? n : null;

        var search = request.Search?.Trim();
        var filtered = string.IsNullOrEmpty(search)
            ? weeks.ToList()
            : weeks.Where(w => (NameOf(w.UserId) ?? string.Empty).Contains(search, StringComparison.CurrentCultureIgnoreCase)
                               || w.WeekKey.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

        var descending = string.Equals(request.OrderDir, "desc", StringComparison.OrdinalIgnoreCase);
        IOrderedEnumerable<Domain.Entities.TimeEntry.TimesheetWeek> ordered = orderKey switch
        {
            "displayName" => descending ? filtered.OrderByDescending(w => NameOf(w.UserId)) : filtered.OrderBy(w => NameOf(w.UserId)),
            "weekKey" => descending ? filtered.OrderByDescending(w => w.WeekKey) : filtered.OrderBy(w => w.WeekKey),
            "totalMinutes" => descending ? filtered.OrderByDescending(w => w.TotalMinutes) : filtered.OrderBy(w => w.TotalMinutes),
            _ => descending ? filtered.OrderByDescending(w => w.SubmittedAtUtcTicks) : filtered.OrderBy(w => w.SubmittedAtUtcTicks)
        };
        ordered = ordered.ThenBy(w => w.Id); // a stable page: the order always ends with the id

        int page, pageSize, skip;
        if (request.Start is { } start && request.Length is { } length)
        {
            skip = start;
            pageSize = length;
            page = start / length + 1;
        }
        else
        {
            page = Math.Max(1, request.Page);
            pageSize = Math.Clamp(request.PageSize <= 0 ? 25 : request.PageSize, 1, TimeEntryLimits.ApprovalsMaxPageSize);
            skip = (page - 1) * pageSize;
        }

        var slice = ordered.Skip(skip).Take(pageSize).ToList();

        // T2b — the marks and the Task Center work-item id, for the page's rows only (one set of reads per row; the
        // approver's page is at most ApprovalsMaxPageSize rows — a batched read is a T4 backlog item).
        var marks = new Dictionary<Guid, ApprovalWeekMarks>();
        foreach (var w in slice)
        {
            marks[w.Id] = await _facts.MarksAsync(w, await _entries.ListByWeekAsync(w.Id, ct), null, ct);
        }

        var items = slice.Select(w => new ApprovalWeekListItemDto(
            w.Id,
            w.UserId,
            NameOf(w.UserId),
            w.WeekKey,
            w.RevisionNumber,
            w.CorrectionOfRevision is not null,
            w.TotalMinutes,
            w.FlaggedDates,
            w.SubmittedAtUtc,
            w.ApproverResolution?.ToString(),
            ApprovalTaskId: marks[w.Id].ApprovalTaskId,
            ApprovalTaskVersion: marks[w.Id].ApprovalTaskVersion,
            AutoClosedDates: marks[w.Id].AutoClosedDates,
            OutsideWorkingMinutes: marks[w.Id].OutsideWorkingMinutes,
            HolidayDates: marks[w.Id].HolidayDates)).ToList();

        return Response<ApprovalWeekListDto>.Success(
            new ApprovalWeekListDto(items, weeks.Count, page, pageSize, filtered.Count), correlationId: request.CorrelationId);
    }
}

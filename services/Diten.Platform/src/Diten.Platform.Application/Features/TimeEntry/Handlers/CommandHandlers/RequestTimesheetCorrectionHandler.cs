using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using MediatR;
using TimeEntryRow = Diten.Platform.Domain.Entities.TimeEntry.TimeEntry;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.CommandHandlers;

/// <summary>
/// MOD-0280-FU01 D5 — a correction of an approved week is a NEW revision, with a mandatory reason, starting from the
/// approved figures. The approved revision is not touched: it stays approved and IN FORCE until the correction itself is
/// approved (the finalizer then supersedes it). One open correction per week — the open-revision unique index is the
/// guarantee, the check below only gives the loser a better answer.
/// </summary>
public sealed class RequestTimesheetCorrectionHandler
    : IRequestHandler<RequestTimesheetCorrectionCommand, Response<TimesheetWeekMutationDto>>
{
    private readonly ITimesheetWeekReader _reader;
    private readonly ITimesheetWeekRepository _weeks;
    private readonly ITimeEntryRepository _entries;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;

    public RequestTimesheetCorrectionHandler(
        ITimesheetWeekReader reader,
        ITimesheetWeekRepository weeks,
        ITimeEntryRepository entries,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext)
    {
        _reader = reader;
        _weeks = weeks;
        _entries = entries;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
    }

    public async Task<Response<TimesheetWeekMutationDto>> Handle(RequestTimesheetCorrectionCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!WeekCalendar.TryParse(request.WeekKey, out var monday))
        {
            return Fail("Week key is not an ISO week.", 400, TimeEntryReasonCodes.WeekKeyInvalid, request);
        }

        var userId = _currentUser.UserId;
        var context = await _reader.LoadAsync(userId, monday, ct);

        if (context.Open is not null)
        {
            return Fail("A correction or draft of this week is already open.", 409, TimeEntryReasonCodes.CorrectionAlreadyOpen, request);
        }

        if (context.InForce is not { } approved)
        {
            return Fail("Only an approved week can be corrected.", 409, TimeEntryReasonCodes.CorrectionNotAllowed, request);
        }

        var correction = TimesheetRules.NewRevision(
            context, _tenantContext.TenantId, context.Revisions.Max(r => r.RevisionNumber) + 1);
        correction.CorrectionOfRevision = approved.RevisionNumber;
        correction.CorrectionReason = request.Request.Reason!.Trim();
        correction.TotalMinutes = approved.TotalMinutes;
        correction.FlaggedDates = approved.FlaggedDates.ToList();

        if (!await _weeks.TryCreateAsync(correction, ct))
        {
            return Fail("A correction of this week is already open.", 409, TimeEntryReasonCodes.CorrectionAlreadyOpen, request);
        }

        // The correction starts from what was approved; the approved rows themselves are never touched.
        foreach (var row in await _entries.ListByWeekAsync(approved.Id, ct))
        {
            await _entries.CreateAsync(new TimeEntryRow
            {
                TenantId = _tenantContext.TenantId,
                TimesheetWeekId = correction.Id,
                UserId = userId,
                WeekKey = correction.WeekKey,
                LocalDate = row.LocalDate,
                DurationMinutes = row.DurationMinutes,
                TaskItemId = row.TaskItemId,
                CategoryCode = row.CategoryCode,
                Source = row.Source,
                SourceRef = row.SourceRef,
                EditedFromTimer = row.EditedFromTimer,
                // CT acceptance (T1b v3): the correction copy keeps what G1/G2 need — without these the approver of the
                // correction saw no captured value and late timer time was not added to a corrected row.
                CapturedMinutes = row.CapturedMinutes,
                CorrectedMinutes = row.CorrectedMinutes,
                CorrectionBaselineSeconds = row.CorrectionBaselineSeconds,
                OutsideWorkingMinutes = row.OutsideWorkingMinutes,
                Note = row.Note,
                CreatedBy = userId.ToString()
            }, ct);
        }

        return Response<TimesheetWeekMutationDto>.Success(
            TimesheetRules.ToMutation(correction), 201, request.CorrelationId);
    }

    private static Response<TimesheetWeekMutationDto> Fail(string message, int status, string code, RequestTimesheetCorrectionCommand request)
        => Response<TimesheetWeekMutationDto>.Fail(message, status, code, request.CorrelationId);
}

using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.CommandHandlers;

/// <summary>
/// MOD-0280-FU01 A9 / F4 — a time admin (<c>time-entry.weeks.reopen</c>) reopens ONE PERSON'S week that is older than
/// the edit window, with a reason. Addressed by (person, week), not by a row id: a week that was never written has no
/// row — reading it creates none — so the reopen creates the revision-1 Draft itself, audited like every other change.
///
/// <list type="bullet">
/// <item>A time admin cannot reopen their OWN week (403): the reopen is a control over somebody else's record.</item>
/// <item>The person must hold a seat in this tenant (404 otherwise, with no difference between "unknown" and "other
/// tenant").</item>
/// <item>A week inside the window, or one that is not an open Draft, or is already reopened, needs no reopen (409).</item>
/// <item>The reopen lasts until the week is APPROVED (F7), so a reopened week that is rejected can still be fixed.</item>
/// </list>
/// </summary>
public sealed class ReopenTimesheetWeekHandler : IRequestHandler<ReopenTimesheetWeekCommand, Response<TimesheetWeekMutationDto>>
{
    private readonly ITimesheetWeekRepository _weeks;
    private readonly ITimesheetWeekReader _reader;
    private readonly ITimeEntryOrgGateway _org;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _clock;

    public ReopenTimesheetWeekHandler(
        ITimesheetWeekRepository weeks,
        ITimesheetWeekReader reader,
        ITimeEntryOrgGateway org,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext,
        TimeProvider clock)
    {
        _weeks = weeks;
        _reader = reader;
        _org = org;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<Response<TimesheetWeekMutationDto>> Handle(ReopenTimesheetWeekCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var admin = _currentUser.UserId;
        var person = request.Request.UserId;
        if (person == admin)
        {
            return Fail("A time admin cannot reopen their own week.", 403, TimeEntryReasonCodes.ReopenOwnWeek, request);
        }

        if (!WeekCalendar.TryParse(request.Request.WeekKey, out var monday))
        {
            return Fail("Week key is not an ISO week.", 400, TimeEntryReasonCodes.WeekKeyInvalid, request);
        }

        if (await _org.PrimarySeatAsync(person, ct) is null)
        {
            return Fail("Person not found.", 404, TimeEntryReasonCodes.PersonNotFound, request);
        }

        var context = await _reader.LoadAsync(person, monday, ct);
        if (context.InsideEditWindow)
        {
            return Fail("This week does not need a reopen.", 409, TimeEntryReasonCodes.ReopenNotNeeded, request);
        }

        var now = _clock.GetUtcNow();
        var reason = request.Request.Reason!.Trim();

        if (context.Revisions.Count == 0)
        {
            if (request.Request.ExpectedVersion != 0)
            {
                return Fail("The week changed meanwhile; reload and retry.", 409, TimeEntryReasonCodes.ConcurrencyConflict, request);
            }

            var created = TimesheetRules.NewRevision(context, _tenantContext.TenantId, 1);
            created.ReopenActive = true;
            created.ReopenedAtUtc = now;
            created.ReopenedByUserId = admin;
            created.ReopenReason = reason;
            return await _weeks.TryCreateAsync(created, ct)
                ? Response<TimesheetWeekMutationDto>.Success(TimesheetRules.ToMutation(created), correlationId: request.CorrelationId)
                : Fail("The week changed meanwhile; reload and retry.", 409, TimeEntryReasonCodes.ConcurrencyConflict, request);
        }

        var week = context.Open;
        if (week is null || week.Status != TimesheetWeekStatus.Draft || week.ReopenActive)
        {
            return Fail("This week does not need a reopen.", 409, TimeEntryReasonCodes.ReopenNotNeeded, request);
        }

        if (request.Request.ExpectedVersion != week.Version)
        {
            return Fail("The week changed meanwhile; reload and retry.", 409, TimeEntryReasonCodes.ConcurrencyConflict, request);
        }

        week.ReopenActive = true;
        week.ReopenedAtUtc = now;
        week.ReopenedByUserId = admin;
        week.ReopenReason = reason;
        week.UpdatedBy = admin.ToString();
        if (!await _weeks.UpdateAsync(week, request.Request.ExpectedVersion, ct))
        {
            return Fail("The week changed meanwhile; reload and retry.", 409, TimeEntryReasonCodes.ConcurrencyConflict, request);
        }

        return Response<TimesheetWeekMutationDto>.Success(TimesheetRules.ToMutation(week), correlationId: request.CorrelationId);
    }

    private static Response<TimesheetWeekMutationDto> Fail(string message, int status, string code, ReopenTimesheetWeekCommand request)
        => Response<TimesheetWeekMutationDto>.Fail(message, status, code, request.CorrelationId);
}

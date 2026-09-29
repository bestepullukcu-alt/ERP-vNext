using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.CommandHandlers;

/// <summary>
/// MOD-0280-FU01 D5 / D6 — submit the caller's open draft.
///
/// <para>Refused while it is empty (A6), while a day is implausible (A3), and while nobody can approve it: the
/// approver candidates are resolved HERE, stored on the week, and handed to MOD-0023 — there is no automatic
/// approval and no local decision (D6).</para>
///
/// <para><b>Retry-safe, and never an inherited decision (F1).</b> The MOD-0023 start is keyed by week + revision +
/// submission number, and the number is written to the week BEFORE the start. A submit that dies between the start and
/// the week's own write leaves an instance nobody recorded: the next submit takes a new number (so a new instance),
/// cancels the stray one first, and adopts only an instance MOD-0023 reports as Active.</para>
///
/// <para>MOD-0023 assigns ONE approver (its first candidate); that person is stored and is the only one the approvals
/// page shows the week to (F3).</para>
/// </summary>
public sealed class SubmitTimesheetWeekHandler : IRequestHandler<SubmitTimesheetWeekCommand, Response<TimesheetWeekMutationDto>>
{
    private readonly ITimesheetWeekReader _reader;
    private readonly ITimesheetWeekRepository _weeks;
    private readonly ITimeEntryRepository _entries;
    private readonly IApproverResolver _approvers;
    private readonly ITimesheetApprovalService _approvals;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _clock;
    private readonly ITimesheetSubmissionProbe _probe;

    public SubmitTimesheetWeekHandler(
        ITimesheetWeekReader reader,
        ITimesheetWeekRepository weeks,
        ITimeEntryRepository entries,
        IApproverResolver approvers,
        ITimesheetApprovalService approvals,
        ICurrentUserContext currentUser,
        TimeProvider clock,
        ITimesheetSubmissionProbe probe)
    {
        _reader = reader;
        _weeks = weeks;
        _entries = entries;
        _approvers = approvers;
        _approvals = approvals;
        _currentUser = currentUser;
        _clock = clock;
        _probe = probe;
    }

    public async Task<Response<TimesheetWeekMutationDto>> Handle(SubmitTimesheetWeekCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!WeekCalendar.TryParse(request.WeekKey, out var monday))
        {
            return Fail("Week key is not an ISO week.", 400, TimeEntryReasonCodes.WeekKeyInvalid, request);
        }

        var userId = _currentUser.UserId;
        var context = await _reader.LoadAsync(userId, monday, ct);

        if (context.Revisions.Count == 0)
        {
            return Fail("An empty week cannot be submitted.", 409, TimeEntryReasonCodes.EmptyWeek, request);
        }

        var refusal = TimesheetRules.WriteRefusal(context);
        var week = context.Open;
        if (refusal is not null || week is null)
        {
            return Fail("This week is not open for submission.", 409, refusal ?? TimeEntryReasonCodes.WeekNotOpen, request);
        }

        if (request.Request.ExpectedVersion != week.Version)
        {
            return Fail("The week changed meanwhile; reload and retry.", 409, TimeEntryReasonCodes.ConcurrencyConflict, request);
        }

        var rows = await _entries.ListByWeekAsync(week.Id, ct);
        if (rows.Count == 0)
        {
            return Fail("An empty week cannot be submitted.", 409, TimeEntryReasonCodes.EmptyWeek, request);
        }

        var dayTotals = TimesheetRules.DayTotals(rows);
        if (dayTotals.Values.Any(total => total > TimeEntryLimits.ImplausibleDayMinutes))
        {
            return Fail("A day above 16 hours must be corrected before submitting.", 409, TimeEntryReasonCodes.DayImplausible, request);
        }

        var approvers = await _approvers.ResolveAsync(userId, ct);
        if (approvers is null)
        {
            return Fail("Nobody can approve this week.", 409, TimeEntryReasonCodes.NoApprover, request);
        }

        // F1, step 1 — CLAIM the submission number before anything is started. The MOD-0023 key carries it, so if this
        // submit dies after the start, the retry uses the NEXT number: it can never be handed back an instance that was
        // started (and perhaps decided) for different content.
        var submissionNumber = week.SubmissionCount + 1;
        week.SubmissionCount = submissionNumber;
        week.UpdatedBy = userId.ToString();
        if (!await _weeks.UpdateAsync(week, request.Request.ExpectedVersion, ct))
        {
            return Fail("The week changed meanwhile; reload and retry.", 409, TimeEntryReasonCodes.ConcurrencyConflict, request);
        }

        // Step 2 — an earlier attempt that started its instance and then died left it open in the approver's inbox.
        await _approvals.RetireSubmissionAsync(week, submissionNumber - 1, userId, ct);

        // Step 3 — start. Only an OPEN instance is adopted; a closed one would carry an old decision onto this content.
        var started = await _approvals.StartAsync(week, approvers.CandidateUserIds, submissionNumber, ct);
        if (started.WorkflowInstanceId is not { } instanceId)
        {
            return Fail("The approval could not be started.", 409,
                started.ReasonCode == TimeEntryReasonCodes.ApprovalInstanceClosed
                    ? TimeEntryReasonCodes.ApprovalInstanceClosed
                    : TimeEntryReasonCodes.ApprovalStartFailed,
                request);
        }

        await _probe.AfterApprovalStartedAsync(week.Id, ct);

        // Step 4 — the week becomes Submitted on THIS instance.
        var now = _clock.GetUtcNow();
        week.Status = TimesheetWeekStatus.Submitted;
        week.WorkflowInstanceId = instanceId;
        week.ApproverCandidateUserIds = approvers.CandidateUserIds.ToList();
        week.ApproverResolution = approvers.Resolution;
        week.AssignedApproverUserId = started.AssignedApproverUserId;
        week.LegalEntityId = approvers.LegalEntityId;
        week.SubmittedAtUtc = now;
        week.SubmittedAtUtcTicks = now.UtcTicks;
        week.SubmittedByUserId = userId;
        week.TotalMinutes = dayTotals.Values.Sum();
        week.FlaggedDates = TimesheetRules.FlaggedDates(dayTotals);
        // ReopenActive is NOT cleared here: a reopen lasts until the week is approved (F7).
        week.FinalizationBlockedReason = null;

        if (!await _weeks.UpdateAsync(week, week.Version, ct))
        {
            // The instance is open and unrecorded; the next submit retires it (step 2).
            return Fail("The week changed meanwhile; reload and retry.", 409, TimeEntryReasonCodes.ConcurrencyConflict, request);
        }

        return Response<TimesheetWeekMutationDto>.Success(TimesheetRules.ToMutation(week), correlationId: request.CorrelationId);
    }

    private static Response<TimesheetWeekMutationDto> Fail(string message, int status, string code, SubmitTimesheetWeekCommand request)
        => Response<TimesheetWeekMutationDto>.Fail(message, status, code, request.CorrelationId);
}

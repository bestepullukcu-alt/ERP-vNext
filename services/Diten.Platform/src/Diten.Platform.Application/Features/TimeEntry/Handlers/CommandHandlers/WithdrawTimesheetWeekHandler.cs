using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.CommandHandlers;

/// <summary>
/// MOD-0280-FU01 A6 — the person takes their submitted week back, until the approver's decision is RECORDED in
/// MOD-0023. "Recorded" is MOD-0023's state, not this module's: an approval that is decided but not yet finalized here
/// is still too late, and the decision is taken on board instead (§13).
/// </summary>
public sealed class WithdrawTimesheetWeekHandler : IRequestHandler<WithdrawTimesheetWeekCommand, Response<TimesheetWeekMutationDto>>
{
    private readonly ITimesheetWeekReader _reader;
    private readonly ITimesheetWeekRepository _weeks;
    private readonly ITimesheetApprovalService _approvals;
    private readonly ITimesheetDecisionPuller _puller;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _clock;
    private readonly ITimesheetSubmissionProbe _probe;
    private readonly ITimeEntryNotifier _notifier;

    public WithdrawTimesheetWeekHandler(
        ITimesheetWeekReader reader,
        ITimesheetWeekRepository weeks,
        ITimesheetApprovalService approvals,
        ITimesheetDecisionPuller puller,
        ICurrentUserContext currentUser,
        TimeProvider clock,
        ITimesheetSubmissionProbe probe,
        ITimeEntryNotifier notifier)
    {
        _notifier = notifier;
        _reader = reader;
        _weeks = weeks;
        _approvals = approvals;
        _puller = puller;
        _currentUser = currentUser;
        _clock = clock;
        _probe = probe;
    }

    public async Task<Response<TimesheetWeekMutationDto>> Handle(WithdrawTimesheetWeekCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!WeekCalendar.TryParse(request.WeekKey, out var monday))
        {
            return Fail("Week key is not an ISO week.", 400, TimeEntryReasonCodes.WeekKeyInvalid, request);
        }

        var userId = _currentUser.UserId;
        var context = await _reader.LoadAsync(userId, monday, ct);
        var week = context.Open;

        if (week is null || week.Status != TimesheetWeekStatus.Submitted || week.WorkflowInstanceId is not { } instanceId)
        {
            // Already approved (no open revision, one in force) → the decision came first. Otherwise nothing is pending.
            var code = week is null && context.InForce is not null
                ? TimeEntryReasonCodes.WithdrawTooLate
                : TimeEntryReasonCodes.WeekNotOpen;
            return Fail("There is no submission to withdraw.", 409, code, request);
        }

        if (request.Request.ExpectedVersion != week.Version)
        {
            return Fail("The week changed meanwhile; reload and retry.", 409, TimeEntryReasonCodes.ConcurrencyConflict, request);
        }

        // BL-483 — "too late" means somebody DECIDED. An instance that was cancelled (or is gone) was decided by nobody:
        // there is nothing left to wait for, CancelAsync answers true for it, and the withdrawal goes through.
        if (IsDecided(await _approvals.ReadDecisionAsync(instanceId, ct)))
        {
            return await TooLateAsync(week, request, ct);
        }

        await _probe.BeforeWithdrawCancelAsync(week.Id, ct);

        if (!await _approvals.CancelAsync(instanceId, userId, ct))
        {
            // MOD-0023 did not cancel. WHY decides the answer, and only MOD-0023's own state says why:
            // the decision landed between our read and our cancel (F13) → too late, take the decision on board;
            // otherwise nobody decided — MOD-0023 lost a concurrent write, or cancelled only part of what was open
            // (BL-483) → a conflict the person can simply retry. THIS handler writes nothing to the week. (After a
            // partial cancel MOD-0023 has already closed the instance as Cancelled, so the next read's pull returns
            // the week to Draft through the finalizer — the withdrawal the person asked for, without its own stamp
            // or e-mail; a retry then finds the week already open.)
            if (IsDecided(await _approvals.ReadDecisionAsync(instanceId, ct)))
            {
                return await TooLateAsync(week, request, ct);
            }

            return Fail("The approval changed meanwhile and nobody has decided; reload and retry.", 409,
                TimeEntryReasonCodes.ConcurrencyConflict, request);
        }

        week.Status = TimesheetWeekStatus.Draft;
        week.WorkflowInstanceId = null;
        week.WithdrawnAtUtc = _clock.GetUtcNow();
        week.UpdatedBy = userId.ToString();
        if (!await _weeks.UpdateAsync(week, request.Request.ExpectedVersion, ct))
        {
            // The instance is cancelled; the next read returns the week to Draft through the finalizer.
            return Fail("The week changed meanwhile; reload and retry.", 409, TimeEntryReasonCodes.ConcurrencyConflict, request);
        }

        // T3 (N4) — the same candidates the submission went to. Best-effort: never fails the withdraw.
        // L4 — not the request's token: the withdrawal is written (see the submit handler).
        await _notifier.WeekWithdrawnAsync(week, CancellationToken.None);

        return Response<TimesheetWeekMutationDto>.Success(TimesheetRules.ToMutation(week), correlationId: request.CorrelationId);
    }

    /// <summary>Only an approval or a rejection is a decision. Pending, cancelled and missing are not.</summary>
    private static bool IsDecided(TimesheetDecision decision)
        => decision.Outcome is TimesheetDecisionOutcome.Approved or TimesheetDecisionOutcome.Rejected;

    private async Task<Response<TimesheetWeekMutationDto>> TooLateAsync(
        Domain.Entities.TimeEntry.TimesheetWeek week, WithdrawTimesheetWeekCommand request, CancellationToken ct)
    {
        await _puller.PullAsync([week], request.CorrelationId, ct);
        return Fail("The approver has already decided.", 409, TimeEntryReasonCodes.WithdrawTooLate, request);
    }

    private static Response<TimesheetWeekMutationDto> Fail(string message, int status, string code, WithdrawTimesheetWeekCommand request)
        => Response<TimesheetWeekMutationDto>.Fail(message, status, code, request.CorrelationId);
}

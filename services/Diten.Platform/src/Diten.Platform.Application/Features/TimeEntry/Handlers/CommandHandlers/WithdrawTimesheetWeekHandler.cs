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

    public WithdrawTimesheetWeekHandler(
        ITimesheetWeekReader reader,
        ITimesheetWeekRepository weeks,
        ITimesheetApprovalService approvals,
        ITimesheetDecisionPuller puller,
        ICurrentUserContext currentUser,
        TimeProvider clock,
        ITimesheetSubmissionProbe probe)
    {
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

        var decision = await _approvals.ReadDecisionAsync(instanceId, ct);
        if (decision.Outcome != TimesheetDecisionOutcome.Pending)
        {
            await _puller.PullAsync([week], request.CorrelationId, ct);
            return Fail("The approver has already decided.", 409, TimeEntryReasonCodes.WithdrawTooLate, request);
        }

        await _probe.BeforeWithdrawCancelAsync(week.Id, ct);

        if (!await _approvals.CancelAsync(instanceId, userId, ct))
        {
            // The decision landed between our read and our cancel (F13): too late — take the decision on board.
            await _puller.PullAsync([week], request.CorrelationId, ct);
            return Fail("The approver has already decided.", 409, TimeEntryReasonCodes.WithdrawTooLate, request);
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

        return Response<TimesheetWeekMutationDto>.Success(TimesheetRules.ToMutation(week), correlationId: request.CorrelationId);
    }

    private static Response<TimesheetWeekMutationDto> Fail(string message, int status, string code, WithdrawTimesheetWeekCommand request)
        => Response<TimesheetWeekMutationDto>.Fail(message, status, code, request.CorrelationId);
}

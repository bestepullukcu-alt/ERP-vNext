using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.CommandHandlers;

/// <summary>
/// MOD-0280-FU01 D2 / §19.1 (system) — one MOD-0024 transition, applied to the timers.
///
/// <para><b>Stop is state-based, not kind-based.</b> After ANY observed write, a running segment on this task stops when
/// the task is no longer InProgress or no longer held by the segment's person — Waiting, SubmittedForReview, Completed,
/// Cancelled, Released, Returned, Reassigned, and an undeclared (<c>Unknown</c>) write alike.</para>
///
/// <para><b>Start</b> only on <c>Started</c>/<c>Resumed</c> performed BY the holder (another person's act never starts my
/// timer — D11), with the timer on for the holder's legal entity (D12). Keyed by the transition id: the same transition
/// twice starts one segment.</para>
/// </summary>
public sealed class ApplyTaskTransitionToTimerHandler : IRequestHandler<ApplyTaskTransitionToTimerCommand, Response<string>>
{
    private readonly ITimerService _timer;
    private readonly ITimerSegmentRepository _segments;
    private readonly TimeProvider _clock;

    public ApplyTaskTransitionToTimerHandler(ITimerService timer, ITimerSegmentRepository segments, TimeProvider clock)
    {
        _timer = timer;
        _segments = segments;
        _clock = clock;
    }

    public async Task<Response<string>> Handle(ApplyTaskTransitionToTimerCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var observation = request.Observation;
        var stopped = 0;

        foreach (var segment in await _segments.ListRunningByTaskAsync(observation.TaskItemId, ct))
        {
            if (TaskTransitionTimerRules.EndsRunFor(observation, segment.UserId)
                && await _timer.CloseAsync(segment, _clock.GetUtcNow(), TimerStopReason.TaskTransition, observation.TransitionId, ct))
            {
                stopped++;
            }
        }

        var started = false;
        if (TaskTransitionTimerRules.StartsRun(observation)
            && observation.CurrentHolderUserId is { } holder
            && await _timer.IsEnabledForAsync(holder, ct))
        {
            var outcome = await _timer.StartAsync(
                holder,
                new TimerTarget(observation.TaskItemId, null),
                TaskTransitionTimerRules.StartSourceOf(observation),
                observation.TransitionId,
                ct);
            started = outcome.Changed;
        }

        return Response<string>.Success($"stopped:{stopped};started:{started}", correlationId: request.CorrelationId);
    }
}

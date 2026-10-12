using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.CommandHandlers;

/// <summary>
/// MOD-0280-FU01 §19.1 — undo a switch inside its 60-second window: the segment the switch started stops, and the target
/// it switched away from starts again FROM NOW (no backdating). The previous target must still be startable: a task that
/// meanwhile left the caller or left InProgress is refused like any other start.
/// </summary>
public sealed class UndoTimerSwitchHandler : IRequestHandler<UndoTimerSwitchCommand, Response<TimerMutationDto>>
{
    private readonly ITimerService _timer;
    private readonly ITimerReadModel _read;
    private readonly ITimerSegmentRepository _segments;
    private readonly ITimeEntryTaskGateway _tasks;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _clock;

    public UndoTimerSwitchHandler(
        ITimerService timer, ITimerReadModel read, ITimerSegmentRepository segments, ITimeEntryTaskGateway tasks,
        ICurrentUserContext currentUser, TimeProvider clock)
    {
        _timer = timer;
        _read = read;
        _segments = segments;
        _tasks = tasks;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Response<TimerMutationDto>> Handle(UndoTimerSwitchCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userId = _currentUser.UserId;

        var running = await _segments.GetRunningAsync(userId, ct);
        if (running is null || running.SwitchToken != request.Request.SwitchToken || running.SwitchedFromSegmentId is not { } previousId
            || running.StartedAtUtc is not { } startedAt || _clock.GetUtcNow() > startedAt + TimerRules.UndoWindow)
        {
            return Fail("This switch can no longer be undone.", 409, TimeEntryReasonCodes.TimerUndoExpired, request);
        }

        var previous = await _segments.GetByIdAsync(previousId, ct);
        if (previous is null)
        {
            return Fail("This switch can no longer be undone.", 409, TimeEntryReasonCodes.TimerUndoExpired, request);
        }

        if (!await _timer.IsEnabledForAsync(userId, ct))
        {
            return Fail("The timer is not switched on for your legal entity.", 409,
                TimeEntryReasonCodes.TimerDisabledForLegalEntity, request);
        }

        if (previous.TaskItemId is { } taskId)
        {
            var facts = await _tasks.TaskFactsAsync([taskId], ct);
            if (!facts.TryGetValue(taskId, out var task) || task.HolderUserId != userId)
            {
                return Fail("You do not hold this task any more.", 409, TimeEntryReasonCodes.TimerTaskNotHeld, request);
            }

            if (!task.IsInProgress)
            {
                return Fail("The task is no longer in progress.", 409, TimeEntryReasonCodes.TimerTaskNotInProgress, request);
            }
        }

        // StartAsync closes the running (switched-to) segment as a switch, then starts the previous target from now.
        var outcome = await _timer.StartAsync(
            userId, new TimerTarget(previous.TaskItemId, previous.CategoryCode), TimerStartSource.UndoSwitch, null, ct);
        if (outcome.ReasonCode is not null)
        {
            return Fail("Another start for you landed at the same moment; reload and retry.", 409, outcome.ReasonCode, request);
        }

        return Response<TimerMutationDto>.Success(
            new TimerMutationDto(await _read.ReadAsync(userId, ct), outcome.StoppedSegmentId), correlationId: request.CorrelationId);
    }

    private static Response<TimerMutationDto> Fail(string message, int status, string code, UndoTimerSwitchCommand request)
        => Response<TimerMutationDto>.Fail(message, status, code, request.CorrelationId);
}

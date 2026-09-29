using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.CommandHandlers;

/// <summary>MOD-0280-FU01 D2 — stop the caller's running timer. The task stays exactly as it is.</summary>
public sealed class StopTimerHandler : IRequestHandler<StopTimerCommand, Response<TimerMutationDto>>
{
    private readonly ITimerService _timer;
    private readonly ITimerReadModel _read;
    private readonly ITimerSegmentRepository _segments;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _clock;

    public StopTimerHandler(
        ITimerService timer, ITimerReadModel read, ITimerSegmentRepository segments, ICurrentUserContext currentUser,
        TimeProvider clock)
    {
        _timer = timer;
        _read = read;
        _segments = segments;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Response<TimerMutationDto>> Handle(StopTimerCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userId = _currentUser.UserId;

        var running = await _segments.GetRunningAsync(userId, ct);
        if (running is null || !await _timer.CloseAsync(running, _clock.GetUtcNow(), TimerStopReason.TimerControl, null, ct))
        {
            return Response<TimerMutationDto>.Fail(
                "No timer is running.", 409, TimeEntryReasonCodes.TimerNotRunning, request.CorrelationId);
        }

        return Response<TimerMutationDto>.Success(
            new TimerMutationDto(await _read.ReadAsync(userId, ct), running.Id), correlationId: request.CorrelationId);
    }
}

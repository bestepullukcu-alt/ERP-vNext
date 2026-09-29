using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.CommandHandlers;

/// <summary>
/// MOD-0280-FU01 D3 (system) — a timer left running is cut at the person's tenant-local midnight: the segment ends AT
/// 00:00 local (never later, whenever the close actually runs), its draft is written, and nothing continues into the next
/// day. Idempotent: the close is conditional, so the job and a read racing each other close it once.
/// </summary>
public sealed class CloseTimersAtLocalMidnightHandler : IRequestHandler<CloseTimersAtLocalMidnightCommand, Response<int>>
{
    private readonly ITimerService _timer;
    private readonly ITimerSegmentRepository _segments;
    private readonly ITimerAutoCloseNotifier _notifier;
    private readonly TimeProvider _clock;

    public CloseTimersAtLocalMidnightHandler(
        ITimerService timer, ITimerSegmentRepository segments, ITimerAutoCloseNotifier notifier, TimeProvider clock)
    {
        _timer = timer;
        _segments = segments;
        _notifier = notifier;
        _clock = clock;
    }

    public async Task<Response<int>> Handle(CloseTimersAtLocalMidnightCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = _clock.GetUtcNow();
        var running = await _segments.GetRunningAsync(request.UserId, ct);
        if (running is null || !TimerRules.PastLocalMidnight(running.LocalDate, TimerService.Zone(running), now))
        {
            return Response<int>.Success(0, correlationId: request.CorrelationId);
        }

        // StopPoint cuts at the midnight whatever "now" is — a close that runs at 03:00 still ends the segment at 00:00.
        if (!await _timer.CloseAsync(running, now, TimerStopReason.LocalMidnight, null, ct))
        {
            return Response<int>.Success(0, correlationId: request.CorrelationId);
        }

        if (request.Notify && await _segments.TryClaimAutoCloseNotificationAsync(running.Id, now, ct))
        {
            await _notifier.NotifyAsync((await _segments.GetByIdAsync(running.Id, ct)) ?? running, ct);
        }

        return Response<int>.Success(1, correlationId: request.CorrelationId);
    }
}

using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Domain.Enums.TimeEntry;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.CommandHandlers;

/// <summary>
/// MOD-0280-FU01 §13 (system) — the read-time safety net behind the hook. A running segment whose task is no longer
/// InProgress, no longer the person's, or gone is closed at the invalidating transition (<c>Reconcile</c>); one whose
/// legal entity has the timer off is closed at the switch time (<c>SwitchedOff</c>); one past its midnight at midnight.
/// </summary>
public sealed class ReconcileOrphanTimerHandler : IRequestHandler<ReconcileOrphanTimerCommand, Response<int>>
{
    private readonly ITimerService _timer;

    public ReconcileOrphanTimerHandler(ITimerService timer) => _timer = timer;

    public async Task<Response<int>> Handle(ReconcileOrphanTimerCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var pending = await _timer.PendingCloseAsync(request.UserId, ct);
        if (pending.Segment is null)
        {
            return Response<int>.Success(0, correlationId: request.CorrelationId);
        }

        var reason = pending.Kind switch
        {
            TimerPendingClose.LocalMidnight => TimerStopReason.LocalMidnight,
            TimerPendingClose.SwitchedOff => TimerStopReason.SwitchedOff,
            _ => TimerStopReason.Reconcile
        };
        var closed = await _timer.CloseAsync(pending.Segment, pending.StopAtUtc, reason, null, ct);
        return Response<int>.Success(closed ? 1 : 0, correlationId: request.CorrelationId);
    }
}

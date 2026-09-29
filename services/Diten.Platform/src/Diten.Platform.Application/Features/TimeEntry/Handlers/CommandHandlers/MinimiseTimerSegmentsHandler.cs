using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.CommandHandlers;

/// <summary>
/// MOD-0280-FU01 D4 / R8 (system) — the approved week's segments lose their start/stop instants; the rows, durations,
/// days, targets and outside-hours minutes stay. An audited UPDATE — this module deletes nothing (AGENTS.md §6).
/// Replay-safe: only segments still carrying their instants are touched.
/// </summary>
public sealed class MinimiseTimerSegmentsHandler : IRequestHandler<MinimiseTimerSegmentsCommand, Response<long>>
{
    private readonly ITimerSegmentRepository _segments;
    private readonly TimeProvider _clock;

    public MinimiseTimerSegmentsHandler(ITimerSegmentRepository segments, TimeProvider clock)
    {
        _segments = segments;
        _clock = clock;
    }

    public async Task<Response<long>> Handle(MinimiseTimerSegmentsCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var changed = await _segments.MinimiseWeekAsync(request.UserId, request.WeekKey, _clock.GetUtcNow(), ct);
        return Response<long>.Success(changed, correlationId: request.CorrelationId);
    }
}

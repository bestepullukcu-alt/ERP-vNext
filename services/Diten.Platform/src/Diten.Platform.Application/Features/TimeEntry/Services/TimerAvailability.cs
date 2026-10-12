namespace Diten.Platform.Application.Features.TimeEntry.Services;

/// <summary>MOD-0280-FU01 T2b — <see cref="ITimeEntryTimerAvailability"/> over the timer's own switch rule (D12): the
/// reader's primary seat → legal entity → switch row; no row, no seat or no legal entity = off.</summary>
public sealed class TimerAvailability : ITimeEntryTimerAvailability
{
    private readonly ITimerService _timer;

    public TimerAvailability(ITimerService timer) => _timer = timer;

    public Task<bool> IsTimerEnabledForAsync(Guid userId, CancellationToken ct = default)
        => _timer.IsEnabledForAsync(userId, ct);
}

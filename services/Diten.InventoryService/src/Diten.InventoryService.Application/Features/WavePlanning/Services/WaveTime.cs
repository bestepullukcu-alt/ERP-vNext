namespace Diten.InventoryService.Application.Features.WavePlanning.Services;

/// <summary>
/// MOD-0180's OWN clock (the MOD-0181 <c>CycleCountTime</c> pattern): production wraps <see cref="TimeProvider.System"/>; a test may move only
/// this clock (the cutoff's "at most one hour in the past" rule, pack §8.4) without moving MOD-0178's.
/// </summary>
public sealed class WaveTime(TimeProvider provider)
{
    public DateTimeOffset Now() => provider.GetUtcNow();
}

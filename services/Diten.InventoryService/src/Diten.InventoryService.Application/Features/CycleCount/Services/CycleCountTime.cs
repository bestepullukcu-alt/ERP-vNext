namespace Diten.InventoryService.Application.Features.CycleCounting.Services;

/// <summary>
/// MOD-0181's OWN clock (pack §4.1 <c>SnapshotAt</c>, r5 SB1/F-3): production wraps <see cref="TimeProvider.System"/>; a test may move only
/// this clock (AC-52 (b)) while the ledger keeps its own <c>DateTimeOffset.UtcNow</c>. Kept apart from the service-wide <see cref="TimeProvider"/>
/// registration so moving it does not move MOD-0178's flight locks.
/// </summary>
public sealed class CycleCountTime(TimeProvider provider)
{
    public DateTimeOffset Now() => provider.GetUtcNow();
}

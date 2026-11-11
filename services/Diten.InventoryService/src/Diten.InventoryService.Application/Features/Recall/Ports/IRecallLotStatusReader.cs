namespace Diten.InventoryService.Application.Features.Recall.Ports;

/// <summary>
/// MOD-0177 R3 (CT-R12) — "which of these lots are under an active recall?", read in-process and TENANT-wide (CT-R7: a recalled lot is recalled
/// in every company of the tenant) — the same answer as RECALL-BUNDLE <c>getLotRecallStatus</c>. MOD-0176's FEFO candidate read uses it to
/// exclude recalled lots with reason <c>RECALLED</c> (FEFO 1.3.0). The ledger registers <see cref="NoRecallLotStatusReader"/>; MOD-0177 replaces it.
/// It decides nothing: the exit block itself is <see cref="IRecallPostingGate"/>.
/// </summary>
public interface IRecallLotStatusReader
{
    Task<IReadOnlySet<string>> UnderRecallAsync(IReadOnlyCollection<string> lotIds, CancellationToken cancellationToken = default);
}

/// <summary>The default without a recall module: no lot is under recall.</summary>
public sealed class NoRecallLotStatusReader : IRecallLotStatusReader
{
    public Task<IReadOnlySet<string>> UnderRecallAsync(IReadOnlyCollection<string> lotIds, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.Ordinal));
}

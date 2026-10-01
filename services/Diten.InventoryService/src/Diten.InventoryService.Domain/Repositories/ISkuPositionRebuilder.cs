namespace Diten.InventoryService.Domain.Repositories;

/// <summary>
/// Rebuilds <c>inventory_sku_positions</c> from the ledger projection + reservations (pack ASSUMPTION-0173-13):
/// idempotent, never deletes data. Each SKU is recomputed in its own transaction so a concurrent posting cannot be lost.
/// </summary>
public interface ISkuPositionRebuilder
{
    /// <summary>Returns the number of SKU positions recomputed.</summary>
    Task<int> RebuildAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Groups the last <see cref="RebuildAllAsync"/> skipped because their scope (tenant, LE, item, sku, level) was
    /// incomplete — never guessed, never deleted (FU-S2-01: the startup gate reports it).
    /// </summary>
    int LastSkippedUnscoped { get; }
}

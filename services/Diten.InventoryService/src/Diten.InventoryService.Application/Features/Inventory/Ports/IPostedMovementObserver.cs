using Diten.InventoryService.Domain.Entities;

namespace Diten.InventoryService.Application.Features.Inventory.Ports;

/// <summary>
/// MOD-0173 seam for MOD-0175 (pack MOD-0175 §5 Q1, additive): told about every transaction the ledger has just COMMITTED
/// — never a replay, never a refusal. It runs post-commit, outside the posting transaction: an observer cannot refuse or
/// roll back a posting, and its failure is logged, not surfaced (the MOD-0175 intake sweep covers a missed call).
/// </summary>
public interface IPostedMovementObserver
{
    Task OnPostedAsync(InventoryTransaction transaction, CancellationToken cancellationToken = default);
}

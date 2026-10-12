using Diten.InventoryService.Domain.Entities;

namespace Diten.InventoryService.Domain.Repositories;

public sealed record BalancePage(IReadOnlyList<InventoryBalance> Rows, long Total);

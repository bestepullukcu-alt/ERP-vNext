using Diten.InventoryService.Domain.Entities;

namespace Diten.InventoryService.Domain.Repositories;

public sealed record TransactionPage(IReadOnlyList<InventoryTransaction> Items, long Total);

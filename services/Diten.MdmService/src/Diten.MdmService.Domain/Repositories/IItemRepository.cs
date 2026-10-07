using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Repositories;

// MOD-0290-FU04 S1: the item store (tenant-bounded; IsDeleted ≠ true everywhere, a legacy document without the field
// included).
public interface IItemRepository
{
    Task<Item?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Item?> GetByCreationCommandIdAsync(string creationCommandId, CancellationToken cancellationToken = default);
    Task<Item?> GetByReservationIdAsync(Guid reservationId, CancellationToken cancellationToken = default);
    Task<bool> NameKeyTakenAsync(string nameKey, Guid? exceptItemId, CancellationToken cancellationToken = default);
    Task<ItemPage> GetPageAsync(ItemPageRequest request, CancellationToken cancellationToken = default);

    // The row goes in only while its code is live: the reservation write and the insert are one transaction.
    Task<ItemWriteResult> CreateDraftAsync(Item item, CancellationToken cancellationToken = default);

    // Compare-and-set on Version; the row's Version moves by one.
    Task<ItemWriteResult> ReplaceAsync(Item item, int expectedVersion, CancellationToken cancellationToken = default);
}

public sealed record ItemWriteResult(bool Succeeded, Item? Item, string? ErrorCode = null, bool WriteOutcomeAmbiguous = false);

public sealed record ItemPageRequest(
    int Page, int PageSize, string? Query, ItemMaterialType? MaterialType, ItemLifecycleStatus? Status);

public sealed record ItemPage(IReadOnlyList<Item> Items, long Total);

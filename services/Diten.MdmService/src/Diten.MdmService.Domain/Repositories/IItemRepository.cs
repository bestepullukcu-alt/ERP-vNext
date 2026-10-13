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

    // FU04 S2 (S1 carry-over b, BL-576): compare-and-set on Version, field by field ($set of the fields this binary
    // knows; a field a later binary wrote stays). Refused while an activation submit holds the item's fence
    // (ITEM_ACTIVATION_SUBMIT_IN_FLIGHT). The row's Version moves by one.
    Task<ItemWriteResult> UpdateFieldsAsync(Item item, int expectedVersion, CancellationToken cancellationToken = default);

    // FU04 S2 (§8.4 A, the LSKU submit fence): the item at its expected version and state takes this operation's fence
    // (or already holds it) — atomically, so no edit slips between the check and the start. The item's version does not
    // move. False: the item moved, or another operation holds it.
    Task<bool> FenceActivationSubmitAsync(Guid itemId, int expectedVersion, ItemLifecycleStatus sourceStatus,
        Guid operationId, CancellationToken cancellationToken = default);
}

public sealed record ItemWriteResult(bool Succeeded, Item? Item, string? ErrorCode = null, bool WriteOutcomeAmbiguous = false);

public sealed record ItemPageRequest(
    int Page, int PageSize, string? Query, ItemMaterialType? MaterialType, ItemLifecycleStatus? Status);

public sealed record ItemPage(IReadOnlyList<Item> Items, long Total);

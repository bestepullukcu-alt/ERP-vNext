using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Traceability.Entities;

namespace Diten.InventoryService.Application.Features.Traceability.Ports;

/// <summary>
/// The ledger's LOT / SERIAL source (MOD-0174 pack §8 "Ledger link", WP-0174-P2) — in-process, implemented by
/// <c>InventoryLotDirectory</c> on the MOD-0174 collections of the same database. No HTTP: a lot check that is a network
/// call would put a second system in the posting's consistency boundary (the FU02 <c>InventoryLocationDirectory</c>
/// pattern). Every member is tenant-scoped by the request.
/// <para>
/// The members run twice per posting: as the pre-check (error order of pack §12) and again INSIDE the posting transaction,
/// where the ledger repository binds its session — there the lot is re-read and its <c>StockTouchSeq</c> written (a
/// concurrent status change conflicts), the serials are re-validated on the committed state and written.
/// </para>
/// </summary>
public interface ILotMasterClient
{
    /// <summary>
    /// The registered lot a ledger <c>lotId</c> names (canonical lot id of this tenant), or null — a typed lot text or an
    /// id the tenant does not have. Inside a posting transaction it also increments the lot's <c>StockTouchSeq</c>.
    /// </summary>
    Task<Lot?> ResolveLotAsync(string lotId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pack §12 serial rules for one posting, in order: <c>SERIAL_ALREADY_EXISTS</c> (inbound), <c>SERIAL_NOT_AVAILABLE</c>
    /// (outbound / transfer: unknown, Consumed, another company or not at the source location), <c>SERIAL_LOT_MISMATCH</c>.
    /// </summary>
    Task<SerialCheck> ValidateSerialsAsync(SerialMovement movement, CancellationToken cancellationToken = default);

    /// <summary>Inbound (posting transaction only): new serials → Active; Consumed ones → Active again (a return / a reversal).</summary>
    Task RegisterSerialsAsync(SerialMovement movement, CancellationToken cancellationToken = default);

    /// <summary>Outbound (posting transaction only): Active at the source location → Consumed.</summary>
    Task ConsumeSerialsAsync(SerialMovement movement, CancellationToken cancellationToken = default);

    /// <summary>Transfer (posting transaction only): Active at the source location → Active at the target location.</summary>
    Task MoveSerialsAsync(SerialMovement movement, CancellationToken cancellationToken = default);

    /// <summary>CT-D1 (a): which of these ledger lot ids are registered lots whose status holds their stock (Blocked / Expired).</summary>
    Task<IReadOnlySet<string>> HeldLotIdsAsync(IReadOnlyCollection<string> lotIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// The serials of one posting. Direction comes from the sides: only a target = inbound, only a source = outbound, both =
/// transfer (location, warehouse or stock-status transfer). <see cref="LotId"/> = the registered lot of the movement (null for
/// a typed lot or none). <see cref="TransactionId"/> / <see cref="At"/> are set for the writes.
/// </summary>
public sealed record SerialMovement(
    Guid ItemId,
    Guid SkuId,
    SkuLevel SkuLevel,
    Guid? LotId,
    IReadOnlyList<string> SerialNumbers,
    string? FromWarehouseId,
    string? FromLocationId,
    string? ToWarehouseId,
    string? ToLocationId,
    bool AcceptUnregistered,
    bool RegisterNew = true,
    Guid? TransactionId = null,
    DateTimeOffset? At = null)
{
    public bool IsInbound => FromLocationId is null && ToLocationId is not null;

    public bool IsOutbound => FromLocationId is not null && ToLocationId is null;
}

/// <summary>Result of <see cref="ILotMasterClient.ValidateSerialsAsync"/>: valid, or one pack §12 code with the serials it names.</summary>
public sealed record SerialCheck(string? Code, string? Message, IReadOnlyList<string> Serials)
{
    public static readonly SerialCheck Valid = new(null, null, []);

    public bool IsValid => Code is null;
}

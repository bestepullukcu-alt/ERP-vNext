using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Traceability.Enums;

namespace Diten.InventoryService.Domain.Traceability.Entities;

/// <summary>
/// MOD-0174 lot (collection <c>trace_lots</c>, pack §4). Identity is tenant-wide (CT-D3): <see cref="EntityBase.LegalEntityId"/>
/// is the company that CREATED the lot, kept for audit — it does not limit who may read or use the lot. The business key
/// <see cref="IdentityKey"/> is built ONLY by <see cref="Rules.LotIdentityPolicy"/> (K-G). <see cref="LotNumber"/>,
/// <see cref="SkuId"/>, <see cref="SkuLevel"/> and <see cref="ItemId"/> never change after creation. A lot is never
/// deleted: <see cref="EntityBase.IsDeleted"/> exists for the entity-base standard and stays false.
/// </summary>
public sealed class Lot : EntityBase
{
    public Guid ItemId { get; set; }

    public Guid SkuId { get; set; }

    public SkuLevel SkuLevel { get; set; }

    public string LotNumber { get; set; } = string.Empty;

    public string IdentityKey { get; set; } = string.Empty;

    public string? ManufacturerLot { get; set; }

    public string? SupplierLot { get; set; }

    /// <summary>UTC midnight of the date (date precision, pack §4).</summary>
    public DateTimeOffset? ManufacturingDate { get; set; }

    public DateTimeOffset? ExpiryDate { get; set; }

    public DateTimeOffset? RetestDate { get; set; }

    /// <summary>ISO 3166-1 alpha-2.</summary>
    public string? CountryOfOrigin { get; set; }

    public string? CoaReference { get; set; }

    public LotStatus Status { get; set; } = LotStatus.Draft;

    public LotTrackingMode TrackingMode { get; set; }

    public string CreationSource { get; set; } = string.Empty;

    public Guid CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public string? CreateIdempotencyKey { get; set; }

    public string? CreateRequestFingerprint { get; set; }

    /// <summary>
    /// WP-0174-P2 (pack §8 "Ledger link"): incremented inside every ledger posting that names this lot. A status change
    /// writes the same document in its own transaction, so a posting racing a block gets a WriteConflict and re-runs on
    /// the committed status (the FU02 <c>StockTouchSeq</c> technique) — stock never moves in a lot that was blocked meanwhile.
    /// </summary>
    public long StockTouchSeq { get; set; }
}

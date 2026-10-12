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

    /// <summary>
    /// MOD-0175 Q4 (KQ-H, CT-Q16): set by the retest sweeper in the SAME transaction that took the lot's AVAILABLE quantity out
    /// of the SKU position; cleared (and the quantity given back) in one transaction when the retest date moves on. The ONE
    /// stored fact every "is this lot's stock held?" question reads (<see cref="Rules.LotPostingRules.HoldsStock(Lot)"/>) —
    /// never recomputed from the date.
    /// </summary>
    public DateTimeOffset? RetestHeldAtUtc { get; set; }

    /// <summary>MOD-0176 F2 (TRACE 1.4.0 <c>LotRef.suggestedExpiryDate</c>): manufacturing date + getSku <c>shelfLifeDays</c> at creation; null = no suggestion.</summary>
    public DateTimeOffset? SuggestedExpiryDate { get; set; }

    /// <summary>MOD-0176 F2 (TRACE 1.4.0 <c>LotRef.suggestedRetestDate</c>): manufacturing date + getSku <c>retestDays</c>; null = no suggestion.</summary>
    public DateTimeOffset? SuggestedRetestDate { get; set; }

    /// <summary>MOD-0176 F2 (KF-D, TRACE 1.4.0 <c>LotRef.expiryOverride</c>): the override the lot dates were set with; null = none.</summary>
    public LotExpiryOverride? ExpiryOverride { get; set; }
}

/// <summary>KF-D: the reason (+ MOD-0022 signature id) of a lot date that differs from its suggestion — a reference, no signature record.</summary>
public sealed class LotExpiryOverride
{
    public Guid OverrideId { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string? SignatureId { get; set; }

    public DateTimeOffset? SuggestedExpiry { get; set; }

    public DateTimeOffset? SuggestedRetest { get; set; }

    public Guid By { get; set; }

    public DateTimeOffset AtUtc { get; set; }
}

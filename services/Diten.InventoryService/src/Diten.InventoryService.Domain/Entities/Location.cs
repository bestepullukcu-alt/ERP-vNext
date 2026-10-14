using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.Entities;

/// <summary>
/// One node of the place hierarchy Site → Warehouse → StorageLocation → Zone → Bin (MOD-0173-FU02 §4). One aggregate
/// for all five levels; <see cref="Type"/> decides the allowed parent (K-01). <see cref="EntityBase.Id"/> is the
/// <c>locationId</c> the ledger stores and never changes; <see cref="Code"/> is always editable (K-02).
/// </summary>
public sealed class Location : EntityBase
{
    public string Code { get; set; } = string.Empty;

    /// <summary><see cref="Code"/> upper-invariant: unique per Tenant + LE across every status (ASSUMPTION-LOC-03).</summary>
    public string CodeNormalized { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public LocationType Type { get; set; }

    public Guid? ParentId { get; set; }

    /// <summary>Materialized path root → parent; rewritten for the whole subtree on a move (K-07).</summary>
    public List<Guid> AncestorIds { get; set; } = [];

    public LocationStatus Status { get; set; } = LocationStatus.Active;

    public bool IsStockable { get; set; }

    /// <summary><c>internal</c> · <c>external-integrated</c>; Site / Warehouse only (DEC-INV-19, K-09).</summary>
    public string? OperatingMode { get; set; }

    /// <summary>Soft link to a Platform OrganizationUnit of the same legal entity — never a foreign key.</summary>
    public string? OrgUnitRef { get; set; }

    public string? CostCenterCode { get; set; }

    /// <summary>
    /// Bumped by every ledger posting to this location INSIDE the posting transaction, so a posting and a lifecycle
    /// change of the same document conflict in Mongo and one of them retries on the committed state (§8).
    /// </summary>
    public long StockTouchSeq { get; set; }

    /// <summary>
    /// Bumped on a parent when a child is created, moved under it or re-activated, so those writes conflict with a
    /// concurrent deactivation / archive of the parent (K-05 / K-06 serialised like <see cref="StockTouchSeq"/>).
    /// </summary>
    public long StructureSeq { get; set; }

    public Guid CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    /// <summary>The create <c>Idempotency-Key</c> (unique per Tenant + LE) and its request fingerprint.</summary>
    public string? CreateIdempotencyKey { get; set; }

    public string? CreateRequestFingerprint { get; set; }

    /// <summary>The bulk-bins <c>Idempotency-Key</c> of the batch that created this bin, and that batch's fingerprint.</summary>
    public string? BulkIdempotencyKey { get; set; }

    public string? BulkRequestFingerprint { get; set; }
}

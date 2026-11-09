using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.CycleCounting.Entities;

/// <summary>
/// MOD-0181 count (pack §3, §4.1) — the mutable head of one physical count: plan, scope and state. It is NOT stock: no member carries an
/// OnHand / Available / Reserved total (pack §2.3 rule 1; the single-SoR architecture test pins that). Every change is a compare-and-set on
/// <see cref="EntityBase.Version"/>. A Draft cancel is a soft delete (<see cref="EntityBase.IsDeleted"/>, <see cref="EntityBase.DeletedAt"/>).
/// </summary>
public sealed class CycleCount : EntityBase
{
    /// <summary><c>{COMPANY}-CNT-{000001}</c> (RCS-001; MOD-0178 temporary counter, type CNT — Q-7 = B). Unique tenant-wide; never changes.</summary>
    public string Number { get; set; } = string.Empty;

    public string CompanyCodePrefix { get; set; } = string.Empty;

    public Guid WarehouseId { get; set; }

    public CycleCountType CountType { get; set; }

    public bool Blind { get; set; }

    public List<Guid> ScopeLocationIds { get; set; } = [];

    public List<Guid> ScopeSkuIds { get; set; } = [];

    /// <summary>UTC calendar day, stored as <c>yyyy-MM-dd</c>.</summary>
    public string PlannedDate { get; set; } = string.Empty;

    public Guid? AssignedTo { get; set; }

    public string? Note { get; set; }

    public CycleCountStatus Status { get; set; } = CycleCountStatus.Draft;

    /// <summary>The UTC instant the release began, from MOD-0181's own <see cref="TimeProvider"/> (pack §4.1, r5 SB1/F-3). Never changes after.</summary>
    public DateTimeOffset? SnapshotAt { get; set; }

    /// <summary>Ledger movements the release's snapshot read saw on the scope bins with <c>CreatedAt ≥ SnapshotAt − W</c> (pack §8.2 rule 4).</summary>
    public List<Guid> SnapshotTransactionIds { get; set; } = [];

    /// <summary>The stockable Active subtree of the scope, computed at release; never changes after.</summary>
    public List<Guid> ScopeBinIds { get; set; } = [];

    public int LineCount { get; set; }

    public Guid? RecountOfId { get; set; }

    public Guid CreatedBy { get; set; }

    public Guid? CancelledBy { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public string? CancelReason { get; set; }

    /// <summary>The create request's key (8–100) and fingerprint — unique per (tenant, company, creator, key).</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    public string RequestFingerprint { get; set; } = string.Empty;

    /// <summary>The last timeline <c>Seq</c> written for this count (a write's events take the next values in the same compare-and-set write).</summary>
    public int EventSeq { get; set; }

    public Guid CorrelationId { get; set; }
}

/// <summary>
/// One line of a count (pack §4.2): one key (bin, SKU, level, lot, stock status). <see cref="ExpectedQuantity"/> is COUNT EVIDENCE written once
/// at release (or 0 for a found line) and never updated — not a balance (pack §2.3 rule 1, AC-4).
/// </summary>
public sealed class CycleCountLine : EntityBase
{
    public Guid CountId { get; set; }

    public Guid WarehouseId { get; set; }

    public Guid LocationId { get; set; }

    public Guid ItemId { get; set; }

    public Guid SkuId { get; set; }

    public SkuLevel SkuLevel { get; set; }

    public string? LotId { get; set; }

    public StockStatus StockStatus { get; set; } = StockStatus.Available;

    public string BaseUomId { get; set; } = string.Empty;

    public CycleCountLineOrigin Origin { get; set; } = CycleCountLineOrigin.Snapshot;

    public Guid? RecountOfLineId { get; set; }

    public int RecountDepth { get; set; }

    public decimal ExpectedQuantity { get; set; }

    public decimal? CountedQuantity { get; set; }

    public bool SerialControlled { get; set; }

    public CycleCountLineStatus Status { get; set; } = CycleCountLineStatus.Open;
}

/// <summary>Insert-only count timeline (pack §4.3), written in the same transaction as the change it records; never deleted.</summary>
public sealed class CycleCountEvent : EntityBase
{
    public Guid CountId { get; set; }

    public Guid? LineId { get; set; }

    public int Seq { get; set; }

    public CycleCountEventKind Kind { get; set; }

    public Guid? ActorUserId { get; set; }

    public DateTimeOffset At { get; set; }

    public string? RequestOp { get; set; }

    public string? IdempotencyKey { get; set; }

    public string? RequestFingerprint { get; set; }

    public Dictionary<string, string?> Data { get; set; } = [];

    public Guid CorrelationId { get; set; }
}

/// <summary>
/// A bin claim of an open count (pack §4.7, F-6): written at release for EVERY scope bin (an empty bin too); a partial unique index on
/// (tenant, company, bin) where <see cref="Open"/> refuses a second open claim. Closed / Cancelled release their own claims (soft). Not a stock
/// record: it carries no quantity and never blocks a ledger movement (Q-6).
/// </summary>
public sealed class CycleCountBinClaim : EntityBase
{
    public Guid BinId { get; set; }

    public Guid CountId { get; set; }

    public bool Open { get; set; } = true;

    public DateTimeOffset ClaimedAt { get; set; }

    public DateTimeOffset? ReleasedAt { get; set; }

    public Guid? TransferredFromCountId { get; set; }
}

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

    /// <summary>
    /// DL-3, a recount only (pack §4.1, r6 SB2/F-2): the keys the recount's snapshot saw with stock on its bins × SKUs that were NOT selected — a
    /// found line on one of them is 422 <c>COUNT_LINE_OUT_OF_SCOPE</c> (its balance is in no expected quantity). Never changes after.
    /// </summary>
    public List<CycleCountKeyRef> RecountExcludedKeys { get; set; } = [];

    public Guid CreatedBy { get; set; }

    /// <summary>DL-2: who submitted the count and when (MOD-0181's clock) — server-written (pack §4.1).</summary>
    public Guid? SubmittedBy { get; set; }

    public DateTimeOffset? SubmittedAt { get; set; }

    public Guid? CancelledBy { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public string? CancelReason { get; set; }

    /// <summary>The create request's key (8–100) and fingerprint — unique per (tenant, company, creator, key).</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    public string RequestFingerprint { get; set; } = string.Empty;

    /// <summary>The last timeline <c>Seq</c> written for this count (a write's events take the next values in the same compare-and-set write).</summary>
    public int EventSeq { get; set; }

    public Guid CorrelationId { get; set; }

    /// <summary>A working copy (the stored instance stays as read for the compare-and-set); lists are copied.</summary>
    public CycleCount Copy()
    {
        var copy = (CycleCount)MemberwiseClone();
        copy.ScopeLocationIds = [.. ScopeLocationIds];
        copy.ScopeSkuIds = [.. ScopeSkuIds];
        copy.SnapshotTransactionIds = [.. SnapshotTransactionIds];
        copy.ScopeBinIds = [.. ScopeBinIds];
        copy.RecountExcludedKeys = RecountExcludedKeys.Select(k => k.Copy()).ToList();
        return copy;
    }
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

    /// <summary>DL-2: who entered the quantity and when — the server's user and MOD-0181's clock (pack §4.2, AC-14).</summary>
    public Guid? CountedBy { get; set; }

    public DateTimeOffset? CountedAt { get; set; }

    /// <summary>DL-2, written at submit (pack §8.4): the net ledger movement on the key between the snapshot and <see cref="CountedAt"/>.</summary>
    public decimal? MovementsSinceSnapshot { get; set; }

    /// <summary><c>ExpectedQuantity + MovementsSinceSnapshot</c>.</summary>
    public decimal? ExpectedAtCount { get; set; }

    /// <summary><c>CountedQuantity − ExpectedAtCount</c>.</summary>
    public decimal? VarianceQuantity { get; set; }

    /// <summary>Q-6 = B: a movement touched the key between the snapshot and the entry — the line needs a decision even at variance 0.</summary>
    public bool MovementDuringCount { get; set; }

    /// <summary>F-5: the balance equation failed at submit — the line needs a decision whatever its variance (0 included), never a tolerance.</summary>
    public bool SnapshotDrift { get; set; }

    public bool SerialControlled { get; set; }

    /// <summary>Opaque scan reference (MOD-0182 fills it; v1 stores, never checks — Q-12 = A).</summary>
    public string? ScanRef { get; set; }

    public CycleCountLineStatus Status { get; set; } = CycleCountLineStatus.Open;

    /// <summary>DL-3 (pack §4.2): the decision — approve (a user or the tolerance), reject or recount. Its label is a key, never stored text.</summary>
    public CycleCountDecision? Decision { get; set; }

    /// <summary>DL-3 (pack §4.2, §8.6): the ledger correction of an approved variance and its recovery state.</summary>
    public CycleCountAdjustment? Adjustment { get; set; }

    /// <summary>DL-3 (pack §4.2, F-12): the <c>resolve-integrity</c> decision of an <c>IntegrityError</c> adjustment.</summary>
    public CycleCountIntegrityResolution? IntegrityResolution { get; set; }

    /// <summary>A working copy (the stored instance stays as read for the compare-and-set); the embedded records are copied.</summary>
    public CycleCountLine Copy()
    {
        var copy = (CycleCountLine)MemberwiseClone();
        copy.Decision = Decision?.Copy();
        copy.Adjustment = Adjustment?.Copy();
        copy.IntegrityResolution = IntegrityResolution?.Copy();
        return copy;
    }
}

/// <summary>A line key as stored (pack §4.1 <c>RecountExcludedKeys</c>): bin, SKU, level, lot, stock status.</summary>
public sealed class CycleCountKeyRef
{
    public Guid LocationId { get; set; }

    public Guid SkuId { get; set; }

    public SkuLevel SkuLevel { get; set; }

    public string? LotId { get; set; }

    public StockStatus StockStatus { get; set; } = StockStatus.Available;

    public CycleCountKeyRef Copy() => (CycleCountKeyRef)MemberwiseClone();
}

/// <summary>
/// Pack §4.2 <c>Decision</c>: <c>{ Kind, Source, By?, At, Reason }</c>. A tolerance approval has <c>Source = Tolerance</c> and no <c>By</c>; the
/// screen and the evidence show the key <c>CycleCount.Decision.Source.{Source}</c>, never a stored label (r5, SB1/F-2).
/// </summary>
public sealed class CycleCountDecision
{
    public CycleCountDecisionKind Kind { get; set; }

    public CycleCountDecisionSource Source { get; set; } = CycleCountDecisionSource.User;

    public Guid? By { get; set; }

    public DateTimeOffset At { get; set; }

    public string? Reason { get; set; }

    public CycleCountDecision Copy() => (CycleCountDecision)MemberwiseClone();
}

/// <summary>
/// Pack §4.2 <c>Adjustment</c> / §8.6: the ONE ledger correction of an approved variance — <c>COUNT_GAIN</c> / <c>COUNT_LOSS</c> of |variance| in the
/// base unit under the key <c>cycle-count:{lineId:D}</c>. <see cref="TransactionDate"/> (the line's <c>CountedAt</c>) and <see cref="PostingDate"/>
/// (the first attempt's UTC day) are written on the first attempt and sent byte-identical on every attempt (the ledger fingerprint covers them).
/// <see cref="FirstAttemptAt"/> is written in transaction 1, before the posting, once (the pack §2.4 match of a Pending line, r4 02/F-2).
/// </summary>
public sealed class CycleCountAdjustment
{
    public MovementType MovementType { get; set; }

    public decimal Quantity { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public CycleCountAdjustmentStatus Status { get; set; } = CycleCountAdjustmentStatus.Pending;

    public Guid? TransactionId { get; set; }

    public string? TransactionNumber { get; set; }

    /// <summary>A ledger refusal's code, details and HTTP status, as the ledger answered them (pack §8.7, AC-27 / AC-28).</summary>
    public string? RefusalCode { get; set; }

    public string? RefusalDetailsJson { get; set; }

    public int? RefusalStatus { get; set; }

    /// <summary>An integrity error's reason (<c>KeyReused</c> / <c>HolderReversed</c>) and the movement that holds the key.</summary>
    public string? IntegrityReason { get; set; }

    public Guid? HolderTransactionId { get; set; }

    public DateTimeOffset TransactionDate { get; set; }

    public DateTimeOffset PostingDate { get; set; }

    public int Attempts { get; set; }

    public DateTimeOffset FirstAttemptAt { get; set; }

    public DateTimeOffset LastAttemptAt { get; set; }

    public CycleCountAdjustment Copy() => (CycleCountAdjustment)MemberwiseClone();
}

/// <summary>Pack §4.2 (F-12): <c>{ Kind: AcceptHolder | Reject, By, At, Reason, HolderTransactionId }</c>.</summary>
public sealed class CycleCountIntegrityResolution
{
    public CycleCountIntegrityDecision Kind { get; set; }

    public Guid By { get; set; }

    public DateTimeOffset At { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid? HolderTransactionId { get; set; }

    public CycleCountIntegrityResolution Copy() => (CycleCountIntegrityResolution)MemberwiseClone();
}

/// <summary>
/// Pack §4.4 (Q-3 / Q-11 / Q-24 = A): the company's count settings — one record per company; without one the defaults hold (0 / 0 / 2 / 2000:
/// every variance needs a decision). No value threshold (Q-3, Q-19). Not stock.
/// </summary>
public sealed class CycleCountSettings : EntityBase
{
    public decimal ToleranceAbsolute { get; set; }

    public decimal TolerancePercent { get; set; }

    public int MaxRecounts { get; set; } = 2;

    public int MaxLinesPerCount { get; set; } = 2000;

    public Guid? UpdatedBy { get; set; }

    /// <summary>The last timeline <c>Seq</c> of the settings' own trail (its rows carry the settings id as their <c>CountId</c>).</summary>
    public int EventSeq { get; set; }

    public CycleCountSettings Copy() => (CycleCountSettings)MemberwiseClone();
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

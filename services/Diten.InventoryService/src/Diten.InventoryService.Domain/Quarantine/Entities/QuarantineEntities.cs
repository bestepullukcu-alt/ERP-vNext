using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Quarantine.Enums;

namespace Diten.InventoryService.Domain.Quarantine.Entities;

// MOD-0175 records (pack §4). Every entity carries TenantId + LegalEntityId (EntityBase), none is ever deleted, none
// keeps a balance: a hold / blocked-stock quantity is the quantity the record is about, the stock itself is the ledger's.

/// <summary>Stock waiting for a quality decision — <c>KRN-{yyyy}-{nnnn}</c>.</summary>
public sealed class QuarantineHold : EntityBase
{
    public string HoldNumber { get; set; } = string.Empty;

    public Guid ItemId { get; set; }

    public Guid SkuId { get; set; }

    public SkuLevel SkuLevel { get; set; }

    /// <summary>Ledger lot id (registered lot = canonical UUID string, or a typed lot text); null for non-lot items.</summary>
    public string? LotId { get; set; }

    public string WarehouseId { get; set; } = string.Empty;

    public string LocationId { get; set; } = string.Empty;

    /// <summary>The bucket status at intake: <see cref="Enums.StockStatus.Quarantine"/> or <see cref="Enums.StockStatus.QualityInspection"/>.</summary>
    public StockStatus StockStatus { get; set; }

    public decimal Quantity { get; set; }

    public string BaseUomId { get; set; } = string.Empty;

    /// <summary>FU-0175-Q1-SER: the serial numbers of a serial-tracked hold (from the intake movement), in intake order.</summary>
    public List<string>? SerialIds { get; set; }

    public HoldSource Source { get; set; }

    /// <summary>Ledger transaction that put the quantity into the bucket; unique per tenant when set (intake idempotency).</summary>
    public string? SourceMovementId { get; set; }

    public HoldSourceDocument? SourceDocument { get; set; }

    public DateTimeOffset EnteredAtUtc { get; set; }

    public Guid CreatedBy { get; set; }

    /// <summary>"Karantinaya Al": the request key + bucket index that created this hold (null for intake / rework holds).</summary>
    public string? CreateIdempotencyKey { get; set; }

    public string? CreateRequestFingerprint { get; set; }

    public HoldReviewState ReviewState { get; set; } = HoldReviewState.Waiting;

    public DateTimeOffset? ReviewStartedAtUtc { get; set; }

    public Guid? ReviewStartedBy { get; set; }

    public string? AnalysisNote { get; set; }

    public List<HoldAttachment> Attachments { get; set; } = [];

    public HoldStatus Status { get; set; } = HoldStatus.Open;

    public Guid? ActiveDecisionId { get; set; }

    public DateTimeOffset? DecidedAtUtc { get; set; }

    public DateTimeOffset? RetestDateAtIntake { get; set; }
}

public sealed class HoldSourceDocument
{
    public string? Module { get; set; }

    public string? Type { get; set; }

    public string? Id { get; set; }

    public string? Number { get; set; }

    public string? Text { get; set; }
}

public sealed class HoldAttachment
{
    public string Name { get; set; } = string.Empty;

    public string DmsRef { get; set; } = string.Empty;
}

/// <summary>
/// A quality decision — <c>QKR-{yyyy}-{nnnn}</c>. INSERT-ONLY: written once at draft time and never changed (it is the
/// subject a decider signs, pack §8 KQ-G). Its state is the last <see cref="QuarantineDecisionEvent"/>.
/// </summary>
public sealed class QuarantineDecision : EntityBase
{
    public string DecisionNumber { get; set; } = string.Empty;

    public DecisionKind Kind { get; set; }

    public Guid? HoldId { get; set; }

    public Guid? BlockedStockId { get; set; }

    /// <summary>Snapshot of the bucket the decision acts on.</summary>
    public Guid ItemId { get; set; }

    public Guid SkuId { get; set; }

    public SkuLevel SkuLevel { get; set; }

    public string? LotId { get; set; }

    public string WarehouseId { get; set; } = string.Empty;

    public string LocationId { get; set; } = string.Empty;

    public StockStatus FromStockStatus { get; set; }

    public string BaseUomId { get; set; } = string.Empty;

    public decimal AvailableQuantityAtDecision { get; set; }

    public List<DecisionLine> Lines { get; set; } = [];

    public string? BlockReasonCode { get; set; }

    public string Reason { get; set; } = string.Empty;

    public bool CoaChecked { get; set; }

    public string? CoaReference { get; set; }

    public DecisionDisposition? Disposition { get; set; }

    public DateTimeOffset? NewRetestDate { get; set; }

    public string? LotStatusTarget { get; set; }

    /// <summary>Q4 (KQ-I): the lot status a LotRelease was requested from (Draft / Blocked) — the only status its approval releases.</summary>
    public string? LotStatusFrom { get; set; }

    /// <summary>FU-0175-Q1-SER: the serials an Unblock decision moves (fixed at draft time).</summary>
    public List<string>? SerialIds { get; set; }

    public Guid DeciderUserId { get; set; }

    public DateTimeOffset DecidedAtUtc { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public string RequestFingerprint { get; set; } = string.Empty;
}

public sealed class DecisionLine
{
    public int LineNo { get; set; }

    public LineDecision Decision { get; set; }

    public decimal Quantity { get; set; }

    public StockStatus ToStockStatus { get; set; }

    /// <summary>FU-0175-Q1-SER: the serials this line moves (fixed at draft time, ASSUMPTION-0175-Q2-01).</summary>
    public List<string>? SerialIds { get; set; }
}

public sealed class DecisionDisposition
{
    public DispositionKind Kind { get; set; }

    public decimal Quantity { get; set; }

    public Guid? SupplierId { get; set; }

    public string? ReturnDocumentNo { get; set; }

    public string? DestructionMethod { get; set; }

    public DateTimeOffset? DestructionDate { get; set; }

    /// <summary>KQ-E: the company's sample location of the warehouse, resolved and checked at draft time (V13).</summary>
    public string? SampleLocationId { get; set; }

    /// <summary>FU-0175-Q1-SER: the serials the disposition moves (fixed at draft time).</summary>
    public List<string>? SerialIds { get; set; }
}

/// <summary>Insert-only event of a decision. <see cref="OnceKey"/> makes the once-only kinds unique per decision.</summary>
public sealed class QuarantineDecisionEvent : EntityBase
{
    public Guid DecisionId { get; set; }

    public DecisionEventKind Kind { get; set; }

    /// <summary>The kind name for once-only kinds (unique index); null for the repeatable StartFailed / ApplyFailed.</summary>
    public string? OnceKey { get; set; }

    public DateTimeOffset AtUtc { get; set; }

    /// <summary>Strictly increasing order of the events of a decision (two events may share a timestamp).</summary>
    public long Sequence { get; set; }

    public Guid? ByUserId { get; set; }

    public string? WorkflowInstanceId { get; set; }

    public string? WorkflowOutcome { get; set; }

    public Guid? ApproverUserId { get; set; }

    public string? ApproverSignatureId { get; set; }

    /// <summary>Q3 — on <c>Submitted</c>: the decider's MOD-0022 signature id (an id only, KQ-G) and whether the policy required it.</summary>
    public string? DeciderSignatureId { get; set; }

    public bool? SignatureRequired { get; set; }

    public List<string> MovementIds { get; set; } = [];

    public string? ErrorCode { get; set; }

    public string? Detail { get; set; }
}

/// <summary>Blocked stock waiting for (or after) a disposition — <c>BLK-{yyyy}-{nnnn}</c>; never deleted.</summary>
public sealed class BlockedStock : EntityBase
{
    public string BlockedNumber { get; set; } = string.Empty;

    public Guid ItemId { get; set; }

    public Guid SkuId { get; set; }

    public SkuLevel SkuLevel { get; set; }

    public string? LotId { get; set; }

    public string WarehouseId { get; set; } = string.Empty;

    public string LocationId { get; set; } = string.Empty;

    /// <summary>Remaining quantity of this record (the stock itself stays BLOCKED in the ledger).</summary>
    public decimal Quantity { get; set; }

    /// <summary>FU-0175-Q1-SER: the remaining serial numbers of a serial-tracked record.</summary>
    public List<string>? SerialIds { get; set; }

    public string BaseUomId { get; set; } = string.Empty;

    public string? BlockReasonCode { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset BlockedAtUtc { get; set; }

    public BlockedOrigin Origin { get; set; }

    public Guid? OriginHoldId { get; set; }

    public Guid? OriginDecisionId { get; set; }

    public int? OriginLineNo { get; set; }

    public string? SourceMovementId { get; set; }

    public string? CreateIdempotencyKey { get; set; }

    public string? CreateRequestFingerprint { get; set; }

    public Guid CreatedBy { get; set; }

    public bool PendingDisposition { get; set; }

    public bool Reviewed { get; set; }

    public BlockedStatus Status { get; set; } = BlockedStatus.Open;

    public Guid? ActiveDecisionId { get; set; }
}

/// <summary>"Karar geçmişi" row of a hold or a blocked record; insert-only.</summary>
public sealed class QuarantineHistoryEntry : EntityBase
{
    public HistorySubject SubjectType { get; set; }

    public Guid SubjectId { get; set; }

    public DateTimeOffset AtUtc { get; set; }

    public Guid ByUserId { get; set; }

    public HistoryAction Action { get; set; }

    public string? Reason { get; set; }

    public Guid? DecisionId { get; set; }

    public string? SignatureId { get; set; }

    public string? ApproverSignatureId { get; set; }

    public Guid CorrelationId { get; set; }

    public string LeSource { get; set; } = string.Empty;
}

/// <summary>
/// Q4R (O-Q4-3): "this lot has an open LotRelease" — at most ONE per Tenant + LegalEntity + lot, held by the unique index
/// <c>ux_quarantine_lot_release_open</c>. Inserted in the SAME 0175 transaction as the LotRelease decision; the decision stays
/// insert-only, so the claim is a separate record. A claim whose decision has closed (Applied / Rejected / Cancelled / TimedOut /
/// Withdrawn / FourEyesViolation) — or that was never sent and no longer matches the lot's status — is replaced by the next request in
/// one transaction (delete + insert; a concurrent replacement conflicts and re-reads).
/// </summary>
public sealed class LotReleaseClaim : EntityBase
{
    public string LotId { get; set; } = string.Empty;

    public Guid DecisionId { get; set; }
}

/// <summary>Quarantine settings of one company (Tenant + LegalEntity).</summary>
public sealed class QuarantineSettings : EntityBase
{
    public int WaitTargetDays { get; set; } = 10;

    public bool FourEyes { get; set; } = true;

    public List<string> ApproverCandidates { get; set; } = [];

    /// <summary>KQ-E: warehouseId → sample locationId.</summary>
    public Dictionary<string, string> SampleLocations { get; set; } = [];

    public bool LotReleaseRequiresApproval { get; set; } = true;

    public int ReconcileAfterSeconds { get; set; } = 30;

    public Guid? UpdatedBy { get; set; }
}

/// <summary>Local tenant block-reason list (CT-Q8); <see cref="EntityBase.LegalEntityId"/> is null (tenant-wide).</summary>
public sealed class BlockReason : EntityBase
{
    public string Code { get; set; } = string.Empty;

    public Dictionary<string, string> Names { get; set; } = [];

    public bool IsActive { get; set; } = true;

    public DateTimeOffset? FirstUsedAtUtc { get; set; }
}

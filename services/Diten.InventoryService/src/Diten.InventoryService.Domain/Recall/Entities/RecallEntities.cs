using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Recall.Enums;

namespace Diten.InventoryService.Domain.Recall.Entities;

// MOD-0177 records (pack §3–§4). Every entity carries TenantId + LegalEntityId (EntityBase, stamped by the repository from the
// resolved scope); none is ever deleted and none keeps a balance: a scope item / declaration quantity is the quantity the record
// is about, the stock itself is the MOD-0173 ledger's. 0177 keeps NO signature record — only MOD-0022 ids (pack §8.8).

/// <summary>
/// The company's recall settings (KR-G). One row per Tenant + LegalEntity; no row = the defaults (<see cref="Rules.RecallSettingsRules.Defaults"/>).
/// <see cref="EntityBase.Version"/> is the If-Match version; the first save is version 0 → 1.
/// </summary>
public sealed class RecallSettings : EntityBase
{
    public int DrillTargetHours { get; set; }

    public int DrillFrequencyWarningDays { get; set; }

    /// <summary>Recall class ("1" | "2" | "3") → target percent 0–100.</summary>
    public Dictionary<string, decimal> EffectivenessTargetPercent { get; set; } = [];

    public int TraceMaxDepth { get; set; }

    public int TraceMaxNodes { get; set; }

    public Guid? UpdatedBy { get; set; }

    public RecallSettings Copy()
    {
        var copy = (RecallSettings)MemberwiseClone();
        copy.EffectivenessTargetPercent = new Dictionary<string, decimal>(EffectivenessTargetPercent, StringComparer.Ordinal);
        return copy;
    }
}

/// <summary>One changed field of one settings save (insert-only; mockup "Değişiklik geçmişi").</summary>
public sealed class RecallSettingsHistoryEntry : EntityBase
{
    public string Field { get; set; } = string.Empty;

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid ChangedBy { get; set; }

    public DateTimeOffset ChangedAtUtc { get; set; }

    public int SettingsVersion { get; set; }

    /// <summary>MOD-0022 id when the tenant's policy row <c>recall.settings.update</c> required one (CT-R16: not by default).</summary>
    public string? SignatureId { get; set; }

    public Guid CorrelationId { get; set; }
}

/// <summary>
/// A recall case — <c>RC-{yyyy}-{nnnn}</c>. Mutable head (If-Match on <see cref="EntityBase.Version"/>); what happened to it is the
/// insert-only <see cref="RecallCaseEvent"/> stream. State machine: pack §8.7 (<see cref="Rules.RecallCaseRules"/>).
/// </summary>
public sealed class RecallCase : EntityBase
{
    public string Number { get; set; } = string.Empty;

    public RecallCaseStatus Status { get; set; } = RecallCaseStatus.Draft;

    /// <summary>Recall class 1–3 (drives the effectiveness target).</summary>
    public int Class { get; set; }

    public RecallLevel Level { get; set; }

    public RecallTraceDirection Direction { get; set; }

    /// <summary>The root lot (tenant lot id = canonical UUID string) and its snapshot at opening.</summary>
    public string RootLotId { get; set; } = string.Empty;

    public string RootLotNumber { get; set; } = string.Empty;

    public Guid ItemId { get; set; }

    public Guid SkuId { get; set; }

    public SkuLevel SkuLevel { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string? ReasonReference { get; set; }

    public Guid OwnerUserId { get; set; }

    public DateTimeOffset OpenedAtUtc { get; set; }

    /// <summary>1 at open; a scope revision after approval (CT-R5) is R2.</summary>
    public int ScopeRevision { get; set; } = 1;

    public RecallScopeSeal? SubmittedScope { get; set; }

    /// <summary>R2 (pack §8.3): the trace snapshot sealed at submit / at approval (insert-only records).</summary>
    public Guid? SubmittedSnapshotId { get; set; }

    public Guid? ApprovedSnapshotId { get; set; }

    public RecallScopeSeal? ApprovedScope { get; set; }

    public Guid? ScopeSubmittedBy { get; set; }

    public DateTimeOffset? ScopeSubmittedAtUtc { get; set; }

    public Guid? ScopeApprovedBy { get; set; }

    public DateTimeOffset? ScopeApprovedAtUtc { get; set; }

    public string? ScopeApprovalSignatureId { get; set; }

    public DateTimeOffset? EffectivenessStartedAtUtc { get; set; }

    public RecallEffectivenessResult? Effectiveness { get; set; }

    /// <summary>CT-R1: the completer may not close.</summary>
    public Guid? EffectivenessCompletedBy { get; set; }

    public DateTimeOffset? EffectivenessCompletedAtUtc { get; set; }

    public string? EffectivenessNote { get; set; }

    public string? EffectivenessSignatureId { get; set; }

    public string? ClosureSummary { get; set; }

    public string? OpenQuantityAcceptance { get; set; }

    public Guid? ClosedBy { get; set; }

    public DateTimeOffset? ClosedAtUtc { get; set; }

    public string? ClosureSignatureId { get; set; }

    public string? CancelReason { get; set; }

    public Guid? CancelledBy { get; set; }

    public DateTimeOffset? CancelledAtUtc { get; set; }

    public RecallPendingAction? PendingAction { get; set; }

    /// <summary>The idempotency key of the opening (unique per company + opener).</summary>
    public string? CreateIdempotencyKey { get; set; }

    public string? CreateRequestFingerprint { get; set; }

    /// <summary>One ACTIVE (non-terminal) case per company + root lot (unique partial index); cleared at Closed / Cancelled.</summary>
    public string? ActiveRootKey { get; set; }

    /// <summary>R3 (CT-R5): a scope revision r+1 waiting for the signed approval of its delta; the case stays Active, the blocks stay.</summary>
    public RecallScopeRevision? PendingRevision { get; set; }
}

/// <summary>CT-R5 — revision r+1 of an approved scope: the out-of-scope findings added (the delta), why, by whom.</summary>
public sealed class RecallScopeRevision
{
    public int Revision { get; set; }

    public string Reason { get; set; } = string.Empty;

    public List<string> AddedNodeKeys { get; set; } = [];

    public Guid RevisedBy { get; set; }

    public DateTimeOffset RevisedAtUtc { get; set; }
}

/// <summary>The canonical hash of the scope at submit / approval (pack §8.3): what the approver signs is exactly this.</summary>
public sealed class RecallScopeSeal
{
    public int Revision { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public string CanonicalVersion { get; set; } = string.Empty;

    public int ItemCount { get; set; }

    public DateTimeOffset ComputedAtUtc { get; set; }
}

/// <summary>A decision the user prepared for signing (15 minutes). Part of the signed subject in R4.</summary>
public sealed class RecallPendingAction
{
    public RecallPendingActionKind Action { get; set; }

    public Guid SetBy { get; set; }

    public DateTimeOffset SetAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    /// <summary>SHA-256 of the prepared payload (note / summary / acceptance) — the command must send the same.</summary>
    public string PayloadSha256 { get; set; } = string.Empty;

    /// <summary>ApproveScope: the scope hash the user prepared to sign.</summary>
    public string? ScopeSha256 { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public string RequestFingerprint { get; set; } = string.Empty;
}

/// <summary>The stored effectiveness result (pack §8.6), per (customer, SKU, base unit) + totals per (SKU, unit).</summary>
public sealed class RecallEffectivenessResult
{
    public int Class { get; set; }

    public decimal TargetPercent { get; set; }

    public List<RecallEffectivenessRow> Rows { get; set; } = [];

    public List<RecallEffectivenessRow> Totals { get; set; } = [];

    public DateTimeOffset ComputedAtUtc { get; set; }
}

public sealed class RecallEffectivenessRow
{
    /// <summary>Null on a total row.</summary>
    public string? CustomerRef { get; set; }

    public Guid SkuId { get; set; }

    public string BaseUomId { get; set; } = string.Empty;

    public decimal Shipped { get; set; }

    public decimal Returned { get; set; }

    public decimal Consumed { get; set; }

    public decimal Open { get; set; }

    public decimal Percent { get; set; }

    public bool Met { get; set; }

    public bool Notified { get; set; }
}

/// <summary>
/// One item of the case scope (pack §4.3). Node key = <see cref="Rules.RecallScopeRules.NodeKey"/> (kind + ids), stable across R1
/// (hand entry) and R2 (trace). <see cref="Included"/> is the item's OWN mark; an excluded ancestor excludes its subtree (CT-R6 —
/// <see cref="Rules.RecallScopeRules.EffectivelyIncluded"/>). Editable in Draft only.
/// </summary>
public sealed class RecallScopeItem : EntityBase
{
    public Guid CaseId { get; set; }

    public int Revision { get; set; }

    public string NodeKey { get; set; } = string.Empty;

    public string? ParentKey { get; set; }

    public RecallScopeItemKind Kind { get; set; }

    public bool IsRoot { get; set; }

    public string LotId { get; set; } = string.Empty;

    public string LotNumber { get; set; } = string.Empty;

    public Guid ItemId { get; set; }

    public Guid SkuId { get; set; }

    public SkuLevel SkuLevel { get; set; }

    public string? WarehouseId { get; set; }

    public string? LocationId { get; set; }

    public StockStatus? StockStatus { get; set; }

    public string? SourceDocumentId { get; set; }

    public string? CustomerRef { get; set; }

    public decimal? Quantity { get; set; }

    public string? BaseUomId { get; set; }

    public bool Included { get; set; } = true;

    public string? ExclusionReason { get; set; }

    /// <summary>R2: a trace item is reconciled with the trace at submit; a manual item is kept.</summary>
    public RecallScopeItemOrigin Origin { get; set; }

    /// <summary>R2: added by the re-trace of the last submit ("yeni", pack §8.3 step 3).</summary>
    public bool IsNew { get; set; }

    public Guid AddedBy { get; set; }

    public DateTimeOffset AddedAtUtc { get; set; }

    public Guid? MarkedBy { get; set; }

    public DateTimeOffset? MarkedAtUtc { get; set; }
}

/// <summary>
/// The lots of an APPROVED scope (pack §4.5): written in the approval transaction, released at closure. Read tenant-wide by
/// <c>getLotRecallStatus</c> (CT-R7) and — from R3 — by the posting gate.
/// </summary>
public sealed class RecallLotBlock : EntityBase
{
    public string LotId { get; set; } = string.Empty;

    /// <summary>R3: the lot's number + SKU, so a typed lot text of the same SKU is refused too (ASSUMPTION-0177-06); null on R1 rows.</summary>
    public string? LotNumber { get; set; }

    public Guid? SkuId { get; set; }

    public SkuLevel? SkuLevel { get; set; }

    /// <summary>R3: the scope revision whose approval wrote the block (CT-R5: a revision adds blocks, never removes one).</summary>
    public int Revision { get; set; } = 1;

    public Guid CaseId { get; set; }

    public string CaseNumber { get; set; } = string.Empty;

    public bool Active { get; set; } = true;

    public DateTimeOffset ActivatedAtUtc { get; set; }

    public DateTimeOffset? ReleasedAtUtc { get; set; }

    public string? ReleasedReason { get; set; }
}

/// <summary>KR-F — who was told, when, by which channel (record only; correction = a new row that supersedes). INSERT ONLY.</summary>
public sealed class RecallCustomerNotification : EntityBase
{
    public Guid CaseId { get; set; }

    public string CustomerRef { get; set; } = string.Empty;

    public DateTimeOffset NotifiedOn { get; set; }

    public RecallNotificationChannel Channel { get; set; }

    public string? Note { get; set; }

    public Guid? SupersedesId { get; set; }

    public Guid RecordedBy { get; set; }

    public DateTimeOffset RecordedAtUtc { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public string RequestFingerprint { get; set; } = string.Empty;
}

/// <summary>A customer's declaration that a quantity was consumed / used (pack §4.8). INSERT ONLY.</summary>
public sealed class RecallConsumptionDeclaration : EntityBase
{
    public Guid CaseId { get; set; }

    public string CustomerRef { get; set; } = string.Empty;

    public Guid SkuId { get; set; }

    public SkuLevel SkuLevel { get; set; }

    public string BaseUomId { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public DateTimeOffset DeclaredOn { get; set; }

    public string Evidence { get; set; } = string.Empty;

    public Guid? SupersedesId { get; set; }

    public Guid RecordedBy { get; set; }

    public DateTimeOffset RecordedAtUtc { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public string RequestFingerprint { get; set; } = string.Empty;
}

/// <summary>The warehouse confirms the physical separation of an in-scope stock item (pack §4.10; record only, no movement). INSERT ONLY.</summary>
public sealed class RecallSegregationConfirmation : EntityBase
{
    public Guid CaseId { get; set; }

    public string NodeKey { get; set; } = string.Empty;

    public string? WarehouseId { get; set; }

    public string LocationId { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public string? Note { get; set; }

    public Guid ConfirmedBy { get; set; }

    public DateTimeOffset ConfirmedAtUtc { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public string RequestFingerprint { get; set; } = string.Empty;
}

/// <summary>
/// One thing that happened to a case — INSERT ONLY. A user event keeps the request's idempotency key + fingerprint: the same key on
/// the same case answers the current state (same body) or 400 (another body).
/// </summary>
public sealed class RecallCaseEvent : EntityBase
{
    public Guid CaseId { get; set; }

    public long Sequence { get; set; }

    public RecallCaseEventKind Kind { get; set; }

    /// <summary>null = the system.</summary>
    public Guid? ActorUserId { get; set; }

    public DateTimeOffset AtUtc { get; set; }

    public string? Text { get; set; }

    public Dictionary<string, string?> Refs { get; set; } = [];

    public string? SignatureId { get; set; }

    public string? IdempotencyKey { get; set; }

    public string? RequestFingerprint { get; set; }

    public Guid CorrelationId { get; set; }
}

/// <summary>
/// The MOD-0177 audit row (AUD-001, pack §8.12) in <c>inventory_audit</c> — written in the SAME transaction as the change it describes,
/// INSERT ONLY (the "equivalent trail" of the audit-trail standard, as MOD-0176's).
/// </summary>
public sealed class RecallAuditRecord : EntityBase
{
    public string Action { get; set; } = string.Empty;

    public string SubjectType { get; set; } = string.Empty;

    public Guid SubjectId { get; set; }

    public string? SubjectNumber { get; set; }

    public Guid? UserId { get; set; }

    public string? IdempotencyKey { get; set; }

    public Dictionary<string, string?> Detail { get; set; } = [];

    public Guid? CorrelationId { get; set; }

    public string? LeSource { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}

/// <summary>
/// Pack §4.4 — the trace sealed at submit / approval (canonical <c>rt-v1</c> + SHA-256). INSERT ONLY: later data never changes it; the
/// approved snapshot is what every read of an approved case shows and what the live re-trace is compared with.
/// </summary>
public sealed class RecallTraceSnapshot : EntityBase
{
    public Guid CaseId { get; set; }

    /// <summary>WP-0177-R5: the drill run of a <c>Drill</c> snapshot (then <see cref="CaseId"/> is empty).</summary>
    public Guid? DrillRunId { get; set; }

    public int Revision { get; set; }

    public RecallSnapshotPurpose Purpose { get; set; }

    public string RootLotId { get; set; } = string.Empty;

    public string Direction { get; set; } = string.Empty;

    public string CanonicalVersion { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;

    /// <summary>The canonical JSON itself (what the hash is of).</summary>
    public string Canonical { get; set; } = string.Empty;

    public List<string> NodeKeys { get; set; } = [];

    public int NodeCount { get; set; }

    public int GapCount { get; set; }

    public bool Truncated { get; set; }

    public DateTimeOffset ComputedAtUtc { get; set; }

    public long DurationMs { get; set; }

    public Guid CreatedBy { get; set; }
}


/// <summary>
/// Pack §8.4 KR-B — one request to MOD-0175 for one included Stock node of an approved scope revision. Created in the approval transaction
/// (<see cref="Status"/> Pending) and sent after the commit by the dispatcher (the sweeper retries Pending only). The 0175 key is
/// <c>recall:{caseId}:r{revision}:{nodeKey}</c>, re-used on every attempt (0175 replays); "Yeniden dene" moves to <c>…:a{n}</c> (CT-R2).
/// The hold status is read from MOD-0175 at display time, never copied. Mutable head (Version) — what happened is the case timeline.
/// </summary>
public sealed class RecallQuarantineRequest : EntityBase
{
    public Guid CaseId { get; set; }

    public string CaseNumber { get; set; } = string.Empty;

    public int Revision { get; set; }

    public string NodeKey { get; set; } = string.Empty;

    public string LotId { get; set; } = string.Empty;

    public string LotNumber { get; set; } = string.Empty;

    public Guid ItemId { get; set; }

    public Guid SkuId { get; set; }

    public SkuLevel SkuLevel { get; set; }

    public string? WarehouseId { get; set; }

    public string? LocationId { get; set; }

    /// <summary>The quantity of the node in the approved scope (or of the live line for a later request).</summary>
    public decimal SnapshotQuantity { get; set; }

    public string? BaseUomId { get; set; }

    /// <summary>The bucket quantity of the call (current AVAILABLE, at most the snapshot quantity) — kept per key so a replay sends the same body.</summary>
    public decimal? PlannedQuantity { get; set; }

    public RecallQuarantineRequestStatus Status { get; set; }

    public RecallNotNeededReason? NotNeededReason { get; set; }

    public List<string> HoldIds { get; set; } = [];

    public decimal HeldQuantity { get; set; }

    public int Attempts { get; set; }

    /// <summary>0 = the base key; n = "Yeniden dene" n (key suffix <c>:a{n}</c>).</summary>
    public int RetryNumber { get; set; }

    /// <summary>The 0175 idempotency key of the current attempt.</summary>
    public string QuarantineKey { get; set; } = string.Empty;

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public List<RecallReservationLine> Reservations { get; set; } = [];

    /// <summary>The approver / the QA user who asked — the actor of the 0175 call (CT-R2), also for the sweeper.</summary>
    public Guid RequestedBy { get; set; }

    public DateTimeOffset RequestedAtUtc { get; set; }

    public DateTimeOffset? LastAttemptAtUtc { get; set; }

    /// <summary>The API key that created (requestQuarantine) or last retried the request — the same key replays the answer.</summary>
    public string? CommandKey { get; set; }

    public string? CommandFingerprint { get; set; }
}

/// <summary>A reservation that blocked a request (0175 <c>ReservationLine</c>) — shown on "Eldeki stok"; 0177 never cancels it.</summary>
public sealed class RecallReservationLine
{
    public string ReservationId { get; set; } = string.Empty;

    /// <summary>The module that holds the reservation (0175 ReservationLine data, shown — never a decision).</summary>
    public string? HolderModule { get; set; }

    public string? SourceDocumentId { get; set; }

    public string? RemainingQuantity { get; set; }
}

/// <summary>
/// Pack §8.6 KR-H — a CUSTOMER_RETURN of an in-scope lot (posted after the approval, into a held status) linked to the case with a customer of
/// the case. One case per return (unique transaction id per company). INSERT ONLY. 0177 writes no movement.
/// </summary>
public sealed class RecallReturnLink : EntityBase
{
    public Guid CaseId { get; set; }

    public RecallReturnSource Source { get; set; }

    public string TransactionId { get; set; } = string.Empty;

    public string? TransactionNumber { get; set; }

    public string LotId { get; set; } = string.Empty;

    public string LotNumber { get; set; } = string.Empty;

    public Guid SkuId { get; set; }

    public decimal Quantity { get; set; }

    public string BaseUomId { get; set; } = string.Empty;

    public StockStatus? ToStockStatus { get; set; }

    public DateTimeOffset PostedAtUtc { get; set; }

    public string CustomerRef { get; set; } = string.Empty;

    public string? ReturnReference { get; set; }

    public Guid LinkedBy { get; set; }

    public DateTimeOffset LinkedAtUtc { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public string RequestFingerprint { get; set; } = string.Empty;
}

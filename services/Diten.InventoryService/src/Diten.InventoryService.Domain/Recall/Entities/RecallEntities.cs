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

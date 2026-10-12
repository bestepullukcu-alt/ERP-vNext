using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Expiry.Enums;

namespace Diten.InventoryService.Domain.Expiry.Entities;

// MOD-0176 records (pack §4). Every entity carries TenantId + LegalEntityId (EntityBase, stamped by the repository from the
// resolved scope), none is ever deleted and none keeps a balance: an exception quantity is the quantity the case is about,
// the stock itself is the MOD-0173 ledger's. 0176 keeps NO signature record — only the MOD-0022 id (KF-E).

/// <summary>
/// The company's expiry policy (KF-A…KF-D, KF-G: company scope only). One row per Tenant + LegalEntity; no row = the defaults
/// (<see cref="Rules.ExpiryPolicyRules.Defaults"/>). <see cref="EntityBase.Version"/> is the If-Match version; the first save
/// is version 0 → 1. The hook modes are only STORED in F1 — the F2 hooks read them.
/// </summary>
public sealed class ExpiryPolicy : EntityBase
{
    public int ApproachingDays { get; set; }

    public int CriticalDays { get; set; }

    public FefoOutboundMode FefoOutboundMode { get; set; }

    public BelowMinRemainingMode BelowMinRemainingMode { get; set; }

    public DateOverrideMode DateOverrideMode { get; set; }

    /// <summary>Exception type wire code → SLA hours (all four types, 1–2160).</summary>
    public Dictionary<string, int> SlaHours { get; set; } = [];

    public int ExpiredOnHandThresholdDays { get; set; }

    public Guid? UpdatedBy { get; set; }

    public ExpiryPolicy Copy()
    {
        var copy = (ExpiryPolicy)MemberwiseClone();
        copy.SlaHours = new Dictionary<string, int>(SlaHours, StringComparer.Ordinal);
        return copy;
    }
}

/// <summary>One changed field of one policy save (insert-only; mockup L:95 "history per field").</summary>
public sealed class ExpiryPolicyHistoryEntry : EntityBase
{
    public string Field { get; set; } = string.Empty;

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid ChangedBy { get; set; }

    public DateTimeOffset ChangedAtUtc { get; set; }

    public int PolicyVersion { get; set; }

    public Guid CorrelationId { get; set; }
}

/// <summary>
/// A FEFO exception case — <c>FX-{yyyy}-{nnnn}</c>. Mutable head (If-Match on <see cref="EntityBase.Version"/>); what happened to it
/// is the insert-only <see cref="FefoExceptionEvent"/> stream. State machine: pack §8.4 (<see cref="Rules.ExpiryExceptionRules"/>).
/// </summary>
public sealed class FefoExceptionCase : EntityBase
{
    public string Number { get; set; } = string.Empty;

    public ExpiryExceptionType Type { get; set; }

    public ExpiryExceptionOrigin Origin { get; set; }

    public ExpiryExceptionStatus Status { get; set; } = ExpiryExceptionStatus.Open;

    public ExpiryResolution? Resolution { get; set; }

    public Guid ItemId { get; set; }

    public Guid SkuId { get; set; }

    public SkuLevel SkuLevel { get; set; }

    /// <summary>The ledger lot id (registered lot = canonical UUID string).</summary>
    public string LotId { get; set; } = string.Empty;

    public string LotNumber { get; set; } = string.Empty;

    /// <summary>Lot dates at opening (snapshot).</summary>
    public DateTimeOffset? ExpiryDate { get; set; }

    public DateTimeOffset? RetestDate { get; set; }

    public string? WarehouseId { get; set; }

    public decimal? Quantity { get; set; }

    public string? BaseUomId { get; set; }

    public FefoSourceMovement? SourceMovement { get; set; }

    /// <summary>Per-type facts (pack §4.3), string → string.</summary>
    public Dictionary<string, string> Facts { get; set; } = [];

    /// <summary>Free text of a manual opening ("İstisna aç").</summary>
    public string? Description { get; set; }

    public DateTimeOffset OpenedAtUtc { get; set; }

    /// <summary>The opener; null = the system (hook / sweeper).</summary>
    public Guid? OpenedBy { get; set; }

    /// <summary>
    /// CT-F10 (SoD): the user whose action opened the case — the opener of a manual case, the poster of a bypassing issue, the
    /// creator of the overriding lot (F2). Null for system cases without a user (BELOW_MIN_REMAINING, EXPIRED_ON_HAND). That
    /// user may not resolve the case.
    /// </summary>
    public Guid? OriginUserId { get; set; }

    /// <summary><c>OpenedAtUtc + SlaHours[type]</c> of the policy at opening.</summary>
    public DateTimeOffset DueAtUtc { get; set; }

    public Guid? AssigneeUserId { get; set; }

    public DateTimeOffset? TakenAtUtc { get; set; }

    public PendingResolution? PendingResolution { get; set; }

    public string? ResolutionReason { get; set; }

    /// <summary>MOD-0022 signature id — a reference only (KF-E). Unique per tenant when set: a signature closes one case.</summary>
    public string? ResolutionSignatureId { get; set; }

    public DateTimeOffset? ResolvedAtUtc { get; set; }

    public Guid? ResolvedBy { get; set; }

    public DateTimeOffset? ClosedAtUtc { get; set; }

    public Guid? ClosedBy { get; set; }

    /// <summary>The idempotency key of a manual opening (unique per company + opener).</summary>
    public string? CreateIdempotencyKey { get; set; }

    public string? CreateRequestFingerprint { get; set; }

    /// <summary>
    /// One ACTIVE case per key (unique partial index): <c>{TYPE}:{lotId}:{warehouseId|-}</c> for manual and sweeper cases,
    /// <c>{TYPE}:{transactionId}</c> for hook cases (F2). Cleared when the case reaches a terminal status.
    /// </summary>
    public string? DedupKey { get; set; }
}

public sealed class FefoSourceMovement
{
    public string TransactionId { get; set; } = string.Empty;

    public string? MovementType { get; set; }

    public string? SourceModule { get; set; }

    public string? SourceDocumentId { get; set; }
}

/// <summary>The decision a user prepared for signing ("prepareResolution", 15 minutes). Part of the signed subject in F3.</summary>
public sealed class PendingResolution
{
    public ExpiryResolution Resolution { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid SetBy { get; set; }

    public DateTimeOffset SetAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public string RequestFingerprint { get; set; } = string.Empty;
}

/// <summary>
/// One thing that happened to a case — INSERT ONLY (no member of the write session updates, replaces or deletes it). A user
/// event keeps the request's idempotency key + fingerprint: the same key on the same case answers the current case (same body)
/// or 400 (another body).
/// </summary>
public sealed class FefoExceptionEvent : EntityBase
{
    public Guid CaseId { get; set; }

    public long Sequence { get; set; }

    public ExpiryExceptionEventAction Action { get; set; }

    /// <summary>null = the system.</summary>
    public Guid? ActorUserId { get; set; }

    public DateTimeOffset AtUtc { get; set; }

    public string? Text { get; set; }

    public ExpiryResolution? Resolution { get; set; }

    public string? SignatureId { get; set; }

    /// <summary>Once-only events (<c>SlaBreached</c>) carry their kind here; unique per case (a concurrent second writer fails).</summary>
    public string? OnceKey { get; set; }

    public string? IdempotencyKey { get; set; }

    public string? RequestFingerprint { get; set; }

    public Guid CorrelationId { get; set; }
}

/// <summary>
/// The MOD-0176 audit row (AUD-001, pack §8.10) in <c>inventory_audit</c> — written in the SAME transaction as the change it
/// describes, INSERT ONLY. The "equivalent trail" of the audit-trail standard (path c): who, what, which record, which company
/// source, which correlation id, when.
/// </summary>
public sealed class ExpiryAuditRecord : EntityBase
{
    /// <summary><c>expiry.policy.updated</c>, <c>expiry.exception.opened</c> … (ExpiryContract.Audit*).</summary>
    public string Action { get; set; } = string.Empty;

    public string SubjectType { get; set; } = string.Empty;

    public Guid SubjectId { get; set; }

    public string? SubjectNumber { get; set; }

    /// <summary>null = the system (sweeper).</summary>
    public Guid? UserId { get; set; }

    public string? IdempotencyKey { get; set; }

    public Dictionary<string, string?> Detail { get; set; } = [];

    public Guid? CorrelationId { get; set; }

    public string? LeSource { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}

using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU15 — the retention SNAPSHOT for one regulated record (GMG-QMS-SOP-0001 §22). Answers, at the time of
/// the last evaluation: which policy applied, when the clock started, when retention is due, whether disposition is
/// eligible, and whether a legal hold blocks it.
///
/// This is a projection, not a source of truth for the record itself — evaluating a subject NEVER touches, hides or
/// deletes the underlying aggregate. Snapshots are themselves never deleted: a re-evaluation overwrites the
/// computed fields in place so the register always reflects the current verdict.
///
/// FAIL-CLOSED: every unknown (no policy, no trigger date, not yet evaluated) resolves to NOT eligible.
/// </summary>
public sealed class DocumentRetentionSubject : TenantScopedEntity
{
    public RetentionSubjectType SubjectType { get; set; } = RetentionSubjectType.Other;
    public required Guid SubjectId { get; set; }

    /// <summary>Governance linkage so a register-entry-scoped hold can reach this subject.</summary>
    public Guid? RegisterEntryId { get; set; }
    public Guid? ControlledDocumentId { get; set; }

    public Guid? PolicyId { get; set; }
    public string? PolicyKey { get; set; }
    public string? RetentionClass { get; set; }

    // ── Computed retention ───────────────────────────────────────────────────────────────────────────────
    public DateTimeOffset? RetentionTriggerDate { get; set; }
    public DateTimeOffset? RetentionDueDate { get; set; }
    public DateTimeOffset? DispositionEligibleAt { get; set; }
    public bool IsDispositionEligible { get; set; }

    public bool IsBlockedByLegalHold { get; set; }
    public List<Guid> ActiveLegalHoldIds { get; set; } = [];

    /// <summary>True when the policy retains the record permanently (e.g. the identifier allocation ledger).</summary>
    public bool IsPermanentRetention { get; set; }

    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public string? LastEvaluatedBy { get; set; }
    public RetentionEvaluationStatus EvaluationStatus { get; set; } = RetentionEvaluationStatus.NotEvaluated;

    /// <summary>Human-readable explanation of the verdict — the audit trail for why disposition is/isn't allowed.</summary>
    public string? EvaluationNote { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

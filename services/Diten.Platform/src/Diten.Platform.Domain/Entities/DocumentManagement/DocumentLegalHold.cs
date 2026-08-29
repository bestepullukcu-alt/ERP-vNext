using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU15 — a legal / litigation hold (GMG-QMS-SOP-0001 §22). An active hold STOPS ALL destruction and
/// disposition activity within its scope, regardless of any elapsed retention period.
///
/// SOP controls enforced by the FU15 service layer:
/// • Activation requires Legal approval evidence.
/// • RELEASE requires BOTH Legal written release approval AND GQD concurrence evidence — a single approval is
///   never sufficient.
/// • The hold record itself is protected against deletion and alteration: it is never hard-deleted, and released
///   holds retain their full issuance + release evidence trail (backdating / undocumented reconstruction is
///   forbidden, so every decision carries its own timestamp and evidence reference).
///
/// BOUNDARY: <see cref="LegalHoldScopeType.CustomQuery"/> is stored as a description only — FU15 does not execute
/// custom scope queries, and an unevaluated scope never blocks silently.
/// </summary>
public sealed class DocumentLegalHold : TenantScopedEntity
{
    public required string HoldKey { get; set; }
    public required string HoldTitle { get; set; }
    public LegalHoldStatus HoldStatus { get; set; } = LegalHoldStatus.Draft;
    public LegalHoldReason HoldReason { get; set; } = LegalHoldReason.Litigation;

    // ── Scope ────────────────────────────────────────────────────────────────────────────────────────────
    public LegalHoldScopeType ScopeType { get; set; } = LegalHoldScopeType.RegisterEntry;
    public List<Guid> RegisterEntryIds { get; set; } = [];
    public List<Guid> ControlledDocumentIds { get; set; } = [];
    public List<RetentionSubjectType> SubjectTypes { get; set; } = [];
    public List<Guid> ExternalDocumentIds { get; set; } = [];
    public string? ScopeDescription { get; set; }

    // ── Issuance: Legal approval (SOP §22) ───────────────────────────────────────────────────────────────
    public Guid? IssuedByLegalUserId { get; set; }
    public string? IssuedByLegalRole { get; set; }
    public DateTimeOffset? IssuedAt { get; set; }

    /// <summary>Mandatory to activate a hold. A reference — never the legal document bytes.</summary>
    public string? LegalApprovalEvidenceReference { get; set; }

    // ── GQD concurrence ──────────────────────────────────────────────────────────────────────────────────
    public Guid? GqdConcurrenceUserId { get; set; }
    public DateTimeOffset? GqdConcurrenceAt { get; set; }
    public string? GqdConcurrenceEvidenceReference { get; set; }

    public DateTimeOffset EffectiveFrom { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EffectiveUntil { get; set; }

    // ── Release: BOTH Legal approval AND GQD concurrence required (SOP §22) ──────────────────────────────
    public DateTimeOffset? ReleaseRequestedAt { get; set; }
    public string? ReleaseRequestedBy { get; set; }
    public string? ReleaseLegalApprovalReference { get; set; }
    public string? ReleaseGqdConcurrenceReference { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
    public string? ReleasedBy { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>
    /// A hold blocks disposition only while Active and within its effective window. Draft, Released and
    /// Cancelled holds never block.
    /// </summary>
    public bool IsActiveAt(DateTimeOffset at) =>
        HoldStatus == LegalHoldStatus.Active
        && at >= EffectiveFrom
        && (EffectiveUntil is null || at <= EffectiveUntil);
}

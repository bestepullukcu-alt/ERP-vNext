using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU06 — Document Master Register entry (GMG-QMS-SOP-0001 §18 LOG-0001, §20). A tenant-scoped, regulated
/// governance projection that carries the controlled document's identity and lifecycle DECISIONS — deliberately
/// separate from the FU01 <see cref="ControlledDocument"/> (a file/version aggregate). One register row may exist
/// BEFORE any document file exists (SOP §9.1–9.3: request → UID/code allocation precede drafting), so the links to
/// <see cref="ControlledDocumentId"/> / template ids are all nullable and never 1:1-enforced.
///
/// FOUNDATION SCOPE: PermanentUid/DocumentCode are nullable here (FU07 allocation engine sets them). The release-gate
/// and approval-evidence fields are EXTENSION POINTS only — FU06 stores/reads them but computes nothing (FU10 engine
/// populates them). Protected fields (UID, code, lifecycle status, effective date, current version label, gate
/// result, approval evidence) are never mutated by the metadata-update path; only dedicated later services set them.
/// No hard delete: archival is a status change.
/// </summary>
public sealed class DocumentMasterRegisterEntry : TenantScopedEntity
{
    // ── Linkage (all nullable; a register row can precede the document file) ─────────────────────────────
    public Guid? ControlledDocumentId { get; set; }
    public Guid? TemplateDocumentId { get; set; }
    public Guid? TemplateMasterId { get; set; }
    public Guid? TemplateVariantId { get; set; }
    public DocumentScope DocumentScope { get; set; } = DocumentScope.Company;
    public Guid ScopeOwnerId { get; set; }
    public Guid CorporateOwnerId { get; set; }
    public Guid CollectionInstanceId { get; set; }
    public Guid FolderId { get; set; }
    public DocumentLinkScopeCompatibilityStatus LinkScopeCompatibilityStatus { get; set; }
        = DocumentLinkScopeCompatibilityStatus.Unvalidated;
    public DateTimeOffset? ControlledDocumentLinkedAt { get; set; }
    public string? ControlledDocumentLinkedBy { get; set; }
    public string? ControlledDocumentLinkReason { get; set; }

    // ── Identity (PROTECTED — allocation is FU07; nullable until allocated) ──────────────────────────────
    public string? PermanentUid { get; set; }
    public string? DocumentCode { get; set; }
    public string? LegacyCode { get; set; }

    /// <summary>Provenance of UID/code: false = manually entered in this FU; true = allocated by the FU07 engine.</summary>
    public bool IsSystemAllocated { get; set; }

    // ── Descriptive metadata ────────────────────────────────────────────────────────────────────────────
    public required string DocumentTitle { get; set; }
    public DocumentType DocumentType { get; set; } = DocumentType.Other;
    public ControlledDocumentClass DocumentClass { get; set; } = ControlledDocumentClass.Other;
    public DocumentCriticality Criticality { get; set; } = DocumentCriticality.Minor;

    // ── Ownership (SOP §5 roles; FU06 records the role string, not an RBAC binding) ──────────────────────
    public string? ProcessOwnerRole { get; set; }
    public Guid? ProcessOwnerUserId { get; set; }
    public string? OwnerFunction { get; set; }
    public Guid? OwnerCompanyId { get; set; }
    public string? GoverningLanguage { get; set; }

    // ── Approved repository pointer (SOP §11; assessment entity is FU16) ─────────────────────────────────
    public string? ApprovedRepositoryId { get; set; }
    public string? ApprovedRepositoryName { get; set; }
    public string? ApprovedRepositoryPath { get; set; }

    // ── Version / status (PROTECTED — lifecycle engine is FU08) ──────────────────────────────────────────
    public string? CurrentVersionLabel { get; set; }
    public int? CurrentVersionNumber { get; set; }
    public ControlledDocumentLifecycleStatus LifecycleStatus { get; set; } = ControlledDocumentLifecycleStatus.Draft;

    // ── Dates & review (periodic-review engine is FU12; FU06 stores the metadata) ────────────────────────
    public DateTimeOffset? EffectiveDate { get; set; }
    public int? ReviewCycleMonths { get; set; }
    public DateTimeOffset? NextReviewDueDate { get; set; }
    public DateTimeOffset? LastPeriodicReviewDate { get; set; }
    public string? RetentionClass { get; set; }

    // ── Object nature flags (SOP §2 boundary) ────────────────────────────────────────────────────────────
    public bool IsControlledDocument { get; set; } = true;
    public bool IsRecord { get; set; }
    public bool IsExternalDocument { get; set; }
    public bool IsTemplate { get; set; }
    public bool IsVariant { get; set; }

    // ── Parent lineage (SOP §13.2 variants/translations) ─────────────────────────────────────────────────
    public string? ParentDocumentUid { get; set; }
    public string? ParentDocumentCode { get; set; }
    public string? ParentVersionLabel { get; set; }

    // ── Migration provenance (SOP §12.3 legacy migration) ────────────────────────────────────────────────
    public string? SourceSystem { get; set; }
    public string? SourceLegacyId { get; set; }

    // ── Register row's own governance status ─────────────────────────────────────────────────────────────
    public DocumentRegisterStatus RegisterStatus { get; set; } = DocumentRegisterStatus.Draft;

    // ── Release-gate EXTENSION POINT (SOP §19/§21; engine is FU10 — FU06 stores only, computes nothing) ──
    public string? LastReleaseGateEvaluationStatus { get; set; }
    public DateTimeOffset? LastReleaseGateEvaluationAt { get; set; }
    public int? LastReleaseGateBlockingCount { get; set; }
    public int? LastReleaseGateWarningCount { get; set; }

    // ── Approval-evidence EXTENSION POINT (SOP §7.2/§9.9). FU06 stores only; MOD-0029-FU09 computes this from the
    //    approval requirements + segregation. Values: NotRequired/Pending/Complete/Rejected/Blocked/SegregationFailed. ─
    public string? ApprovalEvidenceStatus { get; set; }

    // ── MOD-0029-FU09 — approval identity + impact flags (all additive). Author/requester drive segregation (SOP
    //    §5.1); impact flags drive the approval overlay route (SOP §7.2). Populated manually or by a future impact
    //    assessment FU; the route resolver also accepts them as request input. ───────────────────────────────────
    public Guid? AuthorUserId { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public Guid? PreparedByUserId { get; set; }

    public bool HasRaImpact { get; set; }
    public bool HasPvImpact { get; set; }
    public bool HasBatchReleaseImpact { get; set; }
    public bool HasDmsCsvImpact { get; set; }
    public bool HasQualityAgreementImpact { get; set; }
    public bool IsGroupGovernance { get; set; }
    public bool RequiresLegalReview { get; set; }
    public bool RequiresCeoEndorsement { get; set; }
    public bool RequiresIndependentTechnicalReview { get; set; }

    // ── MOD-0029-FU08 — controlled document lifecycle (SOP §6.2). All additive; LifecycleStatus (declared above)
    //    is now driven by the FU08 transition engine, never by the FU06 metadata-update path. ─────────────────
    /// <summary>Free-text summary of the reason for the last lifecycle transition (full history is the ledger).</summary>
    public string? StatusReason { get; set; }
    public DateTimeOffset? LastTransitionAt { get; set; }
    public string? LastTransitionBy { get; set; }

    /// <summary>FU10 EXTENSION POINT: when true, MarkEffective must have a passing release-gate evaluation. Default off.</summary>
    public bool RequiresReleaseGateEvaluation { get; set; }

    /// <summary>Supersession linkage (SOP §6.2): the entry that superseded this one when it was replaced.</summary>
    public Guid? SupersededByRegisterEntryId { get; set; }
    /// <summary>Supersession linkage: the entry this one superseded when it became Effective.</summary>
    public Guid? SupersedesRegisterEntryId { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

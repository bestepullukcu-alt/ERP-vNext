using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU18 — the translation / site-adoption governance profile for a <see cref="TemplateVariant"/>
/// (GMG-QMS-SOP-0001 §13.2).
///
/// DELIBERATELY A SIDECAR, not fields on TemplateVariant: FU03/FU03A/FU03B create, rebase and compare payloads and
/// their tests must keep working untouched, and a variant that never needs localization governance should carry no
/// localization state at all. A variant with no profile behaves exactly as it did before FU18.
///
/// WHAT A VARIANT IS (SOP §13.2): a translation or a site-adopted copy of a parent master. It keeps its parent
/// linkage (uid/code/version) permanently, and carries its own variant identifier, language, status and local
/// effective date. It is NOT a local document — a local document is a separate controlled document with its OWN
/// UID/code allocated by FU07 and its own FU08 lifecycle, which merely references the parent. Converting a variant
/// into a local document is out of scope here; this profile can only RECORD such a reference.
///
/// No content is stored or compared — this module implements no translation management and no binary diff.
/// Nothing here is ever hard-deleted.
/// </summary>
public sealed class TemplateVariantLocalizationProfile : TenantScopedEntity
{
    public required Guid TemplateVariantId { get; set; }

    /// <summary>The variant's own stable identifier within its parent lineage (SOP §13.2 unique variant id).</summary>
    public string? VariantIdentifier { get; set; }

    // ── Language / territory ─────────────────────────────────────────────────────────────────────────────
    public string? VariantLanguageCode { get; set; }
    public string? VariantLanguageName { get; set; }
    public string? SourceLanguageCode { get; set; }
    public string? CountryCode { get; set; }
    public string? SiteCode { get; set; }

    // ── Classification (SOP §13.2: translation vs site adoption) ─────────────────────────────────────────
    public bool IsTranslationVariant { get; set; }
    public bool IsSiteAdoptedVariant { get; set; }

    /// <summary>
    /// When true, the site may not rely on the English/source master: the master revision is not locally
    /// effective until the local-language variant is ready (SOP §13.2).
    /// </summary>
    public bool IsLocalLanguageMandatory { get; set; }

    // ── Parent linkage — must never be lost (SOP §13.2) ──────────────────────────────────────────────────
    public Guid? ParentTemplateMasterId { get; set; }
    public Guid? ParentTemplateMasterVersionId { get; set; }
    public Guid? ParentRegisterEntryId { get; set; }
    public string? ParentDocumentUid { get; set; }
    public string? ParentDocumentCode { get; set; }
    public string? ParentVersionLabel { get; set; }

    /// <summary>
    /// Reference to a separately-registered LOCAL DOCUMENT derived from this variant, if one exists. A pointer
    /// only — FU18 does not create local documents, allocate their UID/code or drive their lifecycle.
    /// </summary>
    public Guid? LocalDocumentRegisterEntryId { get; set; }

    // ── Bilingual review (SOP §13.2) ─────────────────────────────────────────────────────────────────────
    public bool RequiresBilingualReview { get; set; }
    public Guid? BilingualReviewerUserId { get; set; }
    public string? BilingualReviewerRole { get; set; }
    public BilingualReviewStatus BilingualReviewStatus { get; set; } = BilingualReviewStatus.NotRequired;
    public string? BilingualReviewEvidenceReference { get; set; }
    public DateTimeOffset? BilingualReviewCompletedAt { get; set; }

    // ── Local approval (SOP §13.2) ───────────────────────────────────────────────────────────────────────
    public bool RequiresLocalApproval { get; set; }
    public Guid? LocalApproverUserId { get; set; }
    public string? LocalApproverRole { get; set; }
    public LocalApprovalStatus LocalApprovalStatus { get; set; } = LocalApprovalStatus.NotRequired;
    public string? LocalApprovalEvidenceReference { get; set; }
    public DateTimeOffset? LocalApprovalCompletedAt { get; set; }

    /// <summary>The variant author, used for the bilingual-review segregation check (author ≠ sole reviewer).</summary>
    public Guid? AuthorUserId { get; set; }

    // ── Effective dates ──────────────────────────────────────────────────────────────────────────────────
    public DateTimeOffset? LocalEffectiveDate { get; set; }
    public DateTimeOffset? ParentEffectiveDateAtLastAssessment { get; set; }

    // ── Computed governance state (persisted so the register reflects the last verdict) ──────────────────
    public TranslationReadinessStatus TranslationReadinessStatus { get; set; } = TranslationReadinessStatus.NotRequired;
    public LocalAdoptionStatus LocalAdoptionStatus { get; set; } = LocalAdoptionStatus.NotRequired;
    public ParentChangeStatus ParentChangeStatus { get; set; } = ParentChangeStatus.InSync;
    public DateTimeOffset? LastParentAssessmentAt { get; set; }

    // ── Temporary English master allowance (SOP §13.2 — conditional use only) ────────────────────────────
    public bool TemporaryEnglishMasterAllowed { get; set; }
    public string? TemporaryEnglishMasterJustification { get; set; }
    public DateTimeOffset? TemporaryEnglishMasterExpiresAt { get; set; }
    public string? TemporaryEnglishMasterApprovedBy { get; set; }
    public string? TemporaryEnglishMasterEvidenceReference { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>
    /// SOP §13.2 — the temporary English master may only be relied upon while the allowance is approved,
    /// evidenced and unexpired. An expired allowance is the same as no allowance.
    /// </summary>
    public bool HasValidTemporaryEnglishAllowance(DateTimeOffset at) =>
        TemporaryEnglishMasterAllowed
        && !string.IsNullOrWhiteSpace(TemporaryEnglishMasterEvidenceReference)
        && !string.IsNullOrWhiteSpace(TemporaryEnglishMasterApprovedBy)
        && TemporaryEnglishMasterExpiresAt is { } expiry
        && at <= expiry;
}

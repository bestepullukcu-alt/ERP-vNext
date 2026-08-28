using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementVariantLocalization;

// MOD-0029-FU18 — variant / translation governance contracts, reason codes and wire mapping (SOP §13.2).

/// <summary>
/// MOD-0029-FU18 — RECOMMENDED Layer 1 RBAC keys. NOT seeded in this FU (no AuthService change): the controller
/// reuses the already-seeded controlled-documents view/create keys. A later hardening FU should seed these, since
/// recording a bilingual review or a local approval is a distinct competency from creating a template.
/// </summary>
public static class VariantLocalizationPermissions
{
    public const string View = "platform.document-management.template-variants.localization.view";
    public const string Manage = "platform.document-management.template-variants.localization.manage";
    public const string TranslationReviewRecord = "platform.document-management.template-variants.translation-review.record";
    public const string LocalApprovalRecord = "platform.document-management.template-variants.local-approval.record";
}

public static class VariantLocalizationReasonCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string NotFoundNonLeakage = "NOT_FOUND_NON_LEAKAGE";
    public const string VariantNotFound = "TEMPLATE_VARIANT_NOT_FOUND";
    public const string ProfileNotFound = "VARIANT_LOCALIZATION_PROFILE_NOT_FOUND";
    public const string LanguageRequired = "VARIANT_LANGUAGE_CODE_REQUIRED";
    public const string CountryOrSiteRequired = "VARIANT_COUNTRY_OR_SITE_REQUIRED";
    public const string ParentLinkageRequired = "VARIANT_PARENT_LINKAGE_REQUIRED";
    public const string ReviewerRequired = "BILINGUAL_REVIEWER_REQUIRED";
    public const string ReviewEvidenceRequired = "BILINGUAL_REVIEW_EVIDENCE_REQUIRED";
    public const string ReviewerCannotBeAuthor = "BILINGUAL_REVIEWER_CANNOT_BE_SOLE_AUTHOR";
    public const string ApproverRequired = "LOCAL_APPROVER_REQUIRED";
    public const string ApprovalEvidenceRequired = "LOCAL_APPROVAL_EVIDENCE_REQUIRED";
    public const string LocalEffectiveBeforeParent = "LOCAL_EFFECTIVE_DATE_BEFORE_PARENT_EFFECTIVE";
    public const string LocalEffectiveNotReady = "LOCAL_EFFECTIVE_REQUIRES_COMPLETED_REVIEW_AND_APPROVAL";
    public const string TemporaryEnglishEvidenceRequired = "TEMPORARY_ENGLISH_MASTER_EVIDENCE_REQUIRED";
    public const string TemporaryEnglishApproverRequired = "TEMPORARY_ENGLISH_MASTER_APPROVER_REQUIRED";
    public const string TemporaryEnglishExpiryRequired = "TEMPORARY_ENGLISH_MASTER_EXPIRY_REQUIRED";
    public const string ReasonRequired = "REASON_REQUIRED";
    public const string PermissionDenied = "PERMISSION_DENIED";
}

// ── inputs ───────────────────────────────────────────────────────────────────

public sealed record VariantLocalizationProfileInput(
    string? VariantIdentifier,
    string? VariantLanguageCode,
    string? VariantLanguageName,
    string? SourceLanguageCode,
    string? CountryCode,
    string? SiteCode,
    bool IsTranslationVariant,
    bool IsSiteAdoptedVariant,
    bool IsLocalLanguageMandatory,
    Guid? ParentTemplateMasterId,
    Guid? ParentTemplateMasterVersionId,
    Guid? ParentRegisterEntryId,
    string? ParentDocumentUid,
    string? ParentDocumentCode,
    string? ParentVersionLabel,
    Guid? LocalDocumentRegisterEntryId,
    Guid? AuthorUserId,
    Guid? BilingualReviewerUserId,
    string? BilingualReviewerRole,
    Guid? LocalApproverUserId,
    string? LocalApproverRole,
    DateTimeOffset? LocalEffectiveDate);

public sealed record RecordBilingualReviewInput(
    Guid? ReviewerUserId,
    string? ReviewerRole,
    string EvidenceReference,
    string? Comment);

public sealed record RecordLocalApprovalInput(
    Guid? ApproverUserId,
    string? ApproverRole,
    string EvidenceReference,
    string? Comment);

public sealed record RejectVariantReviewInput(string Reason, string? EvidenceReference);

public sealed record AllowTemporaryEnglishMasterInput(
    string Justification,
    string ApprovedBy,
    DateTimeOffset ExpiresAt,
    string EvidenceReference);

// ── output models ────────────────────────────────────────────────────────────

public sealed record VariantLocalizationProfileModel(
    Guid Id,
    Guid TemplateVariantId,
    string? VariantIdentifier,
    string? VariantLanguageCode,
    string? VariantLanguageName,
    string? SourceLanguageCode,
    string? CountryCode,
    string? SiteCode,
    bool IsTranslationVariant,
    bool IsSiteAdoptedVariant,
    bool IsLocalLanguageMandatory,
    Guid? ParentTemplateMasterId,
    Guid? ParentTemplateMasterVersionId,
    Guid? ParentRegisterEntryId,
    string? ParentDocumentUid,
    string? ParentDocumentCode,
    string? ParentVersionLabel,
    Guid? LocalDocumentRegisterEntryId,
    bool RequiresBilingualReview,
    Guid? BilingualReviewerUserId,
    string? BilingualReviewerRole,
    string BilingualReviewStatus,
    string? BilingualReviewEvidenceReference,
    DateTimeOffset? BilingualReviewCompletedAt,
    bool RequiresLocalApproval,
    Guid? LocalApproverUserId,
    string? LocalApproverRole,
    string LocalApprovalStatus,
    string? LocalApprovalEvidenceReference,
    DateTimeOffset? LocalApprovalCompletedAt,
    DateTimeOffset? LocalEffectiveDate,
    DateTimeOffset? ParentEffectiveDateAtLastAssessment,
    string TranslationReadinessStatus,
    string LocalAdoptionStatus,
    string ParentChangeStatus,
    DateTimeOffset? LastParentAssessmentAt,
    bool TemporaryEnglishMasterAllowed,
    string? TemporaryEnglishMasterJustification,
    DateTimeOffset? TemporaryEnglishMasterExpiresAt,
    string? TemporaryEnglishMasterApprovedBy,
    string? TemporaryEnglishMasterEvidenceReference,
    string BoundaryStatement);

public sealed record VariantReviewEvidenceModel(
    Guid Id,
    Guid TemplateVariantId,
    string EvidenceType,
    string Status,
    Guid? PerformedByUserId,
    string? PerformedByRole,
    DateTimeOffset? PerformedAt,
    string EvidenceReference,
    string? Comment);

public sealed record VariantParentChangeAssessmentModel(
    Guid Id,
    Guid TemplateVariantId,
    Guid? ParentTemplateMasterId,
    Guid? ParentTemplateMasterVersionId,
    string? ParentDocumentUid,
    string? ParentDocumentCode,
    string ObservedParentStatus,
    string? ObservedParentVersionLabel,
    int? ObservedParentVersionNumber,
    DateTimeOffset? ObservedParentEffectiveDate,
    string AssessmentStatus,
    bool RequiresVariantRevision,
    bool RequiresBilingualReview,
    bool RequiresLocalApproval,
    bool RequiresSuspension,
    string? AssessmentEvidenceReference,
    string? AssessmentNote,
    DateTimeOffset AssessedAt,
    string? AssessedBy);

/// <summary>
/// MOD-0029-FU18 — the computed readiness verdict for a variant. <c>LocalUseAllowed</c> is the SOP question that
/// matters at the point of use: may this site rely on this variant right now?
/// </summary>
public sealed record VariantReadinessModel(
    Guid TemplateVariantId,
    bool TranslationReady,
    bool LocalApprovalReady,
    bool ParentCurrent,
    bool LocalUseAllowed,
    string TranslationReadinessStatus,
    string LocalAdoptionStatus,
    string ParentChangeStatus,
    bool TemporaryEnglishMasterActive,
    DateTimeOffset? TemporaryEnglishMasterExpiresAt,
    IReadOnlyList<string> BlockingReasons,
    IReadOnlyList<string> WarningReasons,
    string BoundaryStatement);

public static class VariantLocalizationWire
{
    /// <summary>Stated on every profile/readiness read so the module's limits are never misread.</summary>
    public const string BoundaryStatement =
        "Variant translation/site-adoption governance: metadata and evidence only. MOD-0029-FU18 performs no " +
        "content or binary comparison, no machine translation, and never overwrites variant content or " +
        "auto-transitions the parent document.";

    public static VariantLocalizationProfileModel ToProfile(TemplateVariantLocalizationProfile p) => new(
        p.Id, p.TemplateVariantId, p.VariantIdentifier, p.VariantLanguageCode, p.VariantLanguageName,
        p.SourceLanguageCode, p.CountryCode, p.SiteCode, p.IsTranslationVariant, p.IsSiteAdoptedVariant,
        p.IsLocalLanguageMandatory, p.ParentTemplateMasterId, p.ParentTemplateMasterVersionId, p.ParentRegisterEntryId,
        p.ParentDocumentUid, p.ParentDocumentCode, p.ParentVersionLabel, p.LocalDocumentRegisterEntryId,
        p.RequiresBilingualReview, p.BilingualReviewerUserId, p.BilingualReviewerRole,
        p.BilingualReviewStatus.ToString(), p.BilingualReviewEvidenceReference, p.BilingualReviewCompletedAt,
        p.RequiresLocalApproval, p.LocalApproverUserId, p.LocalApproverRole, p.LocalApprovalStatus.ToString(),
        p.LocalApprovalEvidenceReference, p.LocalApprovalCompletedAt, p.LocalEffectiveDate,
        p.ParentEffectiveDateAtLastAssessment, p.TranslationReadinessStatus.ToString(),
        p.LocalAdoptionStatus.ToString(), p.ParentChangeStatus.ToString(), p.LastParentAssessmentAt,
        p.TemporaryEnglishMasterAllowed, p.TemporaryEnglishMasterJustification, p.TemporaryEnglishMasterExpiresAt,
        p.TemporaryEnglishMasterApprovedBy, p.TemporaryEnglishMasterEvidenceReference, BoundaryStatement);

    public static VariantReviewEvidenceModel ToEvidence(TemplateVariantReviewEvidence e) => new(
        e.Id, e.TemplateVariantId, e.EvidenceType.ToString(), e.Status.ToString(), e.PerformedByUserId,
        e.PerformedByRole, e.PerformedAt, e.EvidenceReference, e.Comment);

    public static VariantParentChangeAssessmentModel ToAssessment(TemplateVariantParentChangeAssessment a) => new(
        a.Id, a.TemplateVariantId, a.ParentTemplateMasterId, a.ParentTemplateMasterVersionId, a.ParentDocumentUid,
        a.ParentDocumentCode, a.ObservedParentStatus.ToString(), a.ObservedParentVersionLabel,
        a.ObservedParentVersionNumber, a.ObservedParentEffectiveDate, a.AssessmentStatus.ToString(),
        a.RequiresVariantRevision, a.RequiresBilingualReview, a.RequiresLocalApproval, a.RequiresSuspension,
        a.AssessmentEvidenceReference, a.AssessmentNote, a.AssessedAt, a.AssessedBy);
}

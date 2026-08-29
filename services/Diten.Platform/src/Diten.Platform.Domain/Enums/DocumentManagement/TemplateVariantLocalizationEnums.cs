namespace Diten.Platform.Domain.Enums.DocumentManagement;

// MOD-0029-FU18 — Variant / Translation Hardening (GMG-QMS-SOP-0001 §13.2) enums. Kept in a dedicated file so FU18
// ownership never edits the FU03-owned TemplateVariantStatus / DriftStatus / ContentSource surfaces.
//
// SCOPE BOUNDARY: this is BUSINESS DOCUMENT variant governance — translations and site-adopted copies of controlled
// documents. It is NOT the application's UI localization/resource system (.resx), and it implements no translation
// management, no machine translation and no content/binary diff. Everything here is metadata and evidence.
//
// RELATIONSHIP TO FU03 DRIFT: TemplateVariantDriftStatus answers "is the variant's content lineage behind the
// master version?". The FU18 ParentChangeStatus below answers the SOP question "what must a human DO about the
// parent having changed?". They are complementary and deliberately separate — FU18 never rewrites drift.

/// <summary>
/// MOD-0029-FU18 — bilingual review progress (SOP §13.2). A translation must be verified against its source
/// language by a competent bilingual reviewer before the translation can be relied upon.
/// </summary>
public enum BilingualReviewStatus
{
    NotRequired = 0,
    Pending = 1,
    Completed = 2,
    Rejected = 3,
    Blocked = 4
}

/// <summary>MOD-0029-FU18 — local (site/country) approval progress for an adopted variant (SOP §13.2).</summary>
public enum LocalApprovalStatus
{
    NotRequired = 0,
    Pending = 1,
    Completed = 2,
    Rejected = 3,
    Blocked = 4
}

/// <summary>
/// MOD-0029-FU18 — whether the translation itself is fit to rely on. <see cref="Outdated"/> is the SOP-critical
/// state: the parent moved on, so a previously-ready translation no longer represents the current master.
/// </summary>
public enum TranslationReadinessStatus
{
    NotRequired = 0,
    Pending = 1,
    Ready = 2,
    Blocked = 3,
    Outdated = 4
}

/// <summary>MOD-0029-FU18 — whether the site has adopted the variant for local use (SOP §13.2).</summary>
public enum LocalAdoptionStatus
{
    NotRequired = 0,
    Pending = 1,
    Ready = 2,
    Blocked = 3,
    Effective = 4,
    Suspended = 5
}

/// <summary>
/// MOD-0029-FU18 — the variant's standing relative to its parent master, as of the last assessment. This is the
/// SOP linkage that must never be lost: a variant always knows whether its parent has moved, been superseded,
/// retired or suspended.
/// </summary>
public enum ParentChangeStatus
{
    InSync = 0,
    ParentUpdated = 1,
    ParentSuperseded = 2,
    ParentRetired = 3,
    ParentSuspended = 4
}

/// <summary>
/// MOD-0029-FU18 — the parent state observed at assessment time. Sourced from the TemplateMaster / master version
/// and, when the variant is linked to a Document Master Register entry, from that entry's FU08 lifecycle status.
/// </summary>
public enum ObservedParentStatus
{
    Effective = 0,
    Superseded = 1,
    Retired = 2,
    Suspended = 3,
    Deprecated = 4,
    Archived = 5,
    Unknown = 6
}

/// <summary>MOD-0029-FU18 — what the parent change assessment concluded must happen next.</summary>
public enum ParentChangeAssessmentStatus
{
    InSync = 0,
    RebaseRequired = 1,
    TranslationUpdateRequired = 2,
    LocalApprovalRequired = 3,
    SuspensionRequired = 4,
    Blocked = 5
}

/// <summary>MOD-0029-FU18 — the kind of governance evidence recorded against a variant. Append-only.</summary>
public enum VariantReviewEvidenceType
{
    BilingualReview = 0,
    LocalApproval = 1,
    TranslationVerification = 2,
    LocalAdoption = 3,
    ParentChangeAssessment = 4,
    TemporaryEnglishMasterAllowance = 5
}

/// <summary>MOD-0029-FU18 — evidence record state. Rejection is recorded, never erased.</summary>
public enum VariantReviewEvidenceStatus
{
    Pending = 0,
    Completed = 1,
    Rejected = 2,
    Blocked = 3
}

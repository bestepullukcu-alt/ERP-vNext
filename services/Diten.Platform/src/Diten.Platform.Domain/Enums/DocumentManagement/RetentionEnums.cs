namespace Diten.Platform.Domain.Enums.DocumentManagement;

// MOD-0029-FU15 — Retention Schedule & Litigation Hold (GMG-QMS-SOP-0001 §22) enums. Kept in a dedicated file so
// FU15 ownership never edits the FU06 MasterRegisterEnums.cs or any other FU's enum surface.
//
// SCOPE BOUNDARY: FU15 is a retention POLICY + HOLD + DISPOSITION foundation, NOT a destruction engine. Nothing in
// this feature deletes anything. The vocabulary below models "how long must this be kept", "is it past due",
// "is it under legal hold" and "has a disposition been requested" — the actual purge remains a future task.
//
// NOTE ON NAMING: the platform already has an unrelated Entities.Audit.AuditEventRetentionPolicy (a GLOBAL,
// plan-tier, day-based policy for the audit EVENT STORE). FU15's DocumentRetentionPolicy is tenant-scoped,
// year-based and governs regulated DOCUMENT evidence. They are deliberately separate concerns.

/// <summary>
/// MOD-0029-FU15 — the kind of regulated record a retention policy or hold applies to. Covers every governance
/// evidence aggregate produced by FU06–FU17 so that no regulated record is left without a retention subject.
/// </summary>
public enum RetentionSubjectType
{
    ControlledDocument = 0,
    ControlledDocumentVersion = 1,
    DocumentMasterRegisterEntry = 2,
    IdentifierAllocationLedger = 3,
    LifecycleTransitionRecord = 4,
    ApprovalRequirement = 5,
    ApprovalEvidence = 6,
    ReleaseGateEvaluation = 7,
    ReleaseGateResult = 8,
    ReleaseGateEvidence = 9,
    TrainingRequirement = 10,
    TrainingAssignment = 11,
    PeriodicReview = 12,
    PeriodicReviewExtension = 13,
    PeriodicReviewEscalation = 14,
    SuspensionCase = 15,
    RetirementCase = 16,
    TemporaryInstructionControl = 17,
    RepositoryAssessment = 18,
    RepositoryAssessmentFinding = 19,
    ControlledCopy = 20,
    CopyWithdrawalPlan = 21,
    ObsoleteCopyFinding = 22,
    ExternalDocumentRegisterEntry = 23,
    ExternalDocumentMonitoringCheck = 24,
    ExternalDocumentImpactAssessment = 25,
    ExternalDocumentInternalLink = 26,
    Other = 27,

    // MOD-0029-FU18 — variant / translation governance evidence. APPENDED after Other so no existing persisted
    // ordinal shifts; Other deliberately keeps 27.
    TemplateVariantLocalizationProfile = 28,
    TemplateVariantReviewEvidence = 29,
    TemplateVariantParentChangeAssessment = 30,

    // MOD-0029-FU20 — repository downtime / temporary controlled issue evidence. APPENDED; no existing ordinal moves.
    RepositoryDowntimeEvent = 31,
    TemporaryControlledIssue = 32,
    DowntimeEscalation = 33,

    // MOD-0029-FU21 — GDocP correction trail evidence. APPENDED; no existing ordinal moves.
    GDocPCorrectionRecord = 34,
    GDocPCorrectionPolicy = 35,
    GDocPCorrectionReview = 36,

    // MOD-0029-FU22 — quality event / deviation / CAPA bridge evidence. APPENDED; no existing ordinal moves.
    DocumentQualityEvent = 37,
    DocumentDeviation = 38,
    DocumentCAPAAction = 39,
    DocumentQualityEventSourceLink = 40,

    // MOD-0029-FU23 — electronic signature foundation evidence. APPENDED; no existing ordinal moves.
    DocumentSignaturePolicy = 41,
    DocumentSignatureRequest = 42,
    DocumentSignatureRecord = 43,
    DocumentSignedObjectFingerprint = 44
}

/// <summary>MOD-0029-FU15 — retention policy governance status. A retired policy stops applying to new evaluations.</summary>
public enum RetentionPolicyStatus
{
    Draft = 0,
    Active = 1,
    Retired = 2
}

/// <summary>
/// MOD-0029-FU15 — which date starts the retention clock (SOP §22). The retention period is measured FROM this
/// date, so a missing trigger date makes a subject permanently ineligible for disposition (fail-closed).
/// </summary>
public enum RetentionTrigger
{
    EffectiveDate = 0,
    RetirementDate = 1,
    SupersessionDate = 2,
    CompletionDate = 3,
    ClosureDate = 4,
    CreationDate = 5,
    LastUpdatedDate = 6,
    ExternalSupersessionDate = 7
}

/// <summary>MOD-0029-FU15 — the outcome of the last retention evaluation for a subject. Fail-closed by design.</summary>
public enum RetentionEvaluationStatus
{
    /// <summary>No evaluation has run yet. Never eligible.</summary>
    NotEvaluated = 0,

    /// <summary>Evaluated and still within its retention period (or permanently retained). Not eligible.</summary>
    Current = 1,

    /// <summary>Past its retention due date with no active hold. Eligible for a disposition REQUEST only.</summary>
    Eligible = 2,

    /// <summary>An active legal hold blocks disposition regardless of the retention due date.</summary>
    BlockedByHold = 3,

    /// <summary>No active policy matches this subject. Fail-closed: not eligible.</summary>
    MissingPolicy = 4,

    /// <summary>The retention clock cannot start because the trigger date is unknown. Fail-closed: not eligible.</summary>
    MissingTriggerDate = 5
}

/// <summary>MOD-0029-FU15 — legal / litigation hold governance status.</summary>
public enum LegalHoldStatus
{
    Draft = 0,
    Active = 1,
    Released = 2,
    Cancelled = 3
}

/// <summary>MOD-0029-FU15 — why a hold was issued (SOP §22 litigation hold).</summary>
public enum LegalHoldReason
{
    Litigation = 0,
    Investigation = 1,
    RegulatoryInquiry = 2,
    Audit = 3,
    DataIntegrityConcern = 4,
    Other = 5
}

/// <summary>
/// MOD-0029-FU15 — how broadly a hold reaches. <see cref="CustomQuery"/> is stored as a DESCRIPTION only —
/// FU15 deliberately does not execute custom scope queries.
/// </summary>
public enum LegalHoldScopeType
{
    RegisterEntry = 0,
    ControlledDocument = 1,
    SubjectType = 2,
    Repository = 3,
    ExternalDocument = 4,

    /// <summary>Blocks disposition for every document governance record in the tenant.</summary>
    GlobalDocumentControl = 5,

    /// <summary>Scope recorded as free text; not evaluated by FU15. Fail-closed — it never blocks silently.</summary>
    CustomQuery = 6
}

/// <summary>MOD-0029-FU15 — per-subject hold membership state. Released membership is retained as history.</summary>
public enum LegalHoldSubjectStatus
{
    Active = 0,
    Released = 1
}

/// <summary>
/// MOD-0029-FU15 — disposition request workflow state. <see cref="ExecutedAsNoDeleteMarker"/> is the terminal
/// state in this FU: it records that disposition was authorised WITHOUT deleting anything.
/// </summary>
public enum DispositionRequestStatus
{
    Draft = 0,
    Submitted = 1,
    BlockedByHold = 2,
    ApprovedForDisposition = 3,
    Rejected = 4,

    /// <summary>Disposition executed as an EVIDENCE MARKER only. The subject record still exists, untouched.</summary>
    ExecutedAsNoDeleteMarker = 5,
    Cancelled = 6
}

/// <summary>MOD-0029-FU15 — the eligibility verdict captured on a disposition request at check time.</summary>
public enum DispositionEligibilityResult
{
    Eligible = 0,
    NotEligible = 1,
    BlockedByHold = 2,
    MissingPolicy = 3,
    MissingTriggerDate = 4
}

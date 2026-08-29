namespace Diten.Platform.Domain.Enums.DocumentManagement;

// MOD-0029-FU21 — GDocP / ALCOA+ correction trail (GMG-QMS-SOP-0001 §21) enums. Kept in a dedicated file so FU21
// ownership never edits another FU's enum surface.
//
// WHY THIS IS NOT THE AUDIT LOG: the platform already has an AuditEvent store fed by AuditBehavior, which records
// WHICH COMMAND an actor ran against WHICH ENTITY. Its BeforeState/AfterState dictionaries exist but no document
// management command populates them. The GDocP requirement is different in kind: when a regulated FIELD of a
// regulated record is corrected, the previous value must remain legible and traceable alongside the new value,
// the REASON, the EVIDENCE, the corrector and a server-stamped time. FU21 adds that layer; it replaces nothing and
// rewrites no audit infrastructure.
//
// SCOPE BOUNDARY: FU21 RECORDS corrections. It does not itself mutate the corrected aggregate — the field change
// stays with the owning feature's update command, which may call IGDocPCorrectionRecorder to leave the trail.

/// <summary>
/// MOD-0029-FU21 — the kind of regulated record a correction was applied to. Covers the governance evidence
/// aggregates produced by FU06–FU20 so no regulated correction is left without a home.
/// </summary>
public enum GDocPSubjectType
{
    DocumentMasterRegisterEntry = 0,
    ControlledDocument = 1,
    ControlledDocumentVersion = 2,
    TemplateDocument = 3,
    TemplateMaster = 4,
    TemplateVariant = 5,
    TemplateVariantLocalizationProfile = 6,
    ApprovalEvidence = 7,
    ReleaseGateEvidence = 8,
    TrainingAssignment = 9,
    PeriodicReview = 10,
    SuspensionCase = 11,
    RetirementCase = 12,
    RepositoryAssessment = 13,
    ControlledCopy = 14,
    ExternalDocumentRegisterEntry = 15,
    ExternalDocumentImpactAssessment = 16,
    RetentionSubject = 17,
    LegalHold = 18,
    DispositionRequest = 19,
    DowntimeEvent = 20,
    TemporaryControlledIssue = 21,
    IdentifierAllocationLedger = 22,
    Other = 23
}

/// <summary>
/// MOD-0029-FU21 — how a value snapshot should be read. <see cref="Redacted"/> must be EXPLICIT: a value withheld
/// for confidentiality is marked as withheld, never silently blanked, because a blank previous value is
/// indistinguishable from a lost one.
/// </summary>
public enum GDocPValueFormat
{
    Text = 0,
    Number = 1,
    Boolean = 2,
    Date = 3,
    DateTime = 4,
    Enum = 5,
    Json = 6,
    Reference = 7,

    /// <summary>The value exists but is deliberately withheld. The snapshot carries an explicit redaction marker.</summary>
    Redacted = 8
}

/// <summary>
/// MOD-0029-FU21 — what kind of correction was made. The last four are inherently high risk: they can rewrite the
/// regulated meaning of a record rather than fix its legibility.
/// </summary>
public enum GDocPCorrectionType
{
    /// <summary>A typo or formatting fix that does not change meaning.</summary>
    TypographicalCorrection = 0,

    MetadataCorrection = 1,

    /// <summary>A date/time value correction. Moving a regulated timestamp EARLIER is backdating.</summary>
    DateCorrection = 2,

    /// <summary>Changing which evidence a regulated decision rests on. Always high risk.</summary>
    EvidenceReferenceCorrection = 3,

    /// <summary>Changing a lifecycle/approval/release status. Always high risk.</summary>
    StatusCorrection = 4,

    LinkCorrection = 5,

    /// <summary>Recreating a record or value that was lost. Never permitted silently (SOP §21).</summary>
    Reconstruction = 6,

    /// <summary>A correction raised out of a data-integrity concern. Always high risk.</summary>
    DataIntegrityCorrection = 7,

    Other = 8
}

/// <summary>MOD-0029-FU21 — second-person review state of a correction record.</summary>
public enum GDocPReviewStatus
{
    /// <summary>Policy does not require review. Deliberately distinct from Reviewed — nobody looked at it.</summary>
    NotRequired = 0,

    PendingReview = 1,
    Reviewed = 2,
    Rejected = 3
}

/// <summary>MOD-0029-FU21 — correction policy governance status.</summary>
public enum GDocPCorrectionPolicyStatus
{
    Draft = 0,
    Active = 1,
    Retired = 2
}

/// <summary>MOD-0029-FU21 — the reviewer's verdict on a correction.</summary>
public enum GDocPReviewDecision
{
    Approved = 0,
    Rejected = 1
}

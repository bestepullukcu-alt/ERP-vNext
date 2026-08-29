namespace Diten.Platform.Domain.Enums.DocumentManagement;

// MOD-0029-FU23 — electronic signature foundation enums (GMG-QMS-SOP-0001 §11, §11.2). Kept in a dedicated file so
// FU23 ownership never edits an earlier FU's enum surface.
//
// SCOPE BOUNDARY, STATED ONCE AND MEANT LITERALLY: this vocabulary models WHO attested WHAT, with WHICH MEANING,
// against WHICH exact object state. It is NOT a qualified electronic signature implementation. There is no external
// provider call, no certificate chain validation, no PAdES/XAdES artefact, and no 21 CFR Part 11 / Annex 11
// compliance claim anywhere in this feature. An interim repository's native approval capability is never presented
// as a validated DMS.

/// <summary>
/// MOD-0029-FU23 — the kind of regulated record a signature can be attached to. Deliberately mirrors the governance
/// aggregates FU06–FU22 already produce, so a signature always points at something that exists rather than at a
/// free-text reference.
/// </summary>
public enum SignableSubjectType
{
    ApprovalEvidence = 0,
    ReleaseGateEvidence = 1,
    TrainingAssignment = 2,
    TrainingEffectiveness = 3,
    GDocPCorrectionRecord = 4,
    GDocPCorrectionReview = 5,
    QualityEvent = 6,
    Deviation = 7,
    CAPAAction = 8,
    RepositoryAssessment = 9,
    LegalHold = 10,
    DispositionRequest = 11,
    ControlledCopyWithdrawal = 12,
    TemporaryControlledIssue = 13,
    ExternalImpactAssessment = 14,
    DocumentMasterRegisterEntry = 15,
    Other = 16
}

/// <summary>
/// MOD-0029-FU23 — WHAT THE SIGNATURE MEANS. SOP §11.2: a signature without a stated meaning is not a regulated
/// signature, it is a click. The meaning is mandatory on every signature record and is never inferred.
/// </summary>
public enum SignatureMeaning
{
    AuthorApproval = 0,
    ReviewerApproval = 1,
    QAGQDApproval = 2,
    LocalQAApproval = 3,
    TrainingAcknowledgement = 4,
    EffectivenessConfirmation = 5,
    ReleaseAuthorization = 6,
    GateVerification = 7,
    CorrectionReview = 8,
    DeviationClosureApproval = 9,
    CAPACompletionApproval = 10,
    CAPAEffectivenessApproval = 11,
    LegalHoldReleaseApproval = 12,
    DispositionApproval = 13,
    RepositoryAssessmentApproval = 14,
    Other = 15
}

/// <summary>MOD-0029-FU23 — signature policy governance status. A retired policy stops applying to new signatures.</summary>
public enum SignaturePolicyStatus
{
    Draft = 0,
    Active = 1,
    Retired = 2
}

/// <summary>MOD-0029-FU23 — signature request lifecycle. A signed request is terminal; it cannot be cancelled or rejected.</summary>
public enum SignatureRequestStatus
{
    Draft = 0,
    Pending = 1,
    Signed = 2,
    Rejected = 3,
    Cancelled = 4,
    Expired = 5
}

/// <summary>
/// MOD-0029-FU23 — HOW the attestation was made. Every one of these is a RECORD of an act, never a cryptographic
/// operation performed by this platform.
/// </summary>
public enum SignatureMethod
{
    /// <summary>An authenticated in-platform attestation. Explicitly NOT a qualified electronic signature.</summary>
    InternalAttestation = 0,

    /// <summary>A physical wet signature held elsewhere; the record carries the evidence reference, not the paper.</summary>
    WetSignatureEvidence = 1,

    /// <summary>Approval performed in a separate assessed mechanism (SOP §11 SeparateApprovalMechanism).</summary>
    SeparateApprovalMechanism = 2,

    /// <summary>A signature held in an external provider. FU23 stores the reference and calls no provider API.</summary>
    ExternalProviderReference = 3,

    /// <summary>A qualified e-signature held externally. FU23 never validates it and never claims it is validated.</summary>
    QualifiedElectronicSignatureReference = 4
}

/// <summary>
/// MOD-0029-FU23 — the current standing of a signature. A signature is never deleted when its object changes; it
/// moves to <see cref="RequiresResign"/> or <see cref="Invalidated"/> so the history stays legible.
/// </summary>
public enum SignatureStatus
{
    Valid = 0,
    Invalidated = 1,
    RequiresResign = 2,
    Rejected = 3,
    Revoked = 4
}

/// <summary>
/// MOD-0029-FU23 — whether anything actually validated this signature. <see cref="ValidatedByProvider"/> exists so a
/// future provider integration has a value to write; FU23 itself can never produce it.
/// </summary>
public enum SignatureValidationResult
{
    NotApplicable = 0,

    /// <summary>The honest default for every FU23 signature: recorded, not validated.</summary>
    NotValidated = 1,

    /// <summary>Reserved for a future provider integration. FU23 never sets this.</summary>
    ValidatedByProvider = 2,
    ValidationFailed = 3
}

/// <summary>
/// MOD-0029-FU23 — how the signed object's fingerprint was derived. Always over canonical METADATA, never over
/// document bytes: FU23 does not read, hash or store content.
/// </summary>
public enum SignatureFingerprintAlgorithm
{
    /// <summary>SHA-256 over a canonical, key-sorted JSON projection of the subject's governance metadata.</summary>
    CanonicalJsonSha256 = 0,
    MetadataSha256 = 1,

    /// <summary>The subject is only reachable as an external reference; no fingerprint could be computed.</summary>
    ExternalReferenceOnly = 2
}

/// <summary>
/// MOD-0029-FU23 — the verification verdict computed by recomputing the subject fingerprint and comparing it with
/// the one captured at signing time.
/// </summary>
public enum SignatureVerificationOutcome
{
    /// <summary>Fingerprint matches. The signature still describes the object it was applied to.</summary>
    FingerprintMatches = 0,

    /// <summary>The object changed after signing. The signature no longer attests to the current state.</summary>
    ObjectChanged = 1,

    /// <summary>The subject could not be resolved at verification time. Fail-closed — never reported as valid.</summary>
    SubjectUnresolvable = 2,

    /// <summary>The signature was already invalidated or revoked before this verification ran.</summary>
    AlreadyInvalidated = 3
}

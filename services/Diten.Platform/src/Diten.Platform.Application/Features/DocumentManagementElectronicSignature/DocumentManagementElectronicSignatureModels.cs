using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementElectronicSignature;

// MOD-0029-FU23 — electronic signature foundation contracts, reason codes and wire mapping.

/// <summary>
/// MOD-0029-FU23 — RECOMMENDED Layer 1 RBAC keys. NOT seeded in this FU (no AuthService change): the controller
/// reuses the already-seeded controlled-documents view/create keys. A later hardening FU should seed these —
/// signing on someone's behalf and invalidating an existing signature are materially different authorities from
/// viewing a signature history.
/// </summary>
public static class ElectronicSignaturePermissions
{
    public const string SignaturesView = "platform.document-management.signatures.view";
    public const string SignaturesRequest = "platform.document-management.signatures.request";
    public const string SignaturesSign = "platform.document-management.signatures.sign";
    public const string SignaturesVerify = "platform.document-management.signatures.verify";
    public const string SignaturesInvalidate = "platform.document-management.signatures.invalidate";
    public const string SignaturePoliciesManage = "platform.document-management.signature-policies.manage";
}

public static class ElectronicSignatureReasonCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string PolicyNotFound = "SIGNATURE_POLICY_NOT_FOUND";
    public const string RequestNotFound = "SIGNATURE_REQUEST_NOT_FOUND";
    public const string SignatureNotFound = "SIGNATURE_NOT_FOUND";

    // policy
    public const string PolicyKeyRequired = "SIGNATURE_POLICY_KEY_REQUIRED";
    public const string PolicyNameRequired = "SIGNATURE_POLICY_NAME_REQUIRED";
    public const string PolicyKeyDuplicate = "SIGNATURE_POLICY_KEY_ALREADY_EXISTS";
    public const string PolicyInvalidState = "SIGNATURE_POLICY_INVALID_STATE";

    // request
    public const string SubjectRequired = "SIGNATURE_SUBJECT_REQUIRED";
    public const string MeaningRequired = "SIGNATURE_MEANING_REQUIRED";
    public const string SignerRequired = "SIGNATURE_REQUEST_REQUIRES_SIGNER_USER_OR_ROLE";
    public const string DueDateInPast = "SIGNATURE_REQUEST_DUE_DATE_CANNOT_BE_IN_THE_PAST";
    public const string ReasonRequired = "REASON_REQUIRED";
    public const string RequestAlreadySigned = "SIGNATURE_REQUEST_ALREADY_SIGNED";
    public const string RequestInvalidState = "SIGNATURE_REQUEST_INVALID_STATE";
    public const string RejectionEvidenceRequired = "SIGNATURE_REJECTION_EVIDENCE_REQUIRED";
    public const string SignerNotNominated = "SIGNER_DOES_NOT_MATCH_REQUESTED_SIGNER";

    // sign
    public const string MeaningStatementRequired = "SIGNATURE_MEANING_STATEMENT_REQUIRED";
    public const string SignerIdentityRequired = "SIGNER_IDENTITY_REQUIRED";
    public const string SubjectNotResolvable = "SIGNATURE_SUBJECT_NOT_RESOLVABLE";
    public const string SubjectNotFound = "SIGNATURE_SUBJECT_NOT_FOUND";
    public const string RegisterEntryRequiredForSubject = "SIGNATURE_SUBJECT_REQUIRES_REGISTER_ENTRY_ID";
    public const string WetSignatureEvidenceRequired = "WET_SIGNATURE_REQUIRES_EVIDENCE_REFERENCE";
    public const string ExternalProviderReferenceRequired = "EXTERNAL_PROVIDER_METHOD_REQUIRES_PROVIDER_REFERENCE";
    public const string ReAuthenticationRequired = "RE_AUTHENTICATION_CONTEXT_REQUIRED_BY_POLICY";

    /// <summary>
    /// The policy demands a second factor and FU23 has no authentication context that can evidence one. Blocking is
    /// the only honest outcome: accepting a client-asserted flag would fabricate evidence.
    /// </summary>
    public const string SecondFactorNotAvailable = "SECOND_FACTOR_NOT_AVAILABLE";

    public const string RepositoryAssessmentRequired = "SIGNATURE_REQUIRES_APPROVED_REPOSITORY_ASSESSMENT";
    public const string RepositoryNotApproved = "UNAPPROVED_REPOSITORY_BLOCKS_REGULATED_SIGNATURE";
    public const string InterimRepositoryNotAllowed = "POLICY_DISALLOWS_INTERIM_REPOSITORY_SIGNATURE";
    public const string RepositoryTypeNotAllowed = "REPOSITORY_TYPE_NOT_PERMITTED_BY_POLICY";

    // verification / invalidation
    public const string SignatureAlreadyInvalidated = "SIGNATURE_ALREADY_INVALIDATED";
    public const string InvalidationReasonRequired = "SIGNATURE_INVALIDATION_REASON_REQUIRED";
}

// ── inputs ───────────────────────────────────────────────────────────────────

public sealed record CreateSignaturePolicyInput(
    string PolicyKey,
    string PolicyName,
    string? SignableSubjectType,
    string? SignatureMeaning,
    bool RequiresReAuthentication,
    bool RequiresSecondFactor,
    bool RequiresMeaningStatement,
    bool RequiresRepositoryAssessment,
    bool RequiresObjectFingerprint,
    bool RequiresManifestation,
    IReadOnlyList<string>? AllowedRepositoryTypes,
    bool AllowInterimRepositorySignature,
    string? InterimRepositoryBoundaryStatement);

public sealed record CreateSignatureRequestInput(
    string SubjectType,
    Guid SubjectId,
    Guid? RegisterEntryId,
    Guid? ControlledDocumentId,
    Guid? RequestedSignerUserId,
    string? RequestedSignerRole,
    string SignatureMeaning,
    DateTimeOffset? DueDate,
    string? RequestReason,
    Guid? RepositoryAssessmentId);

public sealed record CancelSignatureRequestInput(string Reason);

public sealed record RejectSignatureRequestInput(
    string Reason,
    string RejectionEvidenceReference,
    Guid? RejectedByUserId);

/// <summary>
/// MOD-0029-FU23 — the sign input. Note what is NOT here: there is no SignedAt, no SecondFactorPerformed and no
/// ReAuthenticationPerformed. Those are server-derived. A client that could assert them could backdate a signature
/// or fabricate an authentication claim, which is precisely what SOP §11.2 exists to prevent.
/// </summary>
public sealed record SignDocumentSubjectInput(
    Guid? SignatureRequestId,
    string SubjectType,
    Guid SubjectId,
    Guid? RegisterEntryId,
    Guid? ControlledDocumentId,
    string SignatureMeaning,
    string MeaningStatement,
    string? SignatureMethod,
    string? SignerRole,
    string? SignatureEvidenceReference,
    string? ExternalProviderReference,
    string? AuthenticationContextReference,
    Guid? RepositoryAssessmentId);

public sealed record InvalidateSignatureInput(string Reason);

// ── output models ────────────────────────────────────────────────────────────

public sealed record SignaturePolicyModel(
    Guid Id,
    string PolicyKey,
    string PolicyName,
    string PolicyStatus,
    string SignableSubjectType,
    string SignatureMeaning,
    bool RequiresReAuthentication,
    bool RequiresSecondFactor,
    bool RequiresMeaningStatement,
    bool RequiresRepositoryAssessment,
    bool RequiresObjectFingerprint,
    bool RequiresManifestation,
    IReadOnlyList<string> AllowedRepositoryTypes,
    bool AllowInterimRepositorySignature,
    string? InterimRepositoryBoundaryStatement,
    string BoundaryStatement);

public sealed record SignatureRequestModel(
    Guid Id,
    string SignatureRequestNumber,
    string SubjectType,
    Guid SubjectId,
    Guid? RegisterEntryId,
    Guid? ControlledDocumentId,
    Guid? RequestedSignerUserId,
    string? RequestedSignerRole,
    string SignatureMeaning,
    string RequestStatus,
    DateTimeOffset RequestedAt,
    string? RequestedBy,
    DateTimeOffset? DueDate,
    string? RequestReason,
    Guid? RepositoryAssessmentId,
    Guid? PolicyId,
    Guid? SignatureRecordId,
    DateTimeOffset? SignedAt,
    string? CancellationReason,
    string? RejectionReason,
    bool IsOverdue,
    string BoundaryStatement);

public sealed record SignatureRecordModel(
    Guid Id,
    string SignatureNumber,
    Guid? SignatureRequestId,
    string SubjectType,
    Guid SubjectId,
    Guid? RegisterEntryId,
    Guid? ControlledDocumentId,
    string SignatureMeaning,
    Guid? SignerUserId,
    string? SignerDisplayName,
    string? SignerRole,
    DateTimeOffset SignedAt,
    string SignatureMethod,
    string SignatureStatus,
    string MeaningStatement,
    string ObjectFingerprint,
    string FingerprintAlgorithm,
    Guid? ObjectSnapshotReferenceId,
    string? ObjectSnapshotSummary,
    Guid? RepositoryAssessmentId,
    string? RepositoryTypeAtSigning,
    string RepositoryBoundaryStatement,
    string? AuthenticationContextReference,
    bool ReAuthenticationPerformed,
    bool SecondFactorPerformed,
    string? SignatureEvidenceReference,
    string? ExternalProviderReference,
    string ValidationResult,
    string? ValidationDetails,
    DateTimeOffset? InvalidatedAt,
    string? InvalidatedBy,
    string? InvalidationReason,
    DateTimeOffset? LastVerifiedAt,
    string BoundaryStatement);

public sealed record SignedObjectFingerprintModel(
    Guid Id,
    string SubjectType,
    Guid SubjectId,
    Guid? RegisterEntryId,
    string FingerprintAlgorithm,
    string FingerprintValue,
    string SnapshotSummary,
    string? SnapshotReference,
    DateTimeOffset GeneratedAt,
    string? GeneratedBy);

/// <summary>MOD-0029-FU23 — the result of re-checking a signature against the current state of its subject.</summary>
public sealed record SignatureVerificationModel(
    Guid SignatureId,
    string SignatureStatusBefore,
    string SignatureStatusAfter,
    string Outcome,
    string SignedFingerprint,
    string? CurrentFingerprint,
    bool FingerprintMatches,
    DateTimeOffset VerifiedAt,
    string? CurrentSnapshotSummary,
    string VerificationNote,
    string BoundaryStatement);

/// <summary>
/// MOD-0029-FU23 — what the resolver established about a subject: does it exist in this tenant, and what does its
/// governance metadata currently look like?
/// </summary>
public sealed record SignableSubjectSnapshot(
    SignableSubjectType SubjectType,
    Guid SubjectId,
    Guid? RegisterEntryId,
    string CanonicalProjection,
    string Fingerprint,
    SignatureFingerprintAlgorithm Algorithm,
    string SnapshotSummary);

public static class ElectronicSignatureWire
{
    /// <summary>
    /// THE CENTRAL DISCLAIMER. Carried on every model FU23 returns and persisted on every signature record, so it
    /// travels with the evidence rather than living only in documentation.
    /// </summary>
    public const string BoundaryStatement =
        "Document-control scoped electronic signature foundation: MOD-0029-FU23 records signer identity, signature " +
        "meaning, a server-stamped timestamp and a canonical metadata fingerprint binding the signature to an exact " +
        "object state. It is NOT a qualified electronic signature capability and makes NO 21 CFR Part 11 or Annex 11 " +
        "compliance claim. No external e-signature provider is called, no certificate chain is validated, and no " +
        "PAdES/XAdES artefact is produced. An approved interim repository is never presented as a validated DMS.";

    public static SignableSubjectType ParseSubjectType(string? v) =>
        Enum.TryParse<SignableSubjectType>(v, true, out var r) ? r : SignableSubjectType.Other;

    public static SignatureMeaning? ParseMeaning(string? v) =>
        Enum.TryParse<SignatureMeaning>(v, true, out var r) ? r : null;

    public static SignatureMethod ParseMethod(string? v) =>
        Enum.TryParse<SignatureMethod>(v, true, out var r) ? r : SignatureMethod.InternalAttestation;

    public static RepositoryType? ParseRepositoryType(string? v) =>
        Enum.TryParse<RepositoryType>(v, true, out var r) ? r : null;

    public static SignaturePolicyModel ToPolicy(DocumentSignaturePolicy p) => new(
        p.Id, p.PolicyKey, p.PolicyName, p.PolicyStatus.ToString(), p.SignableSubjectType.ToString(),
        p.SignatureMeaning.ToString(), p.RequiresReAuthentication, p.RequiresSecondFactor,
        p.RequiresMeaningStatement, p.RequiresRepositoryAssessment, p.RequiresObjectFingerprint,
        p.RequiresManifestation, p.AllowedRepositoryTypes.Select(t => t.ToString()).ToList(),
        p.AllowInterimRepositorySignature, p.InterimRepositoryBoundaryStatement, BoundaryStatement);

    public static SignatureRequestModel ToRequest(DocumentSignatureRequest r, DateTimeOffset now) => new(
        r.Id, r.SignatureRequestNumber, r.SubjectType.ToString(), r.SubjectId, r.RegisterEntryId,
        r.ControlledDocumentId, r.RequestedSignerUserId, r.RequestedSignerRole, r.SignatureMeaning.ToString(),
        r.RequestStatus.ToString(), r.RequestedAt, r.RequestedBy, r.DueDate, r.RequestReason,
        r.RepositoryAssessmentId, r.PolicyId, r.SignatureRecordId, r.SignedAt, r.CancellationReason,
        r.RejectionReason,
        !r.IsTerminal() && r.DueDate is { } due && now > due,
        BoundaryStatement);

    public static SignatureRecordModel ToSignature(DocumentSignatureRecord s) => new(
        s.Id, s.SignatureNumber, s.SignatureRequestId, s.SubjectType.ToString(), s.SubjectId, s.RegisterEntryId,
        s.ControlledDocumentId, s.SignatureMeaning.ToString(), s.SignerUserId, s.SignerDisplayName, s.SignerRole,
        s.SignedAt, s.SignatureMethod.ToString(), s.SignatureStatus.ToString(), s.MeaningStatement,
        s.ObjectFingerprint, s.FingerprintAlgorithm.ToString(), s.ObjectSnapshotReferenceId,
        s.ObjectSnapshotSummary, s.RepositoryAssessmentId, s.RepositoryTypeAtSigning?.ToString(),
        s.RepositoryBoundaryStatement, s.AuthenticationContextReference, s.ReAuthenticationPerformed,
        s.SecondFactorPerformed, s.SignatureEvidenceReference, s.ExternalProviderReference,
        s.ValidationResult.ToString(), s.ValidationDetails, s.InvalidatedAt, s.InvalidatedBy,
        s.InvalidationReason, s.LastVerifiedAt, BoundaryStatement);

    public static SignedObjectFingerprintModel ToFingerprint(DocumentSignedObjectFingerprint f) => new(
        f.Id, f.SubjectType.ToString(), f.SubjectId, f.RegisterEntryId, f.FingerprintAlgorithm.ToString(),
        f.FingerprintValue, f.SnapshotSummary, f.SnapshotReference, f.GeneratedAt, f.GeneratedBy);
}

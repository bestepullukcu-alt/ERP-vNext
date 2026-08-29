namespace Diten.Platform.API.Models.DocumentManagement;

// MOD-0029-FU23 — electronic signature API request contracts.
//
// NOTE WHAT IS ABSENT FROM EVERY REQUEST BELOW: there is no TenantId (resolved server-side from the token, never
// from the client), no SignedAt (always server-stamped — there is no backdating path), and no
// SecondFactorPerformed / ReAuthenticationPerformed (server-derived; a client-asserted authentication claim would
// be fabricated evidence).

public sealed record CreateSignaturePolicyApiRequest(
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

public sealed record CreateSignatureRequestApiRequest(
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

public sealed record CancelSignatureRequestApiRequest(string Reason);

public sealed record RejectSignatureRequestApiRequest(
    string Reason,
    string RejectionEvidenceReference,
    Guid? RejectedByUserId);

public sealed record SignDocumentSubjectApiRequest(
    Guid? SignatureRequestId,
    string SubjectType,
    Guid SubjectId,
    Guid? RegisterEntryId,
    Guid? ControlledDocumentId,
    string SignatureMeaning,

    /// <summary>Mandatory. The signer's stated meaning, in words (SOP §11.2).</summary>
    string MeaningStatement,

    string? SignatureMethod,
    string? SignerRole,

    /// <summary>A storage/reference string — never a file, never raw bytes.</summary>
    string? SignatureEvidenceReference,

    /// <summary>Stored as-is. No provider API is called and no certificate is validated.</summary>
    string? ExternalProviderReference,

    /// <summary>The only thing that can evidence re-authentication. Opaque to the platform.</summary>
    string? AuthenticationContextReference,

    Guid? RepositoryAssessmentId);

public sealed record InvalidateSignatureApiRequest(string Reason);

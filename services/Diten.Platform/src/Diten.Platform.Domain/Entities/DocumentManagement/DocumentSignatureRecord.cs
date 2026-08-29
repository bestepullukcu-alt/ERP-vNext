using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU23 — THE SIGNATURE ITSELF (GMG-QMS-SOP-0001 §11.2). An append-only attestation that a named person,
/// at a server-stamped moment, asserted a specific MEANING against a specific OBJECT STATE.
///
/// WHAT MAKES THIS MORE THAN THE EVIDENCE STRINGS FU09–FU22 ALREADY CARRY: those record that an approval happened.
/// This records WHO asserted WHAT, and — via <see cref="ObjectFingerprint"/> — exactly WHICH VERSION of the record
/// they were looking at. When that object later changes, the fingerprint stops matching and the signature falls to
/// <see cref="SignatureStatus.RequiresResign"/> instead of silently continuing to look like current approval. That
/// binding is the entire point of the feature.
///
/// WHAT THIS IS NOT, AND MUST NEVER BE PRESENTED AS: a qualified electronic signature, a validated DMS capability,
/// or a 21 CFR Part 11 / Annex 11 compliance claim. No external provider is called. No certificate is validated.
/// <see cref="ValidationResult"/> is <see cref="SignatureValidationResult.NotValidated"/> for every signature FU23
/// produces, and <see cref="RepositoryBoundaryStatement"/> states the limitation ON THE RECORD so it travels with
/// the evidence rather than living only in documentation.
///
/// APPEND-ONLY. Never hard-deleted, never rewritten. Invalidation sets status, reason and timestamp; it does not
/// erase what was signed.
/// </summary>
public sealed class DocumentSignatureRecord : TenantScopedEntity
{
    public required string SignatureNumber { get; set; }

    public Guid? SignatureRequestId { get; set; }

    // ── what was signed ──────────────────────────────────────────────────────────────────────────────────
    public SignableSubjectType SubjectType { get; set; } = SignableSubjectType.Other;
    public required Guid SubjectId { get; set; }
    public Guid? RegisterEntryId { get; set; }
    public Guid? ControlledDocumentId { get; set; }

    // ── who signed, and what they meant by it ────────────────────────────────────────────────────────────
    public SignatureMeaning SignatureMeaning { get; set; } = SignatureMeaning.Other;
    public Guid? SignerUserId { get; set; }
    public string? SignerDisplayName { get; set; }
    public string? SignerRole { get; set; }

    /// <summary>
    /// The signer's stated meaning, in words. Mandatory. SOP §11.2: a signature whose meaning is not manifest is
    /// not a regulated signature.
    /// </summary>
    public required string MeaningStatement { get; set; }

    /// <summary>ALWAYS DateTimeOffset.UtcNow at signing. A client-supplied value is never honoured — no backdating.</summary>
    public DateTimeOffset SignedAt { get; set; } = DateTimeOffset.UtcNow;

    public SignatureMethod SignatureMethod { get; set; } = SignatureMethod.InternalAttestation;
    public SignatureStatus SignatureStatus { get; set; } = SignatureStatus.Valid;

    // ── the binding to the exact object state ────────────────────────────────────────────────────────────
    /// <summary>Canonical METADATA hash — never a hash of document bytes. FU23 does not read content.</summary>
    public required string ObjectFingerprint { get; set; }
    public SignatureFingerprintAlgorithm FingerprintAlgorithm { get; set; } = SignatureFingerprintAlgorithm.CanonicalJsonSha256;
    public Guid? ObjectSnapshotReferenceId { get; set; }
    public string? ObjectSnapshotSummary { get; set; }

    // ── repository boundary at the moment of signing (SOP §11) ───────────────────────────────────────────
    public Guid? RepositoryAssessmentId { get; set; }
    public RepositoryType? RepositoryTypeAtSigning { get; set; }

    /// <summary>Generated, never client-supplied. Travels with the record so the limitation cannot be lost.</summary>
    public required string RepositoryBoundaryStatement { get; set; }

    // ── authentication context ───────────────────────────────────────────────────────────────────────────
    /// <summary>The only thing that can justify <see cref="ReAuthenticationPerformed"/>. Opaque to FU23.</summary>
    public string? AuthenticationContextReference { get; set; }

    /// <summary>Server-derived from the presence of an authentication context — never accepted from the client.</summary>
    public bool ReAuthenticationPerformed { get; set; }

    /// <summary>
    /// ALWAYS false in FU23. There is no second-factor authentication context to derive it from, and a
    /// client-asserted value would be fabricated evidence. Kept as a field only so a future real implementation
    /// has somewhere to write.
    /// </summary>
    public bool SecondFactorPerformed { get; set; }

    // ── external / provider seam ─────────────────────────────────────────────────────────────────────────
    /// <summary>A storage/reference string — never the signed file, never raw bytes.</summary>
    public string? SignatureEvidenceReference { get; set; }

    /// <summary>EXTENSION POINT: the provider-side signature id. FU23 stores it and calls no provider API.</summary>
    public string? ExternalProviderReference { get; set; }

    public SignatureValidationResult ValidationResult { get; set; } = SignatureValidationResult.NotValidated;
    public string? ValidationDetails { get; set; }

    // ── invalidation (append-only: status change, never deletion) ────────────────────────────────────────
    public DateTimeOffset? InvalidatedAt { get; set; }
    public string? InvalidatedBy { get; set; }
    public string? InvalidationReason { get; set; }

    public DateTimeOffset? LastVerifiedAt { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>Only a Valid signature can be invalidated or counted as current approval.</summary>
    public bool IsCurrentlyValid() => SignatureStatus == SignatureStatus.Valid;
}

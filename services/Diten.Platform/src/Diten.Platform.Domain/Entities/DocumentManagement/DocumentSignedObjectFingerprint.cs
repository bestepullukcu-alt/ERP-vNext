using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU23 — a captured snapshot of WHAT THE SUBJECT LOOKED LIKE at a point in time (GMG-QMS-SOP-0001 §11.2).
///
/// Stored as its own append-only row rather than only as a field on the signature, because verification needs to
/// answer "what changed?", not merely "did something change?". The <see cref="SnapshotSummary"/> is a canonical,
/// human-legible projection of the subject's governance metadata — the same projection the fingerprint is computed
/// over, so the two can never disagree.
///
/// NO CONTENT, EVER: the projection covers status, dates, references and identifiers. It never includes document
/// bytes, file contents or attachments. FU23 does not read content and cannot hash it.
/// </summary>
public sealed class DocumentSignedObjectFingerprint : TenantScopedEntity
{
    public SignableSubjectType SubjectType { get; set; } = SignableSubjectType.Other;
    public required Guid SubjectId { get; set; }
    public Guid? RegisterEntryId { get; set; }

    public SignatureFingerprintAlgorithm FingerprintAlgorithm { get; set; } = SignatureFingerprintAlgorithm.CanonicalJsonSha256;
    public required string FingerprintValue { get; set; }

    /// <summary>The canonical metadata projection the fingerprint was computed over. Metadata only.</summary>
    public required string SnapshotSummary { get; set; }

    /// <summary>Optional pointer to a snapshot held elsewhere. A reference string — never raw bytes.</summary>
    public string? SnapshotReference { get; set; }

    /// <summary>Server-stamped at generation; never accepted from the client.</summary>
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? GeneratedBy { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

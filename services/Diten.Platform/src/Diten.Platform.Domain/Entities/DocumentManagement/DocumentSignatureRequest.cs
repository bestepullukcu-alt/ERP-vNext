using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU23 — an ASK for a signature (GMG-QMS-SOP-0001 §11.2). Separate from the signature itself because the
/// request and the act are different facts: who was asked, by whom, by when, and for what meaning is evidence in its
/// own right even when nobody ever signs.
///
/// The request names either a specific user or a role. Whoever eventually signs must MATCH that nomination — a
/// signature collected from someone who was never asked is not the signature that was requested.
///
/// Never hard-deleted. Cancellation and rejection are status changes, and neither is possible once the request has
/// been signed: a completed act cannot be retracted by editing the request that produced it.
/// </summary>
public sealed class DocumentSignatureRequest : TenantScopedEntity
{
    public required string SignatureRequestNumber { get; set; }

    public SignableSubjectType SubjectType { get; set; } = SignableSubjectType.Other;
    public required Guid SubjectId { get; set; }
    public Guid? RegisterEntryId { get; set; }
    public Guid? ControlledDocumentId { get; set; }

    // At least one of these is mandatory (enforced by the service): an unaddressed request asks nobody.
    public Guid? RequestedSignerUserId { get; set; }
    public string? RequestedSignerRole { get; set; }

    public SignatureMeaning SignatureMeaning { get; set; } = SignatureMeaning.Other;
    public SignatureRequestStatus RequestStatus { get; set; } = SignatureRequestStatus.Draft;

    /// <summary>Server-stamped; never accepted from the client.</summary>
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? RequestedBy { get; set; }
    public DateTimeOffset? DueDate { get; set; }
    public string? RequestReason { get; set; }

    public Guid? RepositoryAssessmentId { get; set; }
    public Guid? PolicyId { get; set; }

    // ── outcome ──────────────────────────────────────────────────────────────────────────────────────────
    public Guid? SignatureRecordId { get; set; }
    public DateTimeOffset? SignedAt { get; set; }

    /// <summary>Mandatory to cancel or reject: a withdrawn request must say why.</summary>
    public string? CancellationReason { get; set; }
    public string? RejectionReason { get; set; }
    public string? RejectionEvidenceReference { get; set; }
    public Guid? RejectedByUserId { get; set; }
    public DateTimeOffset? RejectedAt { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>The request can take no further transition.</summary>
    public bool IsTerminal() =>
        RequestStatus is SignatureRequestStatus.Signed
            or SignatureRequestStatus.Rejected
            or SignatureRequestStatus.Cancelled
            or SignatureRequestStatus.Expired;

    /// <summary>
    /// Does <paramref name="signerUserId"/> / <paramref name="signerRole"/> satisfy who was nominated? A named user
    /// takes precedence: when a specific person was asked, a role match is not a substitute.
    /// </summary>
    public bool IsSignerNominated(Guid? signerUserId, string? signerRole)
    {
        if (RequestedSignerUserId is { } nominated)
        {
            return signerUserId == nominated;
        }

        return !string.IsNullOrWhiteSpace(RequestedSignerRole)
            && string.Equals(RequestedSignerRole, signerRole?.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}

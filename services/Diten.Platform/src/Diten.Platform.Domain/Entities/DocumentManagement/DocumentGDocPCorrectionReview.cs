using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU21 — the second-person review of a correction record (GMG-QMS-SOP-0001 §21). A high-risk correction
/// — a backdated timestamp, a changed status, a swapped evidence reference, a reconstruction — must be seen by
/// someone other than the system that accepted it.
///
/// Recorded as its own append-only row rather than only as fields on the correction: a rejection followed by a
/// later approval must both remain visible, because the sequence of review decisions is itself the evidence.
/// Never hard-deleted.
/// </summary>
public sealed class DocumentGDocPCorrectionReview : TenantScopedEntity
{
    public required Guid CorrectionRecordId { get; set; }

    public GDocPReviewDecision ReviewDecision { get; set; } = GDocPReviewDecision.Approved;

    public Guid? ReviewerUserId { get; set; }
    public string? ReviewerRole { get; set; }
    public string? ReviewerName { get; set; }

    /// <summary>Mandatory for an approval. A reference — never the reviewed document bytes.</summary>
    public string? ReviewEvidenceReference { get; set; }

    /// <summary>Mandatory for a rejection: a refused correction must say why.</summary>
    public string? ReviewComment { get; set; }

    /// <summary>Server-stamped at review time; never accepted from the client.</summary>
    public DateTimeOffset ReviewedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

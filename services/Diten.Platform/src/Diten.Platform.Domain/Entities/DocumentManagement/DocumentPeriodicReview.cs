using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU12 — one periodic review cycle for a Document Master Register entry (GMG-QMS-SOP-0001 §9.15). A review is
/// initiated 60 calendar days before its due date and must COMPLETE BY the due date, or be formally extended BEFORE it.
/// The review history is permanent (never hard-deleted); a new cycle creates a new review.
/// </summary>
public sealed class DocumentPeriodicReview : TenantScopedEntity
{
    public required Guid RegisterEntryId { get; set; }

    /// <summary>1-based cycle number for this entry.</summary>
    public int ReviewNumber { get; set; }

    public PeriodicReviewStatus ReviewStatus { get; set; } = PeriodicReviewStatus.NotStarted;

    /// <summary>The current due date — extended in place when a formal extension is approved.</summary>
    public DateTimeOffset ReviewDueDate { get; set; }

    /// <summary>Due date minus the initiation window (60 calendar days by default).</summary>
    public DateTimeOffset InitiationWindowStartDate { get; set; }

    public DateTimeOffset? InitiatedAt { get; set; }
    public string? InitiatedBy { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
    public string? CompletedBy { get; set; }

    public PeriodicReviewDecision? ReviewDecision { get; set; }
    public string? ReviewEvidenceReference { get; set; }
    public string? ImpactAssessmentReference { get; set; }
    public string? Comment { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

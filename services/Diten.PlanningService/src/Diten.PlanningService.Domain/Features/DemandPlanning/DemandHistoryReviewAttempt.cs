namespace Diten.PlanningService.Domain.Features.DemandPlanning;

// Append-only evidence for rejected review attempts. Successful decisions are
// recorded atomically with the batch state in its embedded audit trail.
public sealed class DemandHistoryReviewAttempt : EntityBase
{
    public Guid LegalEntityId { get; set; }
    public Guid BatchId { get; set; }
    public Guid ActorId { get; set; }
    public string RequestKey { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string DedupKey { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
}

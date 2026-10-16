using Diten.PlanningService.Domain.Features.DemandPlanning;

namespace Diten.PlanningService.Persistence.Features.DemandPlanning;

public sealed class ManualDraftReviewAuditRecord : EntityBase
{
    public Guid LegalEntityId { get; set; }
    public Guid RevisionId { get; set; }
    public string RequestKey { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public DraftReviewAction Action { get; set; }
    public int ContentVersion { get; set; }
    public int StateVersionAfter { get; set; }
    public Guid ActorId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string? Reason { get; set; }
}

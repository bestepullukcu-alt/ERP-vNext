namespace Diten.PlanningService.Domain.Features.DemandPlanning;
public sealed class DemandAuditEntry : EntityBase
{
    public Guid LegalEntityId { get; set; }
    public Guid ActorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? EvidenceReference { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
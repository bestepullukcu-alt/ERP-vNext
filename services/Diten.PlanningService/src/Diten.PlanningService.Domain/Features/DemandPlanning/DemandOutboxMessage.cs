namespace Diten.PlanningService.Domain.Features.DemandPlanning;
public sealed class DemandOutboxMessage : EntityBase
{
    public Guid EventId { get; set; }
    public Guid LegalEntityId { get; set; }
    public Guid RevisionId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public long StateVersion { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset? DispatchedAt { get; set; }
}
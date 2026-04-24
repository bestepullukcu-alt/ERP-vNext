namespace Diten.AuthService.Domain.Entities;

public sealed class ProcessedIntegrationEvent : GlobalEntityBase
{
    private ProcessedIntegrationEvent() { }

    public ProcessedIntegrationEvent(Guid eventId, string eventName, Guid tenantId)
    {
        EventId = eventId;
        EventName = eventName;
        TenantId = tenantId;
        ProcessedAt = DateTimeOffset.UtcNow;
        CreatedBy = "internal-consumer";
    }

    public Guid EventId { get; private set; }
    public string EventName { get; private set; } = string.Empty;
    public Guid TenantId { get; private set; }
    public DateTimeOffset ProcessedAt { get; private set; }
}

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
        State = IntegrationEventInboxState.Completed;
        CreatedBy = "internal-consumer";
    }

    public ProcessedIntegrationEvent(
        Guid eventId,
        string eventName,
        Guid tenantId,
        Guid claimId,
        DateTimeOffset leaseExpiresAtUtc)
    {
        EventId = eventId;
        EventName = eventName;
        TenantId = tenantId;
        State = IntegrationEventInboxState.Processing;
        ClaimId = claimId;
        LeaseExpiresAtUtc = leaseExpiresAtUtc;
        CreatedBy = "internal-consumer";
    }

    public Guid EventId { get; private set; }
    public string EventName { get; private set; } = string.Empty;
    public Guid TenantId { get; private set; }
    public DateTimeOffset ProcessedAt { get; private set; }
    public IntegrationEventInboxState State { get; private set; } = IntegrationEventInboxState.Completed;
    public Guid? ClaimId { get; private set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; private set; }
}

public enum IntegrationEventInboxState
{
    Completed = 0,
    Processing = 1
}

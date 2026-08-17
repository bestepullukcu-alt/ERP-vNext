namespace Diten.AuthService.Application.Common.Events;

public sealed record TenantActivatedIntegrationEvent(
    Guid EventId,
    Guid TenantId,
    string EventName,
    int EventVersion,
    Guid CorrelationId,
    Guid CausationId,
    DateTimeOffset OccurredAt,
    string Producer);

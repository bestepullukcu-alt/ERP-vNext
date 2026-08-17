namespace Diten.AuthService.Application.Common.Events;

public sealed record EventEnvelope(
    Guid EventId,
    string EventName,
    int EventVersion,
    Guid TenantId,
    Guid CorrelationId,
    Guid CausationId,
    DateTimeOffset OccurredAt,
    string Producer,
    EventActor Actor,
    object Payload);

public sealed record EventActor(string UserId, string ActorType);

namespace Diten.Platform.Application.Contracts.Eventing;

public sealed record EventTransportMessage(
    Guid EventId,
    string EventName,
    int EventVersion,
    Guid CorrelationId,
    Guid? CausationId,
    Guid? TenantId,
    string Producer,
    DateTimeOffset OccurredAtUtc,
    string PayloadJson);

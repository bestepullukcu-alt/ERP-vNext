// MOD-0175 Q2 — copied from CrmService (Diten.CrmService.Infrastructure/Eventing/EventTransportMessage.cs), itself copied
// from AuthService. The namespace deliberately matches Platform's so the MassTransit message URN is identical
// (urn:message:Diten.Platform.Application.Contracts.Eventing:EventTransportMessage) and InventoryService binds to the
// exchange Platform publishes to. Re-declared instead of referencing Diten.Platform.Application (wrong layer, heavy graph).
// Platform does not sign today (EmptyTrustedTransportMetadataProvider) — see QuarantineWorkflowOutcomeConsumer.
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

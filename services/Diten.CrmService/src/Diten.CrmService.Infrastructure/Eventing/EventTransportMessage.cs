// WP-CL-BE-4 — copied from AuthService (Diten.AuthService.Infrastructure/Eventing/EventTransportMessage.cs).
// The namespace deliberately matches Platform's so the MassTransit message URN is identical
// (urn:message:Diten.Platform.Application.Contracts.Eventing:EventTransportMessage) and CRM binds to the exchange
// Platform publishes to. Re-declared instead of referencing Diten.Platform.Application (wrong layer, heavy graph);
// if a shared transport-contracts assembly is introduced, both sides should reference it instead.
// Platform's record also carries TransportHeaders (signature metadata); Platform does not sign today
// (EmptyTrustedTransportMetadataProvider), so there is nothing to verify yet — see ClaimWorkflowOutcomeConsumer.
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

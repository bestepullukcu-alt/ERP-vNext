// NOTE: namespace deliberately matches Platform's so the MassTransit message URN is identical
// (urn:message:Diten.Platform.Application.Contracts.Eventing:EventTransportMessage). Platform's
// publisher uses no [EntityName] override, so the default type-derived URN governs the exchange.
// Re-declaring the contract here (instead of referencing Diten.Platform.Application — wrong layer,
// heavy graph) lets AuthService bind to that exact exchange. There is no shared transport-contracts
// assembly today; if one is introduced, both sides should reference it instead. The actual broker
// binding is verified end-to-end in S18 (no broker in the unit-test harness).
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

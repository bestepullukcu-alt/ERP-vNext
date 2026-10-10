namespace Diten.SupplyChainService.Domain.Features.Shipments;
public sealed record ShipmentLifecycleEntry(Guid Id, Guid TenantId, Guid LegalEntityId, Guid ShipmentId, int Sequence,
 string? FromStatus, string ToStatus, DateTimeOffset OccurredAt, Guid ActorId, Guid CorrelationId, Guid CommandId,
 string? ReasonCode, string? Note);

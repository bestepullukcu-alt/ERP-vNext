namespace Diten.SupplyChainService.Domain.Features.Shipments;
public sealed record ProofOfDelivery(Guid Id, DateTimeOffset ReceivedAt, string RecipientName, string[] EvidenceReferenceIds, string? Note, Guid CorrelationId);

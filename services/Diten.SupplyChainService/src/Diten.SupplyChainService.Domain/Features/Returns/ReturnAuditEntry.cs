namespace Diten.SupplyChainService.Domain.Features.Returns;
public sealed record ReturnAuditEntry(Guid TenantId, Guid LegalEntityId, Guid ReturnId, Guid ActorId, int Version, string? FromStatus, string ToStatus, DateTimeOffset CommittedAt, string? OccurredAt, string? InventoryTransactionReferenceId, string? DispositionCode, string? EvidenceType);

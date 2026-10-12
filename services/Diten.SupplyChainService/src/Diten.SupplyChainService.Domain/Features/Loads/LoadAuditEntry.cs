namespace Diten.SupplyChainService.Domain.Features.Loads;
public sealed record LoadAuditEntry(Guid TenantId, Guid LegalEntityId, Guid LoadId, Guid ActorId, int Version, string? FromStatus, string ToStatus, DateTimeOffset CommittedAt, string? Note);

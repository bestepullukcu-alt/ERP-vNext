namespace Diten.SupplyChainService.Domain.Features.SupplierPerformance;
/// <summary>Tenant and actor from the signed token; the legal entity from X-Legal-Entity-Id after MDM confirmed it (R-2).</summary>
public sealed record SupplierPerformanceScope(Guid TenantId, Guid LegalEntityId, Guid ActorId)
{ public void EnsureTrusted() { if (TenantId == Guid.Empty || LegalEntityId == Guid.Empty || ActorId == Guid.Empty) throw new InvalidOperationException("Trusted scope required."); } }

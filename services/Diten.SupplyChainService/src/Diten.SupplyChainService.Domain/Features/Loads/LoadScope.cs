namespace Diten.SupplyChainService.Domain.Features.Loads;
public sealed record LoadScope(Guid TenantId, Guid LegalEntityId, Guid ActorId)
{ public void EnsureTrusted() { if (TenantId == Guid.Empty || LegalEntityId == Guid.Empty || ActorId == Guid.Empty) throw new InvalidOperationException("Trusted scope required."); } }

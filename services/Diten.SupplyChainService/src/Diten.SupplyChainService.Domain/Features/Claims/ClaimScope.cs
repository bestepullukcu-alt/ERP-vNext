namespace Diten.SupplyChainService.Domain.Features.Claims;
public sealed record ClaimScope(Guid TenantId, Guid LegalEntityId, Guid ActorId)
{
 public void EnsureTrusted() { if(TenantId==Guid.Empty || LegalEntityId==Guid.Empty || ActorId==Guid.Empty) throw new ClaimFailureException(403,"FORBIDDEN"); }
}

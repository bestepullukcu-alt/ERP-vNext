namespace Diten.SupplyChainService.Domain.Features.Returns;
public sealed record ReturnScope(Guid TenantId, Guid LegalEntityId, Guid ActorId)
{ public void EnsureTrusted() { if (TenantId == Guid.Empty || LegalEntityId == Guid.Empty || ActorId == Guid.Empty) throw new ReturnFailureException(403, "INVALID_REQUEST"); } }

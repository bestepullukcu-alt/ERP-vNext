namespace Diten.SupplyChainService.Domain.Features.Carriers;
public sealed record CarrierScope(Guid TenantId, Guid LegalEntityId, Guid ActorId)
{
    public void EnsureTrusted()
    {
        if (TenantId == Guid.Empty || LegalEntityId == Guid.Empty || ActorId == Guid.Empty)
            throw new InvalidOperationException("Trusted Carrier scope is required.");
    }
}

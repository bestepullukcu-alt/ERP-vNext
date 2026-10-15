namespace Diten.MdmService.Application.Contracts.Audit;

public interface ITrustedSourceAuditIntentClient
{
    Task<TrustedSourceAuditIntentDeliveryResult> AcceptAsync(
        TrustedSourceAuditIntentEnvelope envelope,
        TrustedSourceAuditServiceIdentity identity,
        CancellationToken cancellationToken = default);
}

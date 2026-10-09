namespace Diten.Platform.Application.Contracts.Audit;

public interface ITrustedSourceAuditIntentAcceptanceService
{
    Task<TrustedSourceAuditIntentAcceptanceResult> AcceptAsync(
        TrustedSourceAuditIntentEnvelope envelope,
        CancellationToken ct = default);
}

using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Contracts.Audit;

public interface ITrustedSourceAuditIntentOutbox
{
    Task<TrustedSourceAuditIntentAcceptanceResult> AcceptAsync(
        TrustedSourceAuditIntentEnvelope envelope,
        string centralIdempotencyKey,
        string sourceIntentFingerprint,
        string mappedEntityType,
        AuditOperation mappedOperation,
        CancellationToken ct = default);
}

using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Services;

public enum VerifiedMarketOperationalTargetDisposition
{
    Fresh,
    ExactReplay,
    ManualReconciliationRequired
}

public sealed record VerifiedMarketOperationalTargetPreflightResult(
    VerifiedMarketOperationalTargetDisposition Disposition,
    string ReasonCode);

public sealed record VerifiedMarketOperationalAuditProofRequest(
    Guid TenantId,
    Guid CorrelationId,
    string PublicationCorrelationId,
    string AuditIdempotencyKey,
    string RequestType,
    string EntityType,
    Guid EntityId,
    AuditOperation Operation,
    string GovernanceEvent,
    string PublishMode,
    string ActorId,
    string PublicationOperationKey,
    string CatalogVersion,
    string SetCode);

public sealed record VerifiedMarketOperationalAuditProofResult(
    bool IsExact,
    string ReasonCode,
    int MatchCount);

public interface IVerifiedMarketOperationalPreflight
{
    Task<VerifiedMarketOperationalTargetPreflightResult> VerifyBeforeWriteAsync(
        VerifiedMarketOperationalFacts facts,
        CancellationToken ct = default);

    Task<VerifiedMarketOperationalTargetPreflightResult> VerifyCompletionAsync(
        VerifiedMarketOperationalFacts facts,
        CancellationToken ct = default);

    Task<VerifiedMarketOperationalAuditProofResult> VerifyAuditOutboxAsync(
        VerifiedMarketOperationalAuditProofRequest request,
        CancellationToken ct = default);
}

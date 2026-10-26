using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Services;

public enum VerifiedMarketOperationalTargetDisposition
{
    Fresh,
    ExactReplay,
    ManualReconciliationRequired
}

/// <summary>
/// WP-PLT-MARKET-REFTENANT-01 (MOD-0048-FU01 amendment 2026-10-15, BL-645) — the reference tenant this isolated mode was
/// composed with: the RESOLVED <c>BusinessReferenceData:Provider:ReferenceTenantId</c> (the same source as the GSKU path),
/// <see cref="Guid.Empty"/> when it does not resolve. Whether that tenant is ALLOWED (not the platform system tenant, not the
/// GSKU consumer, process value equal to configuration) is decided once, by the eligibility; the audit adapter, the preflight
/// and the event publisher only check that what they are given belongs to this tenant.
/// </summary>
public sealed record VerifiedMarketOperationalReferenceTenant(Guid TenantId);

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

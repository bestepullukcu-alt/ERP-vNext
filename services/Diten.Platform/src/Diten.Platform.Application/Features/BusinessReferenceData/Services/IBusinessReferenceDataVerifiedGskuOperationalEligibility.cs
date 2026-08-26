namespace Diten.Platform.Application.Features.BusinessReferenceData.Services;

/// <summary>
/// Marker for a process-local authorization issued by the registered operational eligibility service.
/// Implementations must reject marker instances they did not issue.
/// </summary>
public interface IBusinessReferenceDataVerifiedGskuOperationalAuthorization;

public sealed record VerifiedGskuOperationalFacts(
    string CatalogPath,
    string CatalogVersion,
    string CatalogFingerprint,
    Guid ReferenceTenantId,
    Guid ConsumerTenantId,
    string ActorId,
    string IdempotencyNamespace,
    IReadOnlyList<string> RequiredSetCodes);

public sealed record VerifiedGskuOperationalEligibilityDecision(
    bool IsEligible,
    string ReasonCode,
    VerifiedGskuOperationalFacts? Facts = null,
    IBusinessReferenceDataVerifiedGskuOperationalAuthorization? Authorization = null);

public sealed record VerifiedGskuEnumerationFacts(
    string CatalogPath,
    string CatalogVersion,
    string CatalogFingerprint,
    Guid ReferenceTenantId,
    Guid ConsumerTenantId,
    IReadOnlyList<string> RequiredSetCodes);

public sealed record VerifiedGskuEnumerationEligibilityDecision(
    bool IsEligible,
    string ReasonCode,
    VerifiedGskuEnumerationFacts? Facts = null);

public interface IBusinessReferenceDataVerifiedGskuOperationalEligibility
{
    Task<VerifiedGskuOperationalEligibilityDecision> EvaluateAsync(CancellationToken ct = default);

    Task<VerifiedGskuEnumerationEligibilityDecision> EvaluateEnumerationAsync(CancellationToken ct = default);

    bool IsAuthorized(
        IBusinessReferenceDataVerifiedGskuOperationalAuthorization authorization,
        VerifiedGskuOperationalFacts facts);
}

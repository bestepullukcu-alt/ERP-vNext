namespace Diten.Platform.Application.Features.BusinessReferenceData.Services;

public interface IBusinessReferenceDataVerifiedMarketOperationalAuthorization;
public sealed record VerifiedMarketOperationalFacts(string CatalogPath, string CatalogVersion, string CatalogFingerprint, Guid ReferenceTenantId, string ActorId, string IdempotencyNamespace);
public sealed record VerifiedMarketOperationalEligibilityDecision(bool IsEligible, string ReasonCode, VerifiedMarketOperationalFacts? Facts = null, IBusinessReferenceDataVerifiedMarketOperationalAuthorization? Authorization = null);
public interface IBusinessReferenceDataVerifiedMarketOperationalEligibility
{
    Task<VerifiedMarketOperationalEligibilityDecision> EvaluateAsync(CancellationToken ct = default);
    bool IsAuthorized(IBusinessReferenceDataVerifiedMarketOperationalAuthorization authorization, VerifiedMarketOperationalFacts facts);
}

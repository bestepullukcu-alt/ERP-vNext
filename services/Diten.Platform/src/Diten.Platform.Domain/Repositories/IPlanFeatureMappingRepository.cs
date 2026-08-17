using Diten.Platform.Domain.Features.SubscriptionFeatures;

namespace Diten.Platform.Domain.Repositories;

public interface IPlanFeatureMappingRepository
{
    Task<IReadOnlyList<PlanFeatureMapping>> GetByPlanIdAsync(Guid subscriptionPlanId, CancellationToken ct = default);
    Task<IReadOnlyList<PlanFeatureMapping>> GetByFeatureIdAsync(Guid featureDefinitionId, CancellationToken ct = default);
    Task<PlanFeatureMapping?> GetByPlanAndFeatureAsync(Guid subscriptionPlanId, Guid featureDefinitionId, CancellationToken ct = default);
    Task<bool> UpsertAsync(PlanFeatureMapping mapping, byte[]? expectedRowVersion = null, CancellationToken ct = default);
}

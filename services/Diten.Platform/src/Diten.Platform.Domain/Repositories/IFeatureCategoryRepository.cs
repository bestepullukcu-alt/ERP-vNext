using Diten.Platform.Domain.Features.SubscriptionFeatures;

namespace Diten.Platform.Domain.Repositories;

public interface IFeatureCategoryRepository
{
    Task<FeatureCategory> CreateAsync(FeatureCategory category, CancellationToken ct = default);
    Task<FeatureCategory?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string categoryCode, Guid? excludeId = null, CancellationToken ct = default);
    Task<bool> UpdateAsync(FeatureCategory category, byte[]? expectedRowVersion = null, CancellationToken ct = default);
    Task<IReadOnlyList<FeatureCategory>> GetAllAsync(FeatureCategoryStatus? status = null, CancellationToken ct = default);
}

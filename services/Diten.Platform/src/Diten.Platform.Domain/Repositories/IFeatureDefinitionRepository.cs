using Diten.Platform.Domain.Features.SubscriptionFeatures;

namespace Diten.Platform.Domain.Repositories;

public interface IFeatureDefinitionRepository
{
    Task<FeatureDefinition> CreateAsync(FeatureDefinition feature, CancellationToken ct = default);
    Task<FeatureDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string featureCode, Guid? excludeId = null, CancellationToken ct = default);
    Task<bool> ExistsBySlugAsync(string featureSlug, Guid? excludeId = null, CancellationToken ct = default);
    Task<bool> UpdateAsync(FeatureDefinition feature, byte[]? expectedRowVersion = null, CancellationToken ct = default);
    Task<(IReadOnlyList<FeatureDefinition> Items, long TotalCount)> QueryAsync(FeatureDefinitionsQuery query, CancellationToken ct = default);
}

public sealed record FeatureDefinitionsQuery(
    string? Search,
    Guid? CategoryId,
    FeatureDefinitionStatus? Status,
    bool? IsCoreFeature,
    int Page,
    int PageSize,
    string Sort);

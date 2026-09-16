using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface ITepRehireRecommendationReadinessMetadataRepository
{
    Task<IReadOnlyList<TepRehireRecommendationReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<TepRehireRecommendationReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(TepRehireRecommendationReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(TepRehireRecommendationReadinessMetadata metadata, CancellationToken ct);
}

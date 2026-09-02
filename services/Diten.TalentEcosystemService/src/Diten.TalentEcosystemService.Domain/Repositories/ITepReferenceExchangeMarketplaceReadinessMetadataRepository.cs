using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface ITepReferenceExchangeMarketplaceReadinessMetadataRepository
{
    Task<IReadOnlyList<TepReferenceExchangeMarketplaceReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<TepReferenceExchangeMarketplaceReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(TepReferenceExchangeMarketplaceReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(TepReferenceExchangeMarketplaceReadinessMetadata metadata, CancellationToken ct);
}

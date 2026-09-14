using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface ITepReferenceExchangeMarketplaceReadinessMetadataRepository
{
    Task<IReadOnlyList<TepReferenceExchangeMarketplaceReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<TepReferenceExchangeMarketplaceReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(TepReferenceExchangeMarketplaceReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(TepReferenceExchangeMarketplaceReadinessMetadata metadata, CancellationToken ct);
}

using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface IIndustryKnowledgeNetworkReadinessMetadataRepository
{
    Task<IReadOnlyList<IndustryKnowledgeNetworkReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<IndustryKnowledgeNetworkReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(IndustryKnowledgeNetworkReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(IndustryKnowledgeNetworkReadinessMetadata metadata, CancellationToken ct);
}

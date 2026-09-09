using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface ICompetencySkillsReadinessMetadataRepository
{
    Task<IReadOnlyList<CompetencySkillsReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<CompetencySkillsReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(CompetencySkillsReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(CompetencySkillsReadinessMetadata metadata, CancellationToken ct);
}

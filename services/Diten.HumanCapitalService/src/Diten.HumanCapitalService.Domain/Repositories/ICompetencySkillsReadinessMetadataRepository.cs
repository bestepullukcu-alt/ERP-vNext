using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface ICompetencySkillsReadinessMetadataRepository
{
    Task<IReadOnlyList<CompetencySkillsReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<CompetencySkillsReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(CompetencySkillsReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(CompetencySkillsReadinessMetadata metadata, CancellationToken ct);
}

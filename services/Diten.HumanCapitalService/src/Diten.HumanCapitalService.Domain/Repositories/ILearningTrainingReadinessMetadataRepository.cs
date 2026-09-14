using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface ILearningTrainingReadinessMetadataRepository
{
    Task<IReadOnlyList<LearningTrainingReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<LearningTrainingReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(LearningTrainingReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(LearningTrainingReadinessMetadata metadata, CancellationToken ct);
}

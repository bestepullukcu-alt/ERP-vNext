using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface ILearningTrainingReadinessMetadataRepository
{
    Task<IReadOnlyList<LearningTrainingReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<LearningTrainingReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(LearningTrainingReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(LearningTrainingReadinessMetadata metadata, CancellationToken ct);
}

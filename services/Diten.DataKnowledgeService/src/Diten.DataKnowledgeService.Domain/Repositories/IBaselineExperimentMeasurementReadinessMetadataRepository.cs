using Diten.DataKnowledgeService.Domain.Entities;

namespace Diten.DataKnowledgeService.Domain.Repositories;

public interface IBaselineExperimentMeasurementReadinessMetadataRepository
{
    Task<IReadOnlyList<BaselineExperimentMeasurementReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<BaselineExperimentMeasurementReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(BaselineExperimentMeasurementReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(BaselineExperimentMeasurementReadinessMetadata metadata, CancellationToken ct);
}

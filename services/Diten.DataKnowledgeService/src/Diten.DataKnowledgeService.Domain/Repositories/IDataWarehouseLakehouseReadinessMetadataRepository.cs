using Diten.DataKnowledgeService.Domain.Entities;

namespace Diten.DataKnowledgeService.Domain.Repositories;

public interface IDataWarehouseLakehouseReadinessMetadataRepository
{
    Task<IReadOnlyList<DataWarehouseLakehouseReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<DataWarehouseLakehouseReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(DataWarehouseLakehouseReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(DataWarehouseLakehouseReadinessMetadata metadata, CancellationToken ct);
}

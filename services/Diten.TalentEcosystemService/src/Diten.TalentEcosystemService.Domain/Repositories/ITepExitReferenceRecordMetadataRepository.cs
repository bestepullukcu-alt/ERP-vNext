using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface ITepExitReferenceRecordMetadataRepository
{
    Task<IReadOnlyList<TepExitReferenceRecordMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<TepExitReferenceRecordMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(TepExitReferenceRecordMetadata metadata, CancellationToken ct);
    Task UpdateAsync(TepExitReferenceRecordMetadata metadata, CancellationToken ct);
}

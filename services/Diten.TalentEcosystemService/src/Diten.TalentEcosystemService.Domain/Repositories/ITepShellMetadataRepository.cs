using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface ITepShellMetadataRepository
{
    Task<IReadOnlyList<TepShellMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<TepShellMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(TepShellMetadata metadata, CancellationToken ct);
    Task UpdateAsync(TepShellMetadata metadata, CancellationToken ct);
}

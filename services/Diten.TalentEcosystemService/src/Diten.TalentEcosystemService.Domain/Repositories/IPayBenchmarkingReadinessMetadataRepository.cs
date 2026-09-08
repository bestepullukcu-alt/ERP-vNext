using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface IPayBenchmarkingReadinessMetadataRepository
{
    Task<IReadOnlyList<PayBenchmarkingReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<PayBenchmarkingReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(PayBenchmarkingReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(PayBenchmarkingReadinessMetadata metadata, CancellationToken ct);
}

using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface IPayBenchmarkingReadinessMetadataRepository
{
    Task<IReadOnlyList<PayBenchmarkingReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<PayBenchmarkingReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(PayBenchmarkingReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(PayBenchmarkingReadinessMetadata metadata, CancellationToken ct);
}

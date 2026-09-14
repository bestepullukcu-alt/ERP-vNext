using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface ITepReviewBoardCaseMetadataRepository
{
    Task<IReadOnlyList<TepReviewBoardCaseMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<TepReviewBoardCaseMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(TepReviewBoardCaseMetadata metadata, CancellationToken ct);
    Task UpdateAsync(TepReviewBoardCaseMetadata metadata, CancellationToken ct);
}

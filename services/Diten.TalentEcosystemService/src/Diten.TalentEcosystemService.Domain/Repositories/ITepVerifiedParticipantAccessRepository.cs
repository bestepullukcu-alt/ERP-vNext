using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface ITepVerifiedParticipantAccessRepository
{
    Task<IReadOnlyList<TepVerifiedParticipantAccess>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<TepVerifiedParticipantAccess?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(TepVerifiedParticipantAccess access, CancellationToken ct);
    Task UpdateAsync(TepVerifiedParticipantAccess access, CancellationToken ct);
}

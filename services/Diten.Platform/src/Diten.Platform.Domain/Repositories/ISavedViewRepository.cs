using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Domain.Repositories;

public interface ISavedViewRepository
{
    Task<IReadOnlyList<SavedView>> GetListAsync(Guid userId, string moduleKey, string pageKey, CancellationToken ct = default);

    Task<SavedView?> GetByIdAsync(string id, CancellationToken ct = default);

    Task<SavedView> InsertAsync(SavedView entity, CancellationToken ct = default);

    Task<SavedView> UpdateAsync(SavedView entity, CancellationToken ct = default);

    Task<bool> DeleteAsync(string id, CancellationToken ct = default);

    Task ClearDefaultsAsync(Guid userId, string moduleKey, string pageKey, string? excludeId = null, CancellationToken ct = default);
}

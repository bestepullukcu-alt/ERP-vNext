using Diten.DevEnablementService.Domain.Entities;

namespace Diten.DevEnablementService.Domain.Repositories;

public interface IGoldenReferenceCompactRepository
{
    Task<IReadOnlyList<GoldenReferenceCompact>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<GoldenReferenceCompact?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<GoldenReferenceCompact> CreateAsync(GoldenReferenceCompact entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(GoldenReferenceCompact entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> BulkDeleteAsync(List<Guid> ids, CancellationToken cancellationToken = default);
}

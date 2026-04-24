using Diten.DevEnablementService.Domain.Entities;

namespace Diten.DevEnablementService.Domain.Repositories;

public interface IGoldenReferenceItemRepository
{
    Task<IReadOnlyList<GoldenReferenceItem>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<GoldenReferenceItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<GoldenReferenceItem> CreateAsync(GoldenReferenceItem entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(GoldenReferenceItem entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> BulkDeleteAsync(List<Guid> ids, CancellationToken cancellationToken = default);
}

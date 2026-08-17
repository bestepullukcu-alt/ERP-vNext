using Diten.DevEnablementService.Domain.Entities;

namespace Diten.DevEnablementService.Domain.Repositories;

public interface IGoldenReferenceSlimRepository
{
    Task<IReadOnlyList<GoldenReferenceSlim>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<GoldenReferenceSlim?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<GoldenReferenceSlim> CreateAsync(GoldenReferenceSlim entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(GoldenReferenceSlim entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> BulkDeleteAsync(List<Guid> ids, CancellationToken cancellationToken = default);
}

using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Interfaces;

/// <summary>
/// Generic base repository interface.
/// All implementations automatically filter by TenantId and IsDeleted=false.
/// </summary>
public interface IRepository<T> where T : EntityBase
{
    Task<T> CreateAsync(T entity, CancellationToken ct = default);
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task<bool> UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    
    // Domain Common Methods
    Task<int> BulkDeleteAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<long> CountAsync(CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default);
}

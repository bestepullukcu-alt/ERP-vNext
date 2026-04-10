using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Interfaces;

/// <summary>
/// Repository for SKU (Stock Keeping Unit) operations.
/// All queries automatically filter by TenantId and IsDeleted=false.
/// </summary>
public interface ISkuRepository
{
    Task<Sku> CreateAsync(Sku entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Sku entity, CancellationToken cancellationToken = default);
    Task<Sku?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Sku>> GetAllAsync(CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> BulkDeleteAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default);
}

using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Interfaces;

/// <summary>
/// Repository for SKU (Stock Keeping Unit) operations.
/// All queries automatically filter by TenantId and IsDeleted=false.
/// </summary>
public interface ISkuRepository : IRepository<Sku>
{
    // SKU-specific methods only — standard CRUD inherited from IRepository<Sku>
    Task<int> BulkDeleteAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default);
}

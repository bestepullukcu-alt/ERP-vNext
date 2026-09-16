using Diten.ProcurementService.Domain.Entities;

namespace Diten.ProcurementService.Domain.Repositories;

/// <summary>
/// Supplier repository sözleşmesi. Her metot Tenant + LegalEntity + IsDeleted=false ile filtrelenir
/// (implementasyon zorunluluğu — multi-tenancy.md). FAZ 1 scaffold: read yolları + soft-delete iskeleti.
/// </summary>
public interface ISupplierRepository
{
    Task<IReadOnlyList<Supplier>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Supplier> CreateAsync(Supplier entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Supplier entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsByTaxIdAsync(string taxId, Guid? excludeId, CancellationToken cancellationToken = default);
}

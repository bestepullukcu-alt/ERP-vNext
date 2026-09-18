using Diten.ProcurementService.Domain.Entities;

namespace Diten.ProcurementService.Domain.Repositories;

/// <summary>
/// Supplier repository sözleşmesi. Her metot Tenant + LegalEntity + IsDeleted=false ile filtrelenir
/// (implementasyon zorunluluğu — multi-tenancy.md). Cross-tenant/LE → boş sonuç → handler 404 (sızıntı yok).
/// Soft-delete zorunlu; hard delete YOK. Optimistic concurrency: <see cref="UpdateAsync"/> expectedVersion ile.
/// </summary>
public interface ISupplierRepository
{
    /// <summary>Tenant+LE filtreli liste; opsiyonel status filtresi (contract listSuppliers).</summary>
    Task<IReadOnlyList<Supplier>> GetAllAsync(SupplierStatus? status = null, CancellationToken cancellationToken = default);

    /// <summary>İç Mongo Id ile getirir (tenant+LE filtreli).</summary>
    Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Public SupplierId kodu ile getirir (contract path {supplierId}; tenant+LE filtreli).</summary>
    Task<Supplier?> GetBySupplierIdAsync(string supplierId, CancellationToken cancellationToken = default);

    /// <summary>Idempotency-Key ile create replay tespiti (tenant+LE filtreli).</summary>
    Task<Supplier?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>Toplu public id doğrulama (validateSuppliers) — bilinen kayıtları döner (tenant+LE filtreli).</summary>
    Task<IReadOnlyList<Supplier>> GetBySupplierIdsAsync(IReadOnlyList<string> supplierIds, CancellationToken cancellationToken = default);

    Task<Supplier> CreateAsync(Supplier entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Optimistic concurrency update: yalnız Version == <paramref name="expectedVersion"/> iken uygular ve
    /// Version'ı bir artırır. Version uyuşmazsa (stale) hiçbir şey yazılmaz → false → handler 409.
    /// </summary>
    Task<bool> UpdateAsync(Supplier entity, int expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>Soft delete (iç Mongo Id).</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Toplu soft delete (iç Mongo Id listesi).</summary>
    Task<int> BulkDeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);

    Task<bool> ExistsByTaxIdAsync(string taxId, Guid? excludeId, CancellationToken cancellationToken = default);
}

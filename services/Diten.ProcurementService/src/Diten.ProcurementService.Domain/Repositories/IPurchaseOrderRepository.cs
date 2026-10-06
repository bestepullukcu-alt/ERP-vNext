using Diten.ProcurementService.Domain.Entities;

namespace Diten.ProcurementService.Domain.Repositories;

/// <summary>
/// Purchase Order repository sözleşmesi (MOD-0141). Her metot Tenant + LegalEntity + IsDeleted=false ile filtrelenir
/// (implementasyon zorunluluğu — multi-tenancy.md). Cross-tenant/LE → boş sonuç → handler 404 (sızıntı yok).
/// Soft-delete zorunlu; hard delete YOK. State geçişi (approve) <see cref="UpdateAsync"/> ile optimistic concurrency
/// altında yürür.
/// </summary>
public interface IPurchaseOrderRepository
{
    /// <summary>Tenant+LE filtreli liste; opsiyonel supplierId + status filtresi (contract listPurchaseOrders).</summary>
    Task<IReadOnlyList<PurchaseOrder>> GetAllAsync(string? supplierId = null, PoStatus? status = null, CancellationToken cancellationToken = default);

    /// <summary>İç Mongo Id ile getirir (tenant+LE filtreli).</summary>
    Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Public PoId kodu ile getirir (contract path {poId}; tenant+LE filtreli).</summary>
    Task<PurchaseOrder?> GetByPoIdAsync(string poId, CancellationToken cancellationToken = default);

    /// <summary>Idempotency-Key ile create replay tespiti (tenant+LE filtreli).</summary>
    Task<PurchaseOrder?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    Task<PurchaseOrder> CreateAsync(PurchaseOrder entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Optimistic concurrency update (state geçişi): yalnız Version == <paramref name="expectedVersion"/> iken
    /// uygular ve Version'ı bir artırır. Version uyuşmazsa hiçbir şey yazılmaz → false → handler 409.
    /// </summary>
    Task<bool> UpdateAsync(PurchaseOrder entity, int expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>Soft delete (iç Mongo Id).</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Toplu soft delete (iç Mongo Id listesi).</summary>
    Task<int> BulkDeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);
}

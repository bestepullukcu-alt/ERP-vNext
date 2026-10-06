using Diten.ProcurementService.Domain.Entities;

namespace Diten.ProcurementService.Domain.Repositories;

/// <summary>
/// Goods Receipt (GRN) repository sözleşmesi (MOD-0142). Her metot Tenant + LegalEntity + IsDeleted=false ile
/// filtrelenir (implementasyon zorunluluğu — multi-tenancy.md). Cross-tenant/LE → boş sonuç → handler 404 (sızıntı
/// yok). Soft-delete zorunlu; hard delete YOK. Idempotency-Key ile create replay tespiti (mükerrer INVENTORY
/// hareketi önlenir). GRN hiçbir stok balance tutmaz — yalnız mal kabul dokümanı + INVENTORY transaction referansı.
/// </summary>
public interface IGrnRepository
{
    /// <summary>Tenant+LE filtreli liste; opsiyonel poId + status filtresi.</summary>
    Task<IReadOnlyList<GoodsReceipt>> GetAllAsync(string? poId = null, GrnStatus? status = null, CancellationToken cancellationToken = default);

    /// <summary>İç Mongo Id ile getirir (tenant+LE filtreli).</summary>
    Task<GoodsReceipt?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Public GrnId kodu ile getirir (contract path {grnId}; tenant+LE filtreli).</summary>
    Task<GoodsReceipt?> GetByGrnIdAsync(string grnId, CancellationToken cancellationToken = default);

    /// <summary>Idempotency-Key ile create replay tespiti (tenant+LE filtreli) — mükerrer INVENTORY post önlenir.</summary>
    Task<GoodsReceipt?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    Task<GoodsReceipt> CreateAsync(GoodsReceipt entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Optimistic concurrency update (state geçişi: reverse): yalnız Version == <paramref name="expectedVersion"/>
    /// iken uygular ve Version'ı bir artırır. Version uyuşmazsa hiçbir şey yazılmaz → false → handler 409.
    /// </summary>
    Task<bool> UpdateAsync(GoodsReceipt entity, int expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>Soft delete (iç Mongo Id).</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Toplu soft delete (iç Mongo Id listesi).</summary>
    Task<int> BulkDeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);
}

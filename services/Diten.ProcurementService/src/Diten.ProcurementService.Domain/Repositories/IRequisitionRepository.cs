using Diten.ProcurementService.Domain.Entities;

namespace Diten.ProcurementService.Domain.Repositories;

/// <summary>
/// Requisition repository sözleşmesi (MOD-0141). Her metot Tenant + LegalEntity + IsDeleted=false ile filtrelenir
/// (implementasyon zorunluluğu — multi-tenancy.md). Cross-tenant/LE → boş sonuç → handler 404 (sızıntı yok).
/// Soft-delete zorunlu; hard delete YOK. State geçişi (submit) <see cref="UpdateAsync"/> ile optimistic concurrency
/// altında yürür.
/// </summary>
public interface IRequisitionRepository
{
    /// <summary>Tenant+LE filtreli liste; opsiyonel status filtresi (contract listRequisitions).</summary>
    Task<IReadOnlyList<Requisition>> GetAllAsync(RequisitionStatus? status = null, CancellationToken cancellationToken = default);

    /// <summary>İç Mongo Id ile getirir (tenant+LE filtreli).</summary>
    Task<Requisition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Public RequisitionId kodu ile getirir (contract path {requisitionId}; tenant+LE filtreli).</summary>
    Task<Requisition?> GetByRequisitionIdAsync(string requisitionId, CancellationToken cancellationToken = default);

    /// <summary>Idempotency-Key ile create replay tespiti (tenant+LE filtreli).</summary>
    Task<Requisition?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    Task<Requisition> CreateAsync(Requisition entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Optimistic concurrency update (state geçişi): yalnız Version == <paramref name="expectedVersion"/> iken
    /// uygular ve Version'ı bir artırır. Version uyuşmazsa hiçbir şey yazılmaz → false → handler 409.
    /// </summary>
    Task<bool> UpdateAsync(Requisition entity, int expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>Soft delete (iç Mongo Id).</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Toplu soft delete (iç Mongo Id listesi).</summary>
    Task<int> BulkDeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);
}

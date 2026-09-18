using Diten.ProcurementService.Domain.Entities;

namespace Diten.ProcurementService.Domain.Repositories;

/// <summary>
/// Invoice + Match-Exception repository sözleşmesi (MOD-0143). Tek yazıcı (K15); Invoice ve MatchException
/// koleksiyonlarını sahiplenir. HER metot Tenant + LegalEntity + IsDeleted=false ile filtrelenir (implementasyon
/// zorunluluğu — multi-tenancy.md). Cross-tenant/LE → boş sonuç → handler 404 (sızıntı yok). Soft-delete zorunlu;
/// hard delete YOK. Idempotency-Key ile capture/match/resolve replay tespiti (yan etki yok). Duplicate lookup
/// (SupplierId+InvoiceNumber) → 409. Exception kuyruğu reasonCode + cursor filtreli listelenir.
/// </summary>
public interface IInvoiceMatchRepository
{
    // ── Invoice ────────────────────────────────────────────────────────────────
    /// <summary>Tenant+LE filtreli liste; opsiyonel supplierId + status filtresi.</summary>
    Task<IReadOnlyList<Invoice>> GetAllAsync(string? supplierId = null, InvoiceStatus? status = null, CancellationToken cancellationToken = default);

    /// <summary>İç Mongo Id ile getirir (tenant+LE filtreli).</summary>
    Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Public InvoiceId kodu ile getirir (contract path {invoiceId}; tenant+LE filtreli).</summary>
    Task<Invoice?> GetByInvoiceIdAsync(string invoiceId, CancellationToken cancellationToken = default);

    /// <summary>Idempotency-Key ile capture replay tespiti (tenant+LE filtreli).</summary>
    Task<Invoice?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>Duplicate tespiti: (SupplierId+InvoiceNumber) tenant+LE bazında var mı → 409 DUPLICATE_INVOICE.</summary>
    Task<bool> ExistsByInvoiceNumberAsync(string supplierId, string invoiceNumber, CancellationToken cancellationToken = default);

    Task<Invoice> CreateAsync(Invoice entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Optimistic concurrency update (state geçişi: match/resolve): yalnız Version == <paramref name="expectedVersion"/>
    /// iken uygular ve Version'ı bir artırır. Version uyuşmazsa hiçbir şey yazılmaz → false → handler 409.
    /// </summary>
    Task<bool> UpdateAsync(Invoice entity, int expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>Soft delete (iç Mongo Id).</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Toplu soft delete (iç Mongo Id listesi).</summary>
    Task<int> BulkDeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);

    // ── Match Exception kuyruğu ──────────────────────────────────────────────────
    Task<MatchException> CreateExceptionAsync(MatchException entity, CancellationToken cancellationToken = default);

    /// <summary>Public ExceptionId kodu ile getirir (contract path {exceptionId}; tenant+LE filtreli).</summary>
    Task<MatchException?> GetExceptionByExceptionIdAsync(string exceptionId, CancellationToken cancellationToken = default);

    /// <summary>Resolve Idempotency-Key ile replay tespiti (tenant+LE filtreli).</summary>
    Task<MatchException?> GetExceptionByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>Exception kuyruğu — opsiyonel reasonCode filtresi (tenant+LE filtreli; ExceptionId sıralı).</summary>
    Task<IReadOnlyList<MatchException>> ListExceptionsAsync(MatchExceptionReason? reasonCode = null, CancellationToken cancellationToken = default);

    /// <summary>Exception state geçişi (resolve) optimistic concurrency altında.</summary>
    Task<bool> UpdateExceptionAsync(MatchException entity, int expectedVersion, CancellationToken cancellationToken = default);
}

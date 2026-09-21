using Diten.ProcurementService.Domain.Entities;

namespace Diten.ProcurementService.Domain.Repositories;

/// <summary>
/// Sourcing (RFx + Bid) repository sözleşmesi (MOD-0145). Her metot Tenant + LegalEntity + IsDeleted=false ile
/// filtrelenir (implementasyon zorunluluğu — multi-tenancy.md). Cross-tenant/LE → boş sonuç → handler 404 (sızıntı yok).
/// Soft-delete zorunlu; hard delete YOK. State geçişleri (publish/award) <see cref="UpdateRfxAsync"/> ile optimistic
/// concurrency altında yürür. Bid'ler RFx'e bağlı alt kaynaktır; aynı repository yönetir.
/// </summary>
public interface IRfxRepository
{
    // ── RFx events ───────────────────────────────────────────────────────────
    /// <summary>Tenant+LE filtreli liste; opsiyonel status filtresi (contract listRfxEvents).</summary>
    Task<IReadOnlyList<RfxEvent>> GetAllAsync(RfxStatus? status = null, CancellationToken cancellationToken = default);

    /// <summary>İç Mongo Id ile getirir (tenant+LE filtreli).</summary>
    Task<RfxEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Public RfxId kodu ile getirir (contract path {rfxId}; tenant+LE filtreli).</summary>
    Task<RfxEvent?> GetByRfxIdAsync(string rfxId, CancellationToken cancellationToken = default);

    /// <summary>Idempotency-Key ile create replay tespiti (tenant+LE filtreli).</summary>
    Task<RfxEvent?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    Task<RfxEvent> CreateAsync(RfxEvent entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Optimistic concurrency update (state geçişleri): yalnız Version == <paramref name="expectedVersion"/> iken
    /// uygular ve Version'ı bir artırır. Version uyuşmazsa hiçbir şey yazılmaz → false.
    /// </summary>
    Task<bool> UpdateRfxAsync(RfxEvent entity, int expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>Soft delete (iç Mongo Id).</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Toplu soft delete (iç Mongo Id listesi).</summary>
    Task<int> BulkDeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);

    // ── Bids (RFx alt kaynağı) ─────────────────────────────────────────────────
    Task<Bid> CreateBidAsync(Bid entity, CancellationToken cancellationToken = default);

    /// <summary>Bir RFx'in teklifleri (tenant+LE filtreli; contract listBids).</summary>
    Task<IReadOnlyList<Bid>> GetBidsByRfxIdAsync(string rfxId, CancellationToken cancellationToken = default);

    /// <summary>Public BidId ile teklif getirir (award bid ownership gate; tenant+LE filtreli).</summary>
    Task<Bid?> GetBidByBidIdAsync(string bidId, CancellationToken cancellationToken = default);

    /// <summary>Idempotency-Key ile bid replay tespiti (tenant+LE filtreli).</summary>
    Task<Bid?> GetBidByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
}

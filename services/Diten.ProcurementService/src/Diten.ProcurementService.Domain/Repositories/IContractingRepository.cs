using Diten.ProcurementService.Domain.Entities;

namespace Diten.ProcurementService.Domain.Repositories;

/// <summary>
/// Contract + Clause-library repository sözleşmesi (MOD-0144). Tek yazıcı (K15); Contract ve Clause koleksiyonlarını
/// sahiplenir. HER metot Tenant + LegalEntity + IsDeleted=false ile filtrelenir (implementasyon zorunluluğu —
/// multi-tenancy.md). Cross-tenant/LE → boş sonuç → handler 404 (sızıntı yok). Soft-delete zorunlu; hard delete YOK.
/// Idempotency-Key ile create/activate/createClause replay tespiti (yan etki yok). Duplicate clause lookup
/// (Category+Title) → 409. Clause IMMUTABLE (ASSUMPTION-0144-03): clause update/delete YOK.
/// </summary>
public interface IContractingRepository
{
    // ── Contract ─────────────────────────────────────────────────────────────
    /// <summary>Tenant+LE filtreli liste; opsiyonel supplierId + status filtresi (contract listContracts).</summary>
    Task<IReadOnlyList<Contract>> GetAllAsync(string? supplierId = null, ContractStatus? status = null, CancellationToken cancellationToken = default);

    /// <summary>İç Mongo Id ile getirir (tenant+LE filtreli).</summary>
    Task<Contract?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Public ContractId kodu ile getirir (contract path {contractId}; tenant+LE filtreli).</summary>
    Task<Contract?> GetByContractIdAsync(string contractId, CancellationToken cancellationToken = default);

    /// <summary>Create Idempotency-Key ile replay tespiti (tenant+LE filtreli).</summary>
    Task<Contract?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    Task<Contract> CreateAsync(Contract entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Optimistic concurrency update (state geçişi: activate/soft-delete-hazırlık): yalnız Version ==
    /// <paramref name="expectedVersion"/> iken uygular ve Version'ı bir artırır. Version uyuşmazsa hiçbir şey yazılmaz
    /// → false → handler 409.
    /// </summary>
    Task<bool> UpdateAsync(Contract entity, int expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>Soft delete (iç Mongo Id).</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Toplu soft delete (iç Mongo Id listesi).</summary>
    Task<int> BulkDeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);

    // ── Clause library ────────────────────────────────────────────────────────
    Task<Clause> CreateClauseAsync(Clause entity, CancellationToken cancellationToken = default);

    /// <summary>Public ClauseId kodu ile getirir (tenant+LE filtreli).</summary>
    Task<Clause?> GetClauseByClauseIdAsync(string clauseId, CancellationToken cancellationToken = default);

    /// <summary>Clause create Idempotency-Key ile replay tespiti (tenant+LE filtreli).</summary>
    Task<Clause?> GetClauseByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>Duplicate tespiti: (Category+Title) tenant+LE bazında var mı → 409 DUPLICATE_CLAUSE.</summary>
    Task<bool> ExistsClauseByCategoryTitleAsync(string category, string title, CancellationToken cancellationToken = default);

    /// <summary>Contract clause referansı doğrulama: ClauseId clause library'de var mı (tenant+LE filtreli).</summary>
    Task<bool> ExistsClauseAsync(string clauseId, CancellationToken cancellationToken = default);

    /// <summary>Clause library — opsiyonel category filtresi (tenant+LE filtreli; ClauseId sıralı).</summary>
    Task<IReadOnlyList<Clause>> ListClausesAsync(string? category = null, CancellationToken cancellationToken = default);
}

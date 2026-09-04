using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

public interface ITerritoryModelRepository
{
    Task<TerritoryModel?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsByCodeAsync(Guid tenantId, string modelCode, Guid? excludeId, CancellationToken cancellationToken);

    Task<(IReadOnlyList<TerritoryModel> Items, long Total)> ListAsync(
        Guid tenantId, string? search, string? status, int page, int pageSize, CancellationToken cancellationToken);

    Task<IReadOnlyList<TerritoryModel>> ListActiveAsync(Guid tenantId, Guid excludeId, CancellationToken cancellationToken);

    /// <summary>MOD-0151 FU05A — bulk model lookup for the current-coverage lifecycle guard. Returns the
    /// non-soft-deleted models of the tenant whose ids are requested; status and effective-window evaluation is done
    /// in memory by <c>TerritoryCoverageLifecyclePolicy</c> so no DateTimeOffset (BSON array) ever enters a Mongo
    /// range filter.</summary>
    Task<IReadOnlyList<TerritoryModel>> ListByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>Non-soft-deleted models of the tenant whose <c>CountryScope</c> is one of <paramref name="countryScopes"/>
    /// (case-insensitive) — resolves the owning models for the Accounts-grid Country Scope filter. The operational
    /// validity (status/effective-window) is still evaluated in memory by <c>TerritoryCoverageLifecyclePolicy</c>, so
    /// this returns every scope-matching model regardless of status.</summary>
    Task<IReadOnlyList<TerritoryModel>> ListByCountryScopesAsync(Guid tenantId, IReadOnlyCollection<string> countryScopes, CancellationToken cancellationToken);

    Task InsertAsync(TerritoryModel model, CancellationToken cancellationToken);

    Task UpdateAsync(TerritoryModel model, CancellationToken cancellationToken);
}

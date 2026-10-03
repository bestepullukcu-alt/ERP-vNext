using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Repositories;

public interface IModuleCatalogRepository
{
    Task<ModuleCatalogItem> CreateAsync(ModuleCatalogItem item, CancellationToken ct = default);
    Task<ModuleCatalogItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ModuleCatalogItem?> GetByCodeAsync(string moduleCode, CancellationToken ct = default);
    /// <summary>
    /// BL-500 — the records <see cref="GetByCodeAsync(string, CancellationToken)"/> would return for each code, in ONE
    /// read; a code with no record is absent from the answer. The default asks one by one so every double of this
    /// interface keeps working; the Mongo repository answers with a single query.
    /// </summary>
    async Task<IReadOnlyDictionary<string, ModuleCatalogItem>> GetByCodesAsync(IReadOnlyCollection<string> moduleCodes, CancellationToken ct = default)
    {
        var found = new Dictionary<string, ModuleCatalogItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in moduleCodes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var item = await GetByCodeAsync(code, ct);
            if (item is not null)
            {
                found[code] = item;
            }
        }

        return found;
    }
    Task<ModuleCatalogItem?> GetByCodeIncludingDeletedAsync(string moduleCode, CancellationToken ct = default) =>
        GetByCodeAsync(moduleCode, ct);
    Task<bool> ExistsByCodeAsync(string moduleCode, Guid? excludeId = null, CancellationToken ct = default);
    Task UpdateAsync(ModuleCatalogItem item, CancellationToken ct = default);
    Task RestoreAsync(ModuleCatalogItem item, CancellationToken ct = default) => UpdateAsync(item, ct);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<ModuleCatalogItem> Items, long TotalCount)> QueryAsync(ModuleCatalogQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<ModuleCatalogItem>> GetAssignableAsync(CancellationToken ct = default);
    Task<IReadOnlyDictionary<ModuleCatalogStatus, long>> GetStatsAsync(CancellationToken ct = default);
}

public interface ITransactionalModuleCatalogRepository : IModuleCatalogRepository
{
    Task<ModuleCatalogItem> CreateAsync(IPlatformTransactionSession session, ModuleCatalogItem item, CancellationToken ct = default);
    Task<ModuleCatalogItem?> GetByIdAsync(IPlatformTransactionSession session, Guid id, CancellationToken ct = default);
    Task<ModuleCatalogItem?> GetByCodeAsync(IPlatformTransactionSession session, string moduleCode, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(IPlatformTransactionSession session, string moduleCode, Guid? excludeId = null, CancellationToken ct = default);
    Task UpdateAsync(IPlatformTransactionSession session, ModuleCatalogItem item, CancellationToken ct = default);
    Task DeleteAsync(IPlatformTransactionSession session, Guid id, CancellationToken ct = default);
}

public sealed record ModuleCatalogQuery(
    string? Search,
    string? Domain,
    string? Service,
    IReadOnlyCollection<ModuleCatalogStatus>? Statuses,
    bool? IsCoreModule,
    bool? IsTenantAssignable,
    int Page,
    int PageSize,
    string Sort);

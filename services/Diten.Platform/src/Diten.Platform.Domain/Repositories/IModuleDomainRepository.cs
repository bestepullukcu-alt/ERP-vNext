using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Domain.Repositories;

public interface IModuleDomainRepository
{
    Task<ModuleDomain> CreateAsync(ModuleDomain item, CancellationToken ct = default);
    Task<ModuleDomain?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ModuleDomain?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default);
    Task UpdateAsync(ModuleDomain item, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<ModuleDomain> Items, long TotalCount)> QueryAsync(ModuleDomainQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<ModuleDomain>> GetActiveAsync(CancellationToken ct = default);
}

public sealed record ModuleDomainQuery(
    string? Search,
    bool? IsActive,
    int Page,
    int PageSize,
    string Sort);

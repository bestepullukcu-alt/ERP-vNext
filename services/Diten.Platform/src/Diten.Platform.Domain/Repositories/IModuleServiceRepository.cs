using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Domain.Repositories;

public interface IModuleServiceRepository
{
    Task<ModuleService> CreateAsync(ModuleService item, CancellationToken ct = default);
    Task<ModuleService?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ModuleService?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default);
    Task UpdateAsync(ModuleService item, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<ModuleService> Items, long TotalCount)> QueryAsync(ModuleServiceQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<ModuleService>> GetActiveAsync(CancellationToken ct = default);
}

public sealed record ModuleServiceQuery(
    string? Search,
    bool? IsActive,
    int Page,
    int PageSize,
    string Sort);

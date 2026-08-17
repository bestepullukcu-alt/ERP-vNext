using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Repositories;

public interface IModulePageDescriptorRepository
{
    Task<ModulePageDescriptor> CreateAsync(ModulePageDescriptor descriptor, CancellationToken ct = default);
    Task<ModulePageDescriptor?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ModuleExistsAsync(string moduleCode, CancellationToken ct = default);
    Task<bool> ExistsByPageCodeAsync(string moduleCode, string pageCode, Guid? excludeId = null, CancellationToken ct = default);
    Task<bool> ExistsByRoutePathAsync(string moduleCode, string routePath, Guid? excludeId = null, CancellationToken ct = default);
    Task UpdateAsync(ModulePageDescriptor descriptor, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ModulePageDescriptor>> GetByModuleAsync(string moduleCode, CancellationToken ct = default);
    Task<(IReadOnlyList<ModulePageDescriptor> Items, long TotalCount)> SearchAsync(ModulePageDescriptorQuery query, CancellationToken ct = default);
}

public sealed record ModulePageDescriptorQuery(
    string? Search,
    string? ModuleCode,
    IReadOnlyCollection<ModulePageType>? PageTypes,
    IReadOnlyCollection<ModulePageStatus>? Statuses,
    int Page,
    int PageSize,
    string Sort);

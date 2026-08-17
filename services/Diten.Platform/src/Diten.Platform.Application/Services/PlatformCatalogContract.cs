using Diten.Platform.Common.Catalog;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Services;

public sealed class PlatformCatalogContract : IPlatformCatalogContract
{
    private readonly IModuleCatalogRepository moduleCatalogRepository;

    public PlatformCatalogContract(IModuleCatalogRepository moduleCatalogRepository)
    {
        this.moduleCatalogRepository = moduleCatalogRepository ?? throw new ArgumentNullException(nameof(moduleCatalogRepository));
    }

    public async Task<IReadOnlyList<AssignableModuleInfo>> GetAssignableModulesAsync(CancellationToken ct)
    {
        var modules = await moduleCatalogRepository.GetAssignableAsync(ct);
        return modules
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.ModuleCode)
            .Select(ToAssignableModuleInfo)
            .ToList();
    }

    private static AssignableModuleInfo ToAssignableModuleInfo(ModuleCatalogItem item) =>
        new(
            item.Id,
            item.ModuleCode,
            item.ModuleName,
            item.DisplayName,
            item.Description,
            item.Domain,
            item.Service,
            item.Status.ToString(),
            item.ModuleVersion,
            item.IsCoreModule,
            item.IsTenantAssignable,
            item.SortOrder,
            item.CreatedAt,
            item.UpdatedAt);
}

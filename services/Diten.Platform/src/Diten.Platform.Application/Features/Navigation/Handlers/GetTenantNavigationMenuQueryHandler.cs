using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Navigation.Queries;
using Diten.Platform.Application.Services;
using Diten.Platform.Common.Catalog;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Navigation.Handlers;

/// <summary>
/// MOD-0285 — builds the tenant navigation tree. Two scopes are involved: entitlement is evaluated for the
/// tenant (ambient tenant context), then page descriptors are read under the platform scope (Guid.Empty)
/// where self-registration stores them. Best-effort: a module with no visible descriptors is simply omitted,
/// so the menu never breaks when a module ships without nav descriptors.
/// </summary>
public sealed class GetTenantNavigationMenuQueryHandler
    : IRequestHandler<GetTenantNavigationMenuQuery, Response<IReadOnlyList<NavigationModuleGroupDto>>>
{
    private readonly IPlatformCatalogContract _catalogContract;
    private readonly ITenantModuleAccessService _accessService;
    private readonly IModulePageDescriptorRepository _pageRepository;
    private readonly ITenantContext _tenantContext;

    public GetTenantNavigationMenuQueryHandler(
        IPlatformCatalogContract catalogContract,
        ITenantModuleAccessService accessService,
        IModulePageDescriptorRepository pageRepository,
        ITenantContext tenantContext)
    {
        _catalogContract = catalogContract;
        _accessService = accessService;
        _pageRepository = pageRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<NavigationModuleGroupDto>>> Handle(
        GetTenantNavigationMenuQuery request,
        CancellationToken ct)
    {
        // Entitled modules first — evaluated against the tenant (catalog is SortOrder-ordered).
        var modules = await _catalogContract.GetAssignableModulesAsync(ct);
        var entitled = new List<AssignableModuleInfo>();
        foreach (var module in modules)
        {
            if (await _accessService.HasAccessAsync(request.TenantId, module.ModuleCode, ct))
            {
                entitled.Add(module);
            }
        }

        var groups = new List<NavigationModuleGroupDto>();

        // Page descriptors live under the platform scope (Guid.Empty), like the catalog UI / self-registration.
        using (TenantScope.BeginPlatform(_tenantContext, Guid.Empty))
        {
            foreach (var module in entitled)
            {
                var descriptors = await _pageRepository.GetByModuleAsync(module.ModuleCode, ct);
                var items = descriptors
                    .Where(d => d.IsNavigationVisible && d.Status == ModulePageStatus.Active)
                    .OrderBy(d => d.SortOrder)
                    .ThenBy(d => d.PageCode, StringComparer.OrdinalIgnoreCase)
                    .Select(d => new NavigationMenuItemDto(
                        d.PageCode,
                        d.DisplayName,
                        d.RoutePath,
                        d.RequiredPermission,
                        d.ParentPageCode,
                        IconHint: null,
                        d.SortOrder))
                    .ToList();

                if (items.Count > 0)
                {
                    var displayName = !string.IsNullOrWhiteSpace(module.DisplayName)
                        ? module.DisplayName
                        : !string.IsNullOrWhiteSpace(module.ModuleName) ? module.ModuleName : module.ModuleCode;
                    groups.Add(new NavigationModuleGroupDto(module.ModuleCode, displayName, items));
                }
            }
        }

        return Response<IReadOnlyList<NavigationModuleGroupDto>>.Success(groups);
    }
}

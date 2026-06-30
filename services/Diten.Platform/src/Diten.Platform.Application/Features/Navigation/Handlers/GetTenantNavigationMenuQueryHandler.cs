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
    private readonly IModuleDomainRepository _domainRepository;
    private readonly ITenantContext _tenantContext;

    public GetTenantNavigationMenuQueryHandler(
        IPlatformCatalogContract catalogContract,
        ITenantModuleAccessService accessService,
        IModulePageDescriptorRepository pageRepository,
        IModuleDomainRepository domainRepository,
        ITenantContext tenantContext)
    {
        _catalogContract = catalogContract;
        _accessService = accessService;
        _pageRepository = pageRepository;
        _domainRepository = domainRepository;
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

        // Domain code → display name (operator-managed platform_module_domains). Unresolved codes fall back to
        // the raw code so the menu still groups data-drivenly. Global entity — read outside the platform scope.
        // FIX-3b — match FORMAT-tolerantly: the manifest domain ("MasterDataManagement") and the domain row code
        // ("MASTER-DATA-MANAGEMENT") differ only by separators/case, so normalize both sides (strip every
        // non-alphanumeric char + uppercase) before keying. Display-only; grouping/data is unaffected.
        var domains = await _domainRepository.GetActiveAsync(ct);
        var domainNames = domains
            .Where(d => NormalizeDomainKey(d.Code).Length > 0)
            .GroupBy(d => NormalizeDomainKey(d.Code), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().DisplayName, StringComparer.Ordinal);

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

                    var domainCode = module.Domain ?? string.Empty;
                    var domainKey = NormalizeDomainKey(domainCode);
                    var domainDisplay = domainKey.Length > 0
                                        && domainNames.TryGetValue(domainKey, out var dn)
                                        && !string.IsNullOrWhiteSpace(dn)
                        ? dn
                        : (string.IsNullOrWhiteSpace(domainCode) ? "Modules" : domainCode);

                    groups.Add(new NavigationModuleGroupDto(module.ModuleCode, displayName, domainCode, domainDisplay, items));
                }
            }
        }

        return Response<IReadOnlyList<NavigationModuleGroupDto>>.Success(groups);
    }

    // Format-tolerant domain key: drop every non-alphanumeric char (dash/space/dot/underscore) and uppercase,
    // so "MasterDataManagement", "MASTER-DATA-MANAGEMENT" and "master data management" all collapse to the same key.
    private static string NormalizeDomainKey(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[code.Length];
        var length = 0;
        foreach (var ch in code)
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer[length++] = char.ToUpperInvariant(ch);
            }
        }

        return new string(buffer[..length]);
    }
}

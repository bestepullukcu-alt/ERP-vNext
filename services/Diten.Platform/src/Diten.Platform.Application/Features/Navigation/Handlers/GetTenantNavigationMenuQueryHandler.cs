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
    private readonly ITenantNavPreferenceRepository _navPreferenceRepository;
    private readonly ITenantNavDomainPreferenceRepository _navDomainPreferenceRepository;
    private readonly ITenantContext _tenantContext;

    public GetTenantNavigationMenuQueryHandler(
        IPlatformCatalogContract catalogContract,
        ITenantModuleAccessService accessService,
        IModulePageDescriptorRepository pageRepository,
        IModuleDomainRepository domainRepository,
        ITenantNavPreferenceRepository navPreferenceRepository,
        ITenantNavDomainPreferenceRepository navDomainPreferenceRepository,
        ITenantContext tenantContext)
    {
        _catalogContract = catalogContract;
        _accessService = accessService;
        _pageRepository = pageRepository;
        _domainRepository = domainRepository;
        _navPreferenceRepository = navPreferenceRepository;
        _navDomainPreferenceRepository = navDomainPreferenceRepository;
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
        var domainByKey = domains
            .Where(d => NormalizeDomainKey(d.Code).Length > 0)
            .GroupBy(d => NormalizeDomainKey(d.Code), StringComparer.Ordinal)
            // FIX-DOMAIN-DEDUP (defense) — a normalized key should map to exactly ONE row (guaranteed by the
            // dedup migration + the unique index on CodeKey), but pick DETERMINISTICALLY anyway so a stray
            // duplicate can never randomize the domain's display name or SortOrder: active first, then lowest
            // SortOrder, then Code. Without this an arbitrary .First() made the operator's ordering edits flaky.
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(d => d.IsActive)
                      .ThenBy(d => d.SortOrder)
                      .ThenBy(d => d.Code, StringComparer.Ordinal)
                      .First(),
                StringComparer.Ordinal);
        var domainNames = domainByKey.ToDictionary(kv => kv.Key, kv => kv.Value.DisplayName, StringComparer.Ordinal);
        // FIX-DOMAIN-SORT — Domain Management's ModuleDomain.SortOrder is the authoritative BASE order of the
        // domain groups in the sidebar. Keyed format-tolerantly like the display-name map; consumed in
        // ApplyDomainPreferences as the primary sort with module first-appearance as the stable tiebreak.
        var domainSortOrders = domainByKey.ToDictionary(kv => kv.Key, kv => kv.Value.SortOrder, StringComparer.Ordinal);

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

                    // FIX-MODULE-ICON — module sidebar icon from the catalog; "bx-box" fallback when unset (regression-safe).
                    var icon = string.IsNullOrWhiteSpace(module.Icon) ? "bx-box" : module.Icon!.Trim();

                    // FIX-CTRLK-GROUPING+CREATE — expose a NAVIGABLE create route (a compact "Add New" page: route ends
                    // "/Create", no {id}). Non-nav-visible, so it isn't in Items; slim/offcanvas modules have none → null.
                    var createPage = descriptors.FirstOrDefault(d =>
                        d.Status == ModulePageStatus.Active
                        && !string.IsNullOrWhiteSpace(d.RoutePath)
                        && !d.RoutePath.Contains('{')
                        && d.RoutePath.EndsWith("/Create", StringComparison.OrdinalIgnoreCase));

                    groups.Add(new NavigationModuleGroupDto(
                        module.ModuleCode, displayName, domainCode, domainDisplay, items,
                        Icon: icon,
                        CreateRoute: createPage?.RoutePath,
                        CreatePermission: createPage?.RequiredPermission));
                }
            }
        }

        // FEAT-TENANT-NAV-PREFS — apply the tenant's per-module preferences over the catalog-ordered groups:
        // hide, rename, reorder. A module with NO preference row keeps the default behavior (catalog order, real
        // display name, visible), so a tenant with no preferences sees exactly the unchanged menu.
        var preferences = await _navPreferenceRepository.GetByTenantAsync(request.TenantId, ct);
        var moduleGroups = ApplyPreferences(groups, preferences);

        // FEAT-NAVPREFS-DOMAINS — stamp each group's effective DomainSortOrder (domain pref override else implicit
        // catalog rank) and apply any domain rename. The frontend orders domain groups by DomainSortOrder.
        var domainPreferences = await _navDomainPreferenceRepository.GetByTenantAsync(request.TenantId, ct);
        var finalGroups = ApplyDomainPreferences(moduleGroups, domainPreferences, domainSortOrders);

        return Response<IReadOnlyList<NavigationModuleGroupDto>>.Success(finalGroups);
    }

    private static IReadOnlyList<NavigationModuleGroupDto> ApplyDomainPreferences(
        IReadOnlyList<NavigationModuleGroupDto> groups,
        IReadOnlyList<Domain.Entities.TenantNavDomainPreference> domainPreferences,
        IReadOnlyDictionary<string, int> domainSortOrders)
    {
        if (groups.Count == 0)
        {
            return groups;
        }

        // First-appearance order of each domain key in the (module-ordered) groups. Used only as a stable
        // tiebreak below when two domains share the same platform SortOrder.
        var firstAppearance = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var g in groups)
        {
            var key = NormalizeDomainKey(g.Domain);
            if (!firstAppearance.ContainsKey(key))
            {
                firstAppearance[key] = firstAppearance.Count;
            }
        }

        // FIX-DOMAIN-SORT — the base rank of each domain group is now driven by Domain Management's
        // ModuleDomain.SortOrder (authoritative), with module first-appearance as the tiebreak. Sort the distinct
        // domain keys by (platform SortOrder ASC, first-appearance ASC) and assign sequential ranks; the frontend's
        // single-int DomainSortOrder then reflects that ordering. Domains with no platform row sort after those
        // that have one (int.MaxValue), still tie-broken by first-appearance. Tenant prefs override below.
        var implicitRank = new Dictionary<string, int>(StringComparer.Ordinal);
        var rankIndex = 0;
        foreach (var key in firstAppearance.Keys
            .OrderBy(k => domainSortOrders.TryGetValue(k, out var so) ? so : int.MaxValue)
            .ThenBy(k => firstAppearance[k]))
        {
            implicitRank[key] = rankIndex++;
        }

        var prefByKey = new Dictionary<string, Domain.Entities.TenantNavDomainPreference>(StringComparer.Ordinal);
        foreach (var p in domainPreferences)
        {
            var key = NormalizeDomainKey(p.DomainCode);
            if (key.Length > 0)
            {
                prefByKey[key] = p;
            }
        }

        return groups.Select(g =>
        {
            var key = NormalizeDomainKey(g.Domain);
            var sort = implicitRank.TryGetValue(key, out var rank) ? rank : 0;
            var display = g.DomainDisplayName;
            var isOverride = false;

            if (prefByKey.TryGetValue(key, out var pref))
            {
                if (pref.SortOrder.HasValue)
                {
                    sort = pref.SortOrder.Value;
                }
                if (!string.IsNullOrWhiteSpace(pref.DisplayNameOverride))
                {
                    display = pref.DisplayNameOverride.Trim();
                    isOverride = true; // FEAT-NAV-L10N — tenant free text; frontend must render it as-typed, not localize.
                }
            }

            return g with { DomainSortOrder = sort, DomainDisplayName = display, DomainDisplayNameIsOverride = isOverride };
        }).ToList();
    }

    private static IReadOnlyList<NavigationModuleGroupDto> ApplyPreferences(
        IReadOnlyList<NavigationModuleGroupDto> groups,
        IReadOnlyList<Domain.Entities.TenantNavPreference> preferences)
    {
        if (preferences.Count == 0)
        {
            return groups; // no preferences → unchanged (regression-safe fast path)
        }

        // Last-writer-wins on duplicate codes (the unique index normally prevents duplicates).
        var prefByCode = new Dictionary<string, Domain.Entities.TenantNavPreference>(StringComparer.OrdinalIgnoreCase);
        foreach (var pref in preferences)
        {
            if (!string.IsNullOrWhiteSpace(pref.ModuleCode))
            {
                prefByCode[pref.ModuleCode] = pref;
            }
        }

        // catalogRank = position in the catalog-ordered groups list. The effective sort key is the preference's
        // SortOrder override when set, else the catalog rank; ties break by catalog rank for stability.
        var ordered = groups
            .Select((group, catalogRank) => (group, catalogRank))
            .Where(x => !(prefByCode.TryGetValue(x.group.ModuleCode, out var p) && p.IsHidden))
            .Select(x =>
            {
                var group = x.group;
                var sortKey = x.catalogRank;
                if (prefByCode.TryGetValue(group.ModuleCode, out var pref))
                {
                    if (!string.IsNullOrWhiteSpace(pref.DisplayNameOverride))
                    {
                        // FEAT-NAV-L10N — tenant free text; frontend renders it as-typed, never localized.
                        group = group with { ModuleDisplayName = pref.DisplayNameOverride.Trim(), ModuleDisplayNameIsOverride = true };
                    }

                    sortKey = pref.SortOrder ?? x.catalogRank;
                }

                return (group, sortKey, x.catalogRank);
            })
            .OrderBy(x => x.sortKey)
            .ThenBy(x => x.catalogRank)
            .Select(x => x.group)
            .ToList();

        return ordered;
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

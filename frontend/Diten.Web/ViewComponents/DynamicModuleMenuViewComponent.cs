using System.Net.Http.Headers;
using System.Text.Json;
using Diten.Web.Services.Auth;
using Diten.Web.Services.Navigation;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.ViewComponents;

// MOD-0285 — renders the dynamic "Modules" navigation section in the tenant shell. AUGMENT only: it sits
// alongside the hardcoded sections and never replaces them. The menu tree is resolved server-side from the
// Platform navigation endpoint using the HttpOnly access token (no client flash, no token in the DOM).
// Best-effort: any failure yields an empty model, so the hardcoded menu keeps working if the endpoint is down.
public sealed class DynamicModuleMenuViewComponent : ViewComponent
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger<DynamicModuleMenuViewComponent> _logger;
    private readonly INavNameLocalizer _navLocalizer;

    public DynamicModuleMenuViewComponent(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<DynamicModuleMenuViewComponent> logger,
        INavNameLocalizer navLocalizer)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
        _logger = logger;
        _navLocalizer = navLocalizer;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var model = await ResolveAsync(HttpContext.RequestAborted);
        return View(model);
    }

    private async Task<DynamicModuleMenuViewModel> ResolveAsync(CancellationToken ct)
    {
        var token = AuthTokenCookies.GetAccessToken(Request);
        if (string.IsNullOrWhiteSpace(token))
        {
            return DynamicModuleMenuViewModel.Empty;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_gatewayUrl}/api/platform/navigation/menu");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var tenantId = GetTenantId();
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                request.Headers.Add("X-Tenant-Id", tenantId);
            }

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Dynamic module menu resolve returned {StatusCode}.", (int)response.StatusCode);
                return DynamicModuleMenuViewModel.Empty;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var payload = await JsonSerializer.DeserializeAsync<NavigationResponse>(stream, JsonOptions, ct);
            var groups = payload?.Data;
            if (groups is null || groups.Count == 0)
            {
                return DynamicModuleMenuViewModel.Empty;
            }

            // FIX-3 — DATA-DRIVEN: group modules by DOMAIN (display name resolved server-side). Each module is one
            // entry; the view links a single-page module straight to its page, or makes a multi-page module a
            // collapsible group. Grouping/order/visibility come entirely from the response (Domain, SortOrder,
            // IsNavigationVisible) — nothing hardcoded; operators manage it from the Module Catalog.
            var moduleEntries = groups
                .Select(g =>
                {
                    var domainCode = g.Domain ?? string.Empty;
                    var serverDomainName = !string.IsNullOrWhiteSpace(g.DomainDisplayName)
                        ? g.DomainDisplayName!
                        : (!string.IsNullOrWhiteSpace(g.Domain) ? g.Domain! : "Modules");
                    var serverModuleName = g.ModuleDisplayName ?? g.ModuleCode ?? string.Empty;

                    // FEAT-NAV-L10N — localize DEFAULT names by stable code (overrides render as-typed via the flags).
                    return new
                    {
                        Domain = domainCode,
                        DomainDisplay = _navLocalizer.Domain(domainCode, serverDomainName, g.DomainDisplayNameIsOverride),
                        // FEAT-NAVPREFS-DOMAINS — effective domain order (tenant override else implicit catalog rank).
                        DomainSort = g.DomainSortOrder,
                        Module = new NavModuleEntryView(
                            _navLocalizer.Module(g.ModuleCode, serverModuleName, g.ModuleDisplayNameIsOverride),
                            BuildTree(g.Items),
                            g.Icon) // FIX-MODULE-ICON — module sidebar icon from the catalog (nav DTO carries the resolved value).
                    };
                })
                .Where(x => x.Module.Nodes.Count > 0)
                .ToList();

            // FEAT-NAVPREFS-DOMAINS — order DOMAIN groups by DomainSortOrder (all modules of a domain share it);
            // modules within stay SortOrder-ordered. OrderBy is stable, so equal ranks keep first-seen order.
            var domains = moduleEntries
                .GroupBy(x => (x.Domain, x.DomainDisplay))
                .Select(dg => new { dg.Key.DomainDisplay, Sort = dg.Min(x => x.DomainSort), Modules = dg.Select(x => x.Module).ToList() })
                .OrderBy(d => d.Sort)
                .Select(d => new NavDomainGroupView(d.DomainDisplay, d.Modules))
                .ToList();

            return new DynamicModuleMenuViewModel(domains);
        }
        catch (Exception ex)
        {
            // Best-effort: a broken nav endpoint must not break the shell.
            _logger.LogDebug(ex, "Dynamic module menu resolve failed; rendering empty section.");
            return DynamicModuleMenuViewModel.Empty;
        }
    }

    // Flat descriptors → one level of parent/child nesting via ParentPageCode. Items whose ParentPageCode
    // matches no in-scope parent are treated as top-level (best-effort; never drop an entitled page).
    private IReadOnlyList<NavNodeView> BuildTree(IReadOnlyList<NavigationItem>? items)
    {
        if (items is null || items.Count == 0)
        {
            return Array.Empty<NavNodeView>();
        }

        var byParent = items
            .Where(i => !string.IsNullOrWhiteSpace(i.ParentPageCode))
            .GroupBy(i => i.ParentPageCode!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var parentCodes = new HashSet<string>(
            items.Select(i => i.PageCode).Where(c => !string.IsNullOrWhiteSpace(c))!,
            StringComparer.OrdinalIgnoreCase);

        var topLevel = items.Where(i =>
            string.IsNullOrWhiteSpace(i.ParentPageCode) || !parentCodes.Contains(i.ParentPageCode!));

        return topLevel.Select(i => ToNode(i, byParent)).ToList();
    }

    private NavNodeView ToNode(NavigationItem item, IReadOnlyDictionary<string, List<NavigationItem>> byParent)
    {
        var children = byParent.TryGetValue(item.PageCode, out var kids)
            ? kids.Select(k => ToNode(k, byParent)).ToList()
            : new List<NavNodeView>();

        // FEAT-NAV-L10N — pages have no per-page override, so they're always localizable by PageCode (default fallback).
        return new NavNodeView(
            _navLocalizer.Page(item.PageCode, item.DisplayName ?? item.PageCode ?? string.Empty),
            item.RoutePath ?? "#",
            item.RequiredPermission,
            item.IconHint,
            children);
    }

    private string? GetTenantId() =>
        UserClaimsPrincipal?.Claims.FirstOrDefault(x =>
            x.Type == "tenantId" ||
            x.Type == "tenant_id" ||
            x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;

    private sealed record NavigationResponse(IReadOnlyList<NavigationGroup>? Data);

    private sealed record NavigationGroup(
        string? ModuleCode,
        string? ModuleDisplayName,
        string? Domain,
        string? DomainDisplayName,
        IReadOnlyList<NavigationItem>? Items,
        int DomainSortOrder = 0,
        string? Icon = null, // FIX-MODULE-ICON — module sidebar icon (boxicons class) from the platform nav DTO.
        // FEAT-NAV-L10N — true when the name is a tenant override (render as-typed); false → localize by code.
        bool ModuleDisplayNameIsOverride = false,
        bool DomainDisplayNameIsOverride = false);

    private sealed record NavigationItem(
        string PageCode,
        string? DisplayName,
        string? RoutePath,
        string? RequiredPermission,
        string? ParentPageCode,
        string? IconHint,
        int SortOrder);
}

public sealed record DynamicModuleMenuViewModel(IReadOnlyList<NavDomainGroupView> Domains)
{
    public static readonly DynamicModuleMenuViewModel Empty = new(Array.Empty<NavDomainGroupView>());

    public bool HasItems => Domains.Count > 0;
}

// FIX-3 — DOMAIN is the menu group; each module is one entry under it.
public sealed record NavDomainGroupView(string DomainDisplayName, IReadOnlyList<NavModuleEntryView> Modules);

public sealed record NavModuleEntryView(string ModuleDisplayName, IReadOnlyList<NavNodeView> Nodes, string? Icon = null)
{
    // A module with exactly one nav-visible page (no sub-pages) links straight to that page (its name is the
    // module name, the page name is not shown separately). Anything else renders as a collapsible module group.
    public bool IsSinglePage => Nodes.Count == 1 && Nodes[0].Children.Count == 0;
}

public sealed record NavNodeView(
    string DisplayName,
    string RoutePath,
    string? RequiredPermission,
    string? IconHint,
    IReadOnlyList<NavNodeView> Children);

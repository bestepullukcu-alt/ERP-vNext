using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

// FEAT-CTRLK-DYNAMIC-SEARCH-S1 — feeds the tenant shell's Ctrl+K (Algolia autocomplete) from the tenant's OWN
// data-driven navigation instead of a hand-maintained static JSON. It fetches the same entitlement- + permission-
// filtered menu the sidebar uses (GET /api/platform/navigation/menu, tenant bearer + X-Tenant-Id) and reshapes it
// into the search schema { navigation: { "<Module>": [ { name, url, group, icon, keywords } ] } }. So any new
// self-registered, nav-visible module page becomes searchable automatically — no JSON edit. Thin proxy/transform,
// No-ViewModel; the backend stays authoritative (only nav-visible pages the user can reach are returned).
[Authorize]
[Route("TenantSearch")]
public sealed class TenantSearchController : Controller
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly Diten.Web.Services.IPermissionSnapshot _permissions;
    private readonly ILogger<TenantSearchController> _logger;

    public TenantSearchController(
        HttpClient httpClient,
        IConfiguration configuration,
        Diten.Web.Services.IPermissionSnapshot permissions,
        ILogger<TenantSearchController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
        _permissions = permissions;
        _logger = logger;
    }

    // Cap for the empty-query quick-nav palette (main.js renders `suggestions` before the user types). The tenant
    // nav is small, but this keeps the palette compact if a tenant has many entitled modules.
    private const int SuggestionsCap = 12;

    [HttpGet("data")]
    public async Task<IActionResult> Data()
    {
        var groups = await FetchMenuAsync(HttpContext.RequestAborted);
        var navigation = BuildSearchSections(groups);
        // FIX-CTRLK-TENANT-SUGGESTIONS — main.js shows `suggestions` on Ctrl+K open (before typing) and filters
        // `navigation` once the user types. Feed BOTH from the same access-filtered nav so the palette isn't empty.
        var suggestions = BuildSuggestions(navigation, SuggestionsCap);

        // Short private cache — the menu is per-tenant/per-user and changes rarely; mirrors the sidebar's freshness.
        Response.Headers.CacheControl = "private, max-age=30";
        return Json(new { navigation, suggestions });
    }

    // Section-grouped quick-nav preview: the same access-filtered items as `navigation`, in section order, capped to
    // a total so the open-state palette stays compact. Empty sections are dropped.
    private static IReadOnlyDictionary<string, List<SearchItem>> BuildSuggestions(
        IReadOnlyDictionary<string, List<SearchItem>> navigation, int cap)
    {
        var suggestions = new Dictionary<string, List<SearchItem>>(StringComparer.Ordinal);
        var remaining = cap;
        foreach (var (section, items) in navigation)
        {
            if (remaining <= 0) break;
            var take = items.Take(remaining).ToList();
            if (take.Count == 0) continue;
            suggestions[section] = take;
            remaining -= take.Count;
        }

        return suggestions;
    }

    private async Task<IReadOnlyList<NavigationGroup>> FetchMenuAsync(CancellationToken ct)
    {
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (string.IsNullOrWhiteSpace(token))
        {
            // No token → empty search (never force a logout here; consistent with the login-bridge resilience fix).
            return Array.Empty<NavigationGroup>();
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
                _logger.LogDebug("Tenant search data resolve returned {StatusCode}.", (int)response.StatusCode);
                return Array.Empty<NavigationGroup>();
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var payload = await JsonSerializer.DeserializeAsync<NavigationResponse>(stream, JsonOptions, ct);
            return payload?.Data ?? Array.Empty<NavigationGroup>();
        }
        catch (Exception ex)
        {
            // Best-effort: a broken nav endpoint must not break Ctrl+K (it just yields no dynamic results).
            _logger.LogDebug(ex, "Tenant search data resolve failed; returning empty search set.");
            return Array.Empty<NavigationGroup>();
        }
    }

    // Reshape the nav groups into the search schema: one SECTION per DOMAIN (matching the sidebar's domain headers,
    // so single-page modules don't spawn a redundant one-item section). One ITEM per nav-visible page the CURRENT
    // USER may reach (permission-gated, mirroring the sidebar's per-item Perms.Has gate). A single-page module
    // contributes one item named after the module. keywords fold in the module code/name + domain + page name so
    // "roles", "access governance" and "administration" all surface the Roles page. Plus, for a module that has a
    // navigable create route (compact "Add New") and the user holds its create permission, a "Create <Module>"
    // shortcut is added to the same domain section (offcanvas-only modules have no create route → skipped).
    private IReadOnlyDictionary<string, List<SearchItem>> BuildSearchSections(IReadOnlyList<NavigationGroup> groups)
    {
        var sections = new Dictionary<string, List<SearchItem>>(StringComparer.Ordinal);

        List<SearchItem> SectionFor(string section) =>
            sections.TryGetValue(section, out var existing) ? existing : sections[section] = new List<SearchItem>();

        foreach (var group in groups)
        {
            var pages = (group.Items ?? new List<NavigationItem>())
                .Where(i => !string.IsNullOrWhiteSpace(i.RoutePath) && i.RoutePath != "#")
                .Where(i => CanAccess(i.RequiredPermission)) // per-user gate — never surface a page the user can't reach
                .ToList();

            var moduleName = FirstNonEmpty(group.ModuleDisplayName, group.ModuleCode) ?? string.Empty;
            var domainName = FirstNonEmpty(group.DomainDisplayName, group.Domain) ?? string.Empty;
            var icon = string.IsNullOrWhiteSpace(group.Icon) ? "bx-box" : group.Icon!;
            var section = string.IsNullOrWhiteSpace(domainName)
                ? (string.IsNullOrWhiteSpace(moduleName) ? "Modules" : moduleName)
                : domainName;

            if (pages.Count > 0)
            {
                var list = SectionFor(section);
                if (pages.Count == 1)
                {
                    var only = pages[0];
                    list.Add(new SearchItem(
                        moduleName.Length > 0 ? moduleName : only.DisplayName ?? section,
                        only.RoutePath!,
                        section,
                        icon,
                        Keywords(group.ModuleCode, moduleName, domainName, only.DisplayName)));
                }
                else
                {
                    foreach (var page in pages)
                    {
                        list.Add(new SearchItem(
                            page.DisplayName ?? page.PageCode ?? section,
                            page.RoutePath!,
                            section,
                            icon,
                            Keywords(group.ModuleCode, moduleName, domainName, page.DisplayName)));
                    }
                }
            }

            // Create shortcut — only when a NAVIGABLE create route exists AND the user holds its create permission.
            if (!string.IsNullOrWhiteSpace(group.CreateRoute) && CanAccess(group.CreatePermission))
            {
                var label = moduleName.Length > 0 ? $"Create {moduleName}" : "Create";
                var keywords = new List<string> { "create", "add", "new", "yeni", "ekle" };
                keywords.AddRange(Keywords(group.ModuleCode, moduleName, domainName, null));
                SectionFor(section).Add(new SearchItem(label, group.CreateRoute!, section, "bx-plus-circle", keywords));
            }
        }

        return sections;
    }

    // UX-only per-user gate mirroring the sidebar: a page/action with no required permission is always visible; one
    // with a permission shows only when the current user holds it (backend [HasPermission] remains authoritative).
    private bool CanAccess(string? requiredPermission) =>
        string.IsNullOrWhiteSpace(requiredPermission) || _permissions.Has(requiredPermission);

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static IReadOnlyList<string> Keywords(string? moduleCode, string? moduleName, string? domain, string? pageName)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            set.Add(value.Trim());
            foreach (var token in value.Split(new[] { ' ', '-', '_', '.', '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (token.Length > 1) set.Add(token);
            }
        }

        Add(moduleCode);
        Add(moduleName);
        Add(domain);
        Add(pageName);
        return set.ToList();
    }

    private string? GetTenantId() =>
        User.Claims.FirstOrDefault(x =>
            x.Type == "tenantId" ||
            x.Type == "tenant_id" ||
            x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;

    // Search schema item consumed by main.js (matches platform-search.json's { name, url, group, icon, keywords }).
    private sealed record SearchItem(string Name, string Url, string Group, string Icon, IReadOnlyList<string> Keywords);

    private sealed record NavigationResponse(IReadOnlyList<NavigationGroup>? Data);

    private sealed record NavigationGroup(
        string? ModuleCode,
        string? ModuleDisplayName,
        string? Domain,
        string? DomainDisplayName,
        IReadOnlyList<NavigationItem>? Items,
        int DomainSortOrder = 0,
        string? Icon = null,
        string? CreateRoute = null,
        string? CreatePermission = null);

    private sealed record NavigationItem(
        string? PageCode,
        string? DisplayName,
        string? RoutePath,
        string? RequiredPermission,
        string? ParentPageCode,
        string? IconHint,
        int SortOrder);
}

using System.Net.Http.Headers;
using System.Text.Json;
using Diten.Web.Services.Auth;
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

    public DynamicModuleMenuViewComponent(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<DynamicModuleMenuViewComponent> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
        _logger = logger;
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

            return new DynamicModuleMenuViewModel(groups
                .Select(g => new NavModuleGroupView(
                    g.ModuleDisplayName ?? g.ModuleCode ?? string.Empty,
                    BuildTree(g.Items)))
                .Where(g => g.Nodes.Count > 0)
                .ToList());
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
    private static IReadOnlyList<NavNodeView> BuildTree(IReadOnlyList<NavigationItem>? items)
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

    private static NavNodeView ToNode(NavigationItem item, IReadOnlyDictionary<string, List<NavigationItem>> byParent)
    {
        var children = byParent.TryGetValue(item.PageCode, out var kids)
            ? kids.Select(k => ToNode(k, byParent)).ToList()
            : new List<NavNodeView>();

        return new NavNodeView(
            item.DisplayName ?? item.PageCode ?? string.Empty,
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

    private sealed record NavigationGroup(string? ModuleCode, string? ModuleDisplayName, IReadOnlyList<NavigationItem>? Items);

    private sealed record NavigationItem(
        string PageCode,
        string? DisplayName,
        string? RoutePath,
        string? RequiredPermission,
        string? ParentPageCode,
        string? IconHint,
        int SortOrder);
}

public sealed record DynamicModuleMenuViewModel(IReadOnlyList<NavModuleGroupView> Groups)
{
    public static readonly DynamicModuleMenuViewModel Empty = new(Array.Empty<NavModuleGroupView>());

    public bool HasItems => Groups.Count > 0;
}

public sealed record NavModuleGroupView(string ModuleDisplayName, IReadOnlyList<NavNodeView> Nodes);

public sealed record NavNodeView(
    string DisplayName,
    string RoutePath,
    string? RequiredPermission,
    string? IconHint,
    IReadOnlyList<NavNodeView> Children);

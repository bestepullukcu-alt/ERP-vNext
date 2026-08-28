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

    /*
     * EVERY empty return goes through here, and every one of them logs.
     *
     * This component used to fall back to an empty menu on three separate paths — no token, non-2xx, empty
     * payload — and NONE of them left a trace above Debug. A tenant's entire dynamic sidebar could disappear with
     * not one line in the log at Information, which is exactly what happened: the menu rendered zero groups for a
     * whole session and the only way to find out was to read the HttpClient trace underneath LogDebug.
     *
     * The reason string is the diagnosis, so the three cases must stay distinguishable: `no_token` is a session
     * problem, `http_401` is authentication, `empty_payload` is entitlement or catalogue data. They point at three
     * different teams.
     */
    // The one reason that is NOT a failure: a successful call whose payload is empty by the server's decision.
    private const string EmptyPayloadReason = "empty_payload";

    private DynamicModuleMenuViewModel EmptyBecause(string reason, string? detail = null)
    {
        _logger.LogWarning(
            "dynamic_module_menu.empty Reason={Reason} TenantId={TenantId} CorrelationId={CorrelationId} Detail={Detail}. "
            + "The tenant's dynamic sidebar rendered NO groups.",
            reason,
            GetTenantId() ?? "<none>",
            HttpContext?.TraceIdentifier ?? "<none>",
            detail ?? "-");

        /*
         * BL-294/nav — the reason decides whether the USER is told, not just the log.
         *
         * `empty_payload` is a 200 with nothing in it: the request worked and the SERVER decided this tenant
         * sees no module. Rendering "couldn't load, refresh" there would be a lie, and would nag every tenant
         * that legitimately has no entitled module. It stays silent.
         *
         * Every other reason — no token, a non-2xx, a transport fault — is a genuine failure to load, and the
         * menu's place must SAY so. Silence is what made the original outage take a whole session to find.
         */
        return reason == EmptyPayloadReason
            ? DynamicModuleMenuViewModel.Empty
            : DynamicModuleMenuViewModel.FailedToLoad;
    }

    private async Task<DynamicModuleMenuViewModel> ResolveAsync(CancellationToken ct)
    {
        var token = AuthTokenCookies.GetAccessToken(Request);
        if (string.IsNullOrWhiteSpace(token))
        {
            return EmptyBecause("no_token");
        }

        try
        {
            var tenantId = GetTenantId();

            // Built per attempt: an HttpRequestMessage cannot be sent twice.
            HttpRequestMessage BuildRequest()
            {
                var message = new HttpRequestMessage(HttpMethod.Get, $"{_gatewayUrl}/api/platform/navigation/menu");
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                if (!string.IsNullOrWhiteSpace(tenantId))
                {
                    message.Headers.Add("X-Tenant-Id", tenantId);
                }

                return message;
            }

            // BL-294/nav — ONE silent retry on a transient fault, then give up and let the caller warn. See
            // NavigationRetry for why it is exactly one attempt, with no delay, and only on transient failures.
            using var response = await Services.Http.NavigationRetry.SendOnceMoreOnTransientAsync(_httpClient, BuildRequest, ct);
            if (!response.IsSuccessStatusCode)
            {
                // 401 here is the live defect: the gateway rejects the token intermittently and the menu vanishes.
                // Warning, not Debug — a disappearing menu is not a debug-level event.
                return EmptyBecause($"http_{(int)response.StatusCode}", response.ReasonPhrase);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var payload = await JsonSerializer.DeserializeAsync<NavigationResponse>(stream, JsonOptions, ct);
            var groups = payload?.Data;
            if (groups is null || groups.Count == 0)
            {
                // 200 with nothing in it: the request was fine and the SERVER decided this tenant sees no module.
                // Entitlement or catalogue data, never authentication — which is why it needs its own reason.
                return EmptyBecause(EmptyPayloadReason);
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
                    return new NavDomainEntry(
                        domainCode,
                        _navLocalizer.Domain(domainCode, serverDomainName, g.DomainDisplayNameIsOverride),
                        // FEAT-NAVPREFS-DOMAINS — effective domain order (tenant override else implicit catalog rank).
                        g.DomainSortOrder,
                        new NavModuleEntryView(
                            _navLocalizer.Module(g.ModuleCode, serverModuleName, g.ModuleDisplayNameIsOverride),
                            BuildTree(g.Items),
                            g.Icon)); // FIX-MODULE-ICON — module sidebar icon from the catalog (nav DTO carries the resolved value).
                })
                .Where(x => x.Module.Nodes.Count > 0)
                .ToList();

            return new DynamicModuleMenuViewModel(GroupByDomain(moduleEntries));
        }
        catch (Exception ex)
        {
            // Best-effort STILL means loud: a broken nav endpoint must not break the shell, but it must not be
            // invisible either. This was the fourth silent exit — a deserialization or transport failure took the
            // whole sidebar out at Debug level.
            _logger.LogWarning(ex, "dynamic_module_menu.empty Reason=exception TenantId={TenantId} CorrelationId={CorrelationId}. "
                + "The tenant's dynamic sidebar rendered NO groups.",
                GetTenantId() ?? "<none>", HttpContext?.TraceIdentifier ?? "<none>");
            return DynamicModuleMenuViewModel.FailedToLoad;
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

    /// <summary>One module entry, already localized, tagged with the raw domain code it came from.</summary>
    public sealed record NavDomainEntry(string DomainCode, string DomainDisplay, int DomainSort, NavModuleEntryView Module);

    /// <summary>
    /// FIX-DOMAIN-NORMALIZATION — group modules into DOMAIN sections by the NORMALIZED domain key, never by the raw
    /// code. The catalog historically stored the same domain in two spellings ("MASTER-DATA-MANAGEMENT" vs
    /// "MASTERDATAMANAGEMENT"); both localize to the SAME heading, so grouping on the raw string rendered the
    /// heading TWICE with the modules split across the two sections. <see cref="NavNameLocalizer.Normalize"/> is the
    /// one transform used for the l10n key, the platform nav handler's domain key and the domain lookup's CodeKey —
    /// reuse it here so all four agree.
    ///
    /// <para>The catalog-side canonicalization migration removes the drift at the source; this is the render-side
    /// guarantee that a future drifted row can never split a heading again.</para>
    ///
    /// <para>Display name is picked DETERMINISTICALLY within a key group (lowest DomainSort, then ordinal) so a
    /// group whose members somehow carry different labels cannot flip between renders.</para>
    /// </summary>
    public static IReadOnlyList<NavDomainGroupView> GroupByDomain(IEnumerable<NavDomainEntry> entries) =>
        entries
            .GroupBy(x => NavNameLocalizer.Normalize(x.DomainCode ?? string.Empty), StringComparer.Ordinal)
            .Select(dg => new
            {
                DomainDisplay = dg
                    .OrderBy(x => x.DomainSort)
                    .ThenBy(x => x.DomainDisplay, StringComparer.Ordinal)
                    .First().DomainDisplay,
                Sort = dg.Min(x => x.DomainSort),
                Modules = dg.Select(x => x.Module).ToList()
            })
            // FEAT-NAVPREFS-DOMAINS — order DOMAIN groups by DomainSortOrder (all modules of a domain share it);
            // modules within stay SortOrder-ordered. OrderBy is stable, so equal ranks keep first-seen order.
            .OrderBy(d => d.Sort)
            .Select(d => new NavDomainGroupView(d.DomainDisplay, d.Modules))
            .ToList();

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

// LoadFailed separates "this tenant has no modules" (silent, legitimate) from "we could not load the menu"
// (shown in the menu's place). Only the second is a defect the user can do something about.
public sealed record DynamicModuleMenuViewModel(IReadOnlyList<NavDomainGroupView> Domains, bool LoadFailed = false)
{
    public static readonly DynamicModuleMenuViewModel Empty = new(Array.Empty<NavDomainGroupView>());

    public static readonly DynamicModuleMenuViewModel FailedToLoad =
        new(Array.Empty<NavDomainGroupView>(), LoadFailed: true);

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

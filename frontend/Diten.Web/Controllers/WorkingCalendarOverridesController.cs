using System.Net.Http.Headers;
using Diten.Web.Models.WorkingCalendar;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

/// <summary>
/// Tenant surface for the working-calendar OVERRIDE layer: company holidays, closures and compensation days.
/// <para>
/// <b>The route is deliberately NOT under /Platform/.</b> Self-registration derives permission scope from a page's
/// route, so <c>/WorkingCalendar/Overrides</c> is what makes <c>platform.working-calendar.override.*</c>
/// tenant-assignable despite living in the platform namespace. Moving this page under <c>/Platform/…</c> would lock
/// tenants out of their own calendars.
/// </para>
/// <para>
/// The view folder is <c>Views/WorkingCalendar/Overrides</c> — an area-shaped path — so the page sits where the
/// DataTable contract verifier looks for it, while the tenant-shell route above is unchanged.
/// </para>
/// <para>
/// Every proxied call targets the <c>/overrides</c> endpoints, never the country ones. That is not just convention:
/// the country controller is platform-actor-only and would answer 403. Override reads expose the caller's own rows
/// plus ACTIVE inherited country rows as read-only; writes remain scoped strictly to the caller's own tenant.
/// </para>
/// </summary>
[Route("WorkingCalendar/Overrides")]
public sealed class WorkingCalendarOverridesController : Controller
{
    private const string ApiBase = "api/platform/working-calendars/overrides";

    /// <summary>
    /// The governed MOD-0048 set that supplies this surface's country options. Upper-case is the stored
    /// <c>set_code</c>, not a display choice — the consumer endpoint matches it exactly.
    /// </summary>
    private const string CountryReferenceSetCode = "COUNTRY_CODES";

    /// <summary>Explicit rather than inherited: the reference envelope is camelCase and this must not depend on
    /// whichever defaults <c>ReadFromJsonAsync</c> happens to apply.</summary>
    private static readonly System.Text.Json.JsonSerializerOptions ReferenceJsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger<WorkingCalendarOverridesController> _logger;

    public WorkingCalendarOverridesController(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<WorkingCalendarOverridesController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
        _logger = logger;
    }

    // ── Views (shell only) ───────────────────────────────────────────────────

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/WorkingCalendar/Overrides/Index.cshtml");

    [HttpGet("Create")]
    public IActionResult Create() => View(
        "~/Views/WorkingCalendar/Overrides/Create.cshtml",
        new WorkingCalendarEditViewModel
        {
            CalendarYear = DateTime.UtcNow.Year,
            ScopeType = "tenant",
            IsCountryLayer = false
        });

    [HttpGet("Edit/{id:guid}")]
    public IActionResult Edit(Guid id) => View(
        "~/Views/WorkingCalendar/Overrides/Edit.cshtml",
        new WorkingCalendarEditViewModel { Id = id, IsCountryLayer = false });

    [HttpGet("Details/{id:guid}")]
    public IActionResult Details(Guid id)
    {
        ViewData["CalendarId"] = id;
        return View("~/Views/WorkingCalendar/Overrides/Details.cshtml");
    }

    // ── Same-origin proxy (override endpoints only) ──────────────────────────

    [HttpGet("api")]
    public Task<IActionResult> List(CancellationToken ct)
        => ForwardAsync(HttpMethod.Get, $"{ApiBase}{Request.QueryString}", null, ct);

    /// <summary>The tenant slice of the contract: country scope and country-layer day types are simply not in it.</summary>
    [HttpGet("api/contract")]
    public Task<IActionResult> Contract(CancellationToken ct)
        => ForwardAsync(HttpMethod.Get, $"{ApiBase}/contract", null, ct);

    /// <summary>
    /// Country options for the OVERRIDE surface, served from the governed MOD-0048 <c>COUNTRY_CODES</c> reference
    /// set instead of the platform provisioning lookup.
    /// <para>
    /// <b>Only this surface moved.</b> <c>/Platform/WorkingCalendars</c>, the Legal Entity wizard and Tenants →
    /// Create still read <c>/api/lookups/countries</c>; changing them was explicitly out of scope.
    /// </para>
    /// <para>
    /// <b>The value must stay ISO alpha-2.</b> A row's <c>CountryCode</c> is what makes a tenant override resolve
    /// against the platform country calendar, and <c>CreateWorkingCalendarHandler</c> still validates it against
    /// the platform <c>countries</c> lookup — so a COUNTRY_CODES value absent from that list saves as 400
    /// <c>country_unknown</c>. The two lists must hold the same codes; this proxy does not translate between them.
    /// </para>
    /// <para>
    /// <b>No scope_key.</b> The set is <c>Global</c> scope, and the consumer service rejects a scope key for a
    /// global set (<c>scope_key_not_allowed_for_global</c>). The read carries the caller's own token, so it is
    /// scoped to that caller and cached nowhere — unlike the platform lookup cache, whose key has no tenant
    /// segment, this path cannot serve one tenant's list to another.
    /// </para>
    /// <para>
    /// <b>Fail-closed, never a fallback list.</b> If the set is unpublished or empty the response is an empty
    /// option list: the form opens but cannot save (400 <c>country_unknown</c>). Substituting a hardcoded list here
    /// is forbidden (PSS-LOOKUPS-001) and would let an override be authored against a country the platform layer
    /// does not know.
    /// </para>
    /// </summary>
    [HttpGet("api/countries")]
    public Task<IActionResult> Countries(CancellationToken ct)
        => ForwardReferenceSetAsOptionsAsync(CountryReferenceSetCode, ct);

    [HttpGet("api/organization-units")]
    public Task<IActionResult> OrganizationUnits(CancellationToken ct)
        => ForwardAsync(HttpMethod.Get, "api/platform/organization-units", null, ct);

    /// <summary>
    /// Tenant-scoped ACTIVE/referenceable legal entities from MDM. This authoring lookup is deliberately separate
    /// from the backend's per-id lookup-validation call: choosing an option never substitutes for the fail-closed
    /// validation performed immediately before persistence.
    /// </summary>
    [HttpGet("api/legal-entities")]
    public Task<IActionResult> LegalEntities(CancellationToken ct)
        => ForwardAsync(HttpMethod.Get, "api/legal-entities/lookup", null, ct);

    /// <summary>Resolution over country + this tenant's override, with the reason codes that say which layer won.</summary>
    [HttpGet("api/resolve")]
    public Task<IActionResult> Resolve(CancellationToken ct)
        => ForwardAsync(HttpMethod.Get, $"{ApiBase}/resolve{Request.QueryString}", null, ct);

    [HttpGet("api/{id:guid}")]
    public Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ForwardAsync(HttpMethod.Get, $"{ApiBase}/{id}", null, ct);

    [HttpPost("api")]
    public Task<IActionResult> Create([FromBody] object body, CancellationToken ct)
        => ForwardAsync(HttpMethod.Post, ApiBase, body, ct);

    [HttpPut("api/{id:guid}")]
    public Task<IActionResult> Update(Guid id, [FromBody] object body, CancellationToken ct)
        => ForwardAsync(HttpMethod.Put, $"{ApiBase}/{id}", body, ct);

    [HttpPost("api/{id:guid}/activate")]
    public Task<IActionResult> Activate(Guid id, [FromBody] object body, CancellationToken ct)
        => ForwardAsync(HttpMethod.Post, $"{ApiBase}/{id}/activate", body, ct);

    [HttpPost("api/{id:guid}/archive")]
    public Task<IActionResult> Archive(Guid id, [FromBody] object body, CancellationToken ct)
        => ForwardAsync(HttpMethod.Post, $"{ApiBase}/{id}/archive", body, ct);

    [HttpPost("api/{id:guid}/days")]
    public Task<IActionResult> UpsertDay(Guid id, [FromBody] object body, CancellationToken ct)
        => ForwardAsync(HttpMethod.Post, $"{ApiBase}/{id}/days", body, ct);

    [HttpPost("api/{id:guid}/days/{dayId:guid}/archive")]
    public Task<IActionResult> ArchiveDay(Guid id, Guid dayId, [FromBody] object body, CancellationToken ct)
        => ForwardAsync(HttpMethod.Post, $"{ApiBase}/{id}/days/{dayId}/archive", body, ct);

    /// <summary>
    /// Forwards verbatim and preserves the Gateway status code. 403 (no RBAC grant yet), 400
    /// (a country-layer day type was attempted) and 409 (concurrency) all have to reach the page unflattened —
    /// they are the answers this UI exists to explain.
    /// </summary>
    private async Task<IActionResult> ForwardAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        AddAuthHeader();

        using var request = new HttpRequestMessage(method, $"{_gatewayUrl}/{path}");
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        try
        {
            using var response = await _httpClient.SendAsync(request, ct);
            var status = (int)response.StatusCode;

            // A bodiless status must be forwarded WITHOUT a body. Writing the "{}" fallback here set Content-Length
            // on a 204 and Kestrel rejected the response outright — "Setting the header Content-Length is not
            // allowed on responses with status code 204" — turning every archive (which answers 204) into a 500
            // even though the backend had already succeeded.
            if (IsBodilessStatus(status))
            {
                return StatusCode(status);
            }

            var payload = await response.Content.ReadAsStringAsync(ct);
            return new ContentResult
            {
                StatusCode = status,
                // Deliberate for statuses that MAY carry a body: an empty 200 would otherwise make the page's
                // res.json() throw. Only the bodiless statuses above are exempt.
                Content = string.IsNullOrWhiteSpace(payload) ? "{}" : payload,
                ContentType = "application/json"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Working calendar override proxy call failed: {Method} {Path}", method, path);
            return StatusCode(502, "{\"message\":\"Gateway unavailable.\"}");
        }
    }

    /// <summary>
    /// Reads a published MOD-0048 reference set and re-shapes it into the same option contract the page's scripts
    /// already consume from the platform lookup surface (<c>code</c> / <c>name</c> / <c>value</c> / <c>sortOrder</c>),
    /// so swapping the source needed no change in <c>form.js</c> or the inline filter.
    /// <para>
    /// Deactivated values are dropped and the published order is preserved. Any failure — unpublished set, missing
    /// permission, gateway down — yields an EMPTY option list with a warning, never a substitute list: an override
    /// authored against a country the platform layer does not know would never resolve.
    /// </para>
    /// </summary>
    private async Task<IActionResult> ForwardReferenceSetAsOptionsAsync(string setCode, CancellationToken ct)
    {
        AddAuthHeader();

        // No scope_key: the set is Global scope and the consumer service rejects a key for global sets.
        var url = $"{_gatewayUrl}/api/v1/reference-data/sets/{Uri.EscapeDataString(setCode)}/published-values";

        try
        {
            using var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Reference set '{SetCode}' returned {Status} for the working-calendar override surface; "
                    + "rendering with no country options (controlled dependency, no fallback list).",
                    setCode, response.StatusCode);
                return EmptyOptions();
            }

            var payload = await response.Content.ReadFromJsonAsync<ReferenceEnvelope>(ReferenceJsonOptions, ct);
            var items = payload?.Data?.Items;
            if (items is null || items.Count == 0)
            {
                _logger.LogWarning(
                    "Reference set '{SetCode}' published no values; the override country selector will be empty.",
                    setCode);
                return EmptyOptions();
            }

            var options = items
                .Where(x => x.IsActive && !string.IsNullOrWhiteSpace(x.Code))
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Label ?? x.Code, StringComparer.OrdinalIgnoreCase)
                .Select(x => new
                {
                    // ISO alpha-2 verbatim — this is what a WorkingCalendar row stores and what the platform
                    // country calendar is matched on. Never lower-cased, never re-mapped.
                    code = x.Code!.Trim(),
                    name = string.IsNullOrWhiteSpace(x.Label) ? x.Code!.Trim() : x.Label!.Trim(),
                    value = x.Code!.Trim(),
                    sortOrder = x.SortOrder
                })
                .ToList();

            return new JsonResult(new { data = options, statusCode = 200, isSuccessful = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex, "Reference set '{SetCode}' load failed for the working-calendar override surface.", setCode);
            return EmptyOptions();
        }
    }

    /// <summary>
    /// Statuses whose response must carry no body. 204 is the one this proxy actually forwards (archive); 205 and
    /// 304 are listed defensively because Kestrel rejects a Content-Length on them the same way, and a future
    /// endpoint returning one would fail identically and just as confusingly.
    /// </summary>
    private static bool IsBodilessStatus(int status)
        => status is 204 or 205 or 304 || (status >= 100 && status < 200);

    private static IActionResult EmptyOptions()
        => new JsonResult(new { data = Array.Empty<object>(), statusCode = 200, isSuccessful = true });

    private sealed record ReferenceEnvelope(ReferencePublishedValues? Data);

    private sealed record ReferencePublishedValues(IReadOnlyList<ReferencePublishedValue>? Items);

    private sealed record ReferencePublishedValue(string? Code, string? Label, bool IsActive, int SortOrder);

    /// <summary>
    /// Tenant actor: the token carries the tenant, and the server resolves it. The tenant id is never taken from a
    /// request the browser could shape.
    /// </summary>
    private void AddAuthHeader()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (!string.IsNullOrWhiteSpace(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
}

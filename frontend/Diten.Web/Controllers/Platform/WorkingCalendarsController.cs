using System.Net.Http.Headers;
using Diten.Web.Models.WorkingCalendar;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.Platform;

/// <summary>
/// Platform Admin surface for the COUNTRY working-calendar layer.
/// <para>
/// Thin by design: the view actions render shell only and every value is fetched by the page scripts through the
/// <c>api/…</c> proxy below (No-ViewModel). The proxy is what makes the page work with an HttpOnly session — it
/// attaches the caller's token server-side, so browser script never holds a credential and never calls a service
/// port directly.
/// </para>
/// <para>The route is <c>/Platform/WorkingCalendars</c> deliberately: self-registration derives a page's permission
/// scope from its route, and <c>/Platform/…</c> is what marks these keys platform-admin-only. Moving this route
/// would silently make the country layer tenant-assignable.</para>
/// </summary>
[Route("Platform/WorkingCalendars")]
public sealed class WorkingCalendarsController : Controller
{
    private const string ApiBase = "api/platform/working-calendars";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger<WorkingCalendarsController> _logger;

    public WorkingCalendarsController(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<WorkingCalendarsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
        _logger = logger;
    }

    // ── Views (shell only) ───────────────────────────────────────────────────

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Platform/WorkingCalendars/Index.cshtml");

    [HttpGet("Create")]
    public IActionResult Create() => View(
        "~/Views/Platform/WorkingCalendars/Create.cshtml",
        new WorkingCalendarEditViewModel { CalendarYear = DateTime.UtcNow.Year, IsCountryLayer = true });

    [HttpGet("Edit/{id:guid}")]
    public IActionResult Edit(Guid id) => View(
        "~/Views/Platform/WorkingCalendars/Edit.cshtml",
        new WorkingCalendarEditViewModel { Id = id, IsCountryLayer = true });

    [HttpGet("Details/{id:guid}")]
    public IActionResult Details(Guid id)
    {
        ViewData["CalendarId"] = id;
        return View("~/Views/Platform/WorkingCalendars/Details.cshtml");
    }

    // ── Same-origin proxy ────────────────────────────────────────────────────

    [HttpGet("api")]
    public Task<IActionResult> List(CancellationToken ct)
        => ForwardAsync(HttpMethod.Get, $"{ApiBase}{Request.QueryString}", null, ct);

    [HttpGet("api/contract")]
    public Task<IActionResult> Contract(CancellationToken ct)
        => ForwardAsync(HttpMethod.Get, $"{ApiBase}/contract", null, ct);

    /// <summary>Country options come from the MOD-0048 reference set, proxied so the page needs no second origin.</summary>
    [HttpGet("api/countries")]
    public Task<IActionResult> Countries(CancellationToken ct)
        => ForwardAsync(HttpMethod.Get, "api/lookups/countries", null, ct);

    [HttpGet("api/organization-units")]
    public Task<IActionResult> OrganizationUnits(CancellationToken ct)
        => ForwardAsync(HttpMethod.Get, "api/platform/organization-units", null, ct);

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
    /// Forwards verbatim and returns the Gateway's status code unchanged. A 403 (no RBAC grant yet) and a 409
    /// (concurrency / already-active) must reach the page as themselves — flattening them into a generic 500 would
    /// hide exactly the answers the UI is built to explain.
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
            _logger.LogError(ex, "Working calendar proxy call failed: {Method} {Path}", method, path);
            return StatusCode(502, "{\"message\":\"Gateway unavailable.\"}");
        }
    }

    /// <summary>
    /// Statuses whose response must carry no body. 204 is the one this proxy actually forwards (archive); 205 and
    /// 304 are listed defensively because Kestrel rejects a Content-Length on them the same way, and a future
    /// endpoint returning one would fail identically and just as confusingly.
    /// </summary>
    private static bool IsBodilessStatus(int status)
        => status is 204 or 205 or 304 || (status >= 100 && status < 200);

    private void AddAuthHeader()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (!string.IsNullOrWhiteSpace(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // Platform actor: no tenant header. The country layer is cross-tenant by definition.
        if (_httpClient.DefaultRequestHeaders.Contains("X-Tenant-Id"))
        {
            _httpClient.DefaultRequestHeaders.Remove("X-Tenant-Id");
        }
    }
}

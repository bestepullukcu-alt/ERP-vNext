using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

/// <summary>
/// MOD-0024 — the Task Engine tenant surface plus its same-origin API proxy.
///
/// <para>The browser never addresses a service port: it calls <c>/Tasks/api/*</c> on this app, and the JWT is read
/// server-side from the HTTP-only auth cookie (never exposed to JS). The proxy path deliberately avoids
/// <c>api/tasks</c>, which the frozen legacy <c>TaskApiController</c> owns.</para>
/// </summary>
[Authorize]
[Route("Tasks")]
public sealed class TasksController : Controller
{
    private const string TenantHeaderName = "X-Tenant-Id";
    private const string CorrelationHeaderName = "X-Correlation-Id";

    /// <summary>The Task Center — the single personal entry point for work.</summary>
    private const string WorkCenterUrl = "/WorkCenterNext";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _gatewayUrl;
    private readonly ILogger<TasksController> _logger;

    public TasksController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TasksController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _gatewayUrl = configuration["GatewayUrl"]?.TrimEnd('/') ?? "http://localhost:5000";
        _logger = logger;
    }

    // ── Views (Golden Reference Compact: separate Create/Edit/Details pages) ──

    /// <summary>
    /// The Task Center (/WorkCenterNext) is the ONE personal work list, so /Tasks deliberately renders no list of
    /// its own — a second list is two places to disagree about the same work, and a competing "Tasks" surface
    /// fragments the entry point. The route is kept (permission assignment and the manifest's ParentPageCode chain
    /// hang off PageTasks) and simply forwards; only the surface behaviour changed.
    /// </summary>
    /// <remarks>
    /// A 302, not a 301: browsers cache a permanent redirect indefinitely, which would make reversing this
    /// product decision impossible for anyone who had already visited the page once.
    /// </remarks>
    [HttpGet("")]
    public IActionResult Index() => Redirect(WorkCenterUrl);

    [HttpGet("Create")]
    public IActionResult Create()
    {
        ViewBag.ActiveMenu = "tasks";
        return View("~/Views/Tasks/Create.cshtml");
    }

    [HttpGet("{id:guid}")]
    public IActionResult Details(Guid id)
    {
        ViewBag.ActiveMenu = "tasks";
        ViewData["TaskId"] = id.ToString();
        return View("~/Views/Tasks/Details.cshtml");
    }

    [HttpGet("{id:guid}/Edit")]
    public IActionResult Edit(Guid id)
    {
        ViewBag.ActiveMenu = "tasks";
        ViewData["TaskId"] = id.ToString();
        return View("~/Views/Tasks/Edit.cshtml");
    }

    // ── Same-origin API proxy ────────────────────────────────────────────────

    [HttpGet("api/list")]
    public Task<IActionResult> ApiList()
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/tasks", readBody: false);

    [HttpGet("api/{id:guid}")]
    public Task<IActionResult> ApiGet(Guid id)
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/tasks/{id}", readBody: false);

    [HttpPost("api")]
    public Task<IActionResult> ApiCreate()
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/tasks", readBody: true);

    [HttpPut("api/{id:guid}")]
    public Task<IActionResult> ApiUpdate(Guid id)
        => ProxyAsync(HttpMethod.Put, $"{_gatewayUrl}/api/v1/tasks/{id}", readBody: true);

    [HttpDelete("api/{id:guid}")]
    public Task<IActionResult> ApiDelete(Guid id)
        => ProxyAsync(HttpMethod.Delete, $"{_gatewayUrl}/api/v1/tasks/{id}", readBody: false);

    /// <summary>
    /// Lifecycle/ownership transitions: accept, claim, release, plan, start, complete, cancel.
    /// The route parameter is named <c>transition</c>, not <c>action</c>: <c>action</c> is reserved by MVC routing
    /// and combining it with a constraint fails endpoint construction at startup.
    /// </summary>
    [HttpPost("api/{id:guid}/{transition:regex(^(accept|claim|release|plan|start|complete|cancel)$)}")]
    public Task<IActionResult> ApiTransition(Guid id, string transition)
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/tasks/{id}/{transition}", readBody: true);

    /// <summary>
    /// Assignable positions. Carries the organization unit code+name so the picker renders
    /// "QA Specialist — Facility A"; without it a pooled task can silently reach the wrong facility.
    /// </summary>
    [HttpGet("api/assignable-positions")]
    public Task<IActionResult> ApiAssignablePositions()
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/tasks/lookups/assignable-positions", readBody: false);

    // ── Phase 2: checklist + subtasks ────────────────────────────────────────

    /// <summary>Tick/untick a checklist item. Expected-version write against the checklist RUN.</summary>
    [HttpPost("api/{id:guid}/checklist/items/state")]
    public Task<IActionResult> ApiSetChecklistItemState(Guid id)
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/tasks/{id}/checklist/items/state", readBody: true);

    /// <summary>Add an ad-hoc checklist item (the user's own text — never a resource key).</summary>
    [HttpPost("api/{id:guid}/checklist/items")]
    public Task<IActionResult> ApiAddChecklistItem(Guid id)
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/tasks/{id}/checklist/items", readBody: true);

    /// <summary>Create a task from a template; its checklist is instantiated server-side.</summary>
    [HttpPost("api/from-template")]
    public Task<IActionResult> ApiCreateFromTemplate()
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/tasks/from-template", readBody: true);

    /// <summary>
    /// People a task may be assigned to (whoever holds a position). Carries the display name, position and
    /// organization unit so the picker never has to show a user GUID.
    /// </summary>
    [HttpGet("api/assignable-people")]
    public Task<IActionResult> ApiAssignablePeople()
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/tasks/lookups/assignable-people", readBody: false);

    private async Task<IActionResult> ProxyAsync(HttpMethod method, string targetUrl, bool readBody)
    {
        if (!TryCreateTenantRequest(method, targetUrl, out var request))
        {
            return Unauthorized(new { message = "Unauthorized" });
        }

        try
        {
            using (request)
            {
                if (readBody)
                {
                    using var reader = new StreamReader(Request.Body, Encoding.UTF8);
                    var body = await reader.ReadToEndAsync(HttpContext.RequestAborted);
                    request.Content = new StringContent(
                        body, Encoding.UTF8, Request.ContentType ?? "application/json");
                }

                var client = _httpClientFactory.CreateClient();
                using var response = await client.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted);
                var content = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted);

                // Pass the upstream status through verbatim: a 403 (permission not granted) or 409 (claim race)
                // must reach the browser as itself so the UI can react precisely.
                return new ContentResult
                {
                    Content = content,
                    ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
                    StatusCode = (int)response.StatusCode
                };
            }
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task engine proxy failed for {Method} {TargetUrl}.", method, targetUrl);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "Task engine dependency unavailable." });
        }
    }

    private bool TryCreateTenantRequest(HttpMethod method, string targetUrl, out HttpRequestMessage request)
    {
        request = new HttpRequestMessage(method, targetUrl);
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token) || !TryResolveTenantId(token, out var tenantId))
        {
            request.Dispose();
            request = null!;
            return false;
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation(TenantHeaderName, tenantId.ToString("D"));
        request.Headers.TryAddWithoutValidation(CorrelationHeaderName, ResolveCorrelationId());
        if (Request.Headers.TryGetValue("Accept-Language", out var acceptLanguage))
        {
            request.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage.ToString());
        }

        return true;
    }

    private string ResolveCorrelationId()
    {
        if (Request.Headers.TryGetValue(CorrelationHeaderName, out var correlationId) &&
            !string.IsNullOrWhiteSpace(correlationId.ToString()))
        {
            return correlationId.ToString();
        }

        return HttpContext.TraceIdentifier;
    }

    private static bool TryResolveTenantId(string token, out Guid tenantId)
    {
        tenantId = Guid.Empty;
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            var claimValue = FindClaim(jwt.Claims, "tenant_id", "tenantId");
            return Guid.TryParse(claimValue, out tenantId) && tenantId != Guid.Empty && jwt.ValidTo > DateTime.UtcNow;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindClaim(IEnumerable<Claim> claims, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var match = claims.FirstOrDefault(c => string.Equals(c.Type, claimType, StringComparison.OrdinalIgnoreCase));
            if (match is not null && !string.IsNullOrWhiteSpace(match.Value))
            {
                return match.Value;
            }
        }

        return null;
    }
}

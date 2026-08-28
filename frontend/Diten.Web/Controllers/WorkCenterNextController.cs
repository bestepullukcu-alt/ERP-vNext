using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

// WorkCenterNext — the Görev Merkezi / Task Center tenant surface (spec: docs/workcenter-rebuild-spec.md).
// WC-1b: the page is fed by the REAL work-item projection through a SAME-ORIGIN proxy — the browser never talks
// to a service port, and the JWT is read server-side from the HTTP-only auth cookie (never exposed to JS).
// The legacy /WorkCenter route is left untouched for comparison + rollback.
[Authorize]
public class WorkCenterNextController : Controller
{
    private const string TenantHeaderName = "X-Tenant-Id";
    private const string CorrelationHeaderName = "X-Correlation-Id";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _gatewayUrl;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<WorkCenterNextController> _logger;

    public WorkCenterNextController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<WorkCenterNextController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _gatewayUrl = configuration["GatewayUrl"]?.TrimEnd('/') ?? "http://localhost:5000";
        _environment = environment;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index()
    {
        ViewBag.ActiveMenu = "workcenternext";
        // DEC-1 — the showcase fixture catalog is a DEVELOPMENT-only affordance. The switch is decided
        // SERVER-side; production renders "false" so there is no client-reachable path to fixture data.
        ViewData["FixturesToggleAllowed"] = _environment.IsDevelopment();
        return View();
    }

    [HttpGet]
    public IActionResult Details(string id)
    {
        ViewBag.ActiveMenu = "workcenternext";
        ViewData["WorkItemId"] = id ?? string.Empty;
        ViewData["FixturesToggleAllowed"] = _environment.IsDevelopment();
        return View();
    }

    // Same-origin read proxy for the personal work-item projection (WC-1, read-only).
    // browser → /WorkCenterNext/api/work-items → Gateway (5000) → Platform. No token or tenant id in JS.
    /// <summary>
    /// BL-023 — <c>?scope=team</c> asks for the caller's SUBORDINATES' work instead of their own.
    ///
    /// <para>The value is not passed through as free text: an unrecognised scope collapses to the caller's own
    /// work, which is the fail-safe direction. A proxy that dropped the parameter instead would answer the
    /// caller's OWN list under a header reading "Ekibim" — wrong data, no error, nothing to notice.</para>
    /// </summary>
    [HttpGet("/WorkCenterNext/api/work-items")]
    public Task<IActionResult> WorkItems([FromQuery] string? scope = null)
    {
        var upstream = string.Equals(scope, "team", StringComparison.OrdinalIgnoreCase)
            ? $"{_gatewayUrl}/api/v1/work-items/mine?scope=team"
            : $"{_gatewayUrl}/api/v1/work-items/mine";
        return ProxyGetAsync(upstream);
    }

    /// <summary>BL-023 — does the caller have a team? Drives whether the scope option is enabled.</summary>
    [HttpGet("/WorkCenterNext/api/team-availability")]
    public Task<IActionResult> TeamAvailability()
        => ProxyGetAsync($"{_gatewayUrl}/api/v1/work-items/team-availability");

    /// <summary>
    /// WC-D2 — the ONE address the Task Center writes an action to, proxied same-origin exactly like the read.
    /// </summary>
    /// <remarks>
    /// <para>The browser names an item, an action and the provider that owns the item; it never names a module's
    /// endpoint, and it never sees a token or a tenant id — those are attached here from the HTTP-only cookie,
    /// the same <see cref="TryCreateTenantRequest"/> the read path uses.</para>
    ///
    /// <para><b>The upstream status and body pass through verbatim</b>, reason code included. The Task Center
    /// resolves its messages from stable codes in seven languages, so a proxy that flattened a 409
    /// TASK_CONCURRENCY_CONFLICT into its own error shape would leave every refusal reading "an error occurred".</para>
    ///
    /// <para><b>/Tasks/api is untouched.</b> The Tasks screens keep their own proxy and their own routes; this
    /// slice adds an address rather than migrating one.</para>
    /// </remarks>
    [HttpPost("/WorkCenterNext/api/work-items/{itemId}/actions/{actionCode}")]
    public async Task<IActionResult> DispatchWorkItemAction(
        string itemId,
        string actionCode,
        [FromBody] JsonElement body)
    {
        var target = $"{_gatewayUrl}/api/v1/work-items/{Uri.EscapeDataString(itemId ?? string.Empty)}"
                     + $"/actions/{Uri.EscapeDataString(actionCode ?? string.Empty)}";

        if (!TryCreateTenantRequest(HttpMethod.Post, target, out var request))
        {
            return Unauthorized(new { message = "Unauthorized" });
        }

        try
        {
            using (request)
            {
                request.Content = new StringContent(
                    body.ValueKind == JsonValueKind.Undefined ? "{}" : body.GetRawText(),
                    Encoding.UTF8,
                    "application/json");

                var client = _httpClientFactory.CreateClient();
                using var response = await client.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted);
                var content = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted);

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
            _logger.LogError(ex, "Work-item action dispatch proxy failed for {TargetUrl}.", target);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "Work aggregation dependency unavailable." });
        }
    }

    private async Task<IActionResult> ProxyGetAsync(string targetUrl)
    {
        if (!TryCreateTenantRequest(HttpMethod.Get, targetUrl, out var request))
        {
            return Unauthorized(new { message = "Unauthorized" });
        }

        try
        {
            using (request)
            {
                var client = _httpClientFactory.CreateClient();
                using var response = await client.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted);
                var content = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted);

                // Pass the upstream status through verbatim: a 403 (permission not granted) must reach the browser
                // as 403 so the surface can render its localized no-access state instead of a broken list.
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
            _logger.LogError(ex, "Work-item projection proxy failed for {TargetUrl}.", targetUrl);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "Work aggregation dependency unavailable." });
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

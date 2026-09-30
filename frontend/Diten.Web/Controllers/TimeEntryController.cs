using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

/// <summary>
/// MOD-0280-FU01 T2a (pack §5.2) — My Timesheet (<c>/TimeEntry</c>) and its same-origin proxy.
///
/// <para><b>One proxy, one prefix.</b> <c>/TimeEntry/api/{**path}</c> → <c>{GatewayUrl}/api/v1/time-entry/{path}</c>, for
/// GET/POST/PUT/DELETE. The path is re-escaped segment by segment (nothing but path segments can reach the upstream path —
/// no <c>..</c>, no second host) and the query string is forwarded whole and still encoded: the parameters are Platform's
/// contract, not this tier's, and re-listing them here is how one gets dropped silently.</para>
///
/// <para><b>The upstream status and body pass through VERBATIM</b>, reason code included — the page turns
/// <c>TIME_ENTRY_*</c> / <c>TIMESHEET_*</c> codes into sentences in seven languages. The token and the tenant id are
/// attached here from the HTTP-only cookie; the browser never sees either.</para>
///
/// <para>The page itself renders the UAS-001 explanation (no skeleton) for a caller without
/// <c>time-entry.timesheets.read</c>; Platform's <c>[HasPermission]</c> stays the authority.</para>
/// </summary>
[Authorize]
public sealed class TimeEntryController : Controller
{
    private const string TenantHeaderName = "X-Tenant-Id";
    private const string CorrelationHeaderName = "X-Correlation-Id";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _gatewayUrl;
    private readonly ILogger<TimeEntryController> _logger;

    public TimeEntryController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TimeEntryController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _gatewayUrl = configuration["GatewayUrl"]?.TrimEnd('/') ?? "http://localhost:5000";
        _logger = logger;
    }

    /// <summary>My Timesheet. <paramref name="week"/> (ISO week, e.g. <c>2026-W40</c>) is handed to the page as is; the
    /// page validates its shape and the server validates the week.</summary>
    [HttpGet("/TimeEntry")]
    public IActionResult Index([FromQuery] string? week = null)
    {
        ViewBag.ActiveMenu = "timeentry";
        ViewData["WeekKey"] = week ?? string.Empty;
        return View("~/Views/TimeEntry/Index.cshtml");
    }

    [AcceptVerbs("GET", "POST", "PUT", "DELETE", Route = "/TimeEntry/api/{**path}")]
    public async Task<IActionResult> Api(string? path)
    {
        var target = UpstreamUrl(path);
        if (target is null)
        {
            return NotFound();
        }

        var method = new HttpMethod(Request.Method);
        if (!TryCreateTenantRequest(method, target, out var request))
        {
            return Unauthorized(new { message = "Unauthorized" });
        }

        try
        {
            using (request)
            {
                if (method != HttpMethod.Get && method != HttpMethod.Delete)
                {
                    using var reader = new StreamReader(Request.Body, Encoding.UTF8);
                    var relayed = await reader.ReadToEndAsync(HttpContext.RequestAborted);
                    request.Content = new StringContent(
                        string.IsNullOrEmpty(relayed) ? "{}" : relayed, Encoding.UTF8, "application/json");
                }

                var client = _httpClientFactory.CreateClient();
                using var response = await client.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted);
                var content = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted);

                // Verbatim: a 403, a 409 TIMESHEET_CONCURRENCY_CONFLICT, a 400 TIME_ENTRY_STEP_INVALID reach the page as
                // themselves, so it can say exactly what happened.
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
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // v3 M5 — the service is down or slow (a restart, a closed port). Every tenant page asks for the timer chip, so
            // this is an expected, recoverable state: a Warning, not an Error flooding the log. The page shows its own
            // "try again" state from the 503.
            _logger.LogWarning(ex, "Time Entry proxy could not reach {Method} {TargetUrl}.", method, target);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "Time Entry dependency unavailable." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Time Entry proxy failed for {Method} {TargetUrl}.", method, target);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "Time Entry dependency unavailable." });
        }
    }

    /// <summary>The upstream URL, or null when the path holds anything but plain segments.</summary>
    private string? UpstreamUrl(string? path)
    {
        var segments = (path ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(s => s is "." or ".."))
        {
            return null;
        }

        var escaped = string.Join('/', segments.Select(Uri.EscapeDataString));
        return $"{_gatewayUrl}/api/v1/time-entry/{escaped}{Request.QueryString.Value}";
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

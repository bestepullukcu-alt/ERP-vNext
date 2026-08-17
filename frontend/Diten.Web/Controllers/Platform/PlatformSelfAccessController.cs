using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.Platform;

// FE-E (MOD-0018-FU9) — platform Self Effective Access page. Read-only: renders the FU14
// self-explain DTO (two separate observations, no combined verdict). The page proxies a GET to the
// gateway self-explain endpoint (GW-A route) server-side with the platform-admin bearer. No backend,
// gateway, or DTO change.
[Authorize]
public sealed class PlatformSelfAccessController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger<PlatformSelfAccessController> _logger;

    public PlatformSelfAccessController(HttpClient httpClient, IConfiguration configuration, ILogger<PlatformSelfAccessController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
        _logger = logger;
    }

    [HttpGet("/Platform/SelfAccess")]
    public IActionResult Index() => View("~/Views/Platform/SelfAccess/Index.cshtml");

    [HttpGet("/Platform/SelfAccess/api")]
    public async Task<IActionResult> ExplainProxy()
    {
        // Self-explain endpoint (GW-A): GET /api/platform/access/explain/me?permissionKey&moduleCode&featureCode
        var targetUrl = $"{_gatewayUrl}/api/platform/access/explain/me{Request.QueryString}";

        if (!TryCreatePlatformAdminRequest(HttpMethod.Get, targetUrl, out var request))
        {
            Diten.Web.Controllers.ProxyAuthFailure.ClearAuthCookies(Response);
            return StatusCode(StatusCodes.Status401Unauthorized, Diten.Web.Controllers.ProxyAuthFailure.PlatformLoginPayload());
        }

        try
        {
            using (request)
            {
                using var response = await _httpClient.SendAsync(request);
                if (Diten.Web.Controllers.ProxyAuthFailure.IsAuthFailure(response.StatusCode))
                {
                    Diten.Web.Controllers.ProxyAuthFailure.ClearAuthCookies(Response);
                    return StatusCode((int)response.StatusCode, Diten.Web.Controllers.ProxyAuthFailure.PlatformLoginPayload());
                }

                var content = await response.Content.ReadAsStringAsync();
                return new ContentResult
                {
                    Content = content,
                    ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
                    StatusCode = (int)response.StatusCode
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Self-access explain proxy failed for {TargetUrl}.", targetUrl);
            return StatusCode(StatusCodes.Status502BadGateway, new { detail = "Gateway request failed." });
        }
    }

    private bool TryCreatePlatformAdminRequest(HttpMethod method, string targetUrl, out HttpRequestMessage request)
    {
        request = new HttpRequestMessage(method, targetUrl);
        var token = Request.Cookies["access_token"] ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(token) && IsPlatformAdminToken(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return true;
        }

        request.Dispose();
        request = null!;
        return false;
    }

    private static bool IsPlatformAdminToken(string token)
    {
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            var actorType = jwt.Claims.FirstOrDefault(c => string.Equals(c.Type, "actor_type", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();
            return jwt.ValidTo > DateTime.UtcNow
                   && (string.Equals(actorType, "platform_admin", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(actorType, "partner_admin", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }
}

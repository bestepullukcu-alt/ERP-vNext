using System.Net.Http.Headers;
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

        // FIX-MYACCESS-PROXY-LOGOUT — forward the cookie token AS-IS and let the gateway decide (mirrors
        // ModuleCatalogController.ProxyGatewayAsync). The previous client-side expiry/actor pre-check tripped on the
        // 5-minute dev token boundary and signed the user out even though the gateway would have accepted the token.
        // Only a real downstream 401 clears cookies; every other status (200/400/403/...) is reflected verbatim so
        // the page shows the actual result or error message instead of logging out.
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, targetUrl);
            var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Self-access explain proxy failed for {TargetUrl}.", targetUrl);
            return StatusCode(StatusCodes.Status502BadGateway, new { detail = "Gateway request failed." });
        }
    }
}

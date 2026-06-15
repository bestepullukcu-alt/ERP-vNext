using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Diten.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.Platform;

[Authorize]
[Route("Platform/Account")]
public sealed class PlatformAccountController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IPlatformProfileSnapshotProvider _profileSnapshotProvider;
    private readonly ILogger<PlatformAccountController> _logger;

    public PlatformAccountController(
        HttpClient httpClient,
        IConfiguration configuration,
        IPlatformProfileSnapshotProvider profileSnapshotProvider,
        ILogger<PlatformAccountController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
        _profileSnapshotProvider = profileSnapshotProvider;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index() => RedirectToAction(nameof(Profile));

    [HttpGet("Profile")]
    public IActionResult Profile() => View("~/Views/Platform/Account/Profile.cshtml");

    [HttpGet("Settings")]
    public IActionResult Settings() => View("~/Views/Platform/Account/Settings.cshtml");

    [HttpGet("api/me")]
    public Task<IActionResult> GetMeProxy() =>
        ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/account/me");

    [HttpPut("api/me")]
    public async Task<IActionResult> UpdateMeProxy()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        var result = await ProxyGatewayAsync(HttpMethod.Put, $"{_gatewayUrl}/api/platform/account/me", body);

        // Bust the Layout's display-name cache so the next page render shows the new value
        // (instead of the stale JWT-derived name). Only invalidate on success.
        if (result is ContentResult content && content.StatusCode is >= 200 and < 300)
        {
            _profileSnapshotProvider.InvalidateCurrent();
        }

        return result;
    }

    [HttpPost("api/invalidate-snapshot")]
    public IActionResult InvalidateSnapshot()
    {
        _profileSnapshotProvider.InvalidateCurrent();
        return NoContent();
    }

    private async Task<IActionResult> ProxyGatewayAsync(HttpMethod method, string targetUrl, string? jsonBody = null)
    {
        if (!TryCreateAuthenticatedRequest(method, targetUrl, out var request))
        {
            Diten.Web.Controllers.ProxyAuthFailure.ClearAuthCookies(Response);
            return StatusCode(StatusCodes.Status401Unauthorized, Diten.Web.Controllers.ProxyAuthFailure.PlatformLoginPayload());
        }

        try
        {
            using (request)
            {
                if (jsonBody is not null)
                {
                    request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                }

                using var response = await _httpClient.SendAsync(request);
                if (Diten.Web.Controllers.ProxyAuthFailure.IsAuthFailure(response.StatusCode))
                {
                    Diten.Web.Controllers.ProxyAuthFailure.ClearAuthCookies(Response);
                    return StatusCode((int)response.StatusCode, Diten.Web.Controllers.ProxyAuthFailure.PlatformLoginPayload());
                }

                var content = await response.Content.ReadAsStringAsync();
                var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
                return new ContentResult
                {
                    Content = content,
                    ContentType = contentType,
                    StatusCode = (int)response.StatusCode
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Platform account proxy request failed for {Method} {TargetUrl}.", method, targetUrl);
            return StatusCode(StatusCodes.Status502BadGateway, new { detail = "Gateway request failed." });
        }
    }

    private bool TryCreateAuthenticatedRequest(HttpMethod method, string targetUrl, out HttpRequestMessage request)
    {
        request = new HttpRequestMessage(method, targetUrl);
        if (TryGetPlatformAccessToken(out var token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return true;
        }

        request.Dispose();
        request = null!;
        return false;
    }

    private bool TryGetPlatformAccessToken(out string token)
    {
        token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            var actorType = FindClaim(jwt.Claims, "actor_type");
            return jwt.ValidTo > DateTime.UtcNow &&
                   (string.Equals(actorType, "platform_admin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(actorType, "partner_admin", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            token = string.Empty;
            return false;
        }
    }

    private static string? FindClaim(IEnumerable<Claim> claims, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = claims.FirstOrDefault(claim => string.Equals(claim.Type, claimType, StringComparison.OrdinalIgnoreCase))?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}

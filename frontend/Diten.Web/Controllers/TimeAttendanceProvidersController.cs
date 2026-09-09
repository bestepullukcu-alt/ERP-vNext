using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

[Route("Platform/TimeAttendanceProviders")]
public sealed class TimeAttendanceProvidersController : Controller
{
    private const string BackendSlug = "time-attendance-providers";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger<TimeAttendanceProvidersController> _logger;

    public TimeAttendanceProvidersController(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<TimeAttendanceProvidersController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Platform/TimeAttendanceProviders/Index.cshtml");

    // ---- Server-side gateway proxy (platform-admin bearer added server-side) ----

    [HttpGet("api")]
    [HttpGet("api/{**rest}")]
    public Task<IActionResult> GetProxy(string? rest)
    {
        return ProxyJsonGatewayAsync(HttpMethod.Get, BuildTargetUrl(rest));
    }

    [HttpPost("api")]
    [HttpPost("api/{**rest}")]
    public Task<IActionResult> PostProxy(string? rest)
    {
        return ProxyJsonGatewayAsync(HttpMethod.Post, BuildTargetUrl(rest), readBody: true);
    }

    [HttpPut("api")]
    [HttpPut("api/{**rest}")]
    public Task<IActionResult> PutProxy(string? rest)
    {
        return ProxyJsonGatewayAsync(HttpMethod.Put, BuildTargetUrl(rest), readBody: true);
    }

    [HttpPatch("api")]
    [HttpPatch("api/{**rest}")]
    public Task<IActionResult> PatchProxy(string? rest)
    {
        return ProxyJsonGatewayAsync(HttpMethod.Patch, BuildTargetUrl(rest), readBody: true);
    }

    [HttpDelete("api")]
    [HttpDelete("api/{**rest}")]
    public Task<IActionResult> DeleteProxy(string? rest)
    {
        return ProxyJsonGatewayAsync(HttpMethod.Delete, BuildTargetUrl(rest));
    }

    private string BuildTargetUrl(string? rest)
    {
        var target = $"{_gatewayUrl}/api/platform/{BackendSlug}";
        if (!string.IsNullOrEmpty(rest))
        {
            target = $"{target}/{rest}";
        }

        return $"{target}{Request.QueryString}";
    }

    // ---- Shared proxy helpers (copied verbatim from TaskChecklistController) ----

    private async Task<IActionResult> ProxyJsonGatewayAsync(HttpMethod method, string targetUrl, bool readBody = false)
    {
        if (!TryCreatePlatformAdminRequest(method, targetUrl, out var request))
        {
            Diten.Web.Controllers.ProxyAuthFailure.ClearAuthCookies(Response);
            return StatusCode(StatusCodes.Status401Unauthorized, Diten.Web.Controllers.ProxyAuthFailure.PlatformLoginPayload());
        }

        try
        {
            using (request)
            {
                if (readBody)
                {
                    using var reader = new StreamReader(Request.Body, Encoding.UTF8);
                    var body = await reader.ReadToEndAsync();
                    request.Content = new StringContent(body, Encoding.UTF8, "application/json");
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
            _logger.LogError(ex, "Platform time & attendance providers proxy request failed for {Method} {TargetUrl}.", method, targetUrl);
            return StatusCode(StatusCodes.Status502BadGateway, new { detail = "Gateway request failed." });
        }
    }

    private bool TryCreatePlatformAdminRequest(HttpMethod method, string targetUrl, out HttpRequestMessage request)
    {
        request = new HttpRequestMessage(method, targetUrl);
        if (TryGetPlatformAdminAccessToken(out var token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return true;
        }

        request.Dispose();
        request = null!;
        return false;
    }

    private bool TryGetPlatformAdminAccessToken(out string token)
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
            return jwt.ValidTo > DateTime.UtcNow
                   && string.Equals(actorType, "platform_admin", StringComparison.OrdinalIgnoreCase);
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
            var value = claims.FirstOrDefault(claim =>
                string.Equals(claim.Type, claimType, StringComparison.OrdinalIgnoreCase))?.Value;

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}

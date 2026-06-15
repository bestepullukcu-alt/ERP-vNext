using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.Platform;

[Authorize]
public sealed class PlatformAuditController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger<PlatformAuditController> _logger;

    public PlatformAuditController(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<PlatformAuditController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
        _logger = logger;
    }

    [HttpGet("/Platform/AuditLog")]
    public IActionResult AuditLog() => View("~/Views/Platform/AuditLog/Index.cshtml");

    [HttpGet("/Platform/AuditRetention")]
    public IActionResult AuditRetention() => View("~/Views/Platform/AuditRetention/Index.cshtml");

    [HttpGet("/Platform/AuditLog/api")]
    public Task<IActionResult> GetEventsProxy()
    {
        return ProxyJsonGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/audit/events{Request.QueryString}");
    }

    [HttpGet("/Platform/AuditLog/api/{id:guid}")]
    public Task<IActionResult> GetEventByIdProxy(Guid id)
    {
        return ProxyJsonGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/audit/events/{id}");
    }

    [HttpGet("/Platform/AuditLog/api/export")]
    public Task<IActionResult> ExportProxy()
    {
        return ProxyFileGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/audit/export{Request.QueryString}");
    }

    [HttpGet("/Platform/AuditLog/api/export-limits")]
    public Task<IActionResult> ExportLimitsProxy()
    {
        return ProxyJsonGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/audit/export-limits");
    }

    [HttpGet("/Platform/AuditLog/api/lookups/{lookupKey}")]
    public Task<IActionResult> AuditLookupProxy(string lookupKey)
    {
        return ProxyJsonGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/lookups/audit/{Uri.EscapeDataString(lookupKey)}");
    }

    [HttpGet("/Platform/AuditRetention/api/lookups/{lookupKey}")]
    public Task<IActionResult> AuditRetentionLookupProxy(string lookupKey)
    {
        return ProxyJsonGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/lookups/audit/{Uri.EscapeDataString(lookupKey)}");
    }

    [HttpPost("/Platform/AuditLog/api/redact-actor")]
    public Task<IActionResult> RedactActorProxy()
    {
        return ProxyJsonGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/audit/redact-actor", readBody: true);
    }

    [HttpGet("/Platform/AuditRetention/api")]
    public Task<IActionResult> GetRetentionProxy()
    {
        return ProxyJsonGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/audit/retention");
    }

    [HttpPut("/Platform/AuditRetention/api")]
    public Task<IActionResult> UpdateRetentionProxy()
    {
        return ProxyJsonGatewayAsync(HttpMethod.Put, $"{_gatewayUrl}/api/platform/audit/retention", readBody: true);
    }

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
            _logger.LogError(ex, "Platform audit proxy request failed for {Method} {TargetUrl}.", method, targetUrl);
            return StatusCode(StatusCodes.Status502BadGateway, new { detail = "Gateway request failed." });
        }
    }

    private async Task<IActionResult> ProxyFileGatewayAsync(HttpMethod method, string targetUrl)
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
                using var response = await _httpClient.SendAsync(request);
                if (Diten.Web.Controllers.ProxyAuthFailure.IsAuthFailure(response.StatusCode))
                {
                    Diten.Web.Controllers.ProxyAuthFailure.ClearAuthCookies(Response);
                    return StatusCode((int)response.StatusCode, Diten.Web.Controllers.ProxyAuthFailure.PlatformLoginPayload());
                }

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return new ContentResult
                    {
                        Content = error,
                        ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
                        StatusCode = (int)response.StatusCode
                    };
                }

                var content = await response.Content.ReadAsByteArrayAsync();
                var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
                var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                    ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                    ?? "audit-export";

                return File(content, contentType, fileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Platform audit export proxy failed for {Method} {TargetUrl}.", method, targetUrl);
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

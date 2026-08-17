using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.Platform;

[Route("Platform/[controller]")]
public sealed class ESignaturesController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;

    public ESignaturesController(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
    }

    [HttpGet("{tenantId:guid}")]
    public IActionResult Index(Guid tenantId)
    {
        ViewData["TenantId"] = tenantId;
        return View("~/Views/Platform/ESignatures/Index.cshtml");
    }

    [HttpGet("{tenantId:guid}/Details/{envelopeId:guid}")]
    public IActionResult Details(Guid tenantId, Guid envelopeId)
    {
        ViewData["TenantId"] = tenantId;
        ViewData["EnvelopeId"] = envelopeId;
        return View("~/Views/Platform/ESignatures/Details.cshtml");
    }

    [HttpGet("api/tenants/{tenantId:guid}/envelopes")]
    public Task<IActionResult> ListEnvelopesProxy(Guid tenantId)
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/e-signatures/admin/tenants/{tenantId}/envelopes{Request.QueryString}");
    }

    [HttpGet("api/tenants/{tenantId:guid}/envelopes/{envelopeId:guid}")]
    public Task<IActionResult> GetEnvelopeProxy(Guid tenantId, Guid envelopeId)
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/e-signatures/admin/tenants/{tenantId}/envelopes/{envelopeId}");
    }

    [HttpPost("api/tenants/{tenantId:guid}/envelopes/{envelopeId:guid}/cancel")]
    public async Task<IActionResult> CancelEnvelopeProxy(Guid tenantId, Guid envelopeId)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/e-signatures/admin/tenants/{tenantId}/envelopes/{envelopeId}/cancel", body);
    }

    [HttpPost("api/tenants/{tenantId:guid}/envelopes/{envelopeId:guid}/audit-export")]
    public async Task<IActionResult> AuditExportProxy(Guid tenantId, Guid envelopeId)
    {
        var correlationId = Request.Query["correlationId"].ToString();
        var query = string.IsNullOrWhiteSpace(correlationId) ? string.Empty : $"?correlationId={correlationId}";
        return await ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/e-signatures/admin/tenants/{tenantId}/envelopes/{envelopeId}/audit-export{query}");
    }

    [HttpGet("api/tenants/{tenantId:guid}/artifacts/{artifactId:guid}")]
    public async Task<IActionResult> DownloadArtifactProxy(Guid tenantId, Guid artifactId)
    {
        if (!TryGetPlatformAccessToken(out var token))
        {
            ProxyAuthFailure.ClearAuthCookies(Response);
            return StatusCode(StatusCodes.Status401Unauthorized, ProxyAuthFailure.PlatformLoginPayload());
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_gatewayUrl}/api/platform/e-signatures/admin/tenants/{tenantId}/artifacts/{artifactId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request);
        if (ProxyAuthFailure.IsAuthFailure(response.StatusCode))
        {
            ProxyAuthFailure.ClearAuthCookies(Response);
            return StatusCode((int)response.StatusCode, ProxyAuthFailure.PlatformLoginPayload());
        }

        if (ProxyAuthFailure.IsForbidden(response.StatusCode))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var content = await response.Content.ReadAsByteArrayAsync();
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar ?? 
                       response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? 
                       "download";

        return File(content, contentType, fileName);
    }

    private async Task<IActionResult> ProxyGatewayAsync(HttpMethod method, string targetUrl, string? jsonBody = null)
    {
        if (!TryGetPlatformAccessToken(out var token))
        {
            ProxyAuthFailure.ClearAuthCookies(Response);
            return StatusCode(StatusCodes.Status401Unauthorized, ProxyAuthFailure.PlatformLoginPayload());
        }

        using var request = new HttpRequestMessage(method, targetUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (jsonBody is not null)
        {
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        }

        var response = await _httpClient.SendAsync(request);
        if (ProxyAuthFailure.IsAuthFailure(response.StatusCode))
        {
            ProxyAuthFailure.ClearAuthCookies(Response);
            return StatusCode((int)response.StatusCode, ProxyAuthFailure.PlatformLoginPayload());
        }

        if (ProxyAuthFailure.IsForbidden(response.StatusCode))
        {
            var forbiddenContent = await response.Content.ReadAsStringAsync();
            return new ContentResult
            {
                Content = forbiddenContent,
                ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
                StatusCode = StatusCodes.Status403Forbidden
            };
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
            var actorType = jwt.Claims.FirstOrDefault(c => c.Type == "actor_type")?.Value;
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
}

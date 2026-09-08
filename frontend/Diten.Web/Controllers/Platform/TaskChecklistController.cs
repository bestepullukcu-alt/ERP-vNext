using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.Platform;

// FE (MOD-0024) — Task & Checklist Engine platform-admin proxy.
// Mirrors PlatformAuditController exactly: MVC Controller forwarding to the gateway
// with the platform-admin bearer token via the shared proxy helpers.
[Authorize]
public sealed class TaskChecklistController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger<TaskChecklistController> _logger;

    public TaskChecklistController(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<TaskChecklistController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
        _logger = logger;
    }

    // ---- Page routes ----

    [HttpGet("/Platform/Tasks")]
    public IActionResult Tasks() => View("~/Views/Platform/Tasks/Index.cshtml");

    [HttpGet("/Platform/Checklists")]
    public IActionResult Checklists() => View("~/Views/Platform/Checklists/Index.cshtml");

    // ---- Tasks proxy routes ----

    [HttpGet("/Platform/Tasks/api")]
    public Task<IActionResult> GetTasksProxy()
    {
        return ProxyJsonGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/tasks{Request.QueryString}");
    }

    [HttpPost("/Platform/Tasks/api")]
    public Task<IActionResult> CreateTaskProxy()
    {
        return ProxyJsonGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/tasks", readBody: true);
    }

    [HttpGet("/Platform/Tasks/api/{id:guid}")]
    public Task<IActionResult> GetTaskByIdProxy(Guid id)
    {
        return ProxyJsonGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/tasks/{id}");
    }

    [HttpPost("/Platform/Tasks/api/{id:guid}/assign")]
    public Task<IActionResult> AssignTaskProxy(Guid id)
    {
        return ProxyJsonGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/tasks/{id}/assign", readBody: true);
    }

    [HttpPost("/Platform/Tasks/api/{id:guid}/complete")]
    public Task<IActionResult> CompleteTaskProxy(Guid id)
    {
        return ProxyJsonGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/tasks/{id}/complete", readBody: true);
    }

    // ---- Checklist template proxy routes ----

    [HttpGet("/Platform/Checklists/api/templates")]
    public Task<IActionResult> GetTemplatesProxy()
    {
        return ProxyJsonGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/checklists/templates{Request.QueryString}");
    }

    [HttpGet("/Platform/Checklists/api/templates/{id:guid}")]
    public Task<IActionResult> GetTemplateByIdProxy(Guid id)
    {
        return ProxyJsonGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/checklists/templates/{id}");
    }

    [HttpPost("/Platform/Checklists/api/templates")]
    public Task<IActionResult> CreateTemplateProxy()
    {
        return ProxyJsonGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/checklists/templates", readBody: true);
    }

    [HttpPatch("/Platform/Checklists/api/templates/{id:guid}")]
    public Task<IActionResult> UpdateTemplateProxy(Guid id)
    {
        return ProxyJsonGatewayAsync(HttpMethod.Patch, $"{_gatewayUrl}/api/platform/checklists/templates/{id}", readBody: true);
    }

    // ---- Checklist run proxy routes ----

    [HttpGet("/Platform/Checklists/api/runs")]
    public Task<IActionResult> GetRunsProxy()
    {
        return ProxyJsonGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/checklists/runs{Request.QueryString}");
    }

    [HttpGet("/Platform/Checklists/api/runs/{id:guid}")]
    public Task<IActionResult> GetRunByIdProxy(Guid id)
    {
        return ProxyJsonGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/checklists/runs/{id}");
    }

    [HttpPost("/Platform/Checklists/api/runs")]
    public Task<IActionResult> CreateRunProxy()
    {
        return ProxyJsonGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/checklists/runs", readBody: true);
    }

    [HttpPost("/Platform/Checklists/api/runs/{runId:guid}/items/{itemId:guid}/complete")]
    public Task<IActionResult> CompleteRunItemProxy(Guid runId, Guid itemId)
    {
        return ProxyJsonGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/checklists/runs/{runId}/items/{itemId}/complete", readBody: true);
    }

    // ---- Shared proxy helpers (copied verbatim from PlatformAuditController) ----

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
            _logger.LogError(ex, "Platform task/checklist proxy request failed for {Method} {TargetUrl}.", method, targetUrl);
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

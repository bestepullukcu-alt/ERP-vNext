using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.Platform;

// FE (MOD-0023) — Workflow Designer platform-admin proxy.
// Mirrors TaskChecklistController exactly: MVC Controller forwarding to the gateway
// with the platform-admin bearer token via the shared proxy helpers.
[Authorize]
public sealed class WorkflowDesignerController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger<WorkflowDesignerController> _logger;

    public WorkflowDesignerController(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<WorkflowDesignerController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
        _logger = logger;
    }

    // ---- Page routes ----

    [HttpGet("/Platform/WorkflowDesigner")]
    public IActionResult WorkflowDesigner() => View("~/Views/Platform/WorkflowDesigner/Index.cshtml");

    [HttpGet("/Platform/Approvals")]
    public IActionResult Approvals() => View("~/Views/Platform/Approvals/Index.cshtml");

    [HttpGet("/Platform/WorkflowRuns")]
    public IActionResult WorkflowRuns() => View("~/Views/Platform/WorkflowRuns/Index.cshtml");

    // ---- Definition proxy routes ----

    [HttpGet("/Platform/Workflow/api/definitions")]
    public Task<IActionResult> GetDefinitionsProxy()
    {
        return ProxyJsonGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/workflow/definitions{Request.QueryString}");
    }

    [HttpGet("/Platform/Workflow/api/definitions/{id:guid}")]
    public Task<IActionResult> GetDefinitionByIdProxy(Guid id)
    {
        return ProxyJsonGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/workflow/definitions/{id}");
    }

    [HttpPost("/Platform/Workflow/api/definitions")]
    public Task<IActionResult> CreateDefinitionProxy()
    {
        return ProxyJsonGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/workflow/definitions", readBody: true);
    }

    [HttpPatch("/Platform/Workflow/api/definitions/{id:guid}")]
    public Task<IActionResult> UpdateDefinitionProxy(Guid id)
    {
        return ProxyJsonGatewayAsync(HttpMethod.Patch, $"{_gatewayUrl}/api/platform/workflow/definitions/{id}", readBody: true);
    }

    [HttpPost("/Platform/Workflow/api/definitions/{code}/versions")]
    public Task<IActionResult> CreateDefinitionVersionProxy(string code)
    {
        return ProxyJsonGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/workflow/definitions/{code}/versions", readBody: true);
    }

    [HttpPost("/Platform/Workflow/api/definitions/{id:guid}/publish")]
    public Task<IActionResult> PublishDefinitionProxy(Guid id)
    {
        return ProxyJsonGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/workflow/definitions/{id}/publish", readBody: true);
    }

    // ---- Instance proxy routes ----

    [HttpPost("/Platform/Workflow/api/instances/start")]
    public Task<IActionResult> StartInstanceProxy()
    {
        return ProxyJsonGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/workflow/instances/start", readBody: true);
    }

    [HttpGet("/Platform/Workflow/api/instances")]
    public Task<IActionResult> GetInstancesProxy()
    {
        return ProxyJsonGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/workflow/instances{Request.QueryString}");
    }

    [HttpGet("/Platform/Workflow/api/instances/{id:guid}")]
    public Task<IActionResult> GetInstanceByIdProxy(Guid id)
    {
        return ProxyJsonGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/workflow/instances/{id}");
    }

    // ---- Approval task (inbox) proxy routes ----

    [HttpGet("/Platform/Workflow/api/tasks")]
    public Task<IActionResult> GetWorkflowTasksProxy()
    {
        return ProxyJsonGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/workflow/tasks{Request.QueryString}");
    }

    [HttpGet("/Platform/Workflow/api/tasks/{id:guid}")]
    public Task<IActionResult> GetWorkflowTaskByIdProxy(Guid id)
    {
        return ProxyJsonGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/workflow/tasks/{id}");
    }

    [HttpPost("/Platform/Workflow/api/tasks/{id:guid}/approve")]
    public Task<IActionResult> ApproveWorkflowTaskProxy(Guid id)
    {
        return ProxyJsonGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/workflow/tasks/{id}/approve", readBody: true);
    }

    [HttpPost("/Platform/Workflow/api/tasks/{id:guid}/reject")]
    public Task<IActionResult> RejectWorkflowTaskProxy(Guid id)
    {
        return ProxyJsonGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/workflow/tasks/{id}/reject", readBody: true);
    }

    [HttpPost("/Platform/Workflow/api/tasks/{id:guid}/delegate")]
    public Task<IActionResult> DelegateWorkflowTaskProxy(Guid id)
    {
        return ProxyJsonGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/workflow/tasks/{id}/delegate", readBody: true);
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
            _logger.LogError(ex, "Platform workflow proxy request failed for {Method} {TargetUrl}.", method, targetUrl);
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

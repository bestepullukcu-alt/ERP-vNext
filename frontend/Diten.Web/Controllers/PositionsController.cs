using System.Text;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

// Positions (tenant shell). Slim pattern: list + create/edit (offcanvas) + archive + delete all happen on
// the Index page via same-origin AJAX to these proxy actions, which forward to the gateway with the
// server-side HttpOnly token and the tenant claim. Backend [HasPermission] is authoritative.
[Route("Positions")]
public sealed class PositionsController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;

    public PositionsController(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
    }

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Organization/Positions/Index.cshtml");

    // MOD-0288 Phase 3 — full-page compact create/edit/details (two-level form; mirrors the Org Unit slice).
    [HttpGet("Create")]
    public IActionResult Create()
    {
        ViewData["PositionId"] = string.Empty;
        return View("~/Views/Organization/Positions/Form.cshtml");
    }

    [HttpGet("Edit/{id:guid}")]
    public IActionResult Edit(Guid id)
    {
        ViewData["PositionId"] = id.ToString();
        return View("~/Views/Organization/Positions/Form.cshtml");
    }

    [HttpGet("Details/{id:guid}")]
    public IActionResult Details(Guid id)
    {
        ViewData["PositionId"] = id.ToString();
        return View("~/Views/Organization/Positions/Details.cshtml");
    }

    [HttpGet("api")]
    public Task<IActionResult> ListProxy()
    {
        var targetUrl = $"{_gatewayUrl}/api/platform/positions{Request.QueryString}";
        return ProxyGatewayAsync(HttpMethod.Get, targetUrl);
    }

    [HttpGet("api/{id:guid}")]
    public Task<IActionResult> GetByIdProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/positions/{id}");
    }

    [HttpGet("api/{id:guid}/manager-chain")]
    public Task<IActionResult> ManagerChainProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/positions/{id}/manager-chain");
    }

    // Organization Unit lookup feed (Platform). Read-only; feeds the required OrgUnit select.
    [HttpGet("api/org-units")]
    public Task<IActionResult> OrgUnitsProxy()
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/organization-units");
    }

    [HttpPost("api")]
    public async Task<IActionResult> CreateProxy()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/positions", body);
    }

    [HttpPut("api/{id:guid}")]
    public async Task<IActionResult> UpdateProxy(Guid id)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Put, $"{_gatewayUrl}/api/platform/positions/{id}", body);
    }

    [HttpPost("api/{id:guid}/archive")]
    public Task<IActionResult> ArchiveProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/positions/{id}/archive");
    }

    [HttpDelete("api/{id:guid}")]
    public Task<IActionResult> DeleteProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Delete, $"{_gatewayUrl}/api/platform/positions/{id}");
    }

    private async Task<IActionResult> ProxyGatewayAsync(HttpMethod method, string targetUrl, string? jsonBody = null)
    {
        AddAuthHeaders();
        using var request = new HttpRequestMessage(method, targetUrl);
        if (jsonBody is not null)
        {
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        }

        var response = await _httpClient.SendAsync(request);
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

    // Tenant pattern: forward the bearer token AND the X-Tenant-Id from the user's tenant claim.
    private void AddAuthHeaders()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (!string.IsNullOrWhiteSpace(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (_httpClient.DefaultRequestHeaders.Contains("X-Tenant-Id"))
        {
            _httpClient.DefaultRequestHeaders.Remove("X-Tenant-Id");
        }

        var tenantId = GetTenantId();
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            _httpClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        }
    }

    private string? GetTenantId() =>
        User.Claims.FirstOrDefault(x =>
            x.Type == "tenantId" ||
            x.Type == "tenant_id" ||
            x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;
}

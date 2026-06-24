using System.Text;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

// Position Assignments (tenant shell). Slim pattern: list + create/edit (offcanvas) + delete all happen on
// the Index page via same-origin AJAX to these proxy actions, which forward to the gateway with the
// server-side HttpOnly token and the tenant claim. Backend [HasPermission] is authoritative. Unlike
// Organization Units, there is NO GetById endpoint and NO archive endpoint for position assignments.
[Route("PositionAssignments")]
public sealed class PositionAssignmentsController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;

    public PositionAssignmentsController(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
    }

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Organization/PositionAssignments/Index.cshtml");

    [HttpGet("api")]
    public Task<IActionResult> ListProxy()
    {
        var targetUrl = $"{_gatewayUrl}/api/platform/position-assignments{Request.QueryString}";
        return ProxyGatewayAsync(HttpMethod.Get, targetUrl);
    }

    // Position lookup feed. Read-only; degrades gracefully on the client when unreachable.
    [HttpGet("api/positions")]
    public Task<IActionResult> PositionsProxy()
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/positions");
    }

    // User lookup feed. Returns a PaginatedResult ({ items, totalCount, ... }), NOT the Response<T> envelope.
    [HttpGet("api/users")]
    public Task<IActionResult> UsersProxy()
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/users?page=1&pageSize=200");
    }

    [HttpPost("api")]
    public async Task<IActionResult> CreateProxy()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/position-assignments", body);
    }

    [HttpPut("api/{id:guid}")]
    public async Task<IActionResult> UpdateProxy(Guid id)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Put, $"{_gatewayUrl}/api/platform/position-assignments/{id}", body);
    }

    [HttpDelete("api/{id:guid}")]
    public Task<IActionResult> DeleteProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Delete, $"{_gatewayUrl}/api/platform/position-assignments/{id}");
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

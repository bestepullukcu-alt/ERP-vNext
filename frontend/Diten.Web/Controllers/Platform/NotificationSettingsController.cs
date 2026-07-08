using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.Platform;

[Route("Platform/NotificationSettings")]
public sealed class NotificationSettingsController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;

    public NotificationSettingsController(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
    }

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Platform/NotificationSettings/Index.cshtml");

    [HttpGet("Create")]
    public IActionResult Create()
    {
        ViewData["FormMode"] = "create";
        ViewData["TargetTenantId"] = string.Empty;
        return View("~/Views/Platform/NotificationSettings/Create.cshtml");
    }

    [HttpGet("Edit/{tenantId:guid}")]
    public IActionResult Edit(Guid tenantId)
    {
        ViewData["FormMode"] = "edit";
        ViewData["TargetTenantId"] = tenantId.ToString();
        return View("~/Views/Platform/NotificationSettings/Edit.cshtml");
    }

    [HttpGet("Details/{tenantId:guid}")]
    public IActionResult Details(Guid tenantId)
    {
        ViewData["TargetTenantId"] = tenantId.ToString();
        return View("~/Views/Platform/NotificationSettings/Details.cshtml");
    }

    [HttpGet("api")]
    public Task<IActionResult> SettingsListProxy() =>
        ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/notifications/tenant-settings{Request.QueryString}");

    [HttpGet("api/{tenantId:guid}")]
    public Task<IActionResult> SettingsProxy(Guid tenantId) =>
        ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/notifications/tenant-settings/{tenantId}");

    [HttpPut("api/{tenantId:guid}")]
    public Task<IActionResult> UpsertSettingsProxy(Guid tenantId) =>
        ProxyGatewayAsync(HttpMethod.Put, $"{_gatewayUrl}/api/platform/notifications/tenant-settings/{tenantId}", readBody: true);

    [HttpDelete("api/{tenantId:guid}")]
    public Task<IActionResult> DeleteSettingsProxy(Guid tenantId) =>
        ProxyGatewayAsync(HttpMethod.Delete, $"{_gatewayUrl}/api/platform/notifications/tenant-settings/{tenantId}");

    [HttpGet("api/{tenantId:guid}/resolved")]
    public Task<IActionResult> ResolvedSettingsProxy(Guid tenantId) =>
        ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/notifications/tenant-settings/{tenantId}/resolved");

    [HttpGet("api/lookups/{lookupKey}")]
    public Task<IActionResult> LookupProxy(string lookupKey) =>
        ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/lookups/{Uri.EscapeDataString(lookupKey)}");

    [HttpGet("api/tenants")]
    public Task<IActionResult> TenantsProxy() =>
        ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/admin/tenants{Request.QueryString}");

    private async Task<IActionResult> ProxyGatewayAsync(HttpMethod method, string targetUrl, bool readBody = false)
    {
        AddAuthHeader();
        using var request = new HttpRequestMessage(method, targetUrl);
        if (readBody)
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
        return new ContentResult
        {
            Content = content,
            ContentType = contentType,
            StatusCode = (int)response.StatusCode
        };
    }

    private void AddAuthHeader()
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
    }
}

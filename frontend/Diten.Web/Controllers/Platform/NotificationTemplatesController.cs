using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.Platform;

[Route("Platform/NotificationTemplates")]
public sealed class NotificationTemplatesController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;

    public NotificationTemplatesController(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
    }

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Platform/NotificationTemplates/Index.cshtml");

    [HttpGet("Create")]
    public IActionResult Create(Guid? tenantId)
    {
        ViewData["FormMode"] = "create";
        ViewData["ScopeTenantId"] = tenantId?.ToString() ?? string.Empty;
        return View("~/Views/Platform/NotificationTemplates/Create.cshtml");
    }

    [HttpGet("Edit/{id:guid}")]
    public IActionResult Edit(Guid id, Guid? tenantId)
    {
        ViewData["FormMode"] = "edit";
        ViewData["TemplateId"] = id.ToString();
        ViewData["ScopeTenantId"] = tenantId?.ToString() ?? string.Empty;
        return View("~/Views/Platform/NotificationTemplates/Edit.cshtml");
    }

    [HttpGet("Details/{id:guid}")]
    public IActionResult Details(Guid id, Guid? tenantId)
    {
        ViewData["TemplateId"] = id.ToString();
        ViewData["ScopeTenantId"] = tenantId?.ToString() ?? string.Empty;
        return View("~/Views/Platform/NotificationTemplates/Details.cshtml");
    }

    [HttpGet("api/templates")]
    public Task<IActionResult> PlatformTemplatesProxy() =>
        ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/notifications/templates{Request.QueryString}");

    [HttpGet("api/tenant/{tenantId:guid}/templates")]
    public Task<IActionResult> TenantTemplatesProxy(Guid tenantId) =>
        ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/notifications/tenant-settings/{tenantId}/templates{Request.QueryString}");

    [HttpGet("api/templates/{id:guid}")]
    public Task<IActionResult> TemplateByIdProxy(Guid id) =>
        ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/notifications/templates/by-id/{id}");

    [HttpPost("api/templates")]
    public Task<IActionResult> CreatePlatformTemplateProxy() =>
        ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/notifications/templates", readBody: true);

    [HttpPost("api/tenant/{tenantId:guid}/templates")]
    public Task<IActionResult> CreateTenantTemplateProxy(Guid tenantId) =>
        ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/notifications/tenant-settings/{tenantId}/templates", readBody: true);

    [HttpPut("api/templates/{id:guid}")]
    public Task<IActionResult> UpdatePlatformTemplateProxy(Guid id) =>
        ProxyGatewayAsync(HttpMethod.Put, $"{_gatewayUrl}/api/platform/notifications/templates/{id}", readBody: true);

    [HttpPut("api/tenant/{tenantId:guid}/templates/{id:guid}")]
    public Task<IActionResult> UpdateTenantTemplateProxy(Guid tenantId, Guid id) =>
        ProxyGatewayAsync(HttpMethod.Put, $"{_gatewayUrl}/api/platform/notifications/tenant-settings/{tenantId}/templates/{id}", readBody: true);

    [HttpPost("api/templates/{id:guid}/archive")]
    public Task<IActionResult> ArchiveTemplateProxy(Guid id) =>
        ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/notifications/templates/{id}/archive");

    [HttpPost("api/templates/render-preview")]
    public Task<IActionResult> RenderPreviewProxy() =>
        ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/notifications/templates/render-preview", readBody: true);

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

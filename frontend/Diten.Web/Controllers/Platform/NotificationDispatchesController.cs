using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.Platform;

[Route("Platform/NotificationDispatches")]
public sealed class NotificationDispatchesController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;

    public NotificationDispatchesController(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
    }

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Platform/NotificationDispatches/Index.cshtml");

    [HttpGet("Details/{id:guid}")]
    public IActionResult Details(Guid id, Guid tenantId)
    {
        ViewData["DispatchId"] = id.ToString();
        ViewData["TargetTenantId"] = tenantId.ToString();
        return View("~/Views/Platform/NotificationDispatches/Details.cshtml");
    }

    [HttpGet("api")]
    public Task<IActionResult> DispatchesProxy() =>
        ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/notifications/dispatches{Request.QueryString}");

    [HttpGet("api/{id:guid}")]
    public Task<IActionResult> DispatchByIdProxy(Guid id) =>
        ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/notifications/dispatches/{id}{Request.QueryString}");

    [HttpPost("api/{id:guid}/cancel")]
    public Task<IActionResult> CancelDispatchProxy(Guid id) =>
        ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/notifications/dispatches/{id}/cancel{Request.QueryString}");

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

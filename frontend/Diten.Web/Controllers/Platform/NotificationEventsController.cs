using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.Platform;

// MOD-0027-FU03 — same-origin proxy for the read-only Notification Event Catalog UI. Browser JS only calls
// /Platform/NotificationEvents/api/...; the HttpOnly cookie is converted to a Gateway Bearer server-side.
[Route("Platform/NotificationEvents")]
public sealed class NotificationEventsController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;

    public NotificationEventsController(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
    }

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Platform/NotificationEvents/Index.cshtml");

    [HttpGet("Details/{eventCode}")]
    public IActionResult Details(string eventCode)
    {
        ViewData["EventCode"] = eventCode;
        return View("~/Views/Platform/NotificationEvents/Details.cshtml");
    }

    private const string Base = "/api/platform/notifications";

    [HttpGet("api/events")]
    public Task<IActionResult> EventsProxy() =>
        ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}{Base}/events{Request.QueryString}");

    [HttpGet("api/events/{eventCode}")]
    public Task<IActionResult> EventByCodeProxy(string eventCode) =>
        ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}{Base}/events/{Uri.EscapeDataString(eventCode)}");

    [HttpGet("api/events/{eventCode}/template-contract")]
    public Task<IActionResult> TemplateContractProxy(string eventCode) =>
        ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}{Base}/events/{Uri.EscapeDataString(eventCode)}/template-contract");

    [HttpPost("api/events/sync-from-manifest")]
    public Task<IActionResult> SyncProxy() =>
        ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}{Base}/events/sync-from-manifest");

    [HttpPost("api/events/{id:guid}/archive")]
    public Task<IActionResult> ArchiveProxy(Guid id) =>
        ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}{Base}/events/{id}/archive");

    [HttpGet("api/template-slots")]
    public Task<IActionResult> TemplateSlotsProxy() =>
        ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}{Base}/template-slots");

    [HttpGet("api/lookups/{lookupKey}")]
    public Task<IActionResult> LookupProxy(string lookupKey) =>
        ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/lookups/{Uri.EscapeDataString(lookupKey)}");

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

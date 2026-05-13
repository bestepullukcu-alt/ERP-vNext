using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.Platform;

[Route("Platform/InterfaceRegistry")]
public sealed class InterfaceRegistryController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;

    public InterfaceRegistryController(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
    }

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Platform/InterfaceRegistry/Index.cshtml");

    [HttpGet("Details")]
    public IActionResult Details(string interfaceCode, string version)
    {
        ViewData["InterfaceCode"] = interfaceCode;
        ViewData["InterfaceVersion"] = version;
        return View("~/Views/Platform/InterfaceRegistry/Details.cshtml");
    }

    [HttpGet("api/interfaces")]
    public Task<IActionResult> InterfacesProxy() =>
        ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/interface-registry/interfaces{Request.QueryString}");

    [HttpGet("api/interfaces/{interfaceCode}/snapshot")]
    public Task<IActionResult> SnapshotProxy(string interfaceCode) =>
        ProxyGatewayAsync(
            HttpMethod.Get,
            $"{_gatewayUrl}/api/platform/interface-registry/interfaces/{Uri.EscapeDataString(interfaceCode)}/snapshot{Request.QueryString}");

    [HttpGet("api/discovery-batches")]
    public Task<IActionResult> DiscoveryBatchesProxy() =>
        ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/interface-registry/discovery-batches{Request.QueryString}");

    [HttpGet("api/discovery-batches/{batchId:guid}")]
    public Task<IActionResult> DiscoveryBatchProxy(Guid batchId) =>
        ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/interface-registry/discovery-batches/{batchId}");

    [HttpGet("api/discovery-batches/{batchId:guid}/diffs")]
    public Task<IActionResult> DiscoveryDiffsProxy(Guid batchId) =>
        ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/interface-registry/discovery-batches/{batchId}/diffs");

    [HttpPost("api/discovery-batches/{batchId:guid}/confirm")]
    public Task<IActionResult> ConfirmBatchProxy(Guid batchId) =>
        ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/interface-registry/discovery-batches/{batchId}/confirm");

    [HttpPost("api/discovery-batches/{batchId:guid}/reject")]
    public Task<IActionResult> RejectBatchProxy(Guid batchId) =>
        ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/interface-registry/discovery-batches/{batchId}/reject", readBody: true);

    [HttpPost("api/diffs/{diffItemId:guid}/confirm")]
    public Task<IActionResult> ConfirmDiffProxy(Guid diffItemId) =>
        ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/interface-registry/diffs/{diffItemId}/confirm");

    [HttpPost("api/diffs/{diffItemId:guid}/reject")]
    public Task<IActionResult> RejectDiffProxy(Guid diffItemId) =>
        ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/interface-registry/diffs/{diffItemId}/reject", readBody: true);

    [HttpPost("api/interfaces/{interfaceCode}/deprecate")]
    public Task<IActionResult> DeprecateProxy(string interfaceCode) =>
        ProxyGatewayAsync(
            HttpMethod.Post,
            $"{_gatewayUrl}/api/platform/interface-registry/interfaces/{Uri.EscapeDataString(interfaceCode)}/deprecate",
            readBody: true);

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
        var token = Request.Cookies["access_token"];
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

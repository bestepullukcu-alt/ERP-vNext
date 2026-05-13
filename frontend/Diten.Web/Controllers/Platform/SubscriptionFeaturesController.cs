using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.Platform;

[Route("Platform/SubscriptionFeatures")]
public sealed class SubscriptionFeaturesController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;

    public SubscriptionFeaturesController(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
    }

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Platform/SubscriptionFeatures/Index.cshtml");

    [HttpGet("api")]
    public Task<IActionResult> ListProxy()
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/subscription-features{Request.QueryString}");
    }

    [HttpGet("api/{id:guid}")]
    public Task<IActionResult> DetailProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/subscription-features/{id}");
    }

    [HttpPost("api")]
    public Task<IActionResult> CreateProxy()
    {
        return ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/subscription-features", readBody: true);
    }

    [HttpPut("api/{id:guid}")]
    public Task<IActionResult> UpdateProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Put, $"{_gatewayUrl}/api/platform/subscription-features/{id}", readBody: true);
    }

    [HttpPost("api/{id:guid}/archive")]
    public Task<IActionResult> ArchiveProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/subscription-features/{id}/archive", readBody: true);
    }

    [HttpPost("api/{id:guid}/deactivate")]
    public Task<IActionResult> DeactivateProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/subscription-features/{id}/deactivate", readBody: true);
    }

    [HttpGet("api/{id:guid}/plan-mappings")]
    public Task<IActionResult> FeatureMappingsProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/subscription-features/{id}/plan-mappings");
    }

    [HttpGet("api/categories")]
    public Task<IActionResult> CategoriesProxy()
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/feature-categories{Request.QueryString}");
    }

    [HttpPost("api/categories")]
    public Task<IActionResult> CreateCategoryProxy()
    {
        return ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/feature-categories", readBody: true);
    }

    [HttpGet("api/plans/active")]
    public Task<IActionResult> ActivePlansProxy()
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/subscription-plans/active");
    }

    [HttpPut("api/plans/{planId:guid}/features")]
    public Task<IActionResult> UpdatePlanFeaturesProxy(Guid planId)
    {
        return ProxyGatewayAsync(HttpMethod.Put, $"{_gatewayUrl}/api/platform/subscription-plans/{planId}/features", readBody: true);
    }

    private async Task<IActionResult> ProxyGatewayAsync(HttpMethod method, string targetUrl, bool readBody = false)
    {
        AddAuthHeader();
        using var request = new HttpRequestMessage(method, targetUrl);
        if (readBody)
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            request.Content = new StringContent(body, Encoding.UTF8, Request.ContentType ?? "application/json");
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

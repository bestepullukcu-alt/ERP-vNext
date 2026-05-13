using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

[Route("Platform/[controller]")]
public sealed class TenantsController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;

    public TenantsController(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        return View("~/Views/Platform/Tenants/Index.cshtml");
    }

    [HttpGet("Create")]
    public IActionResult Create()
    {
        return View("~/Views/Platform/Tenants/Create.cshtml");
    }

    [HttpGet("Details/{id:guid}")]
    public IActionResult Details(Guid id)
    {
        ViewData["TenantId"] = id;
        var actor = ResolveCurrentActor();
        ViewData["CurrentActorId"] = actor.Id;
        ViewData["CurrentActorDisplay"] = actor.Display;
        return View("~/Views/Platform/Tenants/Details.cshtml");
    }

    [HttpGet("Edit/{id:guid}")]
    public IActionResult Edit(Guid id)
    {
        ViewData["TenantId"] = id;
        ViewData["FormMode"] = "Edit";
        return View("~/Views/Platform/Tenants/Create.cshtml");
    }

    [HttpGet("/Platform/TenantSecurity")]
    public IActionResult Security()
    {
        return View("~/Views/Platform/Tenants/Security.cshtml");
    }

    [HttpGet("api")]
    public Task<IActionResult> ListProxy()
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/admin/tenants{Request.QueryString}");
    }

    [HttpGet("api/stats")]
    public Task<IActionResult> StatsProxy()
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/admin/tenants/stats");
    }

    [HttpGet("api/{id:guid}")]
    public Task<IActionResult> DetailProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/admin/tenants/{id}");
    }

    [HttpGet("api/{id:guid}/modules")]
    public Task<IActionResult> ModulesProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/admin/tenants/{id}/modules");
    }

    [HttpGet("api/{id:guid}/users/summary")]
    public Task<IActionResult> UsersSummaryProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/admin/tenants/{id}/users/summary");
    }

    [HttpGet("api/{id:guid}/admin-users")]
    public Task<IActionResult> AdminUsersProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/admin/tenants/{id}/admin-users");
    }

    [HttpPost("api/{id:guid}/admin-users")]
    public async Task<IActionResult> CreateAdminUserProxy(Guid id)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/admin/tenants/{id}/admin-users", body);
    }

    [HttpPut("api/{id:guid}/admin-users/{adminUserId:guid}")]
    public async Task<IActionResult> UpdateAdminUserProxy(Guid id, Guid adminUserId)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Put, $"{_gatewayUrl}/api/admin/tenants/{id}/admin-users/{adminUserId}", body);
    }

    [HttpDelete("api/{id:guid}/admin-users/{adminUserId:guid}")]
    public Task<IActionResult> DeleteAdminUserProxy(Guid id, Guid adminUserId)
    {
        return ProxyGatewayAsync(HttpMethod.Delete, $"{_gatewayUrl}/api/admin/tenants/{id}/admin-users/{adminUserId}");
    }

    [HttpPost("api/{id:guid}/admin-users/{adminUserId:guid}/invite")]
    public Task<IActionResult> InviteAdminUserProxy(Guid id, Guid adminUserId)
    {
        return ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/admin/tenants/{id}/admin-users/{adminUserId}/invite");
    }

    [HttpGet("api/{id:guid}/settings")]
    public Task<IActionResult> SettingsProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/admin/tenants/{id}/settings");
    }

    [HttpGet("api/{id:guid}/login-settings")]
    public Task<IActionResult> LoginSettingsProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/admin/tenants/{id}/login-settings");
    }

    [HttpPut("api/{id:guid}/login-settings")]
    public async Task<IActionResult> UpdateLoginSettingsProxy(Guid id)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Put, $"{_gatewayUrl}/api/admin/tenants/{id}/login-settings", body);
    }

    [HttpPost("api")]
    public async Task<IActionResult> CreateProxy()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/admin/tenants", body);
    }

    [HttpPut("api/{id:guid}")]
    public async Task<IActionResult> UpdateProxy(Guid id)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Put, $"{_gatewayUrl}/api/admin/tenants/{id}", body);
    }

    [HttpPut("api/{id:guid}/branding")]
    public async Task<IActionResult> UpdateBrandingProxy(Guid id)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Put, $"{_gatewayUrl}/api/admin/tenants/{id}/branding", body);
    }

    [HttpGet("api/subscription-plans/active")]
    public Task<IActionResult> ActiveSubscriptionPlansProxy()
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/subscription-plans/active");
    }

    [HttpGet("api/{id:guid}/commercial/subscription")]
    public Task<IActionResult> CommercialSubscriptionProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/tenants/{id}/commercial/subscription");
    }

    [HttpGet("api/{id:guid}/commercial/subscription/history")]
    public Task<IActionResult> CommercialSubscriptionHistoryProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/tenants/{id}/commercial/subscription/history");
    }

    [HttpGet("api/{id:guid}/commercial/module-entitlements")]
    public Task<IActionResult> ModuleEntitlementsProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/tenants/{id}/commercial/module-entitlements");
    }

    [HttpGet("api/{id:guid}/commercial/module-entitlements/available-modules")]
    public Task<IActionResult> AvailableModuleEntitlementsProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/tenants/{id}/commercial/module-entitlements/available-modules");
    }

    [HttpPost("api/{id:guid}/commercial/module-entitlements")]
    public async Task<IActionResult> AddModuleEntitlementProxy(Guid id)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/tenants/{id}/commercial/module-entitlements", body);
    }

    [HttpPost("api/{id:guid}/commercial/module-entitlements/{entitlementId:guid}/enable")]
    public async Task<IActionResult> EnableModuleEntitlementProxy(Guid id, Guid entitlementId)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/tenants/{id}/commercial/module-entitlements/{entitlementId}/enable", body);
    }

    [HttpPost("api/{id:guid}/commercial/module-entitlements/disable")]
    public async Task<IActionResult> DisableModuleEntitlementProxy(Guid id)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/tenants/{id}/commercial/module-entitlements/disable", body);
    }

    [HttpPatch("api/{id:guid}/commercial/module-entitlements/{entitlementId:guid}/expiry")]
    public async Task<IActionResult> UpdateModuleEntitlementExpiryProxy(Guid id, Guid entitlementId)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Patch, $"{_gatewayUrl}/api/platform/tenants/{id}/commercial/module-entitlements/{entitlementId}/expiry", body);
    }

    [HttpDelete("api/{id:guid}/commercial/module-entitlements/{entitlementId:guid}/manual-override")]
    public async Task<IActionResult> RemoveModuleEntitlementOverrideProxy(Guid id, Guid entitlementId)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Delete, $"{_gatewayUrl}/api/platform/tenants/{id}/commercial/module-entitlements/{entitlementId}/manual-override", body);
    }

    [HttpPost("api/{id:guid}/commercial/subscription")]
    public async Task<IActionResult> AssignCommercialSubscriptionProxy(Guid id)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/tenants/{id}/commercial/subscription", body);
    }

    [HttpPost("api/{id:guid}/commercial/subscription/{subscriptionId:guid}/{actionName}")]
    public async Task<IActionResult> CommercialSubscriptionActionProxy(Guid id, Guid subscriptionId, string actionName)
    {
        var allowed = new[] { "activate", "cancel", "renew", "suspend", "reactivate" };
        if (!allowed.Contains(actionName, StringComparer.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/tenants/{id}/commercial/subscription/{subscriptionId}/{actionName}", body);
    }

    [HttpGet("api/lookups/{**everything}")]
    public Task<IActionResult> LookupsProxy(string everything)
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/lookups/{everything}{Request.QueryString}");
    }

    [HttpPost("api/{id:guid}/{actionName}")]
    public async Task<IActionResult> LifecycleProxy(Guid id, string actionName)
    {
        if (!string.Equals(actionName, "suspend", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(actionName, "reactivate", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/admin/tenants/{id}/{actionName}", body);
    }

    [HttpDelete("api/{id:guid}")]
    public Task<IActionResult> DeleteProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Delete, $"{_gatewayUrl}/api/admin/tenants/{id}");
    }

    [HttpDelete("api/bulk")]
    public async Task<IActionResult> BulkDeleteProxy()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Delete, $"{_gatewayUrl}/api/admin/tenants/bulk", body);
    }

    private async Task<IActionResult> ProxyGatewayAsync(HttpMethod method, string targetUrl, string? jsonBody = null)
    {
        AddAuthHeader();
        using var request = new HttpRequestMessage(method, targetUrl);
        if (jsonBody is not null)
        {
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        }

        var response = await _httpClient.SendAsync(request);
        if (ProxyAuthFailure.IsAuthFailure(response.StatusCode))
        {
            ProxyAuthFailure.ClearAuthCookies(Response);
            return StatusCode((int)response.StatusCode, ProxyAuthFailure.PlatformLoginPayload());
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

    private (string Id, string Display) ResolveCurrentActor()
    {
        var token = Request.Cookies["access_token"];
        if (string.IsNullOrWhiteSpace(token))
        {
            return (string.Empty, string.Empty);
        }

        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            var id = FindClaim(jwt.Claims, JwtRegisteredClaimNames.Sub, ClaimTypes.NameIdentifier);
            var email = FindClaim(jwt.Claims, JwtRegisteredClaimNames.Email, ClaimTypes.Email, "email");
            var name = FindClaim(jwt.Claims, ClaimTypes.Name, "name", "preferred_username");
            var givenName = FindClaim(jwt.Claims, JwtRegisteredClaimNames.GivenName, ClaimTypes.GivenName);
            var familyName = FindClaim(jwt.Claims, JwtRegisteredClaimNames.FamilyName, ClaimTypes.Surname);
            var fullName = string.Join(' ', new[] { givenName, familyName }.Where(part => !string.IsNullOrWhiteSpace(part)));
            var display = email ?? name ?? (string.IsNullOrWhiteSpace(fullName) ? null : fullName) ?? string.Empty;
            return (id ?? string.Empty, display);
        }
        catch
        {
            return (string.Empty, string.Empty);
        }
    }

    private static string? FindClaim(IEnumerable<Claim> claims, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = claims.FirstOrDefault(claim => claim.Type == claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}

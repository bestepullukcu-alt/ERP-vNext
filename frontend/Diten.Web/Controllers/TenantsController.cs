using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

[Route("Platform/[controller]")]
public sealed class TenantsController : Controller
{
    private const string QuotaViewPermission = "platform.tenants.quotas.view";
    private static readonly IReadOnlySet<string> StandardQuotaKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "users.max",
        "storage.gb.max",
        "api.calls.per.month",
        "modules.max"
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

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

    [HttpGet("{id:guid}/QuotaStatus")]
    public async Task<IActionResult> QuotaStatusProxy(Guid id, CancellationToken ct)
    {
        if (!HasPermission(QuotaViewPermission))
        {
            return StatusCode(403, TenantQuotaGovernanceResponse.Unauthorized(id));
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_gatewayUrl}/api/platform/tenants/{id}/quotas");
            AddAuthHeader(request);

            using var response = await _httpClient.SendAsync(request, ct);
            var content = await response.Content.ReadAsStringAsync(ct);

            if ((int)response.StatusCode is 401 or 403)
            {
                return StatusCode((int)response.StatusCode, TenantQuotaGovernanceResponse.Unauthorized(id));
            }

            if ((int)response.StatusCode == 404)
            {
                return StatusCode(404, TenantQuotaGovernanceResponse.Error(id, "route-missing", "Quota status route or data was not found."));
            }

            if ((int)response.StatusCode == 503)
            {
                return StatusCode(503, TenantQuotaGovernanceResponse.Error(id, "degraded", "Quota status is temporarily unavailable."));
            }

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, TenantQuotaGovernanceResponse.Error(id, "error", "Quota status could not be loaded."));
            }

            var quotas = ExtractQuotaItems(content);
            if (quotas is null)
            {
                return StatusCode(502, TenantQuotaGovernanceResponse.Error(id, "error", "Quota status response could not be read."));
            }

            if (quotas.Any(item => item.TenantId != id))
            {
                return StatusCode(409, TenantQuotaGovernanceResponse.Error(id, "tenant-mismatch", "Quota status data does not match the selected tenant."));
            }

            var items = quotas.Select(ToQuotaGovernanceItem).ToList();
            if (items.Count == 0 || MissingStandardQuotaKeys(items))
            {
                var fallbackItems = await FetchSubscriptionQuotaFallbackAsync(id, items, ct);
                if (fallbackItems.Count > 0)
                {
                    items = items
                        .Concat(fallbackItems)
                        .GroupBy(item => item.QuotaKey, StringComparer.OrdinalIgnoreCase)
                        .Select(group => group.First())
                        .ToList();
                }
            }

            return Json(new TenantQuotaGovernanceResponse(
                id,
                items,
                items.Count == 0 ? "empty" : "ready",
                items.Count == 0 ? "No quota data is available for this tenant." : null,
                DateTimeOffset.UtcNow));
        }
        catch (TaskCanceledException)
        {
            return StatusCode(503, TenantQuotaGovernanceResponse.Error(id, "degraded", "Quota status request timed out."));
        }
        catch (HttpRequestException)
        {
            return StatusCode(503, TenantQuotaGovernanceResponse.Error(id, "degraded", "Quota status is temporarily unavailable."));
        }
        catch (JsonException)
        {
            return StatusCode(502, TenantQuotaGovernanceResponse.Error(id, "error", "Quota status response could not be read."));
        }
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
        if (!TryGetPlatformAccessToken(out var token))
        {
            ProxyAuthFailure.ClearAuthCookies(Response);
            return StatusCode(StatusCodes.Status401Unauthorized, ProxyAuthFailure.PlatformLoginPayload());
        }

        using var request = new HttpRequestMessage(method, targetUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
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

        if (ProxyAuthFailure.IsForbidden(response.StatusCode))
        {
            var forbiddenContent = await response.Content.ReadAsStringAsync();
            return new ContentResult
            {
                Content = forbiddenContent,
                ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
                StatusCode = StatusCodes.Status403Forbidden
            };
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

    private bool TryGetPlatformAccessToken(out string token)
    {
        token = Request.Cookies["access_token"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            var actorType = FindClaim(jwt.Claims, "actor_type");
            return jwt.ValidTo > DateTime.UtcNow &&
                   (string.Equals(actorType, "platform_admin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(actorType, "partner_admin", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            token = string.Empty;
            return false;
        }
    }

    private void AddAuthHeader(HttpRequestMessage request)
    {
        if (TryGetPlatformAccessToken(out var token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    private bool HasPermission(string permission)
    {
        var claims = User?.Claims ?? Enumerable.Empty<Claim>();
        if (ClaimsContainPermission(claims, permission) || ClaimsContainPlatformRole(claims))
        {
            return true;
        }

        var token = Request.Cookies["access_token"];
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            return ClaimsContainPermission(jwt.Claims, permission) || ClaimsContainPlatformRole(jwt.Claims);
        }
        catch
        {
            return false;
        }
    }

    private static bool ClaimsContainPermission(IEnumerable<Claim> claims, string permission) =>
        claims.Any(claim =>
            string.Equals(claim.Type, "permission", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(claim.Value, permission, StringComparison.OrdinalIgnoreCase));

    private static bool ClaimsContainPlatformRole(IEnumerable<Claim> claims) =>
        claims.Any(claim =>
            string.Equals(claim.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(claim.Value, "SuperAdmin", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(claim.Value, "Admin", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(claim.Value, "PlatformAdmin", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(claim.Value, "CommercialAdmin", StringComparison.OrdinalIgnoreCase)));

    private static IReadOnlyList<QuotaStatusGatewayDto>? ExtractQuotaItems(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        var data = TryGetProperty(root, "data", out var camelData)
            ? camelData
            : TryGetProperty(root, "Data", out var pascalData)
                ? pascalData
                : root;

        return data.ValueKind switch
        {
            JsonValueKind.Array => JsonSerializer.Deserialize<List<QuotaStatusGatewayDto>>(data.GetRawText(), JsonOptions),
            JsonValueKind.Object => JsonSerializer.Deserialize<QuotaStatusGatewayDto>(data.GetRawText(), JsonOptions) is { } item ? [item] : [],
            _ => []
        };
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement property)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out property))
        {
            return true;
        }

        property = default;
        return false;
    }

    private static TenantQuotaGovernanceItemViewModel ToQuotaGovernanceItem(QuotaStatusGatewayDto dto)
    {
        var displayLabel = string.IsNullOrWhiteSpace(dto.QuotaKey) ? "Quota" : QuotaDisplayLabel(dto.QuotaKey);

        var status = dto.LimitValue <= 0
            ? "configurationMissing"
            : dto.IsLimitExceeded
                ? "overLimit"
                : dto.UsagePercent >= 100
                    ? "atLimit"
                    : dto.IsWarning
                        ? "warning"
                        : "healthy";

        return new TenantQuotaGovernanceItemViewModel(
            dto.TenantId,
            dto.QuotaKey ?? string.Empty,
            displayLabel,
            dto.CurrentValue,
            dto.LimitValue,
            dto.UsagePercent,
            dto.IsWarning,
            dto.IsLimitExceeded,
            status,
            dto.PeriodStart,
            dto.PeriodEnd,
            dto.Source,
            dto.OverrideSource,
            string.Equals(dto.QuotaKey, "api.calls.per.month", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<IReadOnlyList<TenantQuotaGovernanceItemViewModel>> FetchSubscriptionQuotaFallbackAsync(
        Guid tenantId,
        IReadOnlyCollection<TenantQuotaGovernanceItemViewModel> existingItems,
        CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_gatewayUrl}/api/platform/tenants/{tenantId}/commercial/subscription/entitlements");
            AddAuthHeader(request);

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            var snapshot = ExtractSubscriptionSnapshot(content);
            if (snapshot?.TenantId != tenantId || snapshot.Quotas is null || snapshot.Quotas.Count == 0)
            {
                return [];
            }

            var existingKeys = existingItems.Select(item => item.QuotaKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var now = DateTimeOffset.UtcNow;
            var status = snapshot.IsActive ? "healthy" : "subscriptionInactive";
            return snapshot.Quotas
                .Where(item => IsStandardQuotaKey(item.Key) && !existingKeys.Contains(item.Key))
                .Select(item => new TenantQuotaGovernanceItemViewModel(
                    tenantId,
                    item.Key.Trim().ToLowerInvariant(),
                    QuotaDisplayLabel(item.Key),
                    0,
                    item.Value,
                    0,
                    false,
                    false,
                    item.Value <= 0 ? "configurationMissing" : status,
                    now,
                    snapshot.CurrentPeriodEndUtc ?? now.AddMonths(1),
                    "PlanDefault",
                    null,
                    string.Equals(item.Key, "api.calls.per.month", StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static TenantSubscriptionEntitlementSnapshotGatewayDto? ExtractSubscriptionSnapshot(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        var data = TryGetProperty(root, "data", out var camelData)
            ? camelData
            : TryGetProperty(root, "Data", out var pascalData)
                ? pascalData
                : root;

        return data.ValueKind == JsonValueKind.Object
            ? JsonSerializer.Deserialize<TenantSubscriptionEntitlementSnapshotGatewayDto>(data.GetRawText(), JsonOptions)
            : null;
    }

    private static bool MissingStandardQuotaKeys(IReadOnlyCollection<TenantQuotaGovernanceItemViewModel> items)
    {
        var keys = items.Select(item => item.QuotaKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return StandardQuotaKeys.Any(key => !keys.Contains(key));
    }

    private static bool IsStandardQuotaKey(string quotaKey) =>
        StandardQuotaKeys.Contains(quotaKey.Trim().ToLowerInvariant());

    private static string QuotaDisplayLabel(string quotaKey) =>
        quotaKey.Trim().ToLowerInvariant() switch
        {
            "users.max" => "Users",
            "storage.gb.max" => "Storage",
            "api.calls.per.month" => "API Calls This Month",
            "modules.max" => "Enabled Modules",
            _ => "Quota"
        };

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

    private sealed record TenantQuotaGovernanceResponse(
        Guid TenantId,
        IReadOnlyList<TenantQuotaGovernanceItemViewModel> Items,
        string State,
        string? Message,
        DateTimeOffset LoadedAtUtc)
    {
        public static TenantQuotaGovernanceResponse Unauthorized(Guid tenantId) =>
            new(tenantId, [], "unauthorized", "You do not have permission to view tenant quota governance.", DateTimeOffset.UtcNow);

        public static TenantQuotaGovernanceResponse Error(Guid tenantId, string state, string message) =>
            new(tenantId, [], state, message, DateTimeOffset.UtcNow);
    }

    private sealed record TenantQuotaGovernanceItemViewModel(
        Guid TenantId,
        string QuotaKey,
        string DisplayLabel,
        decimal CurrentValue,
        decimal LimitValue,
        decimal UsagePercent,
        bool IsWarning,
        bool IsLimitExceeded,
        string Status,
        DateTimeOffset? PeriodStart,
        DateTimeOffset? PeriodEnd,
        string? Source,
        string? OverrideSource,
        bool ApiCallsMvpNote);

    private sealed record QuotaStatusGatewayDto(
        Guid TenantId,
        string? QuotaKey,
        decimal CurrentValue,
        decimal LimitValue,
        decimal UsagePercent,
        bool IsWarning,
        bool IsLimitExceeded,
        DateTimeOffset? PeriodStart,
        DateTimeOffset? PeriodEnd,
        string? Source,
        Guid? SubscriptionId,
        Guid? PlanId,
        string? OverrideSource,
        bool WarningNotificationSentForPeriod,
        bool LimitBreachNotificationSentForPeriod,
        DateTimeOffset? LastWarningNotifiedAtUtc,
        DateTimeOffset? LastLimitBreachNotifiedAtUtc);

    private sealed record TenantSubscriptionEntitlementSnapshotGatewayDto(
        Guid TenantId,
        bool IsActive,
        Guid? PlanId,
        string? PlanCode,
        string? PlanName,
        JsonElement? Status,
        DateTimeOffset? CurrentPeriodEndUtc,
        IReadOnlyDictionary<string, decimal>? Quotas,
        IReadOnlyList<string>? IncludedFeatures,
        IReadOnlyList<string>? IncludedModuleKeys);
}

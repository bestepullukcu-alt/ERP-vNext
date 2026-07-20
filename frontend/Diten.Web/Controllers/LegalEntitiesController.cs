using System.Text;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

// MOD-0220 — Legal Entities (tenant shell, Master Data). Slim pattern: list + create (offcanvas) +
// lifecycle (activate/archive) + delete via same-origin AJAX to these proxy actions, which forward to the
// gateway (MDM downstream) with the server-side HttpOnly token and the tenant claim. Backend
// [HasPermission] is authoritative.
[Route("LegalEntities")]
public sealed class LegalEntitiesController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;

    public LegalEntitiesController(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
    }

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/MasterData/LegalEntities/Index.cshtml");

    // Full-page 8-step create/edit wizard (Görev 5). The id-less route is "create"; the {id} route is "edit".
    [HttpGet("Wizard")]
    public IActionResult Wizard()
    {
        ViewData["LegalEntityId"] = string.Empty;
        return View("~/Views/MasterData/LegalEntities/Wizard.cshtml");
    }

    [HttpGet("Wizard/{id:guid}")]
    public IActionResult WizardEdit(Guid id)
    {
        ViewData["LegalEntityId"] = id.ToString();
        return View("~/Views/MasterData/LegalEntities/Wizard.cshtml");
    }

    // Full details page (Görev 6) — all 8 sections, every field, lifecycle actions.
    [HttpGet("Details/{id:guid}")]
    public IActionResult Details(Guid id)
    {
        ViewData["LegalEntityId"] = id.ToString();
        return View("~/Views/MasterData/LegalEntities/Details.cshtml");
    }

    [HttpGet("api")]
    public Task<IActionResult> ListProxy()
    {
        var targetUrl = $"{_gatewayUrl}/api/legal-entities{Request.QueryString}";
        return ProxyGatewayAsync(HttpMethod.Get, targetUrl);
    }

    [HttpGet("api/{id:guid}")]
    public Task<IActionResult> GetByIdProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/legal-entities/{id}");
    }

    [HttpPost("api")]
    public async Task<IActionResult> CreateProxy()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/legal-entities", body);
    }

    [HttpPut("api/{id:guid}")]
    public async Task<IActionResult> UpdateProxy(Guid id)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Put, $"{_gatewayUrl}/api/legal-entities/{id}", body);
    }

    [HttpPatch("api/{id:guid}/activate")]
    public Task<IActionResult> ActivateProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Patch, $"{_gatewayUrl}/api/legal-entities/{id}/activate");
    }

    [HttpPatch("api/{id:guid}/suspend")]
    public Task<IActionResult> SuspendProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Patch, $"{_gatewayUrl}/api/legal-entities/{id}/suspend");
    }

    [HttpPatch("api/{id:guid}/archive")]
    public Task<IActionResult> ArchiveProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Patch, $"{_gatewayUrl}/api/legal-entities/{id}/archive");
    }

    [HttpDelete("api/{id:guid}")]
    public Task<IActionResult> DeleteProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Delete, $"{_gatewayUrl}/api/legal-entities/{id}");
    }

    // MDM lookups: legal-form, organization-role, control-type, accounting-standard, tax-regime.
    [HttpGet("api/lookups/{type}")]
    public Task<IActionResult> LookupProxy(string type)
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/legal-entities/lookups/{Uri.EscapeDataString(type)}");
    }

    // İŞ3 — referenceable Legal Entity list feeding the Structure section's Parent select2 (Code + Name, GUID hidden).
    // Returns only ACTIVE/referenceable entities; the wizard excludes the current entity in edit mode.
    [HttpGet("api/lookup")]
    public Task<IActionResult> LookupListProxy()
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/legal-entities/lookup");
    }

    // Universal ISO reference lookups (countries, currencies). Routed to the TENANT-accessible reference endpoint
    // (/api/lookups/reference/*) — the main /api/lookups surface is platform-admin-only and 403s tenant users.
    [HttpGet("api/platform-lookups/{key}")]
    public Task<IActionResult> PlatformLookupProxy(string key)
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/lookups/reference/{Uri.EscapeDataString(key)}");
    }

    // MOD-0220 — the wizard's Legal Form / Country / Base Currency now come from governed Business Reference Data
    // (BRD) published values via the tenant-accessible read (allow-listed to these sets). Returns {data:{items:[…]}}.
    [HttpGet("api/reference-data/{setCode}")]
    public Task<IActionResult> ReferenceDataProxy(string setCode)
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/lookups/reference-data/sets/{Uri.EscapeDataString(setCode)}/published-values");
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

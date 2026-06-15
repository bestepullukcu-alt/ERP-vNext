using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models;
using Diten.Web.Models.Governance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

// FE-C (MOD-0018-FU9) — tenant Roles CRUD page. Gateway-proxy MVC controller mirroring the
// golden-reference Slim pattern: mutations go through here (antiforgery + server-side bearer/tenant
// header forwarding), the datatable list is loaded client-side from the gateway. UX-only; backend
// [HasPermission] on AuthService /api/roles is authoritative.
[Authorize]
[Route("Roles")]
public sealed class RolesController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<RolesController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public RolesController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<RolesController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Governance/Roles/Index.cshtml");

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] RoleEditViewModel model)
    {
        if (!ModelState.IsValid)
            return Json(new { success = false, errors = CollectModelErrors() });

        if (!AddAuthHeaders())
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        try
        {
            var payload = new RoleCreatePayload { Name = model.Name, DisplayName = model.DisplayName, Description = model.Description };
            var response = await _httpClient.PostAsJsonAsync($"{_gatewayUrl}/api/roles", payload, _jsonOptions);
            return response.IsSuccessStatusCode
                ? Json(new { success = true })
                : Json(new { success = false, errors = await ExtractGatewayErrorsAsync(response) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Roles create failed.");
            return Json(new { success = false, errors = BuildExceptionErrors(ex) });
        }
    }

    [HttpPost("edit/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, [FromForm] RoleEditViewModel model)
    {
        model.Id = id;
        if (!ModelState.IsValid)
            return Json(new { success = false, errors = CollectModelErrors() });

        if (!AddAuthHeaders())
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        try
        {
            // AuthService UpdateRoleRequest is { displayName, description } — name is immutable.
            var payload = new RoleUpdatePayload { DisplayName = model.DisplayName, Description = model.Description };
            var response = await _httpClient.PutAsJsonAsync($"{_gatewayUrl}/api/roles/{id}", payload, _jsonOptions);
            return response.IsSuccessStatusCode
                ? Json(new { success = true })
                : Json(new { success = false, errors = await ExtractGatewayErrorsAsync(response) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Roles edit failed for {RoleId}.", id);
            return Json(new { success = false, errors = BuildExceptionErrors(ex) });
        }
    }

    [HttpGet("get/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        if (!AddAuthHeaders())
            return Json(new { success = false });

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/roles/{id}");
            if (!response.IsSuccessStatusCode)
                return Json(new { success = false });

            var payload = await response.Content.ReadFromJsonAsync<GovernanceGatewayResponse<RoleDetailViewModel>>(_jsonOptions);
            var model = payload?.Data;
            if (model is null)
                return Json(new { success = false });

            return Json(new
            {
                success = true,
                data = new
                {
                    id = model.Id,
                    name = model.Name,
                    displayName = model.DisplayName,
                    description = model.Description,
                    isSystem = model.IsSystem,
                    permissionCount = model.PermissionCount
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Roles get-by-id failed for {RoleId}.", id);
            return Json(new { success = false });
        }
    }

    private List<string> CollectModelErrors() =>
        ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();

    private List<string> BuildExceptionErrors(Exception ex)
    {
        var message = ex.GetBaseException().Message;
        return [string.IsNullOrWhiteSpace(message) ? _sharedLocalizer["GatewayError"].Value : message];
    }

    private async Task<List<string>> ExtractGatewayErrorsAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return [_sharedLocalizer["Unauthorized"].Value];

        try
        {
            var payload = await response.Content.ReadFromJsonAsync<GovernanceGatewayResponse<object>>(_jsonOptions);
            var errors = payload?.Errors?.Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
            if (errors?.Count > 0)
                return errors;
        }
        catch { }

        var raw = await response.Content.ReadAsStringAsync();
        return [string.IsNullOrWhiteSpace(raw) ? _sharedLocalizer["GatewayError"].Value : raw];
    }

    private bool AddAuthHeaders()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
        var token = Request.Cookies["access_token"];
        if (!string.IsNullOrWhiteSpace(token))
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (_httpClient.DefaultRequestHeaders.Contains("X-Tenant-Id"))
            _httpClient.DefaultRequestHeaders.Remove("X-Tenant-Id");

        var tenantId = GetTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
            return false;

        _httpClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        return true;
    }

    private string? GetTenantId() =>
        User.Claims.FirstOrDefault(x =>
            x.Type == "tenantId" ||
            x.Type == "tenant_id" ||
            x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;
}

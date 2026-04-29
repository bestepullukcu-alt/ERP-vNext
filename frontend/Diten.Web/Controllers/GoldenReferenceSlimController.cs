using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models;
using Diten.Web.Models.GoldenReferenceSlim;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Diten.Web.Controllers;

[Authorize]
[Route("GoldenReferenceSlim")]
public sealed class GoldenReferenceSlimController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<GoldenReferenceSlimController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public GoldenReferenceSlimController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<GoldenReferenceSlimController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/DevEnablement/GoldenReferenceSlim/Index.cshtml");

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] GoldenReferenceSlimEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return Json(new
            {
                success = false,
                errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList()
            });
        }

        if (!AddAuthHeaders())
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_gatewayUrl}/api/golden-reference-slim",
                ToPayload(model),
                _jsonOptions);

            if (response.IsSuccessStatusCode)
                return Json(new { success = true });

            var errors = await ExtractGatewayErrorsAsync(response);
            return Json(new { success = false, errors });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GoldenReferenceSlim create failed.");
            return Json(new { success = false, errors = BuildExceptionErrors(ex) });
        }
    }

    [HttpPost("edit/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, [FromForm] GoldenReferenceSlimEditViewModel model)
    {
        model.Id = id;

        if (!ModelState.IsValid)
        {
            return Json(new
            {
                success = false,
                errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList()
            });
        }

        if (!AddAuthHeaders())
            return Json(new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        try
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"{_gatewayUrl}/api/golden-reference-slim/{id}",
                ToPayload(model),
                _jsonOptions);

            if (response.IsSuccessStatusCode)
                return Json(new { success = true });

            var errors = await ExtractGatewayErrorsAsync(response);
            return Json(new { success = false, errors });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GoldenReferenceSlim edit failed for {GoldenReferenceSlimId}.", id);
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
            var model = await LoadApiModelAsync(id);
            if (model is null)
                return Json(new { success = false });

            return Json(new
            {
                success = true,
                data = new
                {
                    id = model.Id,
                    code = model.Code,
                    name = model.Name,
                    description = model.Description,
                    referenceType = model.ReferenceType,
                    priority = model.Priority,
                    isActive = model.IsActive
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GoldenReferenceSlim get-by-id failed for {GoldenReferenceSlimId}.", id);
            return Json(new { success = false });
        }
    }

    [HttpGet("lookups")]
    public async Task<IActionResult> Lookups()
    {
        if (!AddAuthHeaders())
            return Json(new { referenceTypes = Array.Empty<object>(), priorities = Array.Empty<object>() });

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/golden-reference-slim");
            if (!response.IsSuccessStatusCode)
                return Json(new { referenceTypes = Array.Empty<object>(), priorities = Array.Empty<object>() });

            var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<List<GoldenReferenceSlimDetailViewModel>>>(_jsonOptions);
            var list = payload?.Data ?? [];

            var referenceTypes = list
                .Select(x => x.ReferenceType)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .Select(x => new { value = x!, text = x! })
                .ToList();

            var priorities = list
                .Select(x => x.Priority)
                .Distinct()
                .OrderBy(x => x)
                .Select(x => new
                {
                    value = x.ToString(),
                    text = $"{_sharedLocalizer["LevelPrefix"].Value} {x}"
                })
                .ToList();

            return Json(new { referenceTypes, priorities });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GoldenReferenceSlim lookup load failed.");
            return Json(new { referenceTypes = Array.Empty<object>(), priorities = Array.Empty<object>() });
        }
    }

    private async Task<GoldenReferenceSlimDetailViewModel?> LoadApiModelAsync(Guid id)
    {
        if (!AddAuthHeaders())
            return null;

        var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/golden-reference-slim/{id}");
        if (!response.IsSuccessStatusCode)
            return null;

        var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<GoldenReferenceSlimDetailViewModel>>(_jsonOptions);
        return payload?.Data;
    }

    private List<string> BuildExceptionErrors(Exception ex)
    {
        var message = ex.GetBaseException().Message;
        if (string.IsNullOrWhiteSpace(message))
            message = _sharedLocalizer["GatewayError"].Value;

        return [message];
    }

    private static GoldenReferenceSlimSavePayload ToPayload(GoldenReferenceSlimEditViewModel model) => new()
    {
        Code = model.Code,
        Name = model.Name,
        Description = model.Description,
        ReferenceType = model.ReferenceType,
        Priority = model.Priority,
        IsActive = model.IsActive
    };

    private async Task<List<string>> ExtractGatewayErrorsAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return [_sharedLocalizer["Unauthorized"].Value];

        try
        {
            var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<object>>(_jsonOptions);
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

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using Diten.Web.Models.ModuleCatalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers.Platform;

[Route("Platform/ModuleCatalog")]
public sealed class ModuleCatalogController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<ModuleCatalogController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public ModuleCatalogController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<ModuleCatalogController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Platform/ModuleCatalog/Index.cshtml");

    [AllowAnonymous]
    [HttpGet("/ModuleCatalog")]
    public IActionResult LegacyIndexRedirect() => RedirectToAction(nameof(Index));

    [HttpGet("Create")]
    public IActionResult Create() => View("~/Views/Platform/ModuleCatalog/Create.cshtml", new ModuleCatalogEditViewModel());

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] ModuleCatalogEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("~/Views/Platform/ModuleCatalog/Create.cshtml", model);
        }

        AddAuthHeader();
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_gatewayUrl}/api/platform/module-catalog", ToPayload(model), _jsonOptions);
            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = _sharedLocalizer["RecordCreated"].Value;
                return RedirectToAction(nameof(Index));
            }

            AddGatewayErrorsToModelState(await ExtractGatewayErrorsAsync(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ModuleCatalog create failed.");
            ModelState.AddModelError(string.Empty, _sharedLocalizer["GatewayError"].Value);
        }

        return View("~/Views/Platform/ModuleCatalog/Create.cshtml", model);
    }

    [HttpGet("Edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var detail = await LoadApiModelAsync(id);
        if (detail is null)
        {
            TempData["ErrorMessage"] = _sharedLocalizer["GatewayError"].Value;
            return RedirectToAction(nameof(Index));
        }

        return View("~/Views/Platform/ModuleCatalog/Edit.cshtml", ToEditModel(detail));
    }

    [HttpPost("Edit/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, [FromForm] ModuleCatalogEditViewModel model)
    {
        model.Id = id;
        if (!ModelState.IsValid)
        {
            return View("~/Views/Platform/ModuleCatalog/Edit.cshtml", model);
        }

        AddAuthHeader();
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"{_gatewayUrl}/api/platform/module-catalog/{id}", ToPayload(model), _jsonOptions);
            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = _sharedLocalizer["RecordUpdated"].Value;
                return RedirectToAction(nameof(Index));
            }

            AddGatewayErrorsToModelState(await ExtractGatewayErrorsAsync(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ModuleCatalog edit failed for {ModuleCatalogId}.", id);
            ModelState.AddModelError(string.Empty, _sharedLocalizer["GatewayError"].Value);
        }

        return View("~/Views/Platform/ModuleCatalog/Edit.cshtml", model);
    }

    [HttpGet("Details/{id:guid}")]
    public async Task<IActionResult> Details(Guid id)
    {
        var detail = await LoadApiModelAsync(id);
        if (detail is null)
        {
            TempData["ErrorMessage"] = _sharedLocalizer["GatewayError"].Value;
            return RedirectToAction(nameof(Index));
        }

        return View("~/Views/Platform/ModuleCatalog/Details.cshtml", detail);
    }

    [HttpGet("api")]
    public Task<IActionResult> ListProxy()
    {
        var targetUrl = $"{_gatewayUrl}/api/platform/module-catalog{Request.QueryString}";
        return ProxyGatewayAsync(HttpMethod.Get, targetUrl);
    }

    [HttpGet("api/stats")]
    public Task<IActionResult> StatsProxy()
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/module-catalog/stats");
    }

    [HttpDelete("api/{id:guid}")]
    public Task<IActionResult> DeleteProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Delete, $"{_gatewayUrl}/api/platform/module-catalog/{id}");
    }

    [HttpDelete("api/bulk")]
    public async Task<IActionResult> BulkDeleteProxy()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Delete, $"{_gatewayUrl}/api/platform/module-catalog/bulk", body);
    }

    [HttpPost("api/{id:guid}/activate")]
    public Task<IActionResult> ActivateProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/module-catalog/{id}/activate");
    }

    [HttpPost("api/{id:guid}/deactivate")]
    public Task<IActionResult> DeactivateProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/module-catalog/{id}/deactivate");
    }

    private async Task<ModuleCatalogDetailViewModel?> LoadApiModelAsync(Guid id)
    {
        AddAuthHeader();
        var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/platform/module-catalog/{id}");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<ModuleCatalogDetailViewModel>>(_jsonOptions);
        return payload?.Data;
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

    private void AddGatewayErrorsToModelState(IEnumerable<string> errors)
    {
        foreach (var error in errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }
    }

    private async Task<List<string>> ExtractGatewayErrorsAsync(HttpResponseMessage response)
    {
        try
        {
            var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<object>>(_jsonOptions);
            if (payload?.Errors.Count > 0)
            {
                return payload.Errors;
            }
        }
        catch { }

        var raw = await response.Content.ReadAsStringAsync();
        return [string.IsNullOrWhiteSpace(raw) ? _sharedLocalizer["GatewayError"].Value : raw];
    }

    private static ModuleCatalogEditViewModel ToEditModel(ModuleCatalogDetailViewModel model) => new()
    {
        Id = model.Id,
        ModuleCode = model.ModuleCode,
        ModuleName = model.ModuleName,
        DisplayName = model.DisplayName,
        Description = model.Description,
        Domain = model.Domain,
        Service = model.Service,
        Category = model.Category,
        Status = model.Status,
        ModuleVersion = model.ModuleVersion,
        IsCoreModule = model.IsCoreModule,
        IsTenantAssignable = model.IsTenantAssignable,
        SortOrder = model.SortOrder
    };

    private static ModuleCatalogSavePayload ToPayload(ModuleCatalogEditViewModel model) => new()
    {
        ModuleCode = model.ModuleCode,
        ModuleName = model.ModuleName,
        DisplayName = model.DisplayName,
        Description = model.Description,
        Domain = model.Domain,
        Service = model.Service,
        Category = model.Category,
        Status = model.Status,
        ModuleVersion = model.ModuleVersion,
        IsCoreModule = model.IsCoreModule,
        IsTenantAssignable = model.IsTenantAssignable,
        SortOrder = model.SortOrder
    };
}

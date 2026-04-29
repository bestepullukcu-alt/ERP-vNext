using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models;
using Diten.Web.Models.GoldenReferenceCompact;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Diten.Web.Controllers;

[Authorize]
[Route("GoldenReferenceCompact")]
public sealed class GoldenReferenceCompactController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<GoldenReferenceCompactController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public GoldenReferenceCompactController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<GoldenReferenceCompactController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/DevEnablement/GoldenReferenceCompact/Index.cshtml");

    [HttpGet("Create")]
    public IActionResult Create() =>
        View("~/Views/DevEnablement/GoldenReferenceCompact/Create.cshtml", new GoldenReferenceCompactEditViewModel());

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] GoldenReferenceCompactEditViewModel model)
    {
        if (!ModelState.IsValid)
            return View("~/Views/DevEnablement/GoldenReferenceCompact/Create.cshtml", model);

        if (!AddAuthHeaders())
        {
            ModelState.AddModelError(string.Empty, _sharedLocalizer["Unauthorized"].Value);
            return View("~/Views/DevEnablement/GoldenReferenceCompact/Create.cshtml", model);
        }

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_gatewayUrl}/api/golden-reference-compact",
                ToPayload(model),
                _jsonOptions);

            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = _sharedLocalizer["RecordCreated"].Value;
                return RedirectToAction(nameof(Index));
            }

            AddGatewayErrorsToModelState(await ExtractGatewayErrorsAsync(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GoldenReferenceCompact create failed.");
            AddGatewayErrorsToModelState(BuildExceptionErrors(ex));
        }

        return View("~/Views/DevEnablement/GoldenReferenceCompact/Create.cshtml", model);
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

        return View("~/Views/DevEnablement/GoldenReferenceCompact/Edit.cshtml", ToEditModel(detail));
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

        return View("~/Views/DevEnablement/GoldenReferenceCompact/Details.cshtml", ToEditModel(detail));
    }

    [HttpPost("Edit/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, [FromForm] GoldenReferenceCompactEditViewModel model)
    {
        model.Id = id;
        if (!ModelState.IsValid)
            return View("~/Views/DevEnablement/GoldenReferenceCompact/Edit.cshtml", model);

        if (!AddAuthHeaders())
        {
            ModelState.AddModelError(string.Empty, _sharedLocalizer["Unauthorized"].Value);
            return View("~/Views/DevEnablement/GoldenReferenceCompact/Edit.cshtml", model);
        }

        try
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"{_gatewayUrl}/api/golden-reference-compact/{id}",
                ToPayload(model),
                _jsonOptions);

            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = _sharedLocalizer["RecordUpdated"].Value;
                return RedirectToAction(nameof(Index));
            }

            AddGatewayErrorsToModelState(await ExtractGatewayErrorsAsync(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GoldenReferenceCompact edit failed for {GoldenReferenceCompactId}.", id);
            AddGatewayErrorsToModelState(BuildExceptionErrors(ex));
        }

        return View("~/Views/DevEnablement/GoldenReferenceCompact/Edit.cshtml", model);
    }

    [HttpGet("get/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var model = await LoadApiModelAsync(id);
        if (model is null)
            return Json(new { success = false });

        return Json(new { success = true, data = model });
    }

    [HttpGet("lookups")]
    public async Task<IActionResult> Lookups()
    {
        if (!AddAuthHeaders())
            return Json(EmptyLookups());

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/golden-reference-compact");
            if (!response.IsSuccessStatusCode)
                return Json(EmptyLookups());

            var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<List<GoldenReferenceCompactDetailViewModel>>>(_jsonOptions);
            var list = payload?.Data ?? [];

            return Json(new
            {
                referenceTypes = ToLookup(list.Select(x => x.ReferenceType)),
                categories = ToLookup(list.Select(x => x.Category)),
                owners = ToLookup(list.Select(x => x.Owner)),
                priorities = list
                    .Select(x => x.Priority)
                    .Distinct()
                    .OrderBy(x => x)
                    .Select(x => new { value = x.ToString(), text = $"{_sharedLocalizer["LevelPrefix"].Value} {x}" })
                    .ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GoldenReferenceCompact lookup load failed.");
            return Json(EmptyLookups());
        }
    }

    private async Task<GoldenReferenceCompactDetailViewModel?> LoadApiModelAsync(Guid id)
    {
        if (!AddAuthHeaders())
            return null;

        var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/golden-reference-compact/{id}");
        if (!response.IsSuccessStatusCode)
            return null;

        var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<GoldenReferenceCompactDetailViewModel>>(_jsonOptions);
        return payload?.Data;
    }

    private static GoldenReferenceCompactEditViewModel ToEditModel(GoldenReferenceCompactDetailViewModel model) => new()
    {
        Id = model.Id,
        Code = model.Code,
        Name = model.Name,
        Description = model.Description,
        ReferenceType = model.ReferenceType,
        Category = model.Category,
        GroupKey = model.GroupKey,
        SourceSystem = model.SourceSystem,
        Owner = model.Owner,
        Version = model.Version,
        EffectiveDate = model.EffectiveDate,
        ExpirationDate = model.ExpirationDate,
        Priority = model.Priority,
        IsActive = model.IsActive
    };

    private static GoldenReferenceCompactSavePayload ToPayload(GoldenReferenceCompactEditViewModel model) => new()
    {
        Code = model.Code,
        Name = model.Name,
        Description = model.Description,
        ReferenceType = model.ReferenceType,
        Category = model.Category,
        GroupKey = model.GroupKey,
        SourceSystem = model.SourceSystem,
        Owner = model.Owner,
        Version = model.Version,
        EffectiveDate = model.EffectiveDate,
        ExpirationDate = model.ExpirationDate,
        Priority = model.Priority,
        IsActive = model.IsActive
    };

    private object EmptyLookups() => new
    {
        referenceTypes = Array.Empty<object>(),
        categories = Array.Empty<object>(),
        owners = Array.Empty<object>(),
        priorities = Array.Empty<object>()
    };

    private static List<object> ToLookup(IEnumerable<string?> values) =>
        values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Select(x => new { value = x!, text = x! })
            .Cast<object>()
            .ToList();

    private void AddGatewayErrorsToModelState(IEnumerable<string> errors)
    {
        foreach (var error in errors)
            ModelState.AddModelError(string.Empty, error);
    }

    private List<string> BuildExceptionErrors(Exception ex)
    {
        var message = ex.GetBaseException().Message;
        if (string.IsNullOrWhiteSpace(message))
            message = _sharedLocalizer["GatewayError"].Value;

        return [message];
    }

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

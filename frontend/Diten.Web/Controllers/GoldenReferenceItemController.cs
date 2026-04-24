using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models;
using Diten.Web.Models.GoldenReferenceItem;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

[Authorize]
[Route("GoldenReferenceItem")]
public sealed class GoldenReferenceItemController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public GoldenReferenceItemController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
    }

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/DevEnablement/GoldenReferenceItem/Index.cshtml");

    [HttpGet("create")]
    public IActionResult Create() => View("~/Views/DevEnablement/GoldenReferenceItem/Create.cshtml", new GoldenReferenceItemEditViewModel());

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(GoldenReferenceItemEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("~/Views/DevEnablement/GoldenReferenceItem/Create.cshtml", model);
        }

        if (!AddAuthHeaders())
        {
            return Unauthorized();
        }

        var response = await _httpClient.PostAsJsonAsync(
            $"{_gatewayUrl}/api/golden-reference-item",
            ToPayload(model),
            _jsonOptions);

        if (response.IsSuccessStatusCode)
        {
            TempData["SuccessMessage"] = "RecordCreated";
            return RedirectToAction(nameof(Index));
        }

        await AddGatewayErrorAsync(response);
        return View("~/Views/DevEnablement/GoldenReferenceItem/Create.cshtml", model);
    }

    [HttpGet("edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        if (string.IsNullOrWhiteSpace(GetTenantId()))
        {
            return Unauthorized();
        }

        var detail = await LoadApiModelAsync(id);
        if (detail is null)
        {
            TempData["ErrorMessage"] = _sharedLocalizer["GatewayError"].Value;
            return RedirectToAction(nameof(Index));
        }

        var model = new GoldenReferenceItemEditViewModel
        {
            Id = detail.Id,
            Code = detail.Code,
            Name = detail.Name,
            Description = detail.Description,
            ReferenceType = detail.ReferenceType,
            Priority = detail.Priority,
            IsActive = detail.IsActive
        };

        return View("~/Views/DevEnablement/GoldenReferenceItem/Edit.cshtml", model);
    }

    [HttpPost("edit/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, GoldenReferenceItemEditViewModel model)
    {
        model.Id = id;

        if (!ModelState.IsValid)
        {
            return View("~/Views/DevEnablement/GoldenReferenceItem/Edit.cshtml", model);
        }

        if (!AddAuthHeaders())
        {
            return Unauthorized();
        }

        var response = await _httpClient.PutAsJsonAsync(
            $"{_gatewayUrl}/api/golden-reference-item/{id}",
            ToPayload(model),
            _jsonOptions);

        if (response.IsSuccessStatusCode)
        {
            TempData["SuccessMessage"] = "RecordUpdated";
            return RedirectToAction(nameof(Index));
        }

        await AddGatewayErrorAsync(response);
        return View("~/Views/DevEnablement/GoldenReferenceItem/Edit.cshtml", model);
    }

    [HttpGet("details/{id:guid}")]
    public async Task<IActionResult> Details(Guid id)
    {
        if (string.IsNullOrWhiteSpace(GetTenantId()))
        {
            return Unauthorized();
        }

        var model = await LoadApiModelAsync(id);
        if (model is null)
        {
            TempData["ErrorMessage"] = _sharedLocalizer["GatewayError"].Value;
            return RedirectToAction(nameof(Index));
        }

        return View("~/Views/DevEnablement/GoldenReferenceItem/Details.cshtml", model);
    }

    [HttpGet("lookups")]
    public async Task<IActionResult> Lookups()
    {
        if (!AddAuthHeaders())
        {
            return Unauthorized();
        }

        var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/golden-reference-item");
        if (!response.IsSuccessStatusCode)
        {
            return Json(new { referenceTypes = Array.Empty<object>(), priorities = Array.Empty<object>() });
        }

        var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<List<GoldenReferenceItemDetailViewModel>>>(_jsonOptions);
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

    private async Task<GoldenReferenceItemDetailViewModel?> LoadApiModelAsync(Guid id)
    {
        if (!AddAuthHeaders())
        {
            return null;
        }

        var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/golden-reference-item/{id}");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<GoldenReferenceItemDetailViewModel>>(_jsonOptions);
        return payload?.Data;
    }

    private static GoldenReferenceItemSavePayload ToPayload(GoldenReferenceItemEditViewModel model)
    {
        return new GoldenReferenceItemSavePayload
        {
            Code = model.Code,
            Name = model.Name,
            Description = model.Description,
            ReferenceType = model.ReferenceType,
            Priority = model.Priority,
            IsActive = model.IsActive
        };
    }

    private async Task AddGatewayErrorAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            Response.Redirect("/Account/Login");
            return;
        }

        try
        {
            var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<object>>(_jsonOptions);
            var error = payload?.Errors?.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(error))
            {
                ModelState.AddModelError(string.Empty, error);
                return;
            }
        }
        catch
        {
            // fallback below
        }

        var raw = await response.Content.ReadAsStringAsync();
        ModelState.AddModelError(string.Empty, string.IsNullOrWhiteSpace(raw) ? _sharedLocalizer["GatewayError"].Value : raw);
    }

    private bool AddAuthHeaders()
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

        var tenantId = GetTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return false;
        }

        _httpClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        return true;
    }

    private string? GetTenantId()
    {
        return User.Claims.FirstOrDefault(x =>
            x.Type == "tenantId" ||
            x.Type == "tenant_id" ||
            x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;
    }
}

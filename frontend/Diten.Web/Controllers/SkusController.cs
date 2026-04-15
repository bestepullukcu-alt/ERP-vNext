using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models;
using Diten.Web.Models.Skus;
using Diten.Web.Views.MDM.Skus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

[Authorize]
public sealed class SkusController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly IStringLocalizer _skuLocalizer;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public SkusController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        IStringLocalizerFactory localizerFactory)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
        _sharedLocalizer = sharedLocalizer;
        _skuLocalizer = localizerFactory.Create(typeof(SkusIndex));
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var model = await LoadApiModelAsync(id);
        if (model is null)
        {
            TempData["ErrorMessage"] = _sharedLocalizer["GatewayError"].Value;
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }


    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return View(await BuildIndexPageModelAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new SkuEditViewModel();
        await PopulateLookupsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SkuEditViewModel model)
    {
        await PopulateLookupsAsync(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        AddAuthHeaders();
        var response = await _httpClient.PostAsJsonAsync($"{_gatewayUrl}/api/skus", ToPayload(model), _jsonOptions);
        if (response.IsSuccessStatusCode)
        {
            TempData["SuccessMessage"] = "RecordCreated";
            return RedirectToAction(nameof(Index));
        }

        await AddGatewayErrorAsync(response);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var model = await LoadEditModelAsync(id);
        if (model is null)
        {
            TempData["ErrorMessage"] = _sharedLocalizer["GatewayError"].Value;
            return RedirectToAction(nameof(Index));
        }

        await PopulateLookupsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, SkuEditViewModel model)
    {
        model.Id = id;
        await PopulateLookupsAsync(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        AddAuthHeaders();
        var response = await _httpClient.PutAsJsonAsync($"{_gatewayUrl}/api/skus/{id}", ToPayload(model), _jsonOptions);
        if (response.IsSuccessStatusCode)
        {
            TempData["SuccessMessage"] = "RecordUpdated";
            return RedirectToAction(nameof(Index));
        }

        await AddGatewayErrorAsync(response);
        return View(model);
    }

    private async Task<SkuIndexPageViewModel> BuildIndexPageModelAsync()
    {
        return new SkuIndexPageViewModel
        {
            Products = await GetLookupOptionsAsync("products"),
            Compositions = await GetLookupOptionsAsync("compositions"),
            LifecycleStates = await GetLookupOptionsAsync("lifecycle-states")
        };
    }

    private async Task PopulateLookupsAsync(SkuEditViewModel model)
    {
        model.Products = await GetLookupOptionsAsync("products");
        model.Compositions = await GetLookupOptionsAsync("compositions");
        model.LifecycleStates = await GetLookupOptionsAsync("lifecycle-states");

        if (!model.Id.HasValue && model.LifecycleStateId == Guid.Empty)
        {
            model.LifecycleStateId = model.LifecycleStates.FirstOrDefault(x => x.Code == "DRAFT")?.Id ?? Guid.Empty;
        }
    }

    private async Task<SkuApiDetailViewModel?> LoadApiModelAsync(Guid id)
    {
        AddAuthHeaders();
        var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/skus/{id}");
        if (!response.IsSuccessStatusCode) return null;

        return await response.Content.ReadFromJsonAsync<SkuApiDetailViewModel>(_jsonOptions);
    }

    private async Task<SkuEditViewModel?> LoadEditModelAsync(Guid id)
    {
        var detail = await LoadApiModelAsync(id);
        if (detail is null) return null;

        return new SkuEditViewModel
        {
            Id = detail.Id,
            Code = detail.Code,
            ProductId = detail.ProductId,
            CompositionId = detail.CompositionId,
            CompositionVersion = detail.CompositionVersion,
            CompositionRevision = detail.CompositionRevision,
            PackagingForm = detail.PackagingForm,
            PackagingQuantity = detail.PackagingQuantity,
            Barcode = detail.Barcode,
            LifecycleStateId = detail.LifecycleStateId
        };
    }

    private async Task<List<LookupApiViewModel>> GetLookupOptionsAsync(string resource)
    {
        AddAuthHeaders();
        var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/{resource}");
        if (!response.IsSuccessStatusCode) return [];

        var payload = await response.Content.ReadFromJsonAsync<ApiListResponse<LookupApiViewModel>>(_jsonOptions);
        return payload?.Data ?? [];
    }

    private static SkuSavePayload ToPayload(SkuEditViewModel model)
    {
        return new SkuSavePayload
        {
            Code = model.Code,
            ProductId = model.ProductId,
            CompositionId = model.CompositionId,
            CompositionVersion = model.CompositionVersion,
            CompositionRevision = model.CompositionRevision,
            PackagingForm = model.PackagingForm,
            PackagingQuantity = model.PackagingQuantity,
            Barcode = model.Barcode,
            LifecycleStateId = model.LifecycleStateId
        };
    }

    private async Task AddGatewayErrorAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            Response.Redirect("/Account/Login");
            return;
        }

        ModelState.AddModelError(string.Empty, await ReadErrorAsync(response));
    }

    private async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        var error = await response.Content.ReadAsStringAsync();
        return string.IsNullOrWhiteSpace(error) ? _sharedLocalizer["GatewayError"].Value : error;
    }

    private void AddAuthHeaders()
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
        _httpClient.DefaultRequestHeaders.Add("X-Tenant-Id", GetTenantId());
    }

    private string GetTenantId()
    {
        return User.Claims.FirstOrDefault(x => x.Type == "tenantId")?.Value ?? "00000000-0000-0000-0000-000000000001";
    }
}

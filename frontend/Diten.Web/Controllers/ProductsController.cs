using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models;
using Diten.Web.Models.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

[Authorize]
public sealed class ProductsController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public ProductsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
        _sharedLocalizer = sharedLocalizer;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return View(await BuildIndexPageModelAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new ProductEditViewModel();
        await PopulateLookupsAsync(model);

        if (model.LifecycleStateId == Guid.Empty)
        {
            model.LifecycleStateId = model.LifecycleStates.FirstOrDefault(x => x.Code == "DRAFT")?.Id ?? Guid.Empty;
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductEditViewModel model)
    {
        await PopulateLookupsAsync(model);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        AddAuthHeaders();
        var response = await _httpClient.PostAsJsonAsync($"{_gatewayUrl}/api/products", ToPayload(model), _jsonOptions);
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
        var detail = await LoadApiModelAsync(id);
        if (detail is null)
        {
            TempData["ErrorMessage"] = _sharedLocalizer["GatewayError"].Value;
            return RedirectToAction(nameof(Index));
        }

        var model = new ProductEditViewModel
        {
            Id = detail.Id,
            Code = detail.Code,
            Name = detail.Name,
            ShortName = detail.ShortName,
            Description = detail.Description,
            ProductType = detail.ProductType,
            CategoryId = detail.CategoryId,
            LifecycleStateId = detail.LifecycleStateId,
            IsSaleable = detail.IsSaleable,
            IsPurchasable = detail.IsPurchasable,
            IsManufacturable = detail.IsManufacturable,
            CategoryName = detail.CategoryName,
            LifecycleStateName = detail.LifecycleStateName
        };

        await PopulateLookupsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, ProductEditViewModel model)
    {
        model.Id = id;
        await PopulateLookupsAsync(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        AddAuthHeaders();
        var response = await _httpClient.PutAsJsonAsync($"{_gatewayUrl}/api/products/{id}", ToPayload(model), _jsonOptions);
        if (response.IsSuccessStatusCode)
        {
            TempData["SuccessMessage"] = "RecordUpdated";
            return RedirectToAction(nameof(Index));
        }

        await AddGatewayErrorAsync(response);
        return View(model);
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

    private async Task<ProductIndexPageViewModel> BuildIndexPageModelAsync()
    {
        return new ProductIndexPageViewModel
        {
            Categories = await GetLookupOptionsAsync("item-categories"),
            LifecycleStates = await GetLookupOptionsAsync("lifecycle-states")
        };
    }

    private async Task<ProductDetailViewModel?> LoadApiModelAsync(Guid id)
    {
        AddAuthHeaders();
        var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/products/{id}");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ProductDetailViewModel>(_jsonOptions);
    }

    private async Task PopulateLookupsAsync(ProductEditViewModel model)
    {
        model.Categories = await GetLookupOptionsAsync("item-categories");
        model.LifecycleStates = await GetLookupOptionsAsync("lifecycle-states");
    }

    private async Task<List<LookupApiViewModel>> GetLookupOptionsAsync(string resource)
    {
        AddAuthHeaders();
        var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/{resource}");
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var payload = await response.Content.ReadFromJsonAsync<ApiListResponse<LookupApiViewModel>>(_jsonOptions);
        return payload?.Data ?? [];
    }

    private static ProductSavePayload ToPayload(ProductEditViewModel model)
    {
        return new ProductSavePayload
        {
            Code = model.Code,
            Name = model.Name,
            ShortName = model.ShortName,
            Description = model.Description,
            ProductType = model.ProductType,
            CategoryId = model.CategoryId,
            LifecycleStateId = model.LifecycleStateId,
            IsSaleable = model.IsSaleable,
            IsPurchasable = model.IsPurchasable,
            IsManufacturable = model.IsManufacturable
        };
    }

    private async Task AddGatewayErrorAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            Response.Redirect("/Account/Login");
            return;
        }

        var error = await response.Content.ReadAsStringAsync();
        ModelState.AddModelError(string.Empty, string.IsNullOrWhiteSpace(error) ? _sharedLocalizer["GatewayError"].Value : error);
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

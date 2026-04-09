using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models.Products;
using Diten.Web.Views.MDM.Products;
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
    private readonly IStringLocalizer _productLocalizer;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly ProductTypeOptionViewModel[] ProductTypeCatalog =
    [
        new() { Value = 1, Code = "FINISHED_GOOD" },
        new() { Value = 2, Code = "SERVICE" },
        new() { Value = 3, Code = "DIGITAL" }
    ];

    private static readonly ProductCategoryOptionViewModel[] ProductCategoryCatalog =
    [
        new() { Id = Guid.Parse("60000000-0000-0000-0000-000000000001"), ProductType = 1, Code = "STANDARD" },
        new() { Id = Guid.Parse("60000000-0000-0000-0000-000000000002"), ProductType = 1, Code = "REGULATED" },
        new() { Id = Guid.Parse("60000000-0000-0000-0000-000000000003"), ProductType = 2, Code = "PROFESSIONAL" },
        new() { Id = Guid.Parse("60000000-0000-0000-0000-000000000004"), ProductType = 2, Code = "SUPPORT" },
        new() { Id = Guid.Parse("60000000-0000-0000-0000-000000000005"), ProductType = 3, Code = "LICENSE" },
        new() { Id = Guid.Parse("60000000-0000-0000-0000-000000000006"), ProductType = 3, Code = "SUBSCRIPTION" }
    ];

    public ProductsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        IStringLocalizerFactory localizerFactory)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
        _sharedLocalizer = sharedLocalizer;
        _productLocalizer = localizerFactory.Create(typeof(ProductsIndex));
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
            return RedirectToAction(nameof(Details), new { id });
        }

        await AddGatewayErrorAsync(response);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var model = await LoadDetailsModelAsync(id);
        if (model is null)
        {
            TempData["ErrorMessage"] = _sharedLocalizer["GatewayError"].Value;
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeLifecycle(Guid id, Guid lifecycleStateId)
    {
        AddAuthHeaders();
        using var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"{_gatewayUrl}/api/products/{id}/lifecycle")
        {
            Content = JsonContent.Create(new ProductLifecycleSavePayload { LifecycleStateId = lifecycleStateId }, options: _jsonOptions)
        };

        var response = await _httpClient.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            TempData["SuccessMessage"] = "RecordUpdated";
        }
        else
        {
            TempData["ErrorMessage"] = await ReadErrorAsync(response);
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<ProductIndexPageViewModel> BuildIndexPageModelAsync()
    {
        return new ProductIndexPageViewModel
        {
            ProductTypes = BuildProductTypeOptions(),
            Categories = BuildCategoryOptions(),
            LifecycleStates = await GetLifecycleOptionsAsync()
        };
    }

    private async Task PopulateLookupsAsync(ProductEditViewModel model)
    {
        model.ProductTypes = BuildProductTypeOptions();
        model.Categories = BuildCategoryOptions();
        model.LifecycleStates = await GetLifecycleOptionsAsync();

        if (!model.Id.HasValue && model.LifecycleStateId == Guid.Empty)
        {
            model.LifecycleStateId = model.LifecycleStates.FirstOrDefault(x => x.Code == "DRAFT")?.Id ?? Guid.Empty;
        }
    }

    private async Task<ProductEditViewModel?> LoadEditModelAsync(Guid id)
    {
        var detail = await LoadProductApiModelAsync(id);
        if (detail is null)
        {
            return null;
        }

        return new ProductEditViewModel
        {
            Id = detail.Id,
            Code = detail.Code,
            Name = detail.Name,
            ShortName = detail.ShortName,
            Description = detail.Description,
            ProductType = ProductTypeCatalog.FirstOrDefault(x => x.Code == detail.ProductTypeCode)?.Value ?? 0,
            CategoryId = detail.CategoryId,
            LifecycleStateId = detail.LifecycleStateId,
            IsSaleable = detail.IsSaleable,
            IsPurchasable = detail.IsPurchasable,
            IsManufacturable = detail.IsManufacturable
        };
    }

    private async Task<ProductDetailViewModel?> LoadDetailsModelAsync(Guid id)
    {
        var detail = await LoadProductApiModelAsync(id);
        if (detail is null)
        {
            return null;
        }

        var lifecycleOptions = await GetLifecycleOptionsAsync();
        var currentLifecycle = lifecycleOptions.FirstOrDefault(x => x.Id == detail.LifecycleStateId)
            ?? new ProductLifecycleOptionViewModel
            {
                Id = detail.LifecycleStateId,
                Code = detail.LifecycleStateCode,
                Name = LocalizeLifecycle(detail.LifecycleStateCode, detail.LifecycleState)
            };

        return new ProductDetailViewModel
        {
            Id = detail.Id,
            Code = detail.Code,
            Name = detail.Name,
            ShortName = detail.ShortName,
            Description = detail.Description,
            ProductType = ProductTypeCatalog.FirstOrDefault(x => x.Code == detail.ProductTypeCode)?.Value ?? 0,
            ProductTypeCode = detail.ProductTypeCode,
            ProductTypeName = LocalizeProductType(detail.ProductTypeCode, detail.ProductType),
            CategoryId = detail.CategoryId,
            CategoryCode = detail.CategoryCode,
            CategoryName = LocalizeCategory(detail.CategoryCode, detail.Category),
            LifecycleStateId = currentLifecycle.Id,
            LifecycleStateCode = currentLifecycle.Code,
            LifecycleStateName = currentLifecycle.Name,
            LifecycleBadgeClass = GetLifecycleBadgeClass(currentLifecycle.Code),
            IsSaleable = detail.IsSaleable,
            IsPurchasable = detail.IsPurchasable,
            IsManufacturable = detail.IsManufacturable,
            AvailableTransitions = BuildLifecycleTransitions(currentLifecycle.Code, lifecycleOptions)
        };
    }

    private async Task<ProductApiDetailViewModel?> LoadProductApiModelAsync(Guid id)
    {
        AddAuthHeaders();
        var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/products/{id}");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ProductApiDetailViewModel>(_jsonOptions);
    }

    private async Task<List<ProductLifecycleOptionViewModel>> GetLifecycleOptionsAsync()
    {
        AddAuthHeaders();
        var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/lifecycle-states");
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var payload = await response.Content.ReadFromJsonAsync<ApiListResponse<LookupApiViewModel>>(_jsonOptions);
        return payload?.Data
            .Select(x => new ProductLifecycleOptionViewModel
            {
                Id = x.Id,
                Code = x.Code,
                Name = LocalizeLifecycle(x.Code, x.Name)
            })
            .ToList() ?? [];
    }

    private List<ProductTypeOptionViewModel> BuildProductTypeOptions()
    {
        return ProductTypeCatalog
            .Select(x => new ProductTypeOptionViewModel
            {
                Value = x.Value,
                Code = x.Code,
                Name = LocalizeProductType(x.Code, x.Code)
            })
            .ToList();
    }

    private List<ProductCategoryOptionViewModel> BuildCategoryOptions()
    {
        return ProductCategoryCatalog
            .Select(x => new ProductCategoryOptionViewModel
            {
                Id = x.Id,
                ProductType = x.ProductType,
                Code = x.Code,
                Name = LocalizeCategory(x.Code, x.Code)
            })
            .ToList();
    }

    private List<ProductLifecycleTransitionViewModel> BuildLifecycleTransitions(
        string currentLifecycleCode,
        IReadOnlyList<ProductLifecycleOptionViewModel> allStates)
    {
        var allowedCodes = (currentLifecycleCode ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "DRAFT" => new[] { "ACTIVE" },
            "ACTIVE" => new[] { "BLOCKED", "OBSOLETE" },
            "BLOCKED" => new[] { "ACTIVE", "OBSOLETE" },
            _ => Array.Empty<string>()
        };

        return allStates
            .Where(x => allowedCodes.Contains(x.Code))
            .Select(x => new ProductLifecycleTransitionViewModel
            {
                LifecycleStateId = x.Id,
                Code = x.Code,
                Name = GetLifecycleActionName(x.Code, x.Name),
                ButtonClass = x.Code switch
                {
                    "ACTIVE" => "btn-success",
                    "BLOCKED" => "btn-warning",
                    "OBSOLETE" => "btn-dark",
                    _ => "btn-label-secondary"
                }
            })
            .ToList();
    }

    private string GetLifecycleActionName(string code, string fallback)
    {
        return (code ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "ACTIVE" => _productLocalizer["Activate"].Value,
            "BLOCKED" => _productLocalizer["Block"].Value,
            "OBSOLETE" => _productLocalizer["MarkAsObsolete"].Value,
            _ => fallback
        };
    }

    private string LocalizeProductType(string code, string fallback)
    {
        return (code ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "FINISHED_GOOD" => _productLocalizer["ProductTypeFinishedGood"].Value,
            "SERVICE" => _productLocalizer["ProductTypeService"].Value,
            "DIGITAL" => _productLocalizer["ProductTypeDigital"].Value,
            _ => fallback
        };
    }

    private string LocalizeCategory(string code, string fallback)
    {
        return (code ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "STANDARD" => _productLocalizer["CategoryStandard"].Value,
            "REGULATED" => _productLocalizer["CategoryRegulated"].Value,
            "PROFESSIONAL" => _productLocalizer["CategoryProfessional"].Value,
            "SUPPORT" => _productLocalizer["CategorySupport"].Value,
            "LICENSE" => _productLocalizer["CategoryLicense"].Value,
            "SUBSCRIPTION" => _productLocalizer["CategorySubscription"].Value,
            _ => fallback
        };
    }

    private string LocalizeLifecycle(string code, string fallback)
    {
        return (code ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "DRAFT" => _productLocalizer["LifecycleDraft"].Value,
            "ACTIVE" => _productLocalizer["LifecycleActive"].Value,
            "BLOCKED" => _productLocalizer["LifecycleBlocked"].Value,
            "OBSOLETE" => _productLocalizer["LifecycleObsolete"].Value,
            _ => fallback
        };
    }

    private static string GetLifecycleBadgeClass(string code)
    {
        return (code ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "ACTIVE" => "bg-label-success",
            "BLOCKED" => "bg-label-warning",
            "OBSOLETE" => "bg-label-dark",
            _ => "bg-label-secondary"
        };
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
        var claim = User.Claims.FirstOrDefault(x => x.Type == "tenantId")?.Value;
        if (!string.IsNullOrWhiteSpace(claim))
        {
            return claim;
        }

        return "00000000-0000-0000-0000-000000000001";
    }
}

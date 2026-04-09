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
    private const string RestorePermission = "Modules.Products.Restore";
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
        new() { Value = 1, Code = "FINISHED_PRODUCT" },
        new() { Value = 2, Code = "SEMI_FINISHED_PRODUCT" },
        new() { Value = 3, Code = "SERVICE" },
        new() { Value = 4, Code = "TECHNOLOGY" }
    ];

    private static readonly ProductCategoryOptionViewModel[] ProductCategoryCatalog =
    [
        new() { Id = Guid.Parse("60000000-0000-0000-0000-000000000001"), ProductType = 1, Code = "CNS" },
        new() { Id = Guid.Parse("60000000-0000-0000-0000-000000000002"), ProductType = 1, Code = "PEDIATRIC" },
        new() { Id = Guid.Parse("60000000-0000-0000-0000-000000000003"), ProductType = 1, Code = "UROLOGY" },
        new() { Id = Guid.Parse("60000000-0000-0000-0000-000000000004"), ProductType = 1, Code = "ANTI_INFECTIVE" },
        new() { Id = Guid.Parse("60000000-0000-0000-0000-000000000005"), ProductType = 1, Code = "SUPPLEMENT" },
        new() { Id = Guid.Parse("60000000-0000-0000-0000-000000000006"), ProductType = 1, Code = "DISINFECTANT" },
        new() { Id = Guid.Parse("60000000-0000-0000-0000-000000000007"), ProductType = 2, Code = "CNS" },
        new() { Id = Guid.Parse("60000000-0000-0000-0000-000000000008"), ProductType = 2, Code = "PEDIATRIC" },
        new() { Id = Guid.Parse("60000000-0000-0000-0000-000000000009"), ProductType = 2, Code = "UROLOGY" },
        new() { Id = Guid.Parse("60000000-0000-0000-0000-00000000000A"), ProductType = 2, Code = "ANTI_INFECTIVE" },
        new() { Id = Guid.Parse("60000000-0000-0000-0000-00000000000B"), ProductType = 2, Code = "SUPPLEMENT" },
        new() { Id = Guid.Parse("60000000-0000-0000-0000-00000000000C"), ProductType = 2, Code = "DISINFECTANT" },
        new() { Id = Guid.Parse("60000000-0000-0000-0000-00000000000D"), ProductType = 3, Code = "MANUFACTURING_SERVICE" },
        new() { Id = Guid.Parse("60000000-0000-0000-0000-00000000000E"), ProductType = 3, Code = "CONSULTING" },
        new() { Id = Guid.Parse("60000000-0000-0000-0000-00000000000F"), ProductType = 3, Code = "SUPPORT" },
        new() { Id = Guid.Parse("60000000-0000-0000-0000-000000000010"), ProductType = 4, Code = "DIGITAL_SOLUTION" },
        new() { Id = Guid.Parse("60000000-0000-0000-0000-000000000011"), ProductType = 4, Code = "PLATFORM" },
        new() { Id = Guid.Parse("60000000-0000-0000-0000-000000000012"), ProductType = 4, Code = "INTEGRATION" }
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
            return RedirectToAction(nameof(Index));
        }

        await AddGatewayErrorAsync(response);
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

        return RedirectToAction(nameof(Index));
    }

    private async Task<ProductIndexPageViewModel> BuildIndexPageModelAsync()
    {
        return new ProductIndexPageViewModel
        {
            ProductTypes = BuildProductTypeOptions(),
            Categories = BuildCategoryFilterOptions(),
            LifecycleStates = await GetLifecycleOptionsAsync(),
            CanRestore = HasPermission(RestorePermission)
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

    private List<ProductCategoryOptionViewModel> BuildCategoryFilterOptions()
    {
        return BuildCategoryOptions()
            .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();
    }

    private string LocalizeProductType(string code, string fallback)
    {
        return (code ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "FINISHED_PRODUCT" => _productLocalizer["ProductTypeFinishedProduct"].Value,
            "SEMI_FINISHED_PRODUCT" => _productLocalizer["ProductTypeSemiFinishedProduct"].Value,
            "SERVICE" => _productLocalizer["ProductTypeService"].Value,
            "TECHNOLOGY" => _productLocalizer["ProductTypeTechnology"].Value,
            _ => fallback
        };
    }

    private string LocalizeCategory(string code, string fallback)
    {
        return (code ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "CNS" => _productLocalizer["CategoryCns"].Value,
            "PEDIATRIC" => _productLocalizer["CategoryPediatric"].Value,
            "UROLOGY" => _productLocalizer["CategoryUrology"].Value,
            "ANTI_INFECTIVE" => _productLocalizer["CategoryAntiInfective"].Value,
            "SUPPLEMENT" => _productLocalizer["CategorySupplement"].Value,
            "DISINFECTANT" => _productLocalizer["CategoryDisinfectant"].Value,
            "MANUFACTURING_SERVICE" => _productLocalizer["CategoryManufacturingService"].Value,
            "CONSULTING" => _productLocalizer["CategoryConsulting"].Value,
            "SUPPORT" => _productLocalizer["CategorySupport"].Value,
            "DIGITAL_SOLUTION" => _productLocalizer["CategoryDigitalSolution"].Value,
            "PLATFORM" => _productLocalizer["CategoryPlatform"].Value,
            "INTEGRATION" => _productLocalizer["CategoryIntegration"].Value,
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

    private bool HasPermission(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
        {
            return false;
        }

        foreach (var claim in User.Claims)
        {
            var claimValue = claim.Value?.Trim();
            if (string.IsNullOrWhiteSpace(claimValue))
            {
                continue;
            }

            if (string.Equals(claimValue, permission, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            foreach (var token in claimValue.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (string.Equals(token, permission, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (claimValue.StartsWith("[", StringComparison.Ordinal)
                && claimValue.IndexOf(permission, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}

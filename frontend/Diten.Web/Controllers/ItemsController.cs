using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models.Items;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

[Authorize]
public sealed class ItemsController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public ItemsController(HttpClient httpClient, IConfiguration configuration, IStringLocalizer<SharedResource> localizer)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
        _localizer = localizer;
    }

    [HttpGet]
    public IActionResult Index() => View(new List<ItemIndexViewModel>());

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new ItemEditViewModel();
        await PopulateLookupsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ItemEditViewModel model)
    {
        await PopulateLookupsAsync(model);
        if (!TryParseJsonPayloads(model, out var attributeValues, out var variants))
        {
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        AddAuthHeaders();
        var payload = ToPayload(model, attributeValues, variants);
        var response = await _httpClient.PostAsJsonAsync($"{_gatewayUrl}/api/items", payload, _jsonOptions);
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
        var model = await LoadItemAsync(id);
        if (model is null)
        {
            TempData["ErrorMessage"] = _localizer["GatewayError"].Value;
            return RedirectToAction(nameof(Index));
        }

        await PopulateLookupsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, ItemEditViewModel model)
    {
        model.Id = id;
        await PopulateLookupsAsync(model);
        if (!TryParseJsonPayloads(model, out var attributeValues, out var variants))
        {
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        AddAuthHeaders();
        var payload = ToPayload(model, attributeValues, variants);
        var response = await _httpClient.PutAsJsonAsync($"{_gatewayUrl}/api/items/{id}", payload, _jsonOptions);
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
        var model = await LoadItemAsync(id);
        if (model is null)
        {
            TempData["ErrorMessage"] = _localizer["GatewayError"].Value;
            return RedirectToAction(nameof(Index));
        }

        await PopulateLookupsAsync(model);
        return View(model);
    }

    private async Task<ItemEditViewModel?> LoadItemAsync(Guid id)
    {
        AddAuthHeaders();
        var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/items/{id}");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var item = await response.Content.ReadFromJsonAsync<ItemEditViewModel>(_jsonOptions);
        if (item is null)
        {
            return null;
        }

        item.AttributeValuesJson = JsonSerializer.Serialize(
            item.AttributeValues.Select(x => new ItemAttributePayload { AttributeDefinitionId = x.AttributeDefinitionId, Value = x.Value }),
            _jsonOptions);
        item.VariantsJson = JsonSerializer.Serialize(
            item.Variants.Select(x => new ItemVariantPayload
            {
                Code = x.Code,
                Name = x.Name,
                IsActive = x.IsActive,
                AttributeValues = x.AttributeValues.Select(v => new ItemAttributePayload
                {
                    AttributeDefinitionId = v.AttributeDefinitionId,
                    Value = v.Value
                }).ToList()
            }),
            _jsonOptions);

        return item;
    }

    private async Task PopulateLookupsAsync(ItemEditViewModel model)
    {
        model.ItemTypes = await GetLookupAsync<LookupOptionViewModel>("/api/item-types");
        model.Categories = await GetLookupAsync<ItemCategoryOptionViewModel>("/api/item-categories");
        model.BaseUoms = await GetLookupAsync<LookupOptionViewModel>("/api/unit-of-measures");
        model.TrackingPolicies = await GetLookupAsync<LookupOptionViewModel>("/api/tracking-policies");
        model.LifecycleStates = await GetLookupAsync<LookupOptionViewModel>("/api/lifecycle-states");
        model.VariantModels = await GetLookupAsync<ItemVariantModelOptionViewModel>("/api/item-variant-models");

        if (model.VariantModelId.HasValue && model.VariantTemplates.Count == 0)
        {
            AddAuthHeaders();
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/item-variant-models/{model.VariantModelId.Value}");
            if (response.IsSuccessStatusCode)
            {
                var variantModel = await response.Content.ReadFromJsonAsync<ItemVariantModelAdminViewModel>(_jsonOptions);
                if (variantModel is not null)
                {
                    model.VariantTemplates = variantModel.Attributes;
                }
            }
        }
    }

    private async Task<List<T>> GetLookupAsync<T>(string path)
    {
        AddAuthHeaders();
        var response = await _httpClient.GetAsync($"{_gatewayUrl}{path}");
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var payload = await response.Content.ReadFromJsonAsync<ApiListResponse<T>>(_jsonOptions);
        return payload?.Data ?? [];
    }

    private bool TryParseJsonPayloads(ItemEditViewModel model, out List<ItemAttributePayload> attributeValues, out List<ItemVariantPayload> variants)
    {
        attributeValues = [];
        variants = [];

        try
        {
            attributeValues = JsonSerializer.Deserialize<List<ItemAttributePayload>>(model.AttributeValuesJson ?? "[]", _jsonOptions) ?? [];
            variants = JsonSerializer.Deserialize<List<ItemVariantPayload>>(model.VariantsJson ?? "[]", _jsonOptions) ?? [];
            return true;
        }
        catch (JsonException)
        {
            ModelState.AddModelError(string.Empty, _localizer["ValidationFailed"].Value);
            return false;
        }
    }

    private ItemSavePayload ToPayload(
        ItemEditViewModel model,
        List<ItemAttributePayload> attributeValues,
        List<ItemVariantPayload> variants)
    {
        return new ItemSavePayload
        {
            Code = model.Code,
            Name = model.Name,
            ShortDescription = model.ShortDescription,
            ItemTypeId = model.ItemTypeId,
            CategoryId = model.CategoryId,
            BaseUomId = model.BaseUomId,
            Stockable = model.Stockable,
            Purchasable = model.Purchasable,
            Sellable = model.Sellable,
            ServiceItem = model.ServiceItem,
            TrackingPolicyId = model.TrackingPolicyId,
            LifecycleStateId = model.LifecycleStateId,
            IsActive = model.IsActive,
            VariantModelId = model.VariantModelId,
            AttributeValues = attributeValues,
            Variants = variants
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
        ModelState.AddModelError(string.Empty, string.IsNullOrWhiteSpace(error) ? _localizer["GatewayError"].Value : error);
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

        var tenantId = User.FindFirst("tenant_id")?.Value ?? "00000000-0000-0000-0000-000000000001";
        _httpClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
    }
}

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers.Platform;

[Route("Platform/SubscriptionPlans")]
public sealed class SubscriptionPlansController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<SubscriptionPlansController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public SubscriptionPlansController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<SubscriptionPlansController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Platform/SubscriptionPlans/Index.cshtml");

    [HttpGet("Create")]
    public async Task<IActionResult> Create()
    {
        await LoadCurrencyOptionsAsync();
        return View("~/Views/Platform/SubscriptionPlans/Create.cshtml", new SubscriptionPlanEditViewModel());
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] SubscriptionPlanEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await LoadCurrencyOptionsAsync(model.Currency);
            return View("~/Views/Platform/SubscriptionPlans/Create.cshtml", model);
        }

        if (!TryValidateQuotasJson(model.DefaultQuotasJson))
        {
            await LoadCurrencyOptionsAsync(model.Currency);
            return View("~/Views/Platform/SubscriptionPlans/Create.cshtml", model);
        }

        AddAuthHeader();
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_gatewayUrl}/api/platform/subscription-plans", ToPayload(model), _jsonOptions);
            if (response.IsSuccessStatusCode)
            {
                var created = await response.Content.ReadFromJsonAsync<GatewayResponse<Guid>>(_jsonOptions);
                var planId = created?.Data ?? Guid.Empty;
                if (planId != Guid.Empty && model.IsActive)
                {
                    var entitlementResult = await SaveEntitlementsAsync(planId, model.EntitlementMappingsJson);
                    if (!entitlementResult.Succeeded)
                    {
                        TempData["ErrorMessage"] = string.Join(" ", entitlementResult.Errors);
                        return RedirectToAction(nameof(Edit), new { id = planId });
                    }
                }

                TempData["SuccessMessage"] = _sharedLocalizer["RecordCreated"].Value;
                return RedirectToAction(nameof(Index));
            }

            AddGatewayErrorsToModelState(await ExtractGatewayErrorsAsync(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SubscriptionPlans create failed.");
            ModelState.AddModelError(string.Empty, _sharedLocalizer["GatewayError"].Value);
        }

        await LoadCurrencyOptionsAsync(model.Currency);
        return View("~/Views/Platform/SubscriptionPlans/Create.cshtml", model);
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

        var model = ToEditModel(detail);
        await LoadCurrencyOptionsAsync(model.Currency);
        return View("~/Views/Platform/SubscriptionPlans/Edit.cshtml", model);
    }

    [HttpPost("Edit/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, [FromForm] SubscriptionPlanEditViewModel model)
    {
        model.Id = id;
        if (!ModelState.IsValid)
        {
            await LoadCurrencyOptionsAsync(model.Currency);
            return View("~/Views/Platform/SubscriptionPlans/Edit.cshtml", model);
        }

        if (!TryValidateQuotasJson(model.DefaultQuotasJson))
        {
            await LoadCurrencyOptionsAsync(model.Currency);
            return View("~/Views/Platform/SubscriptionPlans/Edit.cshtml", model);
        }

        AddAuthHeader();
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"{_gatewayUrl}/api/platform/subscription-plans/{id}", ToPayload(model), _jsonOptions);
            if (response.IsSuccessStatusCode)
            {
                var entitlementResult = model.IsActive
                    ? await SaveEntitlementsAsync(id, model.EntitlementMappingsJson)
                    : EntitlementSaveResult.Success();
                if (!entitlementResult.Succeeded)
                {
                    AddGatewayErrorsToModelState(entitlementResult.Errors);
                    await LoadCurrencyOptionsAsync(model.Currency);
                    return View("~/Views/Platform/SubscriptionPlans/Edit.cshtml", model);
                }

                TempData["SuccessMessage"] = _sharedLocalizer["RecordUpdated"].Value;
                return RedirectToAction(nameof(Index));
            }

            AddGatewayErrorsToModelState(await ExtractGatewayErrorsAsync(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SubscriptionPlans edit failed for {SubscriptionPlanId}.", id);
            ModelState.AddModelError(string.Empty, _sharedLocalizer["GatewayError"].Value);
        }

        await LoadCurrencyOptionsAsync(model.Currency);
        return View("~/Views/Platform/SubscriptionPlans/Edit.cshtml", model);
    }

    [HttpGet("api")]
    public Task<IActionResult> ListProxy()
    {
        var targetUrl = $"{_gatewayUrl}/api/platform/subscription-plans{Request.QueryString}";
        return ProxyGatewayAsync(HttpMethod.Get, targetUrl);
    }

    [HttpGet("api/summary")]
    public Task<IActionResult> SummaryProxy()
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/subscription-plans/summary");
    }

    [HttpGet("api/{id:guid}")]
    public Task<IActionResult> DetailProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/subscription-plans/{id}");
    }

    [HttpGet("api/{id:guid}/features")]
    public Task<IActionResult> PlanFeaturesProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/subscription-plans/{id}/features");
    }

    [HttpPut("api/{id:guid}/features")]
    public Task<IActionResult> UpdatePlanFeaturesProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Put, $"{_gatewayUrl}/api/platform/subscription-plans/{id}/features", readBody: true);
    }

    [HttpGet("api/features/catalog")]
    public Task<IActionResult> FeatureCatalogProxy()
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/subscription-features{Request.QueryString}");
    }

    [HttpGet("api/features/categories")]
    public Task<IActionResult> FeatureCategoriesProxy()
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/feature-categories{Request.QueryString}");
    }

    [HttpGet("api/module-catalog")]
    public Task<IActionResult> ModuleCatalogProxy()
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/module-catalog{Request.QueryString}");
    }

    [HttpPost("api/{id:guid}/activate")]
    public Task<IActionResult> ActivateProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/subscription-plans/{id}/activate");
    }

    [HttpPost("api/{id:guid}/deactivate")]
    public Task<IActionResult> DeactivateProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/subscription-plans/{id}/deactivate");
    }

    private async Task<SubscriptionPlanDetailApiModel?> LoadApiModelAsync(Guid id)
    {
        AddAuthHeader();
        var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/platform/subscription-plans/{id}");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<SubscriptionPlanDetailApiModel>>(_jsonOptions);
        return payload?.Data;
    }

    private async Task LoadCurrencyOptionsAsync(string? selectedCurrency = null)
    {
        var currencies = await LoadCurrencyLookupAsync();
        var normalizedSelected = selectedCurrency?.Trim().ToUpperInvariant();
        ViewData["CurrencyOptions"] = currencies
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem
            {
                Value = x.Code,
                Text = $"{x.Code} - {x.Name}",
                Selected = string.Equals(x.Code, normalizedSelected, StringComparison.OrdinalIgnoreCase)
            })
            .ToList();
    }

    private async Task<List<CurrencyLookupApiModel>> LoadCurrencyLookupAsync()
    {
        try
        {
            AddAuthHeader();
            var currencies = await _httpClient.GetFromJsonAsync<List<CurrencyLookupApiModel>>(
                $"{_gatewayUrl}/api/lookups/currencies",
                _jsonOptions);

            if (currencies is { Count: > 0 })
            {
                return currencies
                    .Where(x => !string.IsNullOrWhiteSpace(x.Code))
                    .Select(x => x with { Code = x.Code.Trim().ToUpperInvariant() })
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Currency lookups could not be loaded for SubscriptionPlans form.");
        }

        return
        [
            new("USD", "US Dollar"),
            new("EUR", "Euro"),
            new("TRY", "Turkish Lira"),
            new("GBP", "British Pound")
        ];
    }

    private async Task<IActionResult> ProxyGatewayAsync(HttpMethod method, string targetUrl, string? jsonBody = null, bool readBody = false)
    {
        AddAuthHeader();
        using var request = new HttpRequestMessage(method, targetUrl);
        if (readBody)
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            jsonBody = await reader.ReadToEndAsync();
        }

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

    private async Task<EntitlementSaveResult> SaveEntitlementsAsync(Guid planId, string? mappingsJson)
    {
        if (string.IsNullOrWhiteSpace(mappingsJson))
        {
            return EntitlementSaveResult.Success();
        }

        PlanFeatureMappingsPayload? payload;
        try
        {
            var mappings = JsonSerializer.Deserialize<List<PlanFeatureMappingPayload>>(mappingsJson, _jsonOptions);
            payload = new PlanFeatureMappingsPayload
            {
                Mappings = mappings?
                    .Where(x => x.FeatureDefinitionId != Guid.Empty)
                    .GroupBy(x => x.FeatureDefinitionId)
                    .Select(x => x.Last())
                    .ToList() ?? []
            };
        }
        catch
        {
            return EntitlementSaveResult.Failure([_sharedLocalizer["ValidationFailed"].Value]);
        }

        if (payload.Mappings.Count == 0)
        {
            return EntitlementSaveResult.Success();
        }

        try
        {
            AddAuthHeader();
            var response = await _httpClient.PutAsJsonAsync($"{_gatewayUrl}/api/platform/subscription-plans/{planId}/features", payload, _jsonOptions);
            if (response.IsSuccessStatusCode)
            {
                return EntitlementSaveResult.Success();
            }

            return EntitlementSaveResult.Failure(await ExtractGatewayErrorsAsync(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SubscriptionPlans entitlement save failed for {SubscriptionPlanId}.", planId);
            return EntitlementSaveResult.Failure([_sharedLocalizer["GatewayError"].Value]);
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

    private static SubscriptionPlanEditViewModel ToEditModel(SubscriptionPlanDetailApiModel model) => new()
    {
        Id = model.Id,
        Code = model.Code,
        Name = model.Name,
        Description = model.Description,
        IsActive = model.IsActive,
        IsDefault = model.IsDefault,
        SortOrder = model.SortOrder,
        PriceMonthly = model.PriceMonthly,
        PriceYearly = model.PriceYearly,
        Currency = model.Currency,
        IsTrialPlan = model.IsTrialPlan,
        TrialDurationDays = model.TrialDurationDays,
        DefaultQuotasJson = model.DefaultQuotasJson,
        IncludedFeaturesText = model.IncludedFeaturesText,
        IncludedModuleKeysText = model.IncludedModuleKeysText
    };

    private static SubscriptionPlanSavePayload ToPayload(SubscriptionPlanEditViewModel model) => new()
    {
        Code = model.Code,
        Name = model.Name,
        Description = model.Description,
        IsActive = model.IsActive,
        IsDefault = model.IsDefault,
        SortOrder = model.SortOrder,
        PriceMonthly = model.PriceMonthly,
        PriceYearly = model.PriceYearly,
        Currency = model.Currency,
        IsTrialPlan = model.IsTrialPlan,
        TrialDurationDays = model.TrialDurationDays,
        DefaultQuotas = ParseQuotas(model.DefaultQuotasJson),
        IncludedFeatures = SplitLines(model.IncludedFeaturesText),
        IncludedModuleKeys = SplitLines(model.IncludedModuleKeysText)
    };

    private static IReadOnlyDictionary<string, decimal>? ParseQuotas(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, decimal>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return dict is null || dict.Count == 0 ? null : dict;
        }
        catch
        {
            return null;
        }
    }

    private bool TryValidateQuotasJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }

        try
        {
            JsonSerializer.Deserialize<Dictionary<string, decimal>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return true;
        }
        catch
        {
            ModelState.AddModelError(nameof(SubscriptionPlanEditViewModel.DefaultQuotasJson), _sharedLocalizer["ValidationFailed"].Value);
            return false;
        }
    }

    private static IReadOnlyList<string> SplitLines(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw
            .Split(['\r', '\n', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public sealed class SubscriptionPlanEditViewModel
    {
        public Guid? Id { get; set; }

        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsDefault { get; set; }
        public int? SortOrder { get; set; }
        public decimal? PriceMonthly { get; set; }
        public decimal? PriceYearly { get; set; }
        public string? Currency { get; set; }
        public bool IsTrialPlan { get; set; }
        public int? TrialDurationDays { get; set; }

        // Free-form text inputs for MVP; backend treats as structured dictionary/list.
        public string? DefaultQuotasJson { get; set; }
        public string? IncludedFeaturesText { get; set; }
        public string? IncludedModuleKeysText { get; set; }
        public string? EntitlementMappingsJson { get; set; }
    }

    private sealed class SubscriptionPlanSavePayload
    {
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public bool IsActive { get; init; }
        public bool IsDefault { get; init; }
        public int? SortOrder { get; init; }
        public decimal? PriceMonthly { get; init; }
        public decimal? PriceYearly { get; init; }
        public string? Currency { get; init; }
        public bool IsTrialPlan { get; init; }
        public int? TrialDurationDays { get; init; }
        public IReadOnlyDictionary<string, decimal>? DefaultQuotas { get; init; }
        public IReadOnlyList<string> IncludedFeatures { get; init; } = [];
        public IReadOnlyList<string> IncludedModuleKeys { get; init; } = [];
    }

    private sealed class PlanFeatureMappingsPayload
    {
        public IReadOnlyList<PlanFeatureMappingPayload> Mappings { get; init; } = [];
    }

    private sealed class PlanFeatureMappingPayload
    {
        public Guid FeatureDefinitionId { get; init; }
        public string AvailabilityStatus { get; init; } = "NotAvailable";
        public DateTimeOffset? EffectiveFromUtc { get; init; }
        public DateTimeOffset? EffectiveToUtc { get; init; }
        public byte[]? RowVersion { get; init; }
    }

    private sealed record EntitlementSaveResult(bool Succeeded, IReadOnlyList<string> Errors)
    {
        public static EntitlementSaveResult Success() => new(true, []);
        public static EntitlementSaveResult Failure(IReadOnlyList<string> errors) => new(false, errors.Count == 0 ? [] : errors);
    }

    private sealed class SubscriptionPlanDetailApiModel
    {
        public Guid Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public bool IsActive { get; init; }
        public bool IsDefault { get; init; }
        public int SortOrder { get; init; }
        public decimal? PriceMonthly { get; init; }
        public decimal? PriceYearly { get; init; }
        public string? Currency { get; init; }
        public bool IsTrialPlan { get; init; }
        public int? TrialDurationDays { get; init; }
        public Dictionary<string, decimal>? DefaultQuotas { get; init; }
        public List<string>? IncludedFeatures { get; init; }
        public List<string>? IncludedModuleKeys { get; init; }

        public string DefaultQuotasJson => DefaultQuotas is null ? string.Empty : JsonSerializer.Serialize(DefaultQuotas, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        public string IncludedFeaturesText => IncludedFeatures is null ? string.Empty : string.Join(Environment.NewLine, IncludedFeatures);
        public string IncludedModuleKeysText => IncludedModuleKeys is null ? string.Empty : string.Join(Environment.NewLine, IncludedModuleKeys);
    }

    private sealed class GatewayResponse<T>
    {
        public bool IsSuccessful { get; init; }
        public T? Data { get; init; }
        public List<string> Errors { get; init; } = [];
        public int StatusCode { get; init; }
    }

    private sealed record CurrencyLookupApiModel(string Code, string Name);
}

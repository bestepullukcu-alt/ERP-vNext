using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models.Procurement.Suppliers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Diten.Web.Controllers;

// MOD-0140 Supplier — tenant shell MVC controller.
// Route is single-segment /Suppliers (NO area prefix); views live under Views/Procurement/Suppliers/.
// Mirrors the GoldenReferenceCompact direct-gateway profile: server-side proxy for create/edit/lookups,
// browser JS reads/deletes straight through the Gateway (window.API.procurement + /api/suppliers).
[Authorize]
[Route("Suppliers")]
public sealed class SuppliersController : Controller
{
    private const string ViewRoot = "~/Views/Procurement/Suppliers";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<SuppliersController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public SuppliersController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<SuppliersController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index() => View($"{ViewRoot}/Index.cshtml");

    [HttpGet("Create")]
    public IActionResult Create() =>
        View($"{ViewRoot}/Create.cshtml", new SuppliersEditViewModel());

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] SuppliersEditViewModel model)
    {
        if (!ModelState.IsValid)
            return View($"{ViewRoot}/Create.cshtml", model);

        if (!AddAuthHeaders())
        {
            ModelState.AddModelError(string.Empty, _sharedLocalizer["Unauthorized"].Value);
            return View($"{ViewRoot}/Create.cshtml", model);
        }

        try
        {
            SetIdempotencyKey();
            var response = await _httpClient.PostAsJsonAsync(
                $"{_gatewayUrl}/api/suppliers",
                ToPayload(model),
                _jsonOptions);

            if (response.IsSuccessStatusCode)
            {
                var created = await ReadDataAsync<SupplierApiModel>(response);
                await SubmitOnboardingIfRequestedAsync(created?.SupplierId, model);
                TempData["SuccessMessage"] = _sharedLocalizer["RecordCreated"].Value;
                return RedirectToAction(nameof(Index));
            }

            AddGatewayErrorsToModelState(await ExtractGatewayErrorsAsync(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Supplier create failed.");
            AddGatewayErrorsToModelState(BuildExceptionErrors(ex));
        }

        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpGet("Edit/{id}")]
    public async Task<IActionResult> Edit(string id)
    {
        var model = await LoadEditModelAsync(id);
        if (model is null)
        {
            TempData["ErrorMessage"] = _sharedLocalizer["GatewayError"].Value;
            return RedirectToAction(nameof(Index));
        }

        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    [HttpPost("Edit/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, [FromForm] SuppliersEditViewModel model)
    {
        model.SupplierId = id;
        if (!ModelState.IsValid)
            return View($"{ViewRoot}/Edit.cshtml", model);

        if (!AddAuthHeaders())
        {
            ModelState.AddModelError(string.Empty, _sharedLocalizer["Unauthorized"].Value);
            return View($"{ViewRoot}/Edit.cshtml", model);
        }

        try
        {
            var response = await _httpClient.PatchAsJsonAsync(
                $"{_gatewayUrl}/api/suppliers/{Uri.EscapeDataString(id)}",
                ToPayload(model),
                _jsonOptions);

            if (response.IsSuccessStatusCode)
            {
                await SubmitOnboardingIfRequestedAsync(id, model);
                TempData["SuccessMessage"] = _sharedLocalizer["RecordUpdated"].Value;
                return RedirectToAction(nameof(Index));
            }

            AddGatewayErrorsToModelState(await ExtractGatewayErrorsAsync(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Supplier edit failed for {SupplierId}.", id);
            AddGatewayErrorsToModelState(BuildExceptionErrors(ex));
        }

        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    [HttpGet("Details/{id}")]
    public async Task<IActionResult> Details(string id)
    {
        var model = await LoadEditModelAsync(id);
        if (model is null)
        {
            TempData["ErrorMessage"] = _sharedLocalizer["GatewayError"].Value;
            return RedirectToAction(nameof(Index));
        }

        return View($"{ViewRoot}/Details.cshtml", model);
    }

    [HttpGet("get/{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var model = await LoadSupplierAsync(id);
        if (model is null)
            return Json(new { success = false });

        return Json(new { success = true, data = model });
    }

    // Bulk supplier id validation (fail-closed consumer surface: POST /api/suppliers/validate).
    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] List<string> supplierIds)
    {
        if (!AddAuthHeaders())
            return Json(new { results = Array.Empty<object>() });

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_gatewayUrl}/api/suppliers/validate",
                new { supplierIds = supplierIds ?? [] },
                _jsonOptions);

            if (!response.IsSuccessStatusCode)
                return Json(new { results = Array.Empty<object>() });

            var raw = await response.Content.ReadAsStringAsync();
            return Content(raw, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Supplier validate failed.");
            return Json(new { results = Array.Empty<object>() });
        }
    }

    [HttpGet("lookups")]
    public async Task<IActionResult> Lookups()
    {
        if (!AddAuthHeaders())
            return Json(EmptyLookups());

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/suppliers");
            if (!response.IsSuccessStatusCode)
                return Json(EmptyLookups());

            var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<List<SupplierApiModel>>>(_jsonOptions);
            var list = payload?.Data ?? [];

            return Json(new
            {
                countries = ToLookup(list.Select(x => x.Country))
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Supplier lookup load failed.");
            return Json(EmptyLookups());
        }
    }

    private async Task<SuppliersEditViewModel?> LoadEditModelAsync(string id)
    {
        var supplier = await LoadSupplierAsync(id);
        if (supplier is null)
            return null;

        var model = ToEditModel(supplier);
        var onboarding = await LoadOnboardingAsync(id);
        if (onboarding is not null)
        {
            model.OnboardingStatus = onboarding.OnboardingStatus;
            model.KycOutcome = onboarding.KycOutcome;
            model.SanctionsOutcome = onboarding.SanctionsOutcome;
        }

        return model;
    }

    private async Task<SupplierApiModel?> LoadSupplierAsync(string id)
    {
        if (!AddAuthHeaders())
            return null;

        var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/suppliers/{Uri.EscapeDataString(id)}");
        if (!response.IsSuccessStatusCode)
            return null;

        return await ReadDataAsync<SupplierApiModel>(response);
    }

    private async Task<SupplierOnboardingApiModel?> LoadOnboardingAsync(string id)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/suppliers/{Uri.EscapeDataString(id)}/onboarding");
            if (!response.IsSuccessStatusCode)
                return null;

            return await ReadDataAsync<SupplierOnboardingApiModel>(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Supplier onboarding load failed for {SupplierId}.", id);
            return null;
        }
    }

    private async Task SubmitOnboardingIfRequestedAsync(string? supplierId, SuppliersEditViewModel model)
    {
        if (string.IsNullOrWhiteSpace(supplierId))
            return;

        var hasKyc = !string.IsNullOrWhiteSpace(model.KycLegalName) || !string.IsNullOrWhiteSpace(model.KycRegistrationNo);
        var hasDocument = !string.IsNullOrWhiteSpace(model.DocumentType);
        if (!hasKyc && !hasDocument && !model.SubmitForApproval)
            return;

        try
        {
            SetIdempotencyKey();
            await _httpClient.PostAsJsonAsync(
                $"{_gatewayUrl}/api/suppliers/{Uri.EscapeDataString(supplierId)}/onboarding",
                ToOnboardingPayload(model),
                _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Supplier onboarding submit failed for {SupplierId}.", supplierId);
        }
    }

    private static SuppliersEditViewModel ToEditModel(SupplierApiModel model)
    {
        var primary = model.Contacts.FirstOrDefault(c => string.Equals(c.Type, "primary", StringComparison.OrdinalIgnoreCase))
            ?? model.Contacts.FirstOrDefault();

        return new SuppliersEditViewModel
        {
            SupplierId = model.SupplierId,
            Name = model.Name,
            Status = string.IsNullOrWhiteSpace(model.Status) ? "Active" : model.Status,
            Country = model.Country,
            TaxId = model.TaxId,
            ContactType = string.IsNullOrWhiteSpace(primary?.Type) ? "primary" : primary!.Type,
            ContactEmail = primary?.Email,
            ContactPhone = primary?.Phone,
            SourceSystem = model.SourceSystem,
            ExternalRef = model.ExternalRef
        };
    }

    private static SupplierSavePayload ToPayload(SuppliersEditViewModel model)
    {
        var payload = new SupplierSavePayload
        {
            Name = model.Name,
            Status = string.IsNullOrWhiteSpace(model.Status) ? "Active" : model.Status,
            Country = model.Country,
            TaxId = model.TaxId,
            SourceSystem = model.SourceSystem,
            ExternalRef = model.ExternalRef
        };

        if (!string.IsNullOrWhiteSpace(model.ContactEmail)
            || !string.IsNullOrWhiteSpace(model.ContactPhone)
            || !string.IsNullOrWhiteSpace(model.ContactType))
        {
            payload.Contacts.Add(new SupplierContactPayload
            {
                Type = string.IsNullOrWhiteSpace(model.ContactType) ? "primary" : model.ContactType,
                Email = model.ContactEmail,
                Phone = model.ContactPhone
            });
        }

        return payload;
    }

    private static SupplierOnboardingPayload ToOnboardingPayload(SuppliersEditViewModel model)
    {
        var payload = new SupplierOnboardingPayload
        {
            SubmitForApproval = model.SubmitForApproval
        };

        if (!string.IsNullOrWhiteSpace(model.KycLegalName) || !string.IsNullOrWhiteSpace(model.KycRegistrationNo))
        {
            payload.Kyc = new SupplierKycPayload
            {
                LegalName = model.KycLegalName,
                RegistrationNo = model.KycRegistrationNo
            };
        }

        if (!string.IsNullOrWhiteSpace(model.DocumentType))
        {
            payload.Documents.Add(new SupplierDocumentPayload
            {
                Type = model.DocumentType
            });
        }

        return payload;
    }

    private async Task<T?> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<T>>(_jsonOptions);
        return payload is not null ? payload.Data : default;
    }

    private object EmptyLookups() => new
    {
        countries = Array.Empty<object>()
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
        if (response.StatusCode == HttpStatusCode.Unauthorized)
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

    private void SetIdempotencyKey()
    {
        if (_httpClient.DefaultRequestHeaders.Contains("Idempotency-Key"))
            _httpClient.DefaultRequestHeaders.Remove("Idempotency-Key");

        _httpClient.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
    }

    private bool AddAuthHeaders()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
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

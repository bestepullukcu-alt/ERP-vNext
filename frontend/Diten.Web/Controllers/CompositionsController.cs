using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models.Compositions;
using Diten.Web.Views.MDM.Compositions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

[Authorize]
public sealed class CompositionsController : Controller
{
    private static readonly string[] TechnicalUnitCodes = ["MCG", "MG", "G", "ML", "PERCENT", "IU"];
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer _localizer;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public CompositionsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizerFactory localizerFactory)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
        _localizer = localizerFactory.Create(typeof(CompositionsIndex));
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return View(await BuildIndexPageModelAsync());
    }

    [HttpGet("/Compositions/Data")]
    public async Task<IActionResult> GetData()
    {
        AddAuthHeaders();
        var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/compositions");
        return await ProxyJsonResponseAsync(response);
    }

    [HttpGet("/Compositions/Data/{id:guid}")]
    public async Task<IActionResult> GetDataById(Guid id)
    {
        AddAuthHeaders();
        var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/compositions/{id}");
        return await ProxyJsonResponseAsync(response);
    }

    [HttpDelete("/Compositions/Data/{id:guid}")]
    public async Task<IActionResult> DeleteData(Guid id)
    {
        AddAuthHeaders();
        var response = await _httpClient.DeleteAsync($"{_gatewayUrl}/api/compositions/{id}");
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return Unauthorized();
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return NotFound();
        }

        return response.IsSuccessStatusCode
            ? NoContent()
            : StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private async Task<CompositionIndexPageViewModel> BuildIndexPageModelAsync()
    {
        return new CompositionIndexPageViewModel
        {
            DosageForms = await GetLookupAsync("dosage-forms"),
            LifecycleStates = await GetLookupAsync("lifecycle-states")
        };
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new CompositionEditViewModel();
        await PopulateEditLookupsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CompositionEditViewModel model)
    {
        await PopulateEditLookupsAsync(model);
        ForceCreateDefaults(model);
        ValidateComponentRows(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        AddAuthHeaders();
        var response = await _httpClient.PostAsJsonAsync($"{_gatewayUrl}/api/compositions", model, _jsonOptions);
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
        AddAuthHeaders();
        var composition = await _httpClient.GetFromJsonAsync<CompositionEditViewModel>($"{_gatewayUrl}/api/compositions/{id}", _jsonOptions);
        if (composition == null)
        {
            TempData["ErrorMessage"] = "RecordNotFound";
            return RedirectToAction(nameof(Index));
        }

        await PopulateEditLookupsAsync(composition);
        return View(composition);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, CompositionEditViewModel model)
    {
        model.Id = id;
        await PopulateEditLookupsAsync(model);
        ValidateComponentRows(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        AddAuthHeaders();
        var response = await _httpClient.PutAsJsonAsync($"{_gatewayUrl}/api/compositions/{id}", model, _jsonOptions);
        if (response.IsSuccessStatusCode)
        {
            TempData["SuccessMessage"] = "RecordUpdated";
            return RedirectToAction(nameof(Index));
        }

        await AddGatewayErrorAsync(response);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, [FromQuery] Guid? versionId = null)
    {
        AddAuthHeaders();
        var url = $"{_gatewayUrl}/api/compositions/{id}" + (versionId.HasValue ? $"?versionId={versionId}" : "");
        var response = await _httpClient.GetAsync(url);
        
        if (!response.IsSuccessStatusCode)
        {
            TempData["ErrorMessage"] = "RecordNotFound";
            return RedirectToAction(nameof(Index));
        }

        var composition = await response.Content.ReadFromJsonAsync<CompositionDetailsViewModel>(_jsonOptions);
        return View(composition);
    }

    [HttpPost]
    public async Task<IActionResult> ActivateVersion(Guid id, Guid versionId)
    {
        AddAuthHeaders();
        var response = await _httpClient.PatchAsync($"{_gatewayUrl}/api/compositions/versions/{versionId}/activate", null);
        
        if (response.IsSuccessStatusCode)
        {
            TempData["SuccessMessage"] = "VersionActivated";
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["ErrorMessage"] = "ActivationFailed";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task PopulateEditLookupsAsync(CompositionEditViewModel model)
    {
        model.DosageForms = await GetLookupAsync("dosage-forms");
        model.LifecycleStates = await GetLookupAsync("lifecycle-states");
        var allUnits = await GetLookupAsync("unit-of-measures");
        model.FillUnits = FilterTechnicalUnits(allUnits, model);

        if (!model.Id.HasValue)
        {
            EnsureCreateDefaults(model);
        }
        else
        {
            EnsureComponentDefaults(model, GetPreferredTechnicalUnitId(model.FillUnits));
        }
    }

    private void EnsureCreateDefaults(CompositionEditViewModel model)
    {
        model.LifecycleState = "Draft";

        var preferredUnitId = GetPreferredTechnicalUnitId(model.FillUnits);
        if (!model.TechnicalFillUnitId.HasValue || model.TechnicalFillUnitId == Guid.Empty)
        {
            model.TechnicalFillUnitId = preferredUnitId;
        }

        EnsureComponentDefaults(model, preferredUnitId);
    }

    private void ForceCreateDefaults(CompositionEditViewModel model)
    {
        var preferredUnitId = GetPreferredTechnicalUnitId(model.FillUnits);
        model.LifecycleState = "Draft";
        if (!model.TechnicalFillUnitId.HasValue || model.TechnicalFillUnitId == Guid.Empty)
        {
            model.TechnicalFillUnitId = preferredUnitId;
        }

        EnsureComponentDefaults(model, preferredUnitId);
    }

    private void EnsureComponentDefaults(CompositionEditViewModel model, Guid preferredUnitId)
    {
        for (var i = 0; i < model.Components.Count; i++)
        {
            var component = model.Components[i];
            if (component.Sequence <= 0)
            {
                component.Sequence = i + 1;
            }

            if (component.ComponentType <= 0)
            {
                component.ComponentType = 1;
            }

            if (component.UnitId == Guid.Empty)
            {
                component.UnitId = preferredUnitId;
            }
        }
    }

    private void ValidateComponentRows(CompositionEditViewModel model)
    {
        if (model.Components.Count == 0)
        {
            ModelState.AddModelError(nameof(model.Components), _localizer["AtLeastOneComponentRequired"]);
            return;
        }

        for (var index = 0; index < model.Components.Count; index++)
        {
            var component = model.Components[index];
            if (component.ComponentId == Guid.Empty)
            {
                ModelState.AddModelError($"{nameof(model.Components)}[{index}].{nameof(component.ComponentId)}", _localizer["ComponentIngredientRequired"]);
            }

            if (component.Quantity <= 0)
            {
                ModelState.AddModelError($"{nameof(model.Components)}[{index}].{nameof(component.Quantity)}", _localizer["ComponentQuantityRequired"]);
            }

            if (component.UnitId == Guid.Empty)
            {
                ModelState.AddModelError($"{nameof(model.Components)}[{index}].{nameof(component.UnitId)}", _localizer["ComponentUnitRequired"]);
            }
        }
    }

    private List<LookupApiViewModel> FilterTechnicalUnits(
        IEnumerable<LookupApiViewModel> allUnits,
        CompositionEditViewModel model)
    {
        var selectedUnitIds = model.Components
            .Select(x => x.UnitId)
            .Where(x => x != Guid.Empty)
            .ToHashSet();

        if (model.TechnicalFillUnitId.HasValue && model.TechnicalFillUnitId.Value != Guid.Empty)
        {
            selectedUnitIds.Add(model.TechnicalFillUnitId.Value);
        }

        if (model.StrengthUnitId != Guid.Empty)
        {
            selectedUnitIds.Add(model.StrengthUnitId);
        }

        return allUnits
            .Where(x => TechnicalUnitCodes.Contains(x.Code, StringComparer.OrdinalIgnoreCase) || selectedUnitIds.Contains(x.Id))
            .OrderBy(x => Array.IndexOf(TechnicalUnitCodes, x.Code.ToUpperInvariant()))
            .ThenBy(x => x.Name)
            .ToList();
    }

    private static Guid GetPreferredTechnicalUnitId(IEnumerable<LookupApiViewModel> units)
    {
        return units.FirstOrDefault(x => string.Equals(x.Code, "MG", StringComparison.OrdinalIgnoreCase))?.Id
            ?? units.FirstOrDefault()?.Id
            ?? Guid.Empty;
    }



    private async Task<List<LookupApiViewModel>> GetLookupAsync(string endpoint)
    {
        try
        {
            AddAuthHeaders();
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/{endpoint}");
            if (response.IsSuccessStatusCode)
            {
                var payload = await response.Content.ReadFromJsonAsync<ApiDataResponse<List<LookupApiViewModel>>>(_jsonOptions);
                return payload?.Data ?? [];
            }
            return [];
        }
        catch
        {
            return [];
        }
    }

    private async Task AddGatewayErrorAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            Response.Redirect("/Account/Login");
            return;
        }
        var error = await response.Content.ReadAsStringAsync();
        ModelState.AddModelError(string.Empty, string.IsNullOrWhiteSpace(error) ? "Gateway Error" : error);
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

    private async Task<IActionResult> ProxyJsonResponseAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return Unauthorized();
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return NotFound();
        }

        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode, json);
        }

        return Content(json, "application/json");
    }
}

public class ApiDataResponse<T>
{
    public T Data { get; set; }
}

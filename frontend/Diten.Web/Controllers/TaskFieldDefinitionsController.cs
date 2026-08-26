using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models;
using Diten.Web.Models.TaskFieldDefinitions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Diten.Web.Controllers;

/// <summary>
/// MOD-0024 Phase 5 — the configurable field-definition management surface.
///
/// <para>A copy of <see cref="GoldenReferenceCompactController"/>'s shape, deliberately: two management screens
/// shipping in one week have to read as one product, so the page flow (list → full-page create/edit → details),
/// the gateway plumbing, and the error handling are the reference's rather than re-invented.</para>
///
/// <para>Route lives UNDER Tasks so the surface it configures owns it.</para>
/// </summary>
[Authorize]
[Route("Tasks/FieldDefinitions")]
public sealed class TaskFieldDefinitionsController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<TaskFieldDefinitionsController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public TaskFieldDefinitionsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<TaskFieldDefinitionsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    private const string ApiPath = "/api/v1/tasks/field-definitions";
    private const string ViewRoot = "~/Views/Tasks/FieldDefinitions";

    [HttpGet("")]
    public IActionResult Index() => View($"{ViewRoot}/Index.cshtml");

    [HttpGet("Create")]
    public IActionResult Create() => View($"{ViewRoot}/Create.cshtml", new TaskFieldDefinitionEditViewModel());

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] TaskFieldDefinitionEditViewModel model)
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
            var response = await _httpClient.PostAsJsonAsync($"{_gatewayUrl}{ApiPath}", ToCreatePayload(model), _jsonOptions);
            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = _sharedLocalizer["RecordCreated"].Value;
                return RedirectToAction(nameof(Index));
            }

            AddGatewayErrorsToModelState(await ExtractGatewayErrorsAsync(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task field definition create failed.");
            AddGatewayErrorsToModelState(BuildExceptionErrors(ex));
        }

        return View($"{ViewRoot}/Create.cshtml", model);
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

        return View($"{ViewRoot}/Edit.cshtml", detail);
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

        return View($"{ViewRoot}/Details.cshtml", detail);
    }

    [HttpPost("Edit/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, [FromForm] TaskFieldDefinitionEditViewModel model)
    {
        model.Id = id;
        if (!ModelState.IsValid)
            return View($"{ViewRoot}/Edit.cshtml", model);

        if (!AddAuthHeaders())
        {
            ModelState.AddModelError(string.Empty, _sharedLocalizer["Unauthorized"].Value);
            return View($"{ViewRoot}/Edit.cshtml", model);
        }

        try
        {
            var response = await _httpClient.PutAsJsonAsync($"{_gatewayUrl}{ApiPath}/{id}", ToUpdatePayload(model), _jsonOptions);
            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = _sharedLocalizer["RecordUpdated"].Value;
                return RedirectToAction(nameof(Index));
            }

            AddGatewayErrorsToModelState(await ExtractGatewayErrorsAsync(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task field definition edit failed for {DefinitionId}.", id);
            AddGatewayErrorsToModelState(BuildExceptionErrors(ex));
        }

        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    private async Task<TaskFieldDefinitionEditViewModel?> LoadApiModelAsync(Guid id)
    {
        if (!AddAuthHeaders())
            return null;

        var response = await _httpClient.GetAsync($"{_gatewayUrl}{ApiPath}/{id}");
        if (!response.IsSuccessStatusCode)
            return null;

        var payload = await response.Content
            .ReadFromJsonAsync<GatewayResponse<TaskFieldDefinitionEditViewModel>>(_jsonOptions);
        return payload?.Data;
    }

    /// <summary>
    /// The create payload. Both label fields travel; the server enforces "exactly one" and the view model
    /// pre-checks it, so a definition can never reach storage with nothing to render but its code.
    /// </summary>
    private static object ToCreatePayload(TaskFieldDefinitionEditViewModel model) => new
    {
        code = model.Code,
        labelResourceKey = Nullable(model.LabelResourceKey),
        labelText = Nullable(model.LabelText),
        valueType = model.ValueType,
        section = model.Section,
        importance = model.Importance,
        isRequired = model.IsRequired,
        sortOrder = model.SortOrder,
        optionsSourceKind = model.OptionsSourceKind,
        optionsSourceKey = Nullable(model.OptionsSourceKey),
        appliesToModuleCode = Nullable(model.AppliesToModuleCode),
        classification = model.Classification,
        defaultAccessState = model.DefaultAccessState,
        isActive = model.IsActive
    };

    /// <summary>
    /// The update payload carries NO code. Stored values join to their definition by code, so an edited code
    /// orphans them all — the server refuses one anyway, and not sending it means the form cannot even try.
    /// </summary>
    private static object ToUpdatePayload(TaskFieldDefinitionEditViewModel model) => new
    {
        labelResourceKey = Nullable(model.LabelResourceKey),
        labelText = Nullable(model.LabelText),
        valueType = model.ValueType,
        section = model.Section,
        importance = model.Importance,
        isRequired = model.IsRequired,
        sortOrder = model.SortOrder,
        optionsSourceKind = model.OptionsSourceKind,
        optionsSourceKey = Nullable(model.OptionsSourceKey),
        appliesToModuleCode = Nullable(model.AppliesToModuleCode),
        classification = model.Classification,
        defaultAccessState = model.DefaultAccessState,
        isActive = model.IsActive,
        expectedVersion = model.ExpectedVersion
    };

    private static string? Nullable(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

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

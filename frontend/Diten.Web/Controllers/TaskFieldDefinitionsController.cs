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
        // BL-388 — the form keeps SortOrder optional (UI-020), the API contract's SortOrder is a plain int: empty means 0.
        sortOrder = model.SortOrder ?? 0,
        optionsSourceKind = model.OptionsSourceKind,
        optionsSourceKey = Nullable(model.OptionsSourceKey),
        appliesToModuleCode = Nullable(model.AppliesToModuleCode),
        classification = model.Classification,
        defaultAccessState = model.DefaultAccessState,
        isActive = model.IsActive,
        stage = model.Stage
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
        // BL-388 — the form keeps SortOrder optional (UI-020), the API contract's SortOrder is a plain int: empty means 0.
        sortOrder = model.SortOrder ?? 0,
        optionsSourceKind = model.OptionsSourceKind,
        optionsSourceKey = Nullable(model.OptionsSourceKey),
        appliesToModuleCode = Nullable(model.AppliesToModuleCode),
        classification = model.Classification,
        defaultAccessState = model.DefaultAccessState,
        isActive = model.IsActive,
        expectedVersion = model.ExpectedVersion,
        stage = model.Stage
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
        if (TryReadProblemDetailsErrors(raw, out var problemErrors))
            return problemErrors;

        return [string.IsNullOrWhiteSpace(raw) ? _sharedLocalizer["GatewayError"].Value : raw];
    }

    /// <summary>
    /// BL-388 — ASP.NET's OWN 400 is not the Platform envelope. When a request body cannot even be bound (a null for
    /// a non-nullable field, a wrong type) the API answers before any handler runs, with a ProblemDetails whose
    /// <c>errors</c> is a field → messages dictionary. The envelope read above cannot bind that shape, so the body
    /// used to fall through to the raw fallback and the form printed the JSON itself. Field messages are returned
    /// as they are; a ProblemDetails with none gets the shared generic message, never its own raw body.
    /// </summary>
    private bool TryReadProblemDetailsErrors(string raw, out List<string> errors)
    {
        errors = [];
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        try
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            var hasFieldErrors = root.TryGetProperty("errors", out var fieldErrors) && fieldErrors.ValueKind == JsonValueKind.Object;
            var isProblemDetails = hasFieldErrors || (root.TryGetProperty("title", out _) && root.TryGetProperty("status", out _));
            if (!isProblemDetails)
                return false;

            if (hasFieldErrors)
            {
                errors = fieldErrors.EnumerateObject()
                    .Where(field => field.Value.ValueKind == JsonValueKind.Array)
                    .SelectMany(field => field.Value.EnumerateArray())
                    .Where(message => message.ValueKind == JsonValueKind.String)
                    .Select(message => message.GetString())
                    .Where(message => !string.IsNullOrWhiteSpace(message))
                    .Select(message => message!)
                    .Distinct()
                    .ToList();
            }

            if (errors.Count == 0)
                errors.Add(_sharedLocalizer["GatewayError"].Value);

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
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

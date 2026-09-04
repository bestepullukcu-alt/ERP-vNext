using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models.TaskChecklistTemplates;
using Diten.Web.Models.TaskFieldDefinitions;   // GatewayResponse<T> — the sibling screens already declare it
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

/// <summary>
/// MOD-0024 / BL-054 — the reusable CHECKLIST management surface.
///
/// <para>The first half of the missing link. A recurring rule generated a task carrying a title and nothing else,
/// because the rule's template picker had no source and there was nowhere in the product to create one. This
/// screen ships BEFORE its sibling for exactly the same reason the picker's emptiness was a defect: the
/// task-template form has a checklist picker of its own, and building that before this would have repeated the
/// failure one level in.</para>
///
/// <para>A deliberate copy of <see cref="TaskRecurrenceRulesController"/>'s shape — the two are siblings in one
/// module, and a second pattern would make them read as two products.</para>
/// </summary>
[Authorize]
[Route("Tasks/ChecklistTemplates")]
public sealed class TaskChecklistTemplatesController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<TaskChecklistTemplatesController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public TaskChecklistTemplatesController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<TaskChecklistTemplatesController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    private const string ApiPath = "/api/v1/tasks/checklist-templates";
    private const string ViewRoot = "~/Views/Tasks/ChecklistTemplates";

    [HttpGet("")]
    public IActionResult Index() => View($"{ViewRoot}/Index.cshtml");

    [HttpGet("Create")]
    public IActionResult Create()
        => View($"{ViewRoot}/Create.cshtml", new ChecklistTemplateEditViewModel
        {
            // One blank row to type into. A step editor that opens with no rows makes the user hunt for the
            // "add" button before they can begin, and the emptiest possible state is also the one that most
            // resembles a screen that failed to load.
            Items = [new ChecklistTemplateItemViewModel()]
        });

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] ChecklistTemplateEditViewModel model)
    {
        if (!ModelState.IsValid)
            return View($"{ViewRoot}/Create.cshtml", WithAtLeastOneRow(model));

        if (!AddAuthHeaders())
        {
            ModelState.AddModelError(string.Empty, _sharedLocalizer["Unauthorized"].Value);
            return View($"{ViewRoot}/Create.cshtml", WithAtLeastOneRow(model));
        }

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_gatewayUrl}{ApiPath}", ToCreatePayload(model), _jsonOptions);
            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = _sharedLocalizer["RecordCreated"].Value;
                return RedirectToAction(nameof(Index));
            }

            AddGatewayErrorsToModelState(await ExtractGatewayErrorsAsync(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Checklist template create failed.");
            AddGatewayErrorsToModelState(BuildExceptionErrors(ex));
        }

        return View($"{ViewRoot}/Create.cshtml", WithAtLeastOneRow(model));
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

        return View($"{ViewRoot}/Edit.cshtml", WithAtLeastOneRow(detail));
    }

    [HttpPost("Edit/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, [FromForm] ChecklistTemplateEditViewModel model)
    {
        model.Id = id;
        if (!ModelState.IsValid)
            return View($"{ViewRoot}/Edit.cshtml", WithAtLeastOneRow(model));

        if (!AddAuthHeaders())
        {
            ModelState.AddModelError(string.Empty, _sharedLocalizer["Unauthorized"].Value);
            return View($"{ViewRoot}/Edit.cshtml", WithAtLeastOneRow(model));
        }

        try
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"{_gatewayUrl}{ApiPath}/{id}", ToUpdatePayload(model), _jsonOptions);
            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = _sharedLocalizer["RecordUpdated"].Value;
                return RedirectToAction(nameof(Index));
            }

            AddGatewayErrorsToModelState(await ExtractGatewayErrorsAsync(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Checklist template edit failed for {TemplateId}.", id);
            AddGatewayErrorsToModelState(BuildExceptionErrors(ex));
        }

        return View($"{ViewRoot}/Edit.cshtml", WithAtLeastOneRow(model));
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

    private async Task<ChecklistTemplateEditViewModel?> LoadApiModelAsync(Guid id)
    {
        if (!AddAuthHeaders())
            return null;

        var response = await _httpClient.GetAsync($"{_gatewayUrl}{ApiPath}/{id}");
        if (!response.IsSuccessStatusCode)
            return null;

        var payload = await response.Content
            .ReadFromJsonAsync<GatewayResponse<ChecklistTemplateEditViewModel>>(_jsonOptions);
        return payload?.Data;
    }

    /// <summary>
    /// Keeps one empty row at the bottom of the editor on every render.
    ///
    /// <para>Not cosmetic: a redisplay after a validation failure that dropped the blank row would move the
    /// controls under the user's cursor, and a template whose last row was deleted would come back with nowhere
    /// to type. The blank row is skipped on save (see <c>ChecklistTemplateItemViewModel.IsBlank</c>).</para>
    /// </summary>
    private static ChecklistTemplateEditViewModel WithAtLeastOneRow(ChecklistTemplateEditViewModel model)
    {
        if (model.Items.Count == 0 || !model.Items[^1].IsBlank)
            model.Items.Add(new ChecklistTemplateItemViewModel());

        return model;
    }

    /*
     * The two payloads. Blank rows never travel — the editor always carries one so there is somewhere to type,
     * and sending it would be a step with no code and no words that the server has to refuse.
     *
     * SortOrder is deliberately NOT sent. The server renumbers from arrival order; a client numbering leaves
     * gaps and ties the moment a row is removed, after which the same checklist reads in a different order on
     * two screens.
     */
    private static object ToCreatePayload(ChecklistTemplateEditViewModel model) => new
    {
        code = model.Code,
        name = model.Name,
        description = model.Description,
        items = ToItemPayload(model),
        isActive = model.IsActive
    };

    private static object ToUpdatePayload(ChecklistTemplateEditViewModel model) => new
    {
        code = model.Code,
        name = model.Name,
        description = model.Description,
        items = ToItemPayload(model),
        isActive = model.IsActive,
        expectedVersion = model.ExpectedVersion
    };

    private static object[] ToItemPayload(ChecklistTemplateEditViewModel model) => model.Items
        .Where(item => !item.IsBlank)
        .Select(object (item) => new
        {
            code = item.Code!.Trim(),
            // No resource key from this screen, ever: a tenant administrator cannot add a line to our resx
            // files, so a key they typed would render on the task as the key itself.
            labelResourceKey = (string?)null,
            labelText = item.LabelText!.Trim(),
            requirement = item.Requirement,
            sortOrder = 0,
            evidenceRequired = item.EvidenceRequired
        })
        .ToArray();

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

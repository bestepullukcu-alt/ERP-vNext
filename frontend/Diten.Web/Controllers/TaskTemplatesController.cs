using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models.TaskFieldDefinitions;   // GatewayResponse<T> — the sibling screens already declare it
using Diten.Web.Models.TaskTemplates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

/// <summary>
/// MOD-0024 / BL-054 — the reusable TASK-SHAPE management surface.
///
/// <para>The second half of the missing link, and the one that closes it. A recurring rule generated a task with
/// a title and nothing else — no priority, no due date, no checklist — because its template picker had no source.
/// The entity, the lookup endpoint and the picker all existed; there was simply nowhere to create a template.
/// Nothing needed connecting here, only building.</para>
///
/// <para>⚠ It ships AFTER <see cref="TaskChecklistTemplatesController"/>, not beside it. The form below carries a
/// checklist picker of its own, and building it first would have reproduced the same empty control one level
/// in.</para>
/// </summary>
[Authorize]
[Route("Tasks/Templates")]
public sealed class TaskTemplatesController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<TaskTemplatesController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public TaskTemplatesController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<TaskTemplatesController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    private const string ApiPath = "/api/v1/tasks/templates";
    private const string ViewRoot = "~/Views/Tasks/Templates";

    [HttpGet("")]
    public IActionResult Index() => View($"{ViewRoot}/Index.cshtml");

    [HttpGet("Create")]
    public IActionResult Create() => View($"{ViewRoot}/Create.cshtml", new TaskTemplateEditViewModel());

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] TaskTemplateEditViewModel model)
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
            _logger.LogError(ex, "Task template create failed.");
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

    [HttpPost("Edit/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, [FromForm] TaskTemplateEditViewModel model)
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
            _logger.LogError(ex, "Task template edit failed for {TemplateId}.", id);
            AddGatewayErrorsToModelState(BuildExceptionErrors(ex));
        }

        return View($"{ViewRoot}/Edit.cshtml", model);
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

    private async Task<TaskTemplateEditViewModel?> LoadApiModelAsync(Guid id)
    {
        if (!AddAuthHeaders())
            return null;

        var response = await _httpClient.GetAsync($"{_gatewayUrl}{ApiPath}/{id}");
        if (!response.IsSuccessStatusCode)
            return null;

        var payload = await response.Content
            .ReadFromJsonAsync<GatewayResponse<TaskTemplateEditViewModel>>(_jsonOptions);
        return payload?.Data;
    }

    /*
     * The two payloads.
     *
     * The nulling is the interesting part, and it is the same rule the recurrence-rule controller states: a
     * template whose default is not a pool carries NO pool id. Sending one anyway would leave an identity on the
     * record that nothing reads today and something reads tomorrow — and the server refuses it outright rather
     * than dropping it, so a stale value here would surface as a save that fails for no visible reason.
     *
     * `legalEntityId` travels as sent, empty included: "every company" is an ANSWER, and the server normalises
     * Guid.Empty to null rather than storing a company that does not exist.
     */
    private static object ToCreatePayload(TaskTemplateEditViewModel model) => new
    {
        code = model.Code,
        name = model.Name,
        titleTemplate = model.TitleTemplate,
        descriptionTemplate = model.DescriptionTemplate,
        defaultPriority = model.DefaultPriority,
        defaultAssignmentTarget = model.DefaultAssignmentTarget,
        defaultPoolPositionId = model.DefaultAssignmentTarget == "PositionPool"
            ? model.DefaultPoolPositionId
            : null,
        defaultDueInDays = model.DefaultDueInDays,
        checklistTemplateId = model.ChecklistTemplateId,
        legalEntityId = model.LegalEntityId,
        isActive = model.IsActive
    };

    private static object ToUpdatePayload(TaskTemplateEditViewModel model) => new
    {
        code = model.Code,
        name = model.Name,
        titleTemplate = model.TitleTemplate,
        descriptionTemplate = model.DescriptionTemplate,
        defaultPriority = model.DefaultPriority,
        defaultAssignmentTarget = model.DefaultAssignmentTarget,
        defaultPoolPositionId = model.DefaultAssignmentTarget == "PositionPool"
            ? model.DefaultPoolPositionId
            : null,
        defaultDueInDays = model.DefaultDueInDays,
        checklistTemplateId = model.ChecklistTemplateId,
        legalEntityId = model.LegalEntityId,
        isActive = model.IsActive,
        expectedVersion = model.ExpectedVersion
    };

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

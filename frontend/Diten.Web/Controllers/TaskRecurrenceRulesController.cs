using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models.TaskFieldDefinitions;   // GatewayResponse<T> — the sibling screen already declares it
using Diten.Web.Models.TaskRecurrenceRules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

/// <summary>
/// MOD-0024 Phase 4 — the recurring-task rule management surface (BL-052).
///
/// <para>The engine shipped complete and unreachable: entity, hourly sweep, five CRUD endpoints, proxy routes —
/// and no screen, so a rule could only be defined by calling the API by hand. This is a deliberate copy of
/// <see cref="TaskFieldDefinitionsController"/>'s shape, because the two screens are siblings in the same module
/// and a second pattern would make them read as two products.</para>
/// </summary>
[Authorize]
[Route("Tasks/RecurrenceRules")]
public sealed class TaskRecurrenceRulesController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<TaskRecurrenceRulesController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public TaskRecurrenceRulesController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<TaskRecurrenceRulesController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    private const string ApiPath = "/api/v1/tasks/recurrence-rules";
    private const string ViewRoot = "~/Views/Tasks/RecurrenceRules";

    [HttpGet("")]
    public IActionResult Index() => View($"{ViewRoot}/Index.cshtml");

    [HttpGet("Create")]
    public IActionResult Create() => View($"{ViewRoot}/Create.cshtml", new TaskRecurrenceRuleEditViewModel());

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] TaskRecurrenceRuleEditViewModel model)
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
            _logger.LogError(ex, "Task recurrence rule create failed.");
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
    public async Task<IActionResult> Edit(Guid id, [FromForm] TaskRecurrenceRuleEditViewModel model)
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
            _logger.LogError(ex, "Task recurrence rule edit failed for {RuleId}.", id);
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

    private async Task<TaskRecurrenceRuleEditViewModel?> LoadApiModelAsync(Guid id)
    {
        if (!AddAuthHeaders())
            return null;

        var response = await _httpClient.GetAsync($"{_gatewayUrl}{ApiPath}/{id}");
        if (!response.IsSuccessStatusCode)
            return null;

        var payload = await response.Content
            .ReadFromJsonAsync<GatewayResponse<TaskRecurrenceRuleEditViewModel>>(_jsonOptions);
        return payload?.Data;
    }

    /*
     * The two payloads. `assignmentTarget` travels as the engine's own spelling and NEVER as SelfAssigned —
     * the model refuses that before we get here, and the server refuses it again.
     *
     * The nulling is the interesting part: a rule assigned to a person carries no pool id and vice versa. Sending
     * both would leave a stale identity on the record that nothing reads today and something reads tomorrow.
     */
    private static object ToCreatePayload(TaskRecurrenceRuleEditViewModel model) => new
    {
        name = model.Name,
        frequency = model.Frequency,
        interval = model.Interval ?? 1,
        startsAt = model.StartsAt,
        // Omitted-as-null on purpose: an open-ended rule has no end, and today's date would end it immediately.
        endsAt = model.EndsAt,
        assignmentTarget = model.AssignmentTarget,
        assigneeUserId = model.AssignmentTarget == "Person" ? model.AssigneeUserId : null,
        poolPositionId = model.AssignmentTarget == "PositionPool" ? model.PoolPositionId : null,
        organizationUnitId = (Guid?)null,
        taskTemplateId = model.TaskTemplateId,
        isActive = model.IsActive
    };

    private static object ToUpdatePayload(TaskRecurrenceRuleEditViewModel model) => new
    {
        name = model.Name,
        frequency = model.Frequency,
        interval = model.Interval ?? 1,
        startsAt = model.StartsAt,
        endsAt = model.EndsAt,
        assignmentTarget = model.AssignmentTarget,
        assigneeUserId = model.AssignmentTarget == "Person" ? model.AssigneeUserId : null,
        poolPositionId = model.AssignmentTarget == "PositionPool" ? model.PoolPositionId : null,
        organizationUnitId = (Guid?)null,
        taskTemplateId = model.TaskTemplateId,
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

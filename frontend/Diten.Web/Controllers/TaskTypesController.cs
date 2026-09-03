using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models;
using Diten.Web.Models.TaskTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Diten.Web.Controllers;

/// <summary>
/// DCP-005 slice 1 — the TASK TYPE management surface.
///
/// <para>A copy of <see cref="TaskFieldDefinitionsController"/>'s shape, deliberately: the two configuration
/// screens in this module have to read as one product, so the page flow (list → full-page create/edit →
/// details), the gateway plumbing and the error handling are the sibling's rather than re-invented.</para>
///
/// <para>Route lives UNDER Tasks so the surface it configures owns it.</para>
///
/// <para>⚠ <b>There is no Delete action here, and its absence is the design.</b> A type that has been used is
/// part of the identity of every task opened under it, so it is RETIRED — the same rule folders and controlled
/// documents follow, and the reason the server exposes no delete route either.</para>
/// </summary>
[Authorize]
[Route("Tasks/TaskTypes")]
public sealed class TaskTypesController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly IStringLocalizer<Diten.Web.Views.Tasks.TaskTypes.TaskTypesIndex> _localizer;
    private readonly ILogger<TaskTypesController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public TaskTypesController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        IStringLocalizer<Diten.Web.Views.Tasks.TaskTypes.TaskTypesIndex> localizer,
        ILogger<TaskTypesController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _localizer = localizer;
        _logger = logger;
    }

    private const string ApiPath = "/api/v1/tasks/task-types";
    private const string ViewRoot = "~/Views/Tasks/TaskTypes";

    [HttpGet("")]
    public IActionResult Index() => View($"{ViewRoot}/Index.cshtml");

    [HttpGet("Create")]
    public async Task<IActionResult> Create()
    {
        var model = new TaskTypeEditViewModel();
        model.SystemOutcomes = await LoadSystemOutcomesAsync();
        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] TaskTypeEditViewModel model)
    {
        /*
         * ⚠ RELOADED ON EVERY RE-RENDER, not only on the first GET. The catalogue is not posted back — codes and
         * resource keys are code-owned — so a form redisplayed after a validation failure would lose its picker
         * and silently offer only custom outcomes. Same class of omission as the GroupDocumentsText rehydration.
         */
        model.SystemOutcomes = await LoadSystemOutcomesAsync();

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
            _logger.LogError(ex, "Task type create failed.");
            AddGatewayErrorsToModelState(BuildExceptionErrors(ex));
        }

        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpGet("Edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var detail = await LoadApiModelAsync(id);
        if (detail is not null)
        {
            detail.SystemOutcomes = await LoadSystemOutcomesAsync();
        }

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
    public async Task<IActionResult> Edit(Guid id, [FromForm] TaskTypeEditViewModel model)
    {
        model.Id = id;
        model.SystemOutcomes = await LoadSystemOutcomesAsync();
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
            _logger.LogError(ex, "Task type edit failed for {DefinitionId}.", id);
            AddGatewayErrorsToModelState(BuildExceptionErrors(ex));
        }

        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    private async Task<TaskTypeEditViewModel?> LoadApiModelAsync(Guid id)
    {
        if (!AddAuthHeaders())
            return null;

        var response = await _httpClient.GetAsync($"{_gatewayUrl}{ApiPath}/{id}");
        if (!response.IsSuccessStatusCode)
            return null;

        var payload = await response.Content
            .ReadFromJsonAsync<GatewayResponse<TaskTypeEditViewModel>>(_jsonOptions);

        /*
         * ⚠ THE TEXTAREA HAS TO BE REHYDRATED FROM THE LIST, and leaving this out was silent data loss.
         *
         * The API answers with `groupDocuments` (a list); the form edits `groupDocumentsText` (one UID per
         * line). Nothing derived the second from the first, so the edit form opened EMPTY on a type that had
         * governing documents — and the save is a full replace, so pressing Save without touching the field
         * deleted them. No error, no diff, 302 and a success message. FOUND LIVE on 2026-08-26 while setting up
         * slice 3, on a type carrying two documents.
         *
         * Same shape as the BL-024 field-preservation defect: the write path was right, the READ path never
         * handed it what it needed to preserve.
         */
        if (payload?.Data is { } model)
        {
            if (model.GroupDocuments is { Count: > 0 })
            {
                model.GroupDocumentsText = string.Join(Environment.NewLine, model.GroupDocuments);
            }

            /*
             * ⚠ A JSON `null` OVERWRITES A PROPERTY INITIALISER — it does not leave the default in place.
             *
             * The API emits the dictionary as a list and never as null, so this looks unnecessary today. It is
             * not: `TaskTypeDto.ClosureOutcomes` is a NULLABLE parameter, so one older service, one cached
             * response or one future change that omits it would deserialize null onto this list — and the form
             * enumerates it unconditionally. That is a 500 on the edit screen for a field the user never touched.
             *
             * Empty is also the honest reading: a type with no dictionary asks nothing when a task closes.
             */
            model.ClosureOutcomes ??= [];
        }

        return payload?.Data;
    }

    /// <summary>
    /// The SYSTEM closure outcomes the picker offers.
    ///
    /// <para><b>An empty list is a DEGRADED form, not a broken one.</b> If the gateway cannot answer, the editor
    /// still opens and still saves — the administrator simply gets no system outcomes to pick from and can only
    /// write their own. Refusing to render the page would take away the type's OTHER fields over a list that is
    /// optional by design; the log line says what was lost.</para>
    /// </summary>
    private async Task<IReadOnlyList<TaskTypeClosureOutcomeViewModel>> LoadSystemOutcomesAsync()
    {
        if (!AddAuthHeaders())
        {
            return [];
        }

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}{ApiPath}/closure-outcome-catalog");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Closure outcome catalogue unavailable ({Status}); the type editor offers custom outcomes only.",
                    (int)response.StatusCode);
                return [];
            }

            var payload = await response.Content
                .ReadFromJsonAsync<GatewayResponse<List<TaskTypeClosureOutcomeViewModel>>>(_jsonOptions);
            return payload?.Data ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Closure outcome catalogue could not be read; offering custom outcomes only.");
            return [];
        }
    }

    /// <summary>
    /// The create payload. Documents arrive as one UID per line and are split here; the server de-duplicates
    /// and drops blanks, so a stray empty line is corrected rather than refused.
    /// </summary>
    private static object ToCreatePayload(TaskTypeEditViewModel model) => new
    {
        code = model.Code,
        name = model.Name,
        description = Nullable(model.Description),
        recordClass = model.RecordClass,
        gqmsDomain = Nullable(model.GqmsDomain),
        functionCode = Nullable(model.FunctionCode),
        isQualityEvent = model.IsQualityEvent,
        groupDocuments = SplitDocuments(model.GroupDocumentsText),
        localDocuments = (object?)null,
        closureOutcomes = ClosureOutcomesPayload(model)
    };

    /// <summary>
    /// The update payload DOES carry the code — and that is the opposite of the sibling's choice, on purpose.
    ///
    /// <para>The field definition omits its code so the form cannot even try to change it. A task type sends
    /// the one it displayed, and the server compares: a differing code is REFUSED with a message rather than
    /// silently discarded. The difference matters because a type's code is read aloud, printed on records and
    /// typed into the counterparty's spreadsheets — if a client ever does send a different one, the person
    /// needs to be told, not quietly ignored.</para>
    /// </summary>
    private static object ToUpdatePayload(TaskTypeEditViewModel model) => new
    {
        code = model.Code,
        name = model.Name,
        description = Nullable(model.Description),
        recordClass = model.RecordClass,
        gqmsDomain = Nullable(model.GqmsDomain),
        functionCode = Nullable(model.FunctionCode),
        isQualityEvent = model.IsQualityEvent,
        groupDocuments = SplitDocuments(model.GroupDocumentsText),
        localDocuments = (object?)null,
        closureOutcomes = ClosureOutcomesPayload(model)
    };

    /// <summary>
    /// The closure outcome dictionary — or NULL, meaning "do not touch it".
    ///
    /// <para>⚠ <b>THE NULL IS THE POINT, and getting it wrong deletes a tenant's configuration silently.</b> The
    /// API distinguishes the two on purpose: <c>null</c> is "not asking", <c>[]</c> is "clear it". An update is a
    /// FULL REPLACE everywhere else on this record, so before the form drew this section, sending <c>[]</c> would
    /// have wiped a dictionary on every save from this screen — a 302 and a success message, exactly like the
    /// <c>GroupDocumentsText</c> defect found live on 2026-08-26.</para>
    ///
    /// <para>So the answer comes from the FORM, not from the list being empty. The section posts a hidden marker
    /// when it renders; without it this returns null and the stored dictionary survives untouched. With it, the
    /// posted rows are the truth — including none of them, which is a real instruction to clear.</para>
    ///
    /// <para>Rows with no code are dropped rather than refused: a repeater's freshly added, never-filled row is a
    /// half-typed thought, not a validation failure. The server refuses a code it cannot accept.</para>
    /// </summary>
    private static object? ClosureOutcomesPayload(TaskTypeEditViewModel model)
    {
        if (!model.ClosureOutcomesSubmitted)
        {
            return null;
        }

        return (model.ClosureOutcomes ?? [])
            .Where(outcome => !string.IsNullOrWhiteSpace(outcome.Code))
            .Select((outcome, index) => new
            {
                code = outcome.Code,
                /*
                 * EXACTLY ONE LABEL SOURCE. A system row carries the resource key and no text; a tenant row
                 * carries text and no key. The server refuses both at once (OutcomeLabelAmbiguousMessage), so a
                 * row that somehow carried both is normalised HERE to the system half — the key is the stronger
                 * claim, because it is bound to a translation this product ships.
                 */
                labelResourceKey = Nullable(outcome.LabelResourceKey),
                labelText = string.IsNullOrWhiteSpace(outcome.LabelResourceKey)
                    ? Nullable(outcome.LabelText)
                    : null,
                disposition = outcome.Disposition,
                // ⭐ Read off THIS row. A global flag cannot express "Rejected asks why, Approved does not".
                requiresReason = outcome.RequiresReason,
                /*
                 * Fall back to the ROW'S POSITION when no explicit order was typed. The alternative — every row
                 * at 0 — makes the server's tie-breaker (code, alphabetical) the real order, so a list an
                 * administrator arranged deliberately would come back rearranged.
                 */
                sortOrder = outcome.SortOrder == 0 ? (index + 1) * 10 : outcome.SortOrder
            })
            .ToList();
    }

    /// <summary>One UID per line — the document PICKER is slice 2, and there is nothing to pick from yet.</summary>
    private static string[] SplitDocuments(string? text) => string.IsNullOrWhiteSpace(text)
        ? []
        : text.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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

    /*
     * ── THE RULE, IN THE READER'S LANGUAGE (DCP-005 slice 1) ──────────────────────────────────────────
     *
     * MEASURED live: tampering with the read-only code field and submitting produced the server's refusal
     * verbatim — "A task type's code cannot be changed after it is created…" — in English, on a Turkish form.
     * The refusal was CORRECT; the sentence was not translatable.
     *
     * The service keeps its English message (a service holding seven translations of a rule is a second place
     * for the rule to live) and adds a STABLE CODE. This maps the code; an unmapped one falls through to the
     * server's own words rather than to silence, so a new rule is visibly untranslated instead of invisible.
     */
    private static readonly Dictionary<string, string> ReasonCodeMessages = new(StringComparer.Ordinal)
    {
        ["TASK_TYPE_CODE_IMMUTABLE"] = "ErrorCodeImmutable",
        ["TASK_TYPE_CODE_TAKEN"] = "ErrorCodeTaken",
        ["TASK_TYPE_CLASSIFICATION_INVALID"] = "ErrorClassificationInvalid",
        ["TASK_TYPE_FUNCTION_CODE_INVALID"] = "ErrorFunctionCodeInvalid"
    };

    private async Task<List<string>> ExtractGatewayErrorsAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return [_sharedLocalizer["Unauthorized"].Value];

        try
        {
            var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<object>>(_jsonOptions);
            if (payload?.ReasonCode is { } code && ReasonCodeMessages.TryGetValue(code, out var key))
                return [_localizer[key].Value];

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

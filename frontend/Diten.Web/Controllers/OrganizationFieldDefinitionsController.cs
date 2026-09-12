using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Diten.Web.Models.OrganizationFieldDefinitions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

/// <summary>
/// MOD-0288-FU04 — the Organization field-definition authoring surface.
///
/// <para>Shaped after <see cref="TaskFieldDefinitionsController"/>: list → full-page create/edit → details,
/// server-rendered forms, gateway plumbing and error handling taken from the precedent rather than
/// re-invented. ⚠ Copied, never shared — no file, base class or helper is common to the two, because a shared
/// authoring screen ties two modules' release cycles together (FU02 §7).</para>
///
/// <para>Route lives under <c>Organization/</c> beside the units these fields describe.</para>
/// </summary>
[Authorize]
[Route("Organization/FieldDefinitions")]
public sealed class OrganizationFieldDefinitionsController : Controller
{
    // FU02 §14 verbatim. Not re-invented, and not widened: `read` sees, `manage` changes.
    public const string ReadPermission = "platform.organization-units.custom-fields.read";
    public const string ManagePermission = "platform.organization-units.custom-fields.manage";

    /// <summary>FU02 <c>OrganizationFieldDefinitionRules.MaxActiveDefinitions</c>, mirrored for the UI gate.</summary>
    public const int MaxActiveDefinitions = 50;

    private const string ApiPath = "/api/platform/organization-units/field-definitions";
    private const string ViewRoot = "~/Views/Organization/FieldDefinitions";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly Diten.Web.Services.IPermissionSnapshot _permissions;
    private readonly ILogger<OrganizationFieldDefinitionsController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public OrganizationFieldDefinitionsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        Diten.Web.Services.IPermissionSnapshot permissions,
        ILogger<OrganizationFieldDefinitionsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _permissions = permissions;
        _logger = logger;
    }

    private bool CanManage => _permissions.Has(ManagePermission);

    // ── Pages ────────────────────────────────────────────────────────────────────────────────────────────

    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["CanManage"] = CanManage;
        return View($"{ViewRoot}/Index.cshtml");
    }

    /// <summary>
    /// ⚠ THE LIMIT AND THE PERMISSION ARE CHECKED HERE TOO, NOT ONLY ON THE BUTTON. The list hides the create
    /// action at 50 and without `manage`, and a hidden button is not a closed door: this route is reachable by
    /// typing it. Both refusals redirect with the reason rather than rendering a form that cannot save.
    /// </summary>
    [HttpGet("Create")]
    public async Task<IActionResult> Create()
    {
        if (!CanManage)
        {
            return Forbid();
        }

        if (await ActiveDefinitionCountAsync() >= MaxActiveDefinitions)
        {
            TempData["ErrorMessage"] = DefinitionLimitMessage();
            return RedirectToAction(nameof(Index));
        }

        ViewData["FormMode"] = "create";
        ViewData["CanManage"] = true;
        return View($"{ViewRoot}/Create.cshtml", new OrganizationFieldDefinitionEditViewModel());
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] OrganizationFieldDefinitionEditViewModel model)
    {
        if (!CanManage)
        {
            return Forbid();
        }

        ViewData["FormMode"] = "create";
        ViewData["CanManage"] = true;
        model.Options = CleanOptions(model.Options);

        if (!ModelState.IsValid)
        {
            return View($"{ViewRoot}/Create.cshtml", model);
        }

        if (await ActiveDefinitionCountAsync() >= MaxActiveDefinitions)
        {
            ModelState.AddModelError(string.Empty, DefinitionLimitMessage());
            return View($"{ViewRoot}/Create.cshtml", model);
        }

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
            _logger.LogError(ex, "Organization field definition create failed.");
            AddGatewayErrorsToModelState(BuildExceptionErrors(ex));
        }

        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpGet("Edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        if (!CanManage)
        {
            return Forbid();
        }

        var model = await LoadEditModelAsync(id);
        if (model is null)
        {
            TempData["ErrorMessage"] = _sharedLocalizer["GatewayError"].Value;
            return RedirectToAction(nameof(Index));
        }

        ViewData["FormMode"] = "edit";
        ViewData["CanManage"] = true;
        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    [HttpPost("Edit/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, [FromForm] OrganizationFieldDefinitionEditViewModel model)
    {
        if (!CanManage)
        {
            return Forbid();
        }

        model.Id = id;
        ViewData["FormMode"] = "edit";
        ViewData["CanManage"] = true;
        model.Options = CleanOptions(model.Options);

        if (!ModelState.IsValid)
        {
            return View($"{ViewRoot}/Edit.cshtml", model);
        }

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
            _logger.LogError(ex, "Organization field definition edit failed for {DefinitionId}.", id);
            AddGatewayErrorsToModelState(BuildExceptionErrors(ex));
        }

        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    [HttpGet("Details/{id:guid}")]
    public async Task<IActionResult> Details(Guid id)
    {
        var model = await LoadEditModelAsync(id);
        if (model is null)
        {
            TempData["ErrorMessage"] = _sharedLocalizer["GatewayError"].Value;
            return RedirectToAction(nameof(Index));
        }

        ViewData["FormMode"] = "details";
        ViewData["CanManage"] = CanManage;
        return View($"{ViewRoot}/Details.cshtml", model);
    }

    // ── JSON the DataTable and the row actions call (same-origin proxy) ───────────────────────────────────

    [HttpGet("api")]
    public Task<IActionResult> ListProxy()
    {
        // includeInactive: the list shows deactivated definitions too — they are the ones whose stored values
        // stay readable, so hiding them would hide the explanation for values already on screen.
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}{ApiPath}?includeInactive=true");
    }

    /// <summary>
    /// Deactivate one definition. ⚠ Deactivation is the only lifecycle FU02 offers — there is no delete and no
    /// re-activate — so this screen never presents either.
    /// </summary>
    [HttpPost("api/{id:guid}/deactivate")]
    public Task<IActionResult> DeactivateProxy(Guid id, [FromQuery] int expectedVersion)
    {
        if (!CanManage)
        {
            return Task.FromResult<IActionResult>(Forbid());
        }

        return ProxyGatewayAsync(
            HttpMethod.Post,
            $"{_gatewayUrl}{ApiPath}/{id}/deactivate?expectedVersion={expectedVersion}");
    }

    /// <summary>
    /// The bulk surface the shared DataTable contract requires.
    ///
    /// <para>⚠ IT DEACTIVATES; IT DOES NOT DELETE. FU02 has no delete endpoint for a definition, and inventing
    /// one on this side would mean the button's name was the only thing that ever deleted anything. The
    /// backend takes one definition at a time with its own <c>expectedVersion</c>, so this loops and reports
    /// per id — a batch that silently dropped a stale row would be worse than one that says which failed.</para>
    /// </summary>
    [HttpPost("api/bulk")]
    public async Task<IActionResult> BulkProxy([FromBody] OrganizationFieldDefinitionBulkRequest request)
    {
        if (!CanManage)
        {
            return Forbid();
        }

        if (request?.Items is not { Count: > 0 })
        {
            return BadRequest(new { errors = new[] { "No field definitions were selected." } });
        }

        if (!AddAuthHeaders())
        {
            return Unauthorized(new { errors = new[] { _sharedLocalizer["Unauthorized"].Value } });
        }

        var failed = new List<string>();
        foreach (var item in request.Items)
        {
            try
            {
                var response = await _httpClient.PostAsync(
                    $"{_gatewayUrl}{ApiPath}/{item.Id}/deactivate?expectedVersion={item.ExpectedVersion}",
                    content: null);
                if (!response.IsSuccessStatusCode)
                {
                    failed.Add(item.Id.ToString());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bulk deactivate failed for {DefinitionId}.", item.Id);
                failed.Add(item.Id.ToString());
            }
        }

        return failed.Count == 0
            ? Ok(new { isSuccessful = true })
            : StatusCode(StatusCodes.Status409Conflict, new { errors = failed.Select(id => $"Could not deactivate {id}.") });
    }

    // ── Loading ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// ⚠ THERE IS NO GET-BY-ID ON THE BACKEND. FU02 exposes the definition list and nothing narrower, so an
    /// edit or details page reads the list and picks its row. Stated rather than hidden: if FU02 later adds a
    /// by-id route this is the one place to change.
    /// </summary>
    private async Task<OrganizationFieldDefinitionEditViewModel?> LoadEditModelAsync(Guid id)
    {
        var definition = (await LoadDefinitionsAsync()).FirstOrDefault(d => d.Id == id);
        if (definition is null)
        {
            return null;
        }

        return new OrganizationFieldDefinitionEditViewModel
        {
            Id = definition.Id,
            Code = definition.Code,
            OriginalCode = definition.Code,
            Name = definition.Name,
            DataType = definition.DataType,
            IsRequired = definition.IsRequired,
            IsQueryable = definition.IsQueryable,
            Classification = definition.Classification,
            DisplayOrder = definition.DisplayOrder,
            IsActive = definition.IsActive,
            ExpectedVersion = definition.Version,
            MinLength = definition.ValidationRules?.MinLength,
            MaxLength = definition.ValidationRules?.MaxLength,
            MinValue = definition.ValidationRules?.MinValue,
            MaxValue = definition.ValidationRules?.MaxValue,
            ReferenceTarget = definition.ValidationRules?.ReferenceTarget,
            Options = definition.ValidationRules?.Options ?? []
        };
    }

    private async Task<List<OrganizationFieldDefinitionListItemViewModel>> LoadDefinitionsAsync()
    {
        if (!AddAuthHeaders())
        {
            return [];
        }

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}{ApiPath}?includeInactive=true");
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var payload = await response.Content
                .ReadFromJsonAsync<OrganizationFieldDefinitionGatewayResponse<List<OrganizationFieldDefinitionListItemViewModel>>>(_jsonOptions);
            return payload?.Data ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Organization field definitions could not be read.");
            return [];
        }
    }

    private async Task<int> ActiveDefinitionCountAsync() =>
        (await LoadDefinitionsAsync()).Count(d => d.IsActive);

    private string DefinitionLimitMessage() =>
        $"The limit of {MaxActiveDefinitions} definitions has been reached.";

    // ── Payloads ─────────────────────────────────────────────────────────────────────────────────────────

    private static object ToCreatePayload(OrganizationFieldDefinitionEditViewModel model) => new
    {
        code = model.Code,
        name = model.Name,
        dataType = model.DataType,
        isRequired = model.IsRequired,
        isQueryable = model.IsQueryable,
        displayOrder = model.DisplayOrder ?? 0,
        classification = model.Classification,
        validationRules = ToConstraints(model)
    };

    /// <summary>
    /// ⚠ NO <c>code</c> AND NO <c>isActive</c>. The FU02 update request carries neither: the code is immutable
    /// and activity changes only through the deactivate route. Sending either would be a value the server
    /// discards while the screen reports a successful save.
    /// </summary>
    private static object ToUpdatePayload(OrganizationFieldDefinitionEditViewModel model) => new
    {
        name = model.Name,
        dataType = model.DataType,
        isRequired = model.IsRequired,
        isQueryable = model.IsQueryable,
        displayOrder = model.DisplayOrder ?? 0,
        expectedVersion = model.ExpectedVersion,
        classification = model.Classification,
        validationRules = ToConstraints(model)
    };

    /// <summary>
    /// Only the constraints the declared type can carry travel. A max length on a date, or options on a
    /// number, are refused by FU02's <c>ValidateConstraints</c>; dropping them here means the user is not
    /// refused for a value the form let them leave behind after changing the type.
    /// </summary>
    private static object? ToConstraints(OrganizationFieldDefinitionEditViewModel model)
    {
        var options = CleanOptions(model.Options);
        return model.DataType switch
        {
            "Text" or "MultilineText" => new { minLength = model.MinLength, maxLength = model.MaxLength },
            "Integer" or "Decimal" => new { minValue = model.MinValue, maxValue = model.MaxValue },
            "SingleSelect" => new { options },
            "Reference" => new { referenceTarget = model.ReferenceTarget },
            _ => null
        };
    }

    private static List<string> CleanOptions(IEnumerable<string>? options) =>
        (options ?? [])
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Select(o => o.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    // ── Gateway plumbing (shape taken from the Task precedent) ────────────────────────────────────────────

    private async Task<IActionResult> ProxyGatewayAsync(HttpMethod method, string targetUrl)
    {
        if (!AddAuthHeaders())
        {
            return Unauthorized(new { errors = new[] { _sharedLocalizer["Unauthorized"].Value } });
        }

        using var request = new HttpRequestMessage(method, targetUrl);
        var response = await _httpClient.SendAsync(request);
        if (ProxyAuthFailure.IsAuthFailure(response.StatusCode))
        {
            ProxyAuthFailure.ClearAuthCookies(Response);
            return StatusCode((int)response.StatusCode, ProxyAuthFailure.PlatformLoginPayload());
        }

        var content = await response.Content.ReadAsStringAsync();
        return new ContentResult
        {
            Content = content,
            ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
            StatusCode = (int)response.StatusCode
        };
    }

    private void AddGatewayErrorsToModelState(IEnumerable<string> errors)
    {
        foreach (var error in errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }
    }

    private List<string> BuildExceptionErrors(Exception ex)
    {
        var message = ex.GetBaseException().Message;
        return [string.IsNullOrWhiteSpace(message) ? _sharedLocalizer["GatewayError"].Value : message];
    }

    private async Task<List<string>> ExtractGatewayErrorsAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return [_sharedLocalizer["Unauthorized"].Value];
        }

        try
        {
            var payload = await response.Content
                .ReadFromJsonAsync<OrganizationFieldDefinitionGatewayResponse<object>>(_jsonOptions);
            var errors = payload?.Errors?.Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
            if (errors?.Count > 0)
            {
                return errors;
            }
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
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (_httpClient.DefaultRequestHeaders.Contains("X-Tenant-Id"))
        {
            _httpClient.DefaultRequestHeaders.Remove("X-Tenant-Id");
        }

        var tenantId = GetTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return false;
        }

        _httpClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        return true;
    }

    private string? GetTenantId() =>
        User.Claims.FirstOrDefault(x =>
            x.Type == "tenantId" ||
            x.Type == "tenant_id" ||
            x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;
}

/// <summary>One bulk deactivation request: ids with the versions they were read at.</summary>
public sealed class OrganizationFieldDefinitionBulkRequest
{
    public List<OrganizationFieldDefinitionBulkItem> Items { get; set; } = [];
}

public sealed class OrganizationFieldDefinitionBulkItem
{
    public Guid Id { get; set; }
    public int ExpectedVersion { get; set; }
}

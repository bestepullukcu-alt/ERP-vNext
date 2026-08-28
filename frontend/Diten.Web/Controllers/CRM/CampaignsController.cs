using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Diten.Web.Models.CRM;
using Diten.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// MOD-0165-FU05 Campaign / Targeting Admin UI. All business traffic is proxied server-side through Gateway 5000;
/// the browser never sees a service URL or bearer token. FU04 remains the authoritative validation and permission layer.
/// </summary>
[Authorize]
[Route("CRM/Campaigns")]
public sealed class CampaignsController : Controller
{
    private const string ReadPermission = "crm.campaign.read";
    private const string ManagePermission = "crm.campaign.manage";
    private const string TargetReadPermission = "crm.campaign.target.read";
    private const string TargetManagePermission = "crm.campaign.target.manage";
    private const string SnapshotPermission = "crm.campaign.snapshot.create";
    private const string ReadFallback = "crm.territory.read";
    private const string ManageFallback = "crm.territory.model.manage";
    private const string ViewRoot = "~/Views/CRM/Campaigns";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<CampaignsController> _logger;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public CampaignsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<CampaignsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index() => RequirePage(ReadPermission, ReadFallback) ?? View($"{ViewRoot}/Index.cshtml");

    [HttpGet("Create")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied) return denied;
        var model = new CampaignEditViewModel { StartDate = DateTimeOffset.Now };
        await PopulateContractOptionsAsync(model, cancellationToken);
        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CampaignEditViewModel model, CancellationToken cancellationToken)
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied) return denied;
        if (!ModelState.IsValid)
        {
            await PopulateContractOptionsAsync(model, cancellationToken);
            return View($"{ViewRoot}/Create.cshtml", model);
        }

        var payload = ToCreatePayload(model);
        var response = await SendGatewayAsync(HttpMethod.Post, "/api/crm/campaigns", payload, cancellationToken);
        if (response is not null && response.IsSuccessStatusCode)
        {
            var envelope = await response.Content.ReadFromJsonAsync<CampaignGatewayResponse<Guid>>(_json, cancellationToken);
            TempData["SuccessMessage"] = _sharedLocalizer["RecordCreated"].Value;
            return envelope?.Data is { } id && id != Guid.Empty
                ? RedirectToAction(nameof(Details), new { id })
                : RedirectToAction(nameof(Index));
        }

        AddGatewayErrors(await ExtractErrorsAsync(response, cancellationToken));
        await PopulateContractOptionsAsync(model, cancellationToken);
        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpGet("Edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied) return denied;
        var campaign = await LoadCampaignAsync(id, cancellationToken);
        if (campaign is null) return NotFound();
        if (campaign.IsArchived)
        {
            TempData["WarningMessage"] = "ArchivedCampaignReadOnly";
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = ToEditModel(campaign);
        await PopulateContractOptionsAsync(model, cancellationToken);
        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    [HttpPost("Edit/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, CampaignEditViewModel model, CancellationToken cancellationToken)
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied) return denied;
        model.CampaignId = id;
        if (!ModelState.IsValid)
        {
            await PopulateContractOptionsAsync(model, cancellationToken);
            return View($"{ViewRoot}/Edit.cshtml", model);
        }

        var response = await SendGatewayAsync(HttpMethod.Put, $"/api/crm/campaigns/{id}", ToUpdatePayload(model), cancellationToken);
        if (response is not null && response.IsSuccessStatusCode)
        {
            TempData["SuccessMessage"] = _sharedLocalizer["RecordUpdated"].Value;
            return RedirectToAction(nameof(Details), new { id });
        }

        AddGatewayErrors(await ExtractErrorsAsync(response, cancellationToken));
        await PopulateContractOptionsAsync(model, cancellationToken);
        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    /// <summary>
    /// MOD-0165 FU10 — the targeting page. READ-ONLY by construction: it renders what the campaign targets and
    /// writes nothing, so there is no POST counterpart to this action.
    /// </summary>
    [HttpGet("{id:guid}/Targeting")]
    public async Task<IActionResult> Targeting(Guid id, CancellationToken cancellationToken)
    {
        if (RequirePage(ReadPermission, ReadFallback) is { } denied) return denied;
        var campaign = await LoadCampaignAsync(id, cancellationToken);
        if (campaign is null) return NotFound();

        var targets = campaign.TargetingMode == "manual"
            ? await LoadTargetsAsync(id, cancellationToken)
            : new List<CampaignTargetViewModel>();

        return View($"{ViewRoot}/Targeting.cshtml", new CampaignTargetingPageViewModel
        {
            Campaign = campaign,
            ManualTargets = targets,
            // The PASSIVE mode's data is counted, not hidden: dormant is not the same as gone, and a reader who
            // cannot see it has no way to know it is there.
            DormantSegmentCount = campaign.TargetingMode == "manual" ? campaign.TargetedSegments.Count : 0,
            DormantManualTargetCount = campaign.TargetingMode == "segment"
                ? (await LoadTargetsAsync(id, cancellationToken)).Count
                : 0
        });
    }

    private async Task<List<CampaignTargetViewModel>> LoadTargetsAsync(Guid campaignId, CancellationToken ct)
    {
        var response = await SendGatewayAsync(
            HttpMethod.Get, $"/api/crm/campaigns/{campaignId}/targets", null, ct);
        if (response is null || !response.IsSuccessStatusCode) return [];
        var envelope = await response.Content
            .ReadFromJsonAsync<CampaignGatewayResponse<CampaignTargetListViewModel>>(_json, ct);
        return envelope?.Data?.Items ?? [];
    }

    [HttpGet("Details/{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        if (RequirePage(ReadPermission, ReadFallback) is { } denied) return denied;
        var campaign = await LoadCampaignAsync(id, cancellationToken);
        if (campaign is null) return NotFound();

        var contract = await LoadContractAsync(cancellationToken) ?? new CampaignContractViewModel();
        var model = new CampaignPageViewModel
        {
            Campaign = campaign,
            Contract = contract,
            CanManageCampaign = HasAnyPermission(ManagePermission, ManageFallback) && contract.Features.SupportsCampaignManagement,
            CanReadTargets = HasAnyPermission(TargetReadPermission, ReadPermission, ReadFallback)
                && contract.Features.SupportsCampaignTargetManagement,
            CanManageTargets = !campaign.IsArchived
                && HasAnyPermission(TargetManagePermission, ManagePermission, ManageFallback)
                && contract.Features.SupportsCampaignTargetManagement,
            CanCreateSnapshot = !campaign.IsArchived
                && HasAnyPermission(SnapshotPermission, TargetManagePermission, ManagePermission, ManageFallback)
                && contract.Features.SupportsStaticTargetSnapshot
        };
        return View($"{ViewRoot}/Details.cshtml", model);
    }

    // Same-origin browser proxy. Only the FU04 allowlist below can be reached.

    [HttpGet("api/contract")]
    public Task<IActionResult> Contract(CancellationToken ct) => ProxyGetAsync("/api/crm/campaigns/contract", ReadPermission, ct, ReadFallback);

    [HttpGet("api")]
    public Task<IActionResult> List(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/campaigns{Request.QueryString}", ReadPermission, ct, ReadFallback);

    /// <summary>
    /// MOD-0165 FU08 — the cycle-period picker's option source, proxied so the browser never touches a sibling
    /// module's proxy or a Gateway URL. It is READ-ONLY and forwards only the two filters the picker uses; the
    /// MOD-0165 FU06/FU07 surface itself is untouched by this feature.
    /// <para>Permission is the CAMPAIGN read gate, because that is the page the caller is on. Whether they may read
    /// periods at all is still decided downstream by the cycle-period endpoint's own guard: if they may not, the list
    /// comes back empty and no binding can be authored — fail-closed, not fail-open.</para>
    /// </summary>
    [HttpGet("api/cycle-periods")]
    public Task<IActionResult> CyclePeriods([FromQuery] string? cycleStatus, [FromQuery] int? year, CancellationToken ct)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(cycleStatus)) query.Add($"cycleStatus={Uri.EscapeDataString(cycleStatus.Trim())}");
        if (year is { } y) query.Add($"year={y}");
        var suffix = query.Count == 0 ? string.Empty : "?" + string.Join("&", query);
        return ProxyGetAsync($"/api/crm/cycle-periods/selector{suffix}", ReadPermission, ct, ReadFallback);
    }

    /// <summary>
    /// MOD-0165 FU09 — the cascading scope selector's option source, proxied so the browser never touches a Gateway
    /// URL. READ-ONLY.
    /// </summary>
    [HttpGet("api/scope-options")]
    public Task<IActionResult> ScopeOptions(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/campaigns/scope-options{Request.QueryString}", ReadPermission, ct, ReadFallback);

    /// <summary>
    /// MOD-0165 FU09 — the cycle periods applicable to the scope currently being edited. The applicability rule lives
    /// on the server, so the picker and the write-path guard answer from ONE rule and a direct API call cannot walk
    /// past it. This supersedes the FU08 <c>api/cycle-periods</c> passthrough for the form.
    /// </summary>
    [HttpGet("api/applicable-cycle-periods")]
    public Task<IActionResult> ApplicableCyclePeriods(CancellationToken ct) =>
        ProxyGetAsync(
            $"/api/crm/campaigns/applicable-cycle-periods{Request.QueryString}", ReadPermission, ct, ReadFallback);

    /// <summary>
    /// MOD-0165 FU10 — the segment picker's option source, proxied so the browser never touches a sibling module's
    /// proxy or a Gateway URL. READ-ONLY, and it forwards only the status filter the picker uses.
    /// </summary>
    [HttpGet("api/segments")]
    public Task<IActionResult> Segments([FromQuery] string? segmentStatus, CancellationToken ct)
    {
        var status = string.IsNullOrWhiteSpace(segmentStatus) ? "active" : segmentStatus.Trim();
        return ProxyGetAsync(
            $"/api/crm/segments?segmentStatus={Uri.EscapeDataString(status)}&includeArchived=false",
            ReadPermission, ct, ReadFallback);
    }

    /// <summary>
    /// MOD-0165 FU11 — the manual target picker's account source. A straight passthrough of the existing MOD-0149 list,
    /// mirroring the Segments proxy so the browser never touches a sibling module's proxy or a Gateway URL.
    /// <para>READ-ONLY. The account master is never read into the target and never written: the picker hands over an
    /// id, and the name it shows is stored as a display LABEL only.</para>
    /// </summary>
    [HttpGet("api/accounts")]
    public Task<IActionResult> Accounts(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/accounts{Request.QueryString}", "crm.account.read", ct, ReadFallback);

    /// <summary>
    /// MOD-0165 FU11 — the manual target picker's contact source. Same passthrough, same rules as
    /// <see cref="Accounts"/>: read-only, id passed through untouched, name stored as a label.
    /// </summary>
    [HttpGet("api/contacts")]
    public Task<IActionResult> Contacts(CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/contacts{Request.QueryString}", "crm.contact.read", ct, ReadFallback);

    /// <summary>
    /// MOD-0165 FU11 — the create form's CampaignCode placeholder source. READ-ONLY and NON-COMMITTING: it asks what
    /// the next auto-assigned code would be without consuming the sequence, so opening the form still costs nothing.
    /// <para>The value is shown as a placeholder only. The field is posted EMPTY and the server assigns the real code
    /// at save, which is why a peek that has gone stale cannot produce a duplicate.</para>
    /// </summary>
    [HttpGet("api/next-code")]
    public Task<IActionResult> NextCode(CancellationToken ct) =>
        ProxyGetAsync("/api/crm/campaigns/next-code", ReadPermission, ct, ReadFallback);

    [HttpPost("api/{campaignId:guid}/archive")]
    public Task<IActionResult> Archive(Guid campaignId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"/api/crm/campaigns/{campaignId}/archive", null, ManagePermission, ct, ManageFallback);

    [HttpGet("api/{campaignId:guid}/targets")]
    public Task<IActionResult> Targets(Guid campaignId, CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/campaigns/{campaignId}/targets{Request.QueryString}", TargetReadPermission, ct, ReadPermission, ReadFallback);

    [HttpGet("api/{campaignId:guid}/targets/{campaignTargetId:guid}")]
    public Task<IActionResult> Target(Guid campaignId, Guid campaignTargetId, CancellationToken ct) =>
        ProxyGetAsync($"/api/crm/campaigns/{campaignId}/targets/{campaignTargetId}", TargetReadPermission, ct, ReadPermission, ReadFallback);

    [HttpPost("api/{campaignId:guid}/targets")]
    public Task<IActionResult> CreateTarget(Guid campaignId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"/api/crm/campaigns/{campaignId}/targets", body, TargetManagePermission, ct, ManagePermission, ManageFallback);

    [HttpPut("api/{campaignId:guid}/targets/{campaignTargetId:guid}")]
    public Task<IActionResult> UpdateTarget(Guid campaignId, Guid campaignTargetId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Put, $"/api/crm/campaigns/{campaignId}/targets/{campaignTargetId}", body, TargetManagePermission, ct, ManagePermission, ManageFallback);

    [HttpPost("api/{campaignId:guid}/targets/{campaignTargetId:guid}/archive")]
    public Task<IActionResult> ArchiveTarget(Guid campaignId, Guid campaignTargetId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"/api/crm/campaigns/{campaignId}/targets/{campaignTargetId}/archive", null, TargetManagePermission, ct, ManagePermission, ManageFallback);

    [HttpPost("api/{campaignId:guid}/snapshot")]
    public Task<IActionResult> Snapshot(Guid campaignId, [FromBody] JsonElement body, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"/api/crm/campaigns/{campaignId}/targets/snapshot", body, SnapshotPermission, ct, TargetManagePermission, ManagePermission, ManageFallback);

    private async Task PopulateContractOptionsAsync(CampaignEditViewModel model, CancellationToken ct)
    {
        var contract = await LoadContractAsync(ct);
        if (contract is null || !contract.IsReady || !contract.Features.SupportsCampaignManagement)
        {
            model.ContractError = "CampaignContractUnavailable";
            return;
        }

        model.CampaignTypes = contract.Vocabulary.CampaignTypes;
        model.CampaignStatuses = contract.Vocabulary.CampaignStatuses;
        model.ObjectiveTypes = contract.Vocabulary.ObjectiveTypes;
        model.ConsentChannels = contract.Vocabulary.ConsentChannels;
        model.ConsentPurposes = contract.Vocabulary.ConsentPurposes;

        // FU08 / AC-UI-3 — re-resolve the bound period whenever the form is (re)rendered. On a validation redisplay
        // the projection is gone (only the id is posted), and without this the picker would lose a CLOSED selection
        // and the next save would silently unbind the campaign.
        if (model.CurrentCyclePeriod is null && model.CyclePeriodId is { } cyclePeriodId && cyclePeriodId != Guid.Empty)
        {
            model.CurrentCyclePeriod = await LoadCyclePeriodAsync(cyclePeriodId, ct);
        }
    }

    /// <summary>FU08 — one period by id, READ-ONLY, for the picker's current-value injection. Never writes.</summary>
    private async Task<CampaignCyclePeriodViewModel?> LoadCyclePeriodAsync(Guid cyclePeriodId, CancellationToken ct)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, $"/api/crm/cycle-periods/{cyclePeriodId}", null, ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        return (await response.Content
            .ReadFromJsonAsync<CampaignGatewayResponse<CampaignCyclePeriodViewModel>>(_json, ct))?.Data;
    }

    private async Task<CampaignContractViewModel?> LoadContractAsync(CancellationToken ct)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, "/api/crm/campaigns/contract", null, ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        return (await response.Content.ReadFromJsonAsync<CampaignGatewayResponse<CampaignContractViewModel>>(_json, ct))?.Data;
    }

    private async Task<CampaignDetailViewModel?> LoadCampaignAsync(Guid id, CancellationToken ct)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, $"/api/crm/campaigns/{id}", null, ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        return (await response.Content.ReadFromJsonAsync<CampaignGatewayResponse<CampaignDetailViewModel>>(_json, ct))?.Data;
    }

    private async Task<IActionResult> ProxyGetAsync(string path, string permission, CancellationToken ct, params string[] fallbacks)
    {
        if (RequireJson(permission, fallbacks) is { } denied) return denied;
        var response = await SendGatewayAsync(HttpMethod.Get, path, null, ct);
        return await ToProxyResultAsync(response, ct);
    }

    private async Task<IActionResult> ProxyJsonAsync(
        HttpMethod method, string path, JsonElement? body, string permission, CancellationToken ct, params string[] fallbacks)
    {
        if (RequireJson(permission, fallbacks) is { } denied) return denied;
        if (body.HasValue && ContainsTenantId(body.Value))
            return BadRequest(new { errors = new[] { "TenantId is server-resolved and must not be supplied." } });

        var response = await SendGatewayAsync(method, path, body, ct);
        return await ToProxyResultAsync(response, ct);
    }

    private async Task<HttpResponseMessage?> SendGatewayAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(method, $"{_gatewayUrl}{path}");
            var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var tenantId = GetTenantId();
            if (string.IsNullOrWhiteSpace(tenantId)) return null;
            request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId);

            if (body is not null)
            {
                var json = body is JsonElement element ? element.GetRawText() : JsonSerializer.Serialize(body, _json);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            return await _httpClient.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Campaign Gateway request failed: {Method} {Path}", method, path);
            return null;
        }
    }

    private static async Task<IActionResult> ToProxyResultAsync(HttpResponseMessage? response, CancellationToken ct)
    {
        if (response is null)
            return new ObjectResult(new { errors = new[] { "Gateway unavailable." } }) { StatusCode = 502 };
        var content = await response.Content.ReadAsStringAsync(ct);
        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
            Content = content
        };
    }

    private async Task<List<string>> ExtractErrorsAsync(HttpResponseMessage? response, CancellationToken ct)
    {
        if (response is null) return [_sharedLocalizer["GatewayError"].Value];
        try
        {
            var envelope = await response.Content.ReadFromJsonAsync<CampaignGatewayResponse<object>>(_json, ct);
            if (envelope?.Errors.Count > 0) return envelope.Errors;
        }
        catch { }
        var raw = await response.Content.ReadAsStringAsync(ct);
        return [string.IsNullOrWhiteSpace(raw) ? _sharedLocalizer["GatewayError"].Value : raw];
    }

    private void AddGatewayErrors(IEnumerable<string> errors)
    {
        foreach (var error in errors) ModelState.AddModelError(string.Empty, error);
    }

    private static object ToCreatePayload(CampaignEditViewModel m) => new
    {
        m.CampaignCode, m.CampaignName, m.CampaignType, StartDate = PickedDayToUtc(m.StartDate), m.CampaignStatus, m.ObjectiveType,
        m.BusinessUnitId,
        m.DefaultConsentChannel, m.DefaultConsentPurpose, EndDate = PickedDayToUtc(m.EndDate), m.Description,
        m.CyclePeriodId, m.ScopeType, m.CountryScope, m.LegalEntityId,
        m.TargetingMode, TargetedSegmentIds = m.TargetedSegmentIds
    };

    private static object ToUpdatePayload(CampaignEditViewModel m) => new
    {
        m.CampaignName, m.CampaignType, StartDate = PickedDayToUtc(m.StartDate), m.CampaignStatus, m.ObjectiveType, m.BusinessUnitId, m.DefaultConsentChannel, m.DefaultConsentPurpose,
        EndDate = PickedDayToUtc(m.EndDate), m.Description, m.CyclePeriodId, m.ScopeType, m.CountryScope, m.LegalEntityId,
    };

    private static CampaignEditViewModel ToEditModel(CampaignDetailViewModel c) => new()
    {
        CampaignId = c.CampaignId, CampaignCode = c.CampaignCode, CampaignName = c.CampaignName,
        CampaignType = c.CampaignType, CampaignStatus = c.CampaignStatus, ObjectiveType = c.ObjectiveType,
        BusinessUnitId = c.BusinessUnitId, DefaultConsentChannel = c.DefaultConsentChannel,
        DefaultConsentPurpose = c.DefaultConsentPurpose,
        StartDate = StoredDayToUtc(c.StartDate), EndDate = StoredDayToUtc(c.EndDate),
        Description = c.Description,
        // FU08 — carry BOTH the binding and the projection: the projection keeps a closed period selectable in the
        // picker (AC-UI-3), the binding is what actually gets posted.
        CyclePeriodId = c.CyclePeriodId, CurrentCyclePeriod = c.CyclePeriod,
        // FU09 - the effective address comes back from the API, so a pre-FU09 campaign opens on the scope it always
        // had rather than on an empty selector.
        ScopeType = c.ScopeType, CountryScope = c.CountryScope, LegalEntityId = c.LegalEntityId,
        // FU10 - the effective mode and the pinned segments come back from the API, so a pre-FU10 campaign opens on
        // the mode it always had and an archived/superseded segment survives the round trip (AC-UI-3).
        TargetingMode = c.TargetingMode,
        TargetedSegmentIds = c.TargetedSegmentIds.ToList(),
        CurrentTargetedSegments = c.TargetedSegments.ToList(),
        IsArchived = c.IsArchived
    };

    /// <summary>
    /// MOD-0165 FU10 — a day the AUTHOR picked, anchored to UTC midnight.
    /// <para>The date input hands back the chosen day in the browser's offset. On a negative offset that instant's UTC
    /// date is the previous day, so storing it raw would silently shift every campaign west of Greenwich by one day —
    /// and the containment rule compares canonical UTC days.</para>
    /// </summary>
    private static DateTimeOffset? PickedDayToUtc(DateTimeOffset? value)
        => value is { } d ? new DateTimeOffset(d.Date, TimeSpan.Zero) : null;

    /// <summary>
    /// A day the RUNTIME returned, anchored to UTC midnight. The opposite reading of <see cref="PickedDayToUtc"/> and
    /// NOT interchangeable with it: a stored instant may deserialize into any offset, and on a negative one its local
    /// date component is the previous day. The stored day is the UTC day.
    /// </summary>
    private static DateTimeOffset? StoredDayToUtc(DateTimeOffset? value)
        => value is { } d ? new DateTimeOffset(d.UtcDateTime.Date, TimeSpan.Zero) : null;

    private static bool ContainsTenantId(JsonElement element) => element.ValueKind == JsonValueKind.Object &&
        element.EnumerateObject().Any(x => string.Equals(x.Name, "tenantId", StringComparison.OrdinalIgnoreCase));

    private string? GetTenantId() => User.Claims.FirstOrDefault(x =>
        x.Type == "tenantId" || x.Type == "tenant_id" ||
        x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;

    private bool HasAnyPermission(params string[] permissions) => permissions.Any(x => PermissionClaims.HasPermission(User, x));

    private IActionResult? RequirePage(string permission, params string[] fallbacks) =>
        HasAnyPermission([permission, .. fallbacks]) ? null : StatusCode(StatusCodes.Status403Forbidden);

    private IActionResult? RequireJson(string permission, params string[] fallbacks) =>
        HasAnyPermission([permission, .. fallbacks])
            ? null
            : StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." });
}

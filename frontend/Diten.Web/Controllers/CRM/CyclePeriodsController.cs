using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Diten.Web.Models.CRM;
using Diten.Web.Security;
using Diten.Web.Views.CRM.CyclePeriods;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// MOD-0165 FU06/FU07 + WP-CYC-UI-1 Cycle Period UI. All business traffic is proxied server-side through Gateway 5000;
/// the browser never sees a service URL or a bearer token. The CrmService runtime stays the authoritative validation
/// and permission layer — nothing is decided here.
/// <para><b>WP-CYC-UI-1 surface.</b> The list page carries a table view and a year-timeline view, and create / edit
/// happen in a RIGHT-SIDE PANEL on the list (and on the details page) — the mockup's surface, chosen by the product
/// owner on 2026-10-05 as a <b>known deviation</b> from the Golden Compact rule that puts create / edit on their own
/// pages. The Create / Edit pages are gone; Details stays a page.</para>
/// <para>The screen's display rules (timeline axis and lanes, which fields a status locks, live panel warnings, the
/// effective-period finder, the open-without-capacity band) live in <see cref="CyclePeriodScreenRules"/> and are
/// served by the computed endpoints below, so the browser only renders.</para>
/// <para>There is no delete surface (ending a period is Close), no reopen surface (closed is terminal) and no
/// apply/generate surface: applying a plan to a period is MOD-0155.</para>
/// </summary>
[Authorize]
[Route("CRM/CyclePeriods")]
public sealed class CyclePeriodsController : Controller
{
    private const string ReadPermission = "crm.cycle-period.read";
    private const string ManagePermission = "crm.cycle-period.manage";
    private const string ActivatePermission = "crm.cycle-period.activate";

    /// <summary>Documented DEV-ONLY fallback until F-RBAC lands. It widens no guard: the CrmService still enforces
    /// tenant isolation, the lifecycle, the scope invariant and the overlap ban behind it.</summary>
    private const string ReadFallback = "crm.territory.read";

    private const string ManageFallback = "crm.territory.model.manage";
    private const string ViewRoot = "~/Views/CRM/CyclePeriods";

    /// <summary>The platform working calendar's range operation (the same route the CrmService capacity reads).</summary>
    private const string WorkingDaysPath = "/api/platform/working-calendars/overrides/resolve?op=working-days-between";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger<CyclePeriodsController> _logger;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public CyclePeriodsController(
        HttpClient httpClient, IConfiguration configuration, ILogger<CyclePeriodsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _logger = logger;
    }

    // ---------------- pages ----------------

    /// <summary>The list. <c>?create=1</c> / <c>?edit={id}</c> open the panel on arrival (links from the details page
    /// and from "close and open a new period").</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (RequirePage(ReadPermission, ReadFallback) is { } denied)
        {
            return denied;
        }

        return View($"{ViewRoot}/Index.cshtml", new CyclePeriodIndexViewModel
        {
            CanManage = HasAnyPermission(ManagePermission, ManageFallback),
            CanActivate = HasAnyPermission(ActivatePermission, ManagePermission, ManageFallback),
            ScopeOptions = HasAnyPermission(ManagePermission, ManageFallback)
                ? await LoadScopeOptionsAsync(null, null, null, ct)
                : new CyclePeriodScopeOptionsViewModel()
        });
    }

    [HttpGet("Details/{cyclePeriodId:guid}")]
    public async Task<IActionResult> Details(Guid cyclePeriodId, CancellationToken ct)
    {
        if (RequirePage(ReadPermission, ReadFallback) is { } denied)
        {
            return denied;
        }

        var detail = await LoadDetailAsync(cyclePeriodId, ct);
        if (detail is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var canManage = HasAnyPermission(ManagePermission, ManageFallback);
        var canActivate = HasAnyPermission(ActivatePermission, ManagePermission, ManageFallback);
        return View($"{ViewRoot}/Details.cshtml", new CyclePeriodDetailsViewModel
        {
            Period = detail,
            CanManage = canManage,
            CanActivate = canActivate,
            Actions = CyclePeriodScreenRules.Actions(detail.CycleStatus, canActivate, canManage),
            CalendarCountry = CalendarCountryOf(detail.ScopeType, detail.CountryScope, detail.BusinessUnitCountryContext),
            ScopeOptions = canManage ? await LoadScopeOptionsAsync(null, null, null, ct) : new CyclePeriodScopeOptionsViewModel()
        });
    }

    // ---------------- JSON proxies (same-origin; the browser never calls the service) ----------------

    [HttpGet("api/contract")]
    public Task<IActionResult> Contract(CancellationToken ct) =>
        ProxyAsync(HttpMethod.Get, "/api/crm/cycle-periods/contract", null, ReadPermission, ct, ReadFallback);

    [HttpGet("api/periods")]
    public Task<IActionResult> List(CancellationToken ct) =>
        ProxyAsync(
            HttpMethod.Get, $"/api/crm/cycle-periods{Request.QueryString}", null, ReadPermission, ct, ReadFallback);

    [HttpGet("api/periods/{cyclePeriodId:guid}")]
    public Task<IActionResult> Get(Guid cyclePeriodId, CancellationToken ct) =>
        ProxyAsync(
            HttpMethod.Get, $"/api/crm/cycle-periods/{cyclePeriodId}", null, ReadPermission, ct, ReadFallback);

    /// <summary>WP-CYC-UI-1 — what points at the period (capacity, campaigns, sessions, visits, monthly demand).</summary>
    [HttpGet("api/periods/{cyclePeriodId:guid}/usage")]
    public Task<IActionResult> Usage(Guid cyclePeriodId, CancellationToken ct) =>
        ProxyAsync(
            HttpMethod.Get, $"/api/crm/cycle-periods/{cyclePeriodId}/usage", null, ReadPermission, ct, ReadFallback);

    /// <summary>The read-only "which period is in force?" answer. It creates nothing.</summary>
    [HttpGet("api/periods/resolve-active")]
    public Task<IActionResult> ResolveActive(CancellationToken ct) =>
        ProxyAsync(
            HttpMethod.Get, $"/api/crm/cycle-periods/resolve-active{Request.QueryString}", null,
            ReadPermission, ct, ReadFallback);

    /// <summary>FU07 — the cascading selector's option source. A READ: it decides nothing about what may be saved.</summary>
    [HttpGet("api/scope-options")]
    public Task<IActionResult> ScopeOptions(CancellationToken ct) =>
        ProxyAsync(
            HttpMethod.Get, $"/api/crm/cycle-periods/scope-options{Request.QueryString}", null,
            ReadPermission, ct, ReadFallback);

    /// <summary>K-2 — the suggested code for a new period (a suggestion only; the author may change it).</summary>
    [HttpGet("api/code-suggestion")]
    public Task<IActionResult> CodeSuggestion(CancellationToken ct) =>
        ProxyAsync(
            HttpMethod.Get, $"/api/crm/cycle-periods/code-suggestion{Request.QueryString}", null,
            ManagePermission, ct, ManageFallback);

    [HttpPost("api/periods")]
    public async Task<IActionResult> Create([FromBody] CyclePeriodEditViewModel model, CancellationToken ct)
    {
        if (RequireJson(ManagePermission, ManageFallback) is { } denied)
        {
            return denied;
        }

        var response = await SendGatewayAsync(
            HttpMethod.Post, "/api/crm/cycle-periods", ToPayload(model, includeExpectedVersion: false), ct);
        return await ToProxyResultAsync(response, ct);
    }

    [HttpPut("api/periods/{cyclePeriodId:guid}")]
    public async Task<IActionResult> Update(
        Guid cyclePeriodId, [FromBody] CyclePeriodEditViewModel model, CancellationToken ct)
    {
        if (RequireJson(ManagePermission, ManageFallback) is { } denied)
        {
            return denied;
        }

        var response = await SendGatewayAsync(
            HttpMethod.Put, $"/api/crm/cycle-periods/{cyclePeriodId}",
            ToPayload(model, includeExpectedVersion: true), ct);
        return await ToProxyResultAsync(response, ct);
    }

    [HttpPost("api/periods/{cyclePeriodId:guid}/activate")]
    public Task<IActionResult> Activate(Guid cyclePeriodId, CancellationToken ct) =>
        ProxyAsync(
            HttpMethod.Post, $"/api/crm/cycle-periods/{cyclePeriodId}/activate{Request.QueryString}", null,
            ActivatePermission, ct, ManagePermission, ManageFallback);

    [HttpPost("api/periods/{cyclePeriodId:guid}/close")]
    public Task<IActionResult> Close(Guid cyclePeriodId, CancellationToken ct) =>
        ProxyAsync(
            HttpMethod.Post, $"/api/crm/cycle-periods/{cyclePeriodId}/close{Request.QueryString}", null,
            ActivatePermission, ct, ManagePermission, ManageFallback);

    // ---------------- computed endpoints (CyclePeriodScreenRules) ----------------

    /// <summary>The timeline (dynamic axis = year ± 1) and the "open periods without a capacity" band.</summary>
    [HttpGet("api/overview")]
    public async Task<IActionResult> Overview(
        [FromQuery] int? year, [FromQuery] string? scopeType, [FromQuery] string? country,
        [FromQuery(Name = "status")] string[]? statuses, CancellationToken ct)
    {
        if (RequireJson(ReadPermission, ReadFallback) is { } denied)
        {
            return denied;
        }

        var loaded = await LoadRowsAsync(ct);
        if (loaded is null)
        {
            return GatewayUnavailable();
        }

        // The list's own filters narrow the timeline too (the year sets the axis, it does not hide rows).
        var rows = CyclePeriodScreenRules.Filter(loaded, scopeType, country, statuses);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return Envelope(new
        {
            timeline = CyclePeriodScreenRules.BuildTimeline(rows, year, today),
            openWithoutCapacity = CyclePeriodScreenRules.OpenWithoutCapacity(rows)
                .Select(r => new { r.CyclePeriodId, r.CycleCode, r.CycleName, r.CycleStatus, r.StartDate, r.EndDate })
        });
    }

    /// <summary>The panel's state for one period (or a new one): which fields the status leaves editable, which
    /// lifecycle actions are offered, and the period itself.</summary>
    [HttpGet("api/panel/state")]
    public async Task<IActionResult> PanelState([FromQuery] Guid? cyclePeriodId, CancellationToken ct)
    {
        if (RequireJson(ReadPermission, ReadFallback) is { } denied)
        {
            return denied;
        }

        var canManage = HasAnyPermission(ManagePermission, ManageFallback);
        var canActivate = HasAnyPermission(ActivatePermission, ManagePermission, ManageFallback);
        if (cyclePeriodId is not { } id)
        {
            return Envelope(new
            {
                period = (CyclePeriodDetailApiModel?)null,
                fields = CyclePeriodScreenRules.FieldStates(null, isNew: true),
                actions = new CyclePeriodActions(false, false, canManage)
            });
        }

        var detail = await LoadDetailAsync(id, ct);
        if (detail is null)
        {
            return NotFound(new { errors = new[] { "Cycle period not found." } });
        }

        return Envelope(new
        {
            period = detail,
            fields = CyclePeriodScreenRules.FieldStates(detail.CycleStatus, isNew: false),
            actions = CyclePeriodScreenRules.Actions(detail.CycleStatus, canActivate, canManage)
        });
    }

    /// <summary>The panel's live warnings (end after start, sequence taken, active overlap, other-level overlap) and
    /// the first free sequence for the draft's scope and year.</summary>
    [HttpPost("api/panel/check")]
    public async Task<IActionResult> PanelCheck([FromBody] CyclePeriodDraftRequest draft, CancellationToken ct)
    {
        if (RequireJson(ReadPermission, ReadFallback) is { } denied)
        {
            return denied;
        }

        var rows = await LoadRowsAsync(ct);
        if (rows is null)
        {
            return GatewayUnavailable();
        }

        var scopeRef = ScopeRefOf(draft.ScopeType, draft.CountryScope, draft.LegalEntityId, draft.BusinessUnitId);
        var model = new CyclePeriodDraft(
            draft.CyclePeriodId, draft.Year, draft.SequenceInYear, ParseDay(draft.StartDate), ParseDay(draft.EndDate),
            draft.ScopeType, scopeRef);

        return Envelope(new
        {
            warnings = CyclePeriodScreenRules.Check(model, rows),
            nextSequence = draft.Year is { } y
                ? CyclePeriodScreenRules.NextSequence(rows, draft.ScopeType, scopeRef, y, draft.CyclePeriodId)
                : null
        });
    }

    /// <summary>K-6 — "which period is in force for this unit on this day?". ACTIVE periods only (the runtime's
    /// resolve-active); resolved / none / ambiguous each answered in their own words.</summary>
    [HttpGet("api/finder")]
    public async Task<IActionResult> Finder(
        [FromQuery] DateTimeOffset? at, [FromQuery] string? country, [FromQuery] Guid? legalEntityId,
        [FromQuery] string? businessUnitId, CancellationToken ct)
    {
        if (RequireJson(ReadPermission, ReadFallback) is { } denied)
        {
            return denied;
        }

        if (at is not { } instant)
        {
            return BadRequest(new { errors = new[] { "A date is required." } });
        }

        var query = new List<string> { $"at={Uri.EscapeDataString(instant.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))}" };
        if (!string.IsNullOrWhiteSpace(country)) query.Add($"country={Uri.EscapeDataString(country.Trim())}");
        if (legalEntityId is { } le && le != Guid.Empty) query.Add($"legalEntityId={le:D}");
        if (!string.IsNullOrWhiteSpace(businessUnitId)) query.Add($"businessUnitId={Uri.EscapeDataString(businessUnitId.Trim())}");

        var response = await SendGatewayAsync(
            HttpMethod.Get, "/api/crm/cycle-periods/resolve-active?" + string.Join("&", query), null, ct);
        if (response is null || !response.IsSuccessStatusCode)
        {
            return await ToProxyResultAsync(response, ct);
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        var resolution = JsonSerializer.Deserialize<CyclePeriodGatewayResponse<CyclePeriodResolutionApiModel>>(body, _json)?.Data;
        var view = CyclePeriodScreenRules.Finder(resolution?.Outcome);
        return Envelope(new
        {
            view.Outcome,
            view.Tone,
            view.MessageKey,
            resolvedScopeType = resolution?.ResolvedScopeType,
            period = view.Outcome == "resolved" ? resolution?.Period : null,
            candidateCount = resolution?.CandidateIds?.Count ?? 0
        });
    }

    /// <summary>The panel's live day + working-day count. The working calendar needs a country; without one the answer
    /// is <c>no_country</c>, never a guess.</summary>
    [HttpGet("api/working-days")]
    public async Task<IActionResult> WorkingDays(
        [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? country, [FromQuery] Guid? legalEntityId,
        CancellationToken ct)
    {
        if (RequireJson(ReadPermission, ReadFallback) is { } denied)
        {
            return denied;
        }

        if (ParseDay(from) is not { } start || ParseDay(to) is not { } end || end < start)
        {
            return BadRequest(new { errors = new[] { "A valid range is required." } });
        }

        var count = await CountWorkingDaysAsync(country, legalEntityId, start, end, ct);
        return Envelope(new
        {
            days = CyclePeriodScreenRules.DayCount(start, end),
            workingDays = count.WorkingDays,
            resolution = count.Resolution,
            country = string.IsNullOrWhiteSpace(country) ? null : country.Trim().ToUpperInvariant()
        });
    }

    /// <summary>The details page's calendar summary: one row per month the period touches (clipped at its edges).</summary>
    [HttpGet("api/periods/{cyclePeriodId:guid}/calendar")]
    public async Task<IActionResult> Calendar(Guid cyclePeriodId, CancellationToken ct)
    {
        if (RequireJson(ReadPermission, ReadFallback) is { } denied)
        {
            return denied;
        }

        var detail = await LoadDetailAsync(cyclePeriodId, ct);
        if (detail is null)
        {
            return NotFound(new { errors = new[] { "Cycle period not found." } });
        }

        var country = CalendarCountryOf(detail.ScopeType, detail.CountryScope, detail.BusinessUnitCountryContext);
        var months = new List<object>();
        var resolution = "resolved";
        foreach (var (year, month, from, to) in CyclePeriodScreenRules.Months(
                     CyclePeriodScreenRules.ToDay(detail.StartDate), CyclePeriodScreenRules.ToDay(detail.EndDate)))
        {
            var count = await CountWorkingDaysAsync(country, detail.LegalEntityId, from, to, ct);
            if (count.Resolution != "resolved")
            {
                resolution = count.Resolution;
            }

            var days = CyclePeriodScreenRules.DayCount(from, to);
            months.Add(new
            {
                year,
                month,
                from,
                to,
                days,
                workingDays = count.WorkingDays,
                nonWorkingDays = count.WorkingDays is { } wd ? days - wd : (int?)null,
                partial = from.Day != 1 || to != new DateOnly(year, month, 1).AddMonths(1).AddDays(-1),
                resolution = count.Resolution
            });
        }

        return Envelope(new { country, resolution, months });
    }

    // ---------------- form helpers ----------------

    /// <summary>
    /// The write payload. <c>TenantId</c> and <c>CycleStatus</c> are absent by construction, and only the reference
    /// belonging to the chosen scope is sent: a hidden-but-populated field would otherwise reach the runtime and be
    /// refused as an ambiguous scope, which is a confusing way to fail a form the author filled in correctly.
    /// </summary>
    public static object ToPayload(CyclePeriodEditViewModel model, bool includeExpectedVersion)
    {
        var scopeType = (model.ScopeType ?? string.Empty).Trim().ToLowerInvariant();

        return new
        {
            cycleCode = model.CycleCode?.Trim(),
            cycleName = model.CycleName?.Trim(),
            year = model.Year,
            sequenceInYear = model.SequenceInYear,
            // The picked calendar day, anchored to UTC so the runtime stores the day the author chose.
            startDate = PickedDayToUtc(model.StartDate),
            endDate = PickedDayToUtc(model.EndDate),
            scopeType,
            countryScope = scopeType == "country" ? Clean(model.CountryScope) : null,
            legalEntityId = scopeType == "legal-entity" ? model.LegalEntityId : null,
            businessUnitId = scopeType == "business-unit" ? Clean(model.BusinessUnitId) : null,
            // Context, not scope: sent ONLY with a business unit, so it can never look like a country-scoped period.
            businessUnitCountryContext = scopeType == "business-unit"
                ? Clean(model.BusinessUnitCountryContext)
                : null,
            description = Clean(model.Description),
            expectedVersion = includeExpectedVersion ? model.ExpectedVersion : null
        };
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// A date the AUTHOR picked, anchored to UTC midnight. An <c>&lt;input type="date"&gt;</c> value bound with the
    /// SERVER's offset lands a day early east of Greenwich; the date component is the day the author clicked, and
    /// pairing it with a zero offset sends exactly that day.
    /// </summary>
    private static DateTimeOffset? PickedDayToUtc(DateTimeOffset? value)
        => value is { } d ? new DateTimeOffset(d.Date, TimeSpan.Zero) : null;

    /// <summary>The working-calendar country a period's days are counted in: its own country, a business unit's
    /// country context, otherwise none (tenant and legal-entity periods name no country).</summary>
    public static string? CalendarCountryOf(string? scopeType, string? countryScope, string? businessUnitCountryContext)
        => CyclePeriodScreenRules.NormalizeScope(scopeType) switch
        {
            "country" => Clean(countryScope)?.ToUpperInvariant(),
            "business-unit" => Clean(businessUnitCountryContext)?.ToUpperInvariant(),
            _ => null
        };

    private static string? ScopeRefOf(string? scopeType, string? country, Guid? legalEntityId, string? businessUnitId)
        => CyclePeriodScreenRules.NormalizeScope(scopeType) switch
        {
            "country" => Clean(country),
            "legal-entity" => legalEntityId is { } id && id != Guid.Empty ? id.ToString("D") : null,
            "business-unit" => Clean(businessUnitId),
            _ => null
        };

    private static DateOnly? ParseDay(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        if (DateOnly.TryParseExact(text.Length >= 10 ? text[..10] : text, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var day))
        {
            return day;
        }

        return null;
    }

    private async Task<(int? WorkingDays, string Resolution)> CountWorkingDaysAsync(
        string? country, Guid? legalEntityId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(country))
        {
            return (null, "no_country");
        }

        var path = $"{WorkingDaysPath}&date={CyclePeriodScreenRules.Iso(from)}&toDate={CyclePeriodScreenRules.Iso(to)}"
                   + $"&countryCode={Uri.EscapeDataString(country.Trim().ToUpperInvariant())}"
                   + (legalEntityId is { } le && le != Guid.Empty ? $"&legalEntityId={le:D}" : string.Empty);

        var response = await SendGatewayAsync(HttpMethod.Get, path, null, ct);
        if (response is null)
        {
            return (null, "calendar_unresolved");
        }

        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
        {
            return (null, "calendar_forbidden");
        }

        if (!response.IsSuccessStatusCode)
        {
            return (null, "calendar_unresolved");
        }

        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<CyclePeriodGatewayResponse<CyclePeriodWorkingDaysApiModel>>(body, _json)?.Data;
            return data is not null
                   && string.Equals(data.Resolution, "resolved", StringComparison.OrdinalIgnoreCase)
                   && data.WorkingDayCount is { } count
                ? (count, "resolved")
                : (null, "calendar_unresolved");
        }
        catch (JsonException)
        {
            return (null, "calendar_unresolved");
        }
    }

    private async Task<IReadOnlyList<CyclePeriodRow>?> LoadRowsAsync(CancellationToken ct)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, "/api/crm/cycle-periods", null, ct);
        if (response is null || !response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        var list = JsonSerializer.Deserialize<CyclePeriodGatewayResponse<CyclePeriodListApiModel>>(body, _json)?.Data;
        return (list?.Items ?? []).Select(i => new CyclePeriodRow(
                i.CyclePeriodId, i.CycleCode, i.CycleName, i.Year, i.SequenceInYear,
                CyclePeriodScreenRules.ToDay(i.StartDate), CyclePeriodScreenRules.ToDay(i.EndDate),
                i.ScopeType, i.ScopeRef, i.CycleStatus, i.HasCapacity))
            .ToList();
    }

    private async Task<CyclePeriodDetailApiModel?> LoadDetailAsync(Guid cyclePeriodId, CancellationToken ct)
    {
        var response = await SendGatewayAsync(
            HttpMethod.Get, $"/api/crm/cycle-periods/{cyclePeriodId}", null, ct);
        if (response is null || !response.IsSuccessStatusCode)
        {
            TempData["ErrorMessage"] = cyclePeriodId.ToString();
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer
            .Deserialize<CyclePeriodGatewayResponse<CyclePeriodDetailApiModel>>(body, _json)?.Data;
    }

    /// <summary>
    /// Loads the selector's options. An unreachable source yields an EMPTY, NOT-READY list — never a substituted one:
    /// a hardcoded fallback would let an author pick a value the platform does not know.
    /// </summary>
    private async Task<CyclePeriodScopeOptionsViewModel> LoadScopeOptionsAsync(
        string? country, DateTimeOffset? startDate, DateTimeOffset? endDate, CancellationToken ct)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(country))
        {
            query.Add($"country={Uri.EscapeDataString(country.Trim())}");
        }

        if (startDate is { } start)
        {
            query.Add($"startDate={Uri.EscapeDataString(start.ToString("O"))}");
        }

        if (endDate is { } end)
        {
            query.Add($"endDate={Uri.EscapeDataString(end.ToString("O"))}");
        }

        var path = "/api/crm/cycle-periods/scope-options"
                   + (query.Count == 0 ? string.Empty : "?" + string.Join("&", query));

        var response = await SendGatewayAsync(HttpMethod.Get, path, null, ct);
        if (response is null || !response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Cycle period scope options could not be loaded; rendering the panel without them.");
            return new CyclePeriodScopeOptionsViewModel();
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        var api = JsonSerializer
            .Deserialize<CyclePeriodGatewayResponse<CyclePeriodScopeOptionsApiModel>>(body, _json)?.Data;
        if (api is null)
        {
            return new CyclePeriodScopeOptionsViewModel();
        }

        return new CyclePeriodScopeOptionsViewModel
        {
            ScopeTypes = api.ScopeTypes,
            Countries = api.Countries.Select(Option).ToList(),
            CountryReady = api.CountryReady,
            LegalEntities = api.LegalEntities.Select(Option).ToList(),
            LegalEntityReady = api.LegalEntityReady,
            BusinessUnits = api.BusinessUnits.Select(Option).ToList(),
            BusinessUnitReady = api.BusinessUnitReady,
            BusinessUnitFromTerritory = api.BusinessUnitFromTerritory,
            CountrySetCode = api.CountrySetCode,
            BusinessUnitSetCode = api.BusinessUnitSetCode
        };

        static CyclePeriodScopeOptionViewModel Option(CyclePeriodScopeOptionApiModel o)
            => new() { Value = o.Value, Label = o.Label, Hint = o.Hint };
    }

    // ---------------- proxy helpers ----------------

    private IActionResult Envelope(object data) => Json(new { data, isSuccessful = true, statusCode = 200 });

    private static IActionResult GatewayUnavailable()
        => new ObjectResult(new { errors = new[] { "Gateway unavailable." } }) { StatusCode = 502 };

    private async Task<IActionResult> ProxyAsync(
        HttpMethod method, string path, JsonElement? body, string permission, CancellationToken ct,
        params string[] fallbacks)
    {
        if (RequireJson(permission, fallbacks) is { } denied)
        {
            return denied;
        }

        if (body.HasValue && ContainsTenantId(body.Value))
        {
            return BadRequest(new { errors = new[] { "TenantId is server-resolved and must not be supplied." } });
        }

        var response = await SendGatewayAsync(method, path, body?.GetRawText(), ct);
        return await ToProxyResultAsync(response, ct);
    }

    private Task<HttpResponseMessage?> SendGatewayAsync(
        HttpMethod method, string path, object? body, CancellationToken ct)
        => SendGatewayAsync(method, path, body is null ? null : JsonSerializer.Serialize(body, _json), ct);

    private async Task<HttpResponseMessage?> SendGatewayAsync(
        HttpMethod method, string path, string? rawBody, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(method, $"{_gatewayUrl}{path}");
            var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var tenantId = GetTenantId();
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                return null;
            }

            request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId);

            if (rawBody is not null)
            {
                request.Content = new StringContent(rawBody, Encoding.UTF8, "application/json");
            }

            return await _httpClient.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cycle period Gateway request failed: {Method} {Path}", method, path);
            return null;
        }
    }

    private static async Task<IActionResult> ToProxyResultAsync(HttpResponseMessage? response, CancellationToken ct)
    {
        if (response is null)
        {
            return GatewayUnavailable();
        }

        // A bodiless status must stay bodiless: writing a body onto a 204/205/304/1xx makes Kestrel throw
        // ("Content-Length not allowed"), which turns a perfectly good no-content answer into a 500.
        if (IsBodilessStatus(response.StatusCode))
        {
            return new StatusCodeResult((int)response.StatusCode);
        }

        var content = await response.Content.ReadAsStringAsync(ct);
        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
            Content = content
        };
    }

    private static bool IsBodilessStatus(HttpStatusCode status)
        => (int)status is >= 100 and < 200 || status is HttpStatusCode.NoContent
            or HttpStatusCode.ResetContent or HttpStatusCode.NotModified;

    private static bool ContainsTenantId(JsonElement element) => element.ValueKind == JsonValueKind.Object &&
        element.EnumerateObject().Any(x => string.Equals(x.Name, "tenantId", StringComparison.OrdinalIgnoreCase));

    private string? GetTenantId() => User.Claims.FirstOrDefault(x =>
        x.Type == "tenantId" || x.Type == "tenant_id" ||
        x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;

    private bool HasAnyPermission(params string[] permissions) =>
        permissions.Any(x => PermissionClaims.HasPermission(User, x));

    /// <summary>An unauthorized page answers a bare 403 — no shell, no skeleton, no redirect (UAS-001).</summary>
    private IActionResult? RequirePage(string permission, params string[] fallbacks) =>
        HasAnyPermission([permission, .. fallbacks]) ? null : StatusCode(StatusCodes.Status403Forbidden);

    private IActionResult? RequireJson(string permission, params string[] fallbacks) =>
        HasAnyPermission([permission, .. fallbacks])
            ? null
            : StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." });
}

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Diten.Web.Models.CRM;
using Diten.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// MOD-0155 FU06 Cycle Capacity UI (Golden <b>Compact</b>). All business traffic is proxied server-side through
/// Gateway 5000; the browser never sees a service URL or a bearer token. The CrmService runtime stays the
/// authoritative validation and permission layer — nothing is decided here.
/// <para>There is no delete surface (retiring a capacity is Archive) and no approve surface at all: approving an
/// ESTIMATE is follow-up F-APPROVAL, and this page must not imply the number is a commitment.</para>
/// <para><b>CyclePeriod is consumed READ-ONLY.</b> The period picker reads the CyclePeriod selector endpoint and the
/// detail pages read the projected period; nothing here writes to CyclePeriod, and CyclePeriod does not know this
/// module exists.</para>
/// </summary>
[Authorize]
[Route("CRM/CycleCapacities")]
public sealed class CycleCapacitiesController : Controller
{
    private const string ReadPermission = "crm.cycle-capacity.read";
    private const string ManagePermission = "crm.cycle-capacity.manage";

    /// <summary>Documented DEV-ONLY fallback until F-RBAC lands. It widens no guard: the CrmService still enforces
    /// tenant isolation, the pin, the closed-period lock and the fail-closed calendar read behind it.</summary>
    private const string ReadFallback = "crm.territory.read";

    private const string ManageFallback = "crm.territory.model.manage";
    private const string ViewRoot = "~/Views/CRM/CycleCapacities";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger<CycleCapacitiesController> _logger;

    private readonly JsonSerializerOptions _json =
        new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public CycleCapacitiesController(
        HttpClient httpClient, IConfiguration configuration, ILogger<CycleCapacitiesController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _logger = logger;
    }

    // ---------------- pages ----------------

    /// <summary>
    /// The capacity grid — and, when <c>cyclePeriodId</c> is supplied, the DEEP-LINK RESOLVER behind the CyclePeriod
    /// row action: it answers "does this period already have a capacity?" and redirects to the detail page or to a
    /// prefilled create form.
    /// <para>The resolution happens here rather than in the browser so the answer is decided by the same server that
    /// owns the 1:1 rule; a client-side guess could send an author to a create form for a period that is already
    /// taken.</para>
    /// </summary>
    [HttpGet("")]
    // The row action links to /CRM/CycleCapacities/Index?... ; the bare /CRM/CycleCapacities is the menu entry. Both
    // reach the same action, so a hand-typed URL cannot 404 on a segment this controller happens not to declare.
    [HttpGet("Index")]
    public async Task<IActionResult> Index(
        [FromQuery] Guid? cyclePeriodId, [FromQuery] string? returnTo, CancellationToken ct)
    {
        if (RequirePage(ReadPermission, ReadFallback) is { } denied)
        {
            return denied;
        }

        if (cyclePeriodId is { } periodId && periodId != Guid.Empty)
        {
            var existing = await LoadByCyclePeriodAsync(periodId, ct);
            if (existing is not null)
            {
                // The origin travels with the redirect: whichever page the author ends up on, Save and Cancel must
                // still know where they came from.
                return RedirectToAction(
                    nameof(Details),
                    new { cycleCapacityId = existing.CycleCapacityId, returnTo = OriginRouteValue(returnTo) });
            }

            // 404 from the lookup means "not yet", which is an expected answer rather than an error — so the author
            // lands on a create form already pinned to the period whose row they clicked.
            return HasAnyPermission(ManagePermission, ManageFallback)
                ? RedirectToAction(
                    nameof(Create), new { cyclePeriodId = periodId, returnTo = OriginRouteValue(returnTo) })
                : View($"{ViewRoot}/Index.cshtml", new CycleCapacityIndexViewModel { CanManage = false });
        }

        return View($"{ViewRoot}/Index.cshtml", new CycleCapacityIndexViewModel
        {
            CanManage = HasAnyPermission(ManagePermission, ManageFallback)
        });
    }

    [HttpGet("Create")]
    public async Task<IActionResult> Create(
        [FromQuery] Guid? cyclePeriodId, [FromQuery] string? returnTo, CancellationToken ct)
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied)
        {
            return denied;
        }

        var defaults = await LoadDefaultsAsync(ct);
        var model = new CycleCapacityEditViewModel
        {
            CyclePeriodId = cyclePeriodId is { } id && id != Guid.Empty ? id : null,
            ReturnTo = OriginRouteValue(returnTo),
            DailyWorkMinutes = defaults?.DailyWorkMinutes,
            PromoProductTime = 0,
            NonPromoProductTime = 0,
            TravelingTime = 0,
            ReportDuration = 0,
            QuizDuration = 0,
            // FU06B — the configured buffer, shown as the SAME number the server will write (falls back to 5 only when
            // the contract could not be loaded).
            BetweenVisitTimeMinutes = defaults?.BetweenVisitTimeMinutes ?? 5
        };

        await PopulateOptionsAsync(model, ct);
        // FU07 — the FTE is per month now, so the configured default is seeded onto each row rather than onto the
        // capacity. The server stamps its own value on save regardless; this is what the author SEES.
        SeedMonthsFromPeriod(model, defaults?.Fte, defaults?.FteSource);

        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] CycleCapacityEditViewModel model, CancellationToken ct)
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied)
        {
            return denied;
        }

        if (!ModelState.IsValid)
        {
            return await RedisplayAsync($"{ViewRoot}/Create.cshtml", model, ct);
        }

        var response = await SendGatewayAsync(
            HttpMethod.Post, "/api/crm/cycle-capacities", ToPayload(model, includeExpectedVersion: false), ct);

        if (response is not null && response.IsSuccessStatusCode)
        {
            TempData["SuccessMessage"] = model.CyclePeriod?.CycleName ?? model.CyclePeriodId?.ToString();
            return RedirectToOrigin(model.ReturnTo);
        }

        await AddGatewayErrorsAsync(response, ct);
        return await RedisplayAsync($"{ViewRoot}/Create.cshtml", model, ct);
    }

    [HttpGet("Edit/{cycleCapacityId:guid}")]
    public async Task<IActionResult> Edit(
        Guid cycleCapacityId, [FromQuery] string? returnTo, CancellationToken ct)
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied)
        {
            return denied;
        }

        var detail = await LoadDetailAsync(cycleCapacityId, ct);
        if (detail is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var model = ToEditModel(detail);
        model.ReturnTo = OriginRouteValue(returnTo);
        await PopulateOptionsAsync(model, ct);

        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    [HttpPost("Edit/{cycleCapacityId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        Guid cycleCapacityId, [FromForm] CycleCapacityEditViewModel model, CancellationToken ct)
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied)
        {
            return denied;
        }

        model.CycleCapacityId = cycleCapacityId;
        if (!ModelState.IsValid)
        {
            return await RedisplayAsync($"{ViewRoot}/Edit.cshtml", model, ct);
        }

        var response = await SendGatewayAsync(
            HttpMethod.Put, $"/api/crm/cycle-capacities/{cycleCapacityId}",
            ToPayload(model, includeExpectedVersion: true), ct);

        if (response is not null && response.IsSuccessStatusCode)
        {
            TempData["SuccessMessage"] = model.CyclePeriod?.CycleName ?? cycleCapacityId.ToString();
            return RedirectToOrigin(model.ReturnTo);
        }

        await AddGatewayErrorsAsync(response, ct);
        return await RedisplayAsync($"{ViewRoot}/Edit.cshtml", model, ct);
    }

    /// <summary>
    /// The read-only detail page, including the ESTIMATE.
    /// <para>The estimate is loaded server-side rather than by the page's JS because it is the one call that can fail
    /// for a reason the reader must understand — no calendar, or no permission to read one — and rendering that
    /// explanation in the page beats a toast that disappears.</para>
    /// </summary>
    [HttpGet("Details/{cycleCapacityId:guid}")]
    public async Task<IActionResult> Details(
        Guid cycleCapacityId, [FromQuery] string? returnTo, CancellationToken ct)
    {
        if (RequirePage(ReadPermission, ReadFallback) is { } denied)
        {
            return denied;
        }

        var detail = await LoadDetailAsync(cycleCapacityId, ct);
        if (detail is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var model = ToEditModel(detail);
        model.ReturnTo = OriginRouteValue(returnTo);
        ViewData["CanManage"] = HasAnyPermission(ManagePermission, ManageFallback);
        ViewData["Calculation"] = await LoadCalculationAsync(cycleCapacityId, ct);

        return View($"{ViewRoot}/Details.cshtml", model);
    }

    // ---------------- JSON proxies (same-origin; the browser never calls 5061) ----------------

    [HttpGet("api/contract")]
    public Task<IActionResult> Contract(CancellationToken ct) =>
        ProxyAsync(HttpMethod.Get, "/api/crm/cycle-capacities/contract", null, ReadPermission, ct, ReadFallback);

    [HttpGet("api/capacities")]
    public Task<IActionResult> List(CancellationToken ct) =>
        ProxyAsync(
            HttpMethod.Get, $"/api/crm/cycle-capacities{Request.QueryString}", null, ReadPermission, ct, ReadFallback);

    [HttpGet("api/capacities/{cycleCapacityId:guid}")]
    public Task<IActionResult> Get(Guid cycleCapacityId, CancellationToken ct) =>
        ProxyAsync(
            HttpMethod.Get, $"/api/crm/cycle-capacities/{cycleCapacityId}", null, ReadPermission, ct, ReadFallback);

    /// <summary>The estimate. A READ that reaches the working calendar; it writes nothing and caches nothing, and an
    /// unresolved answer comes back as 503 with its reason codes intact rather than as a generic failure.</summary>
    [HttpGet("api/capacities/{cycleCapacityId:guid}/calculation")]
    public Task<IActionResult> Calculation(Guid cycleCapacityId, CancellationToken ct) =>
        ProxyAsync(
            HttpMethod.Get, $"/api/crm/cycle-capacities/{cycleCapacityId}/calculation", null,
            ReadPermission, ct, ReadFallback);

    /// <summary>
    /// The LIVE estimate the create/edit form calls while the author is typing — a straight passthrough.
    /// <para>It is a POST because it carries a body, and a READ in every other respect: the CrmService builds a
    /// TRANSIENT capacity from these numbers, estimates it and throws it away. Nothing is created, and the answer has
    /// no id to save against — so this proxy guards on <c>read</c>, like the other estimate surface, rather than on
    /// <c>manage</c>.</para>
    /// <para>The 503 an unresolved calendar produces is passed through with its body intact: the page needs the
    /// resolution and reason codes to explain itself, and flattening it into a generic failure would take that away.</para>
    /// </summary>
    [HttpPost("api/capacities/calculation-preview")]
    public Task<IActionResult> CalculationPreview([FromBody] JsonElement body, CancellationToken ct) =>
        ProxyAsync(
            HttpMethod.Post, "/api/crm/cycle-capacities/calculation-preview", body, ReadPermission, ct, ReadFallback);

    [HttpPost("api/capacities/{cycleCapacityId:guid}/archive")]
    public Task<IActionResult> Archive(Guid cycleCapacityId, CancellationToken ct) =>
        ProxyAsync(
            HttpMethod.Post, $"/api/crm/cycle-capacities/{cycleCapacityId}/archive{Request.QueryString}", null,
            ManagePermission, ct, ManageFallback);

    /// <summary>
    /// Where a SAVE lands: back on the Cycle Periods grid.
    /// <para>Authoring a capacity is reached from a period's row action, so the period grid — not the capacity list —
    /// is where the author came from and what they were working on. Returning them to the capacity list would leave
    /// them one step away from the thing they were editing, on a page they never asked for.</para>
    /// <para>It is used ONLY on the two post-save paths. A rejected write still redisplays the form (the author must
    /// not lose their input), a not-found Edit/Details still falls back to the capacity list, and the standalone
    /// <see cref="Index"/> stays fully reachable for anyone who navigates to it directly.</para>
    /// <para><c>TempData["SuccessMessage"]</c> survives the redirect: the toast is rendered by the shared tenant
    /// layout, so it appears on the Cycle Periods page just as it did here.</para>
    /// </summary>
    /// <summary>
    /// The one origin this module recognises. Kept as a constant rather than a magic string so the row action, the
    /// deep-link resolver, the form and the cancel link all mean the same thing by it.
    /// </summary>
    public const string ReturnToCyclePeriods = "cycleperiods";

    private static bool WantsCyclePeriods(string? returnTo)
        => string.Equals((returnTo ?? string.Empty).Trim(), ReturnToCyclePeriods, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Where a finished edit — saved OR cancelled — lands: back where the author came from.
    /// <para>Reached from a period's row action, that is the Cycle Periods grid; reached from the module's own menu
    /// entry, it is the capacity list. Sending everyone to one of the two would strand half of them on a page they
    /// never asked for, which is exactly what an unconditional redirect did.</para>
    /// <para><b>Anything other than the one known origin falls back to the capacity list.</b> The value round-trips
    /// through a query string and a hidden field, so it is caller-supplied: it is compared against a constant and used
    /// only to choose between two fixed local actions — never turned into a URL. A tampered value is simply not the
    /// constant, and the author lands on the list.</para>
    /// </summary>
    private IActionResult RedirectToOrigin(string? returnTo)
        => WantsCyclePeriods(returnTo)
            ? RedirectToAction("Index", "CyclePeriods")
            : RedirectToAction(nameof(Index));

    /// <summary>The origin as a route value, or null when there is none — so a link only carries the parameter when it
    /// means something.</summary>
    private static string? OriginRouteValue(string? returnTo)
        => WantsCyclePeriods(returnTo) ? ReturnToCyclePeriods : null;

    // ---------------- form helpers ----------------

    /// <summary>
    /// The write payload. <c>TenantId</c> is absent by construction, and so is <c>fte</c>: the interim average is
    /// stamped server-side, so this form has nothing to send even though it renders the number.
    /// <para><c>cyclePeriodId</c> is sent on CREATE only. The update endpoint does not accept one — the pin is set
    /// once, and leaving it out of the payload is stronger than rejecting a value.</para>
    /// </summary>
    private static object ToPayload(CycleCapacityEditViewModel model, bool includeExpectedVersion)
    {
        var months = (model.Months ?? [])
            .Where(m => m.Year is > 0 && m.MonthNumber is >= 1 and <= 12)
            .Select(m => new
            {
                year = m.Year,
                monthNumber = m.MonthNumber,
                meetingDays = m.MeetingDays ?? 0,
                trainingDays = m.TrainingDays ?? 0,
                vacationDays = m.VacationDays ?? 0,
                microTargetingDayCount = m.MicroTargetingDayCount ?? 0,
                microTargetingDuration = m.MicroTargetingDuration ?? 0
            })
            .ToList();

        if (includeExpectedVersion)
        {
            return new
            {
                calendarCountryCode = Clean(model.CalendarCountryCode)?.ToUpperInvariant(),
                dailyWorkMinutes = model.DailyWorkMinutes,
                promoProductTime = model.PromoProductTime,
                nonPromoProductTime = model.NonPromoProductTime,
                travelingTime = model.TravelingTime,
                reportDuration = model.ReportDuration,
                quizDuration = model.QuizDuration,
                betweenVisitTimeMinutes = model.BetweenVisitTimeMinutes,
                description = Clean(model.Description),
                months,
                expectedVersion = model.ExpectedVersion
            };
        }

        return new
        {
            cyclePeriodId = model.CyclePeriodId,
            calendarCountryCode = Clean(model.CalendarCountryCode)?.ToUpperInvariant(),
            dailyWorkMinutes = model.DailyWorkMinutes,
            promoProductTime = model.PromoProductTime,
            nonPromoProductTime = model.NonPromoProductTime,
            travelingTime = model.TravelingTime,
            reportDuration = model.ReportDuration,
            quizDuration = model.QuizDuration,
            betweenVisitTimeMinutes = model.BetweenVisitTimeMinutes,
            description = Clean(model.Description),
            months
        };
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static CycleCapacityEditViewModel ToEditModel(CycleCapacityDetailApiModel detail) => new()
    {
        CycleCapacityId = detail.CycleCapacityId,
        CyclePeriodId = detail.CyclePeriodId,
        CalendarCountryCode = detail.CalendarCountryCode,
        CalendarCountryIsDerived = detail.CalendarCountryIsDerived,
        DailyWorkMinutes = detail.DailyWorkMinutes,
        PromoProductTime = detail.PromoProductTime,
        NonPromoProductTime = detail.NonPromoProductTime,
        TravelingTime = detail.TravelingTime,
        ReportDuration = detail.ReportDuration,
        QuizDuration = detail.QuizDuration,
        BetweenVisitTimeMinutes = detail.BetweenVisitTimeMinutes,
        Description = detail.Description,
        ExpectedVersion = detail.Version,
        IsArchived = detail.IsArchived,
        IsEditable = detail.IsEditable,
        CyclePeriod = ToPeriod(detail.CyclePeriod),
        Months = detail.Months
            .OrderBy(m => m.Year).ThenBy(m => m.MonthNumber)
            .Select(m => new CycleCapacityMonthViewModel
            {
                Year = m.Year,
                MonthNumber = m.MonthNumber,
                MeetingDays = m.MeetingDays,
                TrainingDays = m.TrainingDays,
                VacationDays = m.VacationDays,
                MicroTargetingDayCount = m.MicroTargetingDayCount,
                MicroTargetingDuration = m.MicroTargetingDuration,
                Fte = m.Fte,
                FteSource = m.FteSource,
                MonthLabel = MonthLabel(m.Year, m.MonthNumber)
            })
            .ToList()
    };

    private static CycleCapacityPeriodViewModel? ToPeriod(CycleCapacityPeriodApiModel? p)
        => p is null
            ? null
            : new CycleCapacityPeriodViewModel
            {
                CyclePeriodId = p.CyclePeriodId,
                CycleCode = p.CycleCode,
                CycleName = p.CycleName,
                Year = p.Year,
                SequenceInYear = p.SequenceInYear,
                StartDate = StoredDayToUtc(p.StartDate),
                EndDate = StoredDayToUtc(p.EndDate),
                CycleStatus = p.CycleStatus,
                ScopeType = p.ScopeType,
                ScopeRef = p.ScopeRef,
                CountryScope = p.CountryScope,
                IsClosed = p.IsClosed
            };

    /// <summary>
    /// A date the RUNTIME returned, anchored to UTC midnight. A stored instant may deserialize into any offset, and on
    /// a negative one its local date component is the previous day — the stored day is the UTC day. The same reading
    /// the sibling CyclePeriod page uses, so the two never show a window differently.
    /// </summary>
    private static DateTimeOffset StoredDayToUtc(DateTimeOffset value) => new(value.UtcDateTime.Date, TimeSpan.Zero);

    private static string MonthLabel(int year, int monthNumber)
        => monthNumber is >= 1 and <= 12
            ? $"{CultureInfo.CurrentUICulture.DateTimeFormat.GetMonthName(monthNumber)} {year}"
            : $"{year}-{monthNumber:00}";

    /// <summary>
    /// Derives the month rows from the pinned period's window, preserving anything the author already typed.
    /// <para>The author edits DEDUCTIONS, never which months exist: the set of months is a fact of the period, and
    /// letting a form add or remove one would let a capacity describe a month its period does not cover.</para>
    /// </summary>
    private static void SeedMonthsFromPeriod(
        CycleCapacityEditViewModel model, decimal? defaultFte = null, string? defaultFteSource = null)
    {
        if (model.CyclePeriod is not { } period)
        {
            return;
        }

        var existing = (model.Months ?? [])
            .Where(m => m.Year is not null && m.MonthNumber is not null)
            .ToDictionary(m => (m.Year!.Value, m.MonthNumber!.Value));

        var months = new List<CycleCapacityMonthViewModel>();
        var start = period.StartDate.UtcDateTime.Date;
        var end = period.EndDate.UtcDateTime.Date;
        var cursor = new DateTime(start.Year, start.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var last = new DateTime(end.Year, end.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        while (cursor <= last && months.Count < 24)
        {
            months.Add(existing.TryGetValue((cursor.Year, cursor.Month), out var row)
                ? Relabel(row)
                : new CycleCapacityMonthViewModel
                {
                    Year = cursor.Year,
                    MonthNumber = cursor.Month,
                    MeetingDays = 0,
                    TrainingDays = 0,
                    VacationDays = 0,
                    MicroTargetingDayCount = 0,
                    MicroTargetingDuration = 0,
                    Fte = defaultFte,
                    FteSource = defaultFteSource,
                    MonthLabel = MonthLabel(cursor.Year, cursor.Month)
                });

            cursor = cursor.AddMonths(1);
        }

        model.Months = months;

        static CycleCapacityMonthViewModel Relabel(CycleCapacityMonthViewModel row)
        {
            row.MonthLabel = MonthLabel(row.Year ?? 0, row.MonthNumber ?? 0);
            return row;
        }
    }

    /// <summary>Re-renders a rejected form with its option lists and month rows intact — an author must not lose their
    /// work because the runtime refused one field.</summary>
    private async Task<IActionResult> RedisplayAsync(
        string view, CycleCapacityEditViewModel model, CancellationToken ct)
    {
        await PopulateOptionsAsync(model, ct);
        // No default here on purpose: the rows already carry their FTE from the POST, and re-seeding would blank the
        // column on a rejected save.
        SeedMonthsFromPeriod(model);
        return View(view, model);
    }

    /// <summary>
    /// Loads the pinned period, the period picker and the governed country list.
    /// <para>An unreachable source yields an EMPTY, NOT-READY list — never a substituted one: a hardcoded fallback
    /// would let an author pick a value the platform does not know, and the save would then be refused for a reason
    /// the form never showed them.</para>
    /// </summary>
    private async Task PopulateOptionsAsync(CycleCapacityEditViewModel model, CancellationToken ct)
    {
        var periods = await LoadPeriodSelectorAsync(ct);

        model.PeriodOptions = periods
            .Select(p => new CycleCapacityPeriodOptionViewModel
            {
                CyclePeriodId = p.CyclePeriodId,
                Label = $"{p.CycleCode} · {p.CycleName}",
                Hint = $"{StoredDayToUtc(p.StartDate):yyyy-MM-dd} – {StoredDayToUtc(p.EndDate):yyyy-MM-dd}"
            })
            .ToList();

        if (model.CyclePeriodId is { } periodId && periodId != Guid.Empty)
        {
            var picked = periods.FirstOrDefault(p => p.CyclePeriodId == periodId);
            if (picked is not null)
            {
                model.CyclePeriod = new CycleCapacityPeriodViewModel
                {
                    CyclePeriodId = picked.CyclePeriodId,
                    CycleCode = picked.CycleCode,
                    CycleName = picked.CycleName,
                    Year = picked.Year,
                    SequenceInYear = picked.SequenceInYear,
                    StartDate = StoredDayToUtc(picked.StartDate),
                    EndDate = StoredDayToUtc(picked.EndDate),
                    CycleStatus = picked.CycleStatus,
                    ScopeType = picked.ScopeType,
                    ScopeRef = picked.ScopeRef,
                    CountryScope = picked.CountryScope,
                    IsClosed = string.Equals(picked.CycleStatus, "closed", StringComparison.OrdinalIgnoreCase)
                };
            }

            // D-COUNTRY = B, mirrored in the form: a country-scoped period NAMES the country, so the control is
            // prefilled and rendered read-only. The server derives it again on every write, so this is presentation
            // only — a tampered value cannot win.
            if (model.CyclePeriod is { ScopeType: "country" } scoped
                && !string.IsNullOrWhiteSpace(scoped.CountryScope))
            {
                model.CalendarCountryCode = scoped.CountryScope.Trim().ToUpperInvariant();
                model.CalendarCountryIsDerived = true;
            }
        }

        // The governed country values, taken from the periods the tenant already authored. This is deliberately NOT a
        // hardcoded list; when no country-scoped period exists yet the list is empty and NOT ready, and the form says
        // so rather than offering values the platform may refuse.
        model.CountryOptions = periods
            .Select(p => p.CountryScope)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(c => c, StringComparer.Ordinal)
            .Select(c => new CycleCapacityCountryOptionViewModel { Value = c, Label = c })
            .ToList();

        model.CountryReady = model.CountryOptions.Count > 0;
    }

    private async Task<CycleCapacityDetailApiModel?> LoadDetailAsync(Guid cycleCapacityId, CancellationToken ct)
    {
        var response = await SendGatewayAsync(
            HttpMethod.Get, $"/api/crm/cycle-capacities/{cycleCapacityId}", null, ct);
        if (response is null || !response.IsSuccessStatusCode)
        {
            TempData["ErrorMessage"] = cycleCapacityId.ToString();
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer
            .Deserialize<CycleCapacityGatewayResponse<CycleCapacityDetailApiModel>>(body, _json)?.Data;
    }

    /// <summary>The 1:1 lookup behind the deep link. A 404 means "not yet" and is NOT logged as a failure.</summary>
    private async Task<CycleCapacityDetailApiModel?> LoadByCyclePeriodAsync(Guid cyclePeriodId, CancellationToken ct)
    {
        var response = await SendGatewayAsync(
            HttpMethod.Get, $"/api/crm/cycle-capacities/by-cycle-period/{cyclePeriodId}", null, ct);
        if (response is null || !response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer
            .Deserialize<CycleCapacityGatewayResponse<CycleCapacityDetailApiModel>>(body, _json)?.Data;
    }

    /// <summary>
    /// The estimate for the Details page. An unresolved answer arrives as 503 with a BODY, and that body is exactly
    /// what the page needs in order to explain itself — so the envelope is read whatever the status, and only a
    /// completely unreadable response yields null.
    /// </summary>
    private async Task<CycleCapacityCalculationViewModel?> LoadCalculationAsync(
        Guid cycleCapacityId, CancellationToken ct)
    {
        var response = await SendGatewayAsync(
            HttpMethod.Get, $"/api/crm/cycle-capacities/{cycleCapacityId}/calculation", null, ct);
        if (response is null)
        {
            return null;
        }

        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer
                .Deserialize<CycleCapacityGatewayResponse<CycleCapacityCalculationViewModel>>(body, _json)?.Data;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Cycle capacity calculation payload could not be read.");
            return null;
        }
    }

    /// <summary>The period picker's source — the CyclePeriod selector, consumed READ-ONLY. Closed periods are included
    /// so an existing capacity can still show the period it belongs to; the runtime refuses a WRITE against a closed
    /// one, so the list never becomes a way around that rule.</summary>
    private async Task<List<CycleCapacityPeriodSelectorItemApiModel>> LoadPeriodSelectorAsync(CancellationToken ct)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, "/api/crm/cycle-periods/selector", null, ct);
        if (response is null || !response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Cycle period selector could not be loaded; rendering the form without it.");
            return [];
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        var api = JsonSerializer
            .Deserialize<CycleCapacityGatewayResponse<CycleCapacityPeriodSelectorApiModel>>(body, _json)?.Data;

        return api?.Items ?? [];
    }

    /// <summary>The configured defaults a new capacity is born with, so the create form shows the SAME numbers the
    /// server will write instead of hardcoding its own.</summary>
    private async Task<CycleCapacityDefaultsApiModel?> LoadDefaultsAsync(CancellationToken ct)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, "/api/crm/cycle-capacities/contract", null, ct);
        if (response is null || !response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer
            .Deserialize<CycleCapacityGatewayResponse<CycleCapacityContractApiModel>>(body, _json)?.Data?.Defaults;
    }

    /// <summary>Surfaces the runtime's own refusal verbatim. The month-window and duplicate messages name the offending
    /// month or period, and flattening them into "save failed" would take away the only thing an author can act on.
    /// </summary>
    private async Task AddGatewayErrorsAsync(HttpResponseMessage? response, CancellationToken ct)
    {
        if (response is null)
        {
            ModelState.AddModelError(string.Empty, "Gateway unavailable.");
            return;
        }

        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            var envelope = JsonSerializer.Deserialize<CycleCapacityGatewayResponse<object>>(body, _json);
            if (envelope?.Errors is { Count: > 0 })
            {
                foreach (var error in envelope.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                return;
            }
        }
        catch (JsonException)
        {
            // fall through to the status-only message
        }

        ModelState.AddModelError(string.Empty, $"HTTP {(int)response.StatusCode}");
    }

    // ---------------- proxy helpers ----------------

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
            _logger.LogError(ex, "Cycle capacity Gateway request failed: {Method} {Path}", method, path);
            return null;
        }
    }

    private static async Task<IActionResult> ToProxyResultAsync(HttpResponseMessage? response, CancellationToken ct)
    {
        if (response is null)
        {
            return new ObjectResult(new { errors = new[] { "Gateway unavailable." } }) { StatusCode = 502 };
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

    private IActionResult? RequirePage(string permission, params string[] fallbacks) =>
        HasAnyPermission([permission, .. fallbacks]) ? null : StatusCode(StatusCodes.Status403Forbidden);

    private IActionResult? RequireJson(string permission, params string[] fallbacks) =>
        HasAnyPermission([permission, .. fallbacks])
            ? null
            : StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." });
}

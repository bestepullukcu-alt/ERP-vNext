using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers.CRM;
using Diten.Web.Models.CRM;
using Diten.Web.Views.CRM.CyclePeriods;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Lists;

/// <summary>
/// WP-CYC-UI-FIX-2 — the six live-check (E4) findings on the Cycle Periods and Cycle Capacity screens, measured on the
/// real rules, controllers, views, scripts and resx files:
/// <list type="number">
/// <item>"today's active periods" come from the list rows, at EVERY scope level (not only tenant-wide);</item>
/// <item>the timeline is centred on today the first time it is visible (physical delta → right-to-left safe);</item>
/// <item>"Loading" carries its Turkish diacritic and exists in all seven languages; country names in the UI language;</item>
/// <item>planning-session statuses are localised (an unknown one says "unknown", never the raw code);</item>
/// <item>the fixed-daily-charge label follows the visit model (legacy: travel, report, quiz);</item>
/// <item>both modules format days through ONE formatter.</item>
/// </list>
/// </summary>
public sealed class CycleScreensE4FixTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
    private static readonly DateOnly Today = new(2026, 10, 6);

    private static CyclePeriodRow Row(string code, string start, string end, string status, string scope = "country", string? scopeRef = "TR")
        => new(Guid.NewGuid(), code, code + " name", 2026, 1, DateOnly.Parse(start), DateOnly.Parse(end), scope,
            scope == "tenant" ? null : scopeRef, status);

    // ============================================================ 1 · today's active periods

    [Fact]
    public void Todays_active_periods_include_every_scope_level_and_only_active_periods_covering_today()
    {
        var rows = new[]
        {
            Row("tr-2026-q4", "2026-10-01", "2026-12-31", "active"),                          // country, covers today
            Row("gm-2026-02", "2026-07-01", "2026-12-31", "active", scope: "tenant"),         // tenant, covers today
            Row("de-2026-q4", "2026-10-01", "2026-12-31", "draft", scopeRef: "DE"),           // draft
            Row("tr-2026-q3", "2026-07-01", "2026-09-30", "closed"),                          // closed, ended
            Row("fr-2026-q3", "2026-07-01", "2026-09-30", "active", scopeRef: "FR"),          // active, does not cover today
            Row("es-2027-q1", "2027-01-01", "2027-03-31", "active", scopeRef: "ES"),          // active, future
            Row("tr-closed-now", "2026-10-01", "2026-12-31", "closed")                        // closed, covers today
        };

        var active = CyclePeriodScreenRules.ActiveOn(rows, Today);

        // Tenant-wide first (scope precedence), then the country one — the one the unit-less resolve missed.
        Assert.Equal(["gm-2026-02", "tr-2026-q4"], active.Select(a => a.CycleCode).ToArray());
        var tr = active.Single(a => a.CycleCode == "tr-2026-q4");
        Assert.Equal("country", tr.ScopeType);
        Assert.Equal("TR", tr.ScopeRef);
        Assert.Null(active.Single(a => a.ScopeType == "tenant").ScopeRef);
    }

    [Fact]
    public void A_period_is_active_today_on_its_first_and_last_day()
    {
        var rows = new[] { Row("first", "2026-10-06", "2026-10-31", "active"), Row("last", "2026-09-01", "2026-10-06", "active", scopeRef: "DE") };
        Assert.Equal(2, CyclePeriodScreenRules.ActiveOn(rows, Today).Count);
        Assert.Empty(CyclePeriodScreenRules.ActiveOn(rows, Today.AddDays(30)));
    }

    [Fact]
    public async Task The_overview_returns_todays_active_periods_from_the_rows_it_already_read_unfiltered()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = today.AddDays(-5).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var end = today.AddDays(30).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var gateway = new Gateway(uri => uri.EndsWith("/api/crm/cycle-periods")
            ? (HttpStatusCode.OK, $$$"""
                {"isSuccessful":true,"data":{"totalCount":2,"items":[
                  {"cyclePeriodId":"{{{Guid.NewGuid()}}}","cycleCode":"tr-now","cycleName":"TR","year":{{{today.Year}}},"sequenceInYear":1,
                   "startDate":"{{{start}}}T00:00:00+00:00","endDate":"{{{end}}}T00:00:00+00:00","scopeType":"country","scopeRef":"TR","cycleStatus":"active"},
                  {"cyclePeriodId":"{{{Guid.NewGuid()}}}","cycleCode":"de-draft","cycleName":"DE","year":{{{today.Year}}},"sequenceInYear":1,
                   "startDate":"{{{start}}}T00:00:00+00:00","endDate":"{{{end}}}T00:00:00+00:00","scopeType":"country","scopeRef":"DE","cycleStatus":"draft"}]}}
                """)
            : (HttpStatusCode.NotFound, ""));
        var controller = Controller(gateway, "crm.cycle-period.read");

        // A status filter of "draft" narrows the TIMELINE — not "what is in force today".
        var result = Assert.IsType<JsonResult>(await controller.Overview(null, null, null, ["draft"], CancellationToken.None));

        var data = JsonDocument.Parse(JsonSerializer.Serialize(result.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web))).RootElement.GetProperty("data");
        var todayActive = data.GetProperty("todayActive");
        Assert.Equal("tr-now", Assert.Single(todayActive.EnumerateArray()).GetProperty("cycleCode").GetString());
        // One read of the list, nothing else (the unit-less resolve-active call is gone).
        Assert.Equal(["/api/crm/cycle-periods"], gateway.Requests.Select(u => new Uri(u).AbsolutePath).ToArray());
    }

    [Fact]
    public void The_today_box_renders_the_overview_answer_as_plain_text_and_links_the_finder()
    {
        var script = Script("CyclePeriods", "index.js");
        Assert.DoesNotContain("periods/resolve-active?at=", script);
        Assert.DoesNotContain("refreshCurrentPeriod", script);
        Assert.Contains("renderTodayActive(data?.todayActive || []);", script);
        Assert.Contains("link.textContent = `${scope} · ${item.cycleCode}`;", script);
        Assert.Contains("getElementById('todayFinderLink')", script);

        var index = View("CyclePeriods", "Index.cshtml");
        Assert.Contains("@Localizer[\"TodayActivePeriods\"]", index);
        Assert.Contains("id=\"todayActivePeriods\"", index);
        Assert.Contains("id=\"todayFinderLink\"", index);
        Assert.DoesNotContain("currentPeriodBadge", index);
    }

    // ============================================================ 2 · timeline centred on today

    [Fact]
    public void The_timeline_centres_today_with_a_physical_delta_when_first_visible()
    {
        var shared = Script("CyclePeriods", "shared.js");
        // delta = centre of today's line − centre of the visible area (physical pixels; + scrolls right).
        Assert.Contains("Math.round((lineRect.left + lineRect.width / 2) - (scrollerRect.left + scrollerRect.width / 2))", shared);

        var index = Script("CyclePeriods", "index.js");
        Assert.Contains("const delta = S.timelineCentreDelta(todayLine.getBoundingClientRect(), scroller.getBoundingClientRect());", index);
        // scrollBy moves by the same PHYSICAL distance in a right-to-left page — no negative-scrollLeft branch.
        Assert.Contains("scroller.scrollBy({ left: delta, behavior: 'auto' });", index);
        Assert.DoesNotContain("scrollLeft =", index);
        // Hidden views have no width: centre after a draw AND when the view is switched on, once per draw.
        Assert.Contains("if (timeline) centreTimelineOnToday();", index);
        Assert.Matches(new Regex(@"grid\.innerHTML = head \+ rows;\s*timelineCentred = false;\s*centreTimelineOnToday\(\);"), index);
        Assert.Contains("scroller.clientWidth === 0) return;", index);
    }

    [Theory]
    [InlineData(800.0, 4.0, 100.0, 600.0, 402)]    // LTR: today far right of the 600px window → scroll right
    [InlineData(100.0, 4.0, 100.0, 600.0, -298)]   // today left of centre → scroll left (or stay at the start)
    [InlineData(398.0, 4.0, 100.0, 600.0, 0)]      // already centred
    public void The_centre_delta_arithmetic(double lineLeft, double lineWidth, double scrollerLeft, double scrollerWidth, int expected)
    {
        // The same expression shared.js evaluates (asserted verbatim above), worked here on sample rects.
        var delta = (int)Math.Round((lineLeft + lineWidth / 2) - (scrollerLeft + scrollerWidth / 2), MidpointRounding.AwayFromZero);
        Assert.Equal(expected, delta);
        var today = CyclePeriodScreenRules.BuildTimeline([], null, Today).TodayPct;
        Assert.NotNull(today); // the server always places today on a this-year ± 1 axis
    }

    // ============================================================ 3 · language

    [Fact]
    public void Loading_exists_in_seven_languages_and_turkish_keeps_its_diacritic()
    {
        foreach (var language in Languages)
        {
            var shared = Resx(Path.Combine("Resources", $"SharedResource.{language}.resx"));
            Assert.True(shared.TryGetValue("Loading", out var text) && !string.IsNullOrWhiteSpace(text), $"{language}: Loading missing");
        }

        Assert.Equal("Yükleniyor...", Resx(Path.Combine("Resources", "SharedResource.tr.resx"))["Loading"]);
        Assert.DoesNotContain("Yukleniyor", File.ReadAllText(Path.Combine(WebRoot(), "Resources", "SharedResource.tr.resx")));
    }

    [Fact]
    public void Country_options_are_named_in_the_ui_language_with_the_code_kept()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");
            var tr = CycleCountryLabel.For("tr", "Turkey");
            Assert.EndsWith("(TR)", tr);
            Assert.DoesNotContain("Turkey", tr);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }

        // A code ICU does not know keeps the reference-set label — never a guess.
        Assert.Equal("Somewhere", CycleCountryLabel.For("QQ", "Somewhere"));
        Assert.Equal("QQ", CycleCountryLabel.For("qq"));

        Assert.Contains("Label = CycleCountryLabel.For(o.Value, o.Label)", File.ReadAllText(Path.Combine(WebRoot(), "Controllers", "CRM", "CyclePeriodsController.cs")));
        Assert.Contains("Label = CycleCountryLabel.For(c)", File.ReadAllText(Path.Combine(WebRoot(), "Controllers", "CRM", "CycleCapacitiesController.cs")));
    }

    // ============================================================ 4 · session statuses

    [Theory]
    [InlineData("draft", "SessionStatus_draft")]
    [InlineData("generated", "SessionStatus_generated")]
    [InlineData("Committed ", "SessionStatus_committed")]
    [InlineData("ARCHIVED", "SessionStatus_archived")]
    [InlineData("exploded", "SessionStatus_unknown")]
    [InlineData("", "SessionStatus_unknown")]
    [InlineData(null, "SessionStatus_unknown")]
    public void Session_statuses_map_to_localised_keys_and_unknown_never_shows_the_raw_code(string? status, string key)
    {
        Assert.Equal(key, CycleCapacitySessionStatus.LabelKey(status));
    }

    [Fact]
    public void Session_status_keys_exist_in_seven_languages_and_details_uses_them()
    {
        foreach (var language in Languages)
        {
            var values = CapacityResx(language);
            foreach (var key in CycleCapacitySessionStatus.Known.Select(s => "SessionStatus_" + s).Append(CycleCapacitySessionStatus.UnknownKey))
            {
                Assert.True(values.TryGetValue(key, out var text) && !string.IsNullOrWhiteSpace(text) && text != key, $"{language}: {key}");
            }
        }

        Assert.Equal("Bilinmiyor", CapacityResx("tr")["SessionStatus_unknown"]);
        Assert.Equal("Uygulandı", CapacityResx("tr")["SessionStatus_committed"]);
        var details = View("CycleCapacities", "Details.cshtml");
        Assert.Contains("@Localizer[CycleCapacitySessionStatus.LabelKey(s.Status)]", details);
        Assert.DoesNotContain("@(s.Status ?? \"—\")", details);
    }

    // ============================================================ 5 · fixed daily charge label

    [Theory]
    [InlineData("legacy", "WfDailyFixedMinutesLegacy")]
    [InlineData("typical", "WfDailyFixedMinutes")]
    [InlineData(null, "WfDailyFixedMinutes")]
    public void The_fixed_daily_charge_label_follows_the_visit_model(string? visitModel, string key)
    {
        Assert.Equal(key, CycleCapacityWaterfall.DailyFixedLabelKey(visitModel));

        var calc = new CycleCapacityCalculationViewModel
        {
            Resolution = "resolved",
            VisitModel = visitModel ?? string.Empty,
            TypicalVisitMinutes = 4,
            Totals = new CycleCapacityCalculationTotalsViewModel { WorkingDays = 65, AvailableMinutes = 27_360, DailyFixedMinutes = 1_140, Visits = 6_543, AverageFte = 1m }
        };
        Assert.Equal(key, CycleCapacityWaterfall.From(calc)!.Steps[4].LabelKey);
        Assert.Equal(key, CycleCapacitySummary.Build(calc).DailyFixedLabelKey);
    }

    [Fact]
    public void The_legacy_and_typical_labels_say_what_the_charge_is_made_of_in_seven_languages()
    {
        foreach (var language in Languages)
        {
            var values = CapacityResx(language);
            Assert.NotEqual(values["WfDailyFixedMinutes"], values["WfDailyFixedMinutesLegacy"]);
            Assert.False(string.IsNullOrWhiteSpace(values["UnitMinutesPerDay"]));
        }

        Assert.Equal("Günlük sabit işler (yol, rapor, sınav)", CapacityResx("tr")["WfDailyFixedMinutesLegacy"]);
        Assert.Equal("Günlük sabit işler (yol, sınav)", CapacityResx("tr")["WfDailyFixedMinutes"]);

        // The edit form's live summary uses the server's key for the model it computed.
        var form = Script("CycleCapacities", "form.js");
        Assert.Contains("fixedLabel.textContent = L[summary.dailyFixedLabelKey];", form);
        Assert.Contains("@Localizer[CycleCapacityWaterfall.DailyFixedLabelKey(Model.VisitModel)]", View("CycleCapacities", "_Form.cshtml"));
        Assert.Contains("WfDailyFixedMinutesLegacy = Localizer[\"WfDailyFixedMinutesLegacy\"].Value", View("CycleCapacities", "_IndexL10n.cshtml"));
    }

    // ============================================================ 6 · one date formatter

    [Fact]
    public void Both_modules_format_days_through_the_one_cycle_date_formatter()
    {
        var formatter = File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "cycle-dates.js"));
        Assert.Contains("const DAY = { year: 'numeric', month: 'short', day: '2-digit', timeZone: 'UTC' };", formatter);
        Assert.Contains("window.CycleDates = { locale, toDate, day, stamp, formatAll };", formatter);

        var shared = Script("CyclePeriods", "shared.js");
        Assert.Contains("const day = D.day;", shared);
        Assert.DoesNotContain("toLocaleDateString(locale, { year: 'numeric', month: 'short'", shared);
        Assert.Contains("const day = window.CycleDates.day;", Script("CycleCapacities", "index.js"));

        foreach (var (module, files) in new[]
                 {
                     ("CyclePeriods", new[] { "Index.cshtml", "Details.cshtml" }),
                     ("CycleCapacities", new[] { "Index.cshtml", "Create.cshtml", "Edit.cshtml", "Details.cshtml" })
                 })
        {
            foreach (var file in files)
            {
                var view = View(module, file);
                var formatterAt = view.IndexOf("CRM/cycle-dates.js", StringComparison.Ordinal);
                Assert.True(formatterAt >= 0, $"{module}/{file} does not load cycle-dates.js");
                // loaded before the module's own scripts, which use it
                var firstModuleScript = view.IndexOf($"js/CRM/{module}/", StringComparison.Ordinal);
                Assert.True(firstModuleScript < 0 || formatterAt < firstModuleScript, $"{module}/{file}: cycle-dates.js must come first");
            }
        }

        // No server-side short-date format is left on the capacity pages ("1.10.2026").
        foreach (var file in new[] { "Details.cshtml", "_Form.cshtml" })
        {
            var view = View("CycleCapacities", file);
            Assert.DoesNotContain("ToString(\"d\"", view);
            Assert.Contains("data-day=", view);
        }
    }

    // ============================================================ helpers

    private static CyclePeriodsController Controller(Gateway gateway, params string[] permissions)
    {
        var claims = new List<Claim> { new("tenantId", "77777777-7777-7777-7777-777777777777") };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        return new CyclePeriodsController(
            new HttpClient(gateway),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = "http://gateway.test" }).Build(),
            NullLogger<CyclePeriodsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
            }
        };
    }

    private sealed class Gateway(Func<string, (HttpStatusCode Status, string Body)> route) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!.ToString();
            Requests.Add(uri);
            var (status, body) = route(uri);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private static string View(string module, string file) => File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", module, file));

    private static string Script(string module, string file) => File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", module, file));

    private static Dictionary<string, string> CapacityResx(string language)
        => Resx(Path.Combine("Resources", "Views", "CRM", "CycleCapacities", $"CycleCapacitiesIndex.{language}.resx"));

    private static Dictionary<string, string> Resx(string relative)
        => XDocument.Load(Path.Combine(WebRoot(), relative)).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => ((string?)d.Element("value") ?? string.Empty).Trim(), StringComparer.Ordinal);

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }
}

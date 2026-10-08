using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers.CRM;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VP-4B — the Visit Planning screen (1/3) on the REAL views, scripts, resx and controller: the list (targets and
/// weeks columns from the DTO, empty-draft badge, inactive Team switch), the new-plan drawer (no segment / strategy, no
/// past week, 409 → go to plan), the week-aware detail header (status-based actions, reopen ≥ 10 characters through the
/// reopen proxy, capacity cards instead of supply / demand), the page skeleton and the untouched Route code.
/// </summary>
public sealed class VisitPlanningScreenWebTests
{
    private static readonly string[] Cultures = ["", "en", "tr", "fr", "es", "zh", "ar", "ru"];

    // ── 1 · list ────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_list_reads_targets_and_weeks_from_the_dto_badges_empty_drafts_and_keeps_team_inactive()
    {
        var js = Script("index.js");
        var targets = Function(js, "sTargets");
        Assert.Contains("s.doctorCount ?? s.selectedContactCount", targets);
        Assert.Contains("s.pharmacyCount ?? s.selectedPharmacyCount", targets);

        var weeks = Function(js, "sWeeks");
        Assert.Contains("s.approvedWeekCount", weeks);
        Assert.Contains("s.draftWeekCount", weeks);
        Assert.Contains("L.WeekCountFormat", weeks);
        Assert.Contains("L.ApprovedWeekCountFormat", weeks); // before WP-VP-4A: "X approved" only

        Assert.Contains("s.isEmpty === true", Function(js, "isEmptyDraft"));
        Assert.Contains("L.EmptyDraftBadge", Function(js, "statusCell"));
        Assert.Contains("L.LegacyPlanBadge", Function(js, "statusCell"));
        Assert.Contains("{ targets: 5, visible: viewMode !== 'mine'", js); // the rep column is hidden in "my plans" (4J: + select column)
        Assert.Contains("requestedStatus: 'archived'", js);                 // delete empty drafts = the 3A archive
        Assert.Contains("window.VisitPlanningNewPlan.open()", js);

        var index = View("Index.cshtml");
        Assert.Matches(@"id=""vp-view-team"" disabled aria-disabled=""true""", index);
        Assert.Contains("Localizer[\"TeamViewSoon\"]", index);
        Assert.Contains("Localizer[\"MyPlans\"]", index);
        Assert.Contains("<th>@Localizer[\"WeeksSection\"]</th>", View("_DataTable.cshtml"));
        Assert.Equal("Benim planlarım", Resx("tr")["SessionsTitle"]);
        Assert.Equal("{0} onaylı · {1} taslak", Resx("tr")["WeekCountFormat"]);
    }

    // ── 2 · new-plan drawer ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_new_plan_drawer_has_no_segment_or_strategy_never_offers_a_past_week_and_answers_409_with_go_to_plan()
    {
        var drawer = View("_NewPlanDrawer.cshtml");
        Assert.Contains("offcanvas offcanvas-end", drawer);
        Assert.DoesNotMatch("(?i)segment|strateg", drawer);
        Assert.Contains("id=\"vp-np-rep\" class=\"form-control\" disabled aria-readonly=\"true\"", drawer); // 4J: looks read-only too
        Assert.Contains("Localizer[\"GoToPlan\"]", drawer);

        var js = Script("new-plan.js");
        Assert.DoesNotMatch("(?i)segment|strateg", js);
        var weeks = Function(js, "openingWeeks");
        Assert.Contains("if (sun < now) continue; // already over (E7-B5)", weeks);
        Assert.Contains("p.end && p.end >= now", Function(js, "openPeriods"));
        Assert.Contains("r.status === 409 && errors[0] === 'planning_session_exists'", js);
        Assert.Contains("showExists((r.body && r.body.data) || errors[2])", js);
        Assert.Contains("'/CRM/VisitPlanning/Details/' + encodeURIComponent(id)", js);

        // Both pages offer it, behind the generate key.
        foreach (var page in new[] { "Index.cshtml", "Details.cshtml" })
        {
            var view = View(page);
            var at = view.IndexOf("_NewPlanDrawer.cshtml", StringComparison.Ordinal);
            Assert.True(at > 0 && view.LastIndexOf("@if (Model.CanGenerate)", at, StringComparison.Ordinal) > at - 200, page);
        }
    }

    // ── 3 · detail actions follow the selected week ────────────────────────────────────────────────────────────

    [Fact]
    public void The_header_offers_only_the_selected_weeks_actions()
    {
        var page = Script("page.js");
        Assert.Contains("draft: Object.freeze(['saveTargets', 'generateRoute', 'approveWeek']),", page);
        Assert.Contains("empty: Object.freeze(['saveTargets', 'generateRoute']),", page);
        Assert.Contains("approved: Object.freeze(['reopenWeek', 'nextWeek']),", page);
        Assert.Contains("past: Object.freeze([]),", page);
        Assert.Contains("legacy: Object.freeze([])", page);
        Assert.Contains("(isLegacy ? ACTIONS.legacy : (ACTIONS[status] || ACTIONS.past))", page);

        var header = Script("header.js");
        Assert.Contains("const offered = page.actionsFor(status, legacy);", header);
        Assert.Contains("item.classList.toggle('d-none', !show);", header);
        Assert.Contains("L.LegacyPlanBand", header);
        Assert.Contains("L.PastWeekBand", header);
        Assert.Contains("s.currentWeekStart", Function(header, "defaultWeek"));   // WP-VP-4A, with a fallback
        Assert.Contains("s.nextDraftWeekStart", Function(header, "nextOpenWeek")); // WP-VP-4A, with a fallback

        // Every action button names its key and starts hidden until the week is known.
        var details = View("Details.cshtml");
        foreach (var key in new[] { "approveWeek", "reopenWeek", "nextWeek", "saveTargets", "generateRoute" })
        {
            Assert.Matches($@"<li class=""[^""]*d-none""[^>]*data-vp-action-item>\s*<button[^>]*data-vp-action=""{key}""", details);
        }
    }

    // ── 4 · reopen ──────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Reopen_needs_ten_characters_and_posts_to_the_reopen_proxy()
    {
        var header = Script("header.js");
        Assert.Contains("const REOPEN_MIN = 10;", header);
        var confirm = Function(header, "confirmReopen");
        Assert.Contains("if (!reasonOk(reason)) return; // never sent below the minimum", confirm);
        Assert.Contains("'/sessions/' + encodeURIComponent(sessionId) + '/weeks/' + encodeURIComponent(ws) + '/reopen'", confirm);
        Assert.Contains("JSON.stringify({ reason: reason, expectedVersion: s.version })", confirm);
        Assert.Contains("L.ReopenUnavailable", confirm); // a 404 without a body (proxy not there before WP-VP-4A)
        Assert.Contains("btn.disabled = !ok;", Function(header, "syncReopen"));

        var details = View("Details.cshtml");
        var modal = details.IndexOf("id=\"vp-reopen-modal\"", StringComparison.Ordinal);
        Assert.True(modal > 0 && details.LastIndexOf("@if (Model.CanApply)", modal, StringComparison.Ordinal) > modal - 300);
        Assert.Contains("id=\"vp-reopen-confirm\" disabled", details);
        Assert.Equal("En az 10 karakter. Gerekçe haftanın geçmişine kaydedilir.", Resx("tr")["ReopenReasonHint"]);
    }

    // ── 5 · capacity cards ──────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Capacity_cards_read_week_and_period_capacity_and_the_supply_demand_tiles_are_gone()
    {
        var details = View("Details.cshtml");
        foreach (var id in new[] { "vp-cap-week", "vp-cap-week-planned", "vp-cap-period", "vp-cap-period-planned", "vp-cap-period-bar" })
        {
            Assert.Contains($"id=\"{id}\"", details);
        }

        Assert.DoesNotContain("vp-sd-", details);
        Assert.DoesNotContain("SupplyLabel", details);
        Assert.DoesNotContain("vp-sd-", Script("details.js"));

        var capacity = Function(Script("header.js"), "renderCapacity");
        Assert.Contains("p.weekCapacity", capacity);
        Assert.Contains("p.periodCapacity", capacity);
        Assert.Contains("hours(wc.capacityMinutes)", capacity);
        Assert.Contains("pc.budgetSource === 'default_hours'", capacity);
    }

    // ── 6 · texts ───────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Every_new_text_is_in_seven_languages_and_the_turkish_is_real_turkish()
    {
        var keys = new[]
        {
            "MyPlans", "TeamView", "TeamViewSoon", "WeeksSection", "WeekCountFormat", "ApprovedWeekCountFormat", "EmptyDraftBadge",
            "LegacyPlanBadge", "EmptyDraftsNotice", "DeleteEmptyDrafts", "DeleteEmptyDraftsConfirm", "EmptyDraftsDeleted",
            "ListEmptyTitle", "ListEmptyAction", "ListLoadError", "NoActivePeriodBand", "NewPlanTitle", "NewPlanPeriodHint",
            "NewPlanNoPeriod", "NewPlanWeek", "NewPlanWeekHint", "NewPlanCreate", "PlanExists", "GoToPlan", "ApproveWeek",
            "ApproveWeekHint", "ReopenWeek", "NextWeek", "WeekStatusPast", "WeekStatusApproved", "WeekStatusDraft", "WeekStatusEmpty",
            "PastWeekBand", "LegacyPlanBand", "WeekCapacity", "WeekPlanned", "PeriodCapacity", "PeriodPlanned", "HoursFormat",
            "CapacityDefaultHours", "ReopenTitle", "ReopenBody", "ReopenReasonLabel", "ReopenReasonHint", "ReopenConfirm",
            "ReopenDone", "ReopenUnavailable", "DetailLoadError", "ViewSwitchLabel"
        };
        foreach (var culture in Cultures)
        {
            var resx = Resx(culture);
            Assert.All(keys, k => Assert.True(resx.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v), $"{culture}:{k}"));
        }

        var tr = Resx("tr");
        var en = Resx("en");
        Assert.All(keys.Where(k => k != "HoursFormat"), k => Assert.NotEqual(k, tr[k]));
        Assert.All(keys.Where(k => k is not ("HoursFormat")), k => Assert.NotEqual(en[k], tr[k]));
        Assert.Equal("Haftayı yeniden aç", tr["ReopenWeek"]);
        Assert.Equal("Bu haftanın kapasitesi", tr["WeekCapacity"]);

        // Every key the new scripts read is on the bridge.
        var bridge = View("_IndexL10n.cshtml");
        var scripts = Script("header.js") + Script("new-plan.js") + Script("index.js") + Script("page.js");
        var read = Regex.Matches(scripts, @"(?<![\w.])L\.([A-Z][A-Za-z0-9_]*)").Select(m => m.Groups[1].Value)
            .Concat(new[] { "WeekStatusPast", "WeekStatusApproved", "WeekStatusDraft", "WeekStatusEmpty" }).Distinct();
        Assert.All(read, k => Assert.Matches($@"(?<![A-Za-z0-9_]){Regex.Escape(k)}\s*=", bridge));

        // The confirm dialogs on this screen carry no generic "are you sure?" sentence.
        Assert.Contains("confirmButtonText: L.ApplyConfirmButton, subtext: ''", Script("details.js"));
        Assert.Contains("confirmButtonText: L.ArchiveEmptyDrafts, subtext: ''", Script("index.js")); // 4J: archive, not delete
    }

    // ── 7 · the Route code only joined the skeleton ────────────────────────────────────────────────────────────

    [Fact]
    public void The_route_code_is_untouched_apart_from_the_skeleton_wiring()
    {
        var details = Script("details.js");
        // the Route's own machinery is still there, unchanged
        Assert.Contains("const renderDay = ", details);
        Assert.Contains("if (window.Sortable && el('vp-visit-cards') && !readOnly)", details);
        Assert.Contains("draggable: '.vp-tl-row--account', filter: '.vp-tl-row--pharmacy', handle: '.vp-block-handle'", details);
        Assert.Contains("const buildWeekSelector = () => {", details);
        // it talks to the page only through the skeleton
        Assert.Contains("const page = window.VisitPlanningPage || null;", details);
        Assert.Contains("page.setSession(sessionData)", details);
        Assert.Contains("page.setPreview(p)", details);
        Assert.Contains("page.on('week-change'", details);
        Assert.Contains("page.on('request:approve-week', () => apply());", details);

        var view = View("Details.cshtml");
        foreach (var id in new[] { "vp-day-tabs", "vp-route-toggles", "vp-route-view", "vp-visit-cards", "vp-map-panel", "vp-week" })
        {
            Assert.Contains($"id=\"{id}\"", view);
        }

        // Script order: skeleton → Targets + Route → header.
        var skeleton = view.IndexOf("VisitPlanning/page.js", StringComparison.Ordinal);
        var route = view.IndexOf("VisitPlanning/details.js", StringComparison.Ordinal);
        var header = view.IndexOf("VisitPlanning/header.js", StringComparison.Ordinal);
        Assert.True(skeleton > 0 && skeleton < route && route < header);
        // WP-VP-4D opened the Weeks tab (it no longer waits for a package).
        Assert.Contains("role=\"presentation\" id=\"vp-tab-weeks-item\">", view);
        Assert.DoesNotContain("data-pending-package", view);
    }

    // ── unauthorised: no skeleton, no redirect ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Without_the_read_key_the_list_and_the_detail_render_nothing_and_do_not_redirect()
    {
        var controller = new VisitPlanningController(new HttpClient(new NoCall()), new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = "http://gateway.test" }).Build(),
            NullLogger<VisitPlanningController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("tenantId", Guid.NewGuid().ToString()) }, "test")) }
        };

        Assert.Equal(403, Assert.IsType<StatusCodeResult>(controller.Index()).StatusCode);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(await controller.Details(Guid.NewGuid(), default)).StatusCode);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────────────

    private sealed class NoCall : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => throw new InvalidOperationException("no Gateway call is expected without the read key");
    }

    /// <summary>The body of `const name = … => { … };` (to the next line that closes at the same indent).</summary>
    private static string Function(string js, string name)
    {
        var m = Regex.Match(js, @"const " + Regex.Escape(name) + @" = .*?\n    \};|const " + Regex.Escape(name) + @" = [^\n]*;", RegexOptions.Singleline);
        Assert.True(m.Success, name);
        return m.Value;
    }

    private static string Script(string file) => File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "VisitPlanning", file));
    private static string View(string file) => File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "VisitPlanning", file));

    private static Dictionary<string, string> Resx(string culture)
        => XDocument.Load(Path.Combine(WebRoot(), "Resources", "Views", "CRM", "VisitPlanning",
                "VisitPlanningIndex" + (culture.Length == 0 ? "" : "." + culture) + ".resx"))
            .Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }
}

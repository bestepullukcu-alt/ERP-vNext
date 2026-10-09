using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Diten.Web.Controllers.CRM;
using Diten.Web.Models.CRM;
using Diten.Web.Views.CRM.VisitWorkspace;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W2 · W2-WEB-a — on the REAL controller, view, resx and scripts: (1) the page draws only the access notice
/// without the read keys; (2) the ten work statuses → card look; (3) the countdown from reportDeadline; (4) the E2 rules
/// (reasons by appliesTo, requiresNote ⇒ note required, reschedule options, codes → text) and the TWO-step save; (5) every
/// proxy → its CRM path with the CRM endpoint's keys; (6) seven languages, no key echo, the menu key; (7) the phone day
/// list. The pure rules run in Node (workspace-core.js); without Node the source assertions stand alone.
/// </summary>
public sealed class VisitWorkspaceWebTests
{
    private const string GatewayUrl = "http://gateway.test";
    private static readonly Guid TenantId = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
    private static readonly string[] Cultures = ["", "en", "tr", "fr", "es", "zh", "ar", "ru"];
    private static readonly string[] AllKeys = VisitWorkspaceIndex.ScriptKeys.Concat(VisitWorkspaceIndex.ViewKeys).ToArray();

    private const string ReportRead = "crm.visit-report.read";
    private const string ReportRecord = "crm.visit-report.record";
    private const string PlanRead = "crm.visit-plan.read";
    private const string PlanApply = "crm.visit-plan.apply";
    private const string Manage = "crm.planned-visit.manage";
    private const string ContactRead = "crm.contact.read";

    // ═══ 1 · UAS-001 ════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void A1_without_both_read_keys_the_page_draws_only_the_access_notice()
    {
        Assert.False(Model(Arrange(ReportRead).Controller.Index()).CanRead);
        Assert.False(Model(Arrange(PlanRead).Controller.Index()).CanRead);
        var reader = Model(Arrange(ReportRead, PlanRead).Controller.Index());
        Assert.True(reader.CanRead);
        Assert.False(reader.CanRecord || reader.CanManageVisits || reader.CanApplyWeek);

        var full = Model(Arrange(ReportRead, PlanRead, ReportRecord, Manage, PlanApply, ContactRead).Controller.Index());
        Assert.True(full.CanRecord && full.CanManageVisits && full.CanApplyWeek && full.CanSearchContacts);
        Assert.False(Model(Arrange(ReportRead, PlanRead, ReportRecord).Controller.Index()).CanRecord); // record needs manage too

        var view = ViewSource();
        var denied = view.IndexOf("<partial name=\"_AccessDenied\"", StringComparison.Ordinal);
        var bail = view.IndexOf("return;", denied, StringComparison.Ordinal);
        Assert.True(denied > 0 && bail > denied);
        Assert.True(view.IndexOf("id=\"vw-root\"", StringComparison.Ordinal) > bail, "nothing of the page before the access check");
        Assert.Contains("id=\"vw-week-strip\"", view);
        Assert.Contains("id=\"vw-calendar\"", view);
        Assert.Contains("<partial name=\"_CalendarAssets\" />", view); // the vendored FullCalendar, no new library
        Assert.DoesNotContain("cdn.", view);
        Assert.DoesNotContain("pre-order", view, StringComparison.OrdinalIgnoreCase); // no pre-order button (W6)
        Assert.DoesNotContain("PreOrder", view, StringComparison.OrdinalIgnoreCase);
    }

    // ═══ 2 · status → card ═══════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void A2_all_ten_statuses_map_to_their_own_look_and_draft_is_dashed_and_faded()
    {
        var node = RunNode(Core() +
            "const out = C.STATUSES.map(s => C.statusStyle(s));\n" +
            "process.stdout.write(JSON.stringify({ all: out, unknown: C.statusStyle('bogus'), count: C.STATUSES.length }));");
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;
        Assert.Equal(10, r.GetProperty("count").GetInt32());
        var all = r.GetProperty("all").EnumerateArray().ToList();
        Assert.Equal(10, all.Select(s => s.GetProperty("cssClass").GetString()).Distinct().Count());
        Assert.All(all, s => Assert.StartsWith("bx-", s.GetProperty("icon").GetString()));
        var byCode = all.ToDictionary(s => s.GetProperty("code").GetString()!);
        Assert.Equal(
            new[] { "cancelled", "not_done", "rescheduled", "reported", "expired", "report_missing", "missed", "today", "planned", "draft" },
            byCode.Keys);
        Assert.True(byCode["draft"].GetProperty("dashed").GetBoolean() && byCode["draft"].GetProperty("faded").GetBoolean());
        Assert.True(byCode["cancelled"].GetProperty("strike").GetBoolean());
        Assert.True(byCode["expired"].GetProperty("locked").GetBoolean());
        Assert.False(byCode["planned"].GetProperty("dashed").GetBoolean());
        Assert.Equal("planned", r.GetProperty("unknown").GetProperty("code").GetString());

        var css = File.ReadAllText(Path.Combine(ScriptDir(), "visit-workspace.css"));
        Assert.All(byCode.Keys, code => Assert.Contains(".vw-st-" + code, css));
        Assert.All(byCode.Keys, code => Assert.Contains("Status_" + code, VisitWorkspaceIndex.ScriptKeys));
    }

    // ═══ 3 · countdown ═══════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void A3_the_countdown_reads_reportDeadline_at_a_fixed_clock()
    {
        var node = RunNode(Core() +
            "const at = Date.parse('2026-10-16T10:00:00Z');\n" +
            "process.stdout.write(JSON.stringify([C.countdown('2026-10-17T23:59:59Z', at), C.countdown('2026-10-16T09:00:00Z', at), C.countdown(null, at),\n" +
            "  C.showsCountdown('missed'), C.showsCountdown('report_missing'), C.showsCountdown('planned'), C.showsCountdown('expired')]));");
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement.EnumerateArray().ToList();
        Assert.Equal((37, 59, false), (r[0].GetProperty("hours").GetInt32(), r[0].GetProperty("minutes").GetInt32(), r[0].GetProperty("passed").GetBoolean()));
        Assert.True(r[1].GetProperty("passed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, r[2].ValueKind);
        Assert.Equal(new[] { true, true, false, false }, r.Skip(3).Select(x => x.GetBoolean()));
    }

    // ═══ 4 · E2 dialogs + the two-step save ══════════════════════════════════════════════════════════════════

    [Fact]
    public void A4_reasons_are_asked_by_appliesTo_and_requiresNote_makes_the_note_mandatory()
    {
        var js = Script("visit-workspace.js");
        Assert.Contains("const APPLIES = { cancel: 'cancel', notDone: 'missed', reschedule: 'reschedule' };", js);
        Assert.Contains("get('/reasons?appliesTo=' + APPLIES[kind] + '&lang=' + encodeURIComponent(lang()))", js);
        Assert.Contains("get('/reschedule-options?plannedVisitId=' + encodeURIComponent(v.plannedVisitId))", js);
        Assert.Contains("post('/planned-visits/' + encodeURIComponent(v.plannedVisitId) + '/cancel', { reasonCode: dlg.reason.code, note: note || null })", js);
        Assert.Contains("(state.contract && state.contract.maxNoteLength) || 500", js);

        var node = RunNode(Core() +
            "const other = { code: 'other', requiresNote: true }, plain = { code: 'doctor_unavailable', requiresNote: false };\n" +
            "process.stdout.write(JSON.stringify([\n" +
            "  C.noteState(other, '', 500), C.noteState(other, 'Kongre', 500), C.noteState(plain, '', 500), C.noteState(plain, 'x'.repeat(501), 500),\n" +
            "  C.canSave('notDone', { reason: other, note: '' }), C.canSave('notDone', { reason: other, note: 'Kongre' }),\n" +
            "  C.canSave('cancel', { reason: plain, note: '' }), C.canSave('reschedule', { reason: plain, note: '' }),\n" +
            "  C.canSave('reschedule', { reason: plain, note: '', date: '2026-10-20' }), C.canSave('cancel', { reason: null })]));");
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement.EnumerateArray().ToList();
        Assert.True(r[0].GetProperty("required").GetBoolean());
        Assert.False(r[0].GetProperty("valid").GetBoolean());
        Assert.True(r[1].GetProperty("valid").GetBoolean());
        Assert.Equal(6, r[1].GetProperty("length").GetInt32());
        Assert.False(r[2].GetProperty("required").GetBoolean());
        Assert.True(r[2].GetProperty("valid").GetBoolean());
        Assert.True(r[3].GetProperty("tooLong").GetBoolean());
        Assert.Equal(new[] { false, true, true, false, true, false }, r.Skip(4).Select(x => x.GetBoolean()));
    }

    [Fact]
    public void A4_not_done_and_reschedule_save_in_two_steps_and_the_second_waits_for_the_first()
    {
        var node = RunNode(Core() +
            "const run = (firstOk) => { const calls = [];\n" +
            "  const post = (path, body) => { calls.push({ path, body }); return Promise.resolve({ ok: path === '/outcome' ? firstOk : true, status: firstOk ? 200 : 409, body: null }); };\n" +
            "  return C.saveNotDone(post, { kind: 'reschedule', plannedVisitId: 'p1', reasonCode: 'other', reasonNote: ' Kongre ', rescheduleToDate: '2026-10-20' })\n" +
            "    .then(res => ({ res, calls })); };\n" +
            "const missed = () => { const calls = []; const post = (p, b) => { calls.push({ p, b }); return Promise.resolve({ ok: true }); };\n" +
            "  return C.saveNotDone(post, { kind: 'notDone', plannedVisitId: 'p2', reasonCode: 'doctor_unavailable', reasonNote: null }).then(res => ({ res, calls })); };\n" +
            "Promise.all([run(true), run(false), missed()]).then(r => process.stdout.write(JSON.stringify(r)));");
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement.EnumerateArray().ToList();

        var ok = r[0];
        Assert.True(ok.GetProperty("res").GetProperty("ok").GetBoolean());
        var calls = ok.GetProperty("calls").EnumerateArray().ToList();
        Assert.Equal(new[] { "/outcome", "/reports" }, calls.Select(c => c.GetProperty("path").GetString()));
        var first = calls[0].GetProperty("body");
        Assert.Equal("rescheduled", first.GetProperty("executionOutcome").GetString());
        Assert.Equal("other", first.GetProperty("reasonCode").GetString());
        Assert.Equal("Kongre", first.GetProperty("reasonNote").GetString());
        Assert.Equal("2026-10-20", first.GetProperty("rescheduleToDate").GetString());
        Assert.False(first.TryGetProperty("rescheduleNotes", out _)); // only reasonNote is sent
        var second = calls[1].GetProperty("body");
        Assert.Equal("p1", second.GetProperty("plannedVisitId").GetString());
        Assert.Equal("rescheduled", second.GetProperty("executionOutcome").GetString());

        var failed = r[1];
        Assert.False(failed.GetProperty("res").GetProperty("ok").GetBoolean());
        Assert.Equal(1, failed.GetProperty("res").GetProperty("step").GetInt32());
        Assert.Single(failed.GetProperty("calls").EnumerateArray()); // the second is NOT called

        var notDone = r[2].GetProperty("calls").EnumerateArray().ToList();
        Assert.Equal("missed", notDone[0].GetProperty("b").GetProperty("executionOutcome").GetString());
        Assert.False(notDone[0].GetProperty("b").TryGetProperty("rescheduleToDate", out _));
        Assert.Equal("missed", notDone[1].GetProperty("b").GetProperty("executionOutcome").GetString());
    }

    [Fact]
    public void A4_every_refusal_code_reads_in_the_users_language_and_503_says_try_again()
    {
        var tr = Resx("tr");
        var node = RunNode(Core() +
            "process.stdout.write(JSON.stringify({ codes: C.ERROR_CODES,\n" +
            "  mapped: C.errorKey(['Some message.', 'visit_reason_note_required']), none: C.errorKey(['x']),\n" +
            "  text: C.errorText({ ok: false, status: 409, body: { errors: ['m', 'visit_cancel_past_day'] } }, { Err_visit_cancel_past_day: 'GEÇMİŞ', ActionFailed: 'HATA' }),\n" +
            "  unavailable: C.errorText({ ok: false, status: 503, body: null }, { Err_reference_data_unavailable: 'TEKRAR', ActionFailed: 'HATA' }),\n" +
            "  raw: C.errorText({ ok: false, status: 400, body: { errors: ['visit_unknown_code'] } }, { ActionFailed: 'HATA' }) }));");
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;
        var codes = r.GetProperty("codes").EnumerateArray().Select(c => c.GetString()!).ToList();
        foreach (var code in new[]
                 {
                     "visit_report_deadline_passed", "visit_report_plan_cancelled", "visit_reason_invalid", "visit_reason_note_required",
                     "visit_reason_note_too_long", "visit_reschedule_date_invalid", "visit_report_reschedule_date_invalid",
                     "visit_reschedule_already_applied", "visit_cancel_past_day", "unplanned_visit_today_only",
                     "reference_data_unavailable", "visit_report_edit_window_closed", "visit_report_invalid_transition"
                 })
        {
            Assert.Contains(code, codes);
        }

        Assert.All(codes, code =>
        {
            Assert.Contains("Err_" + code, VisitWorkspaceIndex.ScriptKeys);
            Assert.False(string.IsNullOrWhiteSpace(tr["Err_" + code]));
        });
        Assert.Equal("Err_visit_reason_note_required", r.GetProperty("mapped").GetString());
        Assert.Equal(JsonValueKind.Null, r.GetProperty("none").ValueKind);
        Assert.Equal("GEÇMİŞ", r.GetProperty("text").GetString());
        Assert.Equal("TEKRAR", r.GetProperty("unavailable").GetString());
        Assert.Equal("HATA", r.GetProperty("raw").GetString()); // never a raw code
        Assert.Contains("tekrar deneyin", tr["Err_reference_data_unavailable"]);
    }

    [Fact]
    public void A4_the_detail_actions_follow_the_status()
    {
        var node = RunNode(Core() +
            "const p = { manage: true, record: true }, t = '2026-10-15';\n" +
            "const v = (s, d) => ({ workStatus: s, plannedVisitId: 'x', plannedDate: d || t });\n" +
            "process.stdout.write(JSON.stringify([C.actionsFor(v('today'), t, p), C.actionsFor(v('planned', '2026-10-16'), t, p), C.actionsFor(v('missed', '2026-10-14'), t, p),\n" +
            "  C.actionsFor(v('report_missing'), t, p), C.actionsFor(v('expired', '2026-10-10'), t, p), C.actionsFor({ workStatus: 'draft', plannedVisitId: null, plannedDate: t }, t, p),\n" +
            "  C.actionsFor(v('missed', '2026-10-14'), t, {}), C.actionsFor(v('reported'), t, p)]));");
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement.EnumerateArray()
            .Select(a => a.EnumerateArray().Select(x => x.GetString()).ToArray()).ToList();
        Assert.Equal(new[] { "cancel", "result" }, r[0]);
        Assert.Equal(new[] { "cancel" }, r[1]);
        Assert.Equal(new[] { "notDone", "reschedule" }, r[2]);
        Assert.Equal(new[] { "sendReport" }, r[3]);
        Assert.Equal(new[] { "locked" }, r[4]);
        Assert.Equal(new[] { "plan" }, r[5]);
        Assert.Empty(r[6]); // without the record keys nothing to do
        Assert.Empty(r[7]);
    }

    [Fact]
    public void A4_no_reason_list_lives_in_the_web_and_the_rep_never_sees_play_or_campaign()
    {
        var all = Script("workspace-core.js") + Script("visit-workspace.js");
        foreach (var code in new[] { "doctor_unavailable", "clinic_closed", "meeting_conflict", "travel_weather", "target_inactive" })
        {
            Assert.DoesNotContain(code, all);
        }

        Assert.DoesNotContain("strategyTemplate", all, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("campaign", all, StringComparison.OrdinalIgnoreCase);
    }

    // ═══ 5 · proxies: paths + the CRM endpoints' keys ═══════════════════════════════════════════════════════

    public static TheoryData<string, string, string, string[]> ProxyMap => new()
    {
        { "Contract", "GET", "/api/crm/visit-workspace/contract", new[] { ReportRead, PlanRead } },
        { "Calendar", "GET", "/api/crm/visit-workspace/calendar?from=2026-10-05&to=2026-11-08", new[] { ReportRead, PlanRead } },
        { "Reasons", "GET", "/api/crm/visit-workspace/reasons?appliesTo=missed&lang=tr", new[] { ReportRead, PlanRead } },
        { "RescheduleOptions", "GET", "/api/crm/visit-workspace/reschedule-options?plannedVisitId=" + Id, new[] { ReportRead, PlanRead } },
        { "Report", "GET", "/api/crm/visit-report/" + Id, new[] { ReportRead } },
        { "Session", "GET", "/api/crm/visit-plan/sessions/" + Id, new[] { PlanRead } },
        { "SessionTargets", "GET", "/api/crm/visit-plan/sessions/" + Id + "/targets?weekStart=2026-10-12", new[] { PlanRead } },
        { "SearchContacts", "GET", "/api/crm/contacts/search?search=ay", new[] { ContactRead } },
        { "Cancel", "POST", "/api/crm/planned-visits/" + Id + "/cancel", new[] { Manage } },
        { "CreateUnplanned", "POST", "/api/crm/planned-visits", new[] { Manage } },
        { "RecordOutcome", "POST", "/api/crm/visit-report/outcome", new[] { ReportRecord, Manage } },
        { "Submit", "POST", "/api/crm/visit-report", new[] { ReportRecord, Manage } },
        { "ApproveWeek", "POST", "/api/crm/visit-plan/apply", new[] { PlanApply, Manage } },
        { "ReopenWeek", "POST", "/api/crm/visit-plan/sessions/" + Id + "/weeks/2026-10-12/reopen", new[] { PlanApply, Manage } }
    };

    private const string Id = "11111111-1111-1111-1111-111111111111";

    [Theory]
    [MemberData(nameof(ProxyMap))]
    public async Task A5_each_proxy_reaches_its_crm_path_only_with_all_its_keys(string action, string verb, string path, string[] keys)
    {
        var (gateway, controller) = Arrange(keys);
        var query = path.Contains('?') ? path[path.IndexOf('?')..] : string.Empty;
        controller.ControllerContext.HttpContext.Request.QueryString = new QueryString(query);
        var result = await Invoke(controller, action);
        Assert.Equal(200, Status(result));
        var sent = Assert.Single(gateway.Requests);
        Assert.Equal(verb, sent.Method);
        Assert.Equal(GatewayUrl + path, sent.Uri);
        Assert.Equal(TenantId.ToString(), sent.Tenant);

        // one key short ⇒ 403 and nothing reaches the gateway
        foreach (var missing in keys)
        {
            var (shortGateway, shortController) = Arrange(keys.Where(k => k != missing).ToArray());
            shortController.ControllerContext.HttpContext.Request.QueryString = new QueryString(query);
            Assert.Equal(403, Status(await Invoke(shortController, action)));
            Assert.Empty(shortGateway.Requests);
        }
    }

    [Fact]
    public async Task A5_the_workspace_creates_only_unplanned_visits()
    {
        var (gateway, controller) = Arrange(new[] { Manage }, "{\"visitCode\":\"X\",\"targetId\":\"" + Id + "\"}");
        var result = await controller.CreateUnplanned(CancellationToken.None);
        Assert.Equal(400, Status(result));
        Assert.Empty(gateway.Requests);
        Assert.True(VisitWorkspaceController.IsUnplanned("{\"unplanned\":true}"));
        Assert.False(VisitWorkspaceController.IsUnplanned("{\"unplanned\":\"true\"}"));

        var node = RunNode(Core() + "process.stdout.write(JSON.stringify(C.unplannedBody('c1', '2026-10-15', '10:30', 1760522400123)));");
        if (node is null) { return; }
        var body = JsonDocument.Parse(node).RootElement;
        Assert.True(body.GetProperty("unplanned").GetBoolean());
        Assert.Equal("2026-10-15", body.GetProperty("plannedDate").GetString());
        Assert.Matches("^[A-Za-z0-9._-]+$", body.GetProperty("visitCode").GetString()!); // the CRM code pattern
    }

    // ═══ 6 · seven languages + the menu key ════════════════════════════════════════════════════════════════

    [Fact]
    public void A6_every_key_is_filled_in_every_language_and_never_echoes_its_name()
    {
        var english = Resx("en");
        foreach (var culture in Cultures)
        {
            var resx = Resx(culture);
            foreach (var key in AllKeys)
            {
                Assert.True(resx.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value), $"{culture}:{key}");
                Assert.NotEqual(key, value);
            }
        }

        foreach (var culture in new[] { "tr", "fr", "es", "zh", "ar", "ru" })
        {
            var resx = Resx(culture);
            var same = AllKeys.Count(k => resx[k] == english[k] && !resx[k].Contains('{'));
            Assert.True(same <= 6, $"{culture}: {same} values identical to English"); // a few cognates only
        }

        // the view serializes exactly the script keys and uses only declared view keys
        var view = ViewSource();
        Assert.Contains("VisitWorkspaceIndex.ScriptKeys.ToDictionary(k => k, k => Localizer[k].Value)", view);
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(view, "Localizer\\[\"([A-Za-z_]+)\"\\]"))
        {
            Assert.Contains(m.Groups[1].Value, VisitWorkspaceIndex.ViewKeys);
        }

        var js = Script("visit-workspace.js");
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(js, "\\bL\\.([A-Za-z_]+)"))
        {
            Assert.Contains(m.Groups[1].Value, VisitWorkspaceIndex.ScriptKeys);
        }

        foreach (var culture in new[] { "en", "tr", "fr", "es", "zh", "ar", "ru" })
        {
            var shared = XDocument.Load(Path.Combine(WebRoot(), "Resources", $"SharedResource.{culture}.resx")).Root!
                .Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value ?? "");
            Assert.True(shared.TryGetValue("Nav.Page.VISITWORKSPACE", out var nav) && !string.IsNullOrWhiteSpace(nav), culture);
            Assert.NotEqual("Nav.Page.VISITWORKSPACE", nav);
        }

        Assert.Equal("مساحة عمل الزيارات", Resx("ar")["PageTitle"]);
    }

    [Fact]
    public void A6_every_data_name_is_isolated_for_right_to_left()
    {
        var js = Script("visit-workspace.js");
        Assert.Contains("F.bidi(v.targetDisplayName || '—')", js);
        Assert.Contains("F.bidi(C.productLabel(c))", js);
        Assert.Contains("F.bidi(x.label)", js); // the reason labels from the reference set
        Assert.Contains("F.ratio(ns.length, ns.max)", js); // 0 / 500 stays left-to-right
        Assert.DoesNotContain("toLocaleDateString", js);
        Assert.Contains("[dir=\"rtl\"] .vw-flip", File.ReadAllText(Path.Combine(ScriptDir(), "visit-workspace.css")));
    }

    // ═══ 7 · phone layout ══════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void A7_under_768_px_the_page_is_a_day_list()
    {
        var node = RunNode(Core() + "process.stdout.write(JSON.stringify([C.layoutFor(375), C.layoutFor(767), C.layoutFor(768), C.layoutFor(1440)]));");
        if (node is not null)
        {
            Assert.Equal(new[] { "list", "list", "grid", "grid" }, JsonSerializer.Deserialize<string[]>(node));
        }

        var js = Script("visit-workspace.js");
        Assert.Contains("if (state.layout === 'list') { renderDayList(); } else { renderCalendar(); }", js);
        Assert.Contains("const next = C.layoutFor(window.innerWidth);", js);
    }

    [Fact]
    public void Filters_are_remembered_and_survive_a_throwing_storage()
    {
        var node = RunNode(Core() +
            "const mem = {}; const ok = { getItem: k => mem[k] || null, setItem: (k, v) => { mem[k] = v; } };\n" +
            "const bad = { getItem: () => { throw new Error('denied'); }, setItem: () => { throw new Error('denied'); } };\n" +
            "C.saveFilters(ok, { statuses: ['missed', 'bogus'], accountId: 'a1', productId: '' });\n" +
            "const visits = [{ workStatus: 'missed', accountId: 'a1', plannedContent: [{ productId: 'p1' }] }, { workStatus: 'today', accountId: 'a2', plannedContent: [] }];\n" +
            "process.stdout.write(JSON.stringify([C.loadFilters(ok), C.loadFilters(bad), C.saveFilters(bad, {}),\n" +
            "  C.filterVisits(visits, { statuses: ['missed'] }).length, C.filterVisits(visits, { productId: 'p1' }).length, C.filterVisits(visits, {}).length]));");
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement.EnumerateArray().ToList();
        Assert.Equal(new[] { "missed" }, r[0].GetProperty("statuses").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal("a1", r[0].GetProperty("accountId").GetString());
        Assert.Empty(r[1].GetProperty("statuses").EnumerateArray());
        Assert.False(r[2].GetBoolean());
        Assert.Equal((1, 1, 2), (r[3].GetInt32(), r[4].GetInt32(), r[5].GetInt32()));
    }

    // ═══ 8 · the page script end to end in a fake DOM (Node) ═════════════════════════════════════════════

    [Fact]
    public void A8_the_page_draws_the_week_opens_the_detail_and_saves_a_reschedule_in_two_steps()
    {
        var smoke = Path.Combine(Path.GetDirectoryName(WebRoot())!, "Diten.Web.Tests", "Js", "visit-workspace-smoke.js").Replace("\\", "/");
        var crm = Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM").Replace("\\", "/");
        var node = RunNode("process.argv[2] = '" + crm + "'; require('" + smoke + "');");
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;

        // CT (live E4) — drawing the first events inside DitenCalendar.create must not throw (the grid would not render)
        Assert.Equal(JsonValueKind.Null, r.GetProperty("createError").ValueKind);
        Assert.Equal(2, r.GetProperty("events").GetInt32());
        Assert.False(r.GetProperty("weekends").GetBoolean()); // Mon–Fri grid
        var card = r.GetProperty("cardHtml").GetString()!;
        Assert.Contains("vw-st-missed", card);
        Assert.Contains("<bdi>Tutukon</bdi>", card); // the product NAME, the code only as a fallback
        Assert.Contains("bx-pin", card);
        Assert.Contains("bx-walk", card);
        Assert.Contains("vw-countdown", card);
        Assert.Contains("bg-label-danger", r.GetProperty("header").GetString()); // the holiday column
        Assert.False(r.GetProperty("approveHidden").GetBoolean());
        Assert.Contains("data-dialog=\"notDone\"", r.GetProperty("actions").GetString());
        Assert.Contains("data-dialog=\"reschedule\"", r.GetProperty("actions").GetString());
        Assert.Contains("bg-label-primary", r.GetProperty("content").GetString()); // the first product = promo

        Assert.True(r.GetProperty("saveDisabledWithoutNote").GetBoolean());
        Assert.True(r.GetProperty("noteRequiredShown").GetBoolean());
        Assert.True(r.GetProperty("saveDisabledWithoutDay").GetBoolean());
        Assert.True(r.GetProperty("saveEnabled").GetBoolean());
        var save = r.GetProperty("saveCalls").EnumerateArray().ToList();
        Assert.Equal(new[] { "/api/outcome", "/api/reports" }, save.Select(c => c[0].GetString()));
        Assert.Equal("Kongre", save[0][1].GetProperty("reasonNote").GetString());
        Assert.Equal("2099-01-05", save[0][1].GetProperty("rescheduleToDate").GetString());
        Assert.Equal("rescheduled", save[1][1].GetProperty("executionOutcome").GetString());

        var calls = r.GetProperty("calls").EnumerateArray().Select(c => c.GetString()!).ToList();
        Assert.Contains(calls, c => c.StartsWith("GET /api/calendar?from=", StringComparison.Ordinal));
        Assert.Contains(calls, c => c.StartsWith("GET /api/reasons?appliesTo=reschedule&lang=tr", StringComparison.Ordinal));
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    private static VisitWorkspaceIndexViewModel Model(IActionResult result)
        => Assert.IsType<VisitWorkspaceIndexViewModel>(Assert.IsType<ViewResult>(result).Model);

    private static int? Status(IActionResult result) => result switch
    {
        ContentResult c => c.StatusCode,
        ObjectResult o => o.StatusCode ?? 200,
        StatusCodeResult s => s.StatusCode,
        _ => null
    };

    private static Task<IActionResult> Invoke(VisitWorkspaceController c, string action) => action switch
    {
        "Contract" => c.Contract(CancellationToken.None),
        "Calendar" => c.Calendar(CancellationToken.None),
        "Reasons" => c.Reasons(CancellationToken.None),
        "RescheduleOptions" => c.RescheduleOptions(CancellationToken.None),
        "Report" => c.Report(Guid.Parse(Id), CancellationToken.None),
        "Session" => c.Session(Guid.Parse(Id), CancellationToken.None),
        "SessionTargets" => c.SessionTargets(Guid.Parse(Id), CancellationToken.None),
        "SearchContacts" => c.SearchContacts(CancellationToken.None),
        "Cancel" => c.Cancel(Guid.Parse(Id), CancellationToken.None),
        "CreateUnplanned" => c.CreateUnplanned(CancellationToken.None),
        "RecordOutcome" => c.RecordOutcome(CancellationToken.None),
        "Submit" => c.Submit(CancellationToken.None),
        "ApproveWeek" => c.ApproveWeek(CancellationToken.None),
        "ReopenWeek" => c.ReopenWeek(Guid.Parse(Id), "2026-10-12", CancellationToken.None),
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };

    private static (Gateway Gateway, VisitWorkspaceController Controller) Arrange(params string[] permissions)
        => Arrange(permissions, "{\"plannedVisitId\":\"" + Id + "\",\"unplanned\":true}");

    private static (Gateway Gateway, VisitWorkspaceController Controller) Arrange(string[] permissions, string body)
    {
        var gateway = new Gateway();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl }).Build();
        var controller = new VisitWorkspaceController(new HttpClient(gateway), configuration, NullLogger<VisitWorkspaceController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return (gateway, controller);
    }

    private static string Core()
    {
        var file = Path.Combine(ScriptDir(), "workspace-core.js").Replace("\\", "/");
        return "const C = require('" + file + "');\n";
    }

    /// <summary>Runs <paramref name="code"/> in Node; null when Node is not on the machine (the code assertions stand alone).</summary>
    private static string? RunNode(string code)
    {
        var temp = Path.Combine(Path.GetTempPath(), "vw2-" + Guid.NewGuid().ToString("N") + ".js");
        File.WriteAllText(temp, code, new UTF8Encoding(false));
        try
        {
            using var process = Process.Start(new ProcessStartInfo("node", "\"" + temp + "\"")
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, StandardOutputEncoding = Encoding.UTF8
            });
            if (process is null) return null;
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(20000);
            Assert.True(process.ExitCode == 0, error);
            return output;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
        finally
        {
            File.Delete(temp);
        }
    }

    private static string ScriptDir() => Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "VisitWorkspace");

    private static string Script(string file) => File.ReadAllText(Path.Combine(ScriptDir(), file)).Replace("\r\n", "\n");

    private static string ViewSource() => File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "VisitWorkspace", "Index.cshtml")).Replace("\r\n", "\n");

    private static Dictionary<string, string> Resx(string culture)
    {
        var file = "VisitWorkspaceIndex" + (culture.Length == 0 ? "" : "." + culture) + ".resx";
        return XDocument.Load(Path.Combine(WebRoot(), "Resources", "Views", "CRM", "VisitWorkspace", file)).Root!
            .Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value ?? string.Empty, StringComparer.Ordinal);
    }

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }

    private sealed class Gateway : HttpMessageHandler
    {
        public List<(string Method, string Uri, string? Tenant)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.Method.Method, request.RequestUri!.ToString(),
                request.Headers.TryGetValues("X-Tenant-Id", out var t) ? t.Single() : null));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":{}}", Encoding.UTF8, "application/json")
            });
        }
    }
}

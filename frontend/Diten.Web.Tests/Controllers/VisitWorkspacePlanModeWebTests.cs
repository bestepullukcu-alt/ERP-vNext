using System.Diagnostics;
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
/// WP-VW-W2 (WEB-b) — the Visit Workspace's PLAN mode on the REAL scripts, view, controller and resources (Acceptance
/// 1–6): only a draft week is edited; the Targets panel runs on the Visit Planning page's own module (targets-core.js, no
/// copy); a drop is a day pin with the start rounded to 15 minutes (the all-day row = no time), an approved week sends
/// nothing; apply products = the first promo, the others reminders; the institution filter reads accountDisplayName
/// only; the unplaced list; the 7 pin codes in the user's language. The page itself runs in a fake DOM
/// (Js/visit-workspace-plan-smoke.js: the stub calendar draws the first events inside create).
/// </summary>
public sealed class VisitWorkspacePlanModeWebTests
{
    private static readonly string[] Cultures = ["", "en", "tr", "fr", "es", "zh", "ar", "ru"];
    private static readonly string[] PinCodes =
        ["pin_time_invalid", "pin_time_outside_hours", "pin_time_conflict", "pin_time_past_day_end", "pin_overflow", "pin_day_full", "week_already_approved"];

    // ── 1 · Plan mode: a draft week is edited; an approved one is read-only with the reopen hint ─────────────────────

    [Fact]
    public void A1_plan_mode_edits_a_draft_week_and_an_approved_week_is_read_only_with_the_reopen_hint()
    {
        var draft = Smoke("draft");
        if (draft is { } d)
        {
            Assert.Equal(JsonValueKind.Null, d.GetProperty("createError").ValueKind);
            Assert.False(d.GetProperty("panelHidden").GetBoolean());
            Assert.True(d.GetProperty("editable").GetBoolean());
            Assert.Equal("", d.GetProperty("locked").GetString());
            Assert.Equal("Hedefler · 42. Hafta", d.GetProperty("title").GetString());
            Assert.Equal(new[] { "k1" }, d.GetProperty("draftEventEditable").EnumerateArray().Select(x => x.GetString())); // only the draft card moves
        }

        var approved = Smoke("approved");
        if (approved is { } a)
        {
            Assert.Equal(JsonValueKind.Null, a.GetProperty("createError").ValueKind);
            Assert.False(a.GetProperty("editable").GetBoolean());
            Assert.Equal("42. Hafta onaylı — değiştirmek için Haftayı yeniden aç.", a.GetProperty("locked").GetString());
            Assert.Empty(a.GetProperty("draftEventEditable").EnumerateArray());
        }

        var core = Script("VisitWorkspace", "workspace-core.js");
        Assert.Contains("const canPlanWeek = (week, mode) => mode === 'plan' && !!week && week.state === 'draft';", core);
        var js = Script("VisitWorkspace", "visit-workspace.js");
        Assert.Contains("const planEditable = () => !!TC && perms.plan && state.layout === 'grid' && C.canPlanWeek(weekOf(state.week), state.mode);", js);
        Assert.Contains("if (typeof state.calendar.setEditable === 'function') { state.calendar.setEditable(editable); }", js);
        Assert.Contains("const events = visits.map(v => C.eventOf(v, editable && v.workStatus === 'draft'));", js);
        Assert.Contains("@if (Model.CanPlan)", View());
        Assert.Contains("@media (max-width: 767.98px) { .vw-plan-panel { display: none !important; } }", Script("VisitWorkspace", "visit-workspace.css")); // phone: no panel
    }

    [Fact]
    public void A1_plan_mode_needs_the_visit_planning_read_and_generate_keys()
    {
        static VisitWorkspaceIndexViewModel ModelFor(params string[] keys) => Assert.IsType<VisitWorkspaceIndexViewModel>(
            Assert.IsType<ViewResult>(Controller(keys).Index()).Model);

        Assert.False(ModelFor("crm.visit-report.read", "crm.visit-plan.read").CanPlan);
        Assert.False(ModelFor("crm.visit-report.read", "crm.visit-plan.generate").CanPlan);
        Assert.True(ModelFor("crm.visit-report.read", "crm.visit-plan.read", "crm.visit-plan.generate").CanPlan);
    }

    // ── 2 · the Targets panel runs on the Visit Planning module (targets-core.js), not a copy ───────────────────────

    [Fact]
    public void A2_the_targets_panel_and_the_visit_planning_page_run_on_one_shared_module()
    {
        // both pages load the ONE module before their scripts
        var view = View();
        Assert.True(view.IndexOf("CRM/VisitPlanning/targets-core.js", StringComparison.Ordinal) is var a && a > 0
                    && a < view.IndexOf("CRM/VisitWorkspace/visit-workspace.js", StringComparison.Ordinal));
        var details = File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "VisitPlanning", "Details.cshtml"));
        Assert.True(details.IndexOf("targets-core.js", StringComparison.Ordinal) is var b && b > 0
                    && b < details.IndexOf("VisitPlanning/details.js", StringComparison.Ordinal));

        // the workspace uses the rules …
        var js = Script("VisitWorkspace", "visit-workspace.js");
        foreach (var call in new[] { "TC.splitPlanFirst(", "TC.quickCounts(", "TC.selectionUpdate(", "TC.doctorRow", "TC.rolesByOrder(", "TC.unionPicks(", "TC.pinInput" })
        {
            Assert.Contains(call, js);
        }
        // … and does not carry its own copy of them
        foreach (var copy in new[] { "consentStatus", "const splitPlanFirst", "const quickCounts", "const selectionUpdate", "const unionPicks", "role: i === 0" })
        {
            Assert.DoesNotContain(copy, js);
        }
        // the Visit Planning page runs on the same module
        Assert.Contains("return TC.splitPlanFirst(all, [], inPlanHere, row => docMatches(row, re)).plan;", Script("VisitPlanning", "details.js"));
        Assert.Contains("const buildUpdate = changes => TC.selectionUpdate(page.state.session, savedContacts(), changes);", Script("VisitPlanning", "targets.js"));
        Assert.Contains(".dayPins || []).map(TC.pinInput);", Script("VisitPlanning", "weeks.js"));

        var node = RunNode(Require("VisitPlanning", "targets-core.js", "TC") +
            "const pin = TC.pinInput({ targetType: 'contact', targetId: 'c', date: '2026-10-14', startTime: '10:30' });\n" +
            "const up = TC.selectionUpdate({ selectedAccountIds: ['a'], version: 3 }, [{ contactId: 'c', accountId: 'a' }, { contactId: 'd', accountId: 'a' }], [{ doctor: { contactId: 'd', accountId: 'a' }, remove: true }, { doctor: { contactId: 'e', accountId: 'b' } }]);\n" +
            "process.stdout.write(JSON.stringify([pin.startTime, pin.scope, up.selectedContacts.map(c => c.contactId).join(','), up.selectedAccountIds, up.expectedVersion]));");
        if (node is not null)
        {
            var r = JsonSerializer.Deserialize<JsonElement[]>(node)!;
            Assert.Equal("10:30", r[0].GetString()); // a re-save keeps a time pin (the Visit Planning page's weeks.js too)
            Assert.Equal("visit", r[1].GetString());
            Assert.Equal("c,e", r[2].GetString());
            Assert.Equal(new[] { "a", "b" }, r[3].EnumerateArray().Select(x => x.GetString()));
            Assert.Equal(3, r[4].GetInt32());
        }

        if (Smoke("draft") is { } s)
        {
            var list = s.GetProperty("list").GetString()!;
            Assert.True(list.IndexOf("Planda (1)", StringComparison.Ordinal) < list.IndexOf("data-cid=\"p1\"", StringComparison.Ordinal));
            Assert.True(list.IndexOf("data-cid=\"p1\"", StringComparison.Ordinal) < list.IndexOf("Diğer doktorlar", StringComparison.Ordinal));
            Assert.Contains("<bdi>Kardiyoloji</bdi>", list);      // the specialty LABEL
            Assert.Contains("haftada 1 (varsayılan)", list);      // 4L frequency default
            Assert.Contains("Bu hafta görülmeli", list);          // 4M due this week
            Assert.Contains("<bdi>A</bdi>", list);                 // the segment badge
            Assert.Contains("Tümü (1)", s.GetProperty("quick").GetString()); // 4M: the counts are the doctors outside the plan
            Assert.Equal("Tümünü seç (1)", s.GetProperty("selectAll").GetString());
            Assert.Equal("1 doktor · 1 eczane · 1 hesap", s.GetProperty("summary").GetString());
        }
    }

    // ── 3 · a drop → the day pin: 15-minute rounding, the all-day row = no time; an approved week sends nothing ─────

    [Fact]
    public void A3_a_drop_pins_the_day_and_the_start_rounded_to_15_minutes_and_an_approved_week_sends_nothing()
    {
        var node = RunNode(Require("VisitWorkspace", "workspace-core.js", "C") +
            "const p = s => C.pinFromDrop({ startUtc: s });\n" +
            "process.stdout.write(JSON.stringify([p('2026-10-14T10:37:00Z'), p('2026-10-14T10:38:00Z'), p('2026-10-14T10:52:00Z'), p('2026-10-14T23:58:00Z'),\n" +
            "  C.pinFromDrop({ allDay: true, date: '2026-10-14' }), C.pinFromDrop({ date: '2026-10-14', time: '09:07' }),\n" +
            "  C.canPlanWeek({ state: 'draft' }, 'plan'), C.canPlanWeek({ state: 'approved' }, 'plan'), C.canPlanWeek({ state: 'past' }, 'plan'), C.canPlanWeek({ state: 'draft' }, 'execute'),\n" +
            "  C.pinsWith([{ targetType: 'contact', targetId: 'a', date: 'x', scope: 'visit' }, { targetType: 'contact', targetId: 'b', date: 'y', scope: 'institution' }], { targetType: 'contact', targetId: 'b', contactId: 'b' }, { date: 'z', startTime: '10:00' })]));");
        if (node is not null)
        {
            var r = JsonSerializer.Deserialize<JsonElement[]>(node)!;
            string Time(int i) => r[i].GetProperty("startTime").ValueKind == JsonValueKind.Null ? "-" : r[i].GetProperty("startTime").GetString()!;
            Assert.Equal(new[] { "10:30", "10:45", "10:45", "23:45", "-", "09:00" }, Enumerable.Range(0, 6).Select(Time));
            Assert.Equal("2026-10-14", r[4].GetProperty("date").GetString());
            Assert.Equal(new[] { true, false, false, false }, Enumerable.Range(6, 4).Select(i => r[i].GetBoolean()));
            Assert.Equal(3, r[10].GetArrayLength()); // the other pins are kept (an institution pin of the same target too)
        }

        if (Smoke("draft") is { } d)
        {
            var drop = d.GetProperty("dropPut");
            Assert.Equal("/plan/sessions/s1", drop[0].GetString()); // the EXISTING Visit Planning session update
            var body = drop[1];
            var pins = body.GetProperty("dayPins").GetProperty("pins").EnumerateArray().ToList();
            var o1 = Assert.Single(pins, p => p.GetProperty("targetId").GetString() == "o1");
            Assert.Equal(("visit", "10:30"), (o1.GetProperty("scope").GetString(), o1.GetProperty("startTime").GetString())); // 10:37 → 10:30
            Assert.Contains(pins, p => p.GetProperty("targetId").GetString() == "x9" && p.GetProperty("startTime").GetString() == "09:00"); // kept
            Assert.Contains(body.GetProperty("selectedContacts").EnumerateArray(), c => c.GetProperty("contactId").GetString() == "o1"); // joins the plan
            Assert.Equal(7, body.GetProperty("expectedVersion").GetInt32());

            var allDay = d.GetProperty("allDayPut")[1].GetProperty("dayPins").GetProperty("pins").EnumerateArray().Single(p => p.GetProperty("targetId").GetString() == "p1");
            Assert.Equal(JsonValueKind.Null, allDay.GetProperty("startTime").ValueKind);
            Assert.False(d.GetProperty("allDayPut")[1].TryGetProperty("selectedContacts", out _)); // already in the plan: only the pins

            var move = d.GetProperty("movePut")[1].GetProperty("dayPins").GetProperty("pins").EnumerateArray().Single(p => p.GetProperty("targetId").GetString() == "t2");
            Assert.Equal("14:00", move.GetProperty("startTime").GetString()); // a draft card moved to 14:07
            Assert.False(d.GetProperty("moveReverted").GetBoolean());
            Assert.Equal(3, d.GetProperty("putCountAfterDrops").GetInt32());
        }

        if (Smoke("approved") is { } a)
        {
            Assert.Equal(0, a.GetProperty("putCountAfterDrops").GetInt32()); // no request on an approved week
            Assert.True(a.GetProperty("moveReverted").GetBoolean());
        }
    }

    // ── 4 · apply products: the first promo, the others reminders ────────────────────────────────────────────────

    [Fact]
    public void A4_apply_products_sends_the_first_as_promo_and_the_others_as_reminders()
    {
        var node = RunNode(Require("VisitPlanning", "targets-core.js", "TC") +
            "process.stdout.write(JSON.stringify(TC.rolesByOrder([{ productId: 'a' }, { productId: 'b' }, { productId: 'c' }]).map(p => p.role)));");
        if (node is not null)
        {
            Assert.Equal("[\"promo\",\"non-promo\",\"non-promo\"]", node);
        }

        if (Smoke("draft") is { } d)
        {
            Assert.Contains("<bdi>Tutukon</bdi>", d.GetProperty("picked").GetString()); // product NAMES
            Assert.Contains("tanıtım", d.GetProperty("picked").GetString());
            var put = d.GetProperty("productsPut")[1];
            var p1 = put.GetProperty("selectedContacts").EnumerateArray().Single(c => c.GetProperty("contactId").GetString() == "p1");
            var products = p1.GetProperty("products").EnumerateArray().Select(p => p.GetProperty("productId").GetString() + ":" + p.GetProperty("role").GetString()).ToList();
            Assert.Equal(new[] { "old:non-promo", "pA:promo", "pB:non-promo" }, products); // added (S-2), the doctor's own product kept
        }
    }

    // ── 5 · the institution filter reads accountDisplayName; the unplaced list; approve with sessionVersion ──────────

    [Fact]
    public void A5_the_institution_filter_reads_accountDisplayName_only_and_the_unplaced_list_names_the_visits()
    {
        var node = RunNode(Require("VisitWorkspace", "workspace-core.js", "C") +
            "process.stdout.write(JSON.stringify([C.accountOptions([{ accountId: 'a', accountDisplayName: 'Acıbadem', targetDisplayName: 'Dr. X' }, { accountId: 'b', targetDisplayName: 'Dr. Y' }]),\n" +
            "  C.unplacedRows({ unplaced: [{ displayName: 'Dr. Z', accountDisplayName: 'K', reason: 'capacity_full' }, { displayName: 'Dr. W', reason: 'zzz' }] }, { Reason_capacity_full: 'Hafta dolu.', UnplacedReasonOther: 'Yerleştirilemedi.' }),\n" +
            "  C.sessionVersionOf({ sessionVersion: 7 }), C.sessionVersionOf({})]));");
        if (node is not null)
        {
            var r = JsonSerializer.Deserialize<JsonElement[]>(node)!;
            Assert.Equal("{\"a\":\"Acıbadem\"}", r[0].GetRawText()); // a visit without accountDisplayName adds nothing — never a doctor's name
            Assert.Equal("Hafta dolu.", r[1][0].GetProperty("reason").GetString());
            Assert.Equal("Yerleştirilemedi.", r[1][1].GetProperty("reason").GetString());
            Assert.Equal(7, r[2].GetInt32());
            Assert.Equal(JsonValueKind.Null, r[3].ValueKind);
        }

        var js = Script("VisitWorkspace", "visit-workspace.js");
        Assert.Contains("const accounts = C.accountOptions(visits);", js);
        Assert.DoesNotContain("accounts[v.accountId] || v.targetDisplayName", js);
        Assert.Contains("if (accountSelect) { accountSelect.classList.toggle('d-none', !hasAccounts); }", js);

        if (Smoke("draft") is { } d)
        {
            var options = d.GetProperty("accountOptions").GetString()!;
            Assert.Contains("Acıbadem Taksim", options);
            Assert.DoesNotContain("Dr. ", options);
            Assert.False(d.GetProperty("accountHidden").GetBoolean());
            var list = d.GetProperty("unplacedList").GetString()!;
            Assert.Contains("<bdi>Dr. Can Er</bdi>", list);
            Assert.Contains("<bdi>018 KLİNİK</bdi>", list);
            Assert.Contains("Ziyaret mesai bitişini aşıyor, erkene alındı", list);
            var approve = d.GetProperty("approveCalls").EnumerateArray().Select(x => x.GetString()!).ToList();
            Assert.Contains(approve, c => c.StartsWith("POST /api/apply", StringComparison.Ordinal) && c.Contains("\"expectedVersion\":7"));
            Assert.DoesNotContain(approve, c => c.StartsWith("GET /api/sessions/", StringComparison.Ordinal)); // the read's sessionVersion
        }
    }

    // ── 6 · the 7 pin codes in the user's language (7 languages + neutral) ───────────────────────────────────────

    [Fact]
    public void A6_the_seven_pin_codes_read_in_the_users_language_in_every_language()
    {
        Assert.Contains("const PIN_CODES = ['pin_time_invalid', 'pin_time_outside_hours', 'pin_time_conflict', 'pin_time_past_day_end', 'pin_overflow', 'pin_day_full', 'week_already_approved'];",
            Script("VisitWorkspace", "workspace-core.js"));
        var newKeys = PinCodes.Select(c => "Pin_" + c).Concat(new[]
        {
            "TargetsTitle", "TargetsNoPlan", "PlanReadOnlyWeek", "PlanReadOnlyPast", "SelectAll", "ApplyProducts", "SelectionSummary",
            "PlanDoctorsHeading", "OtherDoctorsHeading", "DueThisWeek", "FrequencyDefaultWeekly", "UnplacedReasonOther"
        }).ToList();
        foreach (var key in newKeys) Assert.Contains(key, VisitWorkspaceIndex.ScriptKeys);
        foreach (var key in new[] { "ModeExecute", "ModePlan", "DragHint", "ProductsTitle", "ApplyButton" }) Assert.Contains(key, VisitWorkspaceIndex.ViewKeys);
        var en = Resx("en");
        foreach (var culture in Cultures)
        {
            var resx = Resx(culture);
            foreach (var key in newKeys)
            {
                Assert.True(resx.TryGetValue(key, out var value) && value.Trim().Length > 0 && value != key, $"{culture}:{key}");
                if (culture is not ("" or "en")) Assert.NotEqual(en[key], value);
            }
        }
        Assert.Equal("Ziyaret mesai bitişini aşıyor, erkene alındı", Resx("tr")["Pin_pin_time_past_day_end"]);
        Assert.Equal("{0} onaylı — değiştirmek için Haftayı yeniden aç.", Resx("tr")["PlanReadOnlyWeek"]);

        var node = RunNode(Require("VisitWorkspace", "workspace-core.js", "C") +
            "const L = { Pin_week_already_approved: 'Hafta onaylı', Pin_pin_time_invalid: 'Adım', ActionFailed: 'Olmadı' };\n" +
            "process.stdout.write(JSON.stringify([C.pinText({ ok: false, status: 409, body: { errors: ['week_already_approved', 'x'] } }, L),\n" +
            "  C.pinText({ ok: false, status: 400, body: { errors: ['A pin...', 'pin_time_invalid'] } }, L), C.pinText({ ok: false, status: 500, body: null }, L),\n" +
            "  C.pinMoveCode({ pinnedTime: '17:45', startTime: '17:30' }), C.pinMoveCode({ pinnedTime: '10:30', startTime: '11:00' }), C.pinMoveCode({ pinnedTime: '10:30', startTime: '10:30' })]));");
        if (node is not null)
        {
            Assert.Equal("[\"Hafta onaylı\",\"Adım\",\"Olmadı\",\"pin_time_past_day_end\",\"pin_time_conflict\",null]", node);
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────────

    private static JsonElement? Smoke(string weekState)
    {
        var smoke = Path.Combine(Path.GetDirectoryName(WebRoot())!, "Diten.Web.Tests", "Js", "visit-workspace-plan-smoke.js").Replace("\\", "/");
        var crm = Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM").Replace("\\", "/");
        var node = RunNode("process.argv[2] = '" + crm + "'; process.argv[3] = '" + weekState + "'; require('" + smoke + "');");
        return node is null ? null : JsonDocument.Parse(node).RootElement.Clone();
    }

    private static string Require(string folder, string file, string name)
        => "const " + name + " = require('" + Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", folder, file).Replace("\\", "/") + "');\n";

    private static VisitWorkspaceController Controller(params string[] permissions)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = "http://gateway.test" }).Build();
        var controller = new VisitWorkspaceController(new HttpClient(), configuration, NullLogger<VisitWorkspaceController>.Instance);
        var claims = new List<Claim> { new("tenantId", Guid.NewGuid().ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
        };
        return controller;
    }

    private static string? RunNode(string code)
    {
        var temp = Path.Combine(Path.GetTempPath(), "vw2b-" + Guid.NewGuid().ToString("N") + ".js");
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
            process.WaitForExit(30000);
            Assert.True(process.ExitCode == 0, error);
            return output;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null; // no Node: the code assertions stand alone
        }
        finally
        {
            File.Delete(temp);
        }
    }

    private static string Script(string folder, string file)
        => File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", folder, file)).Replace("\r\n", "\n");

    private static string View() => File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "VisitWorkspace", "Index.cshtml")).Replace("\r\n", "\n");

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
}

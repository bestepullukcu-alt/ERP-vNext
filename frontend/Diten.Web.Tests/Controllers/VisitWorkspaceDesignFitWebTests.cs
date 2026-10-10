using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Views.CRM.VisitWorkspace;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W2 (WEB-c) — the Visit Workspace's design fit with the mockup (DESIGN-GAP-W2 T1–T10, D1–D6, P1–P2, L1–L9) on
/// the REAL scripts, view, stylesheet and resources (Acceptance 1–5): the pure layout rules run in Node
/// (workspace-core.js); the page runs in a fake DOM (Js/visit-workspace-design-smoke.js: the stub calendar draws the
/// first events inside create): no week strip, ONE calendar card with the filters and Day / Week / Month,
/// headerToolbar:false + allDaySlot:false, the cards, the month view, the legend, the detail panel, ONE E2 dialog with
/// three tabs and the 4-column day grid; the 7 languages. Behaviour (data, requests, two-step save) is pinned by the
/// WEB-a / WEB-b tests, unchanged.
/// </summary>
public sealed class VisitWorkspaceDesignFitWebTests
{
    private static readonly string[] Cultures = ["", "en", "tr", "fr", "es", "zh", "ar", "ru"];

    private static readonly string[] NewScriptKeys =
    [
        "TodayBadge", "CapacityWeekLine", "FilterAllStatuses", "FilterStatusCount", "CardReportLeft", "CardMarkLeft", "CardLocked",
        "CardMovedTo", "MonthVisits", "LegendPinned", "LegendUnplanned", "LegendProduct", "DetailPinned", "DetailUnplanned",
        "MinutesShort", "ContentSteps", "AboutMinutes", "EstimateLine", "EstimateLineNoReminder", "DetailFrequency",
        "AlertTitle_draft", "AlertText_draft", "AlertTitle_planned", "AlertText_planned", "AlertTitle_today", "AlertText_today",
        "AlertTitle_report_missing", "AlertText_report_missing", "AlertTitle_reported", "AlertText_reported",
        "AlertTitle_missed", "AlertText_missed", "AlertTitle_missed_locked", "AlertText_missed_locked", "AlertTitle_expired", "AlertText_expired",
        "DlgTab_cancel", "DlgTab_notDone", "DlgTab_reschedule", "DlgMean_cancel", "DlgMean_notDone", "DlgMean_reschedule",
        "DlgConfirm_cancel", "DlgConfirm_notDone", "DlgConfirm_reschedule",
        "TargetsSub", "SummaryTitle", "SummaryDoctors", "SummaryPharmacies", "SummaryAccounts", "NoSelection"
    ];

    private static readonly string[] NewViewKeys = ["ViewLabel", "ViewDay", "ViewWeek", "ViewMonth", "TargetsHeading", "MapPlaceholder"];

    // ── 1 · the pure layout rules (Node, workspace-core.js) ─────────────────────────────────────────────────────

    [Fact]
    public void A1_the_card_month_dialog_day_head_and_duration_rules_follow_the_mockup_and_the_user_decision()
    {
        var node = RunNode(Require("VisitWorkspace", "workspace-core.js", "C") + """
            const now = Date.parse('2026-10-13T08:00:00Z');
            const at = h => new Date(now + h * 3600e3).toISOString();
            const out = {};
            // T7 / L4 — the card
            const rm = C.cardModel({ workStatus: 'report_missing', reportDeadline: at(31), startTime: '09:00', endTime: '09:30', targetDisplayName: 'Dr. A', accountDisplayName: 'H',
              plannedContent: [{ productName: 'Tutukon' }, { productName: 'Almiba' }, { productName: 'Beta', role: 'promo' }] }, { nowMs: now });
            out.rm = { key: rm.bottom.key, h: rm.bottom.args[0], chips: rm.chips.map(c => c.name + ':' + c.promo), time: rm.time, account: rm.account, compact: rm.compact };
            const mi = C.cardModel({ workStatus: 'missed', reportDeadline: at(3) }, { nowMs: now });
            out.missed = { key: mi.bottom.key, h: mi.bottom.args[0], icon: mi.icon };
            const ml = C.cardModel({ workStatus: 'missed', reportDeadline: at(-1) }, { nowMs: now });
            out.missedLocked = { key: ml.bottom.key, icon: ml.icon };
            const rs = C.cardModel({ workStatus: 'rescheduled', rescheduledToPlannedVisitId: 'n1' }, { nowMs: now, visits: [{ plannedVisitId: 'n1', plannedDate: '2026-10-20' }] });
            out.moved = { key: rs.bottom.key, arg: rs.bottom.args[0], date: rs.bottom.date };
            out.movedUnknown = C.cardModel({ workStatus: 'rescheduled', rescheduledToPlannedVisitId: 'zz' }, { nowMs: now, visits: [] }).bottom;
            out.flags = (m => [m.pinned, m.unplanned, m.rescheduledFrom])(C.cardModel({ workStatus: 'planned', isPinned: true, source: 'unplanned', rescheduledFromPlannedVisitId: 'x' }, {}));
            out.compact = [C.compactCard(20, false), C.compactCard(30, false), C.compactCard(30, true), C.compactCard(45, true), C.compactCard(null, true)];
            out.tiny = [C.tinyCard(20), C.tinyCard(25), C.tinyCard(null)];
            // the duration (user decision): the steps' minutes > the planned length > nothing
            out.steps = C.estimate({ startTime: '09:00', endTime: '09:30', plannedContent: [{ steps: [{ durationMinutes: 5 }, { targetMinutes: 7 }] }, { steps: [{ durationMinutes: 3 }] }] });
            out.partial = C.estimate({ startTime: '09:00', endTime: '09:30', plannedContent: [{ steps: [{ durationMinutes: 5 }] }, { steps: [{ title: 'x' }] }] });
            out.durationOnly = C.estimate({ durationMinutes: 25, plannedContent: [] });
            out.none = C.estimate({ plannedContent: [{ steps: [] }] });
            // T9 — the month cell
            out.month = C.monthCell([{ plannedDate: 'd', workStatus: 'missed' }, { plannedDate: 'd', workStatus: 'draft' }, { plannedDate: 'd', workStatus: 'cancelled' }, { plannedDate: 'e', workStatus: 'planned' }], 'd');
            // P1 — the one dialog's tabs
            const tabs = (v, today, p) => C.dialogTabs(v, today, p).map(t => t.key + ':' + t.enabled).join(',');
            out.tabsMissed = tabs({ workStatus: 'missed', plannedVisitId: 'a' }, '2026-10-13', { record: true, manage: true });
            out.tabsFuture = tabs({ workStatus: 'planned', plannedVisitId: 'a', plannedDate: '2026-10-14' }, '2026-10-13', { manage: true });
            out.tabsPast = tabs({ workStatus: 'planned', plannedVisitId: 'a', plannedDate: '2026-10-12' }, '2026-10-13', { manage: true });
            // T5 — the day head
            out.day = C.dayHead({ date: '2026-10-13', capacityMinutes: 480, plannedMinutes: 240, freeMinutes: 240 }, 3, '2026-10-13');
            out.dayOver = C.dayHead({ date: '2026-10-14', capacityMinutes: 480, plannedMinutes: 600, freeMinutes: -120 }, 9, '2026-10-13');
            out.legend = C.LEGEND;
            // L8 — the product distribution
            out.dist = C.productDistribution([{ workStatus: 'planned', plannedContent: [{ productName: 'B' }, { productName: 'A' }] }, { workStatus: 'draft', plannedContent: [{ productName: 'A' }] }, { workStatus: 'cancelled', plannedContent: [{ productName: 'B' }, { productName: 'B' }] }]);
            // D3 — the status box
            out.alertMissed = C.alertFor({ workStatus: 'missed', reportDeadline: at(3) }, now);
            out.alertLocked = C.alertFor({ workStatus: 'missed', reportDeadline: at(-2) }, now);
            out.alertToday = C.alertFor({ workStatus: 'today', startTime: '10:15' }, now);
            process.stdout.write(JSON.stringify(out));
            """);
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;

        var rm = r.GetProperty("rm");
        Assert.Equal("CardReportLeft", rm.GetProperty("key").GetString());
        Assert.InRange(rm.GetProperty("h").GetInt32(), 30, 31);
        Assert.Equal(new[] { "Tutukon:true", "Almiba:false", "Beta:true" }, rm.GetProperty("chips").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal("09:00–09:30", rm.GetProperty("time").GetString());
        Assert.Equal("H", rm.GetProperty("account").GetString());
        Assert.False(rm.GetProperty("compact").GetBoolean());

        Assert.Equal("CardMarkLeft", r.GetProperty("missed").GetProperty("key").GetString());
        Assert.InRange(r.GetProperty("missed").GetProperty("h").GetInt32(), 2, 3);
        Assert.Equal("CardLocked", r.GetProperty("missedLocked").GetProperty("key").GetString());
        Assert.Equal("bx-lock-alt", r.GetProperty("missedLocked").GetProperty("icon").GetString());
        Assert.Equal("CardMovedTo", r.GetProperty("moved").GetProperty("key").GetString());
        Assert.Equal("2026-10-20", r.GetProperty("moved").GetProperty("arg").GetString());
        Assert.True(r.GetProperty("moved").GetProperty("date").GetBoolean());
        Assert.Equal(JsonValueKind.Null, r.GetProperty("movedUnknown").ValueKind);
        Assert.Equal(new[] { true, true, true }, r.GetProperty("flags").EnumerateArray().Select(x => x.GetBoolean()));
        Assert.Equal(new[] { true, false, true, false, false }, r.GetProperty("compact").EnumerateArray().Select(x => x.GetBoolean()));
        Assert.Equal(new[] { true, false, false }, r.GetProperty("tiny").EnumerateArray().Select(x => x.GetBoolean())); // one line only under 25 minutes

        Assert.Equal(15, r.GetProperty("steps").GetProperty("minutes").GetInt32());
        Assert.Equal("steps", r.GetProperty("steps").GetProperty("source").GetString());
        Assert.Equal(30, r.GetProperty("partial").GetProperty("minutes").GetInt32()); // one product without step minutes → the planned length
        Assert.Equal("planned", r.GetProperty("partial").GetProperty("source").GetString());
        Assert.Equal(25, r.GetProperty("durationOnly").GetProperty("minutes").GetInt32());
        Assert.Equal(JsonValueKind.Null, r.GetProperty("none").ValueKind);                // neither → nothing shown

        Assert.Equal(2, r.GetProperty("month").GetProperty("count").GetInt32());           // a cancelled visit does not count
        Assert.Equal(3, r.GetProperty("month").GetProperty("dots").GetArrayLength());      // …but its status dot shows

        Assert.Equal("cancel:false,notDone:true,reschedule:true", r.GetProperty("tabsMissed").GetString());
        Assert.Equal("cancel:true,notDone:false,reschedule:false", r.GetProperty("tabsFuture").GetString());
        Assert.Equal("cancel:false,notDone:false,reschedule:false", r.GetProperty("tabsPast").GetString()); // a past day takes no cancel

        var day = r.GetProperty("day");
        Assert.True(day.GetProperty("isToday").GetBoolean());
        Assert.Equal(50, day.GetProperty("pct").GetInt32());
        Assert.Equal(3, day.GetProperty("count").GetInt32());
        Assert.Equal(240, day.GetProperty("freeMinutes").GetInt32());
        Assert.True(r.GetProperty("dayOver").GetProperty("over").GetBoolean());
        Assert.Equal(0, r.GetProperty("dayOver").GetProperty("freeMinutes").GetInt32());
        Assert.False(r.GetProperty("dayOver").GetProperty("isToday").GetBoolean());
        Assert.Equal(new[] { "draft", "planned", "today", "report_missing", "reported", "missed", "cancelled" }, r.GetProperty("legend").EnumerateArray().Select(x => x.GetString()));

        Assert.Equal("[{\"name\":\"A\",\"count\":2},{\"name\":\"B\",\"count\":1}]", r.GetProperty("dist").GetRawText());

        Assert.Equal("AlertTitle_missed", r.GetProperty("alertMissed").GetProperty("titleKey").GetString());
        Assert.Equal("AlertTitle_missed_locked", r.GetProperty("alertLocked").GetProperty("titleKey").GetString());
        Assert.Equal("AlertText_missed_locked", r.GetProperty("alertLocked").GetProperty("textKey").GetString());
        Assert.Equal("10:15", r.GetProperty("alertToday").GetProperty("time").GetString());
    }

    // ── 2 · the page in a fake DOM: one calendar card, the cards, the detail panel, ONE E2 dialog, the month view ──

    [Fact]
    public void A2_the_page_draws_one_calendar_card_the_cards_the_detail_panel_one_dialog_and_the_month_view()
    {
        var smoke = Path.Combine(Path.GetDirectoryName(WebRoot())!, "Diten.Web.Tests", "Js", "visit-workspace-design-smoke.js").Replace("\\", "/");
        var crm = Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM").Replace("\\", "/");
        var node = RunNode("process.argv[2] = '" + crm + "'; require('" + smoke + "');");
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;
        string S(string name) => r.GetProperty(name).GetString()!;
        string H(string id) => r.GetProperty(id).GetProperty("html").GetString()!;
        bool Hidden(string id) => r.GetProperty(id).GetProperty("hidden").GetBoolean();

        // the grid: drawn inside create without a throw; no strip; FullCalendar's own toolbar and all-day row off
        Assert.Equal(JsonValueKind.Null, r.GetProperty("createError").ValueKind);
        Assert.False(r.GetProperty("stripTouched").GetBoolean());
        Assert.False(r.GetProperty("headerToolbar").GetBoolean());
        Assert.False(r.GetProperty("allDaySlot").GetBoolean());
        Assert.Equal("08:30:00", S("slotMinTime"));
        Assert.False(r.GetProperty("weekends").GetBoolean());
        var untimed = r.GetProperty("events").EnumerateArray().Single(e => e.GetProperty("id").GetString() == "k1");
        Assert.False(untimed.GetProperty("allDay").GetBoolean()); // no all-day row: an untimed draft sits at the first slot, marked
        Assert.Contains("vw-untimed", untimed.GetProperty("cls").EnumerateArray().Select(x => x.GetString()));

        // T1 / T4 / L3 — the period chip; the week head inside the card
        Assert.Equal("<bdi>2026 · 4. Dönem</bdi>", S("period"));
        Assert.False(r.GetProperty("periodHidden").GetBoolean());
        Assert.StartsWith("42. Hafta · ", S("weekTitle"));
        Assert.Equal("vw-state-chip vw-wst-approved", S("weekBadge"));
        Assert.Equal("Bu hafta 40 sa kapasite · 10 sa planlı", S("capacity"));
        Assert.Equal("⚠ 2 ziyaret sığmadı", S("unplaced"));
        Assert.False(r.GetProperty("reopenHidden").GetBoolean());
        Assert.False(r.GetProperty("unplannedHidden").GetBoolean());
        Assert.Equal("Tüm durumlar", S("statusLabel"));                  // T8 — the multi status filter's label

        // T10 / L9 — the legend
        var legend = S("legend");
        Assert.Contains("vw-swatch vw-st-draft vw-dashed", legend);
        Assert.Contains("bxs-pin", legend);
        Assert.Contains("vw-pchip vw-pchip-promo", legend);
        Assert.Contains("vw-pchip vw-pchip-reminder", legend);

        // T7 / L4 — the card: time, bold name, institution, the product chips (promo filled / reminder outlined)
        var card = S("card");
        Assert.Contains("<span class=\"vw-time\">10:15–10:45</span>", card);
        Assert.Contains("<div class=\"vw-name\"><bdi>Dr. Ayşe Kaya</bdi></div>", card);
        Assert.Contains("<div class=\"vw-acc\"><bdi>Hacettepe Hastanesi</bdi></div>", card);
        Assert.Contains("<span class=\"vw-pchip vw-pchip-promo\"><bdi>Tutukon</bdi></span>", card);
        Assert.Contains("<span class=\"vw-pchip vw-pchip-reminder\"><bdi>Almiba</bdi></span>", card);
        Assert.Contains("bxs-pin", card);
        var compact = S("compactCard");                                  // a 20-minute visit: compact + the bottom line
        Assert.Contains("vw-compact vw-tiny", compact);
        Assert.Contains("İşaretlemek için", compact);

        // T5 — the day head: the bar + "N visits · X h free"
        Assert.Contains("vw-day-bar", S("header"));
        Assert.Contains("2 ziyaret · boş 7,5 sa", S("header"));

        // D1 — the status chip, the name, the specialty LABEL (never the code), the badges
        Assert.Contains("vw-status-chip vw-st-planned", H("vw-detail-chips"));
        Assert.Contains("Güne sabit", H("vw-detail-chips"));
        Assert.Contains("<bdi>Kardiyoloji</bdi>", H("vw-detail-tags"));
        Assert.DoesNotContain("SPEC_CARD_X", H("vw-detail-tags"));
        Assert.Contains("<bdi>KOL</bdi>", H("vw-detail-tags"));
        // D2 — the institution block: name, address, date · time · length
        Assert.Equal("<bdi>Hacettepe Hastanesi</bdi>", H("vw-detail-account"));
        Assert.Equal("<bdi>Sıhhiye, Ankara</bdi>", H("vw-detail-address"));
        Assert.False(Hidden("vw-detail-address"));
        Assert.EndsWith("10:15–10:45 · 30 dk", H("vw-detail-when"));
        // D3 — the status box
        Assert.Contains("<strong>Hafta onaylı</strong>", H("vw-detail-band"));
        // D4 — numbered product cards, the frequency, the estimate (no step minutes yet → the planned length)
        Assert.Contains("<span class=\"vw-prod-no\">1</span>", H("vw-detail-content"));
        Assert.Contains("<span class=\"vw-prod-no\">2</span>", H("vw-detail-content"));
        Assert.Contains("vw-role vw-role-promo", H("vw-detail-content"));
        Assert.Contains("vw-role vw-role-reminder", H("vw-detail-content"));
        Assert.Contains("2 içerik adımı", H("vw-detail-content"));
        Assert.StartsWith("Sıklık: dönemde 4 · ", H("vw-detail-frequency"));
        Assert.EndsWith("1 tanıtım + 1 hatırlatma + rapor ≈ 30 dk", H("vw-detail-estimate"));
        // D6 — the actions at the bottom (the data-dialog contract kept)
        Assert.Contains("data-dialog=\"cancel\"", H("vw-detail-actions"));
        Assert.Contains("flex-fill", H("vw-detail-actions"));
        Assert.Contains("data-dialog=\"reschedule\"", S("missedActions"));

        // P1 — ONE dialog, three tabs, the invalid one disabled; the subtitle; the tab's meaning; the confirm text
        var tabs = S("dlgTabs");
        Assert.Equal(3, Regex.Matches(tabs, "data-tab=\"").Count);
        Assert.Contains("data-tab=\"cancel\" aria-selected=\"false\" disabled", tabs);
        Assert.Contains("class=\"vw-tab active\" data-tab=\"reschedule\" aria-selected=\"true\">", tabs);
        Assert.Contains("<bdi>Dr. Can</bdi> · ", S("dlgTarget"));
        Assert.Equal("Başka güne kaydırın; yeni tarih seçin.", S("dlgMean"));
        Assert.Equal("Ziyareti ertele", S("dlgSave"));
        // P2 — the reschedule days: day cards (day, mini bar, load) in the 4-column grid
        var days = S("dlgDays");
        Assert.Equal(6, Regex.Matches(days, "class=\"vw-day-option\"").Count);
        Assert.Contains("vw-mini", days);
        Assert.Contains("1 ziyaret · boş 7 sa", days);

        // T9 — the month view: one summary per day (count + status dots); the week head hides; a day click → the Day view
        Assert.Equal(2, r.GetProperty("monthEvents").GetArrayLength()); // Tue + Fri
        Assert.Contains("2 ziyaret", S("monthCell"));
        Assert.Contains("vw-dot vw-st-planned", S("monthCell"));
        Assert.True(r.GetProperty("monthWeekHeadHidden").GetBoolean());
        var views = r.GetProperty("views").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains("dayGridMonth", views);
        Assert.Contains(views, v => v!.StartsWith("timeGridDay@", StringComparison.Ordinal));
    }

    // ── 3 · the view and the stylesheet: one calendar card, no strip, the 4-column grid, + unplanned in both modes ──

    [Fact]
    public void A3_the_view_has_one_calendar_card_with_the_filters_and_views_and_no_week_strip()
    {
        var view = View();
        var js = Script("visit-workspace.js");
        var css = Script("visit-workspace.css");
        foreach (var source in new[] { view, js, css }) { Assert.DoesNotContain("vw-week-strip", source); }

        var head = view.IndexOf("<div class=\"vw-cal-head\">", StringComparison.Ordinal);
        var weekHead = view.IndexOf("id=\"vw-week-head\"", StringComparison.Ordinal);
        Assert.True(head > 0 && weekHead > head);
        var cardHead = view[head..weekHead];
        foreach (var id in new[] { "vw-prev", "vw-next", "vw-today", "vw-range-title", "vw-filter-account", "vw-filter-status", "vw-filter-product" })
        {
            Assert.Contains("id=\"" + id + "\"", cardHead);
        }
        foreach (var v in new[] { "day", "week", "month" }) { Assert.Contains("data-view=\"" + v + "\"", cardHead); }
        Assert.Contains("id=\"vw-legend\"", view);
        Assert.Contains("id=\"vw-period-chip\"", view);

        // T2 / L1 — Plan / Execute under the title on the left; + unplanned visit outside the CanPlan block (both modes)
        var canPlan = view.IndexOf("@if (Model.CanPlan)", StringComparison.Ordinal);
        var unplanned = view.IndexOf("id=\"vw-unplanned-open\"", StringComparison.Ordinal);
        var blockEnd = view.IndexOf("}", view.IndexOf("id=\"vw-mode-execute\"", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.True(canPlan > 0 && blockEnd > canPlan && unplanned > blockEnd);
        if (PlanSmoke() is { } p) { Assert.True(p.GetProperty("unplannedInPlan").GetBoolean()); }

        // P1 / P2 — ONE dialog with the tab list; the reschedule days in 4 columns
        Assert.Contains("id=\"vw-dlg-tabs\"", view);
        Assert.Contains("id=\"vw-dlg-days\" class=\"vw-day-grid\"", view);
        Assert.Contains(".vw-day-grid { display: grid; grid-template-columns: repeat(4, 1fr);", css);
        Assert.Equal(1, Regex.Matches(view, "class=\"modal fade\" id=\"vw-dialog\"").Count);

        // T6 — the grid: today's column light blue, the now line; the calendar options in the script
        Assert.Contains("fc-day-today", css);
        Assert.Contains("fc-timegrid-now-indicator-line", css);
        Assert.Contains("fc.setOption('headerToolbar', false);", js);
        Assert.Contains("fc.setOption('allDaySlot', false);", js);
        // the CT order stays: the lookup before DitenCalendar.create
        Assert.True(js.IndexOf("state.byId = byId;", StringComparison.Ordinal) < js.IndexOf("window.DitenCalendar.create(", StringComparison.Ordinal));
    }

    // ── 4 · 7 languages: every new key filled, no echo, arguments where they belong ─────────────────────────────

    [Fact]
    public void A4_the_new_texts_exist_in_all_seven_languages_without_echo()
    {
        foreach (var key in NewScriptKeys) { Assert.Contains(key, VisitWorkspaceIndex.ScriptKeys); }
        foreach (var key in NewViewKeys) { Assert.Contains(key, VisitWorkspaceIndex.ViewKeys); }

        var en = Resx("en");
        foreach (var culture in Cultures)
        {
            var resx = Resx(culture);
            foreach (var key in NewScriptKeys.Concat(NewViewKeys))
            {
                Assert.True(resx.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value), culture + ":" + key);
                Assert.NotEqual(key, value);
                // a text with arguments in English has them in every language, and the other way round
                for (var i = 0; i < 3; i++)
                {
                    var arg = "{" + i + "}";
                    Assert.True(en[key].Contains(arg) == value.Contains(arg), culture + ":" + key + " " + arg);
                }
            }
        }
        var tr = Resx("tr");
        Assert.Equal("{0}. Hafta", tr["WeekLabel"]);
        Assert.Equal("Taslak · otomatik", tr["WeekStateDraft"]);
        Assert.Equal("Bu hafta {0} kapasite · {1} planlı", tr["CapacityWeekLine"]);
        Assert.Equal("{0} tanıtım + {1} hatırlatma + rapor ≈ {2} dk", tr["EstimateLine"]);
        Assert.Equal("Sıklık: dönemde {0} · {1}", tr["DetailFrequency"]);
        Assert.Equal("Tüm durumlar", tr["FilterAllStatuses"]);
        Assert.Equal("Görünüm", tr["ViewLabel"]);
        Assert.Equal("التكرار: {0} في الفترة · {1}", Resx("ar")["DetailFrequency"]);
        foreach (var culture in new[] { "fr", "es", "zh", "ar", "ru" })
        {
            var resx = Resx(culture);
            Assert.True(NewScriptKeys.Count(k => resx[k] == en[k]) <= 3, culture + ": too many English echoes");
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────────

    private static JsonElement? PlanSmoke()
    {
        var smoke = Path.Combine(Path.GetDirectoryName(WebRoot())!, "Diten.Web.Tests", "Js", "visit-workspace-plan-smoke.js").Replace("\\", "/");
        var crm = Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM").Replace("\\", "/");
        var node = RunNode("process.argv[2] = '" + crm + "'; process.argv[3] = 'draft'; require('" + smoke + "');");
        return node is null ? null : JsonDocument.Parse(node).RootElement.Clone();
    }

    private static string Require(string folder, string file, string name)
        => "const " + name + " = require('" + Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", folder, file).Replace("\\", "/") + "');\n";

    private static string? RunNode(string code)
    {
        var temp = Path.Combine(Path.GetTempPath(), "vw2c-" + Guid.NewGuid().ToString("N") + ".js");
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

    private static string Script(string file)
        => File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "VisitWorkspace", file)).Replace("\r\n", "\n");

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

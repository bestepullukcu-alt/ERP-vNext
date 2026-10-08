using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VP-4H — mockup v2 alignment, application-culture dates, product names and the empty week, on the REAL scripts,
/// views and resources (Acceptance 1–12).
/// </summary>
public sealed class VisitPlanningMockupCultureWebTests
{
    private static readonly string[] Cultures = ["", "en", "tr", "fr", "es", "zh", "ar", "ru"];

    // ── 1 · no browser-language or fixed US-English formatting; the formatter reads the app culture ───────────────

    [Fact]
    public void No_visit_planning_script_formats_in_the_browser_language_or_us_english()
    {
        foreach (var file in Directory.GetFiles(ScriptDir(), "*.js"))
        {
            var js = File.ReadAllText(file);
            Assert.False(Regex.IsMatch(js, @"toLocale\w*\(\s*undefined"), Path.GetFileName(file) + ": toLocale*(undefined");
            Assert.False(Regex.IsMatch(js, @"['""]en-US['""]"), Path.GetFileName(file) + ": 'en-US'");
        }
        var format = Script("format.js");
        Assert.Contains("const culture = () => ((document.documentElement && document.documentElement.lang) || '').trim() || 'tr';", format);
        // every Visit Planning page loads the formatter right after the L10n bridge
        foreach (var view in new[] { "Index.cshtml", "Details.cshtml", "Create.cshtml", "Edit.cshtml" })
        {
            var v = View(view);
            var l10n = v.IndexOf("index.l10n.js", StringComparison.Ordinal);
            var fmt = v.IndexOf("VisitPlanning/format.js", StringComparison.Ordinal);
            Assert.True(l10n > 0 && fmt > l10n, view + " loads format.js after index.l10n.js");
        }
        // and the page scripts use it
        Assert.Contains("const VPF = window.VisitPlanningFormat;", Script("weeks.js"));
        Assert.Contains("const VPF = window.VisitPlanningFormat;", Script("details.js"));
        Assert.Contains("const VPF = window.VisitPlanningFormat;", Script("header.js"));
        Assert.Contains("const VPF = window.VisitPlanningFormat;", Script("doctor-panel.js"));
    }

    // ── 2 · the formatter's shapes: "Pzt 5 Eki", "5–9 Eki", "28 Eyl–2 Eki", "6,5 sa" / "Mon 5 Oct", "6.5 h" ────────

    [Fact]
    public void The_formatter_builds_day_range_and_hour_labels_in_the_app_culture()
    {
        var js = Script("format.js");
        // English day-first ("Mon 5 Oct"), every other language (tr, ar, …) as Intl gives it
        Assert.Contains("return /^en(-|$)/i.test(c) ? 'en-GB' : c;", js);
        Assert.Contains("new Intl.DateTimeFormat(loc || dateCulture(), options)", js);
        Assert.Contains("new Intl.NumberFormat(culture(), options)", js);
        Assert.Contains("const dayShort = v => fmtDate(v, { weekday: 'short' }).replace(/\\.$/, '');", js);
        Assert.Contains("const dayMonth = v => fmtDate(v, { day: 'numeric', month: 'short' });", js);
        Assert.Contains("return valid(d) ? dayShort(d) + ' ' + dayMonth(d) : '—';", js);
        // same month "5–9 Eki", across months "28 Eyl–2 Eki" (en dash)
        Assert.Contains("return (sameMonth ? dayNumber(a) : dayMonth(a)) + '–' + dayMonth(b);", js);
        // hours: one decimal through the culture's number format ("6,5" in tr, "6.5" in en) into the localized template
        Assert.Contains("const h = Math.round((Number(minutes) / 60) * 10) / 10;", js);
        Assert.Contains("return String(template || '{0} h').split('{0}').join(number(h, 1));", js);
        Assert.Equal("{0} sa", Resx("tr")["HoursFormat"]);
        Assert.Equal("{0} h", Resx("en")["HoursFormat"]);
        Assert.Contains("hours = minutes => VPF.hours(minutes, L.HoursFormat || '{0} h')", Script("header.js"));
        // the layout gives the culture
        Assert.Contains("Layout = \"_LayoutTenantShell\"", View("Details.cshtml"));
        var layout = File.ReadAllText(Path.Combine(WebRoot(), "Views", "Shared", "_LayoutTenantShell.cshtml"));
        Assert.Contains("<html lang=\"@currentCulture\"", layout);
    }

    // ── 3 · an empty week: only the empty state + "Generate this week" ─────────────────────────────────────────

    [Fact]
    public void An_empty_week_draws_only_the_empty_state_and_generate_this_week()
    {
        var js = Script("weeks.js");
        var detail = Between(js, "const renderDetail = () => {", "\n    };");
        var emptyAt = detail.IndexOf("if (w.status === 'empty') {", StringComparison.Ordinal);
        Assert.True(emptyAt > 0, "the empty branch");
        var branch = Between(detail, "if (w.status === 'empty') {", "}\n");
        Assert.Contains("parts.push(emptyWeekHtml());", branch);
        Assert.Contains("return;", branch);
        // the day rows, product and moved sections all come AFTER the early return
        foreach (var later in new[] { "dayRow(d, movable)", "L.ProductVisitsTitle", "pinOverflowHtml(p, ws)", "slipRow" })
        {
            var at = detail.IndexOf(later, StringComparison.Ordinal);
            Assert.True(at > emptyAt, later + " must not be drawn for an empty week");
        }
        var empty = Between(js, "const emptyWeekHtml = () =>", "'</div>';");
        Assert.Contains("border-style:dashed !important", empty);
        Assert.Contains("L.EmptyWeekHint", empty);
        Assert.Contains("js-wk-action\" data-action=\"rebuild\">' + esc(L.GenerateWeek", empty);
        Assert.Contains("canGenerate && !page.isLegacy()", empty);
        Assert.Equal("Bu haftayı üret", Resx("tr")["GenerateWeek"]);
    }

    // ── 4 · the strip card: vertical 128px; an empty week dashed with "—" ─────────────────────────────────────────

    [Fact]
    public void The_strip_card_is_a_vertical_128px_card_and_an_empty_week_is_dashed()
    {
        var js = Script("weeks.js");
        Assert.Contains("const STRIP_CARD = 'flex:0 0 128px;';", js);
        var card = Between(js, "const stripCard = m => {", "\n    };");
        Assert.Contains("d-flex flex-column gap-1", card);
        Assert.Contains("(empty ? 'border-style:dashed !important;' : '')", card);
        Assert.Contains("esc(empty ? '—' : fmt(L.VisitCountShort || '{0}', m.visits))", card);
        Assert.Contains("esc(m.title)", card);
        Assert.Contains("esc(m.range)", card);
        Assert.Contains("esc(m.label)", card);
        Assert.Contains("L.TodayLabel", card);
        Assert.Contains("progress-bar", card);
    }

    // ── 5 · the doctors of the week: "dönemde 1 (varsayılan)", the period strip, the "done" legend ────────────────

    [Fact]
    public void The_week_doctor_list_reads_the_default_frequency_draws_the_period_strip_and_a_done_legend()
    {
        var js = Script("weeks.js");
        var freq = Between(js, "const frequencyLine = (s, status) => {", "\n    };");
        Assert.Contains("(L.FrequencyDefaultOne || '')", freq);
        Assert.Contains("status.done + ' / ' + (status.remaining != null ? status.remaining : '—')", freq);
        Assert.Contains("const stripRects = (cid, firstDraft) => page.weeks().map(pw => {", js);
        Assert.Contains("style=\"width:14px;height:6px\"", js);
        // "done" = a completed report (4G reportStatus); without it a past / fixed visit is approved
        var state = Between(js, "const visitState = (s, firstDraftWeek) => {", "\n    };");
        Assert.Contains("DONE_REPORT.indexOf(String(s.reportStatus).toLowerCase()) > -1) return 'done';", state);
        Assert.Contains("if (s.isFixed || s.plannedDate < todayYmd()) return 'approved';", state);
        Assert.Contains("const STATE_LABEL = { done: 'StateDone', approved: 'StateApproved', draft: 'StateDraft', projected: 'StateProjected' };", js);
        var doctors = Between(js, "const renderDoctors = (ws, weekSlots) => {", "\n    };");
        Assert.Contains("el('vp-wk-doctors')", doctors);
        Assert.Contains("Object.keys(STATE_LABEL).map(k =>", doctors); // the legend, "done" first
        Assert.Contains("fmt(L.ShowAllCount || '{0}', doctors.length)", doctors);
        Assert.Contains("L.WeekDoctorsHeading", doctors);
        // detail and doctors side by side
        var view = View("Details.cshtml");
        var detail = view.IndexOf("id=\"vp-wk-detail\"", StringComparison.Ordinal);
        var docs = view.IndexOf("id=\"vp-wk-doctors\"", StringComparison.Ordinal);
        Assert.True(detail > 0 && docs > detail);
        Assert.Equal("dönemde 1 (varsayılan)", Resx("tr")["FrequencyDefaultOne"]);
        Assert.Equal("yapıldı", Resx("tr")["StateDone"]);
    }

    // ── 6 · the doctor panel lists EVERY week of the period ─────────────────────────────────────────────────────

    [Fact]
    public void The_doctor_panel_lists_every_week_of_the_period_with_three_boxes_and_the_next_visit_card()
    {
        var js = Script("doctor-panel.js");
        Assert.Contains("const weeks = page.weeks();", js);
        Assert.Contains("weeks.map(w => {", js);
        Assert.Contains("const slot = slots.find(s => (s.weekStart || mondayYmd(s.plannedDate)) === w.weekStart) || null;", js);
        Assert.Contains("(state === 'none' ? '<span class=\"text-muted\">—</span>'", js);
        Assert.Contains("(w.status === 'past' ? ' opacity-50' : '')", js);
        Assert.Contains("(L.FrequencyDefaultOne || '')", js);
        Assert.Contains("fmt(L.DurationLine", js);
        Assert.Contains("L.NextContentSoon", js);
        Assert.Equal("Sıradaki içerik: yakında", Resx("tr")["NextContentSoon"]);
    }

    // ── 7 · the Targets doctor table: 8 columns, no responsive collapse, Products + Status visible ───────────────

    [Fact]
    public void The_targets_doctor_table_has_eight_columns_and_no_responsive_collapse()
    {
        var js = Script("details.js");
        var config = Between(js, "const contactsConfig = list => ({", "\n    });");
        Assert.Contains("responsive: false, autoWidth: false,", config);
        Assert.DoesNotMatch(@"responsive:\s*(true|\{)", config);
        Assert.Contains("columns: [{ data: null }, { data: 'name' }, { data: 'specialty' }, { data: null }, { data: null }, { data: null }, { data: null }, { data: null }]", config);
        Assert.Contains("targets: 3", config);
        Assert.Contains("targets: 7", config);
        Assert.Contains("statusCell(row)", config);
        var view = View("Details.cshtml");
        var head = Between(view, "<table id=\"dt-vp-contacts\"", "</thead>");
        Assert.Equal(8, Regex.Matches(head, "<th>").Count);
        Assert.True(head.IndexOf("ColProducts", StringComparison.Ordinal) < head.IndexOf("ColFrequency", StringComparison.Ordinal));
        Assert.Contains("Localizer[\"ColStatus\"]", head);
        // the legend stays under the table; "select all (N)" and the counted quick filters
        Assert.Contains("@Localizer[\"LegendPromo\"]", view);
        Assert.Contains("<span id=\"vp-select-all-count\"></span>", view);
        Assert.Contains("<span class=\"vp-quick-count\" data-quick=\"due\"></span>", view);
        // the left column is the rep's territory accounts as a list; "add clinic / hospital" is gone
        Assert.Contains("id=\"vp-acc-list\"", view);
        Assert.Contains("Localizer[\"MyAccountsTitle\"]", view);
        Assert.DoesNotContain("id=\"vp-add-account\"", view);
        Assert.Contains("const ACCOUNT_PAGE = 50;", js);
        Assert.Contains("api('/my-accounts?search=' + encodeURIComponent(term) + '&page=' + next + '&pageSize=' + ACCOUNT_PAGE)", js);
    }

    // ── 8 · the Targets summary and the header read the SAME week load ───────────────────────────────────────────

    [Fact]
    public void The_targets_summary_and_the_header_read_one_week_load()
    {
        var format = Script("format.js");
        var load = Between(format, "const weekLoad = (preview, weekStart) => {", "\n    };");
        Assert.Contains("p.days.filter(d => d.weekStart === weekStart)", load);
        Assert.Contains("days.reduce((sum, d) => sum + (Number(d.plannedMinutes) || 0), 0)", load);
        Assert.Contains("week ? week.plannedMinutes : null", load);
        var header = Script("header.js");
        Assert.Contains("const load = VPF.weekLoad(p, ws);", header);
        Assert.Contains("setText('vp-cap-week-planned', load.planned != null ? hours(load.planned) : '—');", header);
        Assert.DoesNotContain("hours(wc.plannedMinutes)", header);
        var summary = Between(Script("targets.js"), "const renderSummary = ", "\n    };");
        Assert.Contains("window.VisitPlanningFormat.weekLoad(p, page.state.weekStart)", summary);
        Assert.Contains("hours(load.planned)", summary);
        Assert.DoesNotContain("wc.plannedMinutes", summary);
    }

    // ── 9 · a product reads by its name, the code only without one ────────────────────────────────────────────────

    [Fact]
    public void Every_chip_and_distribution_reads_the_product_name_before_the_code()
    {
        Assert.Contains("const productLabel = p => (p && (p.productName || p.productCode)) || '—';", Script("format.js"));
        var weeks = Script("weeks.js");
        Assert.Contains("esc(VPF.productLabel(c))", weeks);
        Assert.Contains("name: VPF.productLabel(o)", weeks);
        Assert.Contains("esc(VPF.productLabel(c))", Script("doctor-panel.js"));
        var targets = Script("targets.js");
        Assert.Contains("esc(window.VisitPlanningFormat.productLabel(x)) + ' <strong>' + x.doctorCount", targets); // distribution
        Assert.Contains("esc(window.VisitPlanningFormat.productLabel(it))", targets); // chips
        Assert.Contains("productName: x.productName", targets);
        // nowhere a chip still prints the bare code
        foreach (var file in Directory.GetFiles(ScriptDir(), "*.js"))
        {
            Assert.False(Regex.IsMatch(File.ReadAllText(file), @"esc\(\w+\.productCode \|\| '—'\)"), Path.GetFileName(file));
        }
    }

    // ── 10 · approve / reopen from the Weeks tab come back to the Weeks tab, same week ────────────────────────────

    [Fact]
    public void Approving_or_reopening_from_the_weeks_tab_returns_to_the_weeks_tab_on_the_same_week()
    {
        var js = Script("weeks.js");
        Assert.Contains("if (k === 'approve') { rememberWeeksReturn(); page.request('approve-week'); }", js);
        Assert.Contains("else if (k === 'reopen') { rememberWeeksReturn(); page.request('reopen-week'); }", js);
        Assert.Contains("sessionStorage.setItem(RETURN_KEY, JSON.stringify({ session: sessionId, week: page.state.weekStart, at: Date.now() }))", js);
        Assert.Contains("note.session === sessionId && Date.now() - (note.at || 0) < RETURN_TTL_MS", js);
        var back = Between(js, "const returnToWeeks = () => {", "\n    };");
        Assert.Contains("page.selectWeek(note.week, 'force')", back);
        Assert.Contains("el('vp-tab-weeks-btn')", back);
        Assert.Contains("page.on('preview', () => { render(); returnToWeeks(); });", js);
        Assert.Contains("id=\"vp-tab-weeks-btn\"", View("Details.cshtml"));
    }

    // ── 11 · the route's week list is every week of the period (the selected one included) ──────────────────────

    [Fact]
    public void The_route_week_list_holds_every_period_week()
    {
        var js = Script("details.js");
        var selector = Between(js, "const buildWeekSelector = () => {", "\n    };");
        Assert.Contains("lastPreview.weeks.map((w, i) => i)", selector);
        Assert.Contains("const weeks = periodWeeks.length ? periodWeeks : weeksOf(scheduled);", selector);
        Assert.Contains("const chipLabel = VPF.dayLabel(date);", js);
    }

    // ── 12 · the new keys in seven languages (+ neutral) and the bridge; TR with diacritics ─────────────────────

    [Fact]
    public void The_new_keys_are_in_seven_languages_and_the_bridge()
    {
        var bridged = new[]
        {
            "NoAccountsFound", "DueThisWeekBadge", "EditWeekTargets", "EmptyWeekHint", "FrequencyDefaultOne", "NextContentSoon",
            "NoTimeShort", "ShowAllCount", "ShowLess", "WeekDoctorsHeading", "HistoryStateDone", "HistoryStateApproved",
            "HistoryStateDraft", "HistoryStateProjected", "ReasonNoNearDay", "StateDone", "WeekSubApproved", "WeekSubEmpty"
        };
        var viewOnly = new[] { "MyAccountsTitle", "AccountSearch", "ColStatus" };
        var bridge = View("_IndexL10n.cshtml");
        foreach (var key in bridged.Append("GenerateWeek"))
        {
            Assert.Contains($"{key} = Localizer[\"{key}\"].Value", bridge);
        }
        var en = Resx("en");
        var tr = Resx("tr");
        foreach (var culture in Cultures)
        {
            var resx = Resx(culture);
            foreach (var key in bridged.Concat(viewOnly))
            {
                Assert.True(resx.TryGetValue(key, out var value) && value.Length > 0, $"{key} missing in '{culture}'");
                if (key != "ShowAllCount") Assert.DoesNotContain("{0}", value); // an argless localizer value never holds {0}
            }
        }
        foreach (var key in bridged.Concat(viewOnly))
        {
            Assert.NotEqual(en[key], tr[key]);
        }
        Assert.Equal("Hesaplarım (bölgem)", tr["MyAccountsTitle"]);
        Assert.Equal("Bu haftadaki doktorlar", tr["WeekDoctorsHeading"]);
        Assert.Equal("yakın gün yok", tr["ReasonNoNearDay"]);
        Assert.Equal("Onaylı · Planlanan Ziyaretler'e yazıldı · salt okunur", tr["WeekSubApproved"]);
        Assert.Contains("no_near_day: 'ReasonNoNearDay'", Script("weeks.js"));
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────────

    private static string Between(string text, string start, string end)
    {
        var from = text.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, start);
        var to = text.IndexOf(end, from + start.Length, StringComparison.Ordinal);
        Assert.True(to > from, end);
        return text[from..(to + end.Length)];
    }

    private static string ScriptDir() => Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "VisitPlanning");

    private static string Script(string file) => File.ReadAllText(Path.Combine(ScriptDir(), file)).Replace("\r\n", "\n");

    private static string View(string file) =>
        File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "VisitPlanning", file)).Replace("\r\n", "\n");

    private static Dictionary<string, string> Resx(string culture)
    {
        var file = "VisitPlanningIndex" + (culture.Length == 0 ? "" : "." + culture) + ".resx";
        return XDocument.Load(Path.Combine(WebRoot(), "Resources", "Views", "CRM", "VisitPlanning", file)).Root!
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

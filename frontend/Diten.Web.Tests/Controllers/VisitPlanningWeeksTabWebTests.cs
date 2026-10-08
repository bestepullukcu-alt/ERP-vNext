using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VP-4D — the Weeks tab, the doctor panel's period view, moving visits between days, the mockup-aligned detail header
/// and the 4B E4 follow-ups, on the REAL scripts, views and resources.
/// </summary>
public sealed class VisitPlanningWeeksTabWebTests
{
    private static readonly string[] Cultures = ["", "en", "tr", "fr", "es", "zh", "ar", "ru"];

    // ── 1 · the period strip ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_strip_builds_every_week_from_weeks_and_week_capacity_with_holiday_half_day_warning_and_today_marks()
    {
        var js = Script("weeks.js");
        var model = Between(js, "const stripModel = () => {", "\n    };");
        Assert.Contains("page.weeks().map((w, i) =>", model);
        Assert.Contains("(p.weekCapacity || []).find(c => c.weekStart === w.weekStart)", model);
        Assert.Contains("Math.round(wc.plannedMinutes / wc.capacityMinutes * 100)", model);
        Assert.Contains("holidays: holidaysOf(w, p), halfDays: halfDaysOf(w, p), warnings: warningsOf(p, i, w.weekStart)", model);
        Assert.Contains("visits: w.visitCount || 0", model);
        Assert.Contains("isToday: today >= (w.from || w.weekStart) && today <= (w.to || w.weekStart)", model);
        Assert.Contains("off.has(ymd(d))", Between(js, "const holidaysOf = (w, p) => {", "\n    };"));
        Assert.Contains("(p.halfDayDates || [])", js);
        Assert.Contains("(p.shifted || []).filter(s => s.fromWeek === i).length", js);
        Assert.Contains("L.TodayLabel", js);
        // a legacy plan's written week reads "approved (old plan)"
        Assert.Contains("w.storedStatus === 'legacy' ? (L.LegacyWeekLabel || '')", js);
        // a click selects the week for the whole page
        Assert.Contains("page.selectWeek(item.dataset.ws, 'weeks')", js);

        var view = View("Details.cshtml");
        Assert.Contains("role=\"presentation\" id=\"vp-tab-weeks-item\">", view);
        Assert.Contains("<div class=\"tab-pane fade\" id=\"vp-tab-weeks\"", view);
        Assert.Contains("id=\"vp-wk-strip\"", view);
        foreach (var key in new[] { "AutoDraftRuleApprove", "AutoDraftRuleEven", "AutoDraftRuleDays", "RuleUnknownFrequency" })
        {
            Assert.Contains($"Localizer[\"{key}\"]", view);
        }
    }

    // ── 2 · day rows: the first 6 + "+N", the empty-day sentence, idle / over capacity ────────────────────────────

    [Fact]
    public void A_day_row_opens_into_its_first_six_visits_and_a_more_link_and_says_when_no_visit_falls_on_it()
    {
        var js = Script("weeks.js");
        Assert.Contains("const DAY_PREVIEW_LIMIT = 6;", js);
        var row = Between(js, "const dayRow = (day, movable) => {", "\n    };");
        Assert.Contains("const limit = full ? Infinity : DAY_PREVIEW_LIMIT;", row);
        Assert.Contains("const more = day.slots.length - DAY_PREVIEW_LIMIT;", row);
        Assert.Contains("fmt(L.MoreDoctors || '{0}', more)", row);
        Assert.Contains("L.NoVisitThisDay", row);
        Assert.Contains("s.idleMinutes > 0", row);
        Assert.Contains("s.overCapacity", row);
        Assert.Contains("fmt(L.DayCapacityFormat || '{0} / {1}', day.slots.length, day.cap)", row);
        // doctor + institution + chips + ≈ minutes; a doctor opens the panel
        var line = Between(js, "const visitLine = (s, movable) => {", "\n    };");
        Assert.Contains("chipsOf(s)", line);
        Assert.Contains("fmt(L.ApproxMinutes || '{0}', s.durationMinutes || 0)", line);
        Assert.Contains("js-wk-doctor", line);
        Assert.Contains("page.emit('doctor-panel:open', { contactId: doctor.dataset.cid", js);
        // a holiday / half day is marked; the daily cap comes from weekCapacity (half on a half day)
        Assert.Contains("kind === 'half' ? Math.floor(wc.dailyCap / 2)", js);
        Assert.Equal("Bu güne ziyaret düşmüyor.", Resx("tr")["NoVisitThisDay"]);
    }

    // ── 3 · moved / not placed with local reasons ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Moved_and_not_placed_joins_shifted_unscheduled_overflow_products_and_pin_overflow_with_local_reasons()
    {
        var js = Script("weeks.js");
        var slips = Between(js, "const slipItems = (p, i, ws, weekSlots) => {", "\n    };");
        var shift = Between(slips, "(p.shifted || [])", "}));");
        Assert.Contains("reason: reasonText(s.reason)", shift);
        Assert.Contains("fmt(L.MovedToWeek || '{0}', isoOf(s.toWeek))", shift);
        Assert.Contains("reason: reasonText(u.reason)", Between(slips, "(p.unscheduled || [])", "}));"));
        Assert.Contains("(s.overflowProducts || []).forEach(o =>", slips);
        Assert.Contains("(p.pinOverflow || []).filter(o => mondayYmd(o.fromDate) === ws)", slips);

        var reasons = Between(js, "const REASON_KEYS = {", "};");
        foreach (var code in new[] { "capacity_full", "holiday", "half_day", "consent_blocked", "period_exhausted", "pin_overflow", "max_promo" })
        {
            Assert.Contains(code + ":", reasons);
        }

        var tr = Resx("tr");
        Assert.Equal("hafta dolu", tr["ReasonCapacityFull"]);
        Assert.Equal("izin engelli", tr["ReasonConsentBlocked"]);
        Assert.Equal("Kaydırılan / sığmayan hedefler ve ürünler ({0})", tr["SlipTitleCount"]);
    }

    // ── 4 · the doctor panel's period view ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_period_view_shows_target_done_remaining_the_next_visits_products_and_a_history_labelled_by_week_state()
    {
        var js = Script("doctor-panel.js");
        Assert.Contains("base + '/sessions/' + encodeURIComponent(sessionId) + '/targets'", js);
        Assert.Contains("status.requiredVisitCount", js);
        Assert.Contains("status.done != null ? status.done : '—'", js);
        Assert.Contains("status.remaining != null ? status.remaining : '—'", js);
        Assert.Contains("(status.segmentBadges || [])", js);
        // next visit: products (+ their sources) and time from the preview's content; change → the Products tab
        Assert.Contains("(p.content || []).find(c => c.contactId === current.contactId)", js);
        Assert.Contains("L[SOURCE_LABEL[x.source]]", js);
        Assert.Contains("content.durationMinutes", js);
        Assert.Contains("L.NoProductsNoTime", js);
        Assert.Contains("page.emit('request:pick-products'", js);
        Assert.Contains("page.on('request:pick-products'", Script("targets.js"));
        // history label by the week's state
        var state = Between(js, "const historyState = (slot, firstDraft) => {", "\n    };");
        Assert.Contains("return 'presented';", state);
        Assert.Contains("if (slot.isFixed) return 'planned';", state);
        Assert.Contains("'planned' : 'projected'", state);
        Assert.Contains("const HISTORY_LABEL = { presented: 'HistoryPresented', planned: 'HistoryPlanned', projected: 'HistoryProjected' };", js);
        // "Next content" waits for SB-3c: not shown
        Assert.DoesNotContain("NextContent", js);
        Assert.DoesNotContain("NextContent", View("_DoctorPanel.cshtml"));

        // every reader gets the panel; the Products tab is a writer's only
        var details = View("Details.cshtml");
        var panel = details.IndexOf("_DoctorPanel.cshtml", StringComparison.Ordinal);
        var guard = details.LastIndexOf("@if (Model.CanGenerate)", panel, StringComparison.Ordinal);
        var guardEnd = details.IndexOf("}", guard, StringComparison.Ordinal);
        Assert.True(guardEnd < panel, "the doctor panel must not sit behind the generate flag");
        Assert.Contains("if (!canGenerate) el('vp-dp-tab-products-item')?.classList.add('d-none');", js);
        Assert.Equal(("Sunuldu", "Planlandı", "Öngörülen"), (Resx("tr")["HistoryPresented"], Resx("tr")["HistoryPlanned"], Resx("tr")["HistoryProjected"]));
    }

    // ── 5 · actions follow the week's status (draft / approved / past / legacy) ──────────────────────────────────

    [Fact]
    public void The_weeks_actions_follow_the_weeks_status_through_the_page_rule()
    {
        var js = Script("weeks.js");
        var actions = Between(js, "const weekActions = w => {", "\n    };");
        Assert.Contains("const offered = page.actionsFor(w.status, legacy);", actions);
        Assert.Contains("if (offered.indexOf('approveWeek') > -1 && canApply)", actions);
        Assert.Contains("if (offered.indexOf('reopenWeek') > -1 && canApply)", actions);
        Assert.Contains("!legacy && canGenerate && MOVABLE_WEEK_STATUSES.indexOf(w.status) > -1", actions);
        // the same functions as the header
        Assert.Contains("if (k === 'approve') page.request('approve-week');", js);
        Assert.Contains("else if (k === 'reopen') page.request('reopen-week');", js);
        Assert.Contains("page.on('request:reopen-week', () => openReopen());", Script("header.js"));
        // the rule itself (4B): approved → reopen / next; past + legacy → nothing
        var page = Script("page.js");
        Assert.Contains("approved: Object.freeze(['reopenWeek', 'nextWeek']),", page);
        Assert.Contains("legacy: Object.freeze([])", page);
        // the header's "Build this week" only for an empty week
        Assert.Contains("if (key === 'generateWeek') show = !legacy && status === 'empty';", Script("header.js"));
    }

    // ── 6 · the new texts in seven languages, on the bridge ─────────────────────────────────────────────────────

    [Fact]
    public void Every_new_text_is_in_seven_languages_and_every_key_the_new_scripts_read_is_bridged()
    {
        var keys = new[]
        {
            "StatusTextDraft", "StatusTextApproved", "StatusTextPast", "StatusTextEmpty", "GenerateWeek", "WeekCapacityNote",
            "PeriodCapacityNote", "CapacityMissingWeek", "CapacityMissingPeriod", "PeriodWeeksTitle", "PeriodWeeksSummary",
            "AutoDraftRulesTitle", "TodayLabel", "LegacyWeekLabel", "SlipTitleCount", "ReasonCapacityFull", "ReasonPinOverflow",
            "DayByDay", "DayCapacityFormat", "IdleTime", "NoVisitThisDay", "MoreDoctors", "MoveToDay", "MoveAllQuestion",
            "MoveAll", "MoveOnlyThis", "MoveLockedHint", "PinOverflowMessage", "ProductVisitsTitle", "MixedOrderNote",
            "WeekDoctorsTitle", "WeekHistoryTitle", "HistoryOtherUser", "FrequencyTarget", "NextVisitProducts",
            "ProductHistoryTitle", "HistoryPresented", "StateProjected"
        };
        foreach (var culture in Cultures)
        {
            var resx = Resx(culture);
            Assert.All(keys, k => Assert.True(resx.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v) && v != k, $"{culture}:{k}"));
        }

        var tr = Resx("tr");
        Assert.Equal("Bu güne ziyaret düşmüyor.", tr["NoVisitThisDay"]);
        Assert.Equal("Bu kurumdaki diğer ziyaretleri de ({0} doktor, {1} eczane) taşıyayım mı?", tr["MoveAllQuestion"]);
        Assert.Equal("Değiştirmek için haftayı yeniden açın.", tr["MoveLockedHint"]);
        Assert.Equal("Bu hafta için henüz taslak yok.", tr["StatusTextEmpty"]);

        var bridge = View("_IndexL10n.cshtml");
        var scripts = Script("weeks.js") + Script("doctor-panel.js") + Script("header.js");
        var read = Regex.Matches(scripts, @"(?<![\w.])L\.([A-Z][A-Za-z0-9_]*)").Select(m => m.Groups[1].Value)
            .Concat(Regex.Matches(scripts, @"'((?:Reason|State|History|StatusText)[A-Za-z]+)'").Select(m => m.Groups[1].Value))
            .Distinct().ToList();
        Assert.True(read.Count > 60);
        Assert.All(read, k => Assert.Matches($@"(?<![A-Za-z0-9_]){Regex.Escape(k)}\s*=", bridge));
    }

    // ── Ek · detail header mockup alignment ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_header_has_two_capacity_cards_with_bars_and_notes_and_says_when_there_is_no_capacity()
    {
        var view = View("Details.cshtml");
        foreach (var id in new[] { "vp-cap-week", "vp-cap-week-planned", "vp-cap-week-bar", "vp-cap-week-note", "vp-cap-period", "vp-cap-period-planned", "vp-cap-period-bar", "vp-cap-period-note" })
        {
            Assert.Contains($"id=\"{id}\"", view);
        }

        var capacity = Between(Script("header.js"), "const renderCapacity = () => {", "\n    };");
        Assert.Contains("const noCapacity = !!(pc && pc.budgetSource === 'default_hours');", capacity);
        Assert.Contains("L.CapacityMissingWeek", capacity);
        Assert.Contains("L.CapacityMissingPeriod", capacity);
        Assert.Contains("fmt(L.WeekCapacityNote || '{0}', weekPct)", capacity);
        Assert.Contains("wc.holidays > 0 || wc.halfDays > 0", capacity);
        Assert.Equal("Dönemin kapasitesi tanımlı değil; doluluk hesaplanamıyor.", Resx("tr")["CapacityMissingWeek"]);
    }

    [Fact]
    public void The_actions_card_speaks_the_weeks_status_and_has_no_week_dropdown()
    {
        var view = View("Details.cshtml");
        Assert.DoesNotContain("vp-hdr-week", view);
        Assert.Contains("id=\"vp-hdr-status-text\"", view);
        Assert.Matches(@"<li class=""[^""]*d-none""[^>]*data-vp-action-item>\s*<button[^>]*data-vp-action=""generateWeek""", view);
        var header = Script("header.js");
        Assert.DoesNotContain("vp-hdr-week", header);
        Assert.Contains("const STATUS_TEXT = { draft: 'StatusTextDraft', approved: 'StatusTextApproved', past: 'StatusTextPast', empty: 'StatusTextEmpty' };", header);
        Assert.Contains("setText('vp-hdr-status-text'", header);
        Assert.Contains("else if (key === 'generateWeek') page.request('reload-plan');", header);
    }

    [Fact]
    public void A_week_change_refreshes_the_summary_the_actions_and_the_capacity_together_from_the_page_week()
    {
        var header = Script("header.js");
        Assert.Contains("const refresh = () => { renderSummary(); renderActions(); renderCapacity(); };", header);
        Assert.Contains("page.on('week-change', () => refresh());", header);
        var summary = Between(header, "const renderSummary = () => {", "\n    };");
        Assert.Contains("page.week(page.state.weekStart)", summary);
        Assert.Contains("setText('vp-d-week', w ? weekTitle(w) : '—');", summary);
        Assert.Contains("setText('vp-route-range', w ? weekRange(w) + ' · ' + weekYear(w) : '—');", summary);
        Assert.Contains("badge.className = 'badge bg-label-'", summary);
        // the route no longer writes the summary with its own week (the bug: 43 / empty in the summary, 44 in actions)
        var details = Script("details.js");
        Assert.Contains("if (!page) setWeekLabel(week);", details);
        Assert.Contains("const range = page ? null : el('vp-route-range');", details);
    }

    // ── Ek · 4B E4 follow-ups ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_4B_E4_follow_ups_hold()
    {
        // 1 · "Go to plan" up front = the server's plan: the OLDEST not-archived plan of the rep + period
        var newPlan = Script("new-plan.js");
        var existing = Between(newPlan, "const existingPlanFor = periodId => plans", "[0];");
        Assert.Contains("String(s.status || '').toLowerCase() !== 'archived'", existing);
        Assert.Contains(".sort((a, b) => String(a.createdAt || '').localeCompare(String(b.createdAt || '')))", existing);
        // 2 · the rep by name, not an e-mail
        var rep = Between(Script("header.js"), "const renderRep = () => {", "\n    };");
        Assert.Contains("(!name || looksLikeMail(name)) && me && me.displayName", rep);
        // 3 · one lock band (the header's); the route adds none when the page skeleton is there
        Assert.Contains("if (locked && !band && !page) {", Script("details.js"));
        // 4 · the country is read-only from the open periods' countries; a choice only when several
        var country = Between(newPlan, "const renderCountry = () => {", "\n    };");
        Assert.Contains("const list = periodCountries();", country);
        Assert.Contains("if (list.length > 1) {", country);
        Assert.DoesNotContain("countries.map(c =>", country);
        Assert.Contains("p.scopeType === 'country' && p.country", newPlan);
        // 5 · no "Delete empty drafts" when no plan is an empty draft (an approved week makes a plan non-empty)
        Assert.Contains("!(Number(s.approvedWeekCount) > 0)", Between(Script("index.js"), "const isEmptyDraft = s =>", "\n"));
        Assert.Contains("box.classList.toggle('d-none', empty.length === 0);", Script("index.js"));
        // 6 · history "who" is a name: never an id or an e-mail
        var who = Between(Script("weeks.js"), "const whoName = by => {", "\n    };");
        Assert.Contains("const isName = v => !!v && !GUID.test(v) && !/@/.test(v);", who);
        Assert.Contains("GUID.test(String(by)) ? (L.HistoryOtherUser || '') : String(by)", who);
    }

    // ── Ek · move a visit to another day (4E day pins) ─────────────────────────────────────────────────────────

    [Fact]
    public void Dropping_a_visit_sends_the_weeks_day_pins_through_the_session_update()
    {
        var js = Script("weeks.js");
        var save = Between(js, "const savePins = (ws, pins) =>", "\n    });");
        Assert.Contains("base + '/sessions/' + encodeURIComponent(sessionId)", save);
        Assert.Contains("method: 'PUT'", save);
        Assert.Contains("JSON.stringify({ dayPins: { weekStart: ws, pins: pins }, expectedVersion: session().version })", save);
        Assert.Contains("page.request('reload-plan')", save);

        var move = Between(js, "const pinsAfterMove = (current, slot, scope, date, members) => {", "\n    };");
        Assert.Contains("const touched = scope === 'institution' ? members : [slot];", move);
        Assert.Contains("kept.push({ targetType: slot.targetType, targetId: slot.targetId, contactId: slot.contactId || null, date: date, scope: scope });", move);
        Assert.Contains("p.scope === 'institution' && members.some(m => sameTarget(p, m))", Between(js, "const pinsAfterUnpin = ", ";\n"));
        // a doctor dropped alone asks "all of the institution or only this one?" (N doctors, M pharmacies from groupKey)
        var drop = Between(js, "const dropOn = (slot, scope, date) => {", "\n    };");
        Assert.Contains("if (scope === 'visit' && members.length > 1) { askMove(slot, 'visit', date); return; }", drop);
        Assert.Contains("fmt(L.MoveAllQuestion || '{0} {1}', others.filter(m => m.contactId).length, others.filter(m => !m.contactId).length)", js);
        Assert.Contains("const groupOf = s => s.groupKey ||", js);
        // the pin marks + unpin + the pin overflow line
        Assert.Contains("s.autoPinned", js);
        Assert.Contains("js-wk-unpin", js);
        Assert.Contains("fmt(L.PinOverflowMessage || '{0} {1}', byTarget[k], dayLabel(k))", js);
    }

    [Fact]
    public void Moving_is_closed_on_an_approved_or_past_week_and_never_onto_a_holiday_or_weekend()
    {
        var js = Script("weeks.js");
        Assert.Contains("const MOVABLE_WEEK_STATUSES = ['draft', 'empty'];", js);
        Assert.Contains("const DROPPABLE_DAY_KINDS = ['working', 'half'];", js);
        Assert.Contains("const canMove = w => !!w && canGenerate && !readOnly && !page.isLegacy() && MOVABLE_WEEK_STATUSES.indexOf(w.status) > -1;", js);
        var drop = Between(js, "const dropOn = (slot, scope, date) => {", "\n    };");
        Assert.Contains("if (!canMove(w)) return;", drop);
        Assert.Contains("if (!day || date === slot.plannedDate) return;", drop);
        Assert.Contains("DROPPABLE_DAY_KINDS.indexOf(d.kind) > -1 && d.date >= todayYmd()", js);
        Assert.Contains("if (!src || !canMove(page.week(page.state.weekStart))) { e.preventDefault(); return; }", js);
        Assert.Contains("const droppable = movable && DROPPABLE_DAY_KINDS.indexOf(day.kind) > -1 && day.date >= todayYmd();", js);
        Assert.Contains("L.MoveLockedHint", js);
        // the keyboard way exists ("Move to day…") and the dialog is a writer's only
        Assert.Contains("js-wk-move", js);
        var view = View("Details.cshtml");
        var modal = view.IndexOf("id=\"vp-move-modal\"", StringComparison.Ordinal);
        Assert.True(modal > 0 && view.LastIndexOf("@if (Model.CanGenerate)", modal, StringComparison.Ordinal) > modal - 400);
    }

    // ── Ek · the product picker reads the plan's visit model first ─────────────────────────────────────────────

    [Fact]
    public void The_product_picker_uses_the_visit_model_and_reads_the_capacity_proxy_only_without_one()
    {
        var js = Script("targets.js");
        var load = Between(js, "const loadCapacity = periodId => {", "\n    };");
        var vm = load.IndexOf("const vm = visitModelOf();", StringComparison.Ordinal);
        var proxy = load.IndexOf("request(capacityBase", StringComparison.Ordinal);
        Assert.True(vm >= 0 && proxy > vm, "the visit model is consulted before any capacity request");
        Assert.Contains("return Promise.resolve(capacity);\n        }\n        if (!periodId", load);
        Assert.Contains("vm.source === 'none' ? null", load);
        Assert.Contains("page.state.preview && page.state.preview.visitModel", js);
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

    private static string Script(string file) =>
        File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "VisitPlanning", file)).Replace("\r\n", "\n");

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

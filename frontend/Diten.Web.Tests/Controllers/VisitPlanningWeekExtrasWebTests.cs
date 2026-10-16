using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VP-4L (4L-WEB) — the weekly default frequency label, the week-aware Targets tab and the per-week extra visits, on the
/// REAL scripts, views and resources (Acceptance 1–5). The server side (frequencyDefault, isExtra, extraTargets,
/// weekExtras) is 4L-BE.
/// </summary>
public sealed class VisitPlanningWeekExtrasWebTests
{
    private static readonly string[] Cultures = ["", "en", "tr", "fr", "es", "zh", "ar", "ru"];

    // ── 1 · "haftada 1 (varsayılan)" from frequencyDefault; "dönemde 1 (varsayılan)" is gone ───────────────────

    [Fact]
    public void The_unknown_frequency_reads_weekly_default_from_frequency_default_and_the_old_text_is_gone()
    {
        Assert.Contains("st.frequencyDefault === 'weekly' ? (L.FrequencyDefaultWeekly || '') : (L.FrequencyNone || '—')", Script("details.js"));
        var weeks = Between(Script("weeks.js"), "const frequencyLine = (s, status) => {", "\n    };");
        Assert.Contains("(status && status.frequencyDefault) === 'weekly' || s.frequencyDefault === 'weekly'", weeks);
        Assert.Contains("(weeklyDefault ? (L.FrequencyDefaultWeekly || '') : (L.FrequencyNone || ''))", weeks);
        var panel = Script("doctor-panel.js");
        Assert.Contains("const weeklyDefault = status.frequencyDefault === 'weekly' || slots.some(s => s.frequencyDefault === 'weekly');", panel);
        Assert.Contains("(weeklyDefault ? (L.FrequencyDefaultWeekly || '') : (L.FrequencyNone || ''))", panel);
        // the old key / text is nowhere: scripts, views, bridge, resources
        foreach (var file in Directory.GetFiles(ScriptDir(), "*.js"))
        {
            var js = File.ReadAllText(file);
            Assert.DoesNotContain("FrequencyDefaultOne", js);
            Assert.DoesNotContain("dönemde 1", js);
        }
        Assert.DoesNotContain("FrequencyDefaultOne", View("_IndexL10n.cshtml"));
        foreach (var culture in Cultures)
        {
            var resx = Resx(culture);
            Assert.False(resx.ContainsKey("FrequencyDefaultOne"), culture);
            Assert.DoesNotContain(resx.Values, v => v.Contains("dönemde 1", StringComparison.Ordinal));
        }
        Assert.Equal("haftada 1 (varsayılan)", Resx("tr")["FrequencyDefaultWeekly"]);
        Assert.Contains("haftada 1", Resx("tr")["RuleUnknownFrequency"]);
    }

    // ── 2 · the head line: this week's visits apart from the period's doctors ────────────────────────────────

    [Fact]
    public void The_targets_head_line_counts_this_weeks_visits_and_the_periods_doctors_apart()
    {
        var js = Script("details.js");
        var sub = Between(js, "const refreshSubtitle = () => {", "\n    };");
        Assert.Contains("fmt(L.TargetsWeekSubtitle || '{0} · {1} · {2}'", sub);
        Assert.Contains("lastPreview ? weekSlots(page.state.weekStart).length : '—'", sub);
        Assert.Contains("Object.keys(selectedContacts).length", sub);
        Assert.Contains("const weekSlots = ws => ((lastPreview && lastPreview.scheduled) || []).filter(s => (s.weekStart || mondayKey(s.plannedDate)) === ws);", js);
        Assert.Contains("['preview', 'week-change', 'session'].forEach(evt => page.on(evt, () => { drawDoctors(); refreshSubtitle(); }))", js);
        Assert.Equal("{0} · {1} ziyaret bu hafta · {2} doktor dönem hedefinde", Resx("tr")["TargetsWeekSubtitle"]);
    }

    // ── 3 · "Visit this week too" / "Remove the extra visit" / "Add to this week (N)" → weekExtras ─────────────

    [Fact]
    public void Extra_visits_are_offered_on_a_draft_or_empty_week_only_and_send_the_week_extras_body()
    {
        var js = Script("details.js");
        Assert.Contains("const EXTRA_WEEK_STATUSES = ['draft', 'empty'];", js);
        var can = Between(js, "const canEditExtras = () =>", ";\n");
        Assert.Contains("canGenerate && !readOnly", can);
        Assert.Contains("!page.isLegacy() && supportsExtras()", can);
        Assert.Contains("EXTRA_WEEK_STATUSES.indexOf(page.weekStatus(selectedWeek())) > -1", can);
        // without the server's fields (before 4L-BE) nothing is offered
        var supports = Between(js, "const supportsExtras = () =>", ";\n");
        Assert.Contains("Array.isArray(w.extraTargets)", supports);
        Assert.Contains("typeof s.isExtra === 'boolean'", supports);
        // the Status column: planned / extra / none + the action
        var cell = Between(js, "const weekCell = row => {", "\n    };");
        Assert.Contains("if (canEditExtras() && !row.blocked) {", cell);
        Assert.Contains("if (state === 'none') action = '<button type=\"button\" class=\"btn btn-sm btn-text-primary px-1 py-0 js-extra-add\"", cell);
        Assert.Contains("else if (state === 'extra') action = '<button type=\"button\" class=\"btn btn-sm btn-text-secondary px-1 py-0 js-extra-remove\"", cell);
        Assert.Contains("const WEEK_STATE = { planned: ['bg-label-success', 'WeekStatePlanned'], extra: ['bg-label-warning', 'WeekStateExtra'], none: ['bg-label-secondary', 'WeekStateNone'] };", js);
        Assert.Contains("const statusCell = row => (weekCell(row) ||", js);
        // the body: the existing session update, weekExtras { weekStart, targets } (the dayPins pattern), the version
        var save = Between(js, "const saveWeekExtras = targets => {", "\n    };");
        Assert.Contains("if (!canEditExtras()) return Promise.resolve(false);", save);
        Assert.Contains("api('/sessions/' + encodeURIComponent(sessionId), {", save);
        Assert.Contains("method: 'PUT'", save);
        Assert.Contains("body: JSON.stringify({ weekExtras: { weekStart: selectedWeek(), targets: targets }, expectedVersion: sessionData && sessionData.version })", save);
        Assert.Contains("page.request('reload-plan');", save);
        Assert.Contains("return { targetType: 'contact', targetId: cid, contactId: cid, accountId: saved.accountId || null };", js);
        // add keeps the week's other extras; remove sends the rest ([] clears)
        Assert.Contains("return fresh.length ? saveWeekExtras(current.concat(fresh)) : Promise.resolve(false);", js);
        Assert.Contains("const removeWeekExtra = cid => saveWeekExtras(weekExtraTargets(selectedWeek()).filter(t => (t.contactId || t.targetId) !== cid));", js);
        // the bulk "Add to this week (N)": the ticked plan doctors without a visit this week, never a consent-blocked one
        var bulk = Between(js, "\n    paintExtraBulk = () => {", "\n    };");
        Assert.Contains("btn.classList.toggle('d-none', !on);", bulk);
        Assert.Contains("fmt(L.ExtraVisitBulk || '{0}', n)", bulk);
        Assert.Contains("return !(c && c.blocked) && weekStateOf(sc.contactId) === 'none';", js);
        var view = View("Details.cshtml");
        var btn = view.IndexOf("id=\"vp-week-extra-bulk\"", StringComparison.Ordinal);
        Assert.True(btn > 0 && view.LastIndexOf("@if (Model.CanGenerate)", btn, StringComparison.Ordinal) > btn - 900);
        // the errors in words
        Assert.Contains("const EXTRA_ERROR = { extra_target_not_in_plan: 'ExtraNotInPlan', extra_no_room: 'ExtraNoRoom' };", js);
        Assert.Contains("if (r.status === 409 || /not_editable|approved|locked|past/.test(code)) return L.ExtraWeekLocked || code;", js);
        // the empty week's second way
        Assert.Contains("esc(L.EmptyWeekAddFromTargets || '')", Script("weeks.js"));
    }

    // CT (4L-WEB K13) — before 4L-BE answers (no extraTargets / isExtra in the preview) the extra-visit actions stay
    // hidden: an older server must never get a weekExtras body it does not know.
    [Fact]
    public void Without_the_servers_extra_fields_no_extra_visit_action_is_offered()
    {
        var js = Script("details.js");
        var supports = Between(js, "const supportsExtras = () =>", ";\n");
        Assert.StartsWith("const supportsExtras = () => !!lastPreview && (", supports);
        Assert.Contains("Array.isArray(w.extraTargets)", supports);
        Assert.Contains("typeof s.isExtra === 'boolean'", supports);
        Assert.DoesNotContain("true ||", supports);
        Assert.Contains("!page.isLegacy() && supportsExtras()", Between(js, "const canEditExtras = () =>", ";\n"));
    }

    // ── 4 · the "extra" badge: Weeks day rows, Route stops, the doctor panel's history ───────────────────────

    [Fact]
    public void An_extra_visit_carries_its_badge_on_the_weeks_route_and_doctor_panel()
    {
        var weeks = Script("weeks.js");
        Assert.Contains("const extraMark = s => (s && s.isExtra ?", weeks);
        Assert.Contains("'<span class=\"flex-grow-1\" style=\"min-width:0\">' + nameHtml + '</span>' + extraMark(s) +", weeks);
        Assert.Contains("extra_no_room: 'ReasonExtraNoRoom', week_full_skipped: 'ReasonWeekFullSkipped'", weeks);
        var details = Script("details.js");
        Assert.Contains("isExtra: r.isExtra === true", details);
        Assert.Contains("((b.visits || []).some(v => v.isExtra) ? extraBadge() : '')", details);
        Assert.Contains("(v.isExtra ? extraBadge() : '')", details);
        Assert.Contains("(slot.isExtra ? '<span class=\"badge bg-label-warning vp-extra-badge\">' + esc(L.WeekStateExtra || '')", Script("doctor-panel.js"));
        Assert.Equal(("ek", "Ek ziyaret"), (Resx("tr")["ExtraBadgeShort"], Resx("tr")["WeekStateExtra"]));
    }

    // ── 5 · the new texts in seven languages (+ neutral) and the bridge ────────────────────────────────────────

    [Fact]
    public void The_new_keys_are_in_seven_languages_and_the_bridge()
    {
        var keys = new[]
        {
            "FrequencyDefaultWeekly", "TargetsWeekSubtitle", "WeekStatePlanned", "WeekStateExtra", "WeekStateNone", "ExtraVisitAdd",
            "ExtraVisitRemove", "ExtraVisitBulk", "ExtraVisitsSaved", "ExtraNotInPlan", "ExtraWeekLocked", "ExtraNoRoom",
            "ExtraBadgeShort", "ReasonExtraNoRoom", "ReasonWeekFullSkipped", "EmptyWeekAddFromTargets"
        };
        var withArgs = new HashSet<string> { "TargetsWeekSubtitle", "ExtraVisitBulk" };
        var bridge = View("_IndexL10n.cshtml");
        var en = Resx("en");
        var tr = Resx("tr");
        foreach (var key in keys)
        {
            Assert.Contains($"{key} = Localizer[\"{key}\"].Value", bridge);
            Assert.NotEqual(en[key], tr[key]);
        }
        foreach (var culture in Cultures)
        {
            var resx = Resx(culture);
            foreach (var key in keys)
            {
                Assert.True(resx.TryGetValue(key, out var value) && value.Length > 0, $"{key} missing in '{culture}'");
                if (!withArgs.Contains(key)) Assert.DoesNotContain("{0}", value);
            }
        }
        Assert.Equal(("Bu hafta planlı", "Bu hafta yok", "Bu hafta da ziyaret et", "Ek ziyareti kaldır", "Bu haftaya ekle ({0})"),
            (tr["WeekStatePlanned"], tr["WeekStateNone"], tr["ExtraVisitAdd"], tr["ExtraVisitRemove"], tr["ExtraVisitBulk"]));
        Assert.Equal("Önce doktoru plana ekleyin (Hedefleri kaydet).", tr["ExtraNotInPlan"]);
        Assert.Equal("Hedefler'den doktorları bu haftaya ekleyebilirsiniz.", tr["EmptyWeekAddFromTargets"]);
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

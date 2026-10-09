using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using Diten.Web.Controllers.CRM;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-E2E-FIX-1 — on the REAL scripts, view, resx and proxy: the visit execution page shows the planned content, the
/// presented stage is a pick that carries the planned journey / stage ids, refusal codes reach the rep as localized
/// messages (and a ProblemDetails refusal is turned into the envelope), a not-yet-due visit offers no completion, and the
/// Visit Planning edit / apply flows land on the right page in the right state.
/// </summary>
public sealed class VisitExecutionFix1WebTests
{
    private static readonly string[] Langs = { "en", "tr", "fr", "es", "zh", "ar", "ru" };

    // ── 3 · the report body carries the planned journey / stage, or nothing when "not presented" ─────────────────

    [Fact]
    public void The_report_body_sends_the_picked_stage_with_journey_ids_and_not_presented_sends_none()
    {
        var js = VeScript();

        Assert.Contains("contentActuals: stageChoice()", js);
        var choice = Between(js, "function stageChoice()", "\n    }");
        // planned journey + the picked stage's id / index / code; matched only when it IS the planned stage
        Assert.Contains("journeyId: reportPlan.journeyId", choice);
        Assert.Contains("stageId: option.value", choice);
        Assert.Contains("stageIndex: index === '' ? null : parseInt(index, 10)", choice);
        Assert.Contains("stageCode: option.getAttribute('data-code') || null", choice);
        Assert.Contains("matchedPlan: option.value === reportPlan.stageId", choice);
        // "not presented" (or no planned journey) → every stage field empty, matchedPlan false
        Assert.Contains("option.value === NOT_PRESENTED", choice);
        Assert.Contains("var empty = { journeyId: null, stageId: null, stageIndex: null, stageCode: null, matchedPlan: false", choice);

        // the default is the planned stage; the stage index is the position in the ordered stage list
        Assert.Contains("select.value = plan.stageId || NOT_PRESENTED;", js);
        Assert.Contains("stageIndex: i,", js);
        Assert.Contains("'/journeys/' + encodeURIComponent(plan.journeyId) + '/stages'", js);

        // the free-text stage code / index inputs and the matched-plan checkbox are gone
        var view = VeView();
        Assert.Contains("id=\"ve-actual-stage\"", view);
        Assert.DoesNotContain("ve-actual-stage-code", view);
        Assert.DoesNotContain("ve-actual-stage-index", view);
        Assert.DoesNotContain("ve-matched-plan", view);
        Assert.DoesNotContain("ve-actual-stage-code", js);
    }

    [Fact]
    public void The_stage_proxy_reads_the_crm_journey_stages_with_the_calendar_read_permission()
    {
        var method = typeof(CrmVisitExecutionController).GetMethod(nameof(CrmVisitExecutionController.JourneyStages))!;
        Assert.Equal("api/journeys/{journeyId:guid}/stages", method.GetCustomAttribute<HttpGetAttribute>()!.Template);

        var source = File.ReadAllText(Path.Combine(WebRoot(), "Controllers", "CRM", "CrmVisitExecutionController.cs"));
        var proxy = Between(source, "public Task<IActionResult> JourneyStages(", ";");
        Assert.Contains("/api/crm/knowledge/content-engagement-journeys/{journeyId}/stages", proxy);
        Assert.Contains("ReadPermission, ReadFallback", proxy);
    }

    // ── 1 (Web) · planned content on the card and in the report ──────────────────────────────────────────────────

    [Fact]
    public void The_card_shows_product_chip_and_stage_name_and_the_report_lists_the_planned_steps()
    {
        var js = VeScript();
        Assert.DoesNotContain("'#' + it.plannedStageIndex", js);
        var chips = Between(js, "function contentChips(it)", "\n    }");
        Assert.Contains("c.productCode", chips);
        Assert.Contains("stageText(c)", chips);
        var planned = Between(js, "function renderPlannedContent(item)", "\n    }");
        Assert.Contains("s.title", planned);
        Assert.Contains("L.noPlannedContent", planned);
        Assert.Contains("itemsById[it.plannedVisitId] = it", js);
        Assert.Contains("var item = itemsById[plannedVisitId] || null;", js);
    }

    // ── 4 · refusal codes → localized messages; ProblemDetails → envelope ────────────────────────────────────────

    [Fact]
    public void Error_reader_takes_the_root_errors_and_maps_codes_to_localized_messages()
    {
        var js = VeScript();
        var reader = Between(js, "function errorOf(r)", "\n    }");
        // root errors — never `body.data || body` first (data is the empty-Guid string on a failed Response<Guid>)
        Assert.Contains("var b = r.body;", reader);
        Assert.DoesNotContain("r.body.data", reader);
        Assert.Contains("Array.isArray(b.errors)", reader);
        Assert.Contains("map[errors[i]]", reader);

        // client-side required check before the round trip
        Assert.Contains("if (!validateReport()) { return; }", js);

        var view = VeView();
        Assert.Contains("visit_report_outcome_code_required: @Json.Serialize(Localizer[\"ErrOutcomeCodeRequired\"].Value)", view);
        Assert.Contains("visit_not_yet_due: @Json.Serialize(Localizer[\"ErrNotYetDue\"].Value)", view);
        Assert.Equal("Sonuç kodu zorunludur.", Resx("VisitExecution", "VisitExecutionIndex", "tr")["ErrOutcomeCodeRequired"]);

        // every visit_report_* code the CRM publishes is mapped (the list mirrors VisitReportErrorCodes.All)
        foreach (var code in new[]
                 {
                     "unsupported_vocabulary_value", "visit_report_planned_visit_required", "visit_report_planned_visit_not_found",
                     "visit_report_outcome_required", "visit_report_reason_code_required", "visit_report_reschedule_date_invalid",
                     "visit_report_resource_required", "visit_report_outcome_code_required", "visit_report_sample_invalid",
                     "visit_report_content_actuals_invalid", "visit_report_free_text_too_long", "visit_report_not_found",
                     "visit_report_already_exists", "visit_report_not_completed", "visit_report_edit_window_closed",
                     "visit_report_not_finalised", "visit_report_amendment_reason_required", "visit_report_invalid_transition",
                     "visit_report_concurrency_conflict", "visit_not_yet_due"
                 })
        {
            Assert.Contains(code + ": @Json.Serialize(", view);
        }

        // the wrong "reference data" note and the always-empty datalist are gone
        Assert.DoesNotContain("ReferenceDataHint", view);
        Assert.DoesNotContain("datalist", view);
        Assert.DoesNotContain("outcomeCodes", js);
    }

    [Fact]
    public void A_problem_details_refusal_is_turned_into_the_envelope_and_other_bodies_pass_through()
    {
        const string problem = "{\"type\":\"https://tools.ietf.org/html/rfc9110#section-15.5.1\",\"title\":\"One or more validation errors occurred.\","
                               + "\"status\":400,\"errors\":{\"$.plannedVisitId\":[\"The JSON value could not be converted to System.Guid.\"]}}";

        var envelope = CrmVisitExecutionController.ProblemDetailsToEnvelope("application/problem+json", problem, 400);

        using var doc = JsonDocument.Parse(envelope!);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("data").ValueKind);
        Assert.Equal(400, doc.RootElement.GetProperty("statusCode").GetInt32());
        Assert.False(doc.RootElement.GetProperty("isSuccessful").GetBoolean());
        Assert.Equal(
            new[] { "One or more validation errors occurred.", "The JSON value could not be converted to System.Guid." },
            doc.RootElement.GetProperty("errors").EnumerateArray().Select(e => e.GetString()));

        Assert.Null(CrmVisitExecutionController.ProblemDetailsToEnvelope(
            "application/json", "{\"data\":\"00000000-0000-0000-0000-000000000000\",\"errors\":[\"m\",\"visit_not_yet_due\"]}", 409));
    }

    // ── 5 (Web) · a not-yet-due card offers no completion / report; rescheduling stays ──────────────────────────

    [Fact]
    public void A_not_yet_due_card_disables_completed_missed_and_report_but_not_rescheduled()
    {
        var js = VeScript();
        Assert.Contains("return !!(it && it.plannedDate && it.plannedDate > iso(new Date()));", js);
        var cell = Between(js, "function renderCell(it)", "\n    }");
        Assert.Contains("btn(it, 'completed', 'btn-outline-success', L.markCompleted, notDue)", cell);
        Assert.Contains("btn(it, 'missed', 'btn-outline-danger', L.markMissed, notDue)", cell);
        Assert.Contains("btn(it, 'rescheduled', 'btn-outline-warning', L.markRescheduled, false)", cell);
        Assert.Contains("disabledAttr(notDue)", cell);
        Assert.Contains("L.notYetDue", cell);
        Assert.Contains("el('ve-submit-report').disabled = !canRecord || isNotYetDue(item);", js);
    }

    // ── E9-B6 · completed is confirmed, outcome label localized, title = menu ───────────────────────────────────

    [Fact]
    public void Completed_is_confirmed_and_the_outcome_shows_its_localized_label()
    {
        var js = VeScript();
        var record = Between(js, "function recordOutcome(plannedVisitId, outcome)", "\n    }");
        Assert.Contains("window.showConfirm(L.confirmCompleted, go,", record);
        Assert.Contains("esc(outcomeLabel(it.executionOutcome))", js);
        Assert.DoesNotContain("esc(it.executionOutcome)", js);

        var view = VeView();
        foreach (var outcome in new[] { "completed", "missed", "rescheduled" })
        {
            Assert.Contains(outcome + ": @Json.Serialize(Localizer[\"Outcome", view);
        }
    }

    [Fact]
    public void The_page_title_matches_the_menu_label_in_every_language()
    {
        foreach (var lang in Langs)
        {
            var title = Resx("VisitExecution", "VisitExecutionIndex", lang)["PageTitle"];
            var menu = XDocument.Load(Path.Combine(WebRoot(), "Resources", $"SharedResource.{lang}.resx")).Root!
                .Elements("data").Single(d => (string)d.Attribute("name")! == "Nav.Page.VISITEXECUTION")
                .Element("value")!.Value.Trim();
            Assert.Equal(menu, title);
        }

        Assert.Equal("Ziyaret Yürütme", Resx("VisitExecution", "VisitExecutionIndex", "tr")["PageTitle"]);
    }

    // ── 6 · Visit Planning edit → save returns to its own session ────────────────────────────────────────────────

    [Fact]
    public void Editing_a_plan_redirects_to_its_own_session_not_to_the_update_result()
    {
        var js = VpScript("form.js");
        Assert.Contains("const newId = isEdit ? sessionId : ((r.body && r.body.data) || sessionId);", js);
        Assert.DoesNotContain("const newId = (r.body && r.body.data) || sessionId;", js);
    }

    // ── 7 · apply asks first, then reloads into the read-only page with the toast kept ───────────────────────────

    [Fact]
    public void Apply_is_confirmed_and_success_reloads_into_read_only_keeping_the_toast()
    {
        var js = VpScript("details.js");
        var apply = Between(js, "const apply = () => {", "\n    };");
        Assert.Contains("window.showConfirm(text, go,", apply);
        // WP-VP-3A — the confirm now speaks of THIS week (its own visits), not the whole plan.
        Assert.Contains("L.ApplyWeekConfirm", apply);
        Assert.Contains(".replace('{0}', weekRows.length)", apply);
        Assert.Contains("sessionStorage.setItem(APPLIED_TOAST_KEY, message)", apply);
        Assert.Contains("window.location.reload();", apply);
        // the confirm precedes the POST: the request lives inside `go`
        Assert.True(apply.IndexOf("const go = () =>", StringComparison.Ordinal) < apply.IndexOf("api('/apply'", StringComparison.Ordinal));

        Assert.Contains("showAppliedToast(); // the success message of an apply that reloaded", js);
        var shown = Between(js, "const showAppliedToast = () => {", "\n    };");
        Assert.Contains("sessionStorage.removeItem(APPLIED_TOAST_KEY)", shown);
        Assert.Contains("window.showToast?.(message, 'success')", shown);

        var bridge = File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "VisitPlanning", "_IndexL10n.cshtml"));
        Assert.Contains("ApplyConfirm = Localizer[\"ApplyConfirm\"].Value", bridge);
        Assert.Contains("ApplyConfirmButton = Localizer[\"ApplyConfirmButton\"].Value", bridge);
    }

    // ── 8 · every new key in all 7 languages (+ neutral); TR is real Turkish, not the key ─────────────────────────

    [Theory]
    [InlineData("VisitExecution", "VisitExecutionIndex", new[]
    {
        "PageTitle", "PlannedContent", "NoPlannedContent", "StageFallback", "PlannedStageSuffix", "StageNotPresented",
        "StageMatchesPlan", "StageDiffersFromPlan", "NotYetDueHint", "ConfirmCompleted", "ConfirmCompletedButton",
        "OutcomeCompleted", "OutcomeMissed", "OutcomeRescheduled", "OutcomeCodeHint", "ErrOutcomeCodeRequired", "ErrNotYetDue",
        "ErrPlannedVisitRequired", "ErrPlannedVisitNotFound", "ErrOutcomeRequired", "ErrReasonCodeRequired",
        "ErrRescheduleDateInvalid", "ErrResourceRequired", "ErrSampleInvalid", "ErrContentActualsInvalid", "ErrFreeTextTooLong",
        "ErrReportNotFound", "ErrReportAlreadyExists", "ErrNotCompleted", "ErrEditWindowClosed", "ErrNotFinalised",
        "ErrAmendmentReasonRequired", "ErrInvalidTransition", "ErrConcurrencyConflict", "ErrUnsupportedValue"
    })]
    [InlineData("VisitPlanning", "VisitPlanningIndex", new[] { "ApplyConfirm", "ApplyConfirmButton" })]
    public void New_keys_exist_in_every_language_and_turkish_is_translated(string module, string baseName, string[] keys)
    {
        var neutral = Resx(module, baseName, null);
        var tr = Resx(module, baseName, "tr");
        var en = Resx(module, baseName, "en");
        foreach (var key in keys)
        {
            Assert.True(neutral.ContainsKey(key), $"{baseName}.resx misses {key}");
            foreach (var lang in Langs)
            {
                var values = Resx(module, baseName, lang);
                Assert.True(values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value), $"{baseName}.{lang}.resx misses {key}");
            }

            Assert.NotEqual(key, tr[key]);
            Assert.NotEqual(en[key], tr[key]);
        }

        // Turkish carries its diacritics (no ASCII-folded text).
        Assert.Contains(keys, k => tr[k].IndexOfAny("çğıöşüÇĞİÖŞÜ".ToCharArray()) >= 0);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────────

    private static Dictionary<string, string> Resx(string module, string baseName, string? lang)
    {
        var file = lang is null ? $"{baseName}.resx" : $"{baseName}.{lang}.resx";
        return XDocument.Load(Path.Combine(WebRoot(), "Resources", "Views", "CRM", module, file)).Root!
            .Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")!.Value);
    }

    private static string Between(string text, string start, string end)
    {
        var from = text.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, start);
        var to = text.IndexOf(end, from + start.Length, StringComparison.Ordinal);
        Assert.True(to > from, end);
        return text[from..(to + end.Length)];
    }

    private static string VeScript() =>
        File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "VisitExecution", "visit-execution.js")).Replace("\r\n", "\n");

    private static string VeView() =>
        File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "VisitExecution", "Index.cshtml"));

    private static string VpScript(string file) =>
        File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "VisitPlanning", file)).Replace("\r\n", "\n");

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }
}

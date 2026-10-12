using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers.CRM;
using Diten.Web.Models.CRM;
using Diten.Web.Views.CRM.CycleCapacities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-CYC-UI-2 — the cycle capacity screens, measured on the REAL controller, models, views and scripts, with a fake
/// gateway standing in for the CRM (and for WP-CYC-UI-1's usage endpoint, which is written in parallel):
/// <list type="bullet">
/// <item>the typical-visit triple reaches the CRM all-or-none, on the save AND on the live preview;</item>
/// <item>a month's FTE is sent only when touched (invariant culture), so an untouched month keeps the stored value;</item>
/// <item>the form's limits come from the CRM contract;</item>
/// <item>the live preview is debounced, single and newest-wins;</item>
/// <item>legacy band, closed-period read-only, K-4 "no number without a calendar";</item>
/// <item>the waterfall is read off the CRM totals, supply / demand off the period usage;</item>
/// <item>an unauthorized actor gets one explanation and no skeleton;</item>
/// <item>the seven languages carry the same keys.</item>
/// </list>
/// </summary>
public sealed class CycleCapacityScreensWebTests
{
    private const string GatewayUrl = "http://gateway.test";
    private const string Read = "crm.cycle-capacity.read";
    private const string Manage = "crm.cycle-capacity.manage";
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
    private static readonly Guid TenantId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid CapacityId = Guid.Parse("c0000000-0000-0000-0000-000000000001");
    private static readonly Guid PeriodId = Guid.Parse("a0000000-0000-0000-0000-000000000001");

    // ============================================================ typical triple: all or none

    [Fact]
    public async Task Save_sends_the_typical_triple_when_all_three_are_filled()
    {
        var gateway = SaveGateway();
        var controller = Controller(gateway, Manage);

        await controller.Edit(CapacityId, Model(typicalPromo: 2, typicalNonPromo: 1, reportPerVisit: 5), default);

        var body = PutBody(gateway);
        Assert.Equal(2, body.GetProperty("typicalPromoCount").GetInt32());
        Assert.Equal(1, body.GetProperty("typicalNonPromoCount").GetInt32());
        Assert.Equal(5, body.GetProperty("reportMinutesPerVisit").GetInt32());
    }

    [Theory]
    [InlineData(2, 1, null)]
    [InlineData(2, null, 5)]
    [InlineData(null, 1, 5)]
    [InlineData(2, null, null)]
    [InlineData(null, null, null)]
    public async Task Save_sends_no_typical_field_unless_all_three_are_filled(int? promo, int? nonPromo, int? report)
    {
        var gateway = SaveGateway();
        var controller = Controller(gateway, Manage);

        await controller.Edit(CapacityId, Model(promo, nonPromo, report), default);

        var body = PutBody(gateway);
        Assert.False(body.TryGetProperty("typicalPromoCount", out _));
        Assert.False(body.TryGetProperty("typicalNonPromoCount", out _));
        Assert.False(body.TryGetProperty("reportMinutesPerVisit", out _));
    }

    [Fact]
    public async Task Preview_proxy_sends_the_triple_only_when_complete_and_never_the_ceilings()
    {
        var gateway = PreviewGateway(ResolvedCalc());
        var controller = Controller(gateway, Read);

        await controller.CalculationPreview(Json(PreviewInput(typicalPromo: 2, typicalNonPromo: 1, report: 5)), default);
        await controller.CalculationPreview(Json(PreviewInput(typicalPromo: 2, typicalNonPromo: 1, report: null)), default);

        var complete = JsonDocument.Parse(gateway.Requests[0].Body!).RootElement;
        Assert.Equal(2, complete.GetProperty("typicalPromoCount").GetInt32());
        Assert.Equal(1, complete.GetProperty("typicalNonPromoCount").GetInt32());
        Assert.Equal(5, complete.GetProperty("reportMinutesPerVisit").GetInt32());

        var partial = JsonDocument.Parse(gateway.Requests[1].Body!).RootElement;
        Assert.False(partial.TryGetProperty("typicalPromoCount", out _));
        Assert.False(partial.TryGetProperty("typicalNonPromoCount", out _));
        Assert.False(partial.TryGetProperty("reportMinutesPerVisit", out _));

        // The CRM preview takes no ceilings and no "isNew": the Web keeps those to judge the blockers.
        Assert.All(gateway.Requests, r =>
        {
            var body = JsonDocument.Parse(r.Body!).RootElement;
            Assert.False(body.TryGetProperty("maxPromoProducts", out _));
            Assert.False(body.TryGetProperty("isNew", out _));
            Assert.Equal(TenantId.ToString(), r.Tenant);
        });
        Assert.All(gateway.Requests, r => Assert.Equal($"{GatewayUrl}/api/crm/cycle-capacities/calculation-preview", r.Uri));
    }

    [Fact]
    public async Task Preview_proxy_refuses_a_tenant_id_in_the_body_without_calling_the_crm()
    {
        var gateway = PreviewGateway(ResolvedCalc());
        var controller = Controller(gateway, Read);

        var result = await controller.CalculationPreview(Json(new { cyclePeriodId = PeriodId, tenantId = Guid.NewGuid() }), default);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(gateway.Requests);
    }

    // ============================================================ FTE (K-5)

    [Fact]
    public async Task Only_a_touched_month_sends_its_fte_so_an_untouched_month_keeps_the_stored_value()
    {
        var gateway = SaveGateway();
        var controller = Controller(gateway, Manage);
        var model = Model(2, 1, 5);
        model.Months =
        [
            new() { Year = 2026, MonthNumber = 10, MeetingDays = 1, TrainingDays = 0, VacationDays = 0, MicroTargetingDayCount = 0, MicroTargetingDuration = 0, FteText = "0.75", FteTouched = true },
            new() { Year = 2026, MonthNumber = 11, MeetingDays = 0, TrainingDays = 0, VacationDays = 0, MicroTargetingDayCount = 0, MicroTargetingDuration = 0, FteText = "1", FteTouched = false }
        ];

        // A Turkish request culture must not read "0.75" as 75: the FTE travels as an invariant string.
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
        try
        {
            await controller.Edit(CapacityId, model, default);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }

        var months = PutBody(gateway).GetProperty("months");
        Assert.Equal(0.75m, months[0].GetProperty("fte").GetDecimal());
        Assert.False(months[1].TryGetProperty("fte", out _));
    }

    [Fact]
    public void The_form_posts_the_fte_through_an_invariant_hidden_twin_and_marks_it_touched_from_script()
    {
        var form = View("_Form.cshtml");
        Assert.Contains("name=\"Months[@i].FteText\"", form);
        Assert.Contains("name=\"Months[@i].FteTouched\"", form);
        // The visible FTE input has no name — only the twin is posted.
        Assert.Matches(new Regex(@"<input type=""number"" class=""[^""]*js-fte-input"), form);
        Assert.DoesNotContain("asp-for=\"Months[i].Fte\"", form);
        Assert.Contains("step=\"@CycleCapacityFormLimits.FteStep.ToString(inv)\"", form);
        Assert.Equal(0.05m, CycleCapacityFormLimits.FteStep);
        Assert.Equal(1m, CycleCapacityFormLimits.MaxFte);

        var script = Script("form.js");
        Assert.Contains("flag.value = 'true'", script);
        Assert.Contains("touchFte(row, source.value)", script); // "apply to all months" counts as authored
    }

    // ============================================================ limits from the contract (E9)

    [Fact]
    public async Task The_form_limits_come_from_the_crm_contract()
    {
        var gateway = new Gateway((_, uri) => uri switch
        {
            _ when uri.EndsWith("/api/crm/cycle-capacities/contract") => (HttpStatusCode.OK, """
                {"isSuccessful":true,"data":{"defaults":{"dailyWorkMinutes":420,"fte":1,"fteSource":"interim-default"},
                 "limits":{"maxMinutesPerDay":1000,"maxMinutesPerVisit":300,"maxBufferMinutes":120,"minDailyWorkMinutes":5,
                           "maxDailyWorkMinutes":900,"maxDeductionDays":20,"maxDescriptionLength":1000}}}
                """),
            _ when uri.Contains("/api/crm/cycle-periods/selector") => (HttpStatusCode.OK, """{"isSuccessful":true,"data":{"items":[]}}"""),
            _ => (HttpStatusCode.NotFound, "")
        });
        var controller = Controller(gateway, Manage);

        var model = Assert.IsType<CycleCapacityEditViewModel>(Assert.IsType<ViewResult>(await controller.Create(null, null, default)).Model);

        Assert.True(model.Limits.FromContract);
        Assert.Equal(300, model.Limits.MaxMinutesPerVisit);
        Assert.Equal(120, model.Limits.MaxBufferMinutes);
        Assert.Equal(900, model.Limits.MaxDailyWorkMinutes);
        Assert.Equal(5, model.Limits.MinDailyWorkMinutes);
        Assert.Equal(20, model.Limits.MaxDeductionDays);
        Assert.Equal(420, model.DailyWorkMinutes);

        // …and the form renders those numbers rather than literals of its own.
        var form = View("_Form.cshtml");
        Assert.Contains("max=\"@limits.MaxMinutesPerVisit\"", form);
        Assert.Contains("max=\"@limits.MaxBufferMinutes\"", form);
        Assert.Contains("max=\"@limits.MaxDailyWorkMinutes\"", form);
        Assert.DoesNotContain("max=\"480\"", form);
        Assert.DoesNotContain("max=\"240\"", form);
    }

    [Fact]
    public void Without_a_readable_contract_the_limits_fall_back_to_the_crm_constants_not_looser_ones()
    {
        var limits = CycleCapacityFormLimits.From(null);
        Assert.False(limits.FromContract);
        Assert.Equal(1440, limits.MaxMinutesPerDay);
        Assert.Equal(480, limits.MaxMinutesPerVisit);
        Assert.Equal(240, limits.MaxBufferMinutes);
    }

    // ============================================================ the live preview request

    [Fact]
    public void The_live_preview_is_debounced_single_and_newest_wins()
    {
        var script = Script("form.js");

        Assert.Matches(new Regex(@"const DEBOUNCE_MS = \d{3};"), script);
        // debounced: every keystroke restarts ONE timer
        Assert.Contains("window.clearTimeout(debounceHandle);", script);
        Assert.Contains("debounceHandle = window.setTimeout(requestPreview, DEBOUNCE_MS);", script);
        // single: a newer request aborts the older one; an answer that is no longer the newest is dropped
        Assert.Contains("inFlight?.abort();", script);
        Assert.Contains("signal: controller.signal", script);
        Assert.Contains("if (mine !== token) return;", script);
        // an unchanged question is not asked again
        Assert.Contains("if (body === lastAnswered) return;", script);
        // the inputs schedule the request; they never call it directly
        Assert.Contains("el.addEventListener('input', schedulePreview);", script);
        Assert.DoesNotContain("addEventListener('input', requestPreview)", script);
    }

    [Fact]
    public void The_form_script_computes_no_capacity_figure_of_its_own()
    {
        var script = Script("form.js");
        // The figures are painted from the server's answer; the old client-side budget arithmetic is gone.
        Assert.DoesNotContain("evaluateBudget", script);
        Assert.DoesNotContain("sum(visitMinuteEls)", script);
        Assert.Contains("paintSummary(envelope.summary)", script);
        Assert.Contains("const PREVIEW_URL = '/CRM/CycleCapacities/api/capacities/calculation-preview';", script);
    }

    // ============================================================ K-4: no number without a calendar

    [Fact]
    public async Task An_unresolved_calendar_yields_no_number_at_all_from_the_preview()
    {
        var gateway = PreviewGateway(UnresolvedCalc(), HttpStatusCode.ServiceUnavailable);
        var controller = Controller(gateway, Read);

        var result = Assert.IsType<ContentResult>(await controller.CalculationPreview(Json(PreviewInput(2, 1, 5)), default));

        Assert.Equal(503, result.StatusCode);
        var summary = JsonDocument.Parse(result.Content!).RootElement.GetProperty("summary");
        Assert.Equal("calendar_unresolved", summary.GetProperty("calendarStatus").GetString());
        Assert.Equal(JsonValueKind.Null, summary.GetProperty("visits").ValueKind);
        Assert.False(summary.GetProperty("hasNumber").GetBoolean());
        Assert.Contains("calendar_unresolved", summary.GetProperty("reasonCodes").EnumerateArray().Select(c => c.GetString()));
    }

    [Fact]
    public void An_unresolved_calendar_shows_no_weekday_estimate_even_if_month_rows_arrive()
    {
        // Defensive: an unresolved answer that still carried month rows must not turn into a number.
        var calc = Calc(ResolvedCalc());
        calc.Resolution = "calendar_unresolved";
        calc.Totals = null;

        var summary = CycleCapacitySummary.Build(calc);

        Assert.Null(summary.Visits);
        Assert.Null(summary.WorkingDays);
        Assert.Null(summary.FieldDays);
        Assert.Null(summary.RemainingMinutes);
        Assert.Null(summary.AverageFte);
        Assert.Empty(summary.Warnings);
        Assert.Null(CycleCapacityWaterfall.From(calc));
        Assert.False(CycleCapacitySupplyDemand.From(calc, new CyclePeriodUsageApiModel()).SupplyAvailable);

        var forbidden = Calc(UnresolvedCalc());
        forbidden.Resolution = "calendar_forbidden";
        Assert.Null(CycleCapacitySummary.Build(forbidden).Visits);
    }

    [Fact]
    public void The_pages_paint_figures_only_when_the_summary_carries_a_number()
    {
        var script = Script("form.js");
        Assert.Contains("if (!data || summary.visits === null || summary.visits === undefined) return;", script);

        var details = View("Details.cshtml");
        Assert.Contains("@if (waterfall is null)", details);
        Assert.Contains("@if (calc is null || !calc.IsResolved || calc.Months.Count == 0)", details);
    }

    [Fact]
    public void A_resolved_calendar_gives_the_crm_totals_as_the_summary()
    {
        var summary = CycleCapacitySummary.Build(Calc(ResolvedCalc()));

        Assert.Equal("resolved", summary.CalendarStatus);
        Assert.Equal(512, summary.Visits);
        Assert.Equal(60, summary.WorkingDays);
        Assert.Equal(4, summary.DeductedDays);
        Assert.Equal(56, summary.FieldDays);
        Assert.Equal(17_400, summary.RemainingMinutes);
        Assert.Equal(0.95m, summary.AverageFte);
        Assert.Equal(34, summary.TypicalVisitMinutes);
    }

    // ============================================================ blockers

    [Fact]
    public void Blockers_name_the_shapes_the_crm_would_refuse_and_withhold_the_number()
    {
        var calc = Calc(ResolvedCalc());

        Assert.Contains(CycleCapacitySummary.Build(calc, Input(2, null, 5)).Blocks, b => b.Code == "typical_visit_incomplete");
        Assert.Contains(CycleCapacitySummary.Build(calc, Input(0, 0, 5)).Blocks, b => b.Code == "typical_visit_empty");
        Assert.Contains(CycleCapacitySummary.Build(calc, Input(4, 1, 5, maxPromo: 3)).Blocks, b => b.Code == "typical_count_exceeds_max");
        Assert.Contains(CycleCapacitySummary.Build(calc, Input(null, null, null, isNew: true)).Blocks, b => b.Code == "typical_visit_required");
        Assert.Contains(CycleCapacitySummary.Build(calc, Input(2, 1, 5, dailyWork: 100)).Blocks, b => b.Code == "cycle_capacity_daily_spend_exceeds_day");

        var blocked = CycleCapacitySummary.Build(calc, Input(2, null, 5));
        Assert.Null(blocked.Visits);

        // A legacy row being edited with an empty triple is NOT blocked: saving keeps the old model.
        var legacy = CycleCapacitySummary.Build(calc, Input(null, null, null, isNew: false));
        Assert.Empty(legacy.Blocks);
        Assert.Equal(512, legacy.Visits);
    }

    [Fact]
    public void Warnings_flag_a_zeroed_month_and_clipped_micro_targeting_from_the_crm_rows()
    {
        var calc = Calc(ResolvedCalc());
        calc.Months[1].DeductedDays = 25; // > 22 working days
        calc.Months[1].FieldDays = 0;
        calc.Months[1].MicroTargetingDayCount = 3;
        calc.Months[1].MicroTargetingDuration = 30;

        var summary = CycleCapacitySummary.Build(calc);

        Assert.Contains(summary.Warnings, w => w.Code == "month_zeroed" && w.MonthNumber == 11);
        Assert.Contains(summary.Warnings, w => w.Code == "micro_targeting_clipped" && w.MonthNumber == 11);
    }

    [Fact]
    public async Task A_typical_visit_refusal_on_save_is_shown_localised_under_the_typical_fields()
    {
        var gateway = new Gateway((method, _) => method == HttpMethod.Put
            ? (HttpStatusCode.BadRequest, """{"errors":["TypicalPromoCount, TypicalNonPromoCount and ReportMinutesPerVisit go together.","typical_visit_incomplete"]}""")
            : (HttpStatusCode.NotFound, ""));
        var controller = Controller(gateway, Manage);

        await controller.Edit(CapacityId, Model(2, 1, 5), default);

        var field = controller.ModelState[nameof(CycleCapacityEditViewModel.TypicalPromoCount)]!;
        Assert.Equal("L:Err_typical_visit_incomplete", Assert.Single(field.Errors).ErrorMessage);
        Assert.DoesNotContain(controller.ModelState[string.Empty]?.Errors ?? [], e => e.ErrorMessage.Contains("go together"));
    }

    // ============================================================ legacy band / closed period

    [Fact]
    public async Task A_legacy_capacity_opens_with_an_empty_triple_and_the_legacy_band()
    {
        var controller = Controller(EditGateway(DetailJson(visitModel: "legacy", isEditable: true)), Manage);

        var model = Assert.IsType<CycleCapacityEditViewModel>(Assert.IsType<ViewResult>(await controller.Edit(CapacityId, (string?)null, default)).Model);

        Assert.True(model.IsLegacyModel);
        Assert.Null(model.TypicalPromoCount);
        Assert.Null(model.TypicalNonPromoCount);
        Assert.Null(model.ReportMinutesPerVisit);
        Assert.Equal(15, model.ReportDuration); // the stored per-day report rides a hidden field, so the old model survives a save

        var form = View("_Form.cshtml");
        Assert.Matches(new Regex(@"@if \(Model\.IsLegacyModel\)\s*\{[\s\S]{0,800}id=""legacyModelBand"""), form);
        Assert.Contains("<input type=\"hidden\" asp-for=\"ReportDuration\" />", form);
        Assert.Contains("id=\"legacyModelBand\"", View("Details.cshtml"));
    }

    [Fact]
    public async Task A_typical_capacity_opens_with_its_triple_and_no_band()
    {
        var controller = Controller(EditGateway(DetailJson(visitModel: "typical", isEditable: true)), Manage);

        var model = Assert.IsType<CycleCapacityEditViewModel>(Assert.IsType<ViewResult>(await controller.Edit(CapacityId, (string?)null, default)).Model);

        Assert.False(model.IsLegacyModel);
        Assert.Equal(2, model.TypicalPromoCount);
        Assert.Equal(1, model.TypicalNonPromoCount);
        Assert.Equal(5, model.ReportMinutesPerVisit);
        Assert.Equal("authored", model.Months[0].FteSource);
        Assert.Equal("0.9", model.Months[0].FteText);
        Assert.False(model.Months[0].FteTouched);
    }

    [Fact]
    public async Task A_closed_period_renders_the_whole_form_read_only_with_its_reason()
    {
        var controller = Controller(EditGateway(DetailJson(visitModel: "typical", isEditable: false)), Manage);

        var model = Assert.IsType<CycleCapacityEditViewModel>(Assert.IsType<ViewResult>(await controller.Edit(CapacityId, (string?)null, default)).Model);
        Assert.False(model.IsEditable);

        var form = View("_Form.cshtml");
        Assert.Contains("var readOnly = !Model.IsEditable || Model.IsArchived;", form);
        Assert.Contains("<fieldset class=\"border-0 p-0 m-0 min-w-0\" disabled=\"@readOnly\">", form);
        Assert.Matches(new Regex(@"@if \(!readOnly\)\s*\{\s*<button type=""submit"""), form);
        Assert.Contains("ReadOnlyClosedBody", form);
        // …and the script sends nothing when the form is read-only (no listeners, no bulk apply).
        Assert.Contains("if (!readOnly) {", Script("form.js"));
    }

    // ============================================================ waterfall + supply / demand

    [Fact]
    public void The_waterfall_steps_are_the_crm_totals_in_order()
    {
        var waterfall = CycleCapacityWaterfall.From(Calc(ResolvedCalc()))!;

        Assert.Equal(CycleCapacityWaterfall.StepLabelKeys, waterfall.Steps.Select(s => s.LabelKey).ToArray());
        Assert.Equal([60, 4, 56, 26_880, 5_880, 3_600, 17_400], waterfall.Steps.Select(s => s.Value).ToArray());
        Assert.Equal([false, true, false, false, true, true, false], waterfall.Steps.Select(s => s.IsDeduction).ToArray());
        Assert.Equal(34, waterfall.TypicalVisitMinutes);
        Assert.Equal(0.95m, waterfall.AverageFte);
        Assert.Equal(512, waterfall.Visits);
    }

    [Fact]
    public async Task Details_compares_monthly_capacity_with_the_periods_planned_demand()
    {
        var gateway = DetailsGateway(UsageJson);
        var controller = Controller(gateway, Read);

        var view = Assert.IsType<ViewResult>(await controller.Details(CapacityId, null, default));

        Assert.Contains(gateway.Requests, r => r.Uri == $"{GatewayUrl}/api/crm/cycle-periods/{PeriodId}/usage" && r.Tenant == TenantId.ToString());
        var sd = Assert.IsType<CycleCapacitySupplyDemand>(view.ViewData["SupplyDemand"]);
        Assert.True(sd.UsageAvailable);
        Assert.True(sd.SupplyAvailable);
        Assert.Equal([(2026, 10, 260, 240), (2026, 11, 252, 300)],
            sd.Months.Select(m => (m.Year, m.MonthNumber, m.Supply!.Value, m.Demand)).ToArray());
        var over = Assert.Single(sd.OverMonths);
        Assert.Equal(11, over.MonthNumber);
        Assert.Equal(48, over.Overage);
        Assert.Equal(512, sd.TotalSupply);
        Assert.Equal(540, sd.TotalDemand);
        Assert.Equal(105, sd.Percent);
        Assert.Equal(0, sd.FreeCapacity);
        Assert.Equal("Oturum A", Assert.Single(sd.Sessions).Name);
        Assert.NotNull(view.ViewData["Waterfall"]);
    }

    [Fact]
    public async Task Details_says_so_when_the_usage_cannot_be_read_and_still_renders()
    {
        var gateway = DetailsGateway(usageJson: null);
        var controller = Controller(gateway, Read);

        var view = Assert.IsType<ViewResult>(await controller.Details(CapacityId, null, default));

        var sd = Assert.IsType<CycleCapacitySupplyDemand>(view.ViewData["SupplyDemand"]);
        Assert.False(sd.UsageAvailable);
        Assert.Empty(sd.Months);
        Assert.Contains("UsageUnavailable", View("Details.cshtml"));
    }

    // ============================================================ UAS-001

    [Fact]
    public async Task An_unauthorized_actor_gets_one_explanation_no_skeleton_and_no_data_read()
    {
        var gateway = DetailsGateway(UsageJson);
        var controller = Controller(gateway /* no permission */);

        foreach (var result in new[]
                 {
                     await controller.Index(null, null, default),
                     await controller.Create(null, null, default),
                     await controller.Edit(CapacityId, (string?)null, default),
                     await controller.Details(CapacityId, null, default)
                 })
        {
            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal(403, view.StatusCode);
            Assert.Equal("~/Views/CRM/CycleCapacities/AccessDenied.cshtml", view.ViewName);
            Assert.Null(view.Model);
        }

        var json = await controller.CalculationPreview(Json(PreviewInput(2, 1, 5)), default);
        Assert.Equal(403, Assert.IsType<ObjectResult>(json).StatusCode);
        Assert.Empty(gateway.Requests);

        var denied = View("AccessDenied.cshtml");
        Assert.Contains("<partial name=\"_AccessDenied\"", denied);
        Assert.DoesNotContain("<table", denied);
        Assert.DoesNotContain("btn", denied);
    }

    // ============================================================ seven languages

    [Fact]
    public void The_seven_languages_carry_the_same_non_empty_keys()
    {
        var reference = Resx("en").Keys.ToHashSet(StringComparer.Ordinal);
        foreach (var language in Languages)
        {
            var values = Resx(language);
            Assert.True(reference.SetEquals(values.Keys), $"{language}: key set differs from en");
            Assert.All(values, kv => Assert.False(string.IsNullOrWhiteSpace(kv.Value), $"{language}: empty {kv.Key}"));
        }
    }

    [Fact]
    public void Every_code_step_and_page_key_the_screens_use_exists_in_every_language()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in new[] { "_Form.cshtml", "Details.cshtml", "_DataTable.cshtml", "_Filter.cshtml", "_IndexL10n.cshtml", "Index.cshtml", "AccessDenied.cshtml" })
        {
            foreach (Match m in Regex.Matches(View(file), @"(?<!Shared)Localizer\[""([A-Za-z0-9_]+)""")) keys.Add(m.Groups[1].Value);
        }

        keys.Remove("Code_");
        keys.UnionWith(CycleCapacityL10nCodes.All.Select(c => "Code_" + c));
        keys.UnionWith(CycleCapacityWaterfall.StepLabelKeys);
        keys.UnionWith(CycleCapacitiesController.TypicalRefusalFields.Keys.Select(c => "Err_" + c));

        foreach (var language in Languages)
        {
            var values = Resx(language);
            Assert.All(keys, key => Assert.True(values.ContainsKey(key), $"{language}: missing {key}"));
        }

        // Turkish keeps its diacritics; Arabic is Arabic script.
        Assert.Equal("Tahmini ziyaret (temsilci başına)", Resx("tr")["ColEstimatedVisits"]);
        Assert.Equal("Hesap şelalesi", Resx("tr")["WaterfallTitle"]);
        Assert.Matches(new Regex(@"\p{IsArabic}"), Resx("ar")["SupplyDemandTitle"]);
    }

    [Fact]
    public void The_script_bridge_and_its_loader_carry_the_new_keys()
    {
        var bridge = View("_IndexL10n.cshtml");
        var loader = Script("index.l10n.js");
        foreach (var key in new[] { "LegacyBadge", "StatusEditable", "StatusNotCalculable", "UnitMinutesShort", "NoNumberBlocked", "CalendarUnresolvedShort", "Codes" })
        {
            Assert.Contains($"{key} =", bridge);
            Assert.Contains($"'{key}'", loader);
        }

        Assert.Contains("Codes = CycleCapacityL10nCodes.All.ToDictionary(code => code, code => Localizer[\"Code_\" + code].Value)", bridge);
        Assert.Contains("const CODES = L.Codes || {};", Script("form.js"));
    }

    // ============================================================ list

    [Fact]
    public void The_list_shows_the_wp_columns_and_fills_the_estimate_lazily_for_visible_rows_only()
    {
        var table = View("_DataTable.cshtml");
        foreach (var key in new[] { "ColPeriod", "CalendarCountryCode", "ColEstimatedVisits", "ColTypicalVisit", "ColAverageFte", "ColProductLimits", "UpdatedAt" })
        {
            Assert.Contains($"@Localizer[\"{key}\"]", table);
        }

        Assert.Equal(10, Regex.Matches(table, "<th[ >]").Count);

        var index = Script("index.js");
        Assert.Contains("const totalColumnCount = 10;", index);
        Assert.Contains("api.rows({ page: 'current', search: 'applied' })", index);
        Assert.Contains("visits: resolved ? data.totals.visits : null", index);
        Assert.Contains("row.visitModel === 'legacy'", index);
        Assert.Contains("matchesSingle(appliedFilters.year, r.cycleYear)", index);
        Assert.Contains("id=\"filterYear\"", View("_Filter.cshtml"));
    }

    // ============================================================ fixtures

    private static CycleCapacityEditViewModel Model(int? typicalPromo, int? typicalNonPromo, int? reportPerVisit) => new()
    {
        CycleCapacityId = CapacityId,
        CyclePeriodId = PeriodId,
        CalendarCountryCode = "TR",
        DailyWorkMinutes = 480,
        PromoProductTime = 12,
        NonPromoProductTime = 5,
        TravelingTime = 90,
        ReportDuration = 0,
        QuizDuration = 15,
        BetweenVisitTimeMinutes = 10,
        TypicalPromoCount = typicalPromo,
        TypicalNonPromoCount = typicalNonPromo,
        ReportMinutesPerVisit = reportPerVisit,
        ExpectedVersion = 3,
        Months =
        [
            new() { Year = 2026, MonthNumber = 10, MeetingDays = 0, TrainingDays = 0, VacationDays = 0, MicroTargetingDayCount = 0, MicroTargetingDuration = 0 }
        ]
    };

    private static object PreviewInput(int? typicalPromo, int? typicalNonPromo, int? report) => new
    {
        cyclePeriodId = PeriodId,
        calendarCountryCode = "tr",
        dailyWorkMinutes = 480,
        promoProductTime = 12,
        nonPromoProductTime = 5,
        travelingTime = 90,
        reportDuration = 0,
        quizDuration = 15,
        typicalPromoCount = typicalPromo,
        typicalNonPromoCount = typicalNonPromo,
        reportMinutesPerVisit = report,
        maxPromoProducts = 3,
        maxNonPromoProducts = 3,
        isNew = false,
        months = new[] { new { year = 2026, monthNumber = 10, meetingDays = 1, fte = 0.9 } }
    };

    private static CycleCapacityPreviewInput Input(int? promo, int? nonPromo, int? report, int? maxPromo = 3, bool isNew = false, int dailyWork = 480) => new()
    {
        CyclePeriodId = PeriodId,
        DailyWorkMinutes = dailyWork,
        TypicalPromoCount = promo,
        TypicalNonPromoCount = nonPromo,
        ReportMinutesPerVisit = report,
        MaxPromoProducts = maxPromo,
        MaxNonPromoProducts = 3,
        IsNew = isNew
    };

    /// <summary>A resolved CRM calculation: two months, typical visit 34 min, daily fixed 105 min.</summary>
    private static string ResolvedCalc() => $$$"""
        {"isSuccessful":true,"statusCode":200,"errors":[],"data":{
          "cycleCapacityId":"00000000-0000-0000-0000-000000000000","cyclePeriodId":"{{{PeriodId}}}","calendarCountryCode":"TR",
          "resolution":"resolved","isEstimate":true,"totalVisitNumber":512,"minutesPerVisit":34,
          "reasonCodes":["capacity_ok"],"reason":"ok","visitModel":"typical","typicalVisitMinutes":34,"dailyFixedMinutes":105,
          "months":[
            {"year":2026,"monthNumber":10,"calendarDays":31,"workingDays":22,"nonWorkingDays":9,"meetingDays":2,"trainingDays":0,"vacationDays":0,
             "microTargetingDayCount":0,"microTargetingDuration":0,"deductedDays":2,"fieldDays":20,"availableMinutes":9600,"microTargetingMinutes":0,
             "spendMinutes":2100,"visitMinutes":7500,"fte":1.0,"totalVisitNumber":260,"dailyFixedMinutes":2100,"remainingMinutes":7500,"typicalVisitMinutes":34},
            {"year":2026,"monthNumber":11,"calendarDays":30,"workingDays":22,"nonWorkingDays":8,"meetingDays":2,"trainingDays":0,"vacationDays":0,
             "microTargetingDayCount":0,"microTargetingDuration":0,"deductedDays":2,"fieldDays":20,"availableMinutes":9600,"microTargetingMinutes":0,
             "spendMinutes":2100,"visitMinutes":7500,"fte":0.9,"totalVisitNumber":252,"dailyFixedMinutes":2100,"remainingMinutes":7500,"typicalVisitMinutes":34}],
          "totals":{"workingDays":60,"deductedDays":4,"fieldDays":56,"availableMinutes":26880,"dailyFixedMinutes":5880,
                    "microTargetingMinutes":3600,"remainingMinutes":17400,"visits":512,"averageFte":0.95} } }
        """;

    private static string UnresolvedCalc() => $$$"""
        {"isSuccessful":false,"statusCode":503,"errors":["no calendar","calendar_unresolved"],"data":{
          "cycleCapacityId":"00000000-0000-0000-0000-000000000000","cyclePeriodId":"{{{PeriodId}}}","calendarCountryCode":"TR",
          "resolution":"calendar_unresolved","isEstimate":true,"totalVisitNumber":null,"minutesPerVisit":34,"months":[],
          "reasonCodes":["calendar_unresolved"],"reason":"no calendar","visitModel":"typical","typicalVisitMinutes":34,
          "dailyFixedMinutes":105,"totals":null}}
        """;

    private static CycleCapacityCalculationViewModel Calc(string envelope) =>
        JsonSerializer.Deserialize<CycleCapacityGatewayResponse<CycleCapacityCalculationViewModel>>(
            envelope, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Data!;

    private static string DetailJson(string visitModel, bool isEditable)
    {
        var typical = visitModel == "typical";
        return $$$"""
            {"isSuccessful":true,"data":{
              "cycleCapacityId":"{{{CapacityId}}}","cyclePeriodId":"{{{PeriodId}}}",
              "cyclePeriod":{"cyclePeriodId":"{{{PeriodId}}}","cycleCode":"tr-2026-04","cycleName":"2026 · 4","year":2026,"sequenceInYear":4,
                "startDate":"2026-10-01T00:00:00+00:00","endDate":"2026-11-30T00:00:00+00:00","cycleStatus":"{{{(isEditable ? "active" : "closed")}}}",
                "scopeType":"country","countryScope":"tr","isClosed":{{{(isEditable ? "false" : "true")}}}},
              "calendarCountryCode":"TR","calendarCountryIsDerived":true,"dailyWorkMinutes":480,"promoProductTime":12,"nonPromoProductTime":5,
              "travelingTime":90,"reportDuration":{{{(typical ? 0 : 15)}}},"quizDuration":15,"betweenVisitTimeMinutes":10,
              "maxPromoProducts":3,"maxNonPromoProducts":3,
              "typicalPromoCount":{{{(typical ? "2" : "null")}}},"typicalNonPromoCount":{{{(typical ? "1" : "null")}}},"reportMinutesPerVisit":{{{(typical ? "5" : "null")}}},
              "visitModel":"{{{visitModel}}}","typicalVisitMinutes":{{{(typical ? 34 : 17)}}},"dailyFixedMinutes":105,
              "months":[{"year":2026,"monthNumber":10,"meetingDays":2,"trainingDays":0,"vacationDays":0,"microTargetingDayCount":0,
                         "microTargetingDuration":0,"fte":0.9,"fteSource":"authored"}],
              "isArchived":false,"isEditable":{{{(isEditable ? "true" : "false")}}},"version":3} }
            """;
    }

    private const string UsageJson = """
        {"isSuccessful":true,"data":{
          "capacity":{"cycleCapacityId":"c0000000-0000-0000-0000-000000000001","isArchived":false},
          "campaigns":[],
          "planningSessions":[{"planningSessionId":"d0000000-0000-0000-0000-000000000001","name":"Oturum A","ownerDisplayName":"A. Y.","status":"committed","committedVisitCount":540}],
          "plannedVisits":{"total":540,"byStatus":{"planned":540}},
          "demandByMonth":[{"year":2026,"month":10,"plannedVisits":240},{"year":2026,"month":11,"plannedVisits":300}]}}
        """;

    private static Gateway SaveGateway() => new((method, _) => method == HttpMethod.Put || method == HttpMethod.Post
        ? (HttpStatusCode.NoContent, "")
        : (HttpStatusCode.NotFound, ""));

    private static Gateway PreviewGateway(string calc, HttpStatusCode status = HttpStatusCode.OK) =>
        new((method, uri) => method == HttpMethod.Post && uri.EndsWith("/calculation-preview") ? (status, calc) : (HttpStatusCode.NotFound, ""));

    private static Gateway EditGateway(string detail) => new((_, uri) => uri switch
    {
        _ when uri.EndsWith($"/api/crm/cycle-capacities/{CapacityId}") => (HttpStatusCode.OK, detail),
        _ when uri.EndsWith("/api/crm/cycle-capacities/contract") => (HttpStatusCode.OK, """{"isSuccessful":true,"data":{"limits":{"maxMinutesPerDay":1440,"maxMinutesPerVisit":480,"maxBufferMinutes":240}}}"""),
        _ when uri.Contains("/api/crm/cycle-periods/selector") => (HttpStatusCode.OK, """{"isSuccessful":true,"data":{"items":[]}}"""),
        _ => (HttpStatusCode.NotFound, "")
    });

    private static Gateway DetailsGateway(string? usageJson) => new((_, uri) => uri switch
    {
        _ when uri.EndsWith($"/api/crm/cycle-capacities/{CapacityId}") => (HttpStatusCode.OK, DetailJson("typical", true)),
        _ when uri.EndsWith($"/api/crm/cycle-capacities/{CapacityId}/calculation") => (HttpStatusCode.OK, ResolvedCalc()),
        _ when uri.EndsWith($"/api/crm/cycle-periods/{PeriodId}/usage") && usageJson is not null => (HttpStatusCode.OK, usageJson),
        _ => (HttpStatusCode.NotFound, "")
    });

    private static JsonElement PutBody(Gateway gateway) =>
        JsonDocument.Parse(Assert.Single(gateway.Requests, r => r.Method == HttpMethod.Put).Body!).RootElement;

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    private static CycleCapacitiesController Controller(Gateway gateway, params string[] permissions)
    {
        var controller = new CycleCapacitiesController(new HttpClient(gateway), Configuration(),
            NullLogger<CycleCapacitiesController>.Instance, new CapacityLocalizer());
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.TempData = new TempDataDictionary(http, new NullTempDataProvider());
        return controller;
    }

    private static IConfiguration Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl }).Build();

    private static string View(string file) => File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "CycleCapacities", file));

    private static string Script(string file) => File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "CycleCapacities", file));

    private static Dictionary<string, string> Resx(string language)
    {
        var path = Path.Combine(WebRoot(), "Resources", "Views", "CRM", "CycleCapacities", $"CycleCapacitiesIndex.{language}.resx");
        return XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);
    }

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }

    private sealed class Gateway(Func<HttpMethod, string, (HttpStatusCode Status, string Body)> route) : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Uri, string? Tenant, string? Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!.ToString();
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            Requests.Add((request.Method, uri, request.Headers.TryGetValues("X-Tenant-Id", out var t) ? t.Single() : null, body));
            var (status, payload) = route(request.Method, uri);
            return new HttpResponseMessage(status) { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class CapacityLocalizer : IStringLocalizer<CycleCapacitiesIndex>
    {
        public LocalizedString this[string name] => new(name, "L:" + name);
        public LocalizedString this[string name, params object[] arguments] => new(name, "L:" + name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}

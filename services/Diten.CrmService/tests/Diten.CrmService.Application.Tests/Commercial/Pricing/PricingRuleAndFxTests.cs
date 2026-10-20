using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Commercial.Pricing;
using Diten.CrmService.Application.Features.Commercial.Pricing.Contracts;
using Diten.CrmService.Application.Features.Commercial.Pricing.FxRates;
using Diten.CrmService.Application.Features.Commercial.Pricing.PricingHistory;
using Diten.CrmService.Application.Features.Commercial.Pricing.PricingRules;
using Diten.CrmService.Application.Features.Commercial.Pricing.PricingRules.Commands;
using Diten.CrmService.Application.Features.Commercial.Pricing.PricingRules.Queries;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Domain.Entities.Commercial;
using Xunit;

namespace Diten.CrmService.Application.Tests.Commercial.Pricing;

/// <summary>
/// WP-VW-W6 W6-BE-b — MOD-0156 guardrail rules (§4.3: limits soft ≤ hard, the segment not yet a criterion, a Retired SKU
/// refused, four eyes, a revision supersedes, a retirement retires, no two active rules of one specificity overlap, an
/// active rule is never edited, the trial is a read) and the interim FX rates (§4.5: one rate per key, the latest ≤ asOf,
/// a correction logged, the import counts).
/// </summary>
public sealed class PricingRuleAndFxTests : IDisposable
{
    private readonly PricingWorld _w = new();

    public void Dispose() => _w.Dispose();

    private static string Code<T>(Response<T> r) => r.Errors?.FirstOrDefault() ?? string.Empty;

    private static PricingRuleInput Rule(string sku = "TUT", decimal soft = 10, decimal hard = 15, DateOnly? from = null, DateOnly? to = null,
        string? segment = null, Guid? le = null)
        => new(le ?? PricingWorld.LeTr, PricingWorld.Sku(sku), segment, from, to, new PricingPctLimitInput(soft, hard),
            new PricingMfLimitInput(new PricingMfTermInput(10, 2), new PricingMfTermInput(10, 3)), new PricingPctLimitInput(20, 25),
            new PricingQuantityLimitInput(10, 200, 300));

    private async Task<PricingRuleDto> Active(PricingRuleInput input)
    {
        var draft = await _w.Send(new CreatePricingRuleCommand(input), PricingWorld.Marketing);
        Assert.True(draft.IsSuccessful, string.Join(" | ", draft.Errors ?? []));
        return await Approve(draft.Data!.Id);
    }

    private async Task<PricingRuleDto> Approve(Guid id)
    {
        var submitted = await _w.Send(new SubmitPricingRuleCommand(id, null), PricingWorld.Marketing);
        Assert.True(submitted.IsSuccessful, string.Join(" | ", submitted.Errors ?? []));
        var decided = await _w.Send(new DecidePricingRuleCommand(id, new PricingRuleDecisionInput("approve", null)), PricingWorld.SalesOps);
        Assert.True(decided.IsSuccessful, string.Join(" | ", decided.Errors ?? []));
        return decided.Data!;
    }

    // ── rules ────────────────────────────────────────────────────────────────────────────────────────────────────

    public static TheoryData<PricingRuleInput, string> BadRules => new()
    {
        { Rule(soft: 16, hard: 15), PricingErrors.RuleLimitsInvalid },                                   // soft above hard
        { Rule(soft: 10, hard: 101), PricingErrors.RuleLimitsInvalid },                                  // above 100 %
        { Rule() with { MaxMf = new PricingMfLimitInput(new PricingMfTermInput(10, 4), new PricingMfTermInput(10, 3)) }, PricingErrors.RuleLimitsInvalid },
        { Rule() with { MaxMf = new PricingMfLimitInput(new PricingMfTermInput(0, 2), null) }, PricingErrors.RuleLimitsInvalid },
        { Rule() with { Quantity = new PricingQuantityLimitInput(50, 20, 300) }, PricingErrors.RuleLimitsInvalid },
        { new PricingRuleInput(PricingWorld.LeTr, PricingWorld.Sku("TUT"), null, null, null, null, null, null, null), PricingErrors.RuleLimitsInvalid },
        { Rule(segment: "A"), PricingErrors.SegmentNotEnabled },                                         // §20.3: no segment yet
        { Rule(sku: "AL5"), PricingErrors.SkuRetired },
        { Rule(le: Guid.NewGuid()), PricingErrors.LegalEntityInvalid },
        { Rule(from: new DateOnly(2026, 12, 1), to: new DateOnly(2026, 11, 1)), PricingErrors.Invalid }
    };

    [Theory]
    [MemberData(nameof(BadRules))]
    public async Task R1_a_rule_with_bad_limits_a_segment_or_a_Retired_SKU_is_400_and_nothing_is_written(PricingRuleInput input, string code)
    {
        var response = await _w.Send(new CreatePricingRuleCommand(input), PricingWorld.Marketing);

        Assert.Equal((code, 400), (Code(response), response.StatusCode));
        Assert.Empty(_w.Rules.Store);
    }

    [Fact]
    public async Task R2_the_author_cannot_approve_the_second_person_activates_and_the_rule_reaches_the_evaluation()
    {
        var draft = (await _w.Send(new CreatePricingRuleCommand(Rule()), PricingWorld.Marketing)).Data!;
        Assert.Equal((PricingRuleStatuses.Draft, 1, "LS-TUT"), (draft.Status, draft.RuleVersion, draft.SkuCode));
        await _w.Send(new SubmitPricingRuleCommand(draft.Id, null), PricingWorld.Marketing);
        Assert.Equal(PricingApprovalKinds.PricingRule.ObjectType, _w.Workflow.Starts.Single().ObjectType);

        var own = await _w.Send(new DecidePricingRuleCommand(draft.Id, new PricingRuleDecisionInput("approve", null)), PricingWorld.Marketing);
        Assert.Equal((PricingErrors.RuleSod, 403), (Code(own), own.StatusCode));
        Assert.Empty(_w.Decisions.Calls);

        var active = (await _w.Send(new DecidePricingRuleCommand(draft.Id, new PricingRuleDecisionInput("approve", null)), PricingWorld.SalesOps)).Data!;
        Assert.Equal((PricingRuleStatuses.Active, PricingWorld.SalesOps), (active.Status, active.ActivatedBy));

        _w.SeedPublishedList(PricingWorld.LeTr, "TR", "TRY", _w.Today.AddDays(-1), PricingWorld.TrPrices, 10m);
        var result = await _w.Get<IPricingEvaluator>().EvaluateAsync(new EvaluationRequest(PricingWorld.Tenant, PricingWorld.LeTr, "TR", _w.Today,
            PriceTiers.PharmacistPurchase, null, [new EvaluationLine(1, PricingWorld.Sku("TUT"), 20, null, 16m)]));
        Assert.Equal(PricingOutcomes.Hard, result.Outcome);                        // 16 % > the hard 15 %
    }

    [Fact]
    public async Task R3_an_active_rule_is_never_edited_a_revision_supersedes_it_after_approval()
    {
        var v1 = await Active(Rule());

        var edit = await _w.Send(new UpdatePricingRuleCommand(v1.Id, Rule(soft: 12)), PricingWorld.Marketing);
        Assert.Equal((PricingErrors.RuleNotEditable, 409), (Code(edit), edit.StatusCode));

        var v2 = (await _w.Send(new UpdatePricingRuleCommand(v1.Id, Rule(soft: 12, hard: 18), Revision: true), PricingWorld.Marketing)).Data!;
        Assert.Equal((2, v1.RuleKey, PricingRuleStatuses.Draft, PricingRuleChangeKinds.Revise, (decimal?)18m), (v2.RuleVersion, v2.RuleKey, v2.Status, v2.ChangeKind, v2.MaxDiscountPct.Hard));
        var twice = await _w.Send(new UpdatePricingRuleCommand(v1.Id, Rule(), Revision: true), PricingWorld.Marketing);
        Assert.Equal((PricingErrors.RuleOpenDraftExists, 409), (Code(twice), twice.StatusCode));
        Assert.Equal(PricingRuleStatuses.Active, _w.Rules.Store.Single(r => r.Id == v1.Id).Status);   // still in force while v2 waits

        await Approve(v2.Id);

        Assert.Equal(PricingRuleStatuses.Superseded, _w.Rules.Store.Single(r => r.Id == v1.Id).Status);
        var detail = (await _w.Send(new GetPricingRuleByIdQuery(v1.Id))).Data!;
        Assert.Equal(new[] { 1, 2 }, detail.Versions.Select(v => v.RuleVersion).OrderBy(x => x));
        Assert.Contains(_w.Log.Store, e => e.EntityId == v1.Id && e.Action == PricingChangeActions.Superseded);
    }

    [Fact]
    public async Task R4_a_retirement_needs_approval_too_and_then_the_rule_leaves_the_evaluation()
    {
        var v1 = await Active(Rule());

        var request = (await _w.Send(new RequestPricingRuleRetirementCommand(v1.Id), PricingWorld.Marketing)).Data!;
        Assert.Equal(PricingRuleChangeKinds.Retire, request.ChangeKind);
        Assert.Single(await _w.Get<Diten.CrmService.Domain.Repositories.Commercial.IPricingRuleRepository>()
            .ListActiveAsync(PricingWorld.Tenant, PricingWorld.LeTr, [PricingWorld.Sku("TUT")], default));

        await Approve(request.Id);

        Assert.Empty(await _w.Get<Diten.CrmService.Domain.Repositories.Commercial.IPricingRuleRepository>()
            .ListActiveAsync(PricingWorld.Tenant, PricingWorld.LeTr, [PricingWorld.Sku("TUT")], default));
        Assert.Equal(PricingRuleStatuses.Retired, _w.Rules.Store.Single(r => r.Id == v1.Id).Status);
    }

    [Fact]
    public async Task R5_two_active_rules_of_one_specificity_never_overlap_and_disjoint_periods_live_side_by_side()
    {
        await Active(Rule(from: new DateOnly(2026, 11, 1), to: new DateOnly(2026, 11, 30)));
        var overlapping = (await _w.Send(new CreatePricingRuleCommand(Rule(from: new DateOnly(2026, 11, 15), to: new DateOnly(2026, 12, 15))), PricingWorld.Marketing)).Data!;
        var disjoint = (await _w.Send(new CreatePricingRuleCommand(Rule(from: new DateOnly(2026, 12, 1), to: new DateOnly(2026, 12, 31))), PricingWorld.Marketing)).Data!;
        var general = (await _w.Send(new CreatePricingRuleCommand(Rule()), PricingWorld.Marketing)).Data!;   // another specificity

        var refused = await _w.Send(new SubmitPricingRuleCommand(overlapping.Id, null), PricingWorld.Marketing);
        Assert.Equal((PricingErrors.RuleOverlap, 409), (Code(refused), refused.StatusCode));
        await Approve(disjoint.Id);
        await Approve(general.Id);
        Assert.Equal(3, _w.Rules.Store.Count(r => r.Status == PricingRuleStatuses.Active));
    }

    [Fact]
    public async Task R6_a_draft_is_discarded_and_another_tenants_rule_is_404()
    {
        var draft = (await _w.Send(new CreatePricingRuleCommand(Rule()), PricingWorld.Marketing)).Data!;
        Assert.Equal(PricingRuleStatuses.Discarded, (await _w.Send(new DiscardPricingRuleDraftCommand(draft.Id, null), PricingWorld.Marketing)).Data!.Status);

        var foreign = _w.SeedActiveRule(PricingWorld.LeTr, "ALM", [8, 12], [(10, 1), (10, 2)], [15, 20], [5, 100, 150], tenant: PricingWorld.OtherTenant);
        Assert.Equal(404, (await _w.Send(new GetPricingRuleByIdQuery(foreign.Id))).StatusCode);
        Assert.Equal(404, (await _w.Send(new UpdatePricingRuleCommand(foreign.Id, Rule("ALM"), Revision: true), PricingWorld.Marketing)).StatusCode);
        Assert.Equal(404, (await _w.Send(new RequestPricingRuleRetirementCommand(foreign.Id), PricingWorld.Marketing)).StatusCode);
        Assert.Equal(404, (await _w.Send(new DecidePricingRuleCommand(foreign.Id, new PricingRuleDecisionInput("approve", null)), PricingWorld.SalesOps)).StatusCode);
        Assert.DoesNotContain((await _w.Send(new GetPricingRuleListQuery(null, null, null, null, null, null, null))).Data!.Items, r => r.Id == foreign.Id);
    }

    [Fact]
    public async Task R7_the_trial_evaluates_with_the_published_data_and_writes_nothing()
    {
        _w.SeedPublishedList(PricingWorld.LeTr, "TR", "TRY", _w.Today.AddDays(-1), PricingWorld.TrPrices, 10m);
        _w.SeedDemoRules(PricingWorld.LeTr);
        var before = (_w.Rules.Store.Count, _w.Log.Store.Count);

        var trial = await _w.Send(new EvaluatePricingTrialQuery(new PricingTrialInput(PricingWorld.LeTr, null, null, null, null,
            [new PricingTrialLineInput(PricingWorld.Sku("TUT"), 20, new PricingMfTermInput(10, 2), 5m)])));

        Assert.True(trial.IsSuccessful, string.Join(" | ", trial.Errors ?? []));
        Assert.Equal((PricingModes.Priced, PricingOutcomes.Soft, "TRY"), (trial.Data!.Mode, trial.Data.Outcome, trial.Data.Currency));  // 1-(0.95)(0.833)=20.8 % > 20
        Assert.Equal(before, (_w.Rules.Store.Count, _w.Log.Store.Count));
        Assert.Empty(_w.Audit.Bodies);
    }

    [Fact]
    public async Task R8_every_rule_write_is_audited_once()
    {
        var draft = (await _w.Send(new CreatePricingRuleCommand(Rule()), PricingWorld.Marketing)).Data!;
        await _w.Send(new UpdatePricingRuleCommand(draft.Id, Rule(soft: 9)), PricingWorld.Marketing);
        await Approve(draft.Id);
        await _w.Send(new RequestPricingRuleRetirementCommand(draft.Id), PricingWorld.Marketing);

        Assert.Equal(new[] { "CreatePricingRuleCommand", "UpdatePricingRuleCommand", "SubmitPricingRuleCommand", "DecidePricingRuleCommand",
            "RequestPricingRuleRetirementCommand" }, _w.Audit.Bodies.Select(b => System.Text.Json.JsonDocument.Parse(b).RootElement.GetProperty("requestType").GetString()));
    }

    // ── FX rates ───────────────────────────────────────────────────────────────────────────────────────────────────

    private static FxRateInput Fx(DateOnly day, decimal rate, string from = "USD", string to = "AED") => new(PricingWorld.LeAe, from, to, day, rate, "manual");

    [Fact]
    public async Task F1_one_rate_per_key_and_the_reader_takes_the_latest_on_or_before_asOf()
    {
        await _w.Send(new CreateFxRateCommand(Fx(new DateOnly(2026, 10, 1), 3.6725m)), PricingWorld.SalesOps);
        await _w.Send(new CreateFxRateCommand(Fx(new DateOnly(2026, 10, 15), 3.6730m)), PricingWorld.SalesOps);
        var duplicate = await _w.Send(new CreateFxRateCommand(Fx(new DateOnly(2026, 10, 15), 3.70m)), PricingWorld.SalesOps);
        Assert.Equal((PricingErrors.FxRateDuplicate, 409), (Code(duplicate), duplicate.StatusCode));

        var reader = _w.Get<IFxRateReader>();
        Assert.Equal(3.6725m, (await reader.GetAsync(PricingWorld.Tenant, PricingWorld.LeAe, "USD", "AED", new DateOnly(2026, 10, 14), default))!.Rate);
        Assert.Equal(new DateOnly(2026, 10, 15), (await reader.GetAsync(PricingWorld.Tenant, PricingWorld.LeAe, "usd", "aed", new DateOnly(2026, 10, 20), default))!.RateDate);
        Assert.Null(await reader.GetAsync(PricingWorld.Tenant, PricingWorld.LeAe, "USD", "AED", new DateOnly(2026, 9, 30), default));
        Assert.Null(await reader.GetAsync(PricingWorld.OtherTenant, PricingWorld.LeAe, "USD", "AED", new DateOnly(2026, 10, 20), default));
    }

    [Theory]
    [InlineData("USD", "USD", 3.6, "manual")]
    [InlineData("USD", "AE", 3.6, "manual")]
    [InlineData("USD", "AED", 0, "manual")]
    [InlineData("USD", "AED", 3.1234567, "manual")]
    [InlineData("USD", "AED", 3.6, " ")]
    public async Task F2_a_bad_rate_is_400(string from, string to, double rate, string source)
    {
        var response = await _w.Send(new CreateFxRateCommand(new FxRateInput(PricingWorld.LeAe, from, to, _w.Today, (decimal)rate, source)), PricingWorld.SalesOps);
        Assert.Equal((PricingErrors.FxRateInvalid, 400), (Code(response), response.StatusCode));
    }

    [Fact]
    public async Task F3_a_correction_is_logged_with_the_old_and_new_value_and_another_tenant_is_404()
    {
        var rate = (await _w.Send(new CreateFxRateCommand(Fx(_w.Today, 3.6725m)), PricingWorld.SalesOps)).Data!;
        var corrected = (await _w.Send(new UpdateFxRateCommand(rate.Id, new FxRateInput(null, null, null, null, 3.6800m, null, rate.Version)), PricingWorld.SalesOps)).Data!;

        Assert.Equal((3.6800m, rate.Version + 1), (corrected.Rate, corrected.Version));
        var entry = _w.Log.Store.Single(e => e.EntityId == rate.Id && e.Action == PricingChangeActions.Updated);
        var change = entry.Changes.Single(c => c.Field == "rate");
        Assert.Equal(("3.6725", "3.6800"), (change.Old, change.New));

        _w.Fx.Store.Single().TenantId = PricingWorld.OtherTenant;
        Assert.Equal(404, (await _w.Send(new UpdateFxRateCommand(rate.Id, new FxRateInput(null, null, null, null, 3.7m, null)), PricingWorld.SalesOps)).StatusCode);
    }

    [Fact]
    public async Task F4_the_import_counts_created_updated_and_unchanged_and_the_last_row_of_a_key_wins()
    {
        await _w.Send(new CreateFxRateCommand(Fx(new DateOnly(2026, 10, 1), 3.6725m)), PricingWorld.SalesOps);
        await _w.Send(new CreateFxRateCommand(Fx(new DateOnly(2026, 10, 2), 3.6725m)), PricingWorld.SalesOps);

        var result = (await _w.Send(new ImportFxRatesCommand(new FxRateImportInput([
            Fx(new DateOnly(2026, 10, 1), 3.6725m),             // unchanged
            Fx(new DateOnly(2026, 10, 2), 3.6800m),             // updated
            Fx(new DateOnly(2026, 10, 3), 3.0m),
            Fx(new DateOnly(2026, 10, 3), 3.6750m)              // the same key again: this one wins
        ])), PricingWorld.SalesOps)).Data!;

        Assert.Equal((1, 1, 1, (DateOnly?)new DateOnly(2026, 10, 1), (DateOnly?)new DateOnly(2026, 10, 3)),
            (result.Created, result.Updated, result.Unchanged, result.FromDate, result.ToDate));
        Assert.Equal(3.6750m, _w.Fx.Store.Single(r => r.RateDate == new DateOnly(2026, 10, 3)).Rate);
        Assert.Equal(3, _w.Audit.Bodies.Count);                  // two creates + ONE import
    }

    // ── the history / contract reads ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task H1_the_history_is_filtered_to_the_readable_types_and_the_contract_lists_every_reason_code()
    {
        await Active(Rule());
        await _w.Send(new CreateFxRateCommand(Fx(_w.Today, 3.6725m)), PricingWorld.SalesOps);

        var rulesOnly = (await _w.Send(new GetPricingChangeLogQuery([PricingChangeEntityTypes.PricingRule], null, null, null, null, null, null, null, null))).Data!;
        Assert.NotEmpty(rulesOnly.Items);
        Assert.All(rulesOnly.Items, e => Assert.Equal(PricingChangeEntityTypes.PricingRule, e.EntityType));

        var contract = (await _w.Send(new GetPricingContractQuery())).Data!;
        Assert.Contains(contract.ReasonCodes, c => c.Code == PricingReasonCodes.CombinedHardExceeded);
        Assert.Contains(contract.ReasonCodes, c => c.Code == PricingReasonCodes.DiscountNotAllowedQuantityMode);
    }
}

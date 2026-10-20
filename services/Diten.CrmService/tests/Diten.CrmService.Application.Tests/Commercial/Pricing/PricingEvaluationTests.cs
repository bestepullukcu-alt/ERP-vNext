using Diten.CrmService.Application.Features.Commercial.Pricing;
using Diten.CrmService.Application.Features.Commercial.Pricing.Contracts;
using Diten.CrmService.Application.Features.Commercial.Pricing.Evaluation;
using Diten.CrmService.Domain.Entities.Commercial;
using Xunit;

namespace Diten.CrmService.Application.Tests.Commercial.Pricing;

/// <summary>
/// WP-VW-W6 W6-BE-b — MOD-0156 §4.6 on the PRODUCTION evaluator (resolved from the real DI): MF 10+2 → 16.7 %, free boxes
/// floor(q / a) × b, combined 1 − (1 − i)(1 − m) rounded to 0.1 (priced mode only), soft / hard per criterion, the worst
/// finding per line and per order, the quantity mode (no list → no price, no discount; MF + quantity rules only), the most
/// specific rule (period &gt; general; segment rules ignored in the segment-less first release), the fingerprint, the totals,
/// and the mockup's demo scenarios (TR / AE / KG × ok / soft / hard). Data from the mockup (W6-on-siparis.dc.html 730–732).
/// </summary>
public sealed class PricingEvaluationTests : IDisposable
{
    private readonly PricingWorld _w = new();

    public PricingEvaluationTests()
    {
        _w.SeedPublishedList(PricingWorld.LeTr, "TR", "TRY", _w.Today.AddDays(-30), PricingWorld.TrPrices, 10m, name: "TR Resmî · Ekim");
        _w.SeedPublishedList(PricingWorld.LeAe, "AE", "USD", _w.Today.AddDays(-30), PricingWorld.AePrices, 0m, name: "AE · Ekim");
        _w.SeedDemoRules(PricingWorld.LeTr);
        _w.SeedDemoRules(PricingWorld.LeAe);
        _w.SeedDemoRules(PricingWorld.LeKg);
    }

    public void Dispose() => _w.Dispose();

    private Task<EvaluationResult> Evaluate(Guid le, string country, params EvaluationLine[] lines)
        => _w.Get<IPricingEvaluator>().EvaluateAsync(new EvaluationRequest(PricingWorld.Tenant, le, country, _w.Today, PriceTiers.PharmacistPurchase, null, lines));

    private static EvaluationLine Line(string sku, int quantity, (int, int)? mf = null, decimal? discount = null, int no = 1)
        => new(no, PricingWorld.Sku(sku), quantity, mf is { } m ? new MfInput(m.Item1, m.Item2) : null, discount);

    // ── the arithmetic (D7) ──────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task E1_MF_10_plus_2_is_16_7_percent_free_boxes_are_floor_and_the_line_is_priced_at_the_pharmacist_tier()
    {
        var result = await Evaluate(PricingWorld.LeTr, "TR", Line("TUT", 25, (10, 2)));

        var line = Assert.Single(result.Lines);
        Assert.Equal(PricingModes.Priced, result.Mode);
        Assert.Equal("TRY", result.Currency);
        Assert.Equal(16.7m, line.MfRatioPct);
        Assert.Equal(4, line.FreeQuantity);                      // floor(25 / 10) × 2 — not round(25 / 10) × 2 = 6
        Assert.Equal(412.60m, line.UnitPrice);                    // eczacı alış (D11)
        Assert.Equal(10315.00m, line.NetAmount);                  // 25 × 412.60 — the free boxes are not charged
        Assert.Equal(1031.50m, line.VatAmount);
        Assert.Equal(PricingOutcomes.Ok, line.Outcome);           // 16.7 is the soft limit itself: not above it
        Assert.Empty(line.Findings);
    }

    [Theory]
    [InlineData(10, 3, PricingReasonCodes.MfSoftExceeded, "soft", 23.1)]
    [InlineData(10, 4, PricingReasonCodes.MfHardExceeded, "hard", 28.6)]
    public async Task E2_MF_above_the_soft_ratio_is_soft_above_the_hard_ratio_is_hard_with_its_parameters(int buy, int free, string code, string outcome, double pct)
    {
        var line = Assert.Single((await Evaluate(PricingWorld.LeTr, "TR", Line("TUT", 30, (buy, free)))).Lines);

        var finding = Assert.Single(line.Findings, f => f.Code.StartsWith("mf.", StringComparison.Ordinal));
        Assert.Equal(code, finding.Code);
        Assert.Equal(outcome, line.Outcome);
        Assert.Equal($"{buy}+{free}", finding.Params["value"]);
        Assert.Equal((decimal)pct, finding.Params["valuePct"]);
        Assert.Equal(outcome == "soft" ? "10+2" : "10+3", finding.Params["limit"]);
    }

    [Theory]
    [InlineData(10, PricingOutcomes.Ok, null)]
    [InlineData(12, PricingOutcomes.Soft, PricingReasonCodes.DiscountSoftExceeded)]
    [InlineData(16, PricingOutcomes.Hard, PricingReasonCodes.DiscountHardExceeded)]
    public async Task E3_discount_above_soft_is_soft_above_hard_is_hard(double discount, string outcome, string? code)
    {
        var line = Assert.Single((await Evaluate(PricingWorld.LeTr, "TR", Line("TUT", 20, discount: (decimal)discount))).Lines);

        Assert.Equal(outcome, line.Outcome);
        Assert.Equal(code, line.Findings.Select(f => f.Code).SingleOrDefault(c => c.StartsWith("discount.", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task E4_combined_is_1_minus_the_product_rounded_to_a_tenth_and_checked_against_its_own_limits()
    {
        // discount 10 % (not above its soft 10) + MF 10+2 (16.67 %): 1 − 0.9 × 0.8333 = 25.0 % — above the soft 20, not the hard 25
        var line = Assert.Single((await Evaluate(PricingWorld.LeTr, "TR", Line("TUT", 20, (10, 2), 10m))).Lines);

        Assert.Equal(25.0m, line.CombinedDiscountPct);
        var finding = Assert.Single(line.Findings);
        Assert.Equal(PricingReasonCodes.CombinedSoftExceeded, finding.Code);
        Assert.Equal(25.0m, finding.Params["valuePct"]);
        Assert.Equal(20m, finding.Params["limitPct"]);
        Assert.Equal(PricingOutcomes.Soft, line.Outcome);
        // (net: 20 × 412.60 × 0.9 = 7426.80; the free boxes are not charged)
        Assert.Equal(7426.80m, line.NetAmount);
    }

    [Fact]
    public async Task E4b_combined_is_not_the_sum_of_the_two()
    {
        // i + m would be 5 + 16.7 = 21.7 (above URO's hard 22? no — above its soft 18); the product gives 20.8 %
        var line = Assert.Single((await Evaluate(PricingWorld.LeTr, "TR", Line("URO", 20, (10, 2), 5m))).Lines);
        Assert.Equal(20.8m, line.CombinedDiscountPct);
        Assert.Equal(PricingMath.Pct(PricingMath.Combined(0.05m, 2m / 12m)), line.CombinedDiscountPct);
    }

    [Theory]
    [InlineData(5, PricingOutcomes.Hard, PricingReasonCodes.QuantityBelowMin)]
    [InlineData(10, PricingOutcomes.Ok, null)]
    [InlineData(200, PricingOutcomes.Ok, null)]
    [InlineData(201, PricingOutcomes.Soft, PricingReasonCodes.QuantitySoftExceeded)]
    [InlineData(301, PricingOutcomes.Hard, PricingReasonCodes.QuantityHardExceeded)]
    public async Task E5_quantity_below_min_is_hard_above_soft_max_soft_above_hard_max_hard(int quantity, string outcome, string? code)
    {
        var line = Assert.Single((await Evaluate(PricingWorld.LeTr, "TR", Line("TUT", quantity))).Lines);
        Assert.Equal(outcome, line.Outcome);
        Assert.Equal(code, line.Findings.Select(f => f.Code).SingleOrDefault());
    }

    [Fact]
    public async Task E6_a_line_is_its_worst_finding_and_the_order_its_worst_line()
    {
        var result = await Evaluate(PricingWorld.LeTr, "TR",
            Line("TUT", 20, no: 1),
            Line("ALM", 120, discount: 9m, no: 2),          // quantity soft (> 100) + discount soft (> 8)
            Line("GLU", 10, (20, 3), no: 3));               // quantity below min (20): hard

        Assert.Equal(new[] { PricingOutcomes.Ok, PricingOutcomes.Soft, PricingOutcomes.Hard }, result.Lines.Select(l => l.Outcome));
        Assert.Equal(2, result.Lines[1].Findings.Count);
        Assert.Equal(PricingOutcomes.Hard, result.Outcome);
    }

    // ── the modes (K-O4, K-W10 f) ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task E7_no_list_in_force_is_the_quantity_mode_no_price_no_discount_but_MF_and_quantity_rules_still_apply()
    {
        var result = await Evaluate(PricingWorld.LeKg, "KG", Line("TUT", 20, (10, 3), 5m), Line("ALM", 3, no: 2));

        Assert.Equal(PricingModes.Quantity, result.Mode);
        Assert.Null(result.Currency);
        Assert.Null(result.Totals);
        var tut = result.Lines[0];
        Assert.Null(tut.UnitPrice);
        Assert.Null(tut.CombinedDiscountPct);
        Assert.Contains(tut.Findings, f => f.Code == PricingReasonCodes.DiscountNotAllowedQuantityMode && f.Severity == PricingFindingSeverities.Hard);
        Assert.Contains(tut.Findings, f => f.Code == PricingReasonCodes.MfSoftExceeded);           // MF still checked
        Assert.DoesNotContain(tut.Findings, f => f.Code.StartsWith("combined.", StringComparison.Ordinal) || f.Code.StartsWith("discount.") && f.Code != PricingReasonCodes.DiscountNotAllowedQuantityMode);
        Assert.Equal(PricingReasonCodes.QuantityBelowMin, Assert.Single(result.Lines[1].Findings).Code);
    }

    [Fact]
    public async Task E8_a_SKU_that_is_not_in_the_list_in_force_is_hard_with_the_list_name()
    {
        _w.Items.Store.RemoveAll(i => i.SkuId == PricingWorld.Sku("URO") && _w.Lists.Store.Any(l => l.Id == i.PriceListId && l.CountryCode == "TR"));

        var line = Assert.Single((await Evaluate(PricingWorld.LeTr, "TR", Line("URO", 20))).Lines);

        var finding = Assert.Single(line.Findings);
        Assert.Equal(PricingReasonCodes.PriceSkuNotInList, finding.Code);
        Assert.Equal("TR Resmî · Ekim", finding.Params["priceListName"]);
        Assert.Equal(PricingOutcomes.Hard, line.Outcome);
        Assert.Null(line.UnitPrice);
    }

    [Fact]
    public async Task E9_without_a_rule_there_is_no_limit()
    {
        _w.Rules.Store.Clear();
        var line = Assert.Single((await Evaluate(PricingWorld.LeTr, "TR", Line("TUT", 1000, (2, 5), 40m))).Lines);
        Assert.Equal(PricingOutcomes.Ok, line.Outcome);
        Assert.Null(line.Rule);
    }

    [Fact]
    public async Task E10_a_retired_SKU_is_hard()
    {
        _w.Items.Store.Add(new PriceListItem
        {
            TenantId = PricingWorld.Tenant, PriceListId = _w.Lists.Store.First(l => l.CountryCode == "TR").Id, SkuId = PricingWorld.Sku("AL5"),
            PharmacistPurchasePrice = 90m, VatRate = 10m
        });
        var line = Assert.Single((await Evaluate(PricingWorld.LeTr, "TR", Line("AL5", 10))).Lines);
        Assert.Contains(line.Findings, f => f.Code == PricingReasonCodes.SkuRetired && f.Severity == PricingFindingSeverities.Hard);
    }

    // ── the most specific rule (D8, §20.3 AÇIK-3 / AÇIK-4) ──────────────────────────────────────────────────────

    [Fact]
    public async Task E11_a_period_rule_beats_the_general_one_inside_its_period_and_a_segment_rule_is_ignored_in_the_first_release()
    {
        // a period rule for TUT that allows less (discount soft 5)
        var period = _w.SeedActiveRule(PricingWorld.LeTr, "TUT", [5, 8], [(10, 2), (10, 3)], [20, 25], [10, 200, 300],
            from: _w.Today.AddDays(-1), to: _w.Today.AddDays(5));
        // the mockup's "Zincir eczane" segment rule (allows MORE) — not a criterion yet
        _w.SeedActiveRule(PricingWorld.LeTr, "TUT", [12, 18], [(10, 3), (10, 4)], [25, 30], [20, 400, 500], segment: "zincir-eczane");

        var inside = await _w.Get<IPricingEvaluator>().EvaluateAsync(new EvaluationRequest(PricingWorld.Tenant, PricingWorld.LeTr, "TR", _w.Today,
            PriceTiers.PharmacistPurchase, "zincir-eczane", [Line("TUT", 20, discount: 6m)]));
        var line = Assert.Single(inside.Lines);
        Assert.Equal(period.RuleKey, line.Rule!.RuleKey);
        Assert.Equal(1, line.Rule.Specificity);
        Assert.Equal(PricingReasonCodes.DiscountSoftExceeded, Assert.Single(line.Findings).Code);

        var after = await _w.Get<IPricingEvaluator>().EvaluateAsync(new EvaluationRequest(PricingWorld.Tenant, PricingWorld.LeTr, "TR", _w.Today.AddDays(6),
            PriceTiers.PharmacistPurchase, "zincir-eczane", [Line("TUT", 20, discount: 6m)]));
        Assert.Equal(0, after.Lines[0].Rule!.Specificity);         // back to the general rule (its soft 10 allows 6)
        Assert.Equal(PricingOutcomes.Ok, after.Lines[0].Outcome);
    }

    [Fact]
    public void E12_specificity_scores_are_segment_and_period_3_segment_2_period_1_general_0()
    {
        PricingRule Rule(string? segment, DateOnly? from) => new() { SegmentCode = segment, ValidFrom = from };
        Assert.Equal(3, RuleSpecificity.Of(Rule("s", new DateOnly(2026, 1, 1))));
        Assert.Equal(2, RuleSpecificity.Of(Rule("s", null)));
        Assert.Equal(1, RuleSpecificity.Of(Rule(null, new DateOnly(2026, 1, 1))));
        Assert.Equal(0, RuleSpecificity.Of(Rule(null, null)));
    }

    // ── determinism, totals, currency ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task E13_the_same_input_on_the_same_data_gives_the_same_result_and_fingerprint_a_new_rule_version_changes_it()
    {
        var first = await Evaluate(PricingWorld.LeTr, "TR", Line("TUT", 20, (10, 2), 5m), Line("ALM", 10, no: 2));
        var second = await Evaluate(PricingWorld.LeTr, "TR", Line("TUT", 20, (10, 2), 5m), Line("ALM", 10, no: 2));
        Assert.Equal(first.RuleSetFingerprint, second.RuleSetFingerprint);
        Assert.Equal(first.Totals, second.Totals);

        var rule = _w.Rules.Store.First(r => r.LegalEntityId == PricingWorld.LeTr && r.SkuId == PricingWorld.Sku("TUT"));
        rule.RuleVersion = 2;
        Assert.NotEqual(first.RuleSetFingerprint, (await Evaluate(PricingWorld.LeTr, "TR", Line("TUT", 20, (10, 2), 5m), Line("ALM", 10, no: 2))).RuleSetFingerprint);
    }

    [Fact]
    public async Task E14_order_totals_and_the_effective_discount_count_the_free_boxes_at_list_price()
    {
        var result = await Evaluate(PricingWorld.LeTr, "TR", Line("TUT", 20, (10, 2), 5m), Line("GLU", 40, no: 2));

        // TUT: 20 × 412.60 × 0.95 = 7839.40 (+4 free); GLU: 40 × 98.75 = 3950.00
        Assert.Equal(11789.40m, result.Totals!.Net);
        Assert.Equal(1178.94m, result.Totals.Vat);
        Assert.Equal(12968.34m, result.Totals.Gross);
        // 1 − 11789.40 / (412.60 × 24 + 98.75 × 40) = 1 − 11789.40 / 13852.40 = 14.9 %
        Assert.Equal(14.9m, result.Totals.EffectiveDiscountPct);
    }

    [Fact]
    public async Task E15_money_rounds_to_the_currency_minor_unit_away_from_zero_per_line()
    {
        // AE / USD, VAT 0: 3 × 3.40 × (1 − 0.075) = 9.435 → 9.44
        var line = Assert.Single((await Evaluate(PricingWorld.LeAe, "AE", Line("GLU", 3, discount: 7.5m))).Lines);
        Assert.Equal(9.44m, line.NetAmount);
        Assert.Equal(0m, line.VatAmount);
        Assert.Equal(0, PricingMath.MinorUnits("JPY"));
        Assert.Equal(3, PricingMath.MinorUnits("KWD"));
    }

    [Fact]
    public async Task E16_another_tenants_list_and_rules_are_invisible()
    {
        _w.SeedPublishedList(PricingWorld.LeKg, "KG", "USD", _w.Today.AddDays(-5), PricingWorld.AePrices, 12m, tenant: PricingWorld.OtherTenant);
        _w.Rules.Store.Where(r => r.LegalEntityId == PricingWorld.LeKg).ToList().ForEach(r => r.TenantId = PricingWorld.OtherTenant);

        var result = await Evaluate(PricingWorld.LeKg, "KG", Line("TUT", 1, discount: 50m));
        Assert.Equal(PricingModes.Quantity, result.Mode);       // the other tenant's KG list is not ours
        Assert.Null(result.Lines[0].Rule);                       // neither are its rules
    }

    // ── the mockup's demo scenarios (AC-RULE-4): TR / AE / KG × ok / soft / hard ────────────────────────────────

    [Theory]
    [InlineData("TR", "TUT", 20, 10, 2, 0, PricingOutcomes.Ok)]
    [InlineData("TR", "TUT", 20, 10, 2, 10, PricingOutcomes.Soft)]   // combined 25.0 > 20
    [InlineData("TR", "ALM", 20, 10, 3, 0, PricingOutcomes.Hard)]   // MF 23.1 > hard 16.7 (10+2)
    [InlineData("AE", "GLU", 100, 20, 3, 4, PricingOutcomes.Soft)]  // combined 1 − 0.96 × 20/23 = 16.5 > soft 15
    [InlineData("AE", "URO", 300, 0, 0, 0, PricingOutcomes.Hard)]   // quantity 300 > hard 250
    [InlineData("KG", "TUT", 250, 10, 2, 0, PricingOutcomes.Soft)]  // quantity mode: 250 > soft 200
    [InlineData("KG", "GLU", 30, 0, 0, 3, PricingOutcomes.Hard)]    // quantity mode: no discount
    public async Task E17_the_mockup_demo_scenarios_give_the_mockups_outcome(string country, string sku, int quantity, int buy, int free, int discount, string expected)
    {
        var le = country switch { "TR" => PricingWorld.LeTr, "AE" => PricingWorld.LeAe, _ => PricingWorld.LeKg };
        var line = Assert.Single((await Evaluate(le, country, Line(sku, quantity, buy > 0 ? (buy, free) : null, discount > 0 ? discount : null))).Lines);

        Assert.Equal(expected, line.Outcome);
    }
}

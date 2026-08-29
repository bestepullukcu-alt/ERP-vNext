using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.StrategyTemplate;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Application.Features.StrategyTemplate.Handlers.CommandHandlers;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.StrategyTemplate;

/// <summary>
/// MOD-0167 FU04 — the percentage arithmetic. The rule under test is exactness: a sku-allocated line totals 100.00 or
/// the write is refused with the computed total shown. Nothing is normalised, nothing is rounded into place, and no
/// tolerance band exists.
/// </summary>
public sealed class StrategyTemplateAllocationRulesTests
{
    private readonly FakeStrategyTemplateRepository _templates = new();
    private readonly FakeSegmentReadRepository _segments = new();
    private readonly FakeVisitFrequencyPolicyRepository _policies = new();
    private readonly FakeKnowledgePathRepository _paths = new();
    private readonly FakeContentEngagementJourneyRepository _journeys = new();
    private readonly FakeStrategyReferenceValidator _references = new();

    private CreateStrategyTemplateHandler Create() => new(
        StrategyTemplateTestDoubles.Tenant(StrategyTemplateTestDoubles.TenantA),
        new NullActorContext(), _templates,
        new StrategyTemplateBindingValidator(_segments, _policies, _paths, _journeys),
        _references);

    private Guid Segment() => _segments.Add(StrategyTemplateTestDoubles.TenantA).Id;

    private async Task<Common.Models.Response<Guid>> CreateWith(
        params StrategyTemplateProductLineInput[] lines)
        => await Create().Handle(
            StrategyTemplateTestBuilders.NewTemplate(Segment(), productLines: lines), default);

    [Fact]
    public async Task A_line_totalling_exactly_100_is_accepted_and_stored_as_authored()
    {
        var response = await CreateWith(StrategyTemplateTestBuilders.SkuAllocated(
            Guid.NewGuid(),
            new[] { (Guid.NewGuid(), 33.33m), (Guid.NewGuid(), 33.33m), (Guid.NewGuid(), 33.34m) }));

        Assert.True(response.IsSuccessful);
        var line = _templates.Rows.Single().ProductLines.Single();
        Assert.Equal(100.00m, StrategyTemplateAllocationRules.TotalOf(line));
        // Stored exactly as sent: no redistribution, no rounding of the odd cent onto the last row.
        Assert.Equal(new[] { 33.33m, 33.33m, 33.34m }, line.SkuAllocations.Select(a => a.Percentage).ToArray());
    }

    [Theory]
    [InlineData("99.99")]
    [InlineData("100.01")]
    public async Task A_line_that_misses_100_is_a_400_showing_the_computed_total(string totalLiteral)
    {
        // The literal is parsed as decimal on purpose: routing it through double would introduce the very rounding
        // error this rule exists to refuse.
        var total = decimal.Parse(totalLiteral, System.Globalization.CultureInfo.InvariantCulture);

        // Two rows, each individually legal, so the ONLY thing wrong is the total — which is exactly the failure this
        // rule is about (a single out-of-range share is a different, per-row refusal).
        var response = await CreateWith(StrategyTemplateTestBuilders.SkuAllocated(
            Guid.NewGuid(), new[] { (Guid.NewGuid(), 50m), (Guid.NewGuid(), total - 50m) }));

        Assert.Equal(400, response.StatusCode);
        Assert.Contains(StrategyTemplateErrorCodes.SkuAllocationTotalInvalid, response.Errors!);
        // The author can see their own arithmetic in the message.
        Assert.Contains(response.Errors!, e => e.Contains(total.ToString("0.##"), StringComparison.Ordinal));
        Assert.Equal(0, _templates.InsertCalls);
    }

    [Fact]
    public async Task A_single_allocation_of_100_is_valid()
    {
        var response = await CreateWith(StrategyTemplateTestBuilders.SkuAllocated(
            Guid.NewGuid(), new[] { (Guid.NewGuid(), 100.00m) }));

        Assert.True(response.IsSuccessful);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(100.5)]
    public async Task A_percentage_outside_the_open_range_is_refused(double percentageAsDouble)
    {
        var response = await CreateWith(StrategyTemplateTestBuilders.SkuAllocated(
            Guid.NewGuid(),
            new[] { (Guid.NewGuid(), (decimal)percentageAsDouble), (Guid.NewGuid(), 50m) }));

        Assert.Equal(400, response.StatusCode);
        Assert.Contains(StrategyTemplateErrorCodes.SkuAllocationTotalInvalid, response.Errors!);
    }

    [Fact]
    public async Task More_than_two_decimals_is_refused()
    {
        var response = await CreateWith(StrategyTemplateTestBuilders.SkuAllocated(
            Guid.NewGuid(), new[] { (Guid.NewGuid(), 33.333m), (Guid.NewGuid(), 66.667m) }));

        Assert.Equal(400, response.StatusCode);
        Assert.Contains(StrategyTemplateErrorCodes.SkuAllocationTotalInvalid, response.Errors!);
    }

    [Fact]
    public async Task A_product_only_line_may_not_carry_allocations()
    {
        var line = StrategyTemplateTestBuilders.SkuAllocated(
            Guid.NewGuid(), new[] { (Guid.NewGuid(), 100m) }) with
        {
            SkuAllocationMode = StrategySkuAllocationModes.ProductOnly
        };

        var response = await CreateWith(line);

        Assert.Equal(400, response.StatusCode);
        Assert.Contains(StrategyTemplateErrorCodes.SkuAllocationModeMismatch, response.Errors!);
    }

    [Fact]
    public async Task A_sku_allocated_line_with_no_allocation_is_refused()
    {
        var line = StrategyTemplateTestBuilders.ProductOnly(Guid.NewGuid()) with
        {
            SkuAllocationMode = StrategySkuAllocationModes.SkuAllocated
        };

        var response = await CreateWith(line);

        Assert.Equal(400, response.StatusCode);
        Assert.Contains(StrategyTemplateErrorCodes.SkuAllocationModeMismatch, response.Errors!);
    }

    [Fact]
    public async Task A_product_only_line_is_accepted_and_has_no_total_to_meet()
    {
        var response = await CreateWith(StrategyTemplateTestBuilders.ProductOnly(Guid.NewGuid()));

        Assert.True(response.IsSuccessful);
        var line = _templates.Rows.Single().ProductLines.Single();
        Assert.Empty(line.SkuAllocations);
        Assert.Equal(0m, StrategyTemplateAllocationRules.TotalOf(line));
    }

    [Fact]
    public async Task The_same_sku_cannot_appear_twice_on_one_line()
    {
        var gsku = Guid.NewGuid();

        var response = await CreateWith(StrategyTemplateTestBuilders.SkuAllocated(
            Guid.NewGuid(), new[] { (gsku, 50m), (gsku, 50m) }));

        Assert.Equal(400, response.StatusCode);
        Assert.Contains(StrategyTemplateErrorCodes.SkuAllocationDuplicate, response.Errors!);
    }

    [Fact]
    public async Task The_same_global_product_cannot_appear_on_two_lines()
    {
        var product = Guid.NewGuid();

        var response = await CreateWith(
            StrategyTemplateTestBuilders.ProductOnly(product, 10),
            StrategyTemplateTestBuilders.ProductOnly(product, 20));

        Assert.Equal(400, response.StatusCode);
        Assert.Contains(StrategyTemplateErrorCodes.ProductLineDuplicate, response.Errors!);
    }

    [Fact]
    public async Task Line_weights_are_all_or_nothing()
    {
        var response = await CreateWith(
            StrategyTemplateTestBuilders.ProductOnly(Guid.NewGuid(), 10) with { LineWeightPercentage = 60m },
            StrategyTemplateTestBuilders.ProductOnly(Guid.NewGuid(), 20));

        Assert.Equal(400, response.StatusCode);
        Assert.Contains(StrategyTemplateErrorCodes.LineWeightPartiallySpecified, response.Errors!);
    }

    [Fact]
    public async Task Fully_specified_line_weights_must_total_100()
    {
        var wrong = await CreateWith(
            StrategyTemplateTestBuilders.ProductOnly(Guid.NewGuid(), 10) with { LineWeightPercentage = 60m },
            StrategyTemplateTestBuilders.ProductOnly(Guid.NewGuid(), 20) with { LineWeightPercentage = 30m });

        Assert.Equal(400, wrong.StatusCode);
        Assert.Contains(StrategyTemplateErrorCodes.LineWeightTotalInvalid, wrong.Errors!);
    }

    [Fact]
    public async Task Fully_specified_line_weights_totalling_100_are_accepted()
    {
        var response = await CreateWith(
            StrategyTemplateTestBuilders.ProductOnly(Guid.NewGuid(), 10) with { LineWeightPercentage = 60m },
            StrategyTemplateTestBuilders.ProductOnly(Guid.NewGuid(), 20) with { LineWeightPercentage = 40m });

        Assert.True(response.IsSuccessful);
    }

    [Fact]
    public void The_allocation_arithmetic_is_decimal_only()
    {
        // A double here would silently turn "exactly 100" into "about 100", which is a tolerance decision taken by
        // accident. The signature is the guard.
        var totalOf = typeof(StrategyTemplateAllocationRules).GetMethod(
            nameof(StrategyTemplateAllocationRules.TotalOf));

        Assert.NotNull(totalOf);
        Assert.Equal(typeof(decimal), totalOf!.ReturnType);
        Assert.Equal(typeof(decimal), typeof(StrategyTemplateSkuAllocation)
            .GetProperty(nameof(StrategyTemplateSkuAllocation.Percentage))!.PropertyType);
    }
}

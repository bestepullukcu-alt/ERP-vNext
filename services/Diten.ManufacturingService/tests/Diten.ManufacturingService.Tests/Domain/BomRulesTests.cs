using Diten.ManufacturingService.Domain.Entities;
using Diten.ManufacturingService.Domain.Rules;
using Xunit;

namespace Diten.ManufacturingService.Tests.Domain;

/// <summary>Production rules (BomDecimal / BomRules), measured directly — no copy of the arithmetic lives here.</summary>
public sealed class BomRulesTests
{
    [Theory]
    [InlineData("100.000", "2.000", "200.000")]   // the frozen contract's own example
    [InlineData("100.000", "0.500", "50.000")]
    [InlineData("3", "0.25", "0.75")]
    [InlineData("0.333", "0.333", "0.110889")]    // rounding to scale 3 would lose value → full scale kept
    [InlineData("1", "0.000000000000000001", "0.000000000000000001")]
    public void Multiply_keeps_the_larger_input_scale_and_never_drops_value(string left, string right, string expected) =>
        Assert.Equal(expected, BomDecimal.Multiply(left, right));

    [Theory]
    [InlineData("1", true)]
    [InlineData("0.5", true)]
    [InlineData("0", false)]
    [InlineData("0.000", false)]
    [InlineData("-1", false)]
    [InlineData("1e3", false)]
    [InlineData("1,5", false)]
    [InlineData(" 1", false)]
    [InlineData(null, false)]
    public void IsPositive_accepts_only_contract_decimal_strings_above_zero(string? text, bool expected) =>
        Assert.Equal(expected, BomDecimal.IsPositive(text));

    [Fact]
    public void Explode_multiplies_each_line_and_merges_the_same_component_and_uom()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var bom = new BomVersion
        {
            Components =
            [
                new BomComponentLine { ComponentItemId = b, Quantity = "0.500", UomId = "L", Position = 20 },
                new BomComponentLine { ComponentItemId = a, Quantity = "2.000", UomId = "EA", Position = 10 },
                new BomComponentLine { ComponentItemId = a, Quantity = "1.000", UomId = "EA", Position = 30 },
                new BomComponentLine { ComponentItemId = a, Quantity = "1", UomId = "KG", Position = 40 }
            ]
        };

        var result = BomRules.Explode(bom, "100.000");

        Assert.Equal(
            new[] { (a, "300.000", "EA"), (b, "50.000", "L"), (a, "100.000", "KG") },
            result.Select(r => (r.ComponentItemId, r.RequiredQuantity, r.UomId)).ToArray());
    }

    [Fact]
    public async Task Cycle_is_found_through_effective_boms_two_levels_down()
    {
        var parent = Guid.NewGuid();
        var middle = Guid.NewGuid();
        var leaf = Guid.NewGuid();
        var graph = new Dictionary<Guid, IReadOnlyList<Guid>> { [middle] = [leaf], [leaf] = [parent] };

        Assert.True(await BomRules.WouldCreateCycleAsync(parent, [middle], Lookup(graph)));
    }

    [Fact]
    public async Task Control_an_acyclic_structure_is_not_called_a_cycle()
    {
        var parent = Guid.NewGuid();
        var middle = Guid.NewGuid();
        var leaf = Guid.NewGuid();
        var graph = new Dictionary<Guid, IReadOnlyList<Guid>> { [middle] = [leaf], [leaf] = [] };

        Assert.False(await BomRules.WouldCreateCycleAsync(parent, [middle], Lookup(graph)));
    }

    [Fact]
    public async Task A_direct_self_reference_is_a_cycle()
    {
        var parent = Guid.NewGuid();
        Assert.True(await BomRules.WouldCreateCycleAsync(parent, [parent], Lookup([])));
    }

    [Fact]
    public void Effective_window_is_half_open()
    {
        var from = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(5);
        var bom = new BomVersion { Status = BomStatus.Superseded, EffectiveFrom = from, EffectiveTo = to };

        Assert.True(bom.IsEffectiveAt(from));
        Assert.True(bom.IsEffectiveAt(to.AddTicks(-1)));
        Assert.False(bom.IsEffectiveAt(to));
        Assert.False(bom.IsEffectiveAt(from.AddTicks(-1)));
        Assert.False(new BomVersion { Status = BomStatus.Draft, EffectiveFrom = from }.IsEffectiveAt(to));
    }

    [Fact]
    public void AsOfDate_is_the_end_of_that_utc_day_and_absent_means_now()
    {
        var now = new DateTimeOffset(2026, 10, 6, 9, 0, 0, TimeSpan.Zero);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 23, 59, 59, TimeSpan.Zero).AddTicks(9_999_999), BomRules.ResolveAsOf(new DateOnly(2026, 10, 1), now));
        Assert.Equal(now, BomRules.ResolveAsOf(null, now));
    }

    private static Func<IReadOnlyCollection<Guid>, Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>>> Lookup(Dictionary<Guid, IReadOnlyList<Guid>> graph) =>
        items => Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>>(
            items.Where(graph.ContainsKey).ToDictionary(i => i, i => graph[i]));
}

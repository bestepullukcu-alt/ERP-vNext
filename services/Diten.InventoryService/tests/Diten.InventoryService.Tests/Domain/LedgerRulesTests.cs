using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;
using Xunit;

namespace Diten.InventoryService.Tests.Domain;

/// <summary>Bucket rule, decimal handling, UoM conversion, reversal derivation, legs (pack §17 domain unit tests).</summary>
public sealed class LedgerRulesTests
{
    [Theory]
    [InlineData(StockStatus.Available, 100, 30, 70, 0, 0, 0)]
    [InlineData(StockStatus.QualityInspection, 100, 0, 0, 100, 0, 0)]
    [InlineData(StockStatus.Quarantine, 100, 0, 0, 0, 100, 0)]
    [InlineData(StockStatus.Blocked, 100, 0, 0, 0, 100, 0)]
    [InlineData(StockStatus.Damaged, 100, 0, 0, 0, 100, 0)]
    [InlineData(StockStatus.Expired, 100, 0, 0, 0, 100, 0)]
    [InlineData(StockStatus.InTransit, 100, 0, 0, 0, 0, 100)]
    public void Bucket_rule_of_pack_section_4(StockStatus status, int onHand, int reserved, int available, int qi, int blocked, int transit)
        => Assert.Equal(((decimal)available, (decimal)qi, (decimal)blocked, (decimal)transit),
            BalanceBuckets.Derive(status, onHand, reserved));

    [Theory]
    [InlineData("100", 100)]
    [InlineData("0.5", 0.5)]
    [InlineData("0.000001", 0.000001)]
    [InlineData("12345678901234567890", 12345678901234567890)]
    public void Positive_decimal_strings_parse_exactly(string raw, decimal expected)
    {
        Assert.True(InventoryQuantity.TryParsePositive(raw, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("0.000")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("1e2")]
    [InlineData("1.")]
    [InlineData(".5")]
    [InlineData("1.0000001")]
    [InlineData("1 000")]
    [InlineData("NaN")]
    public void Anything_else_is_not_a_posted_quantity(string? raw)
        => Assert.False(InventoryQuantity.TryParsePositive(raw, out _));

    [Theory]
    [InlineData(100, "100.000")]
    [InlineData(0, "0.000")]
    [InlineData(0.5, "0.500")]
    [InlineData(1.123456, "1.123456")]
    [InlineData(80.0000, "80.000")]
    public void Wire_format_has_at_least_three_fraction_digits_and_loses_nothing(decimal value, string wire)
        => Assert.Equal(wire, InventoryQuantity.ToWire(value));

    [Fact]
    public void Uom_conversion_follows_declared_edges_and_chains_only()
    {
        UomConversionRule[] rules = [new("BOX", "EA", 10m, 1m), new("PALLET", "BOX", 100m, 1m), new("PACK", "EA", 1m, 3m)];
        var now = DateTimeOffset.UtcNow;

        Assert.True(UomConverter.TryConvertToBase(100m, "BOX", "EA", rules, now, out var box));
        Assert.Equal(1000m, box);
        Assert.True(UomConverter.TryConvertToBase(2m, "PALLET", "EA", rules, now, out var pallet));
        Assert.Equal(2000m, pallet);
        Assert.True(UomConverter.TryConvertToBase(5m, "EA", "EA", rules, now, out var same));
        Assert.Equal(5m, same);

        Assert.False(UomConverter.TryConvertToBase(1m, "CASE", "EA", rules, now, out _));
        Assert.False(UomConverter.TryConvertToBase(1m, "EA", "BOX", rules, now, out _)); // no inverse edge
        Assert.False(UomConverter.TryConvertToBase(1m, "PACK", "EA", rules, now, out _)); // 0.333… needs > 6 digits
        Assert.True(UomConverter.TryConvertToBase(3m, "PACK", "EA", rules, now, out var pack));
        Assert.Equal(1m, pack);
    }

    [Fact]
    public void Uom_conversion_respects_effective_dates()
    {
        var now = new DateTimeOffset(2026, 9, 30, 0, 0, 0, TimeSpan.Zero);
        UomConversionRule[] rules = [new("BOX", "EA", 12m, 1m, EffectiveFrom: now.AddDays(1))];

        Assert.False(UomConverter.TryConvertToBase(1m, "BOX", "EA", rules, now, out _));
        Assert.True(UomConverter.TryConvertToBase(1m, "BOX", "EA", rules, now.AddDays(2), out _));
    }

    [Fact]
    public void A_transfer_has_a_decrease_leg_first_and_snapshots_the_target()
    {
        var transfer = Transaction(MovementType.LocationTransfer, from: ("wh", "a"), to: ("wh", "b"));

        var legs = LedgerLegs.For(transfer);

        Assert.Equal(2, legs.Count);
        Assert.Equal(-5m, legs[0].Delta);
        Assert.Equal("a", legs[0].Dimension.LocationId);
        Assert.Equal(5m, legs[1].Delta);
        Assert.Same(legs[1], LedgerLegs.SnapshotLeg(legs));
    }

    [Fact]
    public void An_outbound_snapshots_its_source()
    {
        var legs = LedgerLegs.For(Transaction(MovementType.Scrap, from: ("wh", "a")));

        Assert.Same(Assert.Single(legs), LedgerLegs.SnapshotLeg(legs));
    }

    [Fact]
    public void Reversal_envelope_and_match_rules()
    {
        var id = Guid.NewGuid();
        Assert.Equal((id, null), ReversalRules.ReadOriginalReference("MOD-0173", "REVERSAL", id.ToString()));
        Assert.NotNull(ReversalRules.ReadOriginalReference("MOD-0142", "REVERSAL", id.ToString()).Error);
        Assert.NotNull(ReversalRules.ReadOriginalReference("MOD-0173", "REVERSAL", "txn-1").Error);

        var original = Transaction(MovementType.OpeningBalance, to: ("wh", "a"));
        Assert.Null(ReversalRules.MatchOriginal(original, original.ItemId, original.SkuId, original.SkuLevel, 5m, "EA"));
        Assert.NotNull(ReversalRules.MatchOriginal(original, original.ItemId, original.SkuId, original.SkuLevel, 4m, "EA"));
        Assert.NotNull(ReversalRules.MatchOriginal(original, Guid.NewGuid(), original.SkuId, original.SkuLevel, 5m, "EA"));

        original.MovementType = MovementType.Reversal;
        Assert.NotNull(ReversalRules.MatchOriginal(original, original.ItemId, original.SkuId, original.SkuLevel, 5m, "EA"));
    }

    [Fact]
    public void Wire_names_round_trip_and_are_case_sensitive()
    {
        foreach (var type in Enum.GetValues<MovementType>())
        {
            Assert.True(WireNames.TryParseMovementType(WireNames.ToWire(type), out var parsed));
            Assert.Equal(type, parsed);
        }

        Assert.Equal("GOODS_ISSUE_SALES", WireNames.ToWire(MovementType.GoodsIssueSales));
        Assert.False(WireNames.TryParseMovementType("goods_issue_sales", out _));
        Assert.Equal(7, WireNames.AllStockStatuses.Count);
    }

    private static InventoryTransaction Transaction(MovementType type, (string Wh, string Loc)? from = null, (string Wh, string Loc)? to = null) => new()
    {
        MovementType = type,
        ItemId = Guid.NewGuid(),
        SkuId = Guid.NewGuid(),
        SkuLevel = SkuLevel.Lsku,
        FromWarehouseId = from?.Wh,
        FromLocationId = from?.Loc,
        FromStockStatus = from is null ? null : StockStatus.Available,
        ToWarehouseId = to?.Wh,
        ToLocationId = to?.Loc,
        ToStockStatus = to is null ? null : StockStatus.Available,
        Quantity = 5m,
        UomId = "EA",
        BaseQuantity = 5m,
        BaseUomId = "EA"
    };
}

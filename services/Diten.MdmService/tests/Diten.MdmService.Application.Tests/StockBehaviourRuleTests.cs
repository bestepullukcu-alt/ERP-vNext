using Diten.MdmService.Domain.ValueObjects;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>WP-MDM-ITEM-MASTER (MOD-0290-FU04) S3 — the record-level stock-behaviour rules (pack §4.2, §4.4, §12).</summary>
public sealed class StockBehaviourRuleTests
{
    private static readonly IReadOnlySet<string> Uoms =
        new HashSet<string>(StringComparer.Ordinal) { "C62", "MLT", "XBX", "XCS", "XPA", "XPL", "KGM" };

    private static readonly IReadOnlySet<string> StorageCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        "AMBIENT_15_25", "COOL_2_8", "FROZEN_BELOW_MINUS_20", "PROTECT_FROM_LIGHT", "PROTECT_FROM_MOISTURE"
    };

    private static readonly IReadOnlySet<string> Temperatures =
        new HashSet<string>(StringComparer.Ordinal) { "AMBIENT_15_25", "COOL_2_8", "FROZEN_BELOW_MINUS_20" };

    private static ItemUomConversion Edge(string from, string to, string numerator = "10", string denominator = "1")
        => new() { FromUom = from, ToUom = to, Numerator = numerator, Denominator = denominator };

    [Theory]
    [InlineData("1", true)]
    [InlineData("0.5", true)]
    [InlineData("100", true)]
    [InlineData("2.000", true)]
    [InlineData("0", false)]
    [InlineData("0.0", false)]
    [InlineData("-1", false)]
    [InlineData("+1", false)]
    [InlineData("1e3", false)]
    [InlineData("01", false)]
    [InlineData("1,5", false)]
    [InlineData(" 1", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Numerator_and_denominator_are_positive_exact_decimal_text(string? text, bool expected)
        => Assert.Equal(expected, StockBehaviourRules.IsPositiveExactDecimal(text));

    [Fact]
    public void A_chain_box_to_pack_and_case_to_box_within_four_edges_is_valid()
    {
        ItemUomConversion[] conversions = [Edge("XBX", "C62", "30"), Edge("XCS", "XBX", "12"), Edge("XPL", "XCS", "40")];
        Assert.Null(StockBehaviourRules.ValidateConversions("C62", conversions, Uoms));
    }

    [Fact]
    public void Conversion_numerator_zero_is_rejected()
        => Assert.Equal(StockBehaviourRules.ConversionInvalid,
            StockBehaviourRules.ValidateConversions("C62", [Edge("XBX", "C62", "0")], Uoms));

    [Fact]
    public void Conversion_denominator_zero_is_rejected()
        => Assert.Equal(StockBehaviourRules.ConversionInvalid,
            StockBehaviourRules.ValidateConversions("C62", [Edge("XBX", "C62", "30", "0")], Uoms));

    [Fact]
    public void A_unit_outside_the_item_uom_list_is_rejected_by_name()
    {
        Assert.Equal(StockBehaviourRules.UomInvalid,
            StockBehaviourRules.ValidateConversions("C62", [Edge("XBX", "PCE")], Uoms));
        Assert.Equal(StockBehaviourRules.UomInvalid,
            StockBehaviourRules.ValidateConversions("PCE", [], Uoms));
    }

    [Fact]
    public void The_same_pair_twice_in_either_direction_is_rejected()
    {
        Assert.Equal(StockBehaviourRules.ConversionInvalid,
            StockBehaviourRules.ValidateConversions("C62", [Edge("XBX", "C62"), Edge("XBX", "C62", "20")], Uoms));
        Assert.Equal(StockBehaviourRules.ConversionInvalid,
            StockBehaviourRules.ValidateConversions("C62", [Edge("XBX", "C62"), Edge("C62", "XBX", "1", "10")], Uoms));
    }

    [Fact]
    public void A_unit_converted_to_itself_is_rejected()
        => Assert.Equal(StockBehaviourRules.ConversionInvalid,
            StockBehaviourRules.ValidateConversions("C62", [Edge("C62", "C62")], Uoms));

    [Fact]
    public void A_unit_five_edges_from_the_base_is_rejected_and_four_is_accepted()
    {
        ItemUomConversion[] four = [Edge("XBX", "C62"), Edge("XPA", "XBX"), Edge("XCS", "XPA"), Edge("XPL", "XCS")];
        Assert.Null(StockBehaviourRules.ValidateConversions("C62", four, Uoms));
        ItemUomConversion[] five = [.. four, Edge("KGM", "XPL")];
        Assert.Equal(StockBehaviourRules.ConversionInvalid, StockBehaviourRules.ValidateConversions("C62", five, Uoms));
    }

    [Fact]
    public void An_island_not_connected_to_the_base_unit_is_rejected()
        => Assert.Equal(StockBehaviourRules.ConversionInvalid,
            StockBehaviourRules.ValidateConversions("C62", [Edge("XCS", "XBX")], Uoms));

    [Fact]
    public void The_pack_conversion_is_derived_from_pack_quantity_and_pack_unit_whatever_the_unit()
    {
        var millilitres = StockBehaviourRules.DerivedPackConversion(100m, "MLT");
        Assert.Equal(("XBX", "MLT", "100", "1"),
            (millilitres.FromUom, millilitres.ToUom, millilitres.Numerator, millilitres.Denominator));
        var tablets = StockBehaviourRules.DerivedPackConversion(30.00m, "C62");
        Assert.Equal("30", tablets.Numerator);
        Assert.Equal("2.5", StockBehaviourRules.DerivedPackConversion(2.5m, "MLT").Numerator);
        Assert.True(StockBehaviourRules.IsPositiveExactDecimal(StockBehaviourRules.DerivedPackConversion(0.25m, "KGM").Numerator));
    }

    [Theory]
    [InlineData("00012345678905", true)]
    [InlineData("04006381333931", true)]
    [InlineData("04006381333932", false)]
    [InlineData("18690000000017", false)]
    [InlineData("4006381333931", false)]
    [InlineData("040063813339310", false)]
    [InlineData("0400638133393A", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Sales_box_gtin_is_fourteen_digits_with_a_gs1_mod10_check_digit(string? gtin, bool expected)
        => Assert.Equal(expected, StockBehaviourRules.IsValidGtin14(gtin));

    [Fact]
    public void Storage_allows_one_temperature_condition_plus_other_conditions()
    {
        Assert.Null(StockBehaviourRules.ValidateStorageConditions(["COOL_2_8", "PROTECT_FROM_LIGHT"], StorageCodes, Temperatures));
        Assert.Equal(StockBehaviourRules.StorageConditionConflict,
            StockBehaviourRules.ValidateStorageConditions(["COOL_2_8", "AMBIENT_15_25"], StorageCodes, Temperatures));
    }

    [Fact]
    public void Storage_rejects_unknown_repeated_or_empty_codes()
    {
        Assert.Equal(StockBehaviourRules.StorageConditionInvalid,
            StockBehaviourRules.ValidateStorageConditions(["HOT"], StorageCodes, Temperatures));
        Assert.Equal(StockBehaviourRules.StorageConditionInvalid,
            StockBehaviourRules.ValidateStorageConditions(["PROTECT_FROM_MOISTURE", "PROTECT_FROM_MOISTURE"], StorageCodes, Temperatures));
        Assert.Equal(StockBehaviourRules.StorageConditionInvalid,
            StockBehaviourRules.ValidateStorageConditions([], StorageCodes, Temperatures));
    }

    [Fact]
    public void Gsku_is_complete_only_with_base_unit_shelf_life_storage_and_lot_control()
    {
        Assert.True(StockBehaviourRules.GskuIsComplete("C62", 730, ["COOL_2_8"], false));
        Assert.False(StockBehaviourRules.GskuIsComplete(null, 730, ["COOL_2_8"], false));
        Assert.False(StockBehaviourRules.GskuIsComplete("C62", null, ["COOL_2_8"], false));
        Assert.False(StockBehaviourRules.GskuIsComplete("C62", 730, [], false));
        Assert.False(StockBehaviourRules.GskuIsComplete("C62", 730, null, false));
        Assert.False(StockBehaviourRules.GskuIsComplete("C62", 730, ["COOL_2_8"], null));
    }

    [Fact]
    public void Finished_good_is_complete_only_with_case_to_box_storage_lot_serial_and_gtin()
    {
        ItemUomConversion[] caseToBox = [Edge("XCS", "XBX", "12")];
        Assert.True(StockBehaviourRules.FinishedGoodIsComplete("C62", 730, caseToBox, ["COOL_2_8"], true, true, "04006381333931"));
        // Box-to-case in the other direction counts as the same pair.
        Assert.True(StockBehaviourRules.FinishedGoodIsComplete("C62", 730, [Edge("XBX", "XCS", "1", "12")], ["COOL_2_8"], true, false, "04006381333931"));
        Assert.False(StockBehaviourRules.FinishedGoodIsComplete("C62", 730, [Edge("XBX", "C62")], ["COOL_2_8"], true, true, "04006381333931"));
        Assert.False(StockBehaviourRules.FinishedGoodIsComplete("C62", null, caseToBox, ["COOL_2_8"], true, true, "04006381333931"));
        Assert.False(StockBehaviourRules.FinishedGoodIsComplete("C62", 730, caseToBox, [], true, true, "04006381333931"));
        Assert.False(StockBehaviourRules.FinishedGoodIsComplete("C62", 730, caseToBox, ["COOL_2_8"], null, true, "04006381333931"));
        Assert.False(StockBehaviourRules.FinishedGoodIsComplete("C62", 730, caseToBox, ["COOL_2_8"], true, null, "04006381333931"));
        Assert.False(StockBehaviourRules.FinishedGoodIsComplete("C62", 730, caseToBox, ["COOL_2_8"], true, true, null));
        Assert.False(StockBehaviourRules.FinishedGoodIsComplete(null, 730, caseToBox, ["COOL_2_8"], true, true, "04006381333931"));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, null, false)]
    [InlineData(false, true, false)]
    [InlineData(null, true, false)]
    public void Lsku_inherits_stockable_from_its_gsku_and_needs_its_own_serial_control(
        bool? gskuStockable, bool? lskuSerial, bool expected)
        => Assert.Equal(expected, StockBehaviourRules.LskuEffectiveStockable(gskuStockable, lskuSerial));

    [Theory]
    [InlineData(null, true)]
    [InlineData(1, true)]
    [InlineData(3650, true)]
    [InlineData(0, false)]
    [InlineData(-5, false)]
    [InlineData(3651, false)]
    public void Shelf_life_is_whole_days_between_one_and_ten_years(int? days, bool expected)
        => Assert.Equal(expected, StockBehaviourRules.IsValidShelfLife(days));

    [Theory]
    [InlineData(720, 730, true)]
    [InlineData(730, 730, true)]
    [InlineData(731, 730, false)]
    [InlineData(null, 730, true)]
    [InlineData(720, null, true)]
    public void Finished_good_shelf_life_never_exceeds_the_gsku_shelf_life(int? finishedGood, int? gsku, bool expected)
        => Assert.Equal(expected, StockBehaviourRules.FinishedGoodShelfLifeWithinGsku(finishedGood, gsku));
}

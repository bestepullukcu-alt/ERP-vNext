using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;
using Xunit;

namespace Diten.InventoryService.Tests.Domain;

/// <summary>Pack §12 direction matrix — every row, both accept and refuse.</summary>
public sealed class MovementDirectionRulesTests
{
    [Theory]
    [InlineData(MovementType.OpeningBalance, MovementClass.Inbound)]
    [InlineData(MovementType.GoodsReceiptPo, MovementClass.Inbound)]
    [InlineData(MovementType.GoodsReceiptProd, MovementClass.Inbound)]
    [InlineData(MovementType.CustomerReturn, MovementClass.Inbound)]
    [InlineData(MovementType.CountGain, MovementClass.Inbound)]
    [InlineData(MovementType.GoodsIssueSales, MovementClass.Outbound)]
    [InlineData(MovementType.GoodsIssueProd, MovementClass.Outbound)]
    [InlineData(MovementType.SupplierReturn, MovementClass.Outbound)]
    [InlineData(MovementType.CountLoss, MovementClass.Outbound)]
    [InlineData(MovementType.Scrap, MovementClass.Outbound)]
    [InlineData(MovementType.WriteOff, MovementClass.Outbound)]
    [InlineData(MovementType.WarehouseTransfer, MovementClass.WarehouseTransfer)]
    [InlineData(MovementType.LocationTransfer, MovementClass.LocationTransfer)]
    [InlineData(MovementType.StatusTransfer, MovementClass.StatusTransfer)]
    [InlineData(MovementType.Reversal, MovementClass.Reversal)]
    public void Every_canonical_type_has_its_class(MovementType type, MovementClass expected)
        => Assert.Equal(expected, MovementDirectionRules.Classify(type));

    [Fact]
    public void There_are_exactly_15_canonical_movement_types()
        => Assert.Equal(15, Enum.GetValues<MovementType>().Length);

    [Fact]
    public void Inbound_needs_a_target_defaults_to_AVAILABLE_and_forbids_a_source()
    {
        var (ok, error) = Resolve(MovementType.OpeningBalance, to: ("wh", "bin"));
        Assert.Null(error);
        Assert.Equal(StockStatus.Available, ok!.ToStockStatus);
        Assert.False(ok.HasSource);

        Assert.NotNull(Resolve(MovementType.OpeningBalance, to: ("wh", null)).Error);
        Assert.NotNull(Resolve(MovementType.OpeningBalance, from: ("wh", "bin"), to: ("wh", "bin")).Error);
        Assert.NotNull(Resolve(MovementType.OpeningBalance, to: ("wh", "bin"), fromStatus: StockStatus.Available).Error);
    }

    [Fact]
    public void Outbound_needs_a_source_and_forbids_a_target()
    {
        var (ok, error) = Resolve(MovementType.Scrap, from: ("wh", "bin"), fromStatus: StockStatus.Damaged);
        Assert.Null(error);
        Assert.Equal(StockStatus.Damaged, ok!.FromStockStatus);
        Assert.False(ok.HasTarget);

        Assert.NotNull(Resolve(MovementType.Scrap, from: (null, "bin")).Error);
        Assert.NotNull(Resolve(MovementType.Scrap, from: ("wh", "bin"), toStatus: StockStatus.Available).Error);
    }

    [Fact]
    public void Warehouse_transfer_needs_a_different_warehouse_and_keeps_the_status()
    {
        var (ok, error) = Resolve(MovementType.WarehouseTransfer, from: ("wh1", "a"), to: ("wh2", "b"), fromStatus: StockStatus.Quarantine);
        Assert.Null(error);
        Assert.Equal(StockStatus.Quarantine, ok!.ToStockStatus);

        Assert.NotNull(Resolve(MovementType.WarehouseTransfer, from: ("wh1", "a"), to: ("wh1", "b")).Error);
        Assert.NotNull(Resolve(MovementType.WarehouseTransfer, from: ("wh1", "a"), to: ("wh2", "b"), toStatus: StockStatus.Blocked).Error);
    }

    [Fact]
    public void Location_transfer_needs_the_same_warehouse_and_another_location()
    {
        Assert.Null(Resolve(MovementType.LocationTransfer, from: ("wh", "a"), to: ("wh", "b")).Error);
        Assert.NotNull(Resolve(MovementType.LocationTransfer, from: ("wh", "a"), to: ("wh", "a")).Error);
        Assert.NotNull(Resolve(MovementType.LocationTransfer, from: ("wh", "a"), to: ("wh2", "b")).Error);
    }

    [Fact]
    public void Status_transfer_stays_in_place_and_must_change_status()
    {
        var (ok, error) = Resolve(MovementType.StatusTransfer, from: ("wh", "a"), to: ("wh", "a"),
            fromStatus: StockStatus.QualityInspection, toStatus: StockStatus.Available);
        Assert.Null(error);
        Assert.Equal(StockStatus.QualityInspection, ok!.FromStockStatus);

        Assert.NotNull(Resolve(MovementType.StatusTransfer, from: ("wh", "a"), to: ("wh", "a")).Error);
        Assert.NotNull(Resolve(MovementType.StatusTransfer, from: ("wh", "a"), to: ("wh", "b"), toStatus: StockStatus.Blocked).Error);
    }

    [Fact]
    public void Reversal_must_not_name_any_side_and_its_inverse_swaps_them()
    {
        Assert.Equal((null, null), Resolve(MovementType.Reversal));
        Assert.NotNull(Resolve(MovementType.Reversal, to: ("wh", "a")).Error);

        var original = new MovementDirection("wh1", "a", StockStatus.Available, "wh2", "b", StockStatus.Available);
        var inverse = MovementDirectionRules.InverseOf(original);
        Assert.Equal(("wh2", "b", "wh1", "a"), (inverse.FromWarehouseId, inverse.FromLocationId, inverse.ToWarehouseId, inverse.ToLocationId));
    }

    private static (MovementDirection? Direction, string? Error) Resolve(
        MovementType type,
        (string? Wh, string? Loc)? from = null,
        (string? Wh, string? Loc)? to = null,
        StockStatus? fromStatus = null,
        StockStatus? toStatus = null)
        => MovementDirectionRules.Resolve(type, from?.Wh, from?.Loc, fromStatus, to?.Wh, to?.Loc, toStatus);
}

using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;
using Diten.InventoryService.Domain.Traceability.Rules;
using Xunit;

namespace Diten.InventoryService.Tests.Trace;

/// <summary>WP-0174-P2 — <see cref="LotPostingRules"/>: pack §12 lot order, CT-D4 exceptions, expiry by date, CT-D1 holding.</summary>
public sealed class LotPostingRulesTests
{
    private static readonly Guid Sku = Guid.NewGuid();
    private static readonly DateTimeOffset Posting = new(2026, 9, 30, 10, 0, 0, TimeSpan.Zero);

    private static Lot Lot(LotStatus status, string? expiry = null) => new()
    {
        Id = Guid.NewGuid(), SkuId = Sku, SkuLevel = SkuLevel.Lsku, Status = status,
        ExpiryDate = expiry is null ? null : DateTimeOffset.Parse(expiry + "T00:00:00Z")
    };

    [Theory]
    [InlineData(LotStatus.Draft, null, null)]
    [InlineData(LotStatus.Released, null, null)]
    [InlineData(LotStatus.Blocked, null, LotPostingViolation.Blocked)]
    [InlineData(LotStatus.Expired, null, LotPostingViolation.Expired)]
    [InlineData(LotStatus.Released, "2026-09-29", LotPostingViolation.Expired)]
    [InlineData(LotStatus.Released, "2026-09-30", null)] // the expiry day itself is still usable
    [InlineData(LotStatus.Blocked, "2026-09-29", LotPostingViolation.Blocked)] // Blocked is reported first (§12 order)
    public void A_sales_issue_is_judged_by_status_then_by_date(LotStatus status, string? expiry, LotPostingViolation? expected)
        => Assert.Equal(expected, LotPostingRules.Evaluate(Lot(status, expiry), Sku, SkuLevel.Lsku, MovementType.GoodsIssueSales, Posting));

    [Theory]
    [InlineData(MovementType.Scrap)]
    [InlineData(MovementType.WriteOff)]
    [InlineData(MovementType.Reversal)]
    public void CT_D4_disposal_and_reversal_pass_a_Blocked_or_Expired_lot(MovementType type)
    {
        Assert.Null(LotPostingRules.Evaluate(Lot(LotStatus.Blocked), Sku, SkuLevel.Lsku, type, Posting));
        Assert.Null(LotPostingRules.Evaluate(Lot(LotStatus.Expired), Sku, SkuLevel.Lsku, type, Posting));
        Assert.Null(LotPostingRules.Evaluate(Lot(LotStatus.Released, "2026-01-01"), Sku, SkuLevel.Lsku, type, Posting));
    }

    [Theory]
    [InlineData(MovementType.GoodsReceiptPo)]
    [InlineData(MovementType.StatusTransfer)]
    [InlineData(MovementType.LocationTransfer)]
    [InlineData(MovementType.CountLoss)]
    [InlineData(MovementType.SupplierReturn)]
    public void Every_other_movement_is_refused_by_a_Blocked_lot(MovementType type)
        => Assert.Equal(LotPostingViolation.Blocked, LotPostingRules.Evaluate(Lot(LotStatus.Blocked), Sku, SkuLevel.Lsku, type, Posting));

    [Fact]
    public void Another_SKU_is_LOT_SKU_MISMATCH_before_anything_else_even_for_a_reversal()
    {
        Assert.Equal(LotPostingViolation.SkuMismatch, LotPostingRules.Evaluate(Lot(LotStatus.Blocked), Guid.NewGuid(), SkuLevel.Lsku, MovementType.GoodsIssueSales, Posting));
        Assert.Equal(LotPostingViolation.SkuMismatch, LotPostingRules.Evaluate(Lot(LotStatus.Released), Sku, SkuLevel.Gsku, MovementType.Reversal, Posting));
    }

    [Fact]
    public void Only_Blocked_and_Expired_hold_their_stock_and_only_the_canonical_id_is_a_registered_lot()
    {
        Assert.Equal([LotStatus.Blocked, LotStatus.Expired], Enum.GetValues<LotStatus>().Where(LotPostingRules.HoldsStock));
        var id = Guid.NewGuid();
        Assert.True(LotPostingRules.IsRegisteredLotForm(id.ToString("D"), out var parsed));
        Assert.Equal(id, parsed);
        Assert.False(LotPostingRules.IsRegisteredLotForm(id.ToString("D").ToUpperInvariant(), out _));
        Assert.False(LotPostingRules.IsRegisteredLotForm(id.ToString("N"), out _));
        Assert.False(LotPostingRules.IsRegisteredLotForm("LOT-2026-001", out _));
        Assert.False(LotPostingRules.IsRegisteredLotForm(null, out _));
    }
}

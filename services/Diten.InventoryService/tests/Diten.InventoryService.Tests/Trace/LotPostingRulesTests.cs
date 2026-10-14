using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;
using Diten.InventoryService.Domain.Traceability.Rules;
using Xunit;

namespace Diten.InventoryService.Tests.Trace;

/// <summary>
/// WP-0174-P2 — <see cref="LotPostingRules"/>: pack §12 lot order, CT-D4 exceptions, expiry by date, CT-D1 holding; MOD-0175 Q4 — KQ-H
/// <c>RetestDue</c> with its exceptions and the CT-Q16 stored retest hold.
/// </summary>
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
        Assert.Equal([LotStatus.Blocked, LotStatus.Expired], Enum.GetValues<LotStatus>().Where(s => LotPostingRules.HoldsStock(s, null)));
        var id = Guid.NewGuid();
        Assert.True(LotPostingRules.IsRegisteredLotForm(id.ToString("D"), out var parsed));
        Assert.Equal(id, parsed);
        Assert.False(LotPostingRules.IsRegisteredLotForm(id.ToString("D").ToUpperInvariant(), out _));
        Assert.False(LotPostingRules.IsRegisteredLotForm(id.ToString("N"), out _));
        Assert.False(LotPostingRules.IsRegisteredLotForm("LOT-2026-001", out _));
        Assert.False(LotPostingRules.IsRegisteredLotForm(null, out _));
    }

    // ── MOD-0175 Q4: KQ-H retest-due (V24) and the CT-Q16 stored hold ─────────────────────────────────────

    private static Lot DueLot(LotStatus status = LotStatus.Released) => new()
    {
        Id = Guid.NewGuid(), SkuId = Sku, SkuLevel = SkuLevel.Lsku, Status = status, RetestDate = new DateTimeOffset(2026, 9, 29, 0, 0, 0, TimeSpan.Zero)
    };

    private static LotPostingViolation? Judge(Lot lot, MovementType type, StockStatus? from, StockStatus? to)
        => LotPostingRules.Evaluate(lot, Sku, SkuLevel.Lsku, type, Posting, from, to);

    [Theory]
    [InlineData(MovementType.GoodsIssueSales, StockStatus.Available, null)]
    [InlineData(MovementType.GoodsIssueProd, StockStatus.Available, null)]
    [InlineData(MovementType.LocationTransfer, StockStatus.Available, StockStatus.Available)]
    [InlineData(MovementType.WarehouseTransfer, StockStatus.Available, StockStatus.Available)]
    [InlineData(MovementType.SupplierReturn, StockStatus.Available, null)]
    [InlineData(MovementType.GoodsReceiptPo, null, StockStatus.Available)]
    [InlineData(MovementType.StatusTransfer, StockStatus.Quarantine, StockStatus.Available)] // back into use needs the new retest date
    [InlineData(MovementType.StatusTransfer, StockStatus.Blocked, StockStatus.Available)]
    public void A_retest_due_lot_refuses_using_its_stock_or_bringing_stock_into_use(MovementType type, StockStatus? from, StockStatus? to)
        => Assert.Equal(LotPostingViolation.RetestDue, Judge(DueLot(), type, from, to));

    [Theory]
    [InlineData(MovementType.StatusTransfer, StockStatus.Available, StockStatus.Quarantine)]
    [InlineData(MovementType.StatusTransfer, StockStatus.Available, StockStatus.QualityInspection)]
    [InlineData(MovementType.StatusTransfer, StockStatus.Available, StockStatus.Blocked)]
    [InlineData(MovementType.StatusTransfer, StockStatus.Quarantine, StockStatus.Blocked)]   // a Reject / Block line
    [InlineData(MovementType.StatusTransfer, StockStatus.Blocked, StockStatus.Quarantine)]   // Rework
    [InlineData(MovementType.SupplierReturn, StockStatus.Blocked, null)]                     // disposal of blocked stock
    [InlineData(MovementType.LocationTransfer, StockStatus.Blocked, StockStatus.Blocked)]    // Sample
    [InlineData(MovementType.GoodsReceiptPo, null, StockStatus.Quarantine)]                  // a receipt into quarantine
    [InlineData(MovementType.Scrap, StockStatus.Available, null)]
    [InlineData(MovementType.WriteOff, StockStatus.Available, null)]
    [InlineData(MovementType.Reversal, null, StockStatus.Available)]
    public void The_exceptions_keep_the_stock_out_of_use(MovementType type, StockStatus? from, StockStatus? to)
        => Assert.Null(Judge(DueLot(), type, from, to));

    [Fact]
    public void The_retest_date_itself_is_still_usable_and_the_classic_order_comes_first()
    {
        var lot = DueLot();
        lot.RetestDate = new DateTimeOffset(2026, 9, 30, 0, 0, 0, TimeSpan.Zero); // = the posting day
        Assert.Null(Judge(lot, MovementType.GoodsIssueSales, StockStatus.Available, null));
        Assert.Equal(LotPostingViolation.Blocked, Judge(DueLot(LotStatus.Blocked), MovementType.GoodsIssueSales, StockStatus.Available, null));
        Assert.Equal(LotPostingViolation.RetestDue, LotPostingRules.Evaluate(DueLot(), Sku, SkuLevel.Lsku, MovementType.GoodsIssueSales, Posting)); // short form = AVAILABLE
    }

    [Fact]
    public void CT_Q16_the_stored_retest_hold_holds_never_the_date_alone()
    {
        var due = DueLot();
        Assert.False(LotPostingRules.HoldsStock(due)); // the date passed, nothing stored yet: the SKU position still counts it
        due.RetestHeldAtUtc = Posting;
        Assert.True(LotPostingRules.HoldsStock(due));
        Assert.True(LotPostingRules.HoldsStock(LotStatus.Released, Posting));
        Assert.True(LotPostingRules.HoldsStock(LotStatus.Draft, Posting));
        Assert.False(LotPostingRules.HoldsStock(LotStatus.Released, null));
        Assert.True(LotPostingRules.IsRetestDueOn(DueLot(), Posting));
    }
}

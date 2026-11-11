using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;
using Diten.InventoryService.Domain.Traceability.Rules;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Trace;

/// <summary>
/// MOD-0177 CT-R10 — the narrow MOD-0174 lot-rule change (owner step inside WP-0177-R3): a <c>CUSTOMER_RETURN</c> of a held lot (Blocked, or
/// Expired by status or by date) is accepted ONLY into QUARANTINE / QUALITY_INSPECTION / BLOCKED — returned recalled goods must be held
/// (KR-H). Into AVAILABLE it stays <c>LOT_BLOCKED</c> / <c>LOT_EXPIRED</c>, and every other movement of the held lot is refused as before
/// (CT-D4 unchanged). Through the REAL ledger API with lots blocked through the REAL TRACE API.
/// </summary>
public sealed class HeldLotCustomerReturnTests : IClassFixture<TraceApiFactory>
{
    private readonly TraceApiFactory _factory;

    public HeldLotCustomerReturnTests(TraceApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("QUARANTINE")]
    [InlineData("QUALITY_INSPECTION")]
    [InlineData("BLOCKED")]
    public async Task A_Blocked_lot_takes_a_customer_return_into_a_held_status(string toStatus)
    {
        var s = new LotLedgerScenario(_factory);
        var lot = await s.CreateLotAsync();
        await s.ReleaseAsync(lot);
        await s.PostOkAsync(s.Receipt("10", lot));
        await s.BlockAsync(lot);

        await s.PostOkAsync(s.Receipt("2", lot, movementType: "CUSTOMER_RETURN", toStatus: toStatus));

        Assert.Equal(12m, s.BalanceOnHandOfLot(lot));
        Assert.Contains(s.Persisted("inventory_balances"), b => b["StockStatus"].AsString.Replace("_", "").Equals(toStatus.Replace("_", ""), StringComparison.OrdinalIgnoreCase) && b["LotId"].AsString == lot);
    }

    [Fact]
    public async Task A_Blocked_lot_refuses_a_customer_return_into_AVAILABLE_and_every_other_use_as_before()
    {
        var s = new LotLedgerScenario(_factory);
        var lot = await s.CreateLotAsync();
        await s.ReleaseAsync(lot);
        await s.PostOkAsync(s.Receipt("10", lot));
        await s.BlockAsync(lot);

        await s.RefusedAsync(s.Receipt("1", lot, movementType: "CUSTOMER_RETURN"), "LOT_BLOCKED");                          // default AVAILABLE
        await s.RefusedAsync(s.Receipt("1", lot, movementType: "CUSTOMER_RETURN", toStatus: "AVAILABLE"), "LOT_BLOCKED");
        await s.RefusedAsync(s.Receipt("1", lot, movementType: "GOODS_RECEIPT_PO", toStatus: "QUARANTINE"), "LOT_BLOCKED"); // only the return is widened
        await s.RefusedAsync(s.Issue("1", lot), "LOT_BLOCKED");
        Assert.Single(s.Persisted("inventory_transactions"));
    }

    [Fact]
    public async Task An_expired_lot_takes_a_customer_return_into_QUARANTINE_and_refuses_it_into_AVAILABLE()
    {
        var s = new LotLedgerScenario(_factory);
        var lot = await s.CreateLotAsync(expiryDate: "2020-01-31");

        await s.RefusedAsync(s.Receipt("1", lot, movementType: "CUSTOMER_RETURN", toStatus: "AVAILABLE"), "LOT_EXPIRED");
        await s.PostOkAsync(s.Receipt("3", lot, movementType: "CUSTOMER_RETURN", toStatus: "QUARANTINE"));

        Assert.Equal(3m, s.BalanceOnHandOfLot(lot));
    }

    [Fact]
    public void The_rule_is_the_return_type_into_a_held_status_and_nothing_else()
    {
        var sku = Guid.NewGuid();
        var blocked = new Lot { Id = Guid.NewGuid(), SkuId = sku, SkuLevel = SkuLevel.Lsku, Status = LotStatus.Blocked, LotNumber = "B-1" };
        var at = DateTimeOffset.UtcNow;

        foreach (var held in new[] { StockStatus.Quarantine, StockStatus.QualityInspection, StockStatus.Blocked })
        {
            Assert.Null(LotPostingRules.Evaluate(blocked, sku, SkuLevel.Lsku, MovementType.CustomerReturn, at, null, held));
        }

        Assert.Equal(LotPostingViolation.Blocked, LotPostingRules.Evaluate(blocked, sku, SkuLevel.Lsku, MovementType.CustomerReturn, at, null, StockStatus.Available));
        Assert.Equal(LotPostingViolation.Blocked, LotPostingRules.Evaluate(blocked, sku, SkuLevel.Lsku, MovementType.CustomerReturn, at)); // no status named
        Assert.Equal(LotPostingViolation.Blocked, LotPostingRules.Evaluate(blocked, sku, SkuLevel.Lsku, MovementType.GoodsReceiptPo, at, null, StockStatus.Quarantine));
        Assert.Equal(LotPostingViolation.SkuMismatch, LotPostingRules.Evaluate(blocked, Guid.NewGuid(), SkuLevel.Lsku, MovementType.CustomerReturn, at, null, StockStatus.Quarantine));
    }
}

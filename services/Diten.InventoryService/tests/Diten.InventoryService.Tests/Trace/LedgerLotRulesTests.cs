using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Trace;

/// <summary>
/// WP-0174-P2 — MOD-0174 pack §12 "Ledger posting rules" (lot rows) and CT-D4, through the REAL ledger API
/// (<c>POST /api/inventory/movements</c>) with lots created and blocked through the REAL TRACE API. Every refusal is a 422
/// with the pack code and writes nothing (no transaction, no balance change). <c>LOT_REQUIRED</c> is unchanged.
/// </summary>
public sealed class LedgerLotRulesTests : IClassFixture<TraceApiFactory>
{
    private readonly TraceApiFactory _factory;

    public LedgerLotRulesTests(TraceApiFactory factory) => _factory = factory;

    [Fact]
    public async Task LOT_REQUIRED_is_unchanged_for_a_lot_controlled_item_without_lot()
    {
        var s = new LotLedgerScenario(_factory);
        await s.RefusedAsync(s.Receipt("1", lotId: null), "LOT_REQUIRED");
    }

    [Fact]
    public async Task A_registered_lot_on_an_item_that_is_not_lot_controlled_is_LOT_NOT_ALLOWED()
    {
        var lots = new LotLedgerScenario(_factory);
        var lot = await lots.CreateLotAsync();
        var s = new LotLedgerScenario(_factory, lotControlled: false);

        await s.RefusedAsync(s.Receipt("1", lot), "LOT_NOT_ALLOWED");
        Assert.Empty(s.Persisted("inventory_transactions"));
    }

    [Fact]
    public async Task An_id_that_is_not_a_lot_of_this_tenant_is_UNKNOWN_LOT_another_tenants_lot_included()
    {
        var other = new LotLedgerScenario(_factory);
        var foreignLot = await other.CreateLotAsync();
        var s = new LotLedgerScenario(_factory);

        await s.RefusedAsync(s.Receipt("1", Guid.NewGuid().ToString("D")), "UNKNOWN_LOT");
        await s.RefusedAsync(s.Receipt("1", foreignLot), "UNKNOWN_LOT"); // §13 #15: another tenant's lot does not exist
        Assert.Empty(s.Persisted("inventory_transactions"));
        Assert.Empty(s.Persisted("inventory_balances"));
    }

    [Fact]
    public async Task A_lot_of_another_SKU_than_the_movement_is_LOT_SKU_MISMATCH()
    {
        var s = new LotLedgerScenario(_factory);
        var gsku = s.AddSecondSku();
        var lotOfGsku = await s.CreateLotAsync(sku: gsku, level: SkuLevel.Gsku);

        await s.RefusedAsync(s.Receipt("1", lotOfGsku), "LOT_SKU_MISMATCH");
        Assert.Empty(s.Persisted("inventory_transactions"));
    }

    [Fact]
    public async Task A_Draft_and_a_Released_lot_receive_and_issue_and_the_ledger_stores_the_lot_id()
    {
        var s = new LotLedgerScenario(_factory);
        var draft = await s.CreateLotAsync();
        await s.PostOkAsync(s.Receipt("5", draft)); // ASSUMPTION-0174-07: a Draft lot takes stock
        await s.ReleaseAsync(draft);
        await s.PostOkAsync(s.Issue("2", draft));

        var transactions = s.Persisted("inventory_transactions");
        Assert.Equal(2, transactions.Count);
        Assert.All(transactions, t => Assert.Equal(draft, t["LotId"].AsString)); // ASSUMPTION-0174-03: surrogate id
        Assert.Equal(3m, s.BalanceOnHandOfLot(draft));
    }

    [Fact]
    public async Task CT_D4_a_Blocked_lot_takes_only_SCRAP_WRITE_OFF_and_REVERSAL_and_its_rows_keep_their_stock_status()
    {
        var s = new LotLedgerScenario(_factory);
        var lot = await s.CreateLotAsync();
        await s.ReleaseAsync(lot);
        var receipt = s.Receipt("10", lot);
        var receiptId = await s.PostOkAsync(receipt);
        var statusesBefore = s.Persisted("inventory_balances").Select(b => b["StockStatus"].AsString).ToList();

        await s.BlockAsync(lot);

        await s.RefusedAsync(s.Issue("1", lot), "LOT_BLOCKED");
        await s.RefusedAsync(s.Receipt("1", lot), "LOT_BLOCKED");
        await s.RefusedAsync(s.Transfer("1", lot), "LOT_BLOCKED");
        await s.RefusedAsync(s.StatusTransfer("1", lot), "LOT_BLOCKED");
        await s.RefusedAsync(s.Issue("1", lot, movementType: "GOODS_ISSUE_PROD"), "LOT_BLOCKED");
        Assert.Single(s.Persisted("inventory_transactions")); // nothing of the refused ones was written

        await s.PostOkAsync(s.Issue("2", lot, movementType: "SCRAP"));
        await s.PostOkAsync(s.Issue("1", lot, movementType: "WRITE_OFF"));
        Assert.Equal(7m, s.BalanceOnHandOfLot(lot));
        Assert.Equal(statusesBefore, s.Persisted("inventory_balances").Select(b => b["StockStatus"].AsString).ToList());

        // REVERSAL of the original receipt — needs the full quantity on the row, so first give the scrap back.
        var scrapReversalSource = s.Issue("3", lot, movementType: "SCRAP");
        var scrapId = await s.PostOkAsync(scrapReversalSource);
        await s.PostOkAsync(s.Reversal(scrapReversalSource, scrapId)); // REVERSAL into the Blocked lot
        Assert.Equal(7m, s.BalanceOnHandOfLot(lot));
        Assert.True(receiptId.Length > 0);
    }

    [Fact]
    public async Task ASSUMPTION_0174_10_with_the_switch_off_a_typed_lot_posts_exactly_as_before_P2()
    {
        var s = new LotLedgerScenario(_factory);
        await s.PostOkAsync(s.Receipt("5", "W-UI-02/LOT-7"));
        await s.PostOkAsync(s.Issue("2", "W-UI-02/LOT-7"));
        await s.PostOkAsync(s.Transfer("1", "W-UI-02/LOT-7"));

        Assert.Equal(3m, s.BalanceOnHandOfLot("W-UI-02/LOT-7"));
        Assert.Empty(s.Persisted("inventory_lot_positions")); // a typed lot has no K-A position until P4 re-keys it

        // A typed lot on an item that is not lot-controlled was accepted before P2 and still is (switch off).
        var plain = new LotLedgerScenario(_factory, lotControlled: false);
        await plain.PostOkAsync(plain.Receipt("1", "FREE-TEXT-1"));
    }
}

/// <summary>
/// WP-0174-P2 — <c>Trace:EnforceRegisteredLots = true</c> (switched on when P3 is CT-accepted, not in this WP): an inbound
/// movement of a lot-controlled item names a registered lot; a typed lot on an item that is not lot-controlled is
/// <c>LOT_NOT_ALLOWED</c>.
/// </summary>
public sealed class LedgerEnforcedLotsTests : IClassFixture<EnforcedLotsTraceApiFactory>
{
    private readonly EnforcedLotsTraceApiFactory _factory;

    public LedgerEnforcedLotsTests(EnforcedLotsTraceApiFactory factory) => _factory = factory;

    [Fact]
    public async Task With_the_switch_on_an_inbound_typed_lot_is_UNKNOWN_LOT_and_a_registered_lot_posts()
    {
        var s = new LotLedgerScenario(_factory);
        await s.RefusedAsync(s.Receipt("1", "TYPED-LOT-1"), "UNKNOWN_LOT");
        await s.RefusedAsync(s.Receipt("1", "TYPED-LOT-1", movementType: "OPENING_BALANCE"), "UNKNOWN_LOT");

        var lot = await s.CreateLotAsync();
        await s.PostOkAsync(s.Receipt("1", lot));
        Assert.Equal(1m, s.LotPositionOnHand(lot));
    }

    [Fact]
    public async Task With_the_switch_on_a_typed_lot_on_an_item_that_is_not_lot_controlled_is_LOT_NOT_ALLOWED()
    {
        var s = new LotLedgerScenario(_factory, lotControlled: false);
        await s.RefusedAsync(s.Receipt("1", "FREE-TEXT-1"), "LOT_NOT_ALLOWED");
        await s.PostOkAsync(s.Receipt("1", lotId: null));
    }
}

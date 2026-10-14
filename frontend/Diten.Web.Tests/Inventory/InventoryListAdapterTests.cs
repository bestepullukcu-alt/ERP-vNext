using Diten.Web.Models.Inventory;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Diten.Web.Tests.Inventory;

/// <summary>
/// MOD-0173-FU01 list adapter (pack §8): the repo server-mode wire → the frozen INVENTORY-BUNDLE v1 query, measured on
/// the shipping <see cref="InventoryListAdapter"/>.
/// </summary>
public sealed class InventoryListAdapterTests
{
    private static IQueryCollection Query(params (string Key, string Value)[] pairs) =>
        new QueryCollection(pairs.GroupBy(p => p.Key).ToDictionary(g => g.Key, g => new StringValues(g.Select(p => p.Value).ToArray())));

    [Theory]
    [InlineData(0, 50, 1, 50)]
    [InlineData(50, 50, 2, 50)]
    [InlineData(200, 100, 3, 100)]
    [InlineData(0, 500, 1, 200)] // pageSize capped at 200 (contract ceiling)
    public void Paging_is_start_over_length_plus_one(int start, int length, int page, int pageSize)
    {
        var (paging, error, _) = InventoryListAdapter.TranslatePaging(Query(("start", start.ToString()), ("length", length.ToString()), ("draw", "3")));

        Assert.Null(error);
        Assert.Equal(new InventoryListAdapter.Paging(page, pageSize, 3), paging);
    }

    [Fact]
    public void Page_2_of_50_reaches_the_contract_as_page_2_pageSize_50()
    {
        var translation = InventoryListAdapter.BalanceQuery(Query(("start", "50"), ("length", "50"), ("draw", "2")));

        Assert.True(translation.IsValid);
        Assert.Equal("?page=2&pageSize=50", translation.Query);
    }

    [Theory]
    [InlineData("search", "abc", InventoryListAdapter.SearchNotSupported)]
    [InlineData("orderBy", "onHand", InventoryListAdapter.OrderNotSupported)]
    [InlineData("orderDir", "asc", InventoryListAdapter.OrderNotSupported)]
    public void Search_and_sort_are_refused_because_the_contract_has_neither(string key, string value, string code)
    {
        Assert.Equal(code, InventoryListAdapter.BalanceQuery(Query(("start", "0"), ("length", "50"), (key, value))).ErrorCode);
        Assert.Equal(code, InventoryListAdapter.TransactionsQuery(Query(("start", "0"), ("length", "50"), (key, value))).ErrorCode);
    }

    [Fact]
    public void A_start_that_is_not_a_page_boundary_is_refused()
    {
        Assert.Equal(InventoryListAdapter.PageMisaligned, InventoryListAdapter.BalanceQuery(Query(("start", "25"), ("length", "50"))).ErrorCode);
    }

    [Fact]
    public void Balance_filters_map_to_contract_parameters_and_the_sku_carries_its_level()
    {
        var item = Guid.NewGuid();
        var sku = Guid.NewGuid();
        var translation = InventoryListAdapter.BalanceQuery(Query(
            ("start", "0"), ("length", "10"),
            ("itemId", item.ToString()), ("sku", "Lsku:" + sku), ("warehouseId", "wh-01"), ("locationId", "loc A"),
            ("lotId", "LOT-1"), ("stockStatus", "QUARANTINE"), ("unrelated", "x")));

        Assert.True(translation.IsValid);
        Assert.Equal($"?itemId={item:D}&skuId={sku:D}&skuLevel=Lsku&warehouseId=wh-01&locationId=loc%20A&lotId=LOT-1&stockStatus=QUARANTINE&page=1&pageSize=10", translation.Query);
    }

    [Theory]
    [InlineData("itemId", "not-a-guid")]
    [InlineData("sku", "Bogus:3fa85f64-5717-4562-b3fc-2c963f66afa6")]
    [InlineData("sku", "Lsku:nope")]
    [InlineData("stockStatus", "LOST")]
    public void Invalid_balance_filters_never_reach_the_gateway(string key, string value)
    {
        Assert.Equal(InventoryListAdapter.InvalidFilter, InventoryListAdapter.BalanceQuery(Query(("start", "0"), ("length", "10"), (key, value))).ErrorCode);
    }

    [Fact]
    public void Transactions_take_item_lot_location_and_a_date_range_only()
    {
        var item = Guid.NewGuid();
        var translation = InventoryListAdapter.TransactionsQuery(Query(
            ("start", "100"), ("length", "50"), ("itemId", item.ToString()), ("lotId", "L1"), ("locationId", "bin-1"),
            ("from", "2026-10-01"), ("to", "2026-10-31"), ("warehouseId", "wh-01")));

        Assert.True(translation.IsValid);
        Assert.Equal($"?itemId={item:D}&lotId=L1&locationId=bin-1&from=2026-10-01&to=2026-10-31&page=3&pageSize=50", translation.Query);
        Assert.Equal(InventoryListAdapter.InvalidFilter, InventoryListAdapter.TransactionsQuery(Query(("start", "0"), ("length", "10"), ("from", "01.10.2026"))).ErrorCode);
    }

    [Fact]
    public void The_kpi_query_exists_only_with_a_product_or_sku_filter()
    {
        Assert.Null(InventoryListAdapter.AvailabilityQuery(Query(("warehouseId", "wh-01"))));
        var item = Guid.NewGuid();
        Assert.Equal($"?itemId={item:D}&warehouseId=wh-01", InventoryListAdapter.AvailabilityQuery(Query(("itemId", item.ToString()), ("warehouseId", "wh-01"))));
    }

    [Fact]
    public void Summary_sums_the_blocked_bucket_in_decimal_and_keeps_the_contract_strings()
    {
        var summary = InventoryListAdapter.Summary(new AvailabilityViewDto
        {
            OnHand = "100.000",
            Available = "70.000",
            BaseUomId = "EA",
            ByStatus = new() { ["AVAILABLE"] = "70.000", ["QUALITY_INSPECTION"] = "10.000", ["QUARANTINE"] = "0.1", ["BLOCKED"] = "0.2", ["DAMAGED"] = "19.700", ["EXPIRED"] = "0" }
        });

        Assert.Equal("100.000", summary.OnHand);
        Assert.Equal("70.000", summary.Available);
        Assert.Equal("10.000", summary.QualityInspection);
        Assert.Equal("20.000", summary.QuarantineBlocked); // 0.1 + 0.2 + 19.700 + 0, exact (no float drift)
    }

    [Fact]
    public void Reversal_is_offered_only_on_a_manual_non_reversal_movement_not_reversed_on_the_page()
    {
        var reversedHere = Guid.NewGuid().ToString();
        var rows = InventoryListAdapter.MapTransactions(
        [
            Tx("manual", "COUNT_LOSS", "MOD-0173"),
            Tx("automatic", "GOODS_RECEIPT_PO", "MOD-0142"),
            Tx("a-reversal", "REVERSAL", "MOD-0173", sourceDocumentId: reversedHere),
            Tx(reversedHere, "OPENING_BALANCE", "MOD-0173")
        ], MasterLabels.None).ToDictionary(r => r.Id);

        Assert.True(rows["manual"].CanReverse);
        Assert.False(rows["manual"].IsAutomatic);
        Assert.Equal("out", rows["manual"].Direction);

        Assert.False(rows["automatic"].CanReverse);
        Assert.True(rows["automatic"].IsAutomatic);
        Assert.Equal("in", rows["automatic"].Direction);

        Assert.False(rows["a-reversal"].CanReverse);
        Assert.Equal("reversal", rows["a-reversal"].Direction);

        Assert.True(rows[reversedHere].ReversedOnPage);
        Assert.False(rows[reversedHere].CanReverse);
    }

    [Fact]
    public void Unresolved_labels_leave_the_raw_ids_for_the_screen()
    {
        var item = Guid.NewGuid().ToString();
        var row = Assert.Single(InventoryListAdapter.MapBalanceRows([new BalanceViewDto { ItemId = item, SkuId = "s", WarehouseId = "wh", OnHand = "5.000" }], MasterLabels.None));

        Assert.Null(row.ProductLabel);
        Assert.Equal(item, row.ItemId);
        Assert.Equal("5.000", row.OnHand);
        Assert.False(MasterLabels.None.Available);
    }

    private static TransactionItemDto Tx(string id, string type, string module, string? sourceDocumentId = null) => new()
    {
        TransactionId = id,
        TransactionNumber = "INV-" + id,
        MovementType = type,
        SourceModule = module,
        SourceDocumentId = sourceDocumentId,
        Quantity = "1.000",
        UomId = "EA"
    };
}

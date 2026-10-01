namespace Diten.Web.Models.Inventory;

// MOD-0173-FU01 S-1 Stock list — the shapes the MVC list adapter reads from INVENTORY-BUNDLE v1 and the shapes it
// hands to the screen. Decimals stay STRINGS end to end (pack §8): the contract sends "100.000", the screen formats
// that string; nothing here parses a quantity into a float. The one sum the screen needs (KPI "Quarantine /
// Blocked") is computed in C# decimal (ASSUMPTION-UI-13).

/// <summary>One row of <c>GET /api/inventory/balance</c> (contract <c>BalanceView</c>).</summary>
public sealed class BalanceViewDto
{
    public string? ItemId { get; set; }
    public string? SkuId { get; set; }
    public string? SkuLevel { get; set; }
    public string? WarehouseId { get; set; }
    public string? LocationId { get; set; }
    public string? LotId { get; set; }
    public string? StockStatus { get; set; }
    public string? OnHand { get; set; }
    public string? Reserved { get; set; }
    public string? Available { get; set; }
    public string? InTransit { get; set; }
    public string? Blocked { get; set; }
    public string? QualityInspection { get; set; }
    public string? BaseUomId { get; set; }
    public string? LastMovementId { get; set; }
    public string? LastPostingDate { get; set; }
}

/// <summary><c>GET /api/inventory/balance</c> page: <c>{ rows, total, contractVersion }</c>.</summary>
public sealed class BalancePageDto
{
    public List<BalanceViewDto>? Rows { get; set; }
    public int? Total { get; set; }
    public string? ContractVersion { get; set; }
}

/// <summary><c>GET /api/inventory/availability</c> (contract <c>AvailabilityView</c>).</summary>
public sealed class AvailabilityViewDto
{
    public string? ItemId { get; set; }
    public string? SkuId { get; set; }
    public string? OnHand { get; set; }
    public string? Reserved { get; set; }
    public string? Available { get; set; }
    public Dictionary<string, string>? ByStatus { get; set; }
    public string? BaseUomId { get; set; }
    public string? AsOf { get; set; }
}

/// <summary>A balance row as the S-1 table renders it: contract fields + display labels resolved for this page only.</summary>
public sealed class StockLedgerRowViewModel
{
    /// <summary>Row identity for the table (the projection has no id of its own): the dimension tuple.</summary>
    public string Id { get; init; } = string.Empty;
    public string ItemId { get; init; } = string.Empty;
    public string? ProductLabel { get; init; }
    public string SkuId { get; init; } = string.Empty;
    public string? SkuLevel { get; init; }
    public string? SkuLabel { get; init; }
    public string? WarehouseId { get; init; }
    public string? WarehouseLabel { get; init; }
    public string? LocationId { get; init; }
    public string? LocationLabel { get; init; }
    public string? LotId { get; init; }
    public string? StockStatus { get; init; }
    public string OnHand { get; init; } = "0";
    public string Reserved { get; init; } = "0";
    public string Available { get; init; } = "0";
    public string QualityInspection { get; init; } = "0";
    public string Blocked { get; init; } = "0";
    public string InTransit { get; init; } = "0";
    public string? BaseUomId { get; init; }
}

/// <summary>S-1 KPI strip (pack §4) — present only when a product or SKU filter is set (ASSUMPTION-UI-03).</summary>
public sealed class StockLedgerSummaryViewModel
{
    public string OnHand { get; init; } = "0";
    public string Available { get; init; } = "0";
    public string QualityInspection { get; init; } = "0";
    /// <summary>QUARANTINE + BLOCKED + DAMAGED + EXPIRED (parent §4 "Blocked" bucket), summed in decimal.</summary>
    public string QuarantineBlocked { get; init; } = "0";
    public string? BaseUomId { get; init; }
}

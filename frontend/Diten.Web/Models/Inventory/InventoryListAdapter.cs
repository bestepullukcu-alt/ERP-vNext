using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace Diten.Web.Models.Inventory;

/// <summary>
/// MOD-0173-FU01 list adapter (pack §8, ASSUMPTION-UI-01/02). The repo's server-mode list sends
/// <c>start, length, search, orderBy, orderDir, draw, &lt;filters&gt;</c> and reads <c>{ data: { items, total,
/// filteredTotal } }</c>; the FROZEN INVENTORY-BUNDLE v1 takes <c>page, pageSize ≤ 200</c> and answers
/// <c>{ rows|items, total }</c>. This class is the translation and nothing else — presentation code, no business
/// rule: every stock number comes from the service as it was sent.
/// <list type="bullet">
/// <item><c>page = start / length + 1</c>; <c>length</c> capped at 200; <c>start % length ≠ 0</c> → 400.</item>
/// <item><c>search</c> or <c>orderBy</c> → 400 (BG-04: the contract has neither); the shipped JS never sends them.</item>
/// <item>Filters are an allow-list per screen and are validated before anything reaches the Gateway.</item>
/// </list>
/// Pure functions, so the paging and mapping rules are measured directly (Diten.Web.Tests/Inventory).
/// </summary>
public static class InventoryListAdapter
{
    public const int MaxPageSize = 200;
    public const string ManualSourceModule = "MOD-0173";
    public const string ReversalMovementType = "REVERSAL";

    public const string SearchNotSupported = "LIST_SEARCH_NOT_SUPPORTED";
    public const string OrderNotSupported = "LIST_ORDER_NOT_SUPPORTED";
    public const string PageMisaligned = "LIST_PAGE_MISALIGNED";
    public const string InvalidFilter = "INVALID_REQUEST";

    public static readonly IReadOnlyList<string> StockStatuses =
        ["AVAILABLE", "QUALITY_INSPECTION", "QUARANTINE", "BLOCKED", "IN_TRANSIT", "DAMAGED", "EXPIRED"];

    /// <summary>INVENTORY-BUNDLE 1.1.0 levels; <c>Item</c> = a material item stocked as its own SKU (WP-0173-PM1, Q-1).</summary>
    public static readonly IReadOnlyList<string> SkuLevels = ["Gsku", "Lsku", "FinishedGood", "Item"];

    // Direction class of every canonical movement type (parent §12) — decides the sign shown, never a quantity.
    private static readonly HashSet<string> Inbound = new(StringComparer.Ordinal)
        { "OPENING_BALANCE", "GOODS_RECEIPT_PO", "GOODS_RECEIPT_PROD", "CUSTOMER_RETURN", "COUNT_GAIN" };
    private static readonly HashSet<string> Outbound = new(StringComparer.Ordinal)
        { "GOODS_ISSUE_SALES", "GOODS_ISSUE_PROD", "SUPPLIER_RETURN", "COUNT_LOSS", "SCRAP", "WRITE_OFF" };
    private static readonly HashSet<string> Transfer = new(StringComparer.Ordinal)
        { "WAREHOUSE_TRANSFER", "LOCATION_TRANSFER", "STATUS_TRANSFER" };

    // QUARANTINE + BLOCKED + DAMAGED + EXPIRED = the KPI "Quarantine / Blocked" bucket (pack §4, parent §4).
    private static readonly string[] BlockedBucket = ["QUARANTINE", "BLOCKED", "DAMAGED", "EXPIRED"];

    public sealed record Paging(int Page, int PageSize, int Draw);

    public sealed record Translation(string? Query, Paging? Paging, string? ErrorCode, string? ErrorDetail)
    {
        public bool IsValid => ErrorCode is null;
    }

    /// <summary>The wire paging → contract paging, or the reason it cannot be translated.</summary>
    public static (Paging? Paging, string? ErrorCode, string? Detail) TranslatePaging(IQueryCollection query)
    {
        if (!string.IsNullOrWhiteSpace(query["search"]))
            return (null, SearchNotSupported, "search");
        if (!string.IsNullOrWhiteSpace(query["orderBy"]) || !string.IsNullOrWhiteSpace(query["orderDir"]))
            return (null, OrderNotSupported, "orderBy");

        var start = ReadInt(query["start"], 0);
        var length = ReadInt(query["length"], 50);
        var draw = ReadInt(query["draw"], 0);
        if (start is null || length is null || draw is null || start < 0 || length < 1)
            return (null, InvalidFilter, "start/length");

        var size = Math.Min(length.Value, MaxPageSize);
        if (start.Value % size != 0)
            return (null, PageMisaligned, "start");

        return (new Paging(start.Value / size + 1, size, draw.Value), null, null);
    }

    /// <summary>S-1 → <c>GET /api/inventory/balance?…</c>. Filters: itemId, sku (<c>Level:uuid</c>), warehouseId, locationId, lotId, stockStatus.</summary>
    public static Translation BalanceQuery(IQueryCollection query)
    {
        var (paging, error, detail) = TranslatePaging(query);
        if (paging is null) return new Translation(null, null, error, detail);

        var parts = new List<string>();
        if (!TryAddGuid(parts, "itemId", query["itemId"])) return Invalid("itemId");
        var sku = Single(query["sku"]);
        if (sku is not null)
        {
            if (!TryParseSku(sku, out var level, out var skuId)) return Invalid("sku");
            parts.Add("skuId=" + skuId);
            parts.Add("skuLevel=" + level);
        }

        AddText(parts, "warehouseId", query["warehouseId"]);
        AddText(parts, "locationId", query["locationId"]);
        AddText(parts, "lotId", query["lotId"]);
        var status = Single(query["stockStatus"]);
        if (status is not null)
        {
            if (!StockStatuses.Contains(status, StringComparer.Ordinal)) return Invalid("stockStatus");
            parts.Add("stockStatus=" + status);
        }

        parts.Add("page=" + paging.Page.ToString(CultureInfo.InvariantCulture));
        parts.Add("pageSize=" + paging.PageSize.ToString(CultureInfo.InvariantCulture));
        return new Translation("?" + string.Join("&", parts), paging, null, null);
    }

    /// <summary>
    /// The availability query that feeds the S-1 KPI strip, or null when no product / SKU filter is set (UI-03). Same
    /// scope as the list (warehouse / location) so the cards describe what the table shows.
    /// </summary>
    public static string? AvailabilityQuery(IQueryCollection query)
    {
        var parts = new List<string>();
        var itemId = Single(query["itemId"]);
        if (itemId is not null && Guid.TryParse(itemId, out var item)) parts.Add("itemId=" + item.ToString("D"));
        var sku = Single(query["sku"]);
        if (sku is not null && TryParseSku(sku, out var level, out var skuId))
        {
            parts.Add("skuId=" + skuId);
            parts.Add("skuLevel=" + level);
        }

        if (parts.Count == 0) return null;
        AddText(parts, "warehouseId", query["warehouseId"]);
        AddText(parts, "locationId", query["locationId"]);
        return "?" + string.Join("&", parts);
    }

    /// <summary>S-2 → <c>GET /api/inventory/transactions?…</c>. Filters: itemId, lotId, locationId, from, to (yyyy-MM-dd).</summary>
    public static Translation TransactionsQuery(IQueryCollection query)
    {
        var (paging, error, detail) = TranslatePaging(query);
        if (paging is null) return new Translation(null, null, error, detail);

        var parts = new List<string>();
        if (!TryAddGuid(parts, "itemId", query["itemId"])) return Invalid("itemId");
        AddText(parts, "lotId", query["lotId"]);
        AddText(parts, "locationId", query["locationId"]);
        if (!TryAddDate(parts, "from", query["from"])) return Invalid("from");
        if (!TryAddDate(parts, "to", query["to"])) return Invalid("to");

        parts.Add("page=" + paging.Page.ToString(CultureInfo.InvariantCulture));
        parts.Add("pageSize=" + paging.PageSize.ToString(CultureInfo.InvariantCulture));
        return new Translation("?" + string.Join("&", parts), paging, null, null);
    }

    /// <param name="lotNumbers">FIX-LIVE1: lotId → TRACE lot number for the page (<see cref="Diten.Web.Models.Traceability.LotNumberResolver"/>); none = every registered lot is unresolved.</param>
    public static List<StockLedgerRowViewModel> MapBalanceRows(IEnumerable<BalanceViewDto>? rows, MasterLabels labels, IReadOnlyDictionary<string, string>? lotNumbers = null) =>
        (rows ?? []).Select(r => new StockLedgerRowViewModel
        {
            Id = string.Join("|", r.ItemId, r.SkuId, r.SkuLevel, r.WarehouseId, r.LocationId, r.LotId, r.StockStatus),
            ItemId = r.ItemId ?? string.Empty,
            ProductLabel = labels.Product(r.ItemId),
            SkuId = r.SkuId ?? string.Empty,
            SkuLevel = r.SkuLevel,
            SkuLabel = labels.Sku(r.SkuId),
            WarehouseId = r.WarehouseId,
            WarehouseLabel = labels.Location(r.WarehouseId),
            LocationId = r.LocationId,
            LocationLabel = labels.Location(r.LocationId),
            LotId = r.LotId,
            LotNumber = r.LotId is not null && lotNumbers is not null && lotNumbers.TryGetValue(r.LotId, out var number) ? number : null,
            LotUnresolved = Diten.Web.Models.Traceability.LotNumberResolver.Unresolved(r.LotId, lotNumbers ?? new Dictionary<string, string>()),
            StockStatus = r.StockStatus,
            OnHand = r.OnHand ?? "0",
            Reserved = r.Reserved ?? "0",
            Available = r.Available ?? "0",
            QualityInspection = r.QualityInspection ?? "0",
            Blocked = r.Blocked ?? "0",
            InTransit = r.InTransit ?? "0",
            BaseUomId = r.BaseUomId
        }).ToList();

    /// <param name="lotNumbers">WP-MVP1-FIX-LIVE3: lotId → TRACE lot number for the page (<see cref="Diten.Web.Models.Traceability.LotNumberResolver"/>); none = every registered lot is unresolved.</param>
    public static List<StockMovementRowViewModel> MapTransactions(IEnumerable<TransactionItemDto>? items, MasterLabels labels, IReadOnlyDictionary<string, string>? lotNumbers = null)
    {
        var list = (items ?? []).ToList();
        // A REVERSAL carries sourceDocumentId = the original transactionId (parent ASSUMPTION-0173-05).
        var reversedHere = list
            .Where(t => string.Equals(t.MovementType, ReversalMovementType, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(t.SourceDocumentId))
            .Select(t => t.SourceDocumentId!)
            .ToHashSet(StringComparer.Ordinal);

        return list.Select(t =>
        {
            var automatic = !string.Equals(t.SourceModule, ManualSourceModule, StringComparison.Ordinal);
            var isReversal = string.Equals(t.MovementType, ReversalMovementType, StringComparison.Ordinal);
            var reversed = t.TransactionId is not null && reversedHere.Contains(t.TransactionId);
            return new StockMovementRowViewModel
            {
                Id = t.TransactionId ?? string.Empty,
                TransactionNumber = t.TransactionNumber,
                MovementType = t.MovementType,
                Direction = DirectionOf(t.MovementType),
                IsAutomatic = automatic,
                MovementReason = t.MovementReason,
                ItemId = t.ItemId,
                ProductLabel = labels.Product(t.ItemId),
                SkuId = t.SkuId,
                SkuLevel = t.SkuLevel,
                SkuLabel = labels.Sku(t.SkuId),
                Quantity = t.Quantity,
                UomId = t.UomId,
                BaseQuantity = t.BaseQuantity,
                BaseUomId = t.BaseUomId,
                ToWarehouseId = t.ToWarehouseId,
                ToWarehouseLabel = labels.Location(t.ToWarehouseId),
                ToLocationId = t.ToLocationId,
                ToLocationLabel = labels.Location(t.ToLocationId),
                LotId = t.LotId,
                LotNumber = t.LotId is not null && lotNumbers is not null && lotNumbers.TryGetValue(t.LotId, out var number) ? number : null,
                LotUnresolved = Diten.Web.Models.Traceability.LotNumberResolver.Unresolved(t.LotId, lotNumbers ?? new Dictionary<string, string>()),
                FromStockStatus = t.FromStockStatus,
                ToStockStatus = t.ToStockStatus,
                SourceModule = t.SourceModule,
                SourceDocumentId = t.SourceDocumentId,
                SourceLineId = t.SourceLineId,
                TransactionDate = t.TransactionDate,
                PostingDate = t.PostingDate,
                CreatedById = t.CreatedBy?.UserId,
                CreatedByName = string.IsNullOrWhiteSpace(t.CreatedBy?.Name) ? null : t.CreatedBy!.Name!.Trim(),
                ReversedOnPage = reversed,
                CanReverse = IsReversible(t) && !reversed,
                CanCorrect = IsCorrectable(t) && !reversed
            };
        }).ToList();
    }

    /// <summary>UI-08: only a manual movement (sourceModule MOD-0173) that is not itself a reversal can be reversed here.</summary>
    public static bool IsReversible(TransactionItemDto t) =>
        string.Equals(t.SourceModule, ManualSourceModule, StringComparison.Ordinal)
        && !string.Equals(t.MovementType, ReversalMovementType, StringComparison.Ordinal)
        && !string.IsNullOrWhiteSpace(t.TransactionId);

    /// <summary>UI-2 KD-1: reversible (UI-08) AND one of the seven manual types the movement form can re-post.</summary>
    public static bool IsCorrectable(TransactionItemDto t) =>
        IsReversible(t) && MovementRules.ManualTypes.Contains(t.MovementType ?? string.Empty, StringComparer.Ordinal);

    public static string DirectionOf(string? movementType) =>
        movementType is null ? string.Empty
        : Inbound.Contains(movementType) ? "in"
        : Outbound.Contains(movementType) ? "out"
        : Transfer.Contains(movementType) ? "transfer"
        : string.Equals(movementType, ReversalMovementType, StringComparison.Ordinal) ? "reversal"
        : string.Empty;

    /// <summary>KPI strip from <c>AvailabilityView</c>; the one sum is decimal, never float (UI-13).</summary>
    public static StockLedgerSummaryViewModel Summary(AvailabilityViewDto view)
    {
        var byStatus = view.ByStatus ?? new Dictionary<string, string>();
        decimal blocked = 0;
        foreach (var status in BlockedBucket)
        {
            if (byStatus.TryGetValue(status, out var value) && TryDecimal(value, out var amount))
                blocked += amount;
        }

        return new StockLedgerSummaryViewModel
        {
            OnHand = view.OnHand ?? "0",
            Available = view.Available ?? "0",
            QualityInspection = byStatus.TryGetValue("QUALITY_INSPECTION", out var qi) ? qi : "0",
            QuarantineBlocked = blocked.ToString(CultureInfo.InvariantCulture),
            BaseUomId = view.BaseUomId
        };
    }

    public static bool TryParseSku(string value, out string level, out string skuId)
    {
        level = string.Empty;
        skuId = string.Empty;
        var separator = value.IndexOf(':');
        if (separator <= 0) return false;
        var candidateLevel = value[..separator];
        if (!SkuLevels.Contains(candidateLevel, StringComparer.Ordinal)) return false;
        if (!Guid.TryParse(value[(separator + 1)..], out var id)) return false;
        level = candidateLevel;
        skuId = id.ToString("D");
        return true;
    }

    private static Translation Invalid(string field) => new(null, null, InvalidFilter, field);

    private static string? Single(Microsoft.Extensions.Primitives.StringValues values)
    {
        var value = values.Count > 0 ? values[^1] : null;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static int? ReadInt(Microsoft.Extensions.Primitives.StringValues values, int fallback)
    {
        var raw = Single(values);
        if (raw is null) return fallback;
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static bool TryAddGuid(List<string> parts, string name, Microsoft.Extensions.Primitives.StringValues values)
    {
        var raw = Single(values);
        if (raw is null) return true;
        if (!Guid.TryParse(raw, out var id)) return false;
        parts.Add(name + "=" + id.ToString("D"));
        return true;
    }

    private static bool TryAddDate(List<string> parts, string name, Microsoft.Extensions.Primitives.StringValues values)
    {
        var raw = Single(values);
        if (raw is null) return true;
        if (!DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return false;
        parts.Add(name + "=" + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        return true;
    }

    private static void AddText(List<string> parts, string name, Microsoft.Extensions.Primitives.StringValues values)
    {
        var raw = Single(values);
        if (raw is not null) parts.Add(name + "=" + Uri.EscapeDataString(raw));
    }

    private static bool TryDecimal(string? value, out decimal amount) =>
        decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out amount);
}

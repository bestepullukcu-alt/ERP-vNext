using System.Text.Json;

namespace Diten.Web.Models.Inventory;

/// <summary>One picker option: <c>value</c> is what the filter sends, <c>text</c> what the reader sees.</summary>
public sealed record PickerOption(string Value, string Text);

/// <summary>A picker answer. <see cref="Available"/> false = the source could not be asked; the screen says so.</summary>
public sealed record PickerResult(IReadOnlyList<PickerOption> Items, bool Available)
{
    public static readonly PickerResult Unavailable = new([], false);
}

/// <summary>
/// WP-0173-PM1 — the product / SKU pickers read PRODUCT-MASTER v1.1 <c>searchStockableItems</c> (<c>GET /stockable-items</c>, a
/// contract operation — MOD-0290 §8.5 Q-1 answer, draft.8 x-notes "Sözleşme kapsamı"), always with <c>status=Active</c>: a
/// Draft, Deactivated or Retired record is never offered (the service refuses them anyway, CT-PM1-2). This closes the
/// W-UI-01 waiver: the Model A selector routes are no longer called. Every level is offered — Gsku, Lsku, FinishedGood and
/// Item (a material item, <c>skuId == itemId</c>). Every id the screen later sends is still checked by MOD-0173 against
/// PRODUCT-MASTER (fail-closed), so a picker can only narrow a query, never create a reference. No hardcoded list and no
/// fallback data: the source down = an empty picker that says it is unavailable.
/// </summary>
public sealed class ProductPickerSource
{
    /// <summary>The one route this class may call (the contract's, through the Gateway / the development bridge).</summary>
    public static readonly IReadOnlyList<string> AllowedPaths = ["/api/product-master/stockable-items"];

    public const string Path = "/api/product-master/stockable-items";

    /// <summary>Rows of one picker page (the contract allows at most 100).</summary>
    public const int PageSize = 20;

    /// <summary>The contract's page limit, used when the SKUs of one product are collected.</summary>
    public const int ScanPageSize = 100;

    /// <summary>How many pages a product-scoped SKU lookup reads before it says "refine the search".</summary>
    public const int MaxScanPages = 5;

    private readonly InventoryGatewayClient _gateway;

    public ProductPickerSource(InventoryGatewayClient gateway) => _gateway = gateway;

    /// <summary>Products → value = itemId (a Global Product or a material item), one option per item.</summary>
    public async Task<PickerResult> SearchProductsAsync(string? search, CancellationToken cancellationToken)
    {
        var page = await PageAsync(search, 1, PageSize, cancellationToken);
        if (page is null) return PickerResult.Unavailable;
        var options = page.Rows
            .GroupBy(r => r.ItemId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(r => r.SkuLevel == "Item" ? 0 : 1).First())
            .Select(r => new PickerOption(r.ItemId, ProductText(r)))
            .ToList();
        return new PickerResult(options, true);
    }

    /// <summary>SKUs of every level → value = <c>Level:skuId</c> (the contract needs skuId AND skuLevel).</summary>
    public async Task<PickerResult> SearchSkusAsync(string? search, CancellationToken cancellationToken)
    {
        var page = await PageAsync(search, 1, PageSize, cancellationToken);
        return page is null ? PickerResult.Unavailable : new PickerResult(page.Rows.Select(SkuOption).ToList(), true);
    }

    /// <summary>
    /// The SKUs OF ONE PRODUCT (pack §12 field 4): the contract has no "SKUs of product" filter, so pages of the Active
    /// stockable rows are read (≤ <see cref="MaxScanPages"/> × <see cref="ScanPageSize"/>) and kept to that itemId. A scan that hits
    /// the cap is answered as incomplete, never as "no SKU".
    /// </summary>
    public async Task<PickerResult> SearchSkusOfProductAsync(string itemId, string? search, CancellationToken cancellationToken)
    {
        var options = new List<PickerOption>();
        for (var number = 1; number <= MaxScanPages; number++)
        {
            var page = await PageAsync(search, number, ScanPageSize, cancellationToken);
            if (page is null) return new PickerResult(options, false);
            options.AddRange(page.Rows.Where(r => string.Equals(r.ItemId, itemId, StringComparison.OrdinalIgnoreCase)).Select(SkuOption));
            if (page.Count < ScanPageSize || (long)number * ScanPageSize >= page.TotalCount) return new PickerResult(options, true);
        }

        return new PickerResult(options, false);
    }

    private sealed record Row(string SkuId, string SkuLevel, string ItemId, string? Code, string? Name);

    /// <param name="Count">Rows the page carried (before the Active / known-level filter) — decides whether another page exists.</param>
    private sealed record Page(List<Row> Rows, int Count, long TotalCount);

    private async Task<Page?> PageAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var parts = new List<string> { "status=Active", "page=" + page, "pageSize=" + pageSize };
        if (!string.IsNullOrWhiteSpace(search)) parts.Add("q=" + Uri.EscapeDataString(search.Trim()));
        var reply = await _gateway.GetAsync(Path + "?" + string.Join("&", parts), cancellationToken);
        if (!reply.IsSuccess) return null;
        try
        {
            using var doc = JsonDocument.Parse(reply.Body);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) return null;
            var rows = new List<Row>();
            var count = 0;
            foreach (var item in items.EnumerateArray())
            {
                count++;
                var skuId = Text(item, "skuId");
                var level = Text(item, "skuLevel");
                var itemId = Text(item, "itemId");
                // Only the four levels the ledger knows; an unknown level (open enum) is not offered — it could not be posted.
                if (skuId is null || itemId is null || level is null || !InventoryListAdapter.SkuLevels.Contains(level, StringComparer.Ordinal)) continue;
                // status=Active was asked; a row that still says otherwise is not offered.
                if (!string.Equals(Text(item, "lifecycleStatus"), "Active", StringComparison.Ordinal)) continue;
                rows.Add(new Row(skuId, level, itemId, Text(item, "canonicalCode"), Text(item, "name")));
            }

            var total = root.TryGetProperty("totalCount", out var t) && t.TryGetInt64(out var value) ? value : count;
            return new Page(rows, count, total);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static PickerOption SkuOption(Row r) => new(r.SkuLevel + ":" + r.SkuId, Join(r.Code, r.Name) ?? r.SkuId);

    private static string ProductText(Row r) => Join(r.SkuLevel == "Item" ? r.Code : null, r.Name) ?? r.ItemId;

    private static string? Text(JsonElement item, string name) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? Join(string? code, string? name) =>
        string.IsNullOrWhiteSpace(code) ? name : string.IsNullOrWhiteSpace(name) ? code : $"{code} · {name}";
}

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
/// W-UI-01 (owner waiver, K19): the product / SKU pickers read the existing MDM **Model A** selector APIs through
/// Gateway 5000, read-only — the ONLY non-contract call in FU01, kept behind this one swappable class so that the
/// day PRODUCT-MASTER-BUNDLE gains a search operation (v1.1) only this file changes. Never <c>/api/mdm/products</c>
/// or any Model B surface (DEC-INV-17). Every id the screen later sends is still checked by MOD-0173 against
/// PRODUCT-MASTER (fail-closed), so a picker can only narrow a query, never create a reference.
/// No hardcoded list and no fallback data: MDM down = an empty picker that says it is unavailable.
/// </summary>
public sealed class ProductPickerSource
{
    /// <summary>The Model A routes this class may call (measured in ocelot.json). Nothing else.</summary>
    public static readonly IReadOnlyList<string> AllowedPaths =
        ["/api/global-products/selector", "/api/gskus", "/api/lskus", "/api/finished-goods/gsku-selector"];

    public const int PageSize = 20;

    private readonly InventoryGatewayClient _gateway;

    public ProductPickerSource(InventoryGatewayClient gateway) => _gateway = gateway;

    /// <summary>Products (Global Products) → value = itemId.</summary>
    public async Task<PickerResult> SearchProductsAsync(string? search, CancellationToken cancellationToken)
    {
        var reply = await _gateway.GetAsync("/api/global-products/selector" + Query(search), cancellationToken);
        if (!reply.IsSuccess) return PickerResult.Unavailable;
        return new PickerResult(Items(reply, item => (Text(item, "id"), Join(Text(item, "canonicalCode"), Text(item, "globalProductName")))), true);
    }

    /// <summary>SKUs at GSKU and LSKU level → value = <c>Level:skuId</c> (the contract needs skuId AND skuLevel).</summary>
    public async Task<PickerResult> SearchSkusAsync(string? search, CancellationToken cancellationToken)
    {
        var gskus = _gateway.GetAsync("/api/gskus" + Query(search), cancellationToken);
        var lskus = _gateway.GetAsync("/api/lskus" + Query(search), cancellationToken);
        var replies = await Task.WhenAll(gskus, lskus);
        if (!replies[0].IsSuccess && !replies[1].IsSuccess) return PickerResult.Unavailable;

        var options = new List<PickerOption>();
        if (replies[0].IsSuccess)
            options.AddRange(Items(replies[0], item => (Prefixed("Gsku", Text(item, "id")), Join(Text(item, "canonicalCode"), Text(item, "globalProductName")))));
        if (replies[1].IsSuccess)
            options.AddRange(Items(replies[1], item => (Prefixed("Lsku", Text(item, "id")), Join(Text(item, "canonicalCode"), Text(item, "marketCode")))));
        // Both levels must answer for the list to be complete; a half list is shown, but flagged.
        return new PickerResult(options, replies[0].IsSuccess && replies[1].IsSuccess);
    }

    private static string Query(string? search)
    {
        var parts = new List<string> { "pageNumber=1", "pageSize=" + PageSize };
        if (!string.IsNullOrWhiteSpace(search)) parts.Add("search=" + Uri.EscapeDataString(search.Trim()));
        return "?" + string.Join("&", parts);
    }

    // MDM answers Response<PagedResult<T>>: { data: { items: [...] , totalCount }, isSuccessful, … }.
    private static List<PickerOption> Items(GatewayReply reply, Func<JsonElement, (string? Value, string? Text)> map)
    {
        var options = new List<PickerOption>();
        try
        {
            using var doc = JsonDocument.Parse(reply.Body);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return options;
            var data = root.TryGetProperty("data", out var d) ? d : root;
            if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                return options;
            foreach (var item in items.EnumerateArray())
            {
                var (value, text) = map(item);
                if (!string.IsNullOrWhiteSpace(value)) options.Add(new PickerOption(value, string.IsNullOrWhiteSpace(text) ? value : text));
            }
        }
        catch (JsonException) { }
        return options;
    }

    private static string? Prefixed(string level, string? id) => string.IsNullOrWhiteSpace(id) ? null : level + ":" + id;

    private static string? Text(JsonElement item, string name) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? Join(string? code, string? name) =>
        string.IsNullOrWhiteSpace(code) ? name : string.IsNullOrWhiteSpace(name) ? code : $"{code} · {name}";
}

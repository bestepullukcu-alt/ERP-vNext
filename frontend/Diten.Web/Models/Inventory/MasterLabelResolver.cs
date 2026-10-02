using System.Text.Json;

namespace Diten.Web.Models.Inventory;

/// <summary>Display names for the ids of ONE page. A missing name is shown as the raw id (pack §8).</summary>
public sealed class MasterLabels
{
    public static readonly MasterLabels None = new(new Dictionary<string, string>(), new Dictionary<string, string>(), new Dictionary<string, string>(), false);

    private readonly IReadOnlyDictionary<string, string> _products;
    private readonly IReadOnlyDictionary<string, string> _skus;
    private readonly IReadOnlyDictionary<string, string> _locations;

    public MasterLabels(
        IReadOnlyDictionary<string, string> products,
        IReadOnlyDictionary<string, string> skus,
        IReadOnlyDictionary<string, string> locations,
        bool available)
    {
        _products = products;
        _skus = skus;
        _locations = locations;
        Available = available;
    }

    /// <summary>False when a master could not be asked at all — the screen shows the "names unavailable" banner.</summary>
    public bool Available { get; }

    public string? Product(string? id) => Lookup(_products, id);
    public string? Sku(string? id) => Lookup(_skus, id);
    public string? Location(string? id) => Lookup(_locations, id);

    private static string? Lookup(IReadOnlyDictionary<string, string> map, string? id) =>
        id is not null && map.TryGetValue(id, out var label) ? label : null;
}

/// <summary>
/// Resolves product / SKU / location names for the ids on the current page only (≤ 200 distinct ids), through the
/// Gateway contract routes PRODUCT-MASTER-BUNDLE v1 (<c>/api/product-master</c>) and LOCATION v1 (<c>/api/locations</c>)
/// — pack §8. READ PATH: an unreachable master never fails the list; the stock numbers are still right, the names
/// fall back to raw ids and the screen says so. The first answer that is not the contract's own (a Gateway 404 with
/// no contract error = no route, BG-10; a 5xx; no answer) marks that master unavailable and stops asking it.
/// </summary>
public static class MasterLabelResolver
{
    public static async Task<MasterLabels> ResolveAsync(
        InventoryGatewayClient gateway,
        IEnumerable<string?> itemIds,
        IEnumerable<(string? SkuId, string? Level)> skus,
        IEnumerable<string?> locationIds,
        CancellationToken cancellationToken)
    {
        var available = true;

        var products = new Dictionary<string, string>(StringComparer.Ordinal);
        var productIds = Distinct(itemIds);
        if (productIds.Count > 0)
        {
            var ok = await FillAsync(productIds, id => gateway.GetAsync($"/api/product-master/products/{Uri.EscapeDataString(id)}", cancellationToken),
                root => Join(Text(root, "canonicalCode"), Text(root, "name")), products);
            available &= ok;
            if (ok)
            {
                var skuMap = new Dictionary<string, string>(StringComparer.Ordinal);
                var skuList = skus.Where(s => !string.IsNullOrWhiteSpace(s.SkuId) && !string.IsNullOrWhiteSpace(s.Level))
                    .DistinctBy(s => s.SkuId).Take(InventoryListAdapter.MaxPageSize).ToList();
                var skuLevels = skuList.ToDictionary(s => s.SkuId!, s => s.Level!, StringComparer.Ordinal);
                available &= await FillAsync(skuLevels.Keys.ToList(),
                    id => gateway.GetAsync($"/api/product-master/skus/{Uri.EscapeDataString(id)}?level={Uri.EscapeDataString(skuLevels[id])}", cancellationToken),
                    root => Text(root, "canonicalCode"), skuMap);
                return await WithLocationsAsync(gateway, locationIds, products, skuMap, available, cancellationToken);
            }
        }

        return await WithLocationsAsync(gateway, locationIds, products, new Dictionary<string, string>(), available, cancellationToken);
    }

    private static async Task<MasterLabels> WithLocationsAsync(
        InventoryGatewayClient gateway,
        IEnumerable<string?> locationIds,
        Dictionary<string, string> products,
        Dictionary<string, string> skus,
        bool available,
        CancellationToken cancellationToken)
    {
        var locations = new Dictionary<string, string>(StringComparer.Ordinal);
        var ids = Distinct(locationIds);
        if (ids.Count > 0)
        {
            available &= await FillAsync(ids, id => gateway.GetAsync($"/api/locations/{Uri.EscapeDataString(id)}", cancellationToken),
                root => Join(Text(root, "code"), Text(root, "name")), locations);
        }

        return new MasterLabels(products, skus, locations, available);
    }

    /// <returns>False when the master is unavailable (stop asking it).</returns>
    private static async Task<bool> FillAsync(
        IReadOnlyList<string> ids,
        Func<string, Task<GatewayReply>> fetch,
        Func<JsonElement, string?> label,
        Dictionary<string, string> into)
    {
        // Probe with the first id: if the route is not there, one request tells us — not 200.
        var first = await fetch(ids[0]);
        if (!Record(ids[0], first, label, into)) return false;

        var rest = await Task.WhenAll(ids.Skip(1).Select(async id => (Id: id, Reply: await fetch(id))));
        var ok = true;
        foreach (var (id, reply) in rest) ok &= Record(id, reply, label, into);
        return ok;
    }

    private static bool Record(string id, GatewayReply reply, Func<JsonElement, string?> label, Dictionary<string, string> into)
    {
        if (reply.IsSuccess)
        {
            try
            {
                using var doc = JsonDocument.Parse(reply.Body);
                var text = label(doc.RootElement);
                if (!string.IsNullOrWhiteSpace(text)) into[id] = text;
            }
            catch (JsonException) { }
            return true;
        }

        // The contract's own "not found" (UNKNOWN_ITEM / UNKNOWN_SKU / UNKNOWN_LOCATION) is an answer: the master is up.
        return reply.StatusCode == 404 && reply.Error?.Code?.StartsWith("UNKNOWN_", StringComparison.Ordinal) == true;
    }

    private static List<string> Distinct(IEnumerable<string?> ids) =>
        ids.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!).Distinct(StringComparer.Ordinal)
            .Take(InventoryListAdapter.MaxPageSize).ToList();

    private static string? Text(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    // A bulk-created bin's name IS its code (FU02 bulk bins): shown once.
    private static string? Join(string? code, string? name) =>
        string.IsNullOrWhiteSpace(code) ? name
        : string.IsNullOrWhiteSpace(name) || string.Equals(name, code, StringComparison.OrdinalIgnoreCase) ? code
        : $"{code} · {name}";
}

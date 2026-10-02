using System.Text.Json;

namespace Diten.Web.Models.Inventory;

/// <summary>
/// Warehouse / location pickers — LOCATION v1 <c>listLocations(type, parentId)</c> through the Gateway
/// (<c>/api/locations</c>, pack §19). BG-10: there is no such Gateway route and no real LOCATION service yet, so
/// today this answers "unavailable" and the screen says so. No hardcoded list, no fallback ids.
/// </summary>
public sealed class LocationPickerSource
{
    private readonly InventoryGatewayClient _gateway;

    public LocationPickerSource(InventoryGatewayClient gateway) => _gateway = gateway;

    public Task<PickerResult> WarehousesAsync(CancellationToken cancellationToken) =>
        ListAsync("?type=Warehouse", cancellationToken);

    public Task<PickerResult> LocationsAsync(string warehouseId, CancellationToken cancellationToken) =>
        ListAsync("?parentId=" + Uri.EscapeDataString(warehouseId), cancellationToken);

    /// <summary>How many pages of 200 the target-location lookup reads before it answers "refine the search".</summary>
    public const int MaxSearchPages = 10;

    /// <summary>
    /// UI-2 S-3 target warehouse (pack §12 field 6): ACTIVE warehouses only — FU02 K-13: an Inactive or Archived
    /// location is not offered for a posting (the service refuses it as UNKNOWN_LOCATION anyway).
    /// </summary>
    public async Task<PickerResult> ActiveWarehousesAsync(CancellationToken cancellationToken)
    {
        var page = await SearchAllAsync("&type=Warehouse&status=Active", cancellationToken);
        if (page is null) return PickerResult.Unavailable;
        return new PickerResult(page.Items.Select(l => new PickerOption(l.Id, l.Label)).ToList(), page.Complete);
    }

    /// <summary>
    /// UI-2 S-3 target location (pack §12 field 7; FU02 K-13): the ACTIVE, STOCKABLE locations anywhere under the
    /// warehouse. LOCATION v1.1 search filters by direct parent only, so the company's locations below warehouse level
    /// are read (every status — an inactive zone still links its bins to the warehouse) and the warehouse's subtree is
    /// walked here by <c>parentId</c>; only then are Active + stockable kept. <paramref name="search"/> narrows by code
    /// or name. The service re-checks Active / stockable / under-the-warehouse when it posts.
    /// </summary>
    public async Task<PickerResult> StockableLocationsUnderAsync(string warehouseId, string? search, CancellationToken cancellationToken)
    {
        var page = await SearchAllAsync("&type=StorageLocation&type=Zone&type=Bin", cancellationToken);
        if (page is null) return PickerResult.Unavailable;

        var children = page.Items.Where(l => l.ParentId is not null).ToLookup(l => l.ParentId!, StringComparer.Ordinal);
        var under = new List<LocationNode>();
        var frontier = new Queue<string>([warehouseId]);
        var seen = new HashSet<string>(StringComparer.Ordinal) { warehouseId };
        while (frontier.Count > 0)
        {
            foreach (var child in children[frontier.Dequeue()])
            {
                if (!seen.Add(child.Id)) continue;
                under.Add(child);
                frontier.Enqueue(child.Id);
            }
        }

        var term = search?.Trim();
        var options = under
            .Where(l => l.Status == "Active" && l.IsStockable)
            .Where(l => string.IsNullOrEmpty(term)
                        || l.Code.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || l.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(l => l.Code, StringComparer.OrdinalIgnoreCase)
            .Select(l => new PickerOption(l.Id, l.Label))
            .ToList();
        return new PickerResult(options, page.Complete);
    }

    private sealed record LocationNode(string Id, string Code, string Name, string? ParentId, string? Status, bool IsStockable)
    {
        // Bulk-created bins carry their code as their name: "A-01-01-01", not "A-01-01-01 · A-01-01-01".
        public string Label => string.IsNullOrWhiteSpace(Name) || string.Equals(Name, Code, StringComparison.OrdinalIgnoreCase) ? Code : $"{Code} · {Name}";
    }

    private sealed record SearchPage(List<LocationNode> Items, bool Complete);

    private async Task<SearchPage?> SearchAllAsync(string filters, CancellationToken cancellationToken)
    {
        var items = new List<LocationNode>();
        for (var page = 1; page <= MaxSearchPages; page++)
        {
            var reply = await _gateway.GetAsync($"/api/locations/search?page={page}&pageSize=200{filters}", cancellationToken);
            if (!reply.IsSuccess) return null;

            int? total = null;
            var count = 0;
            try
            {
                using var doc = JsonDocument.Parse(reply.Body);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("items", out var list) || list.ValueKind != JsonValueKind.Array)
                    return null;
                if (root.TryGetProperty("total", out var t) && t.TryGetInt32(out var value)) total = value;
                foreach (var item in list.EnumerateArray())
                {
                    count++;
                    var id = Text(item, "locationId");
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    items.Add(new LocationNode(id, Text(item, "code") ?? id, Text(item, "name") ?? string.Empty, Text(item, "parentId"),
                        Text(item, "status"), item.TryGetProperty("isStockable", out var s) && s.ValueKind == JsonValueKind.True));
                }
            }
            catch (JsonException)
            {
                return null;
            }

            if (count < 200 || (total is not null && items.Count >= total)) return new SearchPage(items, true);
        }

        return new SearchPage(items, false);
    }

    private async Task<PickerResult> ListAsync(string query, CancellationToken cancellationToken)
    {
        var reply = await _gateway.GetAsync("/api/locations" + query, cancellationToken);
        if (!reply.IsSuccess) return PickerResult.Unavailable;

        var options = new List<PickerOption>();
        try
        {
            using var doc = JsonDocument.Parse(reply.Body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("items", out var items)
                && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    var id = Text(item, "locationId");
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    var code = Text(item, "code");
                    var name = Text(item, "name");
                    var label = string.IsNullOrWhiteSpace(code) ? name
                        : string.IsNullOrWhiteSpace(name) || string.Equals(name, code, StringComparison.OrdinalIgnoreCase) ? code
                        : $"{code} · {name}";
                    options.Add(new PickerOption(id, string.IsNullOrWhiteSpace(label) ? id : label));
                }
            }
        }
        catch (JsonException)
        {
            return PickerResult.Unavailable;
        }

        return new PickerResult(options, true);
    }

    private static string? Text(JsonElement item, string name) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

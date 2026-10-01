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
                    var label = string.IsNullOrWhiteSpace(code) ? name : string.IsNullOrWhiteSpace(name) ? code : $"{code} · {name}";
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

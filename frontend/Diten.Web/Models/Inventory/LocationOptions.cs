using System.Text.Json;

namespace Diten.Web.Models.Inventory;

/// <summary>
/// WP-MVP1-FIX-LIVE1 — the ONE reader of LOCATION list rows for pickers (<c>listLocations</c> <c>{ items: LocationRef[] }</c> and
/// <c>searchLocations</c> <c>LocationSearchPage</c>). The row identity is <c>LocationRef.locationId</c>
/// (docs/analysis/contracts/location.openapi.yaml, <c>components.schemas.LocationRef.properties.locationId</c>) — never <c>id</c>.
/// The 2026-10-14 live finding: Expiry and Quarantine read <c>id</c>, so every option value was <c>null</c> and the FEFO order could
/// not be computed. A row without <c>locationId</c> is skipped (no option without a value). The label is "code · name", the code
/// alone when the name repeats it (bulk-created bins carry their code as their name).
/// </summary>
public static class LocationOptions
{
    /// <summary>The LOCATION contract's row identity (location.openapi.yaml LocationRef / LocationDetail).</summary>
    public const string IdField = "locationId";

    public sealed record Option(string Value, string Text);

    /// <summary>The picker options of a LOCATION list / search body; <c>null</c> when the body is not JSON.</summary>
    public static IReadOnlyList<Option>? Parse(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("items", out var items)
                || items.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var options = new List<Option>();
            foreach (var item in items.EnumerateArray())
            {
                var id = Text(item, IdField);
                if (string.IsNullOrWhiteSpace(id)) continue;
                var label = Label(Text(item, "code"), Text(item, "name"));
                options.Add(new Option(id, string.IsNullOrWhiteSpace(label) ? id : label));
            }

            return options;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string? Label(string? code, string? name) =>
        string.IsNullOrWhiteSpace(code) ? name
        : string.IsNullOrWhiteSpace(name) || string.Equals(name, code, StringComparison.OrdinalIgnoreCase) ? code
        : $"{code} · {name}";

    private static string? Text(JsonElement item, string name) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

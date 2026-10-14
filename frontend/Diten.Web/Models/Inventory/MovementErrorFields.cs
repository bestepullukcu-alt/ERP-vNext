using System.Text.Json;

namespace Diten.Web.Models.Inventory;

/// <summary>
/// UI-2: WHERE a MOD-0173 refusal of a movement is shown (pack §13 — "field-level message"). The sentence comes from
/// <see cref="InventoryErrorMessages"/>; this class picks the field it belongs to, or the level above it:
/// <list type="bullet">
/// <item><c>field</c> — the form stays filled and the sentence sits under that field;</item>
/// <item><c>form</c> — a refusal that is about the request as a whole (company, permission, invalid request);</item>
/// <item><c>page</c> — an upstream that is down (503 / no answer): "no change was made, retry" with the SAME key;</item>
/// <item><c>restart</c> — IDEMPOTENCY_KEY_REUSED: this form was already submitted with other values; start a new one.</item>
/// </list>
/// </summary>
public static class MovementErrorFields
{
    public const string ScopeField = "field";
    public const string ScopeForm = "form";
    public const string ScopePage = "page";
    public const string ScopeRestart = "restart";

    public static (string Scope, string? Field) Place(string? code, int status, IReadOnlyDictionary<string, object?>? details,
        MovementRules.Direction? direction, MovementRules.SourceRow? source)
    {
        if (code == "IDEMPOTENCY_KEY_REUSED") return (ScopeRestart, null);
        if (status is 0 or 502 or 503 or 504) return (ScopePage, null);

        var field = code switch
        {
            "INSUFFICIENT_STOCK" => MovementRules.Fields.Quantity,
            "LOT_REQUIRED" => MovementRules.Fields.Lot,
            "SERIAL_COUNT_MISMATCH" => MovementRules.Fields.Serials,
            "UOM_CONVERSION_MISSING" => MovementRules.Fields.Unit,
            "UNKNOWN_ITEM" => MovementRules.Fields.Product,
            "SKU_ITEM_MISMATCH" => MovementRules.Fields.Sku,
            "INVALID_MOVEMENT_TYPE" => MovementRules.Fields.MovementType,
            "POSTING_PERIOD_CLOSED" => MovementRules.Fields.PostingDate,
            "UNKNOWN_LOCATION" => LocationField(details, direction, source),
            _ => null
        };
        return field is null ? (ScopeForm, null) : (ScopeField, field);
    }

    /// <summary>An unknown location is the SOURCE when the refused id is the source's (or the movement only has a source).</summary>
    private static string LocationField(IReadOnlyDictionary<string, object?>? details, MovementRules.Direction? direction, MovementRules.SourceRow? source)
    {
        if (direction == MovementRules.Direction.Outbound) return MovementRules.Fields.Source;
        var refused = Detail(details, "locationId");
        return source is not null && refused is not null && string.Equals(refused, source.LocationId, StringComparison.Ordinal)
            ? MovementRules.Fields.Source
            : MovementRules.Fields.TargetLocation;
    }

    /// <summary>A contract <c>error.details</c> value as text (it arrives as a JsonElement).</summary>
    public static string? Detail(IReadOnlyDictionary<string, object?>? details, string name)
    {
        if (details is null || !details.TryGetValue(name, out var value) || value is null) return null;
        return value is JsonElement element
            ? element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.GetRawText(),
                _ => null
            }
            : value.ToString();
    }
}

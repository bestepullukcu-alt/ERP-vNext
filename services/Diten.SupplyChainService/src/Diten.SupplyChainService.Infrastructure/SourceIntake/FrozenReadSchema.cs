using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.SupplyChainService.Application.Features.SourceIntake.Contracts;

namespace Diten.SupplyChainService.Infrastructure.SourceIntake;

/// <summary>Validates the consumed frozen read surface. OAS 3.1 nullable does not permit JSON null (GAP-0183-03).</summary>
internal static class FrozenReadSchema
{
    internal static readonly string[] SkuLevels = ["Gsku", "Lsku", "FinishedGood"];
    private static readonly string[] Statuses = ["Allocated", "Picked", "Packed", "ReadyToShip", "Shipped", "Cancelled"];
    private static readonly string[] StockStatuses = ["AVAILABLE", "QUALITY_INSPECTION", "QUARANTINE", "BLOCKED", "IN_TRANSIT", "DAMAGED", "EXPIRED"];
    private static bool Object(JsonElement e) => e.ValueKind == JsonValueKind.Object &&
        e.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() == e.EnumerateObject().Count();
    private static bool String(JsonElement e) => e.ValueKind == JsonValueKind.String;
    private static bool Uuid(JsonElement e) => String(e) && Guid.TryParseExact(e.GetString(), "D", out _);
    private static bool Date(JsonElement e) => String(e) &&
        Regex.IsMatch(e.GetString()!, @"^[0-9]{4}-[0-9]{2}-[0-9]{2}[Tt][0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]+)?([Zz]|[+-][0-9]{2}:[0-9]{2})$", RegexOptions.CultureInvariant) &&
        DateTimeOffset.TryParse(e.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    private static bool Decimal(JsonElement e) => String(e) && Regex.IsMatch(e.GetString()!, @"^-?[0-9]+(\.[0-9]+)?$", RegexOptions.CultureInvariant);
    private static bool Optional(JsonElement e, string name, Func<JsonElement, bool> check) => !e.TryGetProperty(name, out var value) || check(value);
    private static bool Required(JsonElement e, string name, Func<JsonElement, bool> check) => e.TryGetProperty(name, out var value) && check(value);
    private static bool Array(JsonElement e, Func<JsonElement, bool> check) => e.ValueKind == JsonValueKind.Array && e.EnumerateArray().All(check);
    private static bool Strings(JsonElement e, params string[] names) => names.All(n => Optional(e, n, String));
    private static bool Sku(JsonElement e) => String(e) && SkuLevels.Contains(e.GetString());

    internal static bool Warehouse(JsonElement e) => Object(e) && Required(e, "outboundId", String) && Required(e, "warehouseId", String) &&
        Required(e, "status", v => String(v) && Statuses.Contains(v.GetString())) && Required(e, "shipTo", Address) &&
        Required(e, "lines", v => Array(v, Line)) && Strings(e, "orderRef", "contractVersion") && Optional(e, "readyAt", Date) &&
        Optional(e, "packages", v => Array(v, Package));
    private static bool Address(JsonElement e) => Object(e) && Required(e, "name", String) && Required(e, "country", String) &&
        Strings(e, "city", "postalCode", "line1", "line2");
    private static bool Line(JsonElement e) => Object(e) && Required(e, "itemId", Uuid) && Required(e, "skuId", Uuid) &&
        Required(e, "skuLevel", Sku) && Required(e, "quantity", Decimal) && Required(e, "uomId", String) &&
        Strings(e, "orderLineId", "lotNumber") && Optional(e, "serialIds", v => Array(v, String));
    private static bool Package(JsonElement e) => Object(e) && Required(e, "packageId", String) &&
        Optional(e, "weightKg", Decimal) && Strings(e, "dimensionsCm");
    internal static bool WarehousePage(JsonElement e) => Object(e) && Optional(e, "items", v => Array(v, Warehouse)) && Strings(e, "nextCursor", "contractVersion");

    internal static bool Inventory(JsonElement e, bool balance, Guid legalEntityId, InventoryReference reference)
        => balance
            ? Object(e) && Optional(e, "rows", rows => Array(rows, row => InventoryRow(row, legalEntityId, reference, true))) &&
              Optional(e, "total", total => total.ValueKind == JsonValueKind.Number && total.TryGetInt64(out _)) && Strings(e, "contractVersion")
            : InventoryRow(e, legalEntityId, reference, false);

    private static bool InventoryRow(JsonElement e, Guid legalEntityId, InventoryReference reference, bool balance)
    {
        if (!Object(e) || !Strings(e, "itemId", "skuId", "legalEntityId", "baseUomId", "contractVersion") ||
            !Optional(e, "skuLevel", Sku) || !new[] { "onHand", "reserved", "available" }.All(n => Optional(e, n, Decimal))) return false;
        // Supplied identity must match the trusted request; absent optional fields are permitted by the frozen schema.
        if (!Optional(e, "legalEntityId", v => v.GetString() == legalEntityId.ToString()) ||
            !Optional(e, "itemId", v => v.GetString() == reference.ItemId.ToString()) ||
            !Optional(e, "skuId", v => v.GetString() == reference.SkuId.ToString()) ||
            !Optional(e, "skuLevel", v => v.GetString() == reference.SkuLevel)) return false;
        if (balance) return Strings(e, "warehouseId", "locationId", "lotId", "lastMovementId") &&
            Optional(e, "stockStatus", v => String(v) && StockStatuses.Contains(v.GetString())) &&
            new[] { "inTransit", "blocked", "qualityInspection", "baseQuantity" }.All(n => Optional(e, n, Decimal)) &&
            Optional(e, "lastPostingDate", Date) && Match(e, "warehouseId", reference.WarehouseId) && Match(e, "locationId", reference.LocationId) && Match(e, "lotId", reference.LotId);
        return Optional(e, "asOf", Date) && Optional(e, "byStatus", v => Object(v) && v.EnumerateObject().All(p => Decimal(p.Value))) &&
            Optional(e, "scope", v => Object(v) && v.EnumerateObject().All(p => String(p.Value)) &&
                Match(v, "warehouseId", reference.WarehouseId) && Match(v, "locationId", reference.LocationId));
    }
    private static bool Match(JsonElement e, string name, string? expected) => expected is null || Optional(e, name, v => v.GetString() == expected);
}

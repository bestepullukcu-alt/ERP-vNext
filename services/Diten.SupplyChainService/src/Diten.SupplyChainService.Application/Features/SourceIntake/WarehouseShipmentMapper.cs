using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.SupplyChainService.Domain.Features.Shipments;
using Diten.SupplyChainService.Domain.Features.SourceIntake;
namespace Diten.SupplyChainService.Application.Features.SourceIntake;

public static class WarehouseShipmentMapper
{
    public const string Version = "warehouse-shipment-v1";
    public static string Canonical(JsonElement value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, value);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    private static void Write(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
            { writer.WritePropertyName(property.Name); Write(writer, property.Value); }
            writer.WriteEndObject();
        }
        else if (value.ValueKind == JsonValueKind.Array)
        { writer.WriteStartArray(); foreach (var child in value.EnumerateArray()) Write(writer, child); writer.WriteEndArray(); }
        else value.WriteTo(writer);
    }
    public static string Key(ShipmentScope scope, string outbound)
    {
        var identity = new[] { scope.TenantId.ToString(), scope.LegalEntityId.ToString(), "MOD-0178", "WAREHOUSE_OUTBOUND", outbound };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Concat(identity.Select(s => $"{Encoding.UTF8.GetByteCount(s)}:{s}")))));
    }
    public static string Hash(string snapshot) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot)));
    public static SourceIntent Map(ShipmentScope scope, JsonElement detail, Guid correlation, DateTimeOffset fetchedAt,
        Guid? sourceCorrelationId = null)
    {
        var outbound = detail.GetProperty("outboundId").GetString()!;
        var canonical = Canonical(detail);
        var key = Key(scope, outbound);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        var ready = detail.TryGetProperty("readyAt", out var date) && date.ValueKind == JsonValueKind.String;
        var planned = ready ? DateTimeOffset.Parse(date.GetString()!, CultureInfo.InvariantCulture).ToUniversalTime() : fetchedAt;
        var id = Guid.NewGuid();
        var shipment = new Shipment { Id = id, TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
            CreatedBy = scope.ActorId, CreatedAt = fetchedAt, Version = 1, ShipmentNumber = $"SHP-{id:N}",
            SourceModule = "MOD-0178", SourceType = "WAREHOUSE_OUTBOUND", SourceDocumentId = outbound,
            WarehouseReferenceId = detail.GetProperty("warehouseId").GetString()!,
            ShipToReference = $"warehouse-outbound:{Uri.EscapeDataString(outbound)}:ship-to",
            PlannedShipAt = planned, PlannedDeliverAt = null, Status = ShipmentStatus.Draft, CorrelationId = correlation,
            Lines = detail.GetProperty("lines").EnumerateArray().Select((line, index) => new ShipmentLine(
                (index + 1).ToString(CultureInfo.InvariantCulture), line.GetProperty("itemId").GetGuid(), line.GetProperty("skuId").GetGuid(),
                line.GetProperty("quantity").GetString()!, line.GetProperty("uomId").GetString()!, null)).ToArray() };
        return new SourceIntent(key, canonical, hash, Version, shipment, JsonSerializer.Serialize(new {
            plannedShipAt = planned, plannedShipAtSource = ready ? "readyAt" : "firstSuccessfulFetchUtc",
            plannedDeliverAt = (string?)null, inventoryReconciliation = "NotApplicable", inventoryReferenceId = (string?)null,
            shipToReference = shipment.ShipToReference, mappingVersion = Version }), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            sourceCorrelationId, correlation, sourceCorrelationId.HasValue ? "TrustedSuppliedRoot" : "LocalPollRoot");
    }
}

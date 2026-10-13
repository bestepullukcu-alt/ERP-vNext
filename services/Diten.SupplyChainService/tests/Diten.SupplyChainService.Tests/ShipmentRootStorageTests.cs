using Diten.SupplyChainService.Persistence.Features.Shipments;
using Diten.SupplyChainService.Domain.Features.Shipments;
using MongoDB.Bson;
using Xunit;

namespace Diten.SupplyChainService.Tests;

public sealed class ShipmentRootStorageTests
{
    private static BsonDocument Base() => new()
    {
        { "_id", Guid.NewGuid().ToString() }, { "TenantId", Guid.NewGuid().ToString() },
        { "LegalEntityId", Guid.NewGuid().ToString() }, { "IsDeleted", false },
        { "ShipmentNumber", "S-ROOT" }, { "SourceModule", "MOD-0141" }, { "SourceType", "SALES_ORDER" },
        { "SourceDocumentId", "SO-1" }, { "WarehouseReferenceId", "WH-1" }, { "ShipToReference", "C-1" },
        { "Lines", new BsonArray() }, { "PlannedShipAt", DateTime.UtcNow }, { "Status", "Draft" },
        { "CorrelationId", Guid.NewGuid().ToString() }, { "Version", 1 }, { "CreatedAt", DateTime.UtcNow }
    };

    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("malformed")]
    [InlineData("valid")]
    [InlineData("nil")]
    public void PresenceAwareRootStatesAreDistinct(string fixture)
    {
        var doc = Base();
        if (fixture == "null") doc["LifecycleCorrelationId"] = BsonNull.Value;
        if (fixture == "malformed") doc["LifecycleCorrelationId"] = "not-a-uuid";
        if (fixture == "valid") doc["LifecycleCorrelationId"] = Guid.NewGuid().ToString();
        if (fixture == "nil") doc["LifecycleCorrelationId"] = Guid.Empty.ToString();
        var result = ShipmentDetailMaterializer.Materialize(doc);
        Assert.Equal(fixture switch
        {
            "missing" => RawRootState.Missing, "null" => RawRootState.ExplicitNull,
            "malformed" => RawRootState.InvalidStoredValue, _ => RawRootState.PresentStoredUuid
        }, result.RootState);
        Assert.Equal(fixture is "valid" or "nil" ? Guid.Parse(doc["LifecycleCorrelationId"].AsString) : null, result.LifecycleCorrelationId);
        Assert.False(doc.Contains("LifecycleCorrelationId") && result.Shipment is null);
    }

    [Fact]
    public void MaterializationDoesNotMutateRawDocument()
    {
        var doc = Base(); doc["LifecycleCorrelationId"] = "broken";
        var before = doc.ToJson();
        _ = ShipmentDetailMaterializer.Materialize(doc);
        Assert.Equal(before, doc.ToJson());
    }
}

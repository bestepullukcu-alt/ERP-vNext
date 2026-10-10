using Diten.SupplyChainService.Domain.Features.Loads;
using Diten.SupplyChainService.Persistence.Features.Loads;
using MongoDB.Bson;
using Xunit;

namespace Diten.SupplyChainService.Tests.Loads;

// MOD-0185 producer uptake (DRAFT, not built): presence-aware read of the stored Load root.
// Covers LU-04, LU-05, LU-06, LU-07 at the materializer level (pattern: ShipmentRootStorageTests).
public sealed class LoadRootStorageTests
{
    private const string RootField = "CorrelationRoot";

    private static BsonDocument Base() => new()
    {
        { "_id", Guid.NewGuid().ToString() }, { "TenantId", Guid.NewGuid().ToString() },
        { "IsDeleted", false }, { "CreatedAt", DateTime.UtcNow }, { "Version", 1 },
        { "LegalEntityId", Guid.NewGuid().ToString() }, { "CreatedBy", Guid.NewGuid().ToString() },
        { "LoadNumber", "LOAD-ROOT" }, { "CarrierId", Guid.NewGuid().ToString() },
        { "ShipmentIds", new BsonArray { Guid.NewGuid().ToString() } }, { "Mode", "Road" },
        { "PlannedDepartAt", "2030-01-01T00:00:00Z" },
        { "Stops", new BsonArray
            {
                new BsonDocument { { "Sequence", "1" }, { "LocationReferenceId", "" }, { "Action", "Pickup" } },
                new BsonDocument { { "Sequence", "2" }, { "LocationReferenceId", "destination" }, { "Action", "Delivery" } }
            } },
        { "Status", "Draft" }, { "LastEventId", Guid.NewGuid().ToString() }
    };

    public static TheoryData<string> Fixtures => new()
    {
        "missing", "null", "nil", "valid", "uppercase", "malformed", "braced", "empty", "int32", "binary-uuid"
    };

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void PresenceAwareRootStatesAreDistinct(string fixture)
    {
        var doc = Base();
        var value = Guid.NewGuid();
        switch (fixture)
        {
            case "null": doc[RootField] = BsonNull.Value; break;
            case "nil": doc[RootField] = Guid.Empty.ToString(); break;
            case "valid": doc[RootField] = value.ToString(); break;
            case "uppercase": doc[RootField] = value.ToString().ToUpperInvariant(); break;
            case "malformed": doc[RootField] = "not-a-uuid"; break;
            case "braced": doc[RootField] = "{" + value + "}"; break;
            case "empty": doc[RootField] = ""; break;
            case "int32": doc[RootField] = 7; break;
            case "binary-uuid": doc[RootField] = new BsonBinaryData(value, GuidRepresentation.Standard); break;
        }

        var result = LoadRootMaterializer.Materialize(doc);

        var expectedState = fixture switch
        {
            "missing" => LoadRootState.Missing,
            "null" => LoadRootState.ExplicitNull,
            "nil" or "valid" or "uppercase" => LoadRootState.PresentStoredUuid,
            _ => LoadRootState.InvalidStoredValue
        };
        Guid? expectedValue = fixture switch
        {
            "nil" => Guid.Empty,
            "valid" or "uppercase" => value,
            _ => null
        };
        Assert.Equal(expectedState, result.RootState);
        Assert.Equal(expectedValue, result.LifecycleCorrelationId);
        Assert.Equal(Guid.Parse(doc["_id"].AsString), result.Load.Id);
        Assert.Equal(LoadStatus.Draft, result.Load.Status);
    }

    [Fact]
    public void MissingRootIsNotSynthesizedAsNil()
    {
        var result = LoadRootMaterializer.Materialize(Base());
        Assert.Equal(LoadRootState.Missing, result.RootState);
        Assert.Null(result.LifecycleCorrelationId);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("valid")]
    public void MaterializationDoesNotMutateRawDocument(string fixture)
    {
        var doc = Base();
        if (fixture == "malformed") doc[RootField] = "broken";
        if (fixture == "valid") doc[RootField] = Guid.NewGuid().ToString();
        var before = doc.ToJson();
        _ = LoadRootMaterializer.Materialize(doc);
        Assert.Equal(before, doc.ToJson());
        Assert.Equal(fixture != "missing", doc.Contains(RootField));
    }
}

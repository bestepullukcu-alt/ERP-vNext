using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// MOD-0173-FU03 fault injection without a test seam in production code: a Mongo collection validator that refuses ONE kind of document of
/// ONE test tenant (every other document passes the <c>$or</c>), so the production write fails exactly where the test wants it — inside the
/// production transaction. The validator is removed on dispose. Tests using it run in <see cref="FaultInjectionCollection"/>, alone, so the
/// <c>collMod</c> of a shared collection never meets another test's transaction.
/// </summary>
public sealed class CollectionValidatorFault : IAsyncDisposable
{
    private readonly IMongoDatabase _database;
    private readonly string _collection;

    private CollectionValidatorFault(IMongoDatabase database, string collection)
    {
        _database = database;
        _collection = collection;
    }

    /// <summary>Refuses an insert / update of <paramref name="collection"/> whose TenantId is <paramref name="tenant"/> and whose <paramref name="field"/> equals <paramref name="value"/>.</summary>
    public static async Task<CollectionValidatorFault> RefuseAsync(IMongoDatabase database, string collection, Guid tenant, string field, BsonValue value)
    {
        var validator = new BsonDocument("$or", new BsonArray
        {
            new BsonDocument("TenantId", new BsonDocument("$ne", tenant.ToString())),
            new BsonDocument(field, new BsonDocument("$ne", value))
        });
        await database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            { "collMod", collection }, { "validator", validator }, { "validationLevel", "strict" }, { "validationAction", "error" }
        });
        return new CollectionValidatorFault(database, collection);
    }

    public async ValueTask DisposeAsync()
        => await _database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            { "collMod", _collection }, { "validator", new BsonDocument() }, { "validationLevel", "strict" }, { "validationAction", "error" }
        });
}

/// <summary>Fault-injection tests run alone: xUnit starts a DisableParallelization collection after the parallel ones have finished.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class FaultInjectionCollection
{
    public const string Name = "MOD-0173-FU03 fault injection (collection validators on shared collections)";
}

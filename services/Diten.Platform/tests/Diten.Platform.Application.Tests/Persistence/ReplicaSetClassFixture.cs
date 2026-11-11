using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

/// <summary>
/// TEST-HIZ-01 — ONE test-owned replica set (never the shared 27017) for a whole test class, not one per test: starting a
/// mongod was ~0.9 s of every test in the classes that started one each. A test still starts on an EMPTY database:
/// <see cref="FreshDatabaseAsync"/> drops the class's fixed-name database (DB-010) and hands it back, so nothing a test
/// asserts can see what the test before it wrote.
/// </summary>
public sealed class ReplicaSetClassFixture : IAsyncLifetime
{
    private DisposableMongoReplicaSet? _mongo;

    public DisposableMongoReplicaSet Mongo =>
        _mongo ?? throw new InvalidOperationException("The class's disposable replica set was not started.");

    public async Task InitializeAsync() => _mongo = await DisposableMongoReplicaSet.StartAsync();

    /// <summary>The class's one database, emptied: dropped first, so the test sees no collection, document or index.</summary>
    public static async Task<IMongoDatabase> FreshDatabaseAsync(DisposableMongoReplicaSet mongo, string name)
    {
        await mongo.Client.DropDatabaseAsync(name);
        return mongo.Client.GetDatabase(name);
    }

    /// <summary>The same, for a harness that builds its database in a constructor or another synchronous helper.</summary>
    public static IMongoDatabase FreshDatabase(DisposableMongoReplicaSet mongo, string name)
    {
        mongo.Client.DropDatabase(name);
        return mongo.Client.GetDatabase(name);
    }

    public async Task DisposeAsync()
    {
        if (_mongo is not null)
        {
            await _mongo.DisposeAsync();
        }
    }
}

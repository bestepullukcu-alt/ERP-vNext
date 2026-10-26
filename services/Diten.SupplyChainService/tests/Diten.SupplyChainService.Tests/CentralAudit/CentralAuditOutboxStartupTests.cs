using Diten.BuildingBlocks.CentralAudit;
using Diten.SupplyChainService.Persistence;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
namespace Diten.SupplyChainService.Tests.CentralAudit;

/// <summary>
/// BL-501 (WP-A1 FIX1) — the start-up gate creates the central-audit outbox before the service takes traffic. Without it
/// the shared dispatcher creates the collection in the background while the first command's transaction creates it
/// implicitly; measured 2026-10-21 in the full suite: "Conflicted registering namespace" and a Carrier create answered 500.
/// </summary>
public sealed class CentralAuditOutboxStartupTests
{
    private static string Connection => Environment.GetEnvironmentVariable("MOD0183_TEST_MONGO")
        ?? throw new InvalidOperationException("MOD0183_TEST_MONGO must point to an isolated replica set.");

    [Fact]
    public async Task The_schema_gate_creates_the_outbox_collection_before_any_command()
    {
        var client = new MongoClient(Connection);
        var name = "sce_outbox_startup_" + Guid.NewGuid().ToString("N")[..12];
        var db = client.GetDatabase(name);
        try
        {
            await new ShipmentSchema(db).StartAsync(CancellationToken.None);
            var collections = await (await db.ListCollectionNamesAsync()).ToListAsync();
            Assert.Contains(CentralAuditOutboxRecord.CollectionName, collections);
            var indexes = await (await db.GetCollection<BsonDocument>(CentralAuditOutboxRecord.CollectionName).Indexes.ListAsync()).ToListAsync();
            Assert.True(indexes.Count > 1, "the outbox has only its _id index: the dispatcher's indexes were not created at start-up");
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }
}

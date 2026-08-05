using Diten.DecisionIntelligenceService.Persistence.Modules.OutcomeTracking.Manifests;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

public class MongoEvidenceTests
{
    [Fact]
    public async Task Replica_set_indexes_tenant_concurrency_and_all_participant_rollback_are_real()
    {
        await InitializeReplicaSet();
        var client = new MongoClient("mongodb://127.0.0.1:27020/?replicaSet=mod0072rs&directConnection=true");
        var name = "mod0072_" + Guid.NewGuid().ToString("N");
        var db = client.GetDatabase(name);
        try
        {
            foreach (var collection in OutcomeTrackingMongoOwnershipManifest.Collections) await db.CreateCollectionAsync(collection);
            foreach (var index in OutcomeTrackingMongoOwnershipManifest.Indexes)
            {
                var keys = Builders<BsonDocument>.IndexKeys.Combine(index.Keys.Select(k => Builders<BsonDocument>.IndexKeys.Ascending(k)));
                await db.GetCollection<BsonDocument>(index.Collection).Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(keys, new() { Name = index.Name, Unique = index.Name.StartsWith("ux_", StringComparison.Ordinal) }));
            }
            var listed = 0;
            foreach (var collection in OutcomeTrackingMongoOwnershipManifest.Collections)
            {
                using var cursor = await db.GetCollection<BsonDocument>(collection).Indexes.ListAsync();
                listed += (await cursor.ToListAsync()).Count - 1;
            }
            Assert.Equal(16, listed);

            var outcomes = db.GetCollection<BsonDocument>("diten_di_outcomes");
            var tenant = Guid.NewGuid().ToString("D");
            await outcomes.InsertOneAsync(new BsonDocument("_id", Guid.NewGuid().ToString("D")).Add("TenantId", tenant).Add("Code", "A").Add("LifecycleState", "Active").Add("Version", 1));
            Assert.Equal(1, await outcomes.CountDocumentsAsync(new BsonDocument("TenantId", tenant)));
            Assert.Equal(0, await outcomes.CountDocumentsAsync(new BsonDocument("TenantId", Guid.NewGuid().ToString("D"))));
            var filter = Builders<BsonDocument>.Filter.Eq("TenantId", tenant) & Builders<BsonDocument>.Filter.Eq("Version", 1);
            Assert.Equal(1, (await outcomes.UpdateOneAsync(filter, Builders<BsonDocument>.Update.Set("Version", 2))).ModifiedCount);
            Assert.Equal(0, (await outcomes.UpdateOneAsync(filter, Builders<BsonDocument>.Update.Set("Version", 3))).ModifiedCount);

            var participants = new[] { "diten_di_outcomes", "diten_di_outcome_versions", "diten_di_outcome_measurements", "diten_di_decision_outcome_links", "diten_di_idempotency_receipts", "diten_di_audit_intents", "diten_di_outbox_messages" };
            foreach (var stop in Enumerable.Range(0, participants.Length))
            {
                using var session = await client.StartSessionAsync();
                session.StartTransaction();
                try
                {
                    for (var n = 0; n < participants.Length; n++)
                    {
                        await db.GetCollection<BsonDocument>(participants[n]).InsertOneAsync(session, new BsonDocument("_id", Guid.NewGuid().ToString("D")).Add("TenantId", tenant).Add("Mutation", stop));
                        if (n == stop) throw new InvalidOperationException();
                    }
                }
                catch { await session.AbortTransactionAsync(); }
                foreach (var collection in participants) Assert.Equal(0, await db.GetCollection<BsonDocument>(collection).CountDocumentsAsync(new BsonDocument("Mutation", stop)));
            }

            using (var session = await client.StartSessionAsync())
            {
                session.StartTransaction();
                foreach (var collection in participants) await db.GetCollection<BsonDocument>(collection).InsertOneAsync(session, new BsonDocument("_id", Guid.NewGuid().ToString("D")).Add("TenantId", tenant).Add("Mutation", "success"));
                await session.CommitTransactionAsync();
            }
            foreach (var collection in participants) Assert.Equal(1, await db.GetCollection<BsonDocument>(collection).CountDocumentsAsync(new BsonDocument("Mutation", "success")));
        }
        finally { await client.DropDatabaseAsync(name); }
    }

    private static async Task InitializeReplicaSet()
    {
        var client = new MongoClient("mongodb://127.0.0.1:27020/?directConnection=true");
        try { await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("replSetInitiate", new BsonDocument { { "_id", "mod0072rs" }, { "members", new BsonArray { new BsonDocument { { "_id", 0 }, { "host", "127.0.0.1:27020" } } } } })); }
        catch (MongoCommandException exception) when (exception.CodeName == "AlreadyInitialized") { }
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var hello = await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1));
            if (hello.GetValue("isWritablePrimary", false).ToBoolean()) return;
            await Task.Delay(100);
        }
        throw new TimeoutException("replica set did not become primary");
    }
}

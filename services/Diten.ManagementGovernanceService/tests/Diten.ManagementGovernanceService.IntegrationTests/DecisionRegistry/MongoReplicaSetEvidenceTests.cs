using Diten.ManagementGovernanceService.Application.DecisionRegistry;
using Diten.ManagementGovernanceService.Persistence.DecisionRegistry.Mongo;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.ManagementGovernanceService.IntegrationTests.DecisionRegistry;

public sealed class MongoReplicaSetEvidenceTests
{
    [Fact]
    public async Task Replica_set_proves_indexes_tenant_isolation_rollback_and_cas()
    {
        var uri = Environment.GetEnvironmentVariable("MOD0007_MONGO_URI");
        if (string.IsNullOrWhiteSpace(uri)) return;
        await EnsureReplicaSetAsync();
        var client = new MongoClient(uri);
        var database = "mod0007_evidence_" + Guid.NewGuid().ToString("N");
        var context = new DecisionRegistryMongoContext(client, database);
        try
        {
            var initializer = new DecisionRegistryMongoIndexInitializer(context);
            await initializer.InitializeAsync();
            var snapshot = await initializer.SnapshotAsync();
            Assert.Equal(17, snapshot.Count);
            Assert.All(snapshot, x => { Assert.Equal(new IndexKey("TenantId", 1), x.Keys[0]); Assert.False(x.HasTtl); });
            var expected = DecisionRegistryPersistenceManifest.Instance.Indexes.OrderBy(x => x.Name).ToArray();
            var actual = snapshot.OrderBy(x => x.Name).ToArray();
            Assert.Equal(expected.Select(x => x.Name), actual.Select(x => x.Name));
            Assert.Equal(expected.Select(x => x.Unique), actual.Select(x => x.Unique));
            for (var index = 0; index < expected.Length; index++)
            {
                Assert.Equal(expected[index].Collection, actual[index].Collection);
                Assert.Equal(expected[index].Keys, actual[index].Keys);
                Assert.Equal(expected[index].ExpireAfter is not null, actual[index].HasTtl);
            }

            var tenant = Guid.NewGuid(); var foreign = Guid.NewGuid(); var decision = Guid.NewGuid();
            await context.Collection(0).InsertOneAsync(Doc(("TenantId",tenant),("DecisionId",decision),("SubjectKind","InvestmentCase"),("SubjectId",Guid.NewGuid()),("IsDeleted",false),("LifecycleState","Unpublished"),("Version",1)));
            var repository = new DecisionRegistryMongoRepository(context);
            Assert.NotNull(await repository.FindActiveAsync(0, tenant, "DecisionId", decision));
            Assert.Null(await repository.FindActiveAsync(0, foreign, "DecisionId", decision));

            foreach (var point in new[] { DecisionRegistryFaultPoint.Draft, DecisionRegistryFaultPoint.IdempotencyReceipt, DecisionRegistryFaultPoint.AuditIntent, DecisionRegistryFaultPoint.EventOutbox })
            {
                using var session = await client.StartSessionAsync();
                session.StartTransaction();
                var writer = new DecisionRegistryMongoMutationWriter(context, new ThrowAt(point));
                await Assert.ThrowsAsync<InjectedFault>(() => writer.WriteDraftAsync(new DecisionRegistryMongoTransactionSession(session),
                    Doc(("TenantId",tenant),("DraftId",Guid.NewGuid())), Receipt(tenant), Audit(tenant), Outbox(tenant), CancellationToken.None));
                await session.AbortTransactionAsync();
                Assert.Equal(0, await context.Collection(1).CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("TenantId", tenant)));
                Assert.Equal(0, await context.Collection(6).CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("TenantId", tenant)));
                Assert.Equal(0, await context.Collection(7).CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("TenantId", tenant)));
                Assert.Equal(0, await context.Collection(8).CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("TenantId", tenant)));
            }

            using (var first = await client.StartSessionAsync())
            {
                first.StartTransaction();
                Assert.True(await repository.CompareAndSwapRecordAsync(first, tenant, decision, 1, new BsonDocument("LifecycleState", "Published"), CancellationToken.None));
                await first.CommitTransactionAsync();
            }
            using (var loser = await client.StartSessionAsync())
            {
                loser.StartTransaction();
                Assert.False(await repository.CompareAndSwapRecordAsync(loser, tenant, decision, 1, new BsonDocument("LifecycleState", "Withdrawn"), CancellationToken.None));
                await loser.AbortTransactionAsync();
            }

            var revisionDecision = Guid.NewGuid();
            await context.Collection(2).InsertOneAsync(Doc(("TenantId",tenant),("DecisionRevisionId",Guid.NewGuid()),("DecisionId",revisionDecision),("DecisionRevisionNumber",1)));
            await Assert.ThrowsAsync<MongoWriteException>(() => context.Collection(2).InsertOneAsync(Doc(("TenantId",tenant),("DecisionRevisionId",Guid.NewGuid()),("DecisionId",revisionDecision),("DecisionRevisionNumber",1))));

            foreach (var point in new[] { DecisionRegistryFaultPoint.Revision, DecisionRegistryFaultPoint.RecordPointerCas, DecisionRegistryFaultPoint.PublicationHistory, DecisionRegistryFaultPoint.RevisionReplacement, DecisionRegistryFaultPoint.IdempotencyReceipt, DecisionRegistryFaultPoint.AuditIntent, DecisionRegistryFaultPoint.EventOutbox })
            {
                var publicationDecision = Guid.NewGuid();
                await context.Collection(0).InsertOneAsync(Doc(("TenantId",tenant),("DecisionId",publicationDecision),("SubjectKind","InvestmentCase"),("SubjectId",Guid.NewGuid()),("IsDeleted",false),("LifecycleState","Unpublished"),("Version",1)));
                using var publicationSession = await client.StartSessionAsync();
                publicationSession.StartTransaction();
                var writer = new DecisionRegistryMongoMutationWriter(context, new ThrowAt(point));
                await Assert.ThrowsAsync<InjectedFault>(() => writer.WritePublicationAsync(new DecisionRegistryMongoTransactionSession(publicationSession),
                    Doc(("TenantId",tenant),("DecisionRevisionId",Guid.NewGuid()),("DecisionId",publicationDecision),("DecisionRevisionNumber",1)),
                    tenant, publicationDecision, 1, new BsonDocument { { "LifecycleState", "Published" }, { "CurrentRevisionNumber", 1 } },
                    Doc(("TenantId",tenant),("PublicationHistoryId",Guid.NewGuid()),("DecisionId",publicationDecision),("OccurredAtUtc",DateTime.UtcNow)),
                    Doc(("TenantId",tenant),("DecisionId",publicationDecision),("PriorDecisionRevisionId",Guid.NewGuid()),("NextDecisionRevisionId",Guid.NewGuid())),
                    Receipt(tenant), Audit(tenant), Outbox(tenant), CancellationToken.None));
                await publicationSession.AbortTransactionAsync();
                var unchanged = await context.Collection(0).Find(Builders<BsonDocument>.Filter.Eq("DecisionId",publicationDecision)).SingleAsync();
                Assert.Equal("Unpublished", unchanged["LifecycleState"].AsString);
                Assert.Equal(0, await CountDecision(context, 2, publicationDecision));
                Assert.Equal(0, await CountDecision(context, 3, publicationDecision));
                Assert.Equal(0, await CountDecision(context, 4, publicationDecision));
                Assert.Equal(0, await CountTenant(context, 6, tenant));
                Assert.Equal(0, await CountTenant(context, 7, tenant));
                Assert.Equal(0, await CountTenant(context, 8, tenant));
            }

            foreach (var point in new[] { DecisionRegistryFaultPoint.RecordPointerCas, DecisionRegistryFaultPoint.SupersessionLink, DecisionRegistryFaultPoint.PublicationHistory, DecisionRegistryFaultPoint.IdempotencyReceipt, DecisionRegistryFaultPoint.AuditIntent, DecisionRegistryFaultPoint.EventOutbox })
            {
                var source = Guid.NewGuid();
                await context.Collection(0).InsertOneAsync(Doc(("TenantId",tenant),("DecisionId",source),("SubjectKind","InvestmentCase"),("SubjectId",Guid.NewGuid()),("IsDeleted",false),("LifecycleState","Published"),("Version",1)));
                using var dispositionSession = await client.StartSessionAsync();
                dispositionSession.StartTransaction();
                var writer = new DecisionRegistryMongoMutationWriter(context, new ThrowAt(point));
                await Assert.ThrowsAsync<InjectedFault>(() => writer.WriteDispositionAsync(new DecisionRegistryMongoTransactionSession(dispositionSession),tenant,source,1,
                    new BsonDocument("LifecycleState","Superseded"),
                    Doc(("TenantId",tenant),("PublicationHistoryId",Guid.NewGuid()),("DecisionId",source),("OccurredAtUtc",DateTime.UtcNow)),
                    Doc(("TenantId",tenant),("SourceDecisionId",source),("TargetDecisionId",Guid.NewGuid()),("EffectiveAtUtc",DateTime.UtcNow)),
                    Receipt(tenant),Audit(tenant),Outbox(tenant),CancellationToken.None));
                await dispositionSession.AbortTransactionAsync();
                var unchanged = await repository.FindActiveAsync(0,tenant,"DecisionId",source);
                Assert.NotNull(unchanged); Assert.Equal("Published",unchanged["LifecycleState"].AsString);
                Assert.Equal(0,await CountDecision(context,3,source));
                Assert.Equal(0,await context.Collection(5).CountDocumentsAsync(Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Eq("TenantId",tenant),Builders<BsonDocument>.Filter.Eq("SourceDecisionId",source))));
                Assert.Equal(0,await CountTenant(context,6,tenant)); Assert.Equal(0,await CountTenant(context,7,tenant)); Assert.Equal(0,await CountTenant(context,8,tenant));
            }

            var receiptScope = new IdempotencyScope(tenant,"publish",Guid.NewGuid(),null,Guid.NewGuid().ToString("N"));
            var completedAt = DateTime.UtcNow;
            await context.Collection(6).InsertOneAsync(Doc(("TenantId",tenant),("ReceiptId",Guid.NewGuid()),("Operation",receiptScope.Operation),("EffectiveActorId",receiptScope.EffectiveActorId),("S2SClientId",BsonNull.Value),("IdempotencyKeyHash",receiptScope.IdempotencyKeyHash),("PayloadHash","payload"),("OutcomeKind","Completed"),("OutcomeReference","decision"),("HttpStatus",200),("CreatedAtUtc",completedAt),("CompletedAtUtc",completedAt)));
            var receiptReader = new DecisionRegistryMongoReceiptReader(context);
            Assert.Equal(ReceiptReconciliationKind.MatchingCompleted,(await receiptReader.ReconcileAsync(receiptScope,"payload",CancellationToken.None)).Kind);
            Assert.Equal(ReceiptReconciliationKind.PayloadConflict,(await receiptReader.ReconcileAsync(receiptScope,"other",CancellationToken.None)).Kind);
            Assert.Equal(ReceiptReconciliationKind.MissingOrIndeterminate,(await receiptReader.ReconcileAsync(receiptScope with { IdempotencyKeyHash="missing" },"payload",CancellationToken.None)).Kind);
        }
        finally { await context.DropAsync(); }
    }

    private static BsonDocument Receipt(Guid tenant) => Doc(("TenantId",tenant),("ReceiptId",Guid.NewGuid()),("Operation","draft"),("EffectiveActorId",Guid.NewGuid()),("S2SClientId",BsonNull.Value),("IdempotencyKeyHash",Guid.NewGuid().ToString("N")),("PayloadHash","hash"));
    private static BsonDocument Audit(Guid tenant) => Doc(("TenantId",tenant),("AuditIntentId",Guid.NewGuid()),("DispatchState","Pending"),("NextAttemptAtUtc",DateTime.UtcNow),("OccurredAtUtc",DateTime.UtcNow));
    private static BsonDocument Outbox(Guid tenant) => Doc(("TenantId",tenant),("EventId",Guid.NewGuid()),("Status","Pending"),("NextAttemptAtUtc",DateTime.UtcNow),("CreatedAtUtc",DateTime.UtcNow));
    private static Task<long> CountDecision(DecisionRegistryMongoContext context, int collection, Guid decision) => context.Collection(collection).CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("DecisionId", decision));
    private static Task<long> CountTenant(DecisionRegistryMongoContext context, int collection, Guid tenant) => context.Collection(collection).CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("TenantId", tenant));
    private static BsonDocument Doc(params (string Name, object Value)[] values) => new(values.Select(x => new BsonElement(x.Name, BsonValue.Create(x.Value))));
    private sealed class ThrowAt(DecisionRegistryFaultPoint point) : ITransactionFaultInjector { public void After(DecisionRegistryFaultPoint actual) { if (actual == point) throw new InjectedFault(); } }
    private sealed class InjectedFault : Exception;

    private static async Task EnsureReplicaSetAsync()
    {
        var direct = new MongoClient("mongodb://127.0.0.1:27019/?directConnection=true&serverSelectionTimeoutMS=2000");
        try
        {
            await direct.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("replSetInitiate",
                new BsonDocument { { "_id", "mod0007rs" }, { "members", new BsonArray { new BsonDocument { { "_id", 0 }, { "host", "127.0.0.1:27019" } } } } }));
        }
        catch (MongoCommandException ex) when (ex.CodeName is "AlreadyInitialized" or "AlreadyInitiated") { }
        for (var attempt = 0; attempt < 30; attempt++)
        {
            try { var hello = await direct.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1)); if (hello.GetValue("isWritablePrimary", false).ToBoolean()) return; }
            catch (MongoException) { }
            await Task.Delay(200);
        }
        throw new TimeoutException("Disposable MOD-0007 replica set did not become primary.");
    }
}

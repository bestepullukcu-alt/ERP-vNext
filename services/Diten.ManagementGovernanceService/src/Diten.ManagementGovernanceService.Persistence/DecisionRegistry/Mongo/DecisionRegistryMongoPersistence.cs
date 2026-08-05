using Diten.ManagementGovernanceService.Application.DecisionRegistry;
using Diten.ManagementGovernanceService.Persistence.DecisionRegistry.Transactions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.ManagementGovernanceService.Persistence.DecisionRegistry.Mongo;

public sealed class DecisionRegistryMongoContext
{
    private readonly IMongoDatabase _database;
    public DecisionRegistryMongoContext(IMongoClient client, string databaseName)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (string.IsNullOrWhiteSpace(databaseName)) throw new ArgumentException("Database name is required.", nameof(databaseName));
        DecisionRegistryPersistenceManifest.Instance.Validate();
        Client = client;
        _database = client.GetDatabase(databaseName);
    }
    public IMongoClient Client { get; }
    public IMongoCollection<BsonDocument> Collection(int manifestCollectionIndex) =>
        _database.GetCollection<BsonDocument>(DecisionRegistryPersistenceManifest.Instance.Collections[manifestCollectionIndex]);
    public Task DropAsync(CancellationToken ct = default) => Client.DropDatabaseAsync(_database.DatabaseNamespace.DatabaseName, ct);
}

public sealed class DecisionRegistryMongoIndexInitializer(DecisionRegistryMongoContext context)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var manifest = DecisionRegistryPersistenceManifest.Instance;
        manifest.Validate();
        for (var collectionIndex = 0; collectionIndex < manifest.Collections.Count; collectionIndex++)
        {
            var collection = context.Collection(collectionIndex);
            foreach (var index in manifest.Indexes.Where(x => x.Collection == manifest.Collections[collectionIndex]))
            {
                var keys = new BsonDocument(index.Keys.Select(x => new BsonElement(x.Field, x.Direction)));
                await collection.Indexes.CreateOneAsync(
                    new CreateIndexModel<BsonDocument>(keys, new CreateIndexOptions { Name = index.Name, Unique = index.Unique }),
                    cancellationToken: ct);
            }
        }
    }

    public async Task<IReadOnlyList<IndexSnapshot>> SnapshotAsync(CancellationToken ct = default)
    {
        var result = new List<IndexSnapshot>();
        var manifest = DecisionRegistryPersistenceManifest.Instance;
        for (var i = 0; i < manifest.Collections.Count; i++)
        {
            using var cursor = await context.Collection(i).Indexes.ListAsync(ct);
            foreach (var index in await cursor.ToListAsync(ct))
            {
                if (index["name"].AsString == "_id_") continue;
                result.Add(new(manifest.Collections[i], index["name"].AsString, index.GetValue("unique", false).ToBoolean(), index["key"].AsBsonDocument.Elements.Select(x => new IndexKey(x.Name, x.Value.ToInt32())).ToArray(), index.Contains("expireAfterSeconds")));
            }
        }
        return result;
    }
}

public sealed record IndexSnapshot(string Collection, string Name, bool Unique, IReadOnlyList<IndexKey> Keys, bool HasTtl);

public sealed class DecisionRegistryMongoRepository(DecisionRegistryMongoContext context)
{
    public async Task<BsonDocument?> FindActiveAsync(int collectionIndex, Guid tenantId, string identityField, Guid identity, CancellationToken ct = default) =>
        await context.Collection(collectionIndex).Find(Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("TenantId", tenantId),
            Builders<BsonDocument>.Filter.Eq(identityField, identity),
            Builders<BsonDocument>.Filter.Eq("IsDeleted", false))).FirstOrDefaultAsync(ct);

    public async Task<bool> CompareAndSwapRecordAsync(IClientSessionHandle session, Guid tenantId, Guid decisionId, int expectedVersion, BsonDocument setValues, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Eq("TenantId", tenantId), Builders<BsonDocument>.Filter.Eq("DecisionId", decisionId), Builders<BsonDocument>.Filter.Eq("Version", expectedVersion), Builders<BsonDocument>.Filter.Eq("IsDeleted", false));
        var update = new BsonDocument("$set", setValues).Add("$inc", new BsonDocument("Version", 1));
        return (await context.Collection(0).UpdateOneAsync(session, filter, update, cancellationToken: ct)).ModifiedCount == 1;
    }
}

public static class DecisionRegistryMongoConflictMapper
{
    public static DecisionRegistryConflictException Map(Exception exception) => exception switch
    {
        MongoWriteException write when write.WriteError?.Category == ServerErrorCategory.DuplicateKey => new("DecisionRegistry identity conflict."),
        MongoCommandException command when command.Code is 11000 or 112 || command.HasErrorLabel("TransientTransactionError") => new("DecisionRegistry concurrency conflict."),
        MongoException mongo when mongo.HasErrorLabel("TransientTransactionError") => new("DecisionRegistry concurrency conflict."),
        DecisionRegistryConflictException conflict => conflict,
        _ => throw exception
    };
}

public sealed class DecisionRegistryMongoReceiptReader(DecisionRegistryMongoContext context) : IIdempotencyReceiptReader
{
    public async Task<ReceiptReconciliation> ReconcileAsync(IdempotencyScope scope, string payloadHash, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("TenantId", scope.TenantId),
            Builders<BsonDocument>.Filter.Eq("Operation", scope.Operation),
            Builders<BsonDocument>.Filter.Eq("EffectiveActorId", scope.EffectiveActorId),
            scope.S2SClientId is null ? Builders<BsonDocument>.Filter.Eq("S2SClientId", BsonNull.Value) : Builders<BsonDocument>.Filter.Eq("S2SClientId", scope.S2SClientId),
            Builders<BsonDocument>.Filter.Eq("IdempotencyKeyHash", scope.IdempotencyKeyHash));
        var document = await context.Collection(6).Find(filter).FirstOrDefaultAsync(ct);
        if (document is null || !document.TryGetValue("CompletedAtUtc", out var completed) || completed.IsBsonNull)
            return new(ReceiptReconciliationKind.MissingOrIndeterminate, null);
        if (document.GetValue("PayloadHash", "").AsString != payloadHash)
            return new(ReceiptReconciliationKind.PayloadConflict, null);
        var receipt = new IdempotencyReceipt(
            document["ReceiptId"].AsGuid, scope, payloadHash,
            document.GetValue("OutcomeKind", "Completed").AsString,
            document.GetValue("OutcomeReference", "").AsString,
            document.GetValue("HttpStatus", 200).ToInt32(),
            new DateTimeOffset(document.GetValue("CreatedAtUtc", completed).ToUniversalTime()),
            new DateTimeOffset(completed.ToUniversalTime()));
        return new(ReceiptReconciliationKind.MatchingCompleted, receipt);
    }
}

public sealed class DecisionRegistryMongoTransactionSession(IClientSessionHandle session) : IDecisionRegistryTransactionSession
{
    public Task BeginAsync(CancellationToken ct) { session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot, ReadPreference.Primary, WriteConcern.WMajority)); return Task.CompletedTask; }
    public async Task CommitAsync(CancellationToken ct) { try { await session.CommitTransactionAsync(ct); } catch (MongoException ex) when (ex.HasErrorLabel("UnknownTransactionCommitResult")) { throw new UnknownDecisionRegistryCommitException("Mongo commit result is unknown.", ex); } }
    public Task AbortAsync(CancellationToken ct) => session.IsInTransaction ? session.AbortTransactionAsync(ct) : Task.CompletedTask;
    public ValueTask DisposeAsync() { session.Dispose(); return ValueTask.CompletedTask; }
    public IClientSessionHandle Handle => session;
}

public sealed class DecisionRegistryMongoTransactionSessionFactory(DecisionRegistryMongoContext context) : IDecisionRegistryTransactionSessionFactory
{
    public async Task<IDecisionRegistryTransactionSession> OpenAsync(CancellationToken ct) => new DecisionRegistryMongoTransactionSession(await context.Client.StartSessionAsync(cancellationToken: ct));
}

public sealed class DecisionRegistryMongoMutationWriter(DecisionRegistryMongoContext context, ITransactionFaultInjector faults)
{
    public async Task WriteCreateAsync(IDecisionRegistryTransactionSession transaction, BsonDocument record, BsonDocument receipt, BsonDocument audit, BsonDocument outbox, CancellationToken ct)
    {
        var session = Handle(transaction);
        await Insert(session, 0, record, DecisionRegistryFaultPoint.Draft, ct);
        await Insert(session, 6, receipt, DecisionRegistryFaultPoint.IdempotencyReceipt, ct);
        await Insert(session, 7, audit, DecisionRegistryFaultPoint.AuditIntent, ct);
        await Insert(session, 8, outbox, DecisionRegistryFaultPoint.EventOutbox, ct);
    }

    public async Task WriteDraftAsync(IDecisionRegistryTransactionSession transaction, BsonDocument draft, BsonDocument receipt, BsonDocument audit, BsonDocument outbox, CancellationToken ct)
    {
        var session = Handle(transaction);
        await Insert(session, 1, draft, DecisionRegistryFaultPoint.Draft, ct);
        await Insert(session, 6, receipt, DecisionRegistryFaultPoint.IdempotencyReceipt, ct);
        await Insert(session, 7, audit, DecisionRegistryFaultPoint.AuditIntent, ct);
        await Insert(session, 8, outbox, DecisionRegistryFaultPoint.EventOutbox, ct);
    }

    public async Task WritePublicationAsync(IDecisionRegistryTransactionSession transaction, BsonDocument revision, Guid tenantId, Guid decisionId, int expectedVersion, BsonDocument recordSet, BsonDocument history, BsonDocument? replacement, BsonDocument receipt, BsonDocument audit, BsonDocument outbox, CancellationToken ct)
    {
        var session = Handle(transaction);
        await Insert(session, 2, revision, DecisionRegistryFaultPoint.Revision, ct);
        if (!await new DecisionRegistryMongoRepository(context).CompareAndSwapRecordAsync(session, tenantId, decisionId, expectedVersion, recordSet, ct)) throw new DecisionRegistryConflictException("Record CAS conflict.");
        faults.After(DecisionRegistryFaultPoint.RecordPointerCas);
        await Insert(session, 3, history, DecisionRegistryFaultPoint.PublicationHistory, ct);
        if (replacement is not null) await Insert(session, 4, replacement, DecisionRegistryFaultPoint.RevisionReplacement, ct);
        await Insert(session, 6, receipt, DecisionRegistryFaultPoint.IdempotencyReceipt, ct);
        await Insert(session, 7, audit, DecisionRegistryFaultPoint.AuditIntent, ct);
        await Insert(session, 8, outbox, DecisionRegistryFaultPoint.EventOutbox, ct);
    }

    public async Task WriteDispositionAsync(IDecisionRegistryTransactionSession transaction, Guid tenantId, Guid decisionId, int expectedVersion, BsonDocument recordSet, BsonDocument history, BsonDocument? supersession, BsonDocument receipt, BsonDocument audit, BsonDocument outbox, CancellationToken ct)
    {
        var session = Handle(transaction);
        if (!await new DecisionRegistryMongoRepository(context).CompareAndSwapRecordAsync(session, tenantId, decisionId, expectedVersion, recordSet, ct)) throw new DecisionRegistryConflictException("Record CAS conflict.");
        faults.After(DecisionRegistryFaultPoint.RecordPointerCas);
        if (supersession is not null) await Insert(session, 5, supersession, DecisionRegistryFaultPoint.SupersessionLink, ct);
        await Insert(session, 3, history, DecisionRegistryFaultPoint.PublicationHistory, ct);
        await Insert(session, 6, receipt, DecisionRegistryFaultPoint.IdempotencyReceipt, ct);
        await Insert(session, 7, audit, DecisionRegistryFaultPoint.AuditIntent, ct);
        await Insert(session, 8, outbox, DecisionRegistryFaultPoint.EventOutbox, ct);
    }

    private async Task Insert(IClientSessionHandle session, int collectionIndex, BsonDocument document, DecisionRegistryFaultPoint point, CancellationToken ct)
    { await context.Collection(collectionIndex).InsertOneAsync(session, document, cancellationToken: ct); faults.After(point); }

    private static IClientSessionHandle Handle(IDecisionRegistryTransactionSession transaction) =>
        transaction is DecisionRegistryMongoTransactionSession mongo ? mongo.Handle : throw new ArgumentException("A DecisionRegistry-owned Mongo transaction session is required.", nameof(transaction));
}

using MongoDB.Bson;
using MongoDB.Driver;
using Diten.ManagementGovernanceService.Persistence.Modules.Dws;
using Xunit;

namespace Diten.ManagementGovernanceService.IntegrationTests.Modules.Dws;

[Collection(DwsMongoCollection.Name)]
public sealed class DwsMongoEvidenceTests(DisposableDwsMongo mongo)
{
    private DwsMongoContext Context(string suffix) => new(mongo.Client, "mod0354_b02_" + suffix);

    [Fact]
    public async Task Replica_set_readiness_and_exact_manifest_indexes_are_real()
    {
        var context = Context("manifest");
        await new DwsMongoIndexInitializer(context).InitializeAsync();

        using var collectionsCursor = await context.Database.ListCollectionNamesAsync();
        var collections = (await collectionsCursor.ToListAsync()).Where(x => x.StartsWith("mg_dws_", StringComparison.Ordinal)).OrderBy(x => x).ToArray();
        Assert.Equal(DwsPersistenceOwnershipManifest.Collections.Select(x => x.Name).OrderBy(x => x), collections);

        var actual = new Dictionary<string, BsonDocument>(StringComparer.Ordinal);
        foreach (var collection in DwsMongoContext.CollectionAliases)
        {
            using var cursor = await context.Collection(collection.Key).Indexes.ListAsync();
            foreach (var index in (await cursor.ToListAsync()).Where(x => x["name"].AsString != "_id_")) actual.Add(index["name"].AsString, index);
        }

        Assert.Equal(14, actual.Count);
        foreach (var expected in DwsPersistenceOwnershipManifest.Indexes)
        {
            var index = actual[expected.Name];
            Assert.Equal(expected.Keys, index["key"].AsBsonDocument.Names);
            Assert.Equal(expected.Unique, index.GetValue("unique", false).ToBoolean());
            Assert.False(index.Contains("expireAfterSeconds"));
            Assert.Equal("TenantId", index["key"].AsBsonDocument.GetElement(0).Name);
            Assert.Equal(expected.PartialFilter is not null, index.Contains("partialFilterExpression"));
        }
    }

    [Fact]
    public async Task Every_transaction_family_rolls_back_at_every_participant_boundary()
    {
        var context = Context("rollback");
        await new DwsMongoIndexInitializer(context).InitializeAsync();
        var writer = new DwsMongoAtomicWriter(context);

        foreach (var family in DwsPersistenceOwnershipManifest.Transactions)
        {
            var participantCount = family.BusinessCollections.Count + DwsTransactionFamily.TechnicalParticipants.Count;
            for (var failurePoint = 1; failurePoint <= participantCount; failurePoint++)
            {
                var mutation = BuildMutation(family.Name, Guid.NewGuid());
                await Assert.ThrowsAsync<InjectedFault>(() => writer.ExecuteAsync(mutation, new ThrowAt(failurePoint)));
                Assert.Equal(0, await CountTenantAsync(context, mutation.TenantId));
            }
        }
    }

    [Fact]
    public async Task Success_is_exact_once_and_CAS_and_tenant_isolation_fail_closed()
    {
        var context = Context("cas");
        await new DwsMongoIndexInitializer(context).InitializeAsync();
        var writer = new DwsMongoAtomicWriter(context);
        var tenant = Guid.NewGuid();
        var mutation = BuildMutation("CreateStructure", tenant);
        await writer.ExecuteAsync(mutation);
        Assert.Equal(mutation.Participants.Count, await CountTenantAsync(context, tenant));

        var replay = BuildMutation("UpdateStructureMetadata", tenant, expectedVersion: 1, existingId: mutation.Participants[1].Id);
        await writer.ExecuteAsync(replay);
        var revision = await context.Collection("revisions").Find(IdTenant(replay.Participants[0].Id, tenant)).SingleAsync();
        Assert.Equal(2, revision["Version"].AsInt32);

        var stale = BuildMutation("UpdateStructureMetadata", tenant, expectedVersion: 1, existingId: replay.Participants[0].Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => writer.ExecuteAsync(stale));
        var foreign = BuildMutation("UpdateStructureMetadata", Guid.NewGuid(), expectedVersion: 2, existingId: replay.Participants[0].Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => writer.ExecuteAsync(foreign));
        Assert.Equal(0, await CountTenantAsync(context, foreign.TenantId));
    }

    [Fact]
    public async Task Unknown_commit_is_same_session_commit_only_and_body_is_not_replayed()
    {
        var context = Context("unknown_before");
        await new DwsMongoIndexInitializer(context).InitializeAsync();
        var committer = new UnknownBeforeCommitter(2);
        var mutation = BuildMutation("CreateStructure", Guid.NewGuid());
        await new DwsMongoAtomicWriter(context, committer).ExecuteAsync(mutation);
        Assert.Equal(3, committer.Attempts);
        Assert.Equal(mutation.Participants.Count, await CountTenantAsync(context, mutation.TenantId));
    }

    [Fact]
    public async Task Durable_unknown_commit_reconciles_receipt_without_duplicate_participants()
    {
        var context = Context("unknown_after");
        await new DwsMongoIndexInitializer(context).InitializeAsync();
        var committer = new DurableUnknownOnThirdCommitter();
        var mutation = BuildMutation("CreateStructure", Guid.NewGuid());
        await new DwsMongoAtomicWriter(context, committer).ExecuteAsync(mutation);
        Assert.Equal(3, committer.Attempts);
        Assert.Equal(mutation.Participants.Count, await CountTenantAsync(context, mutation.TenantId));
    }

    [Fact]
    public async Task Unknown_commit_exhaustion_is_503_with_zero_residue()
    {
        var context = Context("unknown_exhausted");
        await new DwsMongoIndexInitializer(context).InitializeAsync();
        var mutation = BuildMutation("CreateStructure", Guid.NewGuid());
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new DwsMongoAtomicWriter(context, new UnknownBeforeCommitter(3)).ExecuteAsync(mutation));
        Assert.Equal("dws_commit_indeterminate", error.Message);
        Assert.Equal(0, await CountTenantAsync(context, mutation.TenantId));
    }

    [Fact]
    public async Task Standalone_Mongo_fails_closed_without_partial_state()
    {
        await using var standalone = await DisposableDwsMongo.StartStandaloneAsync();
        Assert.True(standalone.Port >= 27022);
        var context = new DwsMongoContext(standalone.Client, "mod0354_b02_standalone");
        await new DwsMongoIndexInitializer(context).InitializeAsync();
        var mutation = BuildMutation("CreateStructure", Guid.NewGuid());
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new DwsMongoAtomicWriter(context).ExecuteAsync(mutation));
        Assert.Equal("dws_transaction_unavailable", error.Message);
        Assert.Equal(0, await CountTenantAsync(context, mutation.TenantId));
    }

    private static DwsMongoMutation BuildMutation(string familyName, Guid tenant, int expectedVersion = 0, Guid? existingId = null)
    {
        var family = DwsPersistenceOwnershipManifest.Transactions.Single(x => x.Name == familyName);
        var key = Guid.NewGuid().ToString("N");
        const string hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var participants = new List<DwsMongoParticipant>();
        for (var index = 0; index < family.BusinessCollections.Count; index++)
        {
            var alias = family.BusinessCollections[index];
            participants.Add(new(alias, index == 0 && existingId.HasValue ? existingId.Value : Guid.NewGuid(), index == 0 ? expectedVersion : 0, BusinessValues(alias)));
        }
        participants.Add(new("receipts", Guid.NewGuid(), 0, new BsonDocument { ["CommandFamily"] = familyName, ["IdempotencyKey"] = key, ["RequestPayloadHash"] = hash, ["CreatedAtUtc"] = DateTime.UtcNow }));
        participants.Add(new("audit-intents", Guid.NewGuid(), 0, new BsonDocument { ["AuditIntentId"] = Guid.NewGuid().ToString("D") }));
        participants.Add(new("outbox", Guid.NewGuid(), 0, new BsonDocument { ["EventId"] = Guid.NewGuid().ToString("D"), ["DeliveryState"] = "pending", ["NextAttemptAtUtc"] = BsonNull.Value }));
        return new(tenant, familyName, key, hash, participants);
    }

    private static BsonDocument BusinessValues(string alias)
    {
        var value = Guid.NewGuid();
        return alias switch
        {
            "definitions" => new BsonDocument { ["ExternalContextReference"] = new BsonDocument { ["ContractName"] = "ppm.external-context-reference", ["ContractVersion"] = "1.0", ["ContextKind"] = "program", ["ContextId"] = value.ToString("D") }, ["Value"] = value.ToString("D") },
            "revisions" => new BsonDocument { ["StructureDefinitionId"] = value.ToString("D"), ["RevisionNumber"] = Random.Shared.Next(1, int.MaxValue), ["IsSealed"] = false, ["Value"] = value.ToString("D") },
            "nodes" => new BsonDocument { ["StructureRevisionId"] = value.ToString("D"), ["LogicalNodeId"] = Guid.NewGuid().ToString("D"), ["Code"] = value.ToString("N"), ["ParentLogicalNodeId"] = BsonNull.Value, ["SiblingOrder"] = Random.Shared.Next(0, int.MaxValue), ["Value"] = value.ToString("D") },
            "dependencies" => new BsonDocument { ["StructureRevisionId"] = value.ToString("D"), ["FromLogicalNodeId"] = Guid.NewGuid().ToString("D"), ["ToLogicalNodeId"] = Guid.NewGuid().ToString("D"), ["Value"] = value.ToString("D") },
            "baselines" => new BsonDocument { ["StructureDefinitionId"] = value.ToString("D"), ["BaselineNumber"] = Random.Shared.Next(1, int.MaxValue), ["CanonicalizationVersion"] = "dws.structural-baseline.v1", ["ContentHash"] = new string('a', 64), ["Value"] = value.ToString("D") },
            _ => throw new InvalidOperationException(alias)
        };
    }

    private static async Task<long> CountTenantAsync(DwsMongoContext context, Guid tenant)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("TenantId", new BsonBinaryData(tenant, GuidRepresentation.Standard));
        long count = 0;
        foreach (var alias in DwsMongoContext.CollectionAliases.Keys) count += await context.Collection(alias).CountDocumentsAsync(filter);
        return count;
    }

    private static FilterDefinition<BsonDocument> IdTenant(Guid id, Guid tenant) => Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(id, GuidRepresentation.Standard))
        & Builders<BsonDocument>.Filter.Eq("TenantId", new BsonBinaryData(tenant, GuidRepresentation.Standard));

    private sealed class InjectedFault : Exception;
    private sealed class ThrowAt(int point) : IDwsMongoFaultProbe
    {
        public Task AfterParticipantAsync(int participantNumber, CancellationToken cancellationToken) => participantNumber == point ? Task.FromException(new InjectedFault()) : Task.CompletedTask;
    }

    private sealed class UnknownBeforeCommitter(int unknownAttempts) : IDwsMongoCommitter
    {
        private readonly DwsMongoCommitter _inner = new();
        public int Attempts { get; private set; }
        public Task CommitAsync(IClientSessionHandle session, CancellationToken cancellationToken)
        {
            Attempts++;
            return Attempts <= unknownAttempts ? Task.FromException(new DwsUnknownCommitResultException()) : _inner.CommitAsync(session, cancellationToken);
        }
    }

    private sealed class DurableUnknownOnThirdCommitter : IDwsMongoCommitter
    {
        private readonly DwsMongoCommitter _inner = new();
        public int Attempts { get; private set; }
        public async Task CommitAsync(IClientSessionHandle session, CancellationToken cancellationToken)
        {
            Attempts++;
            if (Attempts < 3) throw new DwsUnknownCommitResultException();
            await _inner.CommitAsync(session, cancellationToken);
            throw new DwsUnknownCommitResultException();
        }
    }
}

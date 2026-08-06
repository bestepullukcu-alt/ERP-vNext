using Diten.AuthService.Application.S2S;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.S2S;

public sealed class PermissionCatalogManifestMongoTests
{
    public static IEnumerable<object[]> Participants => Enum.GetValues<PermissionCatalogTransactionParticipant>().Select(x => new object[] { x });

    [Fact]
    public void Transaction_participant_snapshot_is_exact()
    {
        Assert.Equal(new[]
        {
            PermissionCatalogTransactionParticipant.ManifestHeader,
            PermissionCatalogTransactionParticipant.OperationMappings,
            PermissionCatalogTransactionParticipant.PermissionsCatalog,
            PermissionCatalogTransactionParticipant.PermissionOwnershipBindings,
            PermissionCatalogTransactionParticipant.RegistrationAudit
        }, Enum.GetValues<PermissionCatalogTransactionParticipant>());
    }

    [Theory]
    [MemberData(nameof(Participants))]
    public async Task Failure_after_each_participant_rolls_back_every_mutation_and_retry_succeeds(PermissionCatalogTransactionParticipant participant)
    {
        var (client, database, name) = await CreateDatabaseAsync();
        try
        {
            var manifest = GateIPermissionCatalogManifests.Mod0007;
            var key = manifest.Entries[0].PermissionKey; var parts = key.Split('.');
            await database.GetCollection<Permission>("permissions").InsertOneAsync(
                new Permission(parts[0], string.Join('.', parts[1..^1]), parts[^1], key, null, manifest.OwnerModuleId, PermissionScope.Tenant));
            await database.GetCollection<BsonDocument>("rolePermissions").InsertOneAsync(new("sentinel", "role"));
            await database.GetCollection<BsonDocument>("roleAssignmentVersions").InsertOneAsync(new("sentinel", "version"));
            await database.GetCollection<BsonDocument>("entitlementStates").InsertOneAsync(new("sentinel", "entitlement"));
            var before = await SnapshotAsync(database);
            var probe = new RecordingProbe { FailAfter = participant };

            var failed = await new PermissionCatalogManifestRegistrar(client, database, probe).RegisterAsync(manifest, CancellationToken.None);
            Assert.Equal(Application.Common.Interfaces.PermissionCatalogRegistrationStatus.Unavailable, failed.Status);
            Assert.Equal(before, await SnapshotAsync(database));
            Assert.Equal(0, await CountAsync(database, "s2sPermissionCatalogManifests"));
            Assert.Equal(0, await CountAsync(database, "s2sPermissionCatalogOperations"));
            Assert.Equal(0, await CountAsync(database, "s2sPermissionCatalogPermissionOwners"));
            Assert.Equal(0, await CountAsync(database, "s2sPermissionCatalogRegistrationAudits"));

            probe.FailAfter = null;
            var retry = await new PermissionCatalogManifestRegistrar(client, database, probe).RegisterAsync(manifest, CancellationToken.None);
            Assert.Equal(Application.Common.Interfaces.PermissionCatalogRegistrationStatus.Registered, retry.Status);
            Assert.Equal(8, await CountAsync(database, "s2sPermissionCatalogOperations"));
            Assert.Equal(7, await CountAsync(database, "permissions"));
            Assert.Equal(7, await CountAsync(database, "s2sPermissionCatalogPermissionOwners"));
            Assert.Equal(1, await CountAsync(database, "s2sPermissionCatalogRegistrationAudits"));
        }
        finally { await client.DropDatabaseAsync(name); }
    }

    [Fact]
    public async Task Unknown_before_commit_retries_commit_only_and_executes_body_once()
    {
        var (client, database, name) = await CreateDatabaseAsync();
        try
        {
            var probe = new RecordingProbe { UnknownBeforeSendCount = 1 };
            var result = await new PermissionCatalogManifestRegistrar(client, database, probe).RegisterAsync(GateIPermissionCatalogManifests.Mod0007, CancellationToken.None);
            Assert.Equal(Application.Common.Interfaces.PermissionCatalogRegistrationStatus.Registered, result.Status);
            Assert.Equal(1, probe.BodyExecutions); Assert.Equal(2, probe.CommitAttempts);
            await AssertExactMod0007ResidueAsync(database);
        }
        finally { await client.DropDatabaseAsync(name); }
    }

    [Fact]
    public async Task Unknown_after_durable_commit_reconciles_without_body_retry()
    {
        var (client, database, name) = await CreateDatabaseAsync();
        try
        {
            var probe = new RecordingProbe { UnknownAfterDurableCommit = true };
            var result = await new PermissionCatalogManifestRegistrar(client, database, probe).RegisterAsync(GateIPermissionCatalogManifests.Mod0007, CancellationToken.None);
            Assert.Equal(Application.Common.Interfaces.PermissionCatalogRegistrationStatus.Registered, result.Status);
            Assert.NotEqual(Guid.Empty, result.RegistrationId);
            Assert.Equal(1, probe.BodyExecutions); Assert.Equal(1, probe.CommitAttempts);
            await AssertExactMod0007ResidueAsync(database);
        }
        finally { await client.DropDatabaseAsync(name); }
    }

    [Fact]
    public async Task Commit_unknown_retry_exhaustion_is_503_class_with_body_once_and_zero_residue()
    {
        var (client, database, name) = await CreateDatabaseAsync();
        try
        {
            var probe = new RecordingProbe { UnknownBeforeSendCount = 3 };
            var result = await new PermissionCatalogManifestRegistrar(client, database, probe).RegisterAsync(GateIPermissionCatalogManifests.Mod0007, CancellationToken.None);
            Assert.Equal(Application.Common.Interfaces.PermissionCatalogRegistrationStatus.Unavailable, result.Status);
            Assert.Equal(1, probe.BodyExecutions); Assert.Equal(3, probe.CommitAttempts);
            Assert.Equal(0, await CountAsync(database, "s2sPermissionCatalogManifests"));
            Assert.Equal(0, await CountAsync(database, "permissions"));
        }
        finally { await client.DropDatabaseAsync(name); }
    }
    [Fact]
    public async Task Four_manifests_register_atomically_and_replay_is_stable_without_grants_or_versions()
    {
        var (client, database, name) = await CreateDatabaseAsync();
        try
        {
            var registrar = new PermissionCatalogManifestRegistrar(client, database);
            foreach (var manifest in GateIPermissionCatalogManifests.All)
            {
                var first = await registrar.RegisterAsync(manifest, CancellationToken.None);
                var second = await registrar.RegisterAsync(manifest, CancellationToken.None);
                Assert.Equal(Application.Common.Interfaces.PermissionCatalogRegistrationStatus.Registered, first.Status);
                Assert.Equal(Application.Common.Interfaces.PermissionCatalogRegistrationStatus.NoOp, second.Status);
                Assert.Equal(first.RegistrationId, second.RegistrationId);
            }

            Assert.Equal(4, await database.GetCollection<ManifestDocument>("s2sPermissionCatalogManifests").CountDocumentsAsync(FilterDefinition<ManifestDocument>.Empty));
            Assert.Equal(48, await database.GetCollection<OperationDocument>("s2sPermissionCatalogOperations").CountDocumentsAsync(FilterDefinition<OperationDocument>.Empty));
            Assert.Equal(45, await database.GetCollection<PermissionOwnerDocument>("s2sPermissionCatalogPermissionOwners").CountDocumentsAsync(FilterDefinition<PermissionOwnerDocument>.Empty));
            Assert.Equal(45, await database.GetCollection<Permission>("permissions").CountDocumentsAsync(FilterDefinition<Permission>.Empty));
            Assert.Equal(0, await CountAsync(database, "rolePermissions"));
            Assert.Equal(0, await CountAsync(database, "roleAssignmentVersions"));
            Assert.Equal(0, await CountAsync(database, "entitlementStates"));
            await AssertUniqueIndexAsync(database.GetCollection<ManifestDocument>("s2sPermissionCatalogManifests"), "ux_owner_version");
            await AssertUniqueIndexAsync(database.GetCollection<OperationDocument>("s2sPermissionCatalogOperations"), "ux_operation");
            await AssertUniqueIndexAsync(database.GetCollection<PermissionOwnerDocument>("s2sPermissionCatalogPermissionOwners"), "ux_permission_owner");
            await AssertUniqueIndexAsync(database.GetCollection<RegistrationAuditDocument>("s2sPermissionCatalogRegistrationAudits"), "ux_audit_owner_version");
        }
        finally { await client.DropDatabaseAsync(name); }
    }

    [Fact]
    public async Task Concurrent_same_registration_has_one_winner_and_no_partial_rows()
    {
        var (client, database, name) = await CreateDatabaseAsync();
        try
        {
            var registrar = new PermissionCatalogManifestRegistrar(client, database);
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<Application.Common.Interfaces.PermissionCatalogRegistrationResult> Run()
            { await gate.Task; return await registrar.RegisterAsync(GateIPermissionCatalogManifests.Mod0007, CancellationToken.None); }
            var calls = new[] { Run(), Run() }; gate.SetResult();
            var results = await Task.WhenAll(calls);
            Assert.Single(results, x => x.Status == Application.Common.Interfaces.PermissionCatalogRegistrationStatus.Registered);
            Assert.All(results, x => Assert.Contains(x.Status, new[] { Application.Common.Interfaces.PermissionCatalogRegistrationStatus.Registered, Application.Common.Interfaces.PermissionCatalogRegistrationStatus.NoOp }));
            Assert.Equal(1, await database.GetCollection<ManifestDocument>("s2sPermissionCatalogManifests").CountDocumentsAsync(FilterDefinition<ManifestDocument>.Empty));
            Assert.Equal(8, await database.GetCollection<OperationDocument>("s2sPermissionCatalogOperations").CountDocumentsAsync(FilterDefinition<OperationDocument>.Empty));
            Assert.Equal(7, await database.GetCollection<Permission>("permissions").CountDocumentsAsync(FilterDefinition<Permission>.Empty));
        }
        finally { await client.DropDatabaseAsync(name); }
    }

    [Fact]
    public async Task Concurrent_conflicting_registration_has_one_complete_winner_and_one_409()
    {
        var (client, database, name) = await CreateDatabaseAsync();
        try
        {
            var canonical = GateIPermissionCatalogManifests.Mod0007;
            var changed = canonical with { Entries = new[] { canonical.Entries[0] with { PermissionKey = "management-governance.decisions.create" } }.Concat(canonical.Entries.Skip(1)).ToArray() };
            changed = changed with { CanonicalPayloadHash = PermissionCatalogManifestV1.ComputeHash(changed) };
            var registrar = new PermissionCatalogManifestRegistrar(client, database);
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<Application.Common.Interfaces.PermissionCatalogRegistrationResult> Run(PermissionCatalogManifestV1 manifest)
            { await gate.Task; return await registrar.RegisterAsync(manifest, CancellationToken.None); }
            var tasks = new[] { Run(canonical), Run(changed) }; gate.SetResult(); var results = await Task.WhenAll(tasks);
            Assert.Single(results, x => x.Status == Application.Common.Interfaces.PermissionCatalogRegistrationStatus.Registered);
            Assert.Single(results, x => x.Status == Application.Common.Interfaces.PermissionCatalogRegistrationStatus.Conflict);
            await AssertExactMod0007ResidueAsync(database);
        }
        finally { await client.DropDatabaseAsync(name); }
    }

    [Fact]
    public async Task Soft_deleted_matching_permission_fails_closed_without_manifest_residue()
    {
        var (client, database, name) = await CreateDatabaseAsync();
        try
        {
            var manifest = GateIPermissionCatalogManifests.Mod0007;
            var key = manifest.Entries[0].PermissionKey;
            var parts = key.Split('.');
            var permission = new Permission(parts[0], string.Join('.', parts[1..^1]), parts[^1], key, null, manifest.OwnerModuleId, PermissionScope.Tenant) { IsDeleted = true };
            await database.GetCollection<Permission>("permissions").InsertOneAsync(permission);
            var result = await new PermissionCatalogManifestRegistrar(client, database).RegisterAsync(manifest, CancellationToken.None);
            Assert.Equal(Application.Common.Interfaces.PermissionCatalogRegistrationStatus.Conflict, result.Status);
            Assert.Equal(0, await database.GetCollection<ManifestDocument>("s2sPermissionCatalogManifests").CountDocumentsAsync(FilterDefinition<ManifestDocument>.Empty));
            Assert.Equal(0, await database.GetCollection<OperationDocument>("s2sPermissionCatalogOperations").CountDocumentsAsync(FilterDefinition<OperationDocument>.Empty));
            Assert.True((await database.GetCollection<Permission>("permissions").Find(x => x.Key == key).SingleAsync()).IsDeleted);
        }
        finally { await client.DropDatabaseAsync(name); }
    }

    [Fact]
    public async Task Existing_exact_permission_is_reused_and_cancellation_has_zero_residue()
    {
        var (client, database, name) = await CreateDatabaseAsync();
        try
        {
            var manifest = GateIPermissionCatalogManifests.Mod0072;
            var key = manifest.Entries[0].PermissionKey; var parts = key.Split('.');
            var permission = new Permission(parts[0], string.Join('.', parts[1..^1]), parts[^1], key, null, manifest.OwnerModuleId, PermissionScope.Tenant);
            await database.GetCollection<Permission>("permissions").InsertOneAsync(permission);
            var registrar = new PermissionCatalogManifestRegistrar(client, database);
            var result = await registrar.RegisterAsync(manifest, CancellationToken.None);
            Assert.Equal(Application.Common.Interfaces.PermissionCatalogRegistrationStatus.Registered, result.Status);
            Assert.Equal(permission.Id, (await database.GetCollection<PermissionOwnerDocument>("s2sPermissionCatalogPermissionOwners").Find(x => x.PermissionKey == key).SingleAsync()).PermissionId);

            var otherDatabase = client.GetDatabase(name + "_cancel"); using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new PermissionCatalogManifestRegistrar(client, otherDatabase).RegisterAsync(manifest, cancelled.Token));
            Assert.Equal(0, await otherDatabase.GetCollection<ManifestDocument>("s2sPermissionCatalogManifests").CountDocumentsAsync(FilterDefinition<ManifestDocument>.Empty));
            await client.DropDatabaseAsync(name + "_cancel");
        }
        finally { await client.DropDatabaseAsync(name); }
    }

    [Fact]
    public async Task Persistent_hash_operation_owner_and_permission_owner_conflicts_are_fail_closed()
    {
        async Task WithDatabase(Func<MongoClient, IMongoDatabase, Task> assertion)
        {
            var (client, database, name) = await CreateDatabaseAsync();
            try { await assertion(client, database); } finally { await client.DropDatabaseAsync(name); }
        }
        var manifest = GateIPermissionCatalogManifests.Mod0007;
        await WithDatabase(async (client, database) =>
        {
            await database.GetCollection<ManifestDocument>("s2sPermissionCatalogManifests").InsertOneAsync(WithHash(ManifestDocument.From(Guid.NewGuid(), manifest), "different"));
            var result = await new PermissionCatalogManifestRegistrar(client, database).RegisterAsync(manifest, CancellationToken.None);
            Assert.Equal(Application.Common.Interfaces.PermissionCatalogRegistrationStatus.Conflict, result.Status);
        });
        await WithDatabase(async (client, database) =>
        {
            await database.GetCollection<OperationDocument>("s2sPermissionCatalogOperations").InsertOneAsync(new() { OwnerModuleId = "MOD-9999", ManifestVersion = "v", OperationId = manifest.Entries[0].OperationId, PermissionKey = manifest.Entries[0].PermissionKey });
            var result = await new PermissionCatalogManifestRegistrar(client, database).RegisterAsync(manifest, CancellationToken.None);
            Assert.Equal(Application.Common.Interfaces.PermissionCatalogRegistrationStatus.Conflict, result.Status);
            Assert.Equal(0, await database.GetCollection<ManifestDocument>("s2sPermissionCatalogManifests").CountDocumentsAsync(FilterDefinition<ManifestDocument>.Empty));
        });
        await WithDatabase(async (client, database) =>
        {
            var key = manifest.Entries[0].PermissionKey; var parts = key.Split('.');
            await database.GetCollection<Permission>("permissions").InsertOneAsync(new Permission(parts[0], string.Join('.', parts[1..^1]), parts[^1], key, null, "MOD-9999", PermissionScope.Tenant));
            var result = await new PermissionCatalogManifestRegistrar(client, database).RegisterAsync(manifest, CancellationToken.None);
            Assert.Equal(Application.Common.Interfaces.PermissionCatalogRegistrationStatus.Conflict, result.Status);
            Assert.Equal(0, await database.GetCollection<ManifestDocument>("s2sPermissionCatalogManifests").CountDocumentsAsync(FilterDefinition<ManifestDocument>.Empty));
        });

        static ManifestDocument WithHash(ManifestDocument document, string hash) { document.PayloadHash = hash; return document; }
    }

    private static async Task<(MongoClient Client, IMongoDatabase Database, string Name)> CreateDatabaseAsync()
    {
        var uri = Environment.GetEnvironmentVariable("MONGO_TEST_URI") ?? throw new InvalidOperationException("MONGO_TEST_URI required");
        var url = new MongoUrl(uri);
        if (url.Servers.Any(x => x.Port is 27017 or 27018)) throw new InvalidOperationException("Protected Mongo ports are forbidden.");
        var client = new MongoClient(uri); var name = "diten_auth_b2a1_" + Guid.NewGuid().ToString("N");
        var database = client.GetDatabase(name); await database.RunCommandAsync<MongoDB.Bson.BsonDocument>(new MongoDB.Bson.BsonDocument("ping", 1));
        return (client, database, name);
    }

    private static async Task<long> CountAsync(IMongoDatabase database, string collection) =>
        await database.GetCollection<MongoDB.Bson.BsonDocument>(collection).CountDocumentsAsync(FilterDefinition<MongoDB.Bson.BsonDocument>.Empty);

    private static async Task AssertUniqueIndexAsync<T>(IMongoCollection<T> collection, string name)
    {
        var indexes = await (await collection.Indexes.ListAsync()).ToListAsync();
        var index = Assert.Single(indexes, x => x["name"].AsString == name);
        Assert.True(index["unique"].ToBoolean());
    }

    private static async Task<string> SnapshotAsync(IMongoDatabase database)
    {
        var names = new[] { "permissions", "rolePermissions", "roleAssignmentVersions", "entitlementStates" };
        var parts = new List<string>();
        foreach (var name in names)
        {
            var documents = await database.GetCollection<BsonDocument>(name).Find(FilterDefinition<BsonDocument>.Empty).Sort(new BsonDocument("_id", 1)).ToListAsync();
            parts.Add(name + ":" + string.Join('|', documents.Select(x => Convert.ToHexString(x.ToBson()))));
        }
        return string.Join('\n', parts);
    }

    private static async Task AssertExactMod0007ResidueAsync(IMongoDatabase database)
    {
        Assert.Equal(1, await CountAsync(database, "s2sPermissionCatalogManifests"));
        Assert.Equal(8, await CountAsync(database, "s2sPermissionCatalogOperations"));
        Assert.Equal(7, await CountAsync(database, "permissions"));
        Assert.Equal(7, await CountAsync(database, "s2sPermissionCatalogPermissionOwners"));
        Assert.Equal(1, await CountAsync(database, "s2sPermissionCatalogRegistrationAudits"));
        Assert.Equal(0, await CountAsync(database, "rolePermissions"));
        Assert.Equal(0, await CountAsync(database, "roleAssignmentVersions"));
        Assert.Equal(0, await CountAsync(database, "entitlementStates"));
    }

    private sealed class RecordingProbe : IPermissionCatalogTransactionProbe
    {
        public PermissionCatalogTransactionParticipant? FailAfter { get; set; }
        public int UnknownBeforeSendCount { get; set; }
        public bool UnknownAfterDurableCommit { get; set; }
        public int BodyExecutions { get; private set; }
        public int CommitAttempts { get; private set; }
        private bool _afterCommitThrown;
        public void BodyStarted() => BodyExecutions++;
        public Task AfterParticipantAsync(PermissionCatalogTransactionParticipant participant, CancellationToken cancellationToken)
        {
            if (FailAfter == participant) throw new PermissionCatalogInjectedFailureException();
            return Task.CompletedTask;
        }
        public PermissionCatalogCommitDirective BeforeCommit(int attempt)
        {
            CommitAttempts++;
            return attempt <= UnknownBeforeSendCount ? PermissionCatalogCommitDirective.SimulateUnknownBeforeSend : PermissionCatalogCommitDirective.Send;
        }
        public void AfterCommit(int attempt)
        {
            if (UnknownAfterDurableCommit && !_afterCommitThrown)
            { _afterCommitThrown = true; throw new PermissionCatalogUnknownCommitException(true); }
        }
    }
}

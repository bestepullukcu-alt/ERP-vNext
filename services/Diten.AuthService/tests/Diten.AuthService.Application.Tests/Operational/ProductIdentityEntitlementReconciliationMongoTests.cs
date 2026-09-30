using Diten.AuthService.Application.Common.Entitlements;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Application.Tests.Persistence;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Bson.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Diten.AuthService.Persistence;
using Diten.AuthService.Application.Common.Interfaces;

namespace Diten.AuthService.Application.Tests.Operational;

[Collection(AuthReconciliationMongoCollection.Name)]
public sealed class ProductIdentityEntitlementReconciliationMongoTests(DisposableAuthMongoReplicaSet mongo)
{
    [Fact]
    public async Task OperationalPersistence_WhenRegistered_ContainsOnlyDatabaseAndStoreAndPerformsNoInitializationWrites()
    {
        await mongo.ResetAsync(); var before = await AllRowsAsync();
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["MongoDbSettings:ConnectionString"] = mongo.ConnectionString, ["MongoDbSettings:DatabaseName"] = DisposableAuthMongoReplicaSet.DatabaseName }).Build();
        services.AddEntitlementReconciliationPersistence(configuration);
        Assert.Equal(new[] { typeof(IMongoDatabase), typeof(IEntitlementReconciliationOperationStore) }, services.Select(d => d.ServiceType));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
        Assert.IsType<EntitlementReconciliationOperationStore>(provider.GetRequiredService<IEntitlementReconciliationOperationStore>());
        var database = provider.GetRequiredService<IMongoDatabase>();
        Assert.Equal(EntitlementReconciliationOperationStore.ApplicationName, database.Client.Settings.ApplicationName);
        Assert.False(database.Client.Settings.RetryWrites); Assert.Equal(ReadConcern.Majority, database.Client.Settings.ReadConcern);
        Assert.Equal(WriteConcern.WMajority, database.Client.Settings.WriteConcern);
        await AssertSameRowsAsync(before);
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("deleted")]
    [InlineData("unconfirmed")]
    [InlineData("must-change-password")]
    [InlineData("stale-user-role")]
    [InlineData("deleted-role")]
    [InlineData("deleted-permission")]
    [InlineData("revoked-permission")]
    public async Task Read_WhenPersistedOperatorAuthorityIsInvalid_DeniesDespiteOtherwiseValidClaims(string state)
    {
        var fixture = await SeedPlanAsync(); var adminTenant = Id(EntitlementReconciliationPlan.AdminTenant);
        var collection = "users"; var filter = new BsonDocument("_id", Id(fixture.Plan.ActorId));
        var field = state switch { "inactive" => "IsActive", "unconfirmed" => "EmailConfirmed", "must-change-password" => "MustChangePassword", _ => "IsDeleted" };
        BsonValue value = state is "inactive" or "unconfirmed" ? BsonBoolean.False : BsonBoolean.True;
        if (state == "stale-user-role") { collection = "userRoles"; filter = new("UserId", Id(fixture.Plan.ActorId)); field = "RoleId"; value = Id(Guid.NewGuid()); }
        if (state == "deleted-role") { collection = "roles"; filter = new("TenantId", adminTenant); }
        if (state == "deleted-permission") { collection = "permissions"; filter = new("Key", EntitlementReconciliationPlan.RequiredPermission); }
        if (state == "revoked-permission") { collection = "rolePermissions"; filter = new("TenantId", adminTenant); }
        await mongo.Database.GetCollection<BsonDocument>(collection).UpdateOneAsync(filter, new BsonDocument("$set", new BsonDocument(field, value)));
        var before = await AllRowsAsync(); fixture.Probe.Commands.Clear();
        var local = await fixture.Store.ReadAsync(fixture.Plan.TenantId, fixture.Plan.ActorId, CancellationToken.None);
        Assert.False(local.Operator.IsAuthorized);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.CommitAsync(fixture.Plan, CancellationToken.None));
        Assert.Empty(fixture.Probe.Writes); await AssertSameRowsAsync(before);
    }
    [Theory]
    [InlineData("versionless-protected-row")]
    [InlineData("new-holder")]
    [InlineData("new-token")]
    [InlineData("operator-revoked")]
    [InlineData("token-after-revoke")]
    [InlineData("grant-insert")]
    [InlineData("grant-reactivate")]
    [InlineData("grant-delete")]
    [InlineData("role-change")]
    [InlineData("permission-change")]
    [InlineData("grant-then-delayed-version")]
    public async Task Commit_WhenWriterAppearsAfterSnapshot_CannotFinalizeSuccess(string drift)
    {
        var fixture = await SeedPlanAsync(); var fired = false; var delayedVersion = false;
        fixture.Probe.After = (name, command) =>
        {
            if (fired && drift == "grant-then-delayed-version" && name == "commitTransaction")
            {
                mongo.Database.GetCollection<BsonDocument>("auth_role_assignment_versions").UpdateOne(new BsonDocument("_id", Id(fixture.Plan.TenantId)),
                    new BsonDocument("$inc", new BsonDocument("Version", 1L))); delayedVersion = true; return;
            }
            var barrier = drift == "token-after-revoke"
                ? name == "update" && command["update"] == "refreshTokens"
                : name == "find" && command["find"] == "permissions" && command.Contains("startTransaction");
            if (fired || !barrier) return;
            fired = true;
            switch (drift)
            {
                case "versionless-protected-row":
                    mongo.Database.GetCollection<BsonDocument>("tenant_user_memberships").UpdateOne(new BsonDocument("TenantId", Id(fixture.Plan.TenantId)),
                        new BsonDocument("$set", new BsonDocument("AfterSnapshot", true))); break;
                case "new-holder":
                    mongo.Database.GetCollection<UserRole>("userRoles").InsertOne(new(Guid.NewGuid(),
                        fixture.Plan.Rows.Single(r => r.Action == "remove").RoleId, fixture.Plan.TenantId, "late-writer")); break;
                case "new-token":
                case "token-after-revoke":
                    mongo.Database.GetCollection<RefreshToken>("refreshTokens").InsertOne(new(fixture.Plan.AffectedHolderIds.Single(),
                        Guid.NewGuid().ToString(), DateTime.UtcNow.AddHours(1), "test", fixture.Plan.TenantId, "tenant_user")); break;
                case "operator-revoked":
                    mongo.Database.GetCollection<BsonDocument>("users").UpdateOne(new BsonDocument("_id", Id(fixture.Plan.ActorId)),
                        new BsonDocument("$set", new BsonDocument("IsActive", false))); break;
                case "grant-insert":
                case "grant-then-delayed-version":
                    var retirePermission = mongo.Database.GetCollection<Permission>("permissions").Find(p => p.Key == "mdm.gskus.retire").Single();
                    var stewardId = fixture.Plan.Rows.First(r => r.RoleName == "ProductDataSteward").RoleId;
                    mongo.Database.GetCollection<RolePermission>("rolePermissions").InsertOne(RolePermission.ManualGrant(stewardId, retirePermission.Id,
                        fixture.Plan.TenantId, "late-sessionless-writer")); break;
                case "grant-reactivate":
                    mongo.Database.GetCollection<BsonDocument>("rolePermissions").UpdateOne(new BsonDocument
                        { { "TenantId", Id(fixture.Plan.TenantId) }, { "IsDeleted", true } }, new BsonDocument("$set", new BsonDocument("IsDeleted", false))); break;
                case "grant-delete":
                    mongo.Database.GetCollection<BsonDocument>("rolePermissions").DeleteOne(new BsonDocument
                        { { "TenantId", Id(fixture.Plan.TenantId) }, { "GrantSource", (int)GrantSource.Manual }, { "IsDeleted", false } }); break;
                case "role-change":
                    mongo.Database.GetCollection<BsonDocument>("roles").UpdateOne(new BsonDocument("_id", Id(fixture.Plan.Rows[0].RoleId)),
                        new BsonDocument("$set", new BsonDocument("Description", "late-role-writer"))); break;
                case "permission-change":
                    mongo.Database.GetCollection<BsonDocument>("permissions").UpdateOne(new BsonDocument("_id", Id(fixture.Plan.Rows[0].PermissionId)),
                        new BsonDocument("$set", new BsonDocument("Description", "late-catalog-writer"))); break;
            }
        };
        var committed = await fixture.Store.CommitAsync(fixture.Plan, CancellationToken.None);
        Assert.True(fired); Assert.NotNull(committed.Receipt);
        if (drift == "grant-then-delayed-version") Assert.True(delayedVersion);
        fixture.Probe.After = null;
        var outcome = await fixture.Store.FinalizeAsync(fixture.Plan, true, null, committed.Receipt.PostStateFingerprint, CancellationToken.None);
        Assert.Equal("COMMITTED_MANUAL_RECONCILIATION_REQUIRED", outcome.State);
        Assert.Equal(drift == "operator-revoked" ? "OPERATOR_AUTHORITY_DRIFT_AFTER_COMMIT" : "LOCAL_CONCURRENCY_DRIFT_AFTER_COMMIT", outcome.Reason);
    }

    [Fact]
    public async Task Commit_WhenVersionedWriterWinsAfterSnapshot_AbortsWithoutPartialCommandWrites()
    {
        var fixture = await SeedPlanAsync(); var before = await AllRowsAsync(); var fired = false;
        fixture.Probe.After = (name, command) =>
        {
            if (fired || name != "find" || command["find"] != "permissions" || !command.Contains("startTransaction")) return;
            fired = true;
            mongo.Database.GetCollection<BsonDocument>("auth_role_assignment_versions").UpdateOne(new BsonDocument("_id", Id(fixture.Plan.TenantId)),
                new BsonDocument("$inc", new BsonDocument("Version", 1L)));
        };
        await Assert.ThrowsAnyAsync<MongoException>(() => fixture.Store.CommitAsync(fixture.Plan, CancellationToken.None));
        Assert.True(fired); var after = await AllRowsAsync();
        foreach (var collection in before.Keys.Where(c => c != "auth_role_assignment_versions")) Assert.Equal(before[collection], after[collection]);
        Assert.Equal(8L, after["auth_role_assignment_versions"].Single()["Version"].AsInt64);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InsertExisting_WhenVerifiedCollectionIsDroppedOrRecreated_RejectsUuidWithoutImplicitCreation(bool recreate)
    {
        await mongo.ResetAsync(); var probe = new MongoCommandProbe();
        var store = new EntitlementReconciliationOperationStore(mongo.NewClient(probe).GetDatabase(DisposableAuthMongoReplicaSet.DatabaseName));
        await store.VerifyStorageAsync(CancellationToken.None);
        await mongo.Database.DropCollectionAsync("authAuditLogs");
        if (recreate) await mongo.Database.CreateCollectionAsync("authAuditLogs");
        probe.Commands.Clear();
        var method = typeof(EntitlementReconciliationOperationStore).GetMethod("InsertExistingAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var task = (Task)method.Invoke(store, ["authAuditLogs", new[] { new BsonDocument("_id", Id(Guid.NewGuid())) }, null, CancellationToken.None])!;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Equal("WRITE_ACKNOWLEDGEMENT_UNCERTAIN", error.Message);
        var reply = Assert.Single(probe.Replies, r => r.Name == "insert").Reply;
        // MongoDB error_codes.yml: 361 is CollectionUUIDMismatch; per-write replies omit codeName.
        Assert.Equal(361, Assert.Single(reply["writeErrors"].AsBsonArray).AsBsonDocument["code"].AsInt32);
        using var names = await mongo.Database.ListCollectionNamesAsync();
        Assert.Equal(recreate, (await names.ToListAsync()).Contains("authAuditLogs"));
        if (recreate) Assert.Equal(0, await mongo.Database.GetCollection<BsonDocument>("authAuditLogs").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        Assert.Single(probe.Writes); Assert.True(probe.Writes.Single().Command.Contains("collectionUUID"));
        await mongo.ResetAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Read_WhenUnknownActiveWriterTargetsAuthDatabase_RejectsObservedWriter(bool transactional)
    {
        var fixture = await SeedPlanAsync(); var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var probe = new MongoCommandProbe { Before = (name, _) => { if (name == "insert") started.TrySetResult(); } };
        var writer = mongo.NewClient(probe, "Untrusted.Test.Writer");
        await mongo.Client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument
        {
            { "configureFailPoint", "failCommand" }, { "mode", new BsonDocument("times", 1) },
            { "data", new BsonDocument { { "failCommands", new BsonArray { "insert" } }, { "appName", "Untrusted.Test.Writer" },
                { "blockConnection", true }, { "blockTimeMS", 3000 } } }
        });
        using var session = await writer.StartSessionAsync();
        if (transactional) session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot, ReadPreference.Primary, WriteConcern.WMajority));
        var collection = writer.GetDatabase(DisposableAuthMongoReplicaSet.DatabaseName).GetCollection<BsonDocument>("tenant_user_memberships");
        var row = new BsonDocument { { "_id", Id(Guid.NewGuid()) }, { "TenantId", Id(fixture.Plan.TenantId) } };
        var insertion = transactional ? collection.InsertOneAsync(session, row) : collection.InsertOneAsync(row);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            InvalidOperationException? observed = null;
            for (var attempt = 0; attempt < 30 && observed is null; attempt++)
            {
                try { await fixture.Store.ReadAsync(fixture.Plan.TenantId, fixture.Plan.ActorId, CancellationToken.None); }
                catch (InvalidOperationException error) when (error.Message == "UNKNOWN_AUTH_WRITER_OBSERVED") { observed = error; }
                if (observed is null) await Task.Delay(50);
            }
            Assert.NotNull(observed);
        }
        finally
        {
            await mongo.Client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument { { "configureFailPoint", "failCommand" }, { "mode", "off" } });
            await insertion.WaitAsync(TimeSpan.FromSeconds(6));
            if (session.IsInTransaction) await session.AbortTransactionAsync();
        }
    }

    [Theory]
    [InlineData("collection")]
    [InlineData("index")]
    public async Task VerifyStorage_WhenRequiredExistingStorageIsMissing_RefusesAndCreatesNothing(string missing)
    {
        await mongo.ResetAsync();
        if (missing == "collection") await mongo.Database.DropCollectionAsync("tenant_user_memberships");
        else await mongo.Database.GetCollection<BsonDocument>("permissions").Indexes.DropOneAsync("Key_1");
        var probe = new MongoCommandProbe(); var store = new EntitlementReconciliationOperationStore(
            mongo.NewClient(probe).GetDatabase(DisposableAuthMongoReplicaSet.DatabaseName));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => store.VerifyStorageAsync(CancellationToken.None));
        Assert.Equal(missing == "collection" ? "EXISTING_COLLECTION_REQUIRED" : "EXISTING_INDEX_REQUIRED", error.Message);
        Assert.Empty(probe.Writes); await mongo.ResetAsync();
    }

    [Fact]
    public async Task Read_WhenUnattributedIdleClientExists_RecordsResidualFingerprintWithoutInventingAuthAttribution()
    {
        var fixture = await SeedPlanAsync();
        var idle = mongo.NewClient(applicationName: "Unknown.Idle.Client");
        await idle.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        var snapshot = await fixture.Store.ReadAsync(fixture.Plan.TenantId, fixture.Plan.ActorId, CancellationToken.None);
        Assert.NotEqual(fixture.Plan.QuiescenceFingerprint, snapshot.QuiescenceFingerprint);
        Assert.Equal(fixture.Plan.LocalFingerprint, snapshot.Fingerprint);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Commit_WhenAcknowledgementIsLostAfterRealCommit_ClassifiesByReadbackWithoutRetry(bool readbackUnavailable)
    {
        var fixture = await SeedPlanAsync(); var committed = false;
        fixture.Probe.After = (name, _) =>
        {
            if (name != "commitTransaction") return;
            committed = true; throw new InvalidOperationException("TEST_LOST_COMMIT_ACK");
        };
        fixture.Probe.Before = (name, command) =>
        {
            if (committed && readbackUnavailable && name == "find" && command["find"] == "authAuditLogs")
                throw new InvalidOperationException("TEST_READBACK_UNAVAILABLE");
        };
        var result = await fixture.Store.CommitAsync(fixture.Plan, CancellationToken.None);
        Assert.True(committed); Assert.Single(fixture.Probe.Commands, c => c.Name == "commitTransaction");
        Assert.Equal(readbackUnavailable ? "MANUAL_RECONCILIATION_REQUIRED" : "LOCAL_COMMITTED_AUTHORITY_REVALIDATION_PENDING", result.State);
        Assert.Equal(8, await mongo.Database.GetCollection<BsonDocument>("authAuditLogs").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        fixture.Probe.Before = null; fixture.Probe.After = null;
        Assert.Equal("LOCAL_COMMITTED_AUTHORITY_REVALIDATION_PENDING", (await fixture.Store.ReadReceiptAsync(fixture.Plan.OperationId, CancellationToken.None))!.State);
    }

    [Fact]
    public async Task Commit_WhenOwnedSessionIsKilledBeforeCommit_ReturnsNotAppliedWithUnchangedMajorityStateAndNoRetry()
    {
        var fixture = await SeedPlanAsync(); var before = await AllRowsAsync();
        var killed = false;
        fixture.Probe.Before = (name, command) =>
        {
            if (name != "commitTransaction") return;
            Assert.False(killed);
            // Kill only the captured session of this test's transaction, actually aborting server work.
            var reply = mongo.Client.GetDatabase("admin").RunCommand<BsonDocument>(new BsonDocument("killSessions",
                new BsonArray { command["lsid"].DeepClone() }));
            Assert.Equal(1, reply["ok"].ToInt32()); killed = true;
        };
        var result = await fixture.Store.CommitAsync(fixture.Plan, CancellationToken.None);
        Assert.True(killed);
        Assert.Equal("NOT_APPLIED", result.State); Assert.Null(result.Receipt);
        Assert.Single(fixture.Probe.Commands, c => c.Name == "commitTransaction");
        await AssertSameRowsAsync(before);
    }

    [Fact]
    public async Task Finalize_WhenTerminalAppendFails_PreservesPendingReceiptAndNoSuccess()
    {
        var fixture = await SeedPlanAsync(); var committed = await fixture.Store.CommitAsync(fixture.Plan, CancellationToken.None);
        fixture.Probe.Before = (name, command) =>
        {
            if (name == "insert" && command["insert"] == "authAuditLogs") throw new InvalidOperationException("TEST_TERMINAL_APPEND_FAILURE");
        };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.FinalizeAsync(fixture.Plan, true, null,
            committed.Receipt!.PostStateFingerprint, CancellationToken.None));
        Assert.Equal("TEST_TERMINAL_APPEND_FAILURE", error.Message);
        Assert.Equal(committed.Receipt, await fixture.Store.ReadReceiptAsync(fixture.Plan.OperationId, CancellationToken.None));
        Assert.Equal(8, await mongo.Database.GetCollection<BsonDocument>("authAuditLogs").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }
    [Fact]
    public async Task Commit_WhenExactPlanMatches_AtomicallyAppliesSixAddsOneRemovalRevocationAndEightAudits()
    {
        var fixture = await SeedPlanAsync();
        var before = await AllRowsAsync();
        fixture.Probe.Commands.Clear();

        var result = await fixture.Store.CommitAsync(fixture.Plan, CancellationToken.None);

        Assert.Equal("LOCAL_COMMITTED_AUTHORITY_REVALIDATION_PENDING", result.State);
        Assert.NotNull(result.Receipt);
        var after = await AllRowsAsync();
        var addedIds = fixture.Plan.Rows.Where(r => r.Action == "add").Select(r => Id(r.GrantId)).ToHashSet();
        Assert.Equal(6, after["rolePermissions"].Count(r => addedIds.Contains(r["_id"])));
        Assert.DoesNotContain(after["rolePermissions"], r => r["_id"] == Id(EntitlementReconciliationPlan.RemovedGrant));
        foreach (var collection in before.Keys.Except(["authAuditLogs", "auth_role_assignment_versions", "rolePermissions", "refreshTokens"]))
            Assert.Equal(before[collection], after[collection]);
        Assert.Equal(before["rolePermissions"].Where(r => r["_id"] != Id(EntitlementReconciliationPlan.RemovedGrant)),
            after["rolePermissions"].Where(r => !addedIds.Contains(r["_id"])));
        foreach (var token in before["refreshTokens"])
        {
            var persisted = after["refreshTokens"].Single(r => r["_id"] == token["_id"]);
            if (fixture.Plan.ActiveRefreshTokenIds.Contains(token["_id"].AsBsonBinaryData.ToGuid()))
            { Assert.True(persisted["RevokedAt"].IsBsonDateTime); persisted["RevokedAt"] = BsonNull.Value; }
            Assert.Equal(token, persisted);
        }
        Assert.Equal(8, after["authAuditLogs"].Count);
        Assert.Equal(8L, after["auth_role_assignment_versions"].Single()["Version"].AsInt64);
        Assert.All(fixture.Probe.Writes, write =>
        {
            Assert.Contains(write.Name, new[] { "insert", "update", "delete" });
            Assert.True(write.Command.Contains("lsid"));
            Assert.False(write.Command["autocommit"].AsBoolean);
            if (write.Name == "insert") Assert.True(write.Command.Contains("collectionUUID"));
        });
        var terminal = await fixture.Store.FinalizeAsync(fixture.Plan, true, null, result.Receipt.PostStateFingerprint, CancellationToken.None);
        Assert.Equal("SUCCESS", terminal.State);
        Assert.Equal(9, await mongo.Database.GetCollection<BsonDocument>("authAuditLogs").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        var replay = await fixture.Store.CommitAsync(fixture.Plan, CancellationToken.None);
        Assert.Equal(terminal, replay.Receipt);
        Assert.Equal(9, await mongo.Database.GetCollection<BsonDocument>("authAuditLogs").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [Theory]
    [InlineData("roles")]
    [InlineData("permissions")]
    [InlineData("rolePermissions")]
    [InlineData("users")]
    [InlineData("userRoles")]
    [InlineData("refreshTokens")]
    [InlineData("tenant_user_memberships")]
    public async Task Commit_WhenAnyProtectedRawRowChangesWithoutVersion_RejectsPlanAndWritesNothing(string collection)
    {
        var fixture = await SeedPlanAsync();
        var filter = collection == "permissions" ? new BsonDocument() : new BsonDocument("TenantId", Id(fixture.Plan.TenantId));
        await mongo.Database.GetCollection<BsonDocument>(collection).UpdateOneAsync(filter, new BsonDocument("$set", new BsonDocument("ProtectedMetadata", "changed")));
        var before = await AllRowsAsync(); fixture.Probe.Commands.Clear();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.CommitAsync(fixture.Plan, CancellationToken.None));

        Assert.Equal("PLAN_PRECONDITION_DRIFT", error.Message);
        Assert.Empty(fixture.Probe.Writes);
        await AssertSameRowsAsync(before);
    }

    [Fact]
    public async Task Finalize_WhenSuccessLaterDrifts_AppendsImmutableManualHoldAndCannotClearIt()
    {
        var fixture = await SeedPlanAsync();
        var committed = await fixture.Store.CommitAsync(fixture.Plan, CancellationToken.None);
        var success = await fixture.Store.FinalizeAsync(fixture.Plan, true, null, committed.Receipt!.PostStateFingerprint, CancellationToken.None);
        Assert.Equal("SUCCESS", success.State);
        await mongo.Database.GetCollection<BsonDocument>("tenant_user_memberships").UpdateOneAsync(
            new BsonDocument("TenantId", Id(fixture.Plan.TenantId)), new BsonDocument("$set", new BsonDocument("Changed", true)));

        var hold = await fixture.Store.FinalizeAsync(fixture.Plan, false, "LOCAL_CONCURRENCY_DRIFT_AFTER_COMMIT", success.PostStateFingerprint, CancellationToken.None);
        var replay = await fixture.Store.FinalizeAsync(fixture.Plan, true, null, success.PostStateFingerprint, CancellationToken.None);

        Assert.True(hold.ManualHold); Assert.Equal("COMMITTED_MANUAL_RECONCILIATION_REQUIRED", hold.State);
        Assert.Equal(hold, replay); Assert.Equal(hold, await fixture.Store.ReadReceiptAsync(fixture.Plan.OperationId, CancellationToken.None));
        Assert.Equal(10, await mongo.Database.GetCollection<BsonDocument>("authAuditLogs").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [Theory]
    [InlineData("tombstone")]
    [InlineData("duplicate")]
    [InlineData("malformed")]
    public async Task ReadReceipt_WhenEvidenceIsInvalid_FailsClosed(string mutation)
    {
        var fixture = await SeedPlanAsync();
        await fixture.Store.CommitAsync(fixture.Plan, CancellationToken.None);
        var audits = mongo.Database.GetCollection<BsonDocument>("authAuditLogs");
        var id = Id(EntitlementReconciliationPlan.DeterministicId(fixture.Plan.OperationId, "receipt:local"));
        var row = await audits.Find(new BsonDocument("_id", id)).SingleAsync();
        if (mutation == "duplicate") { row["_id"] = Id(Guid.NewGuid()); await audits.InsertOneAsync(row); }
        else await audits.UpdateOneAsync(new BsonDocument("_id", id), new BsonDocument("$set",
            mutation == "tombstone" ? new BsonDocument("IsDeleted", true) : new BsonDocument("Metadata", "malformed")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.ReadReceiptAsync(fixture.Plan.OperationId, CancellationToken.None));
    }

    [Fact]
    public async Task Commit_WhenAuditInsertFails_RollsBackEveryLocalChange()
    {
        var fixture = await SeedPlanAsync(); var before = await AllRowsAsync();
        fixture.Probe.Before = (name, command) =>
        {
            if (name == "insert" && command["insert"] == "authAuditLogs") throw new InvalidOperationException("TEST_AUDIT_FAILURE");
        };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.CommitAsync(fixture.Plan, CancellationToken.None));
        Assert.Equal("TEST_AUDIT_FAILURE", error.Message);
        await AssertSameRowsAsync(before);
        Assert.Null(await fixture.Store.ReadReceiptAsync(fixture.Plan.OperationId, CancellationToken.None));
    }

    private sealed record Scenario(EntitlementReconciliationOperationStore Store, EntitlementReconciliationPlan Plan, MongoCommandProbe Probe);
    private async Task<Scenario> SeedPlanAsync()
    {
        await SeedVersionAsync(new BsonInt64(7));
        var tenant = EntitlementReconciliationPlan.TargetTenant;
        var retirement = await mongo.Database.GetCollection<Role>("roles").Find(FilterDefinition<Role>.Empty).SingleAsync();
        var steward = new Role("ProductDataSteward", "Steward", null, tenant); steward.MarkAsSystem();
        var negativeRole = new Role("Protected", "Protected", null, tenant); negativeRole.MarkAsSystem();
        var adminRole = new Role("Operator", "Operator", null, EntitlementReconciliationPlan.AdminTenant); adminRole.MarkAsSystem();
        await mongo.Database.GetCollection<Role>("roles").InsertManyAsync([steward, negativeRole, adminRole]);
        var keys = new[] { "mdm.gskus.request-correction", "mdm.gskus.update", "mdm.gskus.withdraw", "mdm.lskus.withdraw",
            "mdm.gskus.request-retirement", "mdm.lskus.request-retirement", "mdm.lskus.retire", "mdm.gskus.retire",
            "mdm.product-identity-lifecycle-operations.recover" };
        var permissions = keys.Select(key => { var last = key.LastIndexOf('.'); return new Permission("mdm", key[4..last], key[(last + 1)..],
            key, null, moduleOverride: EntitlementReconciliationPlan.Module, scope: PermissionScope.Tenant); }).ToArray();
        var assign = new Permission("auth", "roles", "assign-permission", "Assign", null, scope: PermissionScope.PlatformAdmin);
        await mongo.Database.GetCollection<Permission>("permissions").InsertManyAsync(permissions.Append(assign));
        var actor = new User("operator@example.test", "test-hash-not-secret", "Operator", "Test", EntitlementReconciliationPlan.AdminTenant);
        actor.ConfirmEmail(); actor.SetPlatformActorType("platform_admin");
        var holder = new User("holder@example.test", "test-holder-hash", "Holder", "Test", tenant);
        var other = new User("other@example.test", "test-other-hash", "Other", "Test", Guid.NewGuid());
        await mongo.Database.GetCollection<User>("users").InsertManyAsync([actor, holder, other]);
        await mongo.Database.GetCollection<UserRole>("userRoles").InsertManyAsync([
            new(actor.Id, adminRole.Id, actor.TenantId, "test"), new(holder.Id, retirement.Id, tenant, "test"),
            new(other.Id, negativeRole.Id, other.TenantId, "test")]);
        var removal = new RolePermission(retirement.Id, permissions[6].Id, tenant, "test", GrantSource.Module, EntitlementReconciliationPlan.Module)
        { Id = EntitlementReconciliationPlan.RemovedGrant };
        await mongo.Database.GetCollection<RolePermission>("rolePermissions").InsertManyAsync([
            removal, RolePermission.SystemGrant(adminRole.Id, assign.Id, actor.TenantId, "test"),
            RolePermission.ManualGrant(negativeRole.Id, permissions[6].Id, tenant, "test"),
            RolePermission.SystemGrant(negativeRole.Id, permissions[7].Id, tenant, "test"),
            RolePermission.ModuleGrant(negativeRole.Id, permissions[8].Id, tenant, "historical", EntitlementReconciliationPlan.Module),
            RolePermission.ModuleGrant(negativeRole.Id, permissions[0].Id, tenant, "other", "other-module"),
            RolePermission.ManualGrant(negativeRole.Id, permissions[1].Id, other.TenantId, "test"),
            new(negativeRole.Id, permissions[2].Id, tenant, "deleted-test", GrantSource.Manual, null) { IsDeleted = true }]);
        await mongo.Database.GetCollection<RefreshToken>("refreshTokens").InsertManyAsync([
            new(holder.Id, Guid.NewGuid().ToString(), DateTime.UtcNow.AddHours(1), "test", tenant, "tenant_user"),
            new(other.Id, Guid.NewGuid().ToString(), DateTime.UtcNow.AddHours(1), "test", other.TenantId, "tenant_user")]);
        await mongo.Database.GetCollection<BsonDocument>("tenant_user_memberships").InsertOneAsync(new BsonDocument
            { { "_id", Id(Guid.NewGuid()) }, { "TenantId", Id(tenant) }, { "UserId", Id(holder.Id) }, { "IsDeleted", false } });
        var probe = new MongoCommandProbe();
        var store = new EntitlementReconciliationOperationStore(mongo.NewClient(probe).GetDatabase(DisposableAuthMongoReplicaSet.DatabaseName));
        var local = await store.ReadAsync(tenant, actor.Id, CancellationToken.None); Assert.True(local.Operator.IsAuthorized);
        var operation = Guid.NewGuid();
        var rows = permissions.Take(6).Select((permission, i) =>
        {
            var role = i < 4 ? steward : retirement;
            return new EntitlementReconciliationRow("add", EntitlementReconciliationPlan.DeterministicId(operation, "grant:" + role.Name + ":" + permission.Key),
                role.Id, role.Name, permission.Id, permission.Key, permission.Module, PermissionScope.Tenant, GrantSource.Module, EntitlementReconciliationPlan.Module);
        }).Append(new("remove", removal.Id, retirement.Id, retirement.Name, permissions[6].Id, permissions[6].Key,
            permissions[6].Module, PermissionScope.Tenant, GrantSource.Module, EntitlementReconciliationPlan.Module)).ToArray();
        var plan = new EntitlementReconciliationPlan(1, operation, tenant, EntitlementReconciliationPlan.Module, actor.Id, "PlatformAdministrator",
            local.Operator.Fingerprint, ReconciliationTestData.Digest, local.Fingerprint, local.QuiescenceFingerprint, 7,
            local.AffectedHolderIds, local.ActiveRefreshTokenIds, rows, ReconciliationTestData.Digest, ReconciliationTestData.Digest,
            new string('a', 40), DateTimeOffset.UtcNow);
        return new(store, plan, probe);
    }
    private static BsonBinaryData Id(Guid value) => new(value, GuidRepresentation.Standard);
    private async Task<Dictionary<string, List<BsonDocument>>> AllRowsAsync()
    {
        var result = new Dictionary<string, List<BsonDocument>>();
        foreach (var collection in DisposableAuthMongoReplicaSet.Collections)
            result[collection] = (await mongo.Database.GetCollection<BsonDocument>(collection).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync())
                .OrderBy(r => r["_id"].ToString(), StringComparer.Ordinal).ToList();
        return result;
    }
    private async Task AssertSameRowsAsync(Dictionary<string, List<BsonDocument>> before)
    {
        var after = await AllRowsAsync();
        foreach (var collection in before.Keys) Assert.Equal(before[collection], after[collection]);
    }
    [Fact]
    public async Task Read_WhenVersionIsExistingInt64_ReturnsExactVersionWithoutWrites()
    {
        await SeedVersionAsync(new BsonInt64(7));
        var probe = new MongoCommandProbe();
        var store = new EntitlementReconciliationOperationStore(mongo.NewClient(probe).GetDatabase(DisposableAuthMongoReplicaSet.DatabaseName));

        await store.VerifyStorageAsync(CancellationToken.None);
        var snapshot = await store.ReadAsync(EntitlementReconciliationPlan.TargetTenant, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(7, snapshot.RoleAssignmentVersion);
        Assert.Empty(probe.Writes);
    }

    [Theory]
    [InlineData(7.5)]
    [InlineData(7.0)]
    public async Task Read_WhenVersionIsDoubleInsteadOfExistingInt64_RejectsMalformedVersion(double version)
    {
        await SeedVersionAsync(new BsonDouble(version));
        var probe = new MongoCommandProbe();
        var store = new EntitlementReconciliationOperationStore(mongo.NewClient(probe).GetDatabase(DisposableAuthMongoReplicaSet.DatabaseName));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.ReadAsync(EntitlementReconciliationPlan.TargetTenant, Guid.NewGuid(), CancellationToken.None));

        Assert.Equal("EXISTING_VERSION_ROW_REQUIRED", error.Message);
        Assert.Empty(probe.Writes);
        var persisted = await mongo.Database.GetCollection<BsonDocument>("auth_role_assignment_versions")
            .Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
        Assert.Equal(new BsonDouble(version), persisted["Version"]);
    }

    [Theory]
    [InlineData("int32")]
    [InlineData("decimal")]
    [InlineData("null")]
    [InlineData("string")]
    [InlineData("negative")]
    [InlineData("overflow")]
    public async Task Read_WhenVersionStorageIsNonCanonicalOrOutOfRange_FailsClosed(string kind)
    {
        BsonValue value = kind switch
        {
            "int32" => new BsonInt32(7), "decimal" => new BsonDecimal128(7), "null" => BsonNull.Value,
            "string" => new BsonString("7"), "negative" => new BsonInt64(-1), _ => new BsonInt64(long.MaxValue)
        };
        await SeedVersionAsync(value);
        var store = new EntitlementReconciliationOperationStore(mongo.Database);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => store.ReadAsync(
            EntitlementReconciliationPlan.TargetTenant, Guid.NewGuid(), CancellationToken.None));
        Assert.Equal("EXISTING_VERSION_ROW_REQUIRED", error.Message);
    }

    private async Task SeedVersionAsync(BsonValue version)
    {
        await mongo.ResetAsync();
        var role = new Role(ProductIdentityLifecycleEntitlementGrantProfile.RetirementStewardRole,
            "Product Identity Retirement Steward", null, EntitlementReconciliationPlan.TargetTenant);
        role.MarkAsSystem();
        await mongo.Database.GetCollection<Role>("roles").InsertOneAsync(role);
        await mongo.Database.GetCollection<BsonDocument>("auth_role_assignment_versions").InsertOneAsync(new BsonDocument
        {
            { "_id", new BsonBinaryData(EntitlementReconciliationPlan.TargetTenant, GuidRepresentation.Standard) },
            { "Version", version }
        });
    }
}

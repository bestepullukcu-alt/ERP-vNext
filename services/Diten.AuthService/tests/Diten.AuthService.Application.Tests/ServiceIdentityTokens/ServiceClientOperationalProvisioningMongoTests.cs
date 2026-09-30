using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Api.Services.ServiceIdentityTokens;
using Diten.AuthService.Application.Features.ServiceIdentityTokens;
using Diten.AuthService.Application.Features.ServiceIdentityTokens.Operational;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.Repositories;
using Diten.AuthService.Infrastructure.Services;
using Diten.AuthService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using Xunit;
using Xunit.Abstractions;

namespace Diten.AuthService.Application.Tests.ServiceIdentityTokens;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class OperationalMongoCollection : ICollectionFixture<OperationalMongoFixture>
{
    public const string Name = "service-client-operational-owned-replica";
}

[Collection(OperationalMongoCollection.Name)]
public sealed class ServiceClientOperationalProvisioningMongoTests(OperationalMongoFixture fixture, ITestOutputHelper output)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rotation_preserves_ticks_and_replay_after_second_rotation_returns_recorded_old_outcome(bool legacyAbsentVersion)
    {
        await using var scope = await fixture.ScopeAsync(output, legacyAbsentVersion);
        var instant = DateTimeOffset.FromUnixTimeSeconds(1800000000).AddTicks(1234567);
        var sink = new OperationalRecordingSink();
        var service = scope.Service(sink, new FixedOperationalClock(instant));
        var first = scope.Request();
        fixture.Commands.Clear();
        var result = await service.ExecuteAsync(first, OperationalTestData.Actor, CancellationToken.None);
        Assert.Equal(1, result.OperationalVersion);
        var state = await scope.Identities.Find(x => x.Id == scope.Identity.Id).SingleAsync();
        Assert.Equal(instant.AddSeconds(300), state.PreviousValidUntilUtc);
        Assert.True(new ServiceClientCredentialVerifier().Verify(state, sink.LastSecret!, instant));
        var second = first with { CommandId = Guid.NewGuid(), ExpectedOperationalVersion = 1, ExpectedCredentialVersion = result.CredentialVersion };
        scope.Commands.Add(second.CommandId);
        var secondResult = await service.ExecuteAsync(second, OperationalTestData.Actor, CancellationToken.None);
        var replay = await service.ExecuteAsync(first, OperationalTestData.Actor, CancellationToken.None);
        Assert.True(replay.IsReplay);
        Assert.Equal(result.CredentialVersion, replay.CredentialVersion);
        Assert.Equal(1, replay.OperationalVersion);
        Assert.Equal(secondResult.CredentialVersion, replay.CurrentCredentialVersion);
        Assert.Equal(2, replay.CurrentOperationalVersion);
        Assert.Equal(2, sink.Deliveries);
        Assert.Equal(2, await scope.Journal.CountDocumentsAsync(x => x.TargetId == scope.Identity.Id));
        AssertNoUnexpectedWrites(fixture.Commands);
        var raw = await fixture.Database.GetCollection<BsonDocument>(OperationalMongoFixture.Journal).Find(new BsonDocument("CommandId", new BsonBinaryData(first.CommandId, GuidRepresentation.Standard))).SingleAsync();
        Assert.False(raw.ToJson().Contains(sink.LastSecret!, StringComparison.Ordinal), "Journal must never contain raw credential.");
    }

    [Fact]
    public async Task Same_key_drift_and_stale_version_do_not_mutate_again()
    {
        await using var scope = await fixture.ScopeAsync(output);
        var sink = new OperationalRecordingSink();
        var service = scope.Service(sink);
        var request = scope.Request();
        await service.ExecuteAsync(request, OperationalTestData.Actor, CancellationToken.None);
        await Assert.ThrowsAsync<ServiceClientOperationalConflictException>(() => service.ExecuteAsync(request with { Audience = "TRUSTED_AUDIT_SOURCE_INGEST" }, OperationalTestData.Actor, CancellationToken.None));
        await Assert.ThrowsAsync<ServiceClientOperationalConflictException>(() => service.ExecuteAsync(request with { CommandId = Guid.NewGuid() }, OperationalTestData.Actor, CancellationToken.None));
        Assert.Equal(1, sink.Deliveries);
        Assert.Equal(1, (await scope.Identities.Find(x => x.Id == scope.Identity.Id).SingleAsync()).OperationalVersion);
    }

    [Theory]
    [InlineData("client")]
    [InlineData("service")]
    [InlineData("purpose")]
    [InlineData("hash")]
    [InlineData("credential-version")]
    [InlineData("operational-version")]
    [InlineData("revoked")]
    [InlineData("deleted")]
    public async Task Repository_CAS_rejects_every_changed_identity_fact_without_overwriting(string drift)
    {
        await using var scope = await fixture.ScopeAsync(output);
        var before = scope.Identity;
        var now = DateTimeOffset.UtcNow;
        var mutation = new ServiceClientCredentialRotation(before.Id, before.ClientCode, before.ServiceName, before.AllowedAudience,
            0, before.ActiveCredentialHash, before.ActiveCredentialVersion, new ServiceClientCredentialVerifier().Hash("new-fixture-value"), "v2",
            now.AddSeconds(300), now, OperationalTestData.ActorId.ToString(), Guid.NewGuid(), "fixture-fingerprint");
        var update = Builders<ServiceClientIdentity>.Update;
        var change = drift switch
        {
            "client" => update.Set(x => x.ClientCode, before.ClientCode + "-changed"),
            "service" => update.Set(x => x.ServiceName, "other-service"),
            "purpose" => update.Set(x => x.AllowedAudience, "TRUSTED_AUDIT_SOURCE_INGEST"),
            "hash" => update.Set(x => x.ActiveCredentialHash, "changed-hash"),
            "credential-version" => update.Set(x => x.ActiveCredentialVersion, "changed-version"),
            "operational-version" => update.Set(x => x.OperationalVersion, 7),
            "revoked" => update.Set(x => x.IsRevoked, true),
            _ => update.Set(x => x.IsDeleted, true)
        };
        await scope.Identities.UpdateOneAsync(x => x.Id == before.Id, change);
        var changed = await scope.Identities.Find(x => x.Id == before.Id).SingleAsync();
        var result = await new ServiceClientIdentityRepository(fixture.Database).RotateCredentialOperationalAsync(mutation, CancellationToken.None);
        Assert.NotEqual(OperationalMutationStatus.Applied, result.Status);
        Assert.Equal(changed.ToBsonDocument(), (await scope.Identities.Find(x => x.Id == before.Id).SingleAsync()).ToBsonDocument());
        // Restore only this fixture-owned purpose field for the exact cleanup predicate.
        if (drift == "client") await scope.Identities.UpdateOneAsync(x => x.Id == before.Id, update.Set(x => x.ClientCode, before.ClientCode));
    }

    [Fact]
    public async Task Concurrent_different_commands_have_one_CAS_winner_and_zero_automatic_retry()
    {
        await using var scope = await fixture.ScopeAsync(output);
        var request = scope.Request();
        var second = request with { CommandId = Guid.NewGuid() };
        scope.Commands.Add(second.CommandId);
        var sink = new OperationalRecordingSink();
        var results = await Task.WhenAll(new[] { request, second }.Select(async item =>
        {
            try { await scope.Service(sink).ExecuteAsync(item, OperationalTestData.Actor, CancellationToken.None); return true; }
            catch (ServiceClientOperationalConflictException) { return false; }
        }));
        Assert.Single(results, x => x);
        Assert.Equal(1, sink.Deliveries);
        Assert.Equal(1, (await scope.Identities.Find(x => x.Id == scope.Identity.Id).SingleAsync()).OperationalVersion);
    }

    [Fact]
    public async Task Pending_and_ambiguous_outcome_are_manual_reconciliation_without_secret_retry()
    {
        await using var scope = await fixture.ScopeAsync(output);
        var request = scope.Request();
        var failingSink = new OperationalRecordingSink { FailDelivery = true };
        var service = scope.Service(failingSink);
        await Assert.ThrowsAsync<ServiceClientOperationalSecretDeliveryException>(() => service.ExecuteAsync(request, OperationalTestData.Actor, CancellationToken.None));
        var completed = await scope.Journal.Find(x => x.CommandId == request.CommandId).SingleAsync();
        Assert.Equal(ServiceClientOperationalProvisioningState.Completed, completed.State);
        var replaySink = new OperationalRecordingSink();
        var replay = await scope.Service(replaySink).ExecuteAsync(request, OperationalTestData.Actor, CancellationToken.None);
        Assert.True(replay.IsReplay);
        Assert.Equal(0, replaySink.Deliveries);
        await scope.Journal.UpdateOneAsync(x => x.CommandId == request.CommandId,
            Builders<ServiceClientOperationalProvisioningOperation>.Update.Set(x => x.State, ServiceClientOperationalProvisioningState.Pending));
        await Assert.ThrowsAsync<ServiceClientOperationalRecoveryRequiredException>(() => scope.Service(replaySink).ExecuteAsync(request, OperationalTestData.Actor, CancellationToken.None));
        Assert.Equal(0, replaySink.Deliveries);
    }

    [Theory]
    [InlineData("insert", 0, ServiceClientOperationalProvisioningState.Pending)]
    [InlineData("findAndModify", 1, ServiceClientOperationalProvisioningState.RecoveryRequired)]
    public async Task Real_server_lost_write_concern_acknowledgement_never_resumes_or_delivers_secret(
        string failCommand, long expectedIdentityVersion, ServiceClientOperationalProvisioningState expectedJournalState)
    {
        await using var scope = await fixture.ScopeAsync(output);
        var request = scope.Request();
        var sink = new OperationalRecordingSink();
        var admin = fixture.Database.Client.GetDatabase("admin");
        // Test-owned server only: Mongo executes the write and injects a write-concern error in its reply.
        // RetryWrites is false. This is not a hand-edited journal or a mocked repository acknowledgement.
        await admin.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            { "configureFailPoint", "failCommand" }, { "mode", new BsonDocument("times", 1) },
            { "data", new BsonDocument
                {
                    { "failCommands", new BsonArray { failCommand } },
                    { "appName", OperationalMongoFixture.ClientApplicationName },
                    { "writeConcernError", new BsonDocument { { "code", 64 }, { "errmsg", "test-owned acknowledgement uncertainty" } } }
                }
            }
        });
        try
        {
            fixture.Commands.Clear();
            var failure = await Record.ExceptionAsync(() => scope.Service(sink).ExecuteAsync(request, OperationalTestData.Actor, CancellationToken.None));
            Assert.NotNull(failure);
            Assert.True(failure is ServiceIdentityPersistenceUnavailableException or ServiceClientOperationalRecoveryRequiredException,
                "Ambiguous persisted write must be surfaced as storage unavailability or manual recovery.");
            Assert.Equal(0, sink.Deliveries);
            Assert.Single(fixture.Commands, x => x == failCommand + ":" +
                (failCommand == "insert" ? OperationalMongoFixture.Journal : "serviceClientIdentities"));
            var journal = await scope.Journal.Find(x => x.CommandId == request.CommandId).SingleAsync();
            Assert.Equal(expectedJournalState, journal.State);
            Assert.Null(journal.RecordedOutcome);
            Assert.Equal(ServiceClientOperationalProvisioningCheckpoint.Reserved, journal.Checkpoint);
            var beforeReplay = await scope.Identities.Find(x => x.Id == scope.Identity.Id).SingleAsync();
            Assert.Equal(expectedIdentityVersion, beforeReplay.OperationalVersion);
            var journalBeforeReplay = journal.ToBsonDocument();
            var replaySink = new OperationalRecordingSink();
            await Assert.ThrowsAsync<ServiceClientOperationalRecoveryRequiredException>(() => scope.Service(replaySink).ExecuteAsync(request, OperationalTestData.Actor, CancellationToken.None));
            Assert.Equal(0, replaySink.Deliveries);
            Assert.Equal(beforeReplay.ToBsonDocument(), (await scope.Identities.Find(x => x.Id == scope.Identity.Id).SingleAsync()).ToBsonDocument());
            Assert.Equal(journalBeforeReplay, (await scope.Journal.Find(x => x.CommandId == request.CommandId).SingleAsync()).ToBsonDocument());
            output.WriteLine($"Actual Mongo {failCommand} acknowledgement fault: durable journal={expectedJournalState}; identityVersion={expectedIdentityVersion}; secret deliveries=0; replay mutation=0.");
        }
        finally
        {
            await admin.RunCommandAsync<BsonDocument>(new BsonDocument { { "configureFailPoint", "failCommand" }, { "mode", "off" } });
        }
    }

    [Fact]
    public async Task Tombstoned_completed_command_cannot_be_replayed_or_reserved_as_new()
    {
        await using var scope = await fixture.ScopeAsync(output);
        var request = scope.Request();
        var sink = new OperationalRecordingSink();
        await scope.Service(sink).ExecuteAsync(request, OperationalTestData.Actor, CancellationToken.None);
        Assert.Equal(1, sink.Deliveries);
        var identityBefore = (await scope.Identities.Find(x => x.Id == scope.Identity.Id).SingleAsync()).ToBsonDocument();
        await scope.Journal.UpdateOneAsync(x => x.CommandId == request.CommandId && x.TargetId == scope.Identity.Id,
            Builders<ServiceClientOperationalProvisioningOperation>.Update.Set(x => x.IsDeleted, true));
        var tombstone = await scope.Journal.Find(x => x.CommandId == request.CommandId).SingleAsync();
        fixture.Commands.Clear();
        var replaySink = new OperationalRecordingSink();
        await Assert.ThrowsAsync<ServiceClientOperationalConflictException>(() => scope.Service(replaySink).ExecuteAsync(request, OperationalTestData.Actor, CancellationToken.None));
        Assert.Equal(0, replaySink.Deliveries);
        Assert.Equal(1, await scope.Journal.CountDocumentsAsync(x => x.CommandId == request.CommandId));
        Assert.Equal(tombstone.ToBsonDocument(), (await scope.Journal.Find(x => x.CommandId == request.CommandId).SingleAsync()).ToBsonDocument());
        Assert.Equal(identityBefore, (await scope.Identities.Find(x => x.Id == scope.Identity.Id).SingleAsync()).ToBsonDocument());
        Assert.DoesNotContain(fixture.Commands, IsWriteCommand);
    }

    [Fact]
    public async Task Actual_pipe_peer_closing_after_preflight_preserves_completed_outcome_but_reports_secret_uncertainty()
    {
        await using var scope = await fixture.ScopeAsync(output);
        using var pipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        await using var sink = new ServiceClientSecretOutputSink();
        var request = scope.Request() with { InheritedPipeHandle = pipe.GetClientHandleAsString() };
        await sink.PreflightAsync(request.InheritedPipeHandle!, CancellationToken.None);
        pipe.SafePipeHandle.Dispose();
        await Assert.ThrowsAsync<ServiceClientOperationalSecretDeliveryException>(() => scope.Service(sink).ExecuteAsync(request, OperationalTestData.Actor, CancellationToken.None));
        var operation = await scope.Journal.Find(x => x.CommandId == request.CommandId).SingleAsync();
        Assert.Equal(ServiceClientOperationalProvisioningState.Completed, operation.State);
        Assert.Equal(1, (await scope.Identities.Find(x => x.Id == scope.Identity.Id).SingleAsync()).OperationalVersion);
        var replaySink = new OperationalRecordingSink();
        Assert.True((await scope.Service(replaySink).ExecuteAsync(request, OperationalTestData.Actor, CancellationToken.None)).IsReplay);
        Assert.Equal(0, replaySink.Deliveries);
    }

    [Fact]
    public async Task Sanitized_grant_read_is_tenant_purpose_enabled_and_soft_delete_scoped_with_zero_writes()
    {
        await using var scope = await fixture.ScopeAsync(output);
        var grant = new ServiceClientTenantGrant { TenantId = scope.Tenant, ServiceClientIdentityId = scope.Identity.Id, Audience = scope.Identity.AllowedAudience!, IsEnabled = true };
        await scope.Grants.InsertOneAsync(grant);
        var request = scope.Request("read-grant");
        fixture.Commands.Clear();
        var result = await scope.Service(new OperationalRecordingSink()).ExecuteAsync(request, OperationalTestData.Actor, CancellationToken.None);
        Assert.True(result.GrantEnabled);
        await Assert.ThrowsAsync<ServiceClientOperationalNotFoundException>(() => scope.Service(new OperationalRecordingSink()).ExecuteAsync(request with { TenantId = Guid.NewGuid() }, OperationalTestData.Actor, CancellationToken.None));
        await Assert.ThrowsAsync<ServiceClientOperationalConflictException>(() => scope.Service(new OperationalRecordingSink()).ExecuteAsync(request with { Audience = "TRUSTED_AUDIT_SOURCE_INGEST" }, OperationalTestData.Actor, CancellationToken.None));
        Assert.DoesNotContain(fixture.Commands, x => x.StartsWith("insert:") || x.StartsWith("update:") || x.StartsWith("delete:") || x.StartsWith("findAndModify:"));
        await scope.Grants.UpdateOneAsync(x => x.Id == grant.Id && x.TenantId == scope.Tenant, Builders<ServiceClientTenantGrant>.Update.Set(x => x.IsDeleted, true));
        await Assert.ThrowsAsync<ServiceClientOperationalNotFoundException>(() => scope.Service(new OperationalRecordingSink()).ExecuteAsync(request, OperationalTestData.Actor, CancellationToken.None));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Journal_UUID_fence_prevents_implicit_create_or_insert_into_replaced_collection(bool recreate)
    {
        await using var scope = await fixture.ScopeAsync(output);
        var request = scope.Request();
        var intercepted = 0;
        var database = fixture.NewDatabase(command =>
        {
            if (command.CommandName == "insert" && command.Command.GetValue("insert", "") == OperationalMongoFixture.Journal && Interlocked.Exchange(ref intercepted, 1) == 0)
            {
                Assert.True(command.Command.Contains("collectionUUID"));
                // This is the fixture-owned, empty journal collection only, never the fixed database.
                Assert.Equal(0, fixture.Database.GetCollection<BsonDocument>(OperationalMongoFixture.Journal).CountDocuments(FilterDefinition<BsonDocument>.Empty));
                fixture.Database.DropCollection(OperationalMongoFixture.Journal);
                if (recreate) fixture.Database.CreateCollection(OperationalMongoFixture.Journal);
            }
        });
        var sink = new OperationalRecordingSink();
        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() => scope.Service(sink, database: database).ExecuteAsync(request, OperationalTestData.Actor, CancellationToken.None));
            Assert.Equal(1, intercepted);
            Assert.Equal(0, sink.Deliveries);
            var names = await (await fixture.Database.ListCollectionNamesAsync()).ToListAsync();
            Assert.Equal(recreate, names.Contains(OperationalMongoFixture.Journal));
            if (recreate) Assert.Equal(0, await scope.Journal.CountDocumentsAsync(FilterDefinition<ServiceClientOperationalProvisioningOperation>.Empty));
            Assert.Equal(0, (await scope.Identities.Find(x => x.Id == scope.Identity.Id).SingleAsync()).OperationalVersion);
        }
        finally { await fixture.EnsureJournalAsync(); }
    }

    [Fact]
    public async Task Missing_or_changed_index_metadata_fails_before_any_reservation_or_DDL()
    {
        await using var scope = await fixture.ScopeAsync(output);
        await scope.Journal.Indexes.DropOneAsync("ix_service_client_operational_state_updated");
        fixture.Commands.Clear();
        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() => scope.Service(new OperationalRecordingSink()).ExecuteAsync(scope.Request(), OperationalTestData.Actor, CancellationToken.None));
            Assert.DoesNotContain(fixture.Commands, x => x.StartsWith("insert:") || x.StartsWith("createIndexes:") || x.StartsWith("create:"));
        }
        finally { await fixture.EnsureJournalAsync(); }
    }

    [Theory]
    [InlineData("key")]
    [InlineData("order")]
    [InlineData("unique")]
    [InlineData("partial-missing")]
    [InlineData("partial-changed")]
    [InlineData("ttl")]
    [InlineData("hidden")]
    [InlineData("sparse")]
    [InlineData("collation")]
    public async Task Real_existing_index_metadata_mismatch_is_read_only_fail_closed(string mismatch)
    {
        await using var scope = await fixture.ScopeAsync(output);
        var compound = mismatch is "order" or "sparse";
        var name = compound ? "ix_service_client_operational_state_updated" : "ux_service_client_operational_command_active";
        var key = compound ? new BsonDocument { { "State", 1 }, { "UpdatedAt", 1 } } : new BsonDocument("CommandId", 1);
        var options = new CreateIndexOptions<BsonDocument>
        {
            Name = name, Unique = !compound, PartialFilterExpression = compound ? null : new BsonDocument("IsDeleted", false)
        };
        switch (mismatch)
        {
            case "key": key = new BsonDocument("TargetId", 1); break;
            case "order": key = new BsonDocument { { "UpdatedAt", 1 }, { "State", 1 } }; break;
            case "unique": options.Unique = false; break;
            case "partial-missing": options.PartialFilterExpression = null; break;
            case "partial-changed": options.PartialFilterExpression = new BsonDocument("IsDeleted", true); break;
            case "ttl": options.ExpireAfter = TimeSpan.FromDays(1); break;
            case "hidden": options.Hidden = true; break;
            case "sparse": options.Sparse = true; break;
            case "collation": options.Collation = new Collation("en", strength: CollationStrength.Secondary); break;
        }
        var raw = fixture.Database.GetCollection<BsonDocument>(OperationalMongoFixture.Journal);
        await raw.Indexes.DropOneAsync(name);
        try
        {
            await raw.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(key, options));
            var persistedWrongSpec = (await (await raw.Indexes.ListAsync()).ToListAsync()).Single(x => x["name"] == name);
            fixture.Commands.Clear();
            var sink = new OperationalRecordingSink();
            await Assert.ThrowsAsync<ServiceIdentityPersistenceUnavailableException>(() => scope.Service(sink).ExecuteAsync(scope.Request(), OperationalTestData.Actor, CancellationToken.None));
            Assert.Equal(0, sink.Deliveries);
            Assert.DoesNotContain(fixture.Commands, IsWriteCommand);
            Assert.Equal(persistedWrongSpec, (await (await raw.Indexes.ListAsync()).ToListAsync()).Single(x => x["name"] == name));
            Assert.Equal(0, await scope.Journal.CountDocumentsAsync(x => x.TargetId == scope.Identity.Id));
            Assert.Equal(0, (await scope.Identities.Find(x => x.Id == scope.Identity.Id).SingleAsync()).OperationalVersion);
        }
        finally
        {
            await raw.Indexes.DropOneAsync(name);
            await fixture.EnsureJournalAsync();
        }
    }

    private static bool IsWriteCommand(string command) => new[]
    {
        "insert:", "update:", "delete:", "findAndModify:", "create:", "createIndexes:", "drop:", "dropIndexes:", "dropDatabase:", "collMod:"
    }.Any(prefix => command.StartsWith(prefix, StringComparison.Ordinal));

    private static void AssertNoUnexpectedWrites(IEnumerable<string> commands)
    {
        Assert.DoesNotContain(commands, x => x.StartsWith("create:") || x.StartsWith("createIndexes:") || x.StartsWith("drop:") || x.StartsWith("dropDatabase:"));
        var writes = commands.Where(x => x.StartsWith("insert:") || x.StartsWith("update:") || x.StartsWith("delete:") || x.StartsWith("findAndModify:"));
        Assert.All(writes, value => Assert.True(value.EndsWith(OperationalMongoFixture.Journal, StringComparison.Ordinal) || value.EndsWith("serviceClientIdentities", StringComparison.Ordinal), "Only journal and exact identity writes are permitted."));
    }
}

public sealed class OperationalMongoFixture : IAsyncLifetime
{
    internal const string DatabaseName = "diten_auth_service_identity_itest";
    internal const string Journal = "serviceClientOperationalProvisioningOperations";
    internal const string ClientApplicationName = "credential-operational-test";
    private Process? _process;
    private string _directory = string.Empty;
    internal string ConnectionString { get; private set; } = string.Empty;
    internal IMongoDatabase Database { get; private set; } = null!;
    internal ConcurrentQueue<string> Commands { get; } = new();
    internal int Pid => _process!.Id;
    internal int Port { get; private set; }
    public async Task InitializeAsync()
    {
        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        ConventionRegistry.Register("OwnedServiceClientOperationalIgnoreExtraElements",
            new ConventionPack { new IgnoreExtraElementsConvention(true) },
            type => type.Namespace == "Diten.AuthService.Domain.Entities");
        var executable = @"C:\Program Files\MongoDB\Server\7.0\bin\mongod.exe";
        if (!File.Exists(executable)) throw new InvalidOperationException("The installed Mongo test executable is unavailable; no fallback is allowed.");
        using (var listener = new TcpListener(IPAddress.Loopback, 0)) { listener.Start(); Port = ((IPEndPoint)listener.LocalEndpoint).Port; }
        if (Port == 27017) throw new InvalidOperationException("Forbidden application port.");
        _directory = Path.Combine(Path.GetTempPath(), "diten-auth-operational-owned-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "--dbpath", _directory, "--bind_ip", "127.0.0.1", "--port", Port.ToString(), "--replSet", "operational-fixture-rs", "--setParameter", "enableTestCommands=1", "--quiet" }) start.ArgumentList.Add(argument);
        _process = Process.Start(start) ?? throw new InvalidOperationException("Fixture process failed.");
        _process.OutputDataReceived += (_, _) => { }; _process.ErrorDataReceived += (_, _) => { };
        _process.BeginOutputReadLine(); _process.BeginErrorReadLine();
        try
        {
            var direct = new MongoClient($"mongodb://127.0.0.1:{Port}/?directConnection=true&serverSelectionTimeoutMS=1000");
            var admin = direct.GetDatabase("admin");
            var ready = false;
            for (var attempt = 0; attempt < 60; attempt++)
            {
                if (_process.HasExited) throw new InvalidOperationException("Owned Mongo exited before readiness.");
                try { await admin.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1)); ready = true; break; }
                catch (TimeoutException) { await Task.Delay(100); }
                catch (MongoException) { await Task.Delay(100); }
            }
            if (!ready) throw new InvalidOperationException("Owned Mongo did not become reachable.");
            await admin.RunCommandAsync<BsonDocument>(new BsonDocument("replSetInitiate", new BsonDocument { { "_id", "operational-fixture-rs" }, { "members", new BsonArray { new BsonDocument { { "_id", 0 }, { "host", $"127.0.0.1:{Port}" } } } } }));
            for (var attempt = 0; attempt < 100; attempt++)
            {
                var hello = await admin.RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1));
                if (hello.GetValue("setName", "") == "operational-fixture-rs" && hello.GetValue("isWritablePrimary", false).ToBoolean()) { ready = true; break; }
                ready = false; await Task.Delay(100);
            }
            if (!ready) throw new InvalidOperationException("Owned replica did not become PRIMARY.");
            ConnectionString = $"mongodb://127.0.0.1:{Port}/?replicaSet=operational-fixture-rs&retryWrites=false&serverSelectionTimeoutMS=5000";
            Database = NewDatabase();
            await Database.CreateCollectionAsync("serviceClientIdentities");
            await Database.CreateCollectionAsync("serviceClientTenantGrants");
            await IndexAsync("serviceClientIdentities", "ux_service_client_identity_code_active", new("ClientCode", 1), true, true);
            await IndexAsync("serviceClientTenantGrants", "ux_service_client_grant_tenant_client_audience_active", new() { { "TenantId", 1 }, { "ServiceClientIdentityId", 1 }, { "Audience", 1 } }, true, true);
            await IndexAsync("serviceClientTenantGrants", "ix_service_client_grant_tenant_enabled", new() { { "TenantId", 1 }, { "IsEnabled", 1 } });
            await EnsureJournalAsync();
        }
        catch { await DisposeAsync(); throw; }
    }
    internal IMongoDatabase NewDatabase(Action<CommandStartedEvent>? interceptor = null)
    {
        var settings = MongoClientSettings.FromConnectionString(ConnectionString);
        settings.GuidRepresentation = GuidRepresentation.Standard;
        settings.WriteConcern = WriteConcern.WMajority;
        settings.ApplicationName = ClientApplicationName;
        settings.ClusterConfigurator = builder => builder.Subscribe<CommandStartedEvent>(command =>
        {
            Commands.Enqueue(command.CommandName + ":" + command.Command.GetElement(0).Value.ToString());
            interceptor?.Invoke(command);
        });
        return new MongoClient(settings).GetDatabase(DatabaseName);
    }
    internal async Task EnsureJournalAsync()
    {
        var names = await (await Database.ListCollectionNamesAsync()).ToListAsync();
        if (!names.Contains(Journal)) await Database.CreateCollectionAsync(Journal);
        await IndexAsync(Journal, "ux_service_client_operational_command_active", new("CommandId", 1), true, true);
        await IndexAsync(Journal, "ix_service_client_operational_state_updated", new() { { "State", 1 }, { "UpdatedAt", 1 } });
    }
    private Task<string> IndexAsync(string collection, string name, BsonDocument key, bool unique = false, bool active = false) =>
        Database.GetCollection<BsonDocument>(collection).Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(key,
            new CreateIndexOptions<BsonDocument> { Name = name, Unique = unique, PartialFilterExpression = active ? new BsonDocument("IsDeleted", false) : null }));
    internal async Task<OperationalMongoScope> ScopeAsync(ITestOutputHelper output, bool absentVersion = false)
    {
        output.WriteLine($"Owned replica PID={Pid}; standalone PID=none (single process fixture); host=127.0.0.1; port={Port}; database={DatabaseName}; replica=operational-fixture-rs; primary=true.");
        var scope = new OperationalMongoScope(this, output);
        await scope.Identities.InsertOneAsync(scope.Identity);
        if (absentVersion) await scope.Identities.UpdateOneAsync(x => x.Id == scope.Identity.Id, Builders<ServiceClientIdentity>.Update.Unset(x => x.OperationalVersion));
        return scope;
    }
    public async Task DisposeAsync()
    {
        if (_process is not null)
        {
            if (!_process.HasExited && Database is not null)
            {
                try { await Database.Client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument { { "shutdown", 1 }, { "force", true } }); }
                catch (MongoException) { /* An owned server closes the command connection during shutdown. */ }
                catch (TimeoutException) { /* Fall back only to this fixture's owned process below. */ }
                try { await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (TimeoutException) { }
            }
            if (!_process.HasExited) { _process.Kill(entireProcessTree: true); await _process.WaitForExitAsync(); }
            if (!_process.HasExited) throw new InvalidOperationException("Owned replica failed to exit.");
            _process.Dispose();
        }
        if (!string.IsNullOrEmpty(_directory))
        {
            var resolved = Path.GetFullPath(_directory);
            var expectedRoot = Path.GetFullPath(Path.GetTempPath());
            if (!resolved.StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(resolved).StartsWith("diten-auth-operational-owned-", StringComparison.Ordinal))
                throw new InvalidOperationException("Unsafe fixture cleanup target.");
            for (var attempt = 0; Directory.Exists(resolved); attempt++)
            {
                try { Directory.Delete(resolved, recursive: true); }
                catch (IOException) when (attempt < 19) { await Task.Delay(100); }
            }
        }
    }
}

internal sealed class OperationalMongoScope(OperationalMongoFixture fixture, ITestOutputHelper output) : IAsyncDisposable
{
    internal Guid Tenant { get; } = Guid.NewGuid();
    internal ServiceClientIdentity Identity { get; } = new()
    {
        ClientCode = "fixture-" + Guid.NewGuid().ToString("N"), ServiceName = "Diten.MDM", AllowedAudience = "TRUSTED_WORKFLOW_CONSUMER",
        ActiveCredentialVersion = "v1", ActiveCredentialHash = new ServiceClientCredentialVerifier().Hash("test-only-original-credential")
    };
    internal List<Guid> Commands { get; } = [];
    internal IMongoCollection<ServiceClientIdentity> Identities => fixture.Database.GetCollection<ServiceClientIdentity>("serviceClientIdentities");
    internal IMongoCollection<ServiceClientTenantGrant> Grants => fixture.Database.GetCollection<ServiceClientTenantGrant>("serviceClientTenantGrants");
    internal IMongoCollection<ServiceClientOperationalProvisioningOperation> Journal => fixture.Database.GetCollection<ServiceClientOperationalProvisioningOperation>(OperationalMongoFixture.Journal);
    internal ServiceClientOperationalProvisioningRequest Request(string operation = "rotate-credential")
    {
        var request = OperationalTestData.Request(operation) with { ServiceClientIdentityId = Identity.Id, TenantId = operation == "read-grant" ? Tenant : null, ClientCode = Identity.ClientCode };
        Commands.Add(request.CommandId); return request;
    }
    internal ServiceClientOperationalProvisioningService Service(IServiceClientSecretOutputSink sink, TimeProvider? clock = null, IMongoDatabase? database = null)
    {
        database ??= fixture.Database;
        return new(new ServiceClientIdentityRepository(database), new ServiceClientTenantGrantRepository(database),
            new ServiceClientOperationalProvisioningOperationRepository(database), new ServiceClientCredentialVerifier(), sink, clock ?? TimeProvider.System);
    }
    public async ValueTask DisposeAsync()
    {
        var journalFilter = Builders<ServiceClientOperationalProvisioningOperation>.Filter.And(
            Builders<ServiceClientOperationalProvisioningOperation>.Filter.Eq(x => x.TargetId, Identity.Id),
            Builders<ServiceClientOperationalProvisioningOperation>.Filter.In(x => x.CommandId, Commands));
        var removedJournal = await Journal.DeleteManyAsync(journalFilter);
        var removedGrants = await Grants.DeleteManyAsync(x => x.TenantId == Tenant && x.ServiceClientIdentityId == Identity.Id);
        var removedIdentity = await Identities.DeleteOneAsync(x => x.Id == Identity.Id && x.ClientCode == Identity.ClientCode);
        Assert.True(removedJournal.IsAcknowledged && removedGrants.IsAcknowledged && removedIdentity.IsAcknowledged);
        Assert.Equal(0, await Journal.CountDocumentsAsync(journalFilter));
        Assert.Equal(0, await Grants.CountDocumentsAsync(x => x.TenantId == Tenant && x.ServiceClientIdentityId == Identity.Id));
        Assert.Equal(0, await Identities.CountDocumentsAsync(x => x.Id == Identity.Id));
        output.WriteLine("Tenant grants and fixture-owned identity/journal cleanup acknowledged; remaining=0; fixed DB not dropped.");
    }
}

internal sealed class OperationalRecordingSink : IServiceClientSecretOutputSink
{
    private int _deliveries;
    internal int Deliveries => _deliveries;
    internal string? LastSecret { get; private set; }
    internal bool FailDelivery { get; init; }
    public Task PreflightAsync(string inheritedPipeHandle, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task DeliverOnceAsync(Guid id, string code, string rawSecret, CancellationToken ct)
    {
        if (FailDelivery) throw new IOException("Fixture output failure.");
        Interlocked.Increment(ref _deliveries); LastSecret = rawSecret; return Task.CompletedTask;
    }
}

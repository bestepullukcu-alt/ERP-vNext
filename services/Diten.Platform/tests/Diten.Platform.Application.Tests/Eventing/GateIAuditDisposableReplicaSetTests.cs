using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Diten.Platform.Infrastructure.Eventing;
using Diten.Platform.Infrastructure.Persistence.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Eventing;

public sealed class GateIAuditDisposableReplicaSetTests
{
    [Fact]
    public async Task DisposableReplicaSet_ProvesAtomicIdempotentConflictAndCancellationMatrix()
    {
        await using var mongo = await DisposableReplicaSet.StartAsync();
        var client = new MongoClient(mongo.ConnectionString);
        var database = client.GetDatabase($"gate_i_{Guid.NewGuid():N}");
        try
        {
            await EnsureAuditOutboxIndexAsync(database);
            var repository = new GateIAuditAcceptanceRepository(client, database, TimeProvider.System);
            await repository.EnsureIndexesAsync();
            var profile = GateIAuditContractTests.Profiles.Budgeting;
            var registry = new GateIAuditAdapterRegistry();
            var message = GateIAuditContractTests.SyntheticMessage(profile);
            var intent = Parse(message, registry);

            Assert.Equal(GateIAuditAcceptanceResult.Accepted,
                await repository.AcceptAsync(message, intent, profile, CancellationToken.None));
            Assert.Equal(GateIAuditAcceptanceResult.Duplicate,
                await repository.AcceptAsync(message, intent, profile, CancellationToken.None));
            Assert.Equal((1, 1), await Counts(database));

            var changedBytes = GateIAuditContractTests.SyntheticMessage(profile,
                payloadOverride: GateIAuditContractTests.Payload("Budget", "updated"));
            var changedIntent = Parse(changedBytes, registry);
            await Assert.ThrowsAsync<GateIAuditConflictException>(() => repository.AcceptAsync(
                changedBytes, changedIntent, registry.Resolve(profile.Producer, profile.EventName, 1, "Budget", "updated"),
                CancellationToken.None));

            var changedMetadata = GateIAuditContractTests.SyntheticMessage(profile,
                correlationId: Guid.Parse("99999999-9999-9999-9999-999999999999"));
            await Assert.ThrowsAsync<GateIAuditConflictException>(() => repository.AcceptAsync(
                changedMetadata, Parse(changedMetadata, registry), profile, CancellationToken.None));
            await Assert.ThrowsAsync<GateIAuditConflictException>(() => repository.AcceptAsync(
                message, intent with { VerifiedSigningIdentity = "different-verified-signing-identity" }, profile,
                CancellationToken.None));
            Assert.Equal((1, 1), await Counts(database));

            await ProveConcurrentIdenticalAsync(client, database, profile, registry);
            await ProveCanonicalLegacyBothDirectionsAsync(client, database, profile, registry);
            await ProveOutboxFailureRollbackAsync(client, database, profile, registry);
            await ProveCancellationLeavesNoResidueAsync(client, database, profile, registry);
        }
        finally
        {
            await client.DropDatabaseAsync(database.DatabaseNamespace.DatabaseName);
        }
    }

    [Fact]
    public async Task DisposableStandaloneMongo_FailsClosedWithoutResidue()
    {
        await using var mongo = await DisposableReplicaSet.StartStandaloneAsync();
        var client = new MongoClient(mongo.ConnectionString);
        var database = client.GetDatabase($"gate_i_standalone_{Guid.NewGuid():N}");
        var profile = GateIAuditContractTests.Profiles.Budgeting;
        var registry = new GateIAuditAdapterRegistry();
        var message = GateIAuditContractTests.SyntheticMessage(profile);
        try
        {
            await EnsureAuditOutboxIndexAsync(database);
            var repository = new GateIAuditAcceptanceRepository(client, database);
            await repository.EnsureIndexesAsync();
            await Assert.ThrowsAnyAsync<Exception>(() => repository.AcceptAsync(
                message, Parse(message, registry), profile, CancellationToken.None));
            Assert.Equal((0, 0), await Counts(database));
        }
        finally
        {
            await client.DropDatabaseAsync(database.DatabaseNamespace.DatabaseName);
        }
    }

    private static async Task ProveConcurrentIdenticalAsync(IMongoClient client, IMongoDatabase database,
        GateIAuditMapping profile, GateIAuditAdapterRegistry registry)
    {
        var eventId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var payload = GateIAuditContractTests.Payload("Budget", "created")
            .Replace("11111111-1111-1111-1111-111111111111", eventId.ToString("D"), StringComparison.Ordinal);
        var message = GateIAuditContractTests.SyntheticMessage(profile, payload, eventId);
        var intent = Parse(message, registry);
        var first = new GateIAuditAcceptanceRepository(client, database);
        var second = new GateIAuditAcceptanceRepository(client, database);
        var results = await Task.WhenAll(
            AcceptWithTransientRetry(first, message, intent, profile),
            AcceptWithTransientRetry(second, message, intent, profile));
        Assert.Contains(GateIAuditAcceptanceResult.Accepted, results);
        Assert.Contains(GateIAuditAcceptanceResult.Duplicate, results);
    }

    private static async Task ProveOutboxFailureRollbackAsync(IMongoClient client, IMongoDatabase database,
        GateIAuditMapping profile, GateIAuditAdapterRegistry registry)
    {
        var eventId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var payload = GateIAuditContractTests.Payload("Budget", "created")
            .Replace("11111111-1111-1111-1111-111111111111", eventId.ToString("D"), StringComparison.Ordinal);
        var message = GateIAuditContractTests.SyntheticMessage(profile, payload, eventId);
        var idempotency = $"gate-i.audit-intent:{GateIAuditAcceptanceRepository.ConsumerName}:{eventId:D}";
        await database.GetCollection<AuditOutboxMessage>("audit_outbox").InsertOneAsync(new AuditOutboxMessage
        {
            TenantId = message.TenantId!.Value,
            CorrelationId = message.CorrelationId,
            IdempotencyKey = idempotency,
            RequestType = profile.EventName,
            Operation = profile.Operation,
            EntityType = profile.AuditEntityType,
            EntityId = Guid.Parse("44444444-4444-4444-4444-444444444444")
        });
        await Assert.ThrowsAnyAsync<Exception>(() => new GateIAuditAcceptanceRepository(client, database)
            .AcceptAsync(message, Parse(message, registry), profile, CancellationToken.None));
        Assert.Equal(0, await database.GetCollection<GateIAuditInboxMessage>(GateIAuditAcceptanceRepository.InboxCollectionName)
            .CountDocumentsAsync(item => item.EventId == eventId.ToString("D")));
    }

    private static async Task ProveCanonicalLegacyBothDirectionsAsync(
        IMongoClient client, IMongoDatabase database, GateIAuditMapping profile, GateIAuditAdapterRegistry registry)
    {
        foreach (var legacyFirst in new[] { false, true })
        {
            var eventId = legacyFirst
                ? Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd")
                : Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
            var payload = GateIAuditContractTests.Payload("Budget", "created")
                .Replace("11111111-1111-1111-1111-111111111111", eventId.ToString("D"), StringComparison.Ordinal);
            var canonical = GateIAuditContractTests.SyntheticMessage(profile, payload, eventId);
#pragma warning disable CS0618
            var legacy = new Diten.Platform.Application.Contracts.Eventing.EventTransportMessage(
                canonical.EventId, canonical.EventName, canonical.EventVersion, canonical.CorrelationId,
                canonical.CausationId, canonical.TenantId, canonical.Producer, canonical.OccurredAtUtc,
                canonical.PayloadJson, canonical.TransportMetadata.Headers);
#pragma warning restore CS0618
            var mappedLegacy = LegacyEventTransportMessageMapper.Map(legacy);
            var first = legacyFirst ? mappedLegacy : canonical;
            var second = legacyFirst ? canonical : mappedLegacy;
            var repository = new GateIAuditAcceptanceRepository(client, database);
            Assert.Equal(GateIAuditAcceptanceResult.Accepted, await repository.AcceptAsync(
                first, Parse(first, registry), profile, CancellationToken.None));
            Assert.Equal(GateIAuditAcceptanceResult.Duplicate, await repository.AcceptAsync(
                second, Parse(second, registry), profile, CancellationToken.None));
        }
    }

    private static async Task ProveCancellationLeavesNoResidueAsync(IMongoClient client, IMongoDatabase database,
        GateIAuditMapping profile, GateIAuditAdapterRegistry registry)
    {
        var eventId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var payload = GateIAuditContractTests.Payload("Budget", "created")
            .Replace("11111111-1111-1111-1111-111111111111", eventId.ToString("D"), StringComparison.Ordinal);
        var message = GateIAuditContractTests.SyntheticMessage(profile, payload, eventId);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new GateIAuditAcceptanceRepository(client, database)
            .AcceptAsync(message, Parse(message, registry), profile, cancellation.Token));
        Assert.Equal(0, await database.GetCollection<GateIAuditInboxMessage>(GateIAuditAcceptanceRepository.InboxCollectionName)
            .CountDocumentsAsync(item => item.EventId == eventId.ToString("D")));
    }

    private static async Task<GateIAuditAcceptanceResult> AcceptWithTransientRetry(
        GateIAuditAcceptanceRepository repository, Diten.BuildingBlocks.Eventing.EventTransportMessage message,
        GateIAuditIntent intent, GateIAuditMapping profile)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await repository.AcceptAsync(message, intent, profile, CancellationToken.None);
            }
            catch (MongoException exception) when (attempt < 5 &&
                (exception.HasErrorLabel("TransientTransactionError")
                 || exception.HasErrorLabel("UnknownTransactionCommitResult")))
            {
                await Task.Delay(25 * (attempt + 1));
            }
        }
    }

    private static GateIAuditIntent Parse(
        Diten.BuildingBlocks.Eventing.EventTransportMessage message,
        GateIAuditAdapterRegistry registry) => GateIAuditIntentParser.Parse(
        message, registry, GateIAuditContractTests.MechanicsSigningIdentity);

    private static async Task<(long Inbox, long Outbox)> Counts(IMongoDatabase database) => (
        await database.GetCollection<BsonDocument>(GateIAuditAcceptanceRepository.InboxCollectionName)
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty),
        await database.GetCollection<BsonDocument>("audit_outbox")
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));

    private static Task EnsureAuditOutboxIndexAsync(IMongoDatabase database) =>
        database.GetCollection<AuditOutboxMessage>("audit_outbox").Indexes.CreateOneAsync(
            new CreateIndexModel<AuditOutboxMessage>(
                Builders<AuditOutboxMessage>.IndexKeys.Ascending(item => item.IdempotencyKey),
                new CreateIndexOptions { Unique = true, Name = "ux_audit_outbox_idempotency_key" }));

    private sealed class DisposableReplicaSet : IAsyncDisposable
    {
        private readonly Process _process;
        private readonly string _directory;
        private DisposableReplicaSet(Process process, string directory, string connectionString)
        {
            _process = process;
            _directory = directory;
            ConnectionString = connectionString;
        }

        public string ConnectionString { get; }

        public static async Task<DisposableReplicaSet> StartAsync()
        {
            var port = FreePort();
            var directory = Path.Combine(Path.GetTempPath(), $"diten-gate-i-mongo-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            var setName = $"gate-i-rs-{Guid.NewGuid():N}";
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "/opt/homebrew/bin/mongod",
                ArgumentList =
                {
                    "--dbpath", directory,
                    "--port", port.ToString(),
                    "--bind_ip", "127.0.0.1",
                    "--replSet", setName,
                    "--logpath", Path.Combine(directory, "mongod.log"),
                    "--quiet"
                },
                UseShellExecute = false,
                CreateNoWindow = true
            }) ?? throw new InvalidOperationException("Disposable mongod failed to start.");
            var directConnection = $"mongodb://127.0.0.1:{port}/?directConnection=true&serverSelectionTimeoutMS=1000";
            var direct = new MongoClient(directConnection);
            await WaitForPing(direct);
            await direct.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument
            {
                ["replSetInitiate"] = new BsonDocument
                {
                    ["_id"] = setName,
                    ["members"] = new BsonArray { new BsonDocument { ["_id"] = 0, ["host"] = $"127.0.0.1:{port}" } }
                }
            });
            var connection = $"mongodb://127.0.0.1:{port}/?replicaSet={setName}&serverSelectionTimeoutMS=5000";
            var replicaClient = new MongoClient(connection);
            await WaitForPrimary(replicaClient);
            return new DisposableReplicaSet(process, directory, connection);
        }

        public static async Task<DisposableReplicaSet> StartStandaloneAsync()
        {
            var port = FreePort();
            var directory = Path.Combine(Path.GetTempPath(), $"diten-gate-i-mongo-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            var process = StartProcess(directory, port, replicaSet: null);
            var connection = $"mongodb://127.0.0.1:{port}/?directConnection=true&serverSelectionTimeoutMS=5000";
            await WaitForPing(new MongoClient(connection));
            return new DisposableReplicaSet(process, directory, connection);
        }

        public ValueTask DisposeAsync()
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(5000);
            }
            _process.Dispose();
            Directory.Delete(_directory, recursive: true);
            return ValueTask.CompletedTask;
        }

        private static int FreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static Process StartProcess(string directory, int port, string? replicaSet)
        {
            var start = new ProcessStartInfo
            {
                FileName = "/opt/homebrew/bin/mongod",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            start.ArgumentList.Add("--dbpath");
            start.ArgumentList.Add(directory);
            start.ArgumentList.Add("--port");
            start.ArgumentList.Add(port.ToString());
            start.ArgumentList.Add("--bind_ip");
            start.ArgumentList.Add("127.0.0.1");
            if (replicaSet is not null)
            {
                start.ArgumentList.Add("--replSet");
                start.ArgumentList.Add(replicaSet);
            }
            start.ArgumentList.Add("--logpath");
            start.ArgumentList.Add(Path.Combine(directory, "mongod.log"));
            start.ArgumentList.Add("--quiet");
            return Process.Start(start) ?? throw new InvalidOperationException("Disposable mongod failed to start.");
        }

        private static async Task WaitForPing(IMongoClient client)
        {
            for (var attempt = 0; attempt < 50; attempt++)
            {
                try
                {
                    await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
                    return;
                }
                catch (Exception)
                {
                    await Task.Delay(100);
                }
            }
            throw new InvalidOperationException("Disposable mongod did not become reachable.");
        }

        private static async Task WaitForPrimary(IMongoClient client)
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                try
                {
                    var hello = await client.GetDatabase("admin")
                        .RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1));
                    if (hello.TryGetValue("isWritablePrimary", out var primary) && primary.ToBoolean()) return;
                }
                catch (Exception)
                {
                }
                await Task.Delay(100);
            }
            throw new InvalidOperationException("Disposable replica set did not elect a primary.");
        }
    }
}

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Diten.Platform.Infrastructure.Eventing;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Models;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Eventing;

[CollectionDefinition(CollectionName, DisableParallelization = true)]
public sealed class GateIAuditMongoCollection
{
    public const string CollectionName = "MOD-0018 Gate-I disposable Mongo";
}

[Collection(GateIAuditMongoCollection.CollectionName)]
public sealed class GateIAuditDisposableReplicaSetTests
{
    private const string DatabaseName = "diten_platform_mod0018_gatei_tests";

    [Fact]
    public async Task EventingProfile_DeclaresAndAppliesGateIInboxExactlyOnceWithoutTtl()
    {
        var declarations = PlatformSchemaManifest.For(SchemaProfile.Eventing)
            .Where(item => item.Name == PlatformCollections.GateIAuditInbox)
            .ToArray();
        var declaration = Assert.Single(declarations);
        Assert.Equal(2, declaration.LogicalIndexCount); // implicit _id + exact unique identity index

        await using var mongo = await DisposableMongo.StartReplicaSetAsync();
        var client = new MongoClient(mongo.ConnectionString);
        var database = client.GetDatabase(DatabaseName);
        await CleanAsync(database);
        try
        {
            await PlatformSchemaManifest.ApplyAsync(database, [SchemaProfile.Eventing]);
            await PlatformSchemaManifest.ApplyAsync(database, [SchemaProfile.Eventing]);

            using var cursor = await database
                .GetCollection<BsonDocument>(PlatformCollections.GateIAuditInbox)
                .Indexes.ListAsync();
            var indexes = await cursor.ToListAsync();
            Assert.Equal(2, indexes.Count);
            var identity = Assert.Single(indexes, item =>
                item["name"].AsString == "ux_gate_i_audit_inbox_consumer_event");
            Assert.True(identity["unique"].ToBoolean());
            Assert.Equal(new[] { "ConsumerName", "EventId" },
                identity["key"].AsBsonDocument.Names.ToArray());
            Assert.DoesNotContain(indexes, item => item.Contains("expireAfterSeconds"));
        }
        finally
        {
            await CleanAsync(database);
        }
    }

    [Fact]
    public async Task DisposableReplicaSet_ProvesAtomicIdempotentConflictAndCancellationMatrix()
    {
        await using var mongo = await DisposableMongo.StartReplicaSetAsync();
        var client = new MongoClient(mongo.ConnectionString);
        var database = client.GetDatabase(DatabaseName);
        await CleanAsync(database);
        try
        {
            await PlatformSchemaManifest.ApplyAsync(database, [SchemaProfile.Eventing]);
            var repository = new GateIAuditAcceptanceRepository(client, database, TimeProvider.System);
            var profile = GateIAuditContractTests.Profiles.Budgeting;
            var registry = new GateIAuditAdapterRegistry();
            var message = GateIAuditContractTests.SyntheticMessage(profile);
            var intent = Parse(message, registry);

            Assert.Equal(GateIAuditAcceptanceResult.Accepted,
                await repository.AcceptAsync(message, intent, profile, CancellationToken.None));
            Assert.Equal(GateIAuditAcceptanceResult.Duplicate,
                await repository.AcceptAsync(message, intent, profile, CancellationToken.None));
            Assert.Equal((1, 1), await Counts(database));

            var changedBytes = GateIAuditContractTests.SyntheticMessage(
                profile,
                payloadOverride: GateIAuditContractTests.Payload("Budget", "updated"));
            await Assert.ThrowsAsync<GateIAuditConflictException>(() => repository.AcceptAsync(
                changedBytes,
                Parse(changedBytes, registry),
                registry.Resolve(profile.Producer, profile.EventName, 1, "Budget", "updated"),
                CancellationToken.None));

            var changedMetadata = GateIAuditContractTests.SyntheticMessage(
                profile,
                correlationId: Guid.Parse("99999999-9999-9999-9999-999999999999"));
            await Assert.ThrowsAsync<GateIAuditConflictException>(() => repository.AcceptAsync(
                changedMetadata, Parse(changedMetadata, registry), profile, CancellationToken.None));
            await Assert.ThrowsAsync<GateIAuditConflictException>(() => repository.AcceptAsync(
                message,
                intent with { VerifiedSigningIdentity = "different-verified-signing-identity" },
                profile,
                CancellationToken.None));
            Assert.Equal((1, 1), await Counts(database));

            await ProveConcurrentIdenticalAsync(client, database, profile, registry);
            await ProveCancellationLeavesNoResidueAsync(client, database, profile, registry);
        }
        finally
        {
            await CleanAsync(database);
        }
    }

    [Fact]
    public async Task DisposableStandaloneMongo_FailsClosedWithoutResidue()
    {
        await using var mongo = await DisposableMongo.StartStandaloneAsync();
        var client = new MongoClient(mongo.ConnectionString);
        var database = client.GetDatabase(DatabaseName);
        await CleanAsync(database);
        try
        {
            await PlatformSchemaManifest.ApplyAsync(database, [SchemaProfile.Eventing]);
            var profile = GateIAuditContractTests.Profiles.Budgeting;
            var registry = new GateIAuditAdapterRegistry();
            var message = GateIAuditContractTests.SyntheticMessage(profile);

            await Assert.ThrowsAnyAsync<Exception>(() => new GateIAuditAcceptanceRepository(client, database)
                .AcceptAsync(message, Parse(message, registry), profile, CancellationToken.None));
            Assert.Equal((0, 0), await Counts(database));
        }
        finally
        {
            await CleanAsync(database);
        }
    }

    private static async Task ProveConcurrentIdenticalAsync(
        IMongoClient client,
        IMongoDatabase database,
        GateIAuditMapping profile,
        GateIAuditAdapterRegistry registry)
    {
        var eventId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var payload = GateIAuditContractTests.Payload("Budget", "created")
            .Replace("11111111-1111-1111-1111-111111111111", eventId.ToString("D"), StringComparison.Ordinal);
        var message = GateIAuditContractTests.SyntheticMessage(profile, payload, eventId);
        var intent = Parse(message, registry);
        var first = new GateIAuditAcceptanceRepository(client, database);
        var second = new GateIAuditAcceptanceRepository(client, database);

        var results = await Task.WhenAll(
            AcceptWithTransientBodyRetryAsync(first, message, intent, profile),
            AcceptWithTransientBodyRetryAsync(second, message, intent, profile));
        Assert.Contains(GateIAuditAcceptanceResult.Accepted, results);
        Assert.Contains(GateIAuditAcceptanceResult.Duplicate, results);
    }

    private static async Task ProveCancellationLeavesNoResidueAsync(
        IMongoClient client,
        IMongoDatabase database,
        GateIAuditMapping profile,
        GateIAuditAdapterRegistry registry)
    {
        var eventId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var payload = GateIAuditContractTests.Payload("Budget", "created")
            .Replace("11111111-1111-1111-1111-111111111111", eventId.ToString("D"), StringComparison.Ordinal);
        var message = GateIAuditContractTests.SyntheticMessage(profile, payload, eventId);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new GateIAuditAcceptanceRepository(client, database)
                .AcceptAsync(message, Parse(message, registry), profile, cancellation.Token));
        Assert.Equal(0, await database
            .GetCollection<GateIAuditInboxMessage>(PlatformCollections.GateIAuditInbox)
            .CountDocumentsAsync(item => item.EventId == eventId.ToString("D")));
    }

    private static async Task<GateIAuditAcceptanceResult> AcceptWithTransientBodyRetryAsync(
        GateIAuditAcceptanceRepository repository,
        Diten.BuildingBlocks.Eventing.EventTransportMessage message,
        GateIAuditIntent intent,
        GateIAuditMapping profile)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await repository.AcceptAsync(message, intent, profile, CancellationToken.None);
            }
            catch (MongoException exception) when (
                attempt < 2 && exception.HasErrorLabel("TransientTransactionError"))
            {
                await Task.Delay(25 * (attempt + 1));
            }
        }
    }

    private static GateIAuditIntent Parse(
        Diten.BuildingBlocks.Eventing.EventTransportMessage message,
        GateIAuditAdapterRegistry registry) =>
        GateIAuditIntentParser.Parse(message, registry, GateIAuditContractTests.MechanicsSigningIdentity);

    private static async Task<(long Inbox, long Outbox)> Counts(IMongoDatabase database) => (
        await database.GetCollection<BsonDocument>(PlatformCollections.GateIAuditInbox)
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty),
        await database.GetCollection<BsonDocument>(AuditCollectionNames.AuditOutbox)
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));

    private static async Task CleanAsync(IMongoDatabase database)
    {
        await database.GetCollection<BsonDocument>(PlatformCollections.GateIAuditInbox)
            .DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);
        await database.GetCollection<BsonDocument>(AuditCollectionNames.AuditOutbox)
            .DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);
    }

    private sealed class DisposableMongo : IAsyncDisposable
    {
        private readonly Process _process;
        private readonly string _directory;

        private DisposableMongo(Process process, string directory, string connectionString)
        {
            _process = process;
            _directory = directory;
            ConnectionString = connectionString;
        }

        public string ConnectionString { get; }

        public static async Task<DisposableMongo> StartReplicaSetAsync()
        {
            var port = FreePort();
            var directory = CreateDirectory();
            var setName = $"mod0018-gate-i-{Guid.NewGuid():N}";
            var process = StartProcess(directory, port, setName);
            try
            {
                var direct = new MongoClient(
                    $"mongodb://127.0.0.1:{port}/?directConnection=true&serverSelectionTimeoutMS=1000");
                await WaitForPingAsync(direct);
                await direct.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument
                {
                    ["replSetInitiate"] = new BsonDocument
                    {
                        ["_id"] = setName,
                        ["members"] = new BsonArray
                        {
                            new BsonDocument { ["_id"] = 0, ["host"] = $"127.0.0.1:{port}" }
                        }
                    }
                });
                var connection =
                    $"mongodb://127.0.0.1:{port}/?replicaSet={setName}&serverSelectionTimeoutMS=5000";
                await WaitForPrimaryAsync(new MongoClient(connection));
                return new DisposableMongo(process, directory, connection);
            }
            catch
            {
                Stop(process, directory);
                throw;
            }
        }

        public static async Task<DisposableMongo> StartStandaloneAsync()
        {
            var port = FreePort();
            var directory = CreateDirectory();
            var process = StartProcess(directory, port, null);
            try
            {
                var connection =
                    $"mongodb://127.0.0.1:{port}/?directConnection=true&serverSelectionTimeoutMS=5000";
                await WaitForPingAsync(new MongoClient(connection));
                return new DisposableMongo(process, directory, connection);
            }
            catch
            {
                Stop(process, directory);
                throw;
            }
        }

        public ValueTask DisposeAsync()
        {
            Stop(_process, _directory);
            return ValueTask.CompletedTask;
        }

        private static string CreateDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"diten-mod0018-gate-i-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static int FreePort()
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                listener.Stop();
                if (port >= 27022)
                {
                    return port;
                }
            }
            throw new InvalidOperationException("Unable to allocate a disposable Mongo port >= 27022.");
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
            return Process.Start(start)
                ?? throw new InvalidOperationException("Disposable mongod failed to start.");
        }

        private static void Stop(Process process, string directory)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
            }
            finally
            {
                process.Dispose();
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }

        private static async Task WaitForPingAsync(IMongoClient client)
        {
            for (var attempt = 0; attempt < 50; attempt++)
            {
                try
                {
                    await client.GetDatabase("admin")
                        .RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
                    return;
                }
                catch (Exception)
                {
                    await Task.Delay(100);
                }
            }
            throw new InvalidOperationException("Disposable mongod did not become reachable.");
        }

        private static async Task WaitForPrimaryAsync(IMongoClient client)
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                try
                {
                    var hello = await client.GetDatabase("admin")
                        .RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1));
                    if (hello.TryGetValue("isWritablePrimary", out var primary) && primary.ToBoolean())
                    {
                        return;
                    }
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

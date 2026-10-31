using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Diten.PvgService.Application.RegPvBase;
using Diten.PvgService.Domain.RegPvBase;
using Diten.PvgService.Infrastructure.RegPvBase;
using Diten.PvgService.Persistence;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using Xunit;

namespace Diten.PvgService.Api.Tests.Persistence;

// WP-0230-DL10, AC-35 (d), (e): the tests that stop a mongod, set a fail point, or need a server of another shape. Each
// starts its own mongod (enableTestCommands=1), never touches the one the other Mongo tests share, and runs one after the
// other in this non-parallel collection.
[Collection(PvgDedicatedMongoCollection.Name)]
public sealed class PvgMongoDedicatedTests
{
    private const string StoreAppName = "pvg-intake-store-test";
    private const string Actor = "actor-dedicated-fake";

    // ---- AC-35 (d): start refusals and readiness --------------------------------------------------------------------

    [Fact]
    public async Task Unreachable_mongo_store_refuses_start()
    {
        // A closed local port: bind a loopback listener to learn a free port, then close it.
        int port;
        using (var listener = new TcpListener(IPAddress.Loopback, 0))
        {
            listener.Start();
            port = ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        PvgMongoTestHarness.ResolvePort(port);
        var configuration = PvgMongoApiHost.Configuration(
            "Mongo",
            $"mongodb://127.0.0.1:{port}/?directConnection=true",
            PvgMongoTestHarness.SharedDatabaseName);
        using var host = PvgMongoApiHost.Build(configuration);

        var clock = Stopwatch.StartNew();
        var exception = await Assert.ThrowsAnyAsync<Exception>(() => host.StartAsync());
        clock.Stop();

        var refusal = Unwrap(exception);
        Assert.Equal("PVG_PERSISTENCE_STORE_UNREACHABLE", refusal.Message);
        Assert.DoesNotContain("127.0.0.1", exception.ToString(), StringComparison.Ordinal);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(15), $"the refusal took {clock.Elapsed}");
    }

    [Fact]
    public async Task Ready_returns_503_when_the_selected_mongo_store_stops_answering()
    {
        await using var mongod = await PvgOwnedMongod.StartAsync(replicaSet: true);
        await using var host = await PvgMongoApiHost.StartMongoAsync(mongod.ConnectionString);

        // Waits for a ready host.
        var ready = await host.GetReadyAsync();
        Assert.Equal(200, ready.StatusCode);

        await mongod.StopAsync();

        var clock = Stopwatch.StartNew();
        var notReady = await host.GetReadyAsync();
        clock.Stop();

        Assert.Equal(503, notReady.StatusCode);
        Assert.Contains("PVG_DEPENDENCY_INTAKE_STORE_UNREACHABLE", notReady.Raw, StringComparison.Ordinal);
        Assert.DoesNotContain("127.0.0.1", notReady.Raw, StringComparison.Ordinal);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(15), $"the readiness answer took {clock.Elapsed}");
    }

    [Fact]
    public async Task Mongo_store_refuses_start_without_transaction_support()
    {
        // A standalone mongod: collection and indexes can be created, a transaction cannot start.
        await using var standalone = await PvgOwnedMongod.StartAsync(replicaSet: false);
        using var host = PvgMongoApiHost.Build(PvgMongoApiHost.Configuration(
            "Mongo", standalone.ConnectionString, PvgMongoTestHarness.SharedDatabaseName));

        var exception = await Assert.ThrowsAnyAsync<Exception>(() => host.StartAsync());

        var refusal = Unwrap(exception);
        Assert.Equal("PVG_PERSISTENCE_TRANSACTIONS_UNAVAILABLE", refusal.Message);
        Assert.DoesNotContain("127.0.0.1", exception.ToString(), StringComparison.Ordinal);
    }

    // ---- AC-35 (e): a write is lost when its transaction cannot commit ----------------------------------------------

    [Fact]
    public async Task Mongo_write_is_lost_when_its_transaction_commit_fails()
    {
        await using var mongod = await PvgOwnedMongod.StartAsync(replicaSet: true);
        var commits = new CommitCounter();
        await using var services = PvgMongoTestStores.BuildServices(mongod.ConnectionString, StoreSettings(commits));
        await PvgMongoTestStores.RunBootstrapAsync(services);
        var store = services.GetRequiredService<IPvgIntakeDraftStore>();
        var raw = mongod.CreateRawClient();
        var tenant = PvgMongoTestData.NewTenant();
        var scope = new PvgPersistenceTenantScope(tenant);

        await SetCommitFailPointAsync(raw, on: true);
        try
        {
            commits.Reset();

            await Assert.ThrowsAnyAsync<Exception>(async () =>
                await store.AddAsync(scope, Actor, PvgMongoTestData.Intake(tenant)));

            Assert.Equal(1, commits.Count);
        }
        finally
        {
            await SetCommitFailPointAsync(raw, on: false);
        }

        Assert.Equal(0, await RawCountAsync(raw, tenant));
        Assert.Equal(0, await store.CountAsync(scope, null));
    }

    [Fact]
    public async Task Mongo_replace_is_lost_when_its_transaction_commit_fails()
    {
        await using var mongod = await PvgOwnedMongod.StartAsync(replicaSet: true);
        var commits = new CommitCounter();
        await using var services = PvgMongoTestStores.BuildServices(mongod.ConnectionString, StoreSettings(commits));
        await PvgMongoTestStores.RunBootstrapAsync(services);
        var store = services.GetRequiredService<IPvgIntakeDraftStore>();
        var raw = mongod.CreateRawClient();
        var tenant = PvgMongoTestData.NewTenant();
        var scope = new PvgPersistenceTenantScope(tenant);

        // The first draft is written with the fail point off (version 1).
        var intakeDraftId = await store.AddAsync(
            scope, Actor, PvgMongoTestData.Intake(tenant, narrative: "narrative-before"));

        await SetCommitFailPointAsync(raw, on: true);
        try
        {
            commits.Reset();

            await Assert.ThrowsAnyAsync<Exception>(async () =>
                await store.ReplaceAsync(
                    scope,
                    intakeDraftId,
                    1,
                    "actor-replacing-fake",
                    PvgMongoTestData.Intake(tenant, PvgIntakeStatus.IntakeUpdated, narrative: "narrative-after")));

            Assert.Equal(1, commits.Count);
        }
        finally
        {
            await SetCommitFailPointAsync(raw, on: false);
        }

        var stored = await store.FindByIdAsync(scope, intakeDraftId);
        Assert.Equal(1, stored!.Version);
        Assert.Equal("narrative-before", stored.Intake.AdverseEventNarrative);
        Assert.Equal(PvgIntakeStatus.IntakeCreated, stored.Intake.Status);

        var document = await raw
            .GetDatabase(PvgMongoTestHarness.SharedDatabaseName)
            .GetCollection<BsonDocument>(PvgIntakeDraftIndexCatalog.CollectionName)
            .Find(new BsonDocument("IntakeDraftId", intakeDraftId))
            .FirstAsync();
        Assert.Equal(Actor, document["UpdatedBy"].AsString);
    }

    // The store's own client: a known application name (the fail point matches it) and a command event subscription.
    private static Action<MongoClientSettings> StoreSettings(CommitCounter commits) =>
        settings =>
        {
            settings.ApplicationName = StoreAppName;
            settings.ClusterConfigurator = builder => builder.Subscribe<CommandStartedEvent>(commits.OnStarted);
        };

    // failCommand on commitTransaction: BadValue (2) is not retryable and carries no error label, so the driver must
    // neither retry the commit nor report the outcome as unknown.
    private static async Task SetCommitFailPointAsync(IMongoClient raw, bool on)
    {
        var command = new BsonDocument { { "configureFailPoint", "failCommand" } };
        if (on)
        {
            command.Add("mode", "alwaysOn");
            command.Add("data", new BsonDocument
            {
                { "failCommands", new BsonArray { "commitTransaction" } },
                { "appName", StoreAppName },
                { "errorCode", 2 },
                { "errorLabels", new BsonArray() }
            });
        }
        else
        {
            command.Add("mode", "off");
        }

        await raw.GetDatabase("admin").RunCommandAsync<BsonDocument>(command);
    }

    private static async Task<long> RawCountAsync(IMongoClient raw, string tenant) =>
        await raw
            .GetDatabase(PvgMongoTestHarness.SharedDatabaseName)
            .GetCollection<BsonDocument>(PvgIntakeDraftIndexCatalog.CollectionName)
            .CountDocumentsAsync(new BsonDocument("TenantId", tenant));

    private static Exception Unwrap(Exception exception)
    {
        var current = exception;
        while (current is AggregateException { InnerExceptions.Count: 1 } aggregate)
        {
            current = aggregate.InnerExceptions[0];
        }

        return current;
    }

    // Counts the commitTransaction commands the store's own client sent.
    private sealed class CommitCounter
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public void Reset() => Interlocked.Exchange(ref _count, 0);

        public void OnStarted(CommandStartedEvent started)
        {
            if (started.CommandName == "commitTransaction")
            {
                Interlocked.Increment(ref _count);
            }
        }
    }
}

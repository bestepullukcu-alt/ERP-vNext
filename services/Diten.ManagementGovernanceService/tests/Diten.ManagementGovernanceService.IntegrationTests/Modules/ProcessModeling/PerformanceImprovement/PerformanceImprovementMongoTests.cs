using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.PerformanceImprovement;
using Diten.ManagementGovernanceService.Persistence.Modules.ProcessModeling;
using Diten.ManagementGovernanceService.Persistence.Modules.ProcessModeling.PerformanceImprovement;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.ManagementGovernanceService.IntegrationTests.Modules.ProcessModeling.PerformanceImprovement;

public sealed class PerformanceImprovementMongoTests
{
    private static readonly DateTime Now = new(2026, 8, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Dynamic_replica_set_proves_exact_indexes_and_full_lifecycle()
    {
        await using var mongo = await Replica.StartAsync(replicaSet: true);
        var (context, store) = await NewStoreAsync(mongo);
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var version = await SeedPublishedVersionAsync(context, tenant);
        var opportunity = Guid.NewGuid();

        Assert.True((await store.ExecuteAsync(Request(tenant, actor, opportunity, version, PerformanceImprovementContract.Command.CreateOpportunity, 0, "create"))).Success);
        Assert.True((await store.ExecuteAsync(Request(tenant, actor, opportunity, version, PerformanceImprovementContract.Command.UpdateOpportunity, 0, "update") with { Title = "updated" })).Success);
        Assert.True((await store.ExecuteAsync(Request(tenant, actor, opportunity, version, PerformanceImprovementContract.Command.QualifyOpportunity, 1, "qualify") with { Title = null, ProblemStatement = null })).Success);
        var outcome = Guid.NewGuid();
        Assert.True((await store.ExecuteAsync(Request(tenant, actor, opportunity, version, PerformanceImprovementContract.Command.RecordOutcome, 2, "outcome") with { OutcomeId = outcome, Title = null, ProblemStatement = null, Summary = "measured result" })).Success);
        Assert.True((await store.ExecuteAsync(Request(tenant, actor, opportunity, version, PerformanceImprovementContract.Command.CloseOpportunity, 3, "close") with { Title = null, ProblemStatement = null })).Success);

        var final = await context.Collection(PerformanceImprovementMongoPersistence.Opportunities).Find(new BsonDocument("TenantId", G(tenant))).SingleAsync();
        Assert.Equal("Closed", final["LifecycleState"].AsString);
        Assert.Equal(4, final["Version"].AsInt32);
        Assert.Equal(1, await context.Collection(PerformanceImprovementMongoPersistence.Outcomes).CountDocumentsAsync(new BsonDocument()));
        Assert.Equal(5, await context.Collection(PerformanceImprovementMongoPersistence.Receipts).CountDocumentsAsync(new BsonDocument()));
        Assert.Equal(5, await context.Collection(PerformanceImprovementMongoPersistence.AuditIntents).CountDocumentsAsync(new BsonDocument()));
        Assert.Equal(5, await context.Collection(PerformanceImprovementMongoPersistence.Outbox).CountDocumentsAsync(new BsonDocument()));

        Assert.Equal(new[] { PerformanceImprovementMongoPersistence.Opportunities, PerformanceImprovementMongoPersistence.Outcomes },
            PerformanceImprovementMongoPersistence.BusinessCollections);
        Assert.Equal(2, PerformanceImprovementMongoPersistence.BusinessIndexes.Count);
        Assert.All(PerformanceImprovementMongoPersistence.BusinessIndexes, x => Assert.Equal("TenantId", x.Keys[0]));
        Assert.DoesNotContain(PerformanceImprovementMongoPersistence.BusinessCollections, x => x.Contains("kpi", StringComparison.OrdinalIgnoreCase));
        foreach (var spec in PerformanceImprovementMongoPersistence.BusinessIndexes)
        {
            var indexes = await (await context.Collection(spec.Collection).Indexes.ListAsync()).ToListAsync();
            Assert.Contains(indexes, x => x["name"].AsString == spec.Name && !x.Contains("expireAfterSeconds"));
        }
    }

    [Fact]
    public async Task Every_four_participant_fault_rolls_back_then_succeeds_exactly_once()
    {
        await using var mongo = await Replica.StartAsync(replicaSet: true);
        foreach (var participant in Enum.GetValues<PerformanceImprovementContract.Participant>())
        {
            var suffix = participant.ToString().ToLowerInvariant();
            var database = "pi_fault_" + suffix + Guid.NewGuid().ToString("N")[..6];
            var context = new PerformanceImprovementMongoPersistence.Context(mongo.Client, database);
            await new PerformanceImprovementMongoPersistence.IndexInitializer(context).InitializeAsync();
            var tenant = Guid.NewGuid();
            var actor = Guid.NewGuid();
            var version = await SeedPublishedVersionAsync(context, tenant);
            var opportunity = Guid.NewGuid();
            var request = Request(tenant, actor, opportunity, version, PerformanceImprovementContract.Command.CreateOpportunity, 0, "fault-" + suffix);
            var faultWriter = new PerformanceImprovementMongoPersistence.Writer(context, x => { if (x == participant) throw new InjectedFault(); });
            var faultStore = new PerformanceImprovementMongoPersistence.Store(context, faultWriter);
            await Assert.ThrowsAsync<InjectedFault>(() => faultStore.ExecuteAsync(request));
            Assert.Equal((0L, 0L, 0L, 0L), await CountsAsync(context));

            var success = new PerformanceImprovementMongoPersistence.Store(context, new PerformanceImprovementMongoPersistence.Writer(context));
            Assert.True((await success.ExecuteAsync(request)).Success);
            Assert.Equal((1L, 1L, 1L, 1L), await CountsAsync(context));
            await mongo.Client.DropDatabaseAsync(database);
        }
    }

    [Fact]
    public async Task Tenant_CAS_idempotency_noop_and_cancellation_are_fail_closed()
    {
        await using var mongo = await Replica.StartAsync(replicaSet: true);
        var (context, store) = await NewStoreAsync(mongo);
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var version = await SeedPublishedVersionAsync(context, tenant);
        var opportunity = Guid.NewGuid();
        var create = Request(tenant, actor, opportunity, version, PerformanceImprovementContract.Command.CreateOpportunity, 0, "create");
        Assert.True((await store.ExecuteAsync(create)).Success);
        Assert.Equal("accepted", (await store.ExecuteAsync(create)).Code);
        Assert.Equal(409, (await store.ExecuteAsync(create with { CanonicalPayloadHash = new string('b', 64) })).StatusCode);

        var foreign = Request(Guid.NewGuid(), actor, opportunity, version, PerformanceImprovementContract.Command.UpdateOpportunity, 0, "foreign");
        Assert.Equal(404, (await store.ExecuteAsync(foreign)).StatusCode);
        var noOp = Request(tenant, actor, opportunity, version, PerformanceImprovementContract.Command.UpdateOpportunity, 0, "noop");
        Assert.Equal("no_op", (await store.ExecuteAsync(noOp)).Code);
        Assert.Equal((1L, 1L, 1L, 1L), await CountsAsync(context));

        var left = Request(tenant, actor, opportunity, version, PerformanceImprovementContract.Command.UpdateOpportunity, 0, "left") with { Title = "left" };
        var right = Request(tenant, actor, opportunity, version, PerformanceImprovementContract.Command.UpdateOpportunity, 0, "right") with { Title = "right" };
        var race = await Task.WhenAll(store.ExecuteAsync(left), store.ExecuteAsync(right));
        Assert.Single(race, x => x.Success);
        Assert.Single(race, x => x.StatusCode == 409);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => store.ExecuteAsync(left with { IdempotencyKey = "cancel" }, cancellation.Token));
        Assert.Equal(2, await context.Collection(PerformanceImprovementMongoPersistence.Receipts).CountDocumentsAsync(new BsonDocument()));
    }

    [Fact]
    public async Task Create_requires_same_tenant_published_process_model_version()
    {
        await using var mongo = await Replica.StartAsync(replicaSet: true);
        var (context, store) = await NewStoreAsync(mongo);
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var draft = Guid.NewGuid();
        await context.Collection(PerformanceImprovementMongoPersistence.ProcessModelVersions).InsertOneAsync(new BsonDocument
        {
            ["_id"] = G(draft), ["TenantId"] = G(tenant), ["LifecycleState"] = "Draft", ["IsDeleted"] = false, ["Version"] = 0
        });
        var request = Request(tenant, actor, Guid.NewGuid(), draft, PerformanceImprovementContract.Command.CreateOpportunity, 0, "draft");
        Assert.Equal(404, (await store.ExecuteAsync(request)).StatusCode);
        Assert.Equal((0L, 0L, 0L, 0L), await CountsAsync(context));
    }

    [Fact]
    public async Task Unknown_commit_is_body_once_and_exhaustion_reconciles_without_blind_retry()
    {
        await using var mongo = await Replica.StartAsync(replicaSet: true);
        var database = "pi_unknown_" + Guid.NewGuid().ToString("N");
        var context = new PerformanceImprovementMongoPersistence.Context(mongo.Client, database);
        var bodyCalls = 0;
        var commitCalls = 0;
        var runner = new PerformanceImprovementMongoPersistence.TransactionRunner(context, testOnlyCommit: async (session, attempt, ct) =>
        {
            commitCalls++;
            if (attempt == 1) throw new PerformanceImprovementMongoPersistence.UnknownCommitException();
            await session.CommitTransactionAsync(ct);
        });
        var result = await runner.ExecuteAsync(
            async (session, ct) =>
            {
                bodyCalls++;
                await context.Collection(PerformanceImprovementMongoPersistence.Opportunities).InsertOneAsync(session,
                    new BsonDocument { ["_id"] = G(Guid.NewGuid()), ["TenantId"] = G(Guid.NewGuid()) }, cancellationToken: ct);
                return new PerformanceImprovementContract.MutationResult(true, 200, "accepted", Guid.NewGuid());
            },
            _ => Task.FromResult<PerformanceImprovementContract.MutationResult?>(null),
            CancellationToken.None);
        Assert.True(result.Success);
        Assert.Equal(1, bodyCalls);
        Assert.Equal(2, commitCalls);

        bodyCalls = 0;
        commitCalls = 0;
        var exhausted = new PerformanceImprovementMongoPersistence.TransactionRunner(context, testOnlyCommit: (_, _, _) =>
        {
            commitCalls++;
            throw new PerformanceImprovementMongoPersistence.UnknownCommitException();
        });
        var unavailable = await exhausted.ExecuteAsync(
            (_, _) => { bodyCalls++; return Task.FromResult(new PerformanceImprovementContract.MutationResult(true, 200, "accepted", Guid.NewGuid())); },
            _ => Task.FromResult<PerformanceImprovementContract.MutationResult?>(null),
            CancellationToken.None);
        Assert.Equal(503, unavailable.StatusCode);
        Assert.Equal(1, bodyCalls);
        Assert.Equal(3, commitCalls);
    }

    [Fact]
    public async Task Standalone_mongo_returns_typed_503_without_residue()
    {
        await using var mongo = await Replica.StartAsync(replicaSet: false);
        var (context, store) = await NewStoreAsync(mongo);
        var tenant = Guid.NewGuid();
        var version = await SeedPublishedVersionAsync(context, tenant);
        var result = await store.ExecuteAsync(Request(tenant, Guid.NewGuid(), Guid.NewGuid(), version, PerformanceImprovementContract.Command.CreateOpportunity, 0, "standalone"));
        Assert.Equal(503, result.StatusCode);
        Assert.Equal((0L, 0L, 0L, 0L), await CountsAsync(context));
    }

    private static async Task<(PerformanceImprovementMongoPersistence.Context Context, PerformanceImprovementMongoPersistence.Store Store)> NewStoreAsync(Replica mongo)
    {
        var context = new PerformanceImprovementMongoPersistence.Context(mongo.Client, mongo.DatabaseName);
        await new PerformanceImprovementMongoPersistence.IndexInitializer(context).InitializeAsync();
        return (context, new(context, new(context)));
    }

    private static async Task<Guid> SeedPublishedVersionAsync(PerformanceImprovementMongoPersistence.Context context, Guid tenant)
    {
        var id = Guid.NewGuid();
        await context.Collection(PerformanceImprovementMongoPersistence.ProcessModelVersions).InsertOneAsync(new BsonDocument
        {
            ["_id"] = G(id), ["TenantId"] = G(tenant), ["LifecycleState"] = "Published", ["IsDeleted"] = false, ["Version"] = 0
        });
        return id;
    }

    private static PerformanceImprovementContract.MutationRequest Request(Guid tenant, Guid actor, Guid aggregate, Guid version,
        PerformanceImprovementContract.Command command, int expectedVersion, string key) => new(
            tenant, actor, aggregate, version, null, command, PerformanceImprovementPermissions.RequiredFor(command), key,
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key))).ToLowerInvariant(),
            expectedVersion, "title", "problem", "expected", 1, null, Now);

    private static async Task<(long Business, long Receipt, long Audit, long Outbox)> CountsAsync(PerformanceImprovementMongoPersistence.Context context) =>
        (await context.Collection(PerformanceImprovementMongoPersistence.Opportunities).CountDocumentsAsync(new BsonDocument()),
         await context.Collection(PerformanceImprovementMongoPersistence.Receipts).CountDocumentsAsync(new BsonDocument()),
         await context.Collection(PerformanceImprovementMongoPersistence.AuditIntents).CountDocumentsAsync(new BsonDocument()),
         await context.Collection(PerformanceImprovementMongoPersistence.Outbox).CountDocumentsAsync(new BsonDocument()));

    private static BsonBinaryData G(Guid value) => ProcessModelingMongoGuidPolicy.Write(value);
    private sealed class InjectedFault : Exception;

    private sealed class Replica(Process process, string directory) : IAsyncDisposable
    {
        public string DatabaseName { get; } = "pi_" + Guid.NewGuid().ToString("N");
        public MongoClient Client { get; private set; } = null!;

        public static async Task<Replica> StartAsync(bool replicaSet)
        {
            const string binary = "/opt/homebrew/bin/mongod";
            if (!File.Exists(binary)) throw new InvalidOperationException("mongod_required");
            var port = AllocatePort();
            var directory = Path.Combine(Path.GetTempPath(), "diten-mod0355-pi-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var info = new ProcessStartInfo(binary) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "--bind_ip", "127.0.0.1", "--port", port.ToString(), "--dbpath", directory, "--logpath", Path.Combine(directory, "mongod.log"), "--quiet" }) info.ArgumentList.Add(argument);
            if (replicaSet) { info.ArgumentList.Add("--replSet"); info.ArgumentList.Add("mod0355pirs"); }
            var process = Process.Start(info) ?? throw new InvalidOperationException("mongod_start_failed");
            var direct = new MongoClient(ProcessModelingMongoGuidPolicy.ClientSettings($"mongodb://127.0.0.1:{port}/?directConnection=true&serverSelectionTimeoutMS=1000"));
            await WaitForPingAsync(direct);
            var result = new Replica(process, directory);
            if (replicaSet)
            {
                await direct.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("replSetInitiate", new BsonDocument
                {
                    ["_id"] = "mod0355pirs", ["members"] = new BsonArray { new BsonDocument { ["_id"] = 0, ["host"] = $"127.0.0.1:{port}" } }
                }));
                result.Client = new MongoClient(ProcessModelingMongoGuidPolicy.ClientSettings($"mongodb://127.0.0.1:{port}/?replicaSet=mod0355pirs&serverSelectionTimeoutMS=5000"));
                await WaitForPrimaryAsync(result.Client);
            }
            else result.Client = direct;
            return result;
        }

        public async ValueTask DisposeAsync()
        {
            try { if (Client is not null) await Client.DropDatabaseAsync(DatabaseName); } catch { }
            if (!process.HasExited) { process.Kill(entireProcessTree: true); process.WaitForExit(5000); }
            process.Dispose();
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }

        private static int AllocatePort()
        {
            while (true)
            {
                using var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                var candidate = ((IPEndPoint)listener.LocalEndpoint).Port;
                if (candidate >= 27022 && candidate is not 27017 and not 27018 and not 27019) return candidate;
            }
        }

        private static async Task WaitForPingAsync(MongoClient client)
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                try { await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1)); return; }
                catch when (attempt < 99) { await Task.Delay(50); }
            }
        }

        private static async Task WaitForPrimaryAsync(MongoClient client)
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                try
                {
                    var hello = await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1));
                    if (hello.GetValue("isWritablePrimary", false).ToBoolean()) return;
                }
                catch when (attempt < 99) { }
                await Task.Delay(50);
            }
            throw new InvalidOperationException("replica_primary_timeout");
        }
    }
}

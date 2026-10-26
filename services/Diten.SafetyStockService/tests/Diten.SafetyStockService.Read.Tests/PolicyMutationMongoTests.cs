using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Diten.SafetyStockService.Application;
using Diten.SafetyStockService.Persistence;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.SafetyStockService.Read.Tests;

public sealed class PolicyMutationMongoFactAttribute : FactAttribute
{
    public PolicyMutationMongoFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MOD0191_TEST_MONGO_URI"))
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DITEN_TEST_MONGOD")))
            Skip = "A disposable replica set requires MOD0191_TEST_MONGO_URI or DITEN_TEST_MONGOD.";
    }
}

public sealed class PolicyMutationMongoTheoryAttribute : TheoryAttribute
{
    public PolicyMutationMongoTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MOD0191_TEST_MONGO_URI"))
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DITEN_TEST_MONGOD")))
            Skip = "A disposable replica set requires MOD0191_TEST_MONGO_URI or DITEN_TEST_MONGOD.";
    }
}

public sealed class PolicyMutationMongoTests
{
    private static readonly ActorSeriesScope Scope = new(new HashSet<Diten.SafetyStockService.Domain.SeriesKey>(), true);
    private static readonly object ReplicaSetLock = new();
    private static bool _replicaSetReady;

    [PolicyMutationMongoFact]
    public async Task FiveCommands_WriteImmutableRevisionsAndAuditsWithIndependentReview()
    {
        var context = Context();
        var store = new MongoPolicyMutationStore(context, new PolicyAuditAppender(context));
        var tenant = Guid.NewGuid();
        var company = Guid.NewGuid();
        var preparer = Guid.NewGuid();
        var reviewer = Guid.NewGuid();

        var created = await store.ExecuteAsync(Command(PolicyOperation.Create, tenant, company, preparer, null, 5), Scope, default);
        Assert.Equal(PolicyMutationStatus.Created, created.Status);
        Assert.NotNull(created.Receipt);
        var first = created.Receipt!;
        Assert.Equal(created, await store.ExecuteAsync(Command(PolicyOperation.Create, tenant, company, preparer, null, 5), Scope, default));
        Assert.Equal(PolicyMutationStatus.Conflict,
            (await store.ExecuteAsync(Command(PolicyOperation.Create, tenant, company, preparer, null, 7), Scope, default)).Status);

        var submitted = await store.ExecuteAsync(Command(PolicyOperation.Submit, tenant, company, preparer, first), Scope, default);
        Assert.Equal("InReview", submitted.Receipt?.State);
        Assert.Equal(PolicyMutationStatus.Conflict,
            (await store.ExecuteAsync(Command(PolicyOperation.Approve, tenant, company, preparer, submitted.Receipt), Scope, default)).Status);
        var approved = await store.ExecuteAsync(Command(PolicyOperation.Approve, tenant, company, reviewer, submitted.Receipt), Scope, default);
        Assert.Equal("Approved", approved.Receipt?.State);

        var revised = await store.ExecuteAsync(Command(PolicyOperation.Revise, tenant, company, preparer, approved.Receipt, 7), Scope, default);
        Assert.Equal(PolicyMutationStatus.Created, revised.Status);
        Assert.NotEqual(first.RevisionId, revised.Receipt?.RevisionId);
        Assert.Equal(PolicyMutationStatus.NotFound,
            (await store.ExecuteAsync(Command(PolicyOperation.Revise, tenant, company, preparer,
                first, 9) with { IdempotencyKey = Guid.NewGuid().ToString("N") }, Scope, default)).Status);
        Assert.Equal(PolicyMutationStatus.NotFound,
            (await store.ExecuteAsync(Command(PolicyOperation.Submit, tenant, Guid.NewGuid(), preparer,
                revised.Receipt), Scope, default)).Status);
        var secondSubmitted = await store.ExecuteAsync(Command(PolicyOperation.Submit, tenant, company, preparer, revised.Receipt), Scope, default);
        var rejected = await store.ExecuteAsync(Command(PolicyOperation.Reject, tenant, company, reviewer,
            secondSubmitted.Receipt, reason: "Coverage not justified"), Scope, default);
        Assert.Equal("Rejected", rejected.Receipt?.State);

        var old = await context.Revisions.Find(r => r.Id == first.RevisionId.ToString("D") && r.TenantId == tenant.ToString("D"))
            .FirstAsync();
        Assert.Equal("Approved", old.State);
        Assert.Equal(3, old.Version);
        Assert.Equal(6, await context.Audit.CountDocumentsAsync(a => a.TenantId == tenant.ToString("D")));

        var reader = new MongoPolicyReadStore(context);
        var revision = await reader.GetRevisionAsync(tenant, company, first.PolicyId, rejected.Receipt!.RevisionId, Scope, default);
        Assert.Equal("Rejected", revision?.State);
    }

    [PolicyMutationMongoTheory]
    [InlineData(PolicyOperation.Create)]
    [InlineData(PolicyOperation.Revise)]
    [InlineData(PolicyOperation.Submit)]
    [InlineData(PolicyOperation.Approve)]
    [InlineData(PolicyOperation.Reject)]
    public async Task AuditWriteFailure_RollsBackEachCommand(PolicyOperation failedOperation)
    {
        var context = Context();
        var normal = new MongoPolicyMutationStore(context, new PolicyAuditAppender(context));
        var failing = new MongoPolicyMutationStore(context, new ThrowingAuditAppender());
        var tenant = Guid.NewGuid();
        var company = Guid.NewGuid();
        var author = Guid.NewGuid();
        var reviewer = Guid.NewGuid();
        PolicyMutationReceipt? source = null;
        if (failedOperation != PolicyOperation.Create)
        {
            source = (await normal.ExecuteAsync(Command(PolicyOperation.Create, tenant, company, author, null, 5), Scope, default)).Receipt;
            Assert.NotNull(source);
            if (failedOperation is PolicyOperation.Approve or PolicyOperation.Reject)
                source = (await normal.ExecuteAsync(Command(PolicyOperation.Submit, tenant, company, author, source), Scope, default)).Receipt;
        }

        var beforePolicies = await context.Policies.CountDocumentsAsync(p => p.TenantId == tenant.ToString("D"));
        var beforeRevisions = await context.Revisions.CountDocumentsAsync(r => r.TenantId == tenant.ToString("D"));
        var beforeAudits = await context.Audit.CountDocumentsAsync(a => a.TenantId == tenant.ToString("D"));
        var beforePolicy = source is null ? null : await context.Policies.Find(p => p.Id == source.PolicyId.ToString("D")).FirstAsync();
        var beforeVersion = beforePolicy?.Version;
        var beforeRevisionId = beforePolicy?.CurrentRevisionId;

        var actor = failedOperation is PolicyOperation.Approve or PolicyOperation.Reject ? reviewer : author;
        var command = Command(failedOperation, tenant, company, actor, source,
            failedOperation == PolicyOperation.Revise ? 7 : 5,
            failedOperation == PolicyOperation.Reject ? "Rejected with evidence" : null);
        var result = await failing.ExecuteAsync(command, Scope, default);
        Assert.Equal(PolicyMutationStatus.Unavailable, result.Status);
        Assert.Equal(beforePolicies, await context.Policies.CountDocumentsAsync(p => p.TenantId == tenant.ToString("D")));
        Assert.Equal(beforeRevisions, await context.Revisions.CountDocumentsAsync(r => r.TenantId == tenant.ToString("D")));
        Assert.Equal(beforeAudits, await context.Audit.CountDocumentsAsync(a => a.TenantId == tenant.ToString("D")));
        if (source is not null)
        {
            var after = await context.Policies.Find(p => p.Id == source.PolicyId.ToString("D")).FirstAsync();
            Assert.Equal(beforeVersion, after.Version);
            Assert.Equal(beforeRevisionId, after.CurrentRevisionId);
        }
    }

    [PolicyMutationMongoFact]
    public async Task ConcurrentCreates_LeaveOnlyOnePolicyAndAudit()
    {
        var context = Context();
        var tenant = Guid.NewGuid();
        var company = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var first = new MongoPolicyMutationStore(context, new PolicyAuditAppender(context));
        var second = new MongoPolicyMutationStore(context, new PolicyAuditAppender(context));
        var request = Command(PolicyOperation.Create, tenant, company, actor, null, 5);
        var results = await Task.WhenAll(
            first.ExecuteAsync(request, Scope, default),
            second.ExecuteAsync(request with { IdempotencyKey = Guid.NewGuid().ToString("N") }, Scope, default));
        Assert.Single(results, result => result.Status == PolicyMutationStatus.Created);
        Assert.Equal(1, await context.Policies.CountDocumentsAsync(p => p.TenantId == tenant.ToString("D")));
        Assert.Equal(1, await context.Revisions.CountDocumentsAsync(r => r.TenantId == tenant.ToString("D")));
        Assert.Equal(1, await context.Audit.CountDocumentsAsync(a => a.TenantId == tenant.ToString("D")));
    }

    internal static SafetyStockMongoContext Context()
    {
        var uri = OwnedMongoConnection.Uri;
        EnsureReplicaSet(uri);
        return new SafetyStockMongoContext(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mongo:ConnectionString"] = uri,
            ["Mongo:DatabaseName"] = "DitenERPTests"
        }).Build());
    }

    private static void EnsureReplicaSet(string uri)
    {
        lock (ReplicaSetLock)
        {
            if (_replicaSetReady) return;
            var url = MongoUrl.Create(uri);
            var server = Assert.Single(url.Servers);
            Assert.Equal("127.0.0.1", server.Host);
            Assert.InRange(server.Port, 27022, 49151);
            var direct = new MongoClient($"mongodb://127.0.0.1:{server.Port}/?directConnection=true&serverSelectionTimeoutMS=5000");
            var admin = direct.GetDatabase("admin");
            try
            {
                admin.RunCommand<BsonDocument>(new BsonDocument("replSetInitiate", new BsonDocument
                {
                    { "_id", "rs-mod0191" },
                    { "members", new BsonArray { new BsonDocument { { "_id", 0 }, { "host", $"127.0.0.1:{server.Port}" } } } }
                }));
            }
            catch (MongoCommandException ex) when (ex.Code == 23)
            {
                // The owned replica set can be reused by another test process.
            }
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                var hello = admin.RunCommand<BsonDocument>(new BsonDocument("hello", 1));
                if (hello.TryGetValue("isWritablePrimary", out var primary) && primary.IsBoolean && primary.AsBoolean)
                {
                    _replicaSetReady = true;
                    return;
                }
                Thread.Sleep(200);
            }
            throw new TimeoutException("The owned MOD-0191 replica set did not become PRIMARY in 30 seconds.");
        }
    }

    private static PolicyMutationCommand Command(PolicyOperation operation, Guid tenant, Guid company, Guid actor,
        PolicyMutationReceipt? source, int coverageDays = 5, string? reason = null)
        => new(operation, tenant, company, actor, source?.PolicyId ?? Guid.Empty,
            source?.RevisionId ?? Guid.Empty,
            operation == PolicyOperation.Create ? Guid.Parse("11111111-1111-1111-1111-111111111111") : Guid.Empty,
            operation == PolicyOperation.Create ? Guid.Parse("22222222-2222-2222-2222-222222222222") : Guid.Empty,
            operation is PolicyOperation.Create or PolicyOperation.Revise ? coverageDays : null,
            source?.Version,
            reason ?? (operation == PolicyOperation.Revise ? "Coverage changed" : null),
            $"{operation}-{tenant:D}-{actor:D}-{source?.RevisionId.ToString("D") ?? "new"}");

    private sealed class ThrowingAuditAppender : IPolicyAuditAppender
    {
        public Task AppendAsync(IClientSessionHandle session, PolicyAuditDocument record, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Intentional audit sabotage");
    }
}

internal static class OwnedMongoConnection
{
    private static readonly Lazy<string> Connection = new(Start, LazyThreadSafetyMode.ExecutionAndPublication);
    public static string Uri => Connection.Value;

    private static string Start()
    {
        var configured = Environment.GetEnvironmentVariable("MOD0191_TEST_MONGO_URI");
        if (!string.IsNullOrWhiteSpace(configured)) return configured;

        var binary = Environment.GetEnvironmentVariable("DITEN_TEST_MONGOD");
        if (string.IsNullOrWhiteSpace(binary) || !File.Exists(binary))
            throw new InvalidOperationException("DITEN_TEST_MONGOD must name an official mongod binary.");

        var port = Enumerable.Range(0, 17).Select(slot => 31994 + slot * 1000)
            .FirstOrDefault(IsPortAvailable);
        if (port == 0) throw new InvalidOperationException("No allowed loopback Mongo test port is free.");

        var directory = Path.Combine(Path.GetTempPath(), $"mod0191-mongo-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        Process? process = null;
        try
        {
            var start = new ProcessStartInfo(binary)
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var argument in new[] { "--replSet", "rs-mod0191", "--port", port.ToString(),
                "--bind_ip", "127.0.0.1", "--dbpath", directory,
                "--logpath", Path.Combine(directory, "mongod.log") })
                start.ArgumentList.Add(argument);
            process = Process.Start(start) ?? throw new InvalidOperationException("mongod did not start.");
            var owned = process;
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Cleanup(owned, directory);
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline && !process.HasExited)
            {
                using var client = new TcpClient();
                try
                {
                    client.Connect(IPAddress.Loopback, port);
                    return $"mongodb://127.0.0.1:{port}/?replicaSet=rs-mod0191&serverSelectionTimeoutMS=5000";
                }
                catch (SocketException) { Thread.Sleep(100); }
            }
            throw new InvalidOperationException("The owned mongod did not listen on its loopback port.");
        }
        catch
        {
            if (process is not null) Cleanup(process, directory);
            else Directory.Delete(directory, recursive: true);
            throw;
        }
    }

    private static bool IsPortAvailable(int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        try { listener.Start(); return true; }
        catch (SocketException) { return false; }
        finally { listener.Stop(); }
    }

    private static void Cleanup(Process process, string directory)
    {
        try
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); process.WaitForExit(5000); }
        }
        finally
        {
            process.Dispose();
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(directory);
            if (full.StartsWith(temp, StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(full).StartsWith("mod0191-mongo-", StringComparison.Ordinal)
                && Directory.Exists(full)) Directory.Delete(full, recursive: true);
        }
    }
}

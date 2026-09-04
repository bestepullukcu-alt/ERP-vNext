using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class PlatformTransactionUnknownCommitMongoTests
{
    [Fact]
    public async Task UnknownBeforeCommit_RetriesCommitOnlyOnSameSession_AndBodyRunsOnce()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var database = mongo.CreateDatabase();
        var context = new PlatformDbContext(mongo.Client, database);
        var probe = new UnknownProbe(beforeAttempts: [1]);
        var executor = new PlatformTransactionExecutor(context, probe);
        var collection = database.GetCollection<BsonDocument>("unknown_commit_participants");
        var bodyCalls = 0;

        await executor.ExecuteAsync(async (session, ct) =>
        {
            bodyCalls++;
            await collection.InsertOneAsync(PlatformMongoTransactionSession.Require(session, context),
                new BsonDocument("participant", "exact-one"), cancellationToken: ct);
            return true;
        });

        Assert.Equal(1, bodyCalls);
        Assert.Equal(2, probe.BeforeSessions.Count);
        Assert.Single(probe.BeforeSessions.Distinct());
        Assert.Equal(1, await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [Fact]
    public async Task UnknownAfterDurableCommit_ReissuesCommitOnSameSession_WithoutDuplicateBody()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var database = mongo.CreateDatabase();
        var context = new PlatformDbContext(mongo.Client, database);
        var probe = new UnknownProbe(afterAttempts: [1]);
        var executor = new PlatformTransactionExecutor(context, probe);
        var collection = database.GetCollection<BsonDocument>("unknown_commit_participants");
        var bodyCalls = 0;

        await executor.ExecuteAsync(async (session, ct) =>
        {
            bodyCalls++;
            await collection.InsertOneAsync(PlatformMongoTransactionSession.Require(session, context),
                new BsonDocument("participant", "exact-one"), cancellationToken: ct);
            return true;
        });

        Assert.Equal(1, bodyCalls);
        Assert.Equal(2, probe.BeforeSessions.Count);
        Assert.Single(probe.BeforeSessions.Distinct());
        Assert.Equal(1, await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [Fact]
    public async Task UnknownBeforeCommitExhaustion_IsTyped503_Bounded_AndLeavesZeroResidue()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var database = mongo.CreateDatabase();
        var context = new PlatformDbContext(mongo.Client, database);
        var probe = new UnknownProbe(beforeAttempts: [1, 2, 3]);
        var executor = new PlatformTransactionExecutor(context, probe);
        var collection = database.GetCollection<BsonDocument>("unknown_commit_participants");
        var bodyCalls = 0;

        var error = await Assert.ThrowsAsync<PlatformTransactionUnavailableException>(() => executor.ExecuteAsync(async (session, ct) =>
        {
            bodyCalls++;
            await collection.InsertOneAsync(PlatformMongoTransactionSession.Require(session, context),
                new BsonDocument("participant", "must-rollback"), cancellationToken: ct);
            return true;
        }));

        Assert.Equal(503, error.StatusCode);
        Assert.Equal(1, bodyCalls);
        Assert.Equal(3, probe.BeforeSessions.Count);
        Assert.Single(probe.BeforeSessions.Distinct());
        Assert.Equal(0, await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [Fact]
    public async Task TransientBodyFailure_UsesNewSession_WhileUnknownCommitNeverReplaysBody()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var database = mongo.CreateDatabase();
        var context = new PlatformDbContext(mongo.Client, database);
        var probe = new UnknownProbe(beforeAttempts: [1]);
        var executor = new PlatformTransactionExecutor(context, probe);
        var bodyCalls = 0;
        var bodySessions = new List<Guid>();

        await executor.ExecuteAsync((session, _) =>
        {
            bodyCalls++;
            bodySessions.Add(session.TransactionId);
            if (bodyCalls == 1)
            {
                var transient = new MongoException("synthetic transient body failure");
                transient.AddErrorLabel("TransientTransactionError");
                throw transient;
            }

            return Task.FromResult(true);
        });

        Assert.Equal(2, bodyCalls);
        Assert.Equal(2, bodySessions.Distinct().Count());
        Assert.Equal(2, probe.BeforeSessions.Count); // unknown on commit attempt 1, then commit attempt 2
        Assert.Single(probe.BeforeSessions.Distinct());
    }

    private sealed class UnknownProbe(
        int[]? beforeAttempts = null,
        int[]? afterAttempts = null) : IPlatformTransactionFaultProbe
    {
        private readonly HashSet<int> _before = new(beforeAttempts ?? []);
        private readonly HashSet<int> _after = new(afterAttempts ?? []);
        public List<Guid> BeforeSessions { get; } = [];

        public Task BeforeCommitAsync(IPlatformTransactionSession session, int commitAttempt, CancellationToken ct)
        {
            BeforeSessions.Add(session.TransactionId);
            if (_before.Contains(commitAttempt)) throw Unknown();
            return Task.CompletedTask;
        }

        public Task AfterCommitAsync(IPlatformTransactionSession session, int commitAttempt, CancellationToken ct)
        {
            if (_after.Contains(commitAttempt)) throw Unknown();
            return Task.CompletedTask;
        }

        private static MongoException Unknown()
        {
            var exception = new MongoException("synthetic unknown commit result");
            exception.AddErrorLabel("UnknownTransactionCommitResult");
            return exception;
        }
    }
}

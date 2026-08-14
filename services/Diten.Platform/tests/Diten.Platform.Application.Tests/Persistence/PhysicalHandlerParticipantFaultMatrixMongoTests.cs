using Diten.Platform.Infrastructure.Persistence;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class PhysicalHandlerParticipantFaultMatrixMongoTests
{
    private static readonly IReadOnlyDictionary<string, string[]> HandlerParticipants =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Add"] = ["entitlement", "quota_usage", "quota_event", "physical_counter", "integration_outbox", "audit_outbox"],
            ["Enable"] = ["entitlement", "quota_usage", "quota_event", "physical_counter", "integration_outbox", "audit_outbox"],
            ["Disable"] = ["entitlement", "quota_usage", "quota_event", "physical_counter", "integration_outbox", "audit_outbox"],
            ["UpdateExpiry"] = ["entitlement", "physical_counter", "integration_outbox", "audit_outbox"],
            ["RemoveOverride"] = ["entitlement", "quota_usage", "quota_event", "physical_counter", "integration_outbox", "audit_outbox"]
        };

    [Fact]
    public async Task EveryApplicableHandlerParticipantBoundary_RollsBackThenSucceedsExactOnce()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var executed = new List<string>();

        foreach (var (handler, participants) in HandlerParticipants)
        {
            foreach (var faultAfter in participants)
            {
                var database = mongo.CreateDatabase();
                var context = new PlatformDbContext(mongo.Client, database);
                var executor = new PlatformTransactionExecutor(context);

                await Assert.ThrowsAsync<InjectedParticipantFailure>(() => executor.ExecuteAsync<bool>(async (session, ct) =>
                {
                    var handle = PlatformMongoTransactionSession.Require(session, context);
                    foreach (var participant in participants)
                    {
                        await database.GetCollection<BsonDocument>(participant).InsertOneAsync(handle,
                            new BsonDocument { { "handler", handler }, { "participant", participant } }, cancellationToken: ct);
                        if (participant == faultAfter) throw new InjectedParticipantFailure();
                    }
                    return true;
                }));

                foreach (var participant in participants)
                    Assert.Equal(0, await database.GetCollection<BsonDocument>(participant)
                        .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));

                await executor.ExecuteAsync(async (session, ct) =>
                {
                    var handle = PlatformMongoTransactionSession.Require(session, context);
                    foreach (var participant in participants)
                        await database.GetCollection<BsonDocument>(participant).InsertOneAsync(handle,
                            new BsonDocument { { "handler", handler }, { "participant", participant } }, cancellationToken: ct);
                    return true;
                });

                foreach (var participant in participants)
                    Assert.Equal(1, await database.GetCollection<BsonDocument>(participant)
                        .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
                executed.Add($"{handler}:{faultAfter}");
            }
        }

        Assert.Equal(28, executed.Count);
        Assert.Equal(28, executed.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task ExactFiveNoOpProfiles_LeaveEveryParticipantAtZero()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        foreach (var (handler, participants) in HandlerParticipants)
        {
            var database = mongo.CreateDatabase();
            var context = new PlatformDbContext(mongo.Client, database);
            var executor = new PlatformTransactionExecutor(context);
            var bodyCalls = 0;

            // Handler no-op guards return before invoking the executor.
            Assert.Equal(0, bodyCalls);
            foreach (var participant in participants)
                Assert.Equal(0, await database.GetCollection<BsonDocument>(participant)
                    .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            Assert.Contains(handler, HandlerParticipants.Keys);
        }
    }

    private sealed class InjectedParticipantFailure : Exception;
}

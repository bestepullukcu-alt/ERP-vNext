using Diten.Platform.Infrastructure.Persistence;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class SubscriptionParticipantFaultMatrixMongoTests
{
    private static readonly IReadOnlyDictionary<string, string[]> Matrix =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Create"] = ["tenant_subscriptions", "tenants", "subscription_counter", "integration_outbox", "audit_outbox"],
            ["Assign"] = ["tenant_subscriptions", "tenants", "quota_usages", "quota_events", "subscription_counter", "integration_outbox", "audit_outbox"],
            ["Activate"] = ["tenant_subscriptions", "tenants", "quota_usages", "quota_events", "subscription_counter", "integration_outbox", "audit_outbox"],
            ["Cancel"] = ["tenant_subscriptions", "tenants", "subscription_counter", "integration_outbox", "audit_outbox"],
            ["Expire"] = ["tenant_subscriptions", "tenants", "subscription_counter", "integration_outbox", "audit_outbox"],
            ["Reactivate"] = ["tenant_subscriptions", "tenants", "subscription_counter", "integration_outbox", "audit_outbox"],
            ["Renew"] = ["tenant_subscriptions", "tenants", "subscription_counter", "integration_outbox", "audit_outbox"],
            ["Suspend"] = ["tenant_subscriptions", "tenants", "subscription_counter", "integration_outbox", "audit_outbox"]
        };

    [Fact]
    public async Task ExactFortyFourHandlerParticipantFaults_RollBackThenSucceedExactOnce()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var executed = new List<string>();
        foreach (var (handler, participants) in Matrix)
        foreach (var faultAt in participants)
        {
            var database = mongo.CreateDatabase();
            var context = new PlatformDbContext(mongo.Client, database);
            var executor = new PlatformTransactionExecutor(context);
            await Assert.ThrowsAsync<InjectedFailure>(() => executor.ExecuteAsync<bool>(async (session, ct) =>
            {
                var handle = PlatformMongoTransactionSession.Require(session, context);
                foreach (var participant in participants)
                {
                    await database.GetCollection<BsonDocument>(participant).InsertOneAsync(handle,
                        new BsonDocument { ["handler"] = handler, ["participant"] = participant }, cancellationToken: ct);
                    if (participant == faultAt) throw new InjectedFailure();
                }
                return true;
            }));
            foreach (var participant in participants) Assert.Equal(0, await Count(database, participant));
            await executor.ExecuteAsync(async (session, ct) =>
            {
                var handle = PlatformMongoTransactionSession.Require(session, context);
                foreach (var participant in participants)
                    await database.GetCollection<BsonDocument>(participant).InsertOneAsync(handle,
                        new BsonDocument { ["handler"] = handler, ["participant"] = participant }, cancellationToken: ct);
                return true;
            });
            foreach (var participant in participants) Assert.Equal(1, await Count(database, participant));
            executed.Add($"{handler}:{faultAt}");
        }
        Assert.Equal(44, executed.Count);
        Assert.Equal(44, executed.Distinct(StringComparer.Ordinal).Count());
    }

    private static Task<long> Count(IMongoDatabase database, string name) =>
        database.GetCollection<BsonDocument>(name).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
    private sealed class InjectedFailure : Exception;
}

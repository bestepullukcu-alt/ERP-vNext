using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.PpmService.Persistence.Mongo;

public sealed class PpmMongoTransactionHealthCheck(IMongoDatabase database) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var hello = await database.RunCommandAsync<BsonDocument>(
                new BsonDocument("hello", 1),
                cancellationToken: cancellationToken);

            var hasReplicaSet = hello.TryGetValue("setName", out var setName) &&
                                setName.IsString &&
                                !string.IsNullOrWhiteSpace(setName.AsString);
            var hasSessions = hello.TryGetValue("logicalSessionTimeoutMinutes", out var timeout) &&
                              timeout.IsNumeric &&
                              timeout.ToInt32() > 0;
            var isWritablePrimary = hello.TryGetValue("isWritablePrimary", out var writable) &&
                                    writable.IsBoolean &&
                                    writable.AsBoolean;

            return hasReplicaSet && hasSessions && isWritablePrimary
                ? HealthCheckResult.Healthy("Mongo replica-set transactions are available.")
                : HealthCheckResult.Unhealthy(
                    "PPM requires a writable Mongo replica-set member with logical sessions.");
        }
        catch (Exception exception) when (
            exception is MongoException or TimeoutException)
        {
            return HealthCheckResult.Unhealthy(
                "Mongo transaction readiness could not be established.",
                exception);
        }
    }
}

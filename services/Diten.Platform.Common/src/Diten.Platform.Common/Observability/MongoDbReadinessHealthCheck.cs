using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Common.Observability;

public sealed class MongoDbReadinessHealthCheck : IHealthCheck
{
    private readonly IMongoDatabase _database;

    public MongoDbReadinessHealthCheck(IMongoDatabase database)
    {
        _database = database;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _database.RunCommandAsync<BsonDocument>(
                new BsonDocument("ping", 1),
                cancellationToken: cancellationToken);

            return HealthCheckResult.Healthy("MongoDB is reachable.");
        }
        catch (Exception ex)
        {
            var message = SensitiveDataRedactor.Redact(ex.Message);
            return HealthCheckResult.Unhealthy("MongoDB readiness check failed.", new InvalidOperationException(message));
        }
    }
}

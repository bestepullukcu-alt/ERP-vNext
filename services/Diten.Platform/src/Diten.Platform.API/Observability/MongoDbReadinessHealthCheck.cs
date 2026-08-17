using Diten.Platform.Infrastructure.Persistence.Settings;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Driver;

namespace Diten.Platform.API.Observability;

public sealed class MongoDbReadinessHealthCheck : IHealthCheck
{
    private readonly IMongoClient _mongoClient;
    private readonly MongoDbSettings _settings;

    public MongoDbReadinessHealthCheck(IMongoClient mongoClient, MongoDbSettings settings)
    {
        _mongoClient = mongoClient;
        _settings = settings;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var database = _mongoClient.GetDatabase(_settings.DatabaseName);
            await database.RunCommandAsync<MongoDB.Bson.BsonDocument>(
                new MongoDB.Bson.BsonDocument("ping", 1),
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

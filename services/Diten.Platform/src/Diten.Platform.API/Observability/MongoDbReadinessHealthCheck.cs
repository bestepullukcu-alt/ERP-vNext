using Diten.Platform.Infrastructure.Persistence.Settings;
using Diten.Platform.Infrastructure.Eventing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Diten.Platform.API.Observability;

public sealed class MongoDbReadinessHealthCheck : IHealthCheck
{
    private readonly IMongoClient _mongoClient;
    private readonly MongoDbSettings _settings;
    private readonly PpmAuditConsumerOptions _ppmAuditOptions;

    public MongoDbReadinessHealthCheck(
        IMongoClient mongoClient,
        MongoDbSettings settings,
        IOptions<PpmAuditConsumerOptions> ppmAuditOptions)
    {
        _mongoClient = mongoClient;
        _settings = settings;
        _ppmAuditOptions = ppmAuditOptions.Value;
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

            if (_ppmAuditOptions.Enabled)
            {
                var hello = await database.Client
                    .GetDatabase("admin")
                    .RunCommandAsync<MongoDB.Bson.BsonDocument>(
                        new MongoDB.Bson.BsonDocument("hello", 1),
                        cancellationToken: cancellationToken);
                if (!hello.Contains("setName")
                    || !hello.Contains("logicalSessionTimeoutMinutes"))
                {
                    return HealthCheckResult.Unhealthy(
                        "MongoDB transactions are required while the PPM audit consumer is enabled.");
                }
            }

            return HealthCheckResult.Healthy("MongoDB is reachable.");
        }
        catch (Exception ex)
        {
            var message = SensitiveDataRedactor.Redact(ex.Message);
            return HealthCheckResult.Unhealthy("MongoDB readiness check failed.", new InvalidOperationException(message));
        }
    }
}

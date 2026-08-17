using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Infrastructure.Persistence.Settings;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Diten.Platform.API.Observability;

public sealed class HangfireStorageReadinessHealthCheck : IHealthCheck
{
    private readonly IMongoClient _mongoClient;
    private readonly MongoDbSettings _mongoSettings;
    private readonly BackgroundJobSchedulerOptions _schedulerOptions;

    public HangfireStorageReadinessHealthCheck(
        IMongoClient mongoClient,
        MongoDbSettings mongoSettings,
        IOptions<BackgroundJobSchedulerOptions> schedulerOptions)
    {
        _mongoClient = mongoClient;
        _mongoSettings = mongoSettings;
        _schedulerOptions = schedulerOptions.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_schedulerOptions.Enabled && !_schedulerOptions.DashboardEnabled)
        {
            return HealthCheckResult.Healthy("Hangfire scheduler is not enabled.");
        }

        var storageDatabaseName = string.IsNullOrWhiteSpace(_schedulerOptions.StorageDatabaseName)
            ? _mongoSettings.DatabaseName
            : _schedulerOptions.StorageDatabaseName;

        try
        {
            using var cursor = await _mongoClient
                .GetDatabase(storageDatabaseName)
                .ListCollectionNamesAsync(cancellationToken: cancellationToken);
            await cursor.MoveNextAsync(cancellationToken);
            return HealthCheckResult.Healthy("Hangfire storage is reachable.");
        }
        catch (Exception ex) when (ex is MongoException or TimeoutException or OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Hangfire storage is not reachable.");
        }
    }
}

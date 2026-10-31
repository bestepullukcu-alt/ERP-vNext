using Diten.BuildingBlocks.Security.Secrets;
using Diten.PvgService.Application.RegPvBase;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Diten.PvgService.Persistence;

// The registration of the build/test Mongo store (pack "Build/Test Persistence (Mongo Store)"). Whether this runs at all
// is decided by the host (Pvg:Persistence:Store = Mongo); this method assumes the choice was made and only registers.
public static class PvgPersistenceServiceCollectionExtensions
{
    public const string ConnectionStringKey = "Mongo:ConnectionString";
    public const string DatabaseNameKey = "Mongo:DatabaseName";

    // The overload PvgServiceApiHost calls. It carries no Mongo type.
    public static IServiceCollection AddPvgMongoIntakeStore(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment) =>
        Register(services, configuration, environment, null);

    // Test-only: lets a test shape the store's own client (application name, command events). PvgServiceApiHost never
    // calls this overload.
    public static IServiceCollection AddPvgMongoIntakeStore(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        Action<MongoClientSettings> configureClientSettings)
    {
        ArgumentNullException.ThrowIfNull(configureClientSettings);
        return Register(services, configuration, environment, configureClientSettings);
    }

    private static IServiceCollection Register(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        Action<MongoClientSettings>? configureClientSettings)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        // The connection string is a secret: read through the secrets rules of the other services, never logged or echoed.
        services.ValidateRequiredSecrets(configuration, environment, "Pvg.Persistence", [
            new(ConnectionStringKey, "Pvg.Persistence", SecretRequirementKind.ConnectionString)
        ]);

        var connectionString = configuration[ConnectionStringKey]
            ?? throw new InvalidOperationException($"Configuration error: '{ConnectionStringKey}' is missing.");
        var databaseName = configuration[DatabaseNameKey];
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException($"Configuration error: '{DatabaseNameKey}' is missing.");
        }

        services.AddSingleton(_ => new PvgMongoContext(connectionString, databaseName, configureClientSettings));
        services.AddSingleton<IPvgIntakeDraftStore>(provider =>
            new PvgMongoIntakeDraftStore(provider.GetRequiredService<PvgMongoContext>()));
        services.AddSingleton<IPvgIntakeStoreReadiness>(provider =>
            new PvgMongoStoreReadiness(provider.GetRequiredService<PvgMongoContext>()));
        services.AddSingleton<IHostedService>(provider =>
            new PvgMongoSchemaBootstrap(
                provider.GetRequiredService<PvgMongoContext>(),
                provider.GetService<ILoggerFactory>()?.CreateLogger<PvgMongoSchemaBootstrap>()));

        return services;
    }
}

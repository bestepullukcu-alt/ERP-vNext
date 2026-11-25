using Diten.BuildingBlocks.Security.Secrets;
using Diten.PvgService.Application.RegPvBase;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

        // The connection string is a secret: it is required here (a missing value is refused at registration, and the
        // message names only the key) and it is read through the secrets provider of the other services, never logged or
        // echoed. ConfigurationSecretsProvider asks the container for IConfiguration, which a bare ServiceCollection does
        // not have; TryAdd leaves the host's own registration alone.
        services.TryAddSingleton(configuration);
        services.AddSecretsProvider(configuration, environment, options => options.ServiceName = "Pvg.Persistence");
        services.ValidateRequiredSecrets(configuration, environment, "Pvg.Persistence", [
            new(ConnectionStringKey, "Pvg.Persistence", SecretRequirementKind.ConnectionString)
        ]);

        var databaseName = configuration[DatabaseNameKey];
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException($"Configuration error: '{DatabaseNameKey}' is missing.");
        }

        // The secrets provider is asynchronous, so the value is read where the connection is made (the first resolution of
        // the context, which is the start-up bootstrap), not at registration. The provider of this package answers from
        // memory, so waiting for it blocks nothing; the host has no synchronization context.
        services.AddSingleton(provider =>
        {
            var connectionString = provider
                .GetRequiredService<ISecretsProvider>()
                .GetSecretAsync(ConnectionStringKey, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            return new PvgMongoContext(connectionString, databaseName, configureClientSettings);
        });
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

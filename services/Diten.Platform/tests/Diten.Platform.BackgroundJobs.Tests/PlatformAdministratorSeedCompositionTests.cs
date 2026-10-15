using Diten.Platform.Application;
using Diten.Platform.Infrastructure;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Tests.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.BackgroundJobs.Tests;

/// <summary>
/// WP-AUTH-ADMIN-BOOTSTRAP-01 (BL-534) — the platform administrator seed through Platform's REAL start-up composition
/// (<c>AddInfrastructure</c> with its start-up maintenance: migrations, schema, every seed) on a test-owned mongod whose
/// database starts empty. Outside Development the start-up writes its system data and NO administrator; in Development it
/// still writes the development administrator.
/// </summary>
public sealed class PlatformAdministratorSeedCompositionTests
{
    [Theory]
    [InlineData("Staging", "diten_platform_itest_admin_seed_staging", false)]
    [InlineData("Development", "diten_platform_itest_admin_seed_development", true)]
    public async Task The_start_up_writes_the_development_administrator_only_in_development(
        string environmentName, string databaseName, bool administratorExpected)
    {
        // The test process's own mongod (PlatformTestMongo: DITEN_TEST_MONGOD), never the shared 27017; a fixed name, emptied.
        var connectionString = await PlatformTestMongo.ConnectionStringAsync();
        var client = new MongoClient(connectionString);
        await client.DropDatabaseAsync(databaseName);
        var database = client.GetDatabase(databaseName);

        var configuration = Configuration(connectionString, databaseName);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplication();
        services.AddInfrastructure(configuration, new HostEnvironment(environmentName), runStartupMaintenance: true);

        var administrators = await database.GetCollection<BsonDocument>(PlatformCollections.PlatformAdministrators)
            .Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        // Non-vacuity: the start-up ran — its system seed is there (the platform tenant).
        Assert.True(await database.GetCollection<BsonDocument>(PlatformCollections.Tenants).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty) > 0,
            "the start-up maintenance did not run");
        if (administratorExpected)
        {
            var record = Assert.Single(administrators);
            Assert.Equal(PlatformAdministratorSeed.DevelopmentAdministratorId, record["_id"].AsGuid);
        }
        else
        {
            Assert.Empty(administrators);
        }
    }

    private static IConfiguration Configuration(string mongoConnectionString, string databaseName) =>
        new ConfigurationBuilder()
            .AddJsonFile(RepoFile("services", "Diten.Platform", "src", "Diten.Platform.API", "appsettings.json"), optional: false)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MongoDbSettings:ConnectionString"] = mongoConnectionString,
                ["MongoDbSettings:DatabaseName"] = databaseName,
                ["JwtSettings:Secret"] = "admin-seed-composition-test-only-jwt-secret-0123456789",
                ["JwtSettings:Issuer"] = "diten-platform-admin-seed-tests",
                ["JwtSettings:Audience"] = "diten-platform-admin-seed-tests",
                ["AuthService:BaseUrl"] = "http://127.0.0.1:1",
                ["AuthService:InternalApiKey"] = "admin-seed-composition-test-only-internal-api-key",
                ["ModuleRegistrationCredentials:Mdm:Identifier"] = "ditenmdmservice",
                ["ModuleRegistrationCredentials:Mdm:ActiveSecret"] = "admin-seed-composition-test-only-module-registration-secret",
                ["BackgroundJobs:Enabled"] = "false",
                ["BackgroundJobs:DashboardEnabled"] = "false",
                ["Eventing:Transport"] = "InMemory",
                ["Eventing:WorkerEnabled"] = "false",
                ["Smtp:Enabled"] = "false"
            })
            .Build();

    private static string RepoFile(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
        {
            current = current.Parent;
        }

        return Path.Combine([current?.FullName ?? throw new InvalidOperationException("Checkout required"), .. segments]);
    }

    private sealed class HostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Diten.Platform.API";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

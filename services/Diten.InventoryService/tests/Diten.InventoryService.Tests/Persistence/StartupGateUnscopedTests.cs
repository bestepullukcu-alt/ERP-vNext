using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Persistence;
using Diten.InventoryService.Persistence.Repositories;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Persistence;

/// <summary>
/// FU-S2-01 — the startup gate reports the groups the SKU-position rebuild skipped for an incomplete scope, and fails
/// closed on a database that is not a DB-010 test database. The REAL gate (<see cref="InventoryIndexInitializerHostedService"/>)
/// and the REAL rebuilder run against real Mongo; only the logger is a recorder.
/// </summary>
public sealed class StartupGateUnscopedTests
{
    /// <summary>Fixed (DB-010) name WITHOUT a test segment: the one database these tests create, dropped every time.</summary>
    private const string NonTestDatabase = "diten_inventory_k1_gate";

    static StartupGateUnscopedTests() => InventoryBsonMappings.Register();

    [Theory]
    [InlineData("diten_inventory_s1_tests", true)]
    [InlineData("diten_platform_itest", true)]
    [InlineData("diten_ppm_integration_tests", true)]
    [InlineData("diten_inventory_test", true)]
    [InlineData("diten_inventory_v1", false)]
    [InlineData("diten_inventory_dev", false)]
    [InlineData("diten_inventory_mock", false)]
    [InlineData("diten_inventory_k1_gate", false)]
    [InlineData("diten_contest", false)]
    [InlineData("", false)]
    public void Test_database_names_follow_the_repository_convention(string name, bool isTest) =>
        Assert.Equal(isTest, InventoryTestDatabases.IsTestDatabase(name));

    [Fact]
    public async Task On_the_test_database_unscoped_groups_are_a_warning_with_the_count_and_start_continues()
    {
        var client = new MongoClient(TestSettings.MongoConnectionString);
        var balances = client.GetDatabase(TestSettings.DatabaseName).GetCollection<BsonDocument>(InventoryCollections.Balances);
        var marker = UnscopedBalance();
        await balances.InsertOneAsync(marker);
        try
        {
            var log = new RecordingLogger();
            await NewGate(client, TestSettings.DatabaseName, log).StartAsync(CancellationToken.None);

            var warning = Assert.Single(log.Entries, e => e.Level == LogLevel.Warning);
            Assert.Contains("never guessed, never deleted", warning.Message);
            Assert.True(SkippedCount(warning.Message) >= 1);
            Assert.DoesNotContain(log.Entries, e => e.Level == LogLevel.Critical);
            Assert.Equal(1, await balances.CountDocumentsAsync(new BsonDocument("_id", marker["_id"])));
        }
        finally
        {
            await balances.DeleteOneAsync(new BsonDocument("_id", marker["_id"]));
        }
    }

    [Fact]
    public async Task On_a_non_test_database_unscoped_groups_stop_the_service_and_nothing_is_deleted()
    {
        var client = new MongoClient(TestSettings.MongoConnectionString);
        await client.DropDatabaseAsync(NonTestDatabase);
        try
        {
            var balances = client.GetDatabase(NonTestDatabase).GetCollection<BsonDocument>(InventoryCollections.Balances);
            await balances.InsertOneAsync(UnscopedBalance());
            var log = new RecordingLogger();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => NewGate(client, NonTestDatabase, log).StartAsync(CancellationToken.None));

            Assert.Contains(NonTestDatabase, ex.Message);
            var warning = Assert.Single(log.Entries, e => e.Level == LogLevel.Warning);
            Assert.Equal(1, SkippedCount(warning.Message));
            Assert.Single(log.Entries, e => e.Level == LogLevel.Critical && e.Message.Contains("refusing to start"));
            Assert.Equal(1, await balances.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        }
        finally
        {
            await client.DropDatabaseAsync(NonTestDatabase);
        }
    }

    [Fact]
    public async Task Control_a_non_test_database_without_unscoped_groups_starts_without_a_warning()
    {
        var client = new MongoClient(TestSettings.MongoConnectionString);
        await client.DropDatabaseAsync(NonTestDatabase);
        try
        {
            var log = new RecordingLogger();
            await NewGate(client, NonTestDatabase, log).StartAsync(CancellationToken.None);

            Assert.DoesNotContain(log.Entries, e => e.Level >= LogLevel.Warning);
        }
        finally
        {
            await client.DropDatabaseAsync(NonTestDatabase);
        }
    }

    /// <summary>An AVAILABLE balance row without TenantId / LegalEntityId — the S1 sabotage residue shape.</summary>
    private static BsonDocument UnscopedBalance() => new()
    {
        { "_id", Guid.NewGuid().ToString() },
        { "ItemId", Guid.NewGuid().ToString() },
        { "SkuId", Guid.NewGuid().ToString() },
        { "SkuLevel", "Gsku" },
        { "StockStatus", "Available" },
        { "OnHand", new BsonDecimal128(1m) },
        { "IsDeleted", false }
    };

    private static InventoryIndexInitializerHostedService NewGate(IMongoClient client, string database, RecordingLogger log)
    {
        var services = new ServiceCollection();
        services.AddSingleton(client);
        services.AddScoped<IMongoDatabase>(_ => client.GetDatabase(database));
        services.AddScoped<ISkuPositionRebuilder, SkuPositionRebuilder>();
        return new InventoryIndexInitializerHostedService(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), log);
    }

    private static int SkippedCount(string message) =>
        int.Parse(message.Split("skipped ")[1].Split(' ')[0]);

    private sealed class RecordingLogger : ILogger<InventoryIndexInitializerHostedService>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}

using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ProductAbbreviationWorkItemMongoTests
{
    [Fact]
    public async Task Pending_query_is_tenant_safe_initial_only_bounded_and_orders_by_scalar_id()
    {
        await using var scope = await MongoScope.CreateAsync();
        var repository = scope.Repository(scope.TenantA);
        var largerId = Guid.Parse("f0000000-0000-0000-0000-000000000001");
        var smallerId = Guid.Parse("10000000-0000-0000-0000-000000000001");

        await InsertAsync(repository, largerId, DateTimeOffset.Parse("2026-01-01T00:00:00+14:00"), "A");
        await InsertAsync(repository, smallerId, DateTimeOffset.Parse("2026-12-01T00:00:00-12:00"), "B");
        await InsertAsync(repository, Guid.NewGuid(), DateTimeOffset.UtcNow, "CORRECTION", Guid.NewGuid());
        var terminal = await InsertAsync(repository, Guid.NewGuid(), DateTimeOffset.UtcNow, "TERMINAL");
        Assert.True((await repository.TransitionAsync(
            terminal.Id, 0, ProductAbbreviationLifecycleStatus.REQUESTED,
            ProductAbbreviationLifecycleStatus.CANCELLED, "maker", "terminal-transition", null,
            DateTimeOffset.UtcNow)).Succeeded);
        await InsertAsync(scope.Repository(scope.TenantB), Guid.NewGuid(), DateTimeOffset.UtcNow, "TENANT-B");

        var results = await repository.GetInitialPendingWorkItemsAsync(101);

        Assert.Equal([smallerId, largerId], results.Select(x => x.Id).ToArray());
        Assert.All(results, x =>
        {
            Assert.Equal(scope.TenantA, x.TenantId);
            Assert.Equal(ProductAbbreviationLifecycleStatus.REQUESTED, x.LifecycleStatus);
            Assert.Null(x.ReplacesEntryId);
        });
    }

    [Fact]
    public async Task Pending_query_returns_the_101st_overflow_sentinel_and_rejects_larger_limits()
    {
        await using var scope = await MongoScope.CreateAsync();
        var repository = scope.Repository(scope.TenantA);
        for (var index = 0; index < 102; index++)
        {
            await InsertAsync(repository, Guid.NewGuid(), DateTimeOffset.UtcNow, $"K{index:D3}");
        }

        Assert.Equal(101, (await repository.GetInitialPendingWorkItemsAsync(101)).Count);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => repository.GetInitialPendingWorkItemsAsync(102));
    }

    private static async Task<ProductAbbreviationRegisterEntry> InsertAsync(
        ProductAbbreviationRegisterRepository repository,
        Guid id,
        DateTimeOffset requestedAt,
        string key,
        Guid? replaces = null)
    {
        var result = await repository.InsertRequestedAsync(new ProductAbbreviationRegisterEntry
        {
            Id = id,
            GlobalProductId = Guid.NewGuid(),
            NormalizedAbbreviation = key,
            AllocationLedgerId = Guid.NewGuid(),
            AllocationIdempotencyKey = "work-item-" + key,
            RequestedByCanonicalSubjectId = "maker",
            RequestedAtUtc = requestedAt,
            ReplacesEntryId = replaces
        });
        Assert.True(result.Succeeded);
        return result.Entry!;
    }

    private sealed class MongoScope : IAsyncDisposable
    {
        private readonly IMongoClient _client;
        private readonly string _databaseName;

        private MongoScope(IMongoClient client, IMongoDatabase database, string databaseName)
        {
            _client = client;
            Database = database;
            _databaseName = databaseName;
        }

        public Guid TenantA { get; } = Guid.NewGuid();
        public Guid TenantB { get; } = Guid.NewGuid();
        public IMongoDatabase Database { get; }

        public static async Task<MongoScope> CreateAsync()
        {
            var settings = MongoClientSettings.FromConnectionString(
                Environment.GetEnvironmentVariable("MONGO_TEST_URI") ?? "mongodb://localhost:27017");
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            settings.ConnectTimeout = TimeSpan.FromSeconds(5);
#pragma warning disable CS0618
            settings.GuidRepresentation = MongoDB.Bson.GuidRepresentation.Standard;
#pragma warning restore CS0618
            var client = new MongoClient(settings);
            var databaseName = "diten_mdm_abb_wc_itest_" + Guid.NewGuid().ToString("N");
            var database = client.GetDatabase(databaseName);
            await database.RunCommandAsync<MongoDB.Bson.BsonDocument>(
                new MongoDB.Bson.BsonDocument("ping", 1));
            return new MongoScope(client, database, databaseName);
        }

        public ProductAbbreviationRegisterRepository Repository(Guid tenantId)
        {
            var context = new TenantContext();
            context.SetTenant(tenantId);
            return new ProductAbbreviationRegisterRepository(Database, context);
        }

        public async ValueTask DisposeAsync() => await _client.DropDatabaseAsync(_databaseName);
    }
}

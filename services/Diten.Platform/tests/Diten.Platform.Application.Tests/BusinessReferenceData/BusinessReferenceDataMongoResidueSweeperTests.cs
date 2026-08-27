using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class BusinessReferenceDataMongoResidueSweeperTests : IAsyncLifetime
{
    private readonly MongoClient _client = new("mongodb://127.0.0.1:27017");
    private readonly List<string> _createdDatabaseNames = [];

    public async Task InitializeAsync()
    {
        await _client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
    }

    public async Task DisposeAsync()
    {
        foreach (var databaseName in _createdDatabaseNames)
        {
            await _client.DropDatabaseAsync(databaseName);
        }
    }

    [Fact]
    public void BusinessReferenceDataProfile_HasExactlyEightCollectionsAndAtMostEighteenLogicalIndexes()
    {
        var profile = PlatformSchemaManifest.For(SchemaProfile.BusinessReferenceData);

        Assert.Equal(8, profile.Count);
        Assert.True(profile.Sum(collection => collection.LogicalIndexCount) <= 18);
        Assert.Contains(profile, collection => collection.Name == "business_reference_data_validation_results");
    }

    [Fact]
    public async Task Sweep_KeepsForeignPrefixMissingMarkerOtherHarnessCurrentRunAndFreshDatabases()
    {
        var foreignPrefix = await CreateDatabaseAsync("diten_platform_brd_test_" + Guid.NewGuid().ToString("N"));
        var missingMarker = await CreateDatabaseAsync(BusinessReferenceDataMongoResidueSweeper.CreateDatabaseNameForTests("miss", Guid.NewGuid()));
        var otherHarness = await CreateMarkedDatabaseAsync("other", "previous-run", DateTime.UtcNow.AddMinutes(-2), "OtherHarness");
        var currentRun = await CreateMarkedDatabaseAsync("cur", BusinessReferenceDataMongoResidueSweeper.CurrentRunIdForTests, DateTime.UtcNow.AddMinutes(-2));
        var fresh = await CreateMarkedDatabaseAsync("fresh", "previous-run", DateTime.UtcNow);

        var dropped = await BusinessReferenceDataMongoResidueSweeper.SweepAsync(_client);

        Assert.DoesNotContain(foreignPrefix, dropped);
        Assert.DoesNotContain(missingMarker, dropped);
        Assert.DoesNotContain(otherHarness, dropped);
        Assert.DoesNotContain(currentRun, dropped);
        Assert.DoesNotContain(fresh, dropped);
        await AssertDatabaseExistsAsync(foreignPrefix);
        await AssertDatabaseExistsAsync(missingMarker);
        await AssertDatabaseExistsAsync(otherHarness);
        await AssertDatabaseExistsAsync(currentRun);
        await AssertDatabaseExistsAsync(fresh);
    }

    [Fact]
    public async Task Sweep_DropsOnlyStaleOwnedMarkedDatabaseFromPreviousRun()
    {
        var stale = await CreateMarkedDatabaseAsync("stale", "previous-run", DateTime.UtcNow.AddMinutes(-2));

        var dropped = await BusinessReferenceDataMongoResidueSweeper.SweepAsync(_client);

        Assert.Contains(stale, dropped);
        await AssertDatabaseDoesNotExistAsync(stale);
    }

    [Fact]
    public async Task CreateAndDispose_UsesMarkerInfrastructureExemptionAndLeavesNoResidue()
    {
        var databaseName = await BusinessReferenceDataMongoResidueSweeper.CreateDatabaseAsync(_client, "drop");
        _createdDatabaseNames.Add(databaseName);
        var database = _client.GetDatabase(databaseName);
        await PlatformSchemaManifest.ApplyAsync(database, new[] { SchemaProfile.BusinessReferenceData });

        var collections = await (await database.ListCollectionNamesAsync()).ToListAsync();
        Assert.Contains(BusinessReferenceDataMongoResidueSweeper.MarkerCollectionName, collections);

        await _client.DropDatabaseAsync(databaseName);
        _createdDatabaseNames.Remove(databaseName);
        await AssertDatabaseDoesNotExistAsync(databaseName);
    }

    [Fact]
    public async Task TwoSuccessiveSuites_DoNotIncreaseOwnedDatabaseCount()
    {
        const string scope = "suite";
        var before = await CountOwnedDatabasesAsync(scope);
        var first = await BusinessReferenceDataMongoResidueSweeper.CreateDatabaseAsync(_client, "suite");
        await _client.DropDatabaseAsync(first);
        var afterFirst = await CountOwnedDatabasesAsync(scope);
        var second = await BusinessReferenceDataMongoResidueSweeper.CreateDatabaseAsync(_client, "suite");
        await _client.DropDatabaseAsync(second);
        var afterSecond = await CountOwnedDatabasesAsync(scope);

        Assert.Equal(before, afterFirst);
        Assert.Equal(before, afterSecond);
    }

    private async Task<string> CreateMarkedDatabaseAsync(string scope, string runId, DateTime createdAtUtc, string harness = "BusinessReferenceDataTestHarness")
    {
        var databaseName = BusinessReferenceDataMongoResidueSweeper.CreateDatabaseNameForTests(scope, Guid.NewGuid());
        _createdDatabaseNames.Add(databaseName);
        await BusinessReferenceDataMongoResidueSweeper.WriteMarkerForTestsAsync(_client.GetDatabase(databaseName), runId, createdAtUtc, harness);
        return databaseName;
    }

    private async Task<string> CreateDatabaseAsync(string databaseName)
    {
        _createdDatabaseNames.Add(databaseName);
        await _client.GetDatabase(databaseName).GetCollection<BsonDocument>("data").InsertOneAsync(new BsonDocument("created", true));
        return databaseName;
    }

    private async Task<int> CountOwnedDatabasesAsync(string scope)
    {
        using var cursor = await _client.ListDatabaseNamesAsync();
        var scopePrefix = $"{BusinessReferenceDataMongoResidueSweeper.DatabasePrefix}_{scope}_";
        return (await cursor.ToListAsync()).Count(name => name.StartsWith(scopePrefix, StringComparison.Ordinal));
    }

    private async Task AssertDatabaseExistsAsync(string databaseName)
    {
        using var cursor = await _client.ListDatabaseNamesAsync();
        Assert.Contains(databaseName, await cursor.ToListAsync());
    }

    private async Task AssertDatabaseDoesNotExistAsync(string databaseName)
    {
        using var cursor = await _client.ListDatabaseNamesAsync();
        Assert.DoesNotContain(databaseName, await cursor.ToListAsync());
    }
}

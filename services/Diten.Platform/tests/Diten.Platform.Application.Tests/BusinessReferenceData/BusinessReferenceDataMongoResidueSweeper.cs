using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.RegularExpressions;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

/// <summary>
/// Owns only abandoned real-Mongo databases created by the BRD test harness.
/// The marker is test infrastructure, not part of the BusinessReferenceData schema profile.
/// </summary>
internal static partial class BusinessReferenceDataMongoResidueSweeper
{
    internal const string DatabasePrefix = "diten_platform_brd_itest";
    internal const string MarkerCollectionName = "__diten_platform_brd_itest_harness";
    private const string MarkerId = "business-reference-data-test-harness";
    private const string HarnessName = "BusinessReferenceDataTestHarness";
    private static readonly string CurrentRunId = Guid.NewGuid().ToString("N");
    private static readonly TimeSpan MinimumResidueAge = TimeSpan.FromMinutes(1);

    public static async Task<string> CreateDatabaseAsync(IMongoClient client, string scope)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        if (!ScopeRegex().IsMatch(scope))
        {
            throw new ArgumentException("BRD Mongo test scope must be 1–6 lowercase alphanumeric characters.", nameof(scope));
        }

        await SweepAsync(client);
        var databaseName = $"{DatabasePrefix}_{scope}_{Guid.NewGuid():N}";
        await WriteMarkerAsync(client.GetDatabase(databaseName), CurrentRunId, DateTime.UtcNow);
        return databaseName;
    }

    public static async Task<IReadOnlyList<string>> SweepAsync(IMongoClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        using var cursor = await client.ListDatabaseNamesAsync();
        var databaseNames = await cursor.ToListAsync();
        var dropped = new List<string>();
        foreach (var databaseName in databaseNames.Where(name => DatabaseNameRegex().IsMatch(name)))
        {
            var database = client.GetDatabase(databaseName);
            var marker = await database.GetCollection<BsonDocument>(MarkerCollectionName)
                .Find(Builders<BsonDocument>.Filter.Eq("_id", MarkerId))
                .FirstOrDefaultAsync();

            if (marker is null
                || !marker.TryGetValue("harness", out var harness)
                || harness != HarnessName
                || !marker.TryGetValue("runId", out var runId)
                || runId == CurrentRunId
                || !IsOlderThanThreshold(marker))
            {
                continue;
            }

            await client.DropDatabaseAsync(databaseName);
            dropped.Add(databaseName);
        }

        return dropped;
    }

    internal static string CreateDatabaseNameForTests(string scope, Guid id)
        => $"{DatabasePrefix}_{scope}_{id:N}";

    internal static string CurrentRunIdForTests => CurrentRunId;

    internal static Task WriteMarkerForTestsAsync(
        IMongoDatabase database,
        string runId,
        DateTime createdAtUtc,
        string harness = HarnessName)
        => WriteMarkerAsync(database, runId, createdAtUtc, harness);

    private static Task WriteMarkerAsync(IMongoDatabase database, string runId, DateTime createdAtUtc, string harness = HarnessName)
        => database.GetCollection<BsonDocument>(MarkerCollectionName).ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", MarkerId),
            new BsonDocument
            {
                ["_id"] = MarkerId,
                ["harness"] = harness,
                ["runId"] = runId,
                ["createdAtUtc"] = createdAtUtc
            },
            new ReplaceOptions { IsUpsert = true });

    private static bool IsOlderThanThreshold(BsonDocument marker)
        => marker.TryGetValue("createdAtUtc", out var createdAt)
           && createdAt.IsBsonDateTime
           && DateTime.UtcNow - createdAt.AsBsonDateTime.ToUniversalTime() > MinimumResidueAge;

    [GeneratedRegex("^[a-z0-9]{1,6}$")]
    private static partial Regex ScopeRegex();

    [GeneratedRegex("^diten_platform_brd_itest_[a-z0-9]+_[0-9a-f]{32}$")]
    private static partial Regex DatabaseNameRegex();
}

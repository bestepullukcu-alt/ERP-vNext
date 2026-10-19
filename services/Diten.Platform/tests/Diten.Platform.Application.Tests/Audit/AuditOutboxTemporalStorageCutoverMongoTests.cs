using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

public sealed class AuditOutboxTemporalStorageCutoverMongoTests : IAsyncLifetime
{
    private const string CollectionName = "audit_outbox_temporal_candidate_benchmark";
    private const string CandidateRangeFirst = "ix_candidate_status_next_created_id";
    private const string CandidateSortFirst = "ix_candidate_status_created_id_next";

    private MongoIntegrationHarness _harness = null!;
    private IMongoCollection<BsonDocument> _collection = null!;

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateIsolatedAsync(
            "audit_outbox_temporal_cutover",
            SchemaProfile.AccessGovernance);
        _collection = _harness.Database.GetCollection<BsonDocument>(CollectionName);
        await _collection.DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);
        await DropCandidateIndexesAsync();
    }

    public async Task DisposeAsync()
    {
        await DropCandidateIndexesAsync();
        await _collection.DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);
        await _harness.DisposeAsync();
    }

    [Fact]
    public async Task ScalarCandidates_WithMixedOffsets_ReturnExactInstantOrderAndMeasuredPlans()
    {
        var cutoff = new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);
        var expectedReady = new List<(Guid Id, long CreatedTicks)>();
        var documents = new List<BsonDocument>();

        for (var index = 0; index < 600; index++)
        {
            var id = GuidFromOrdinal(index + 1);
            var createdInstant = cutoff.AddMinutes(index - 600);
            var createdWithMixedOffset = createdInstant.ToOffset(OffsetFor(index));
            var isCompleted = index >= 500;
            var isReady = index < 100;
            var nextInstant = isReady ? cutoff.AddMinutes(-index - 1) : cutoff.AddMinutes(index + 1);
            var nextWithMixedOffset = nextInstant.ToOffset(OffsetFor(index + 1));

            documents.Add(new BsonDocument
            {
                ["_id"] = new BsonBinaryData(id, GuidRepresentation.Standard),
                ["Status"] = isCompleted ? 3 : 1,
                ["Attempts"] = 0,
                ["NextAttemptAtUtc"] = Legacy(nextWithMixedOffset),
                ["CreatedAtUtc"] = Legacy(createdWithMixedOffset),
                ["NextAttemptAtUtcTicksV1"] = nextWithMixedOffset.UtcTicks,
                ["CreatedAtUtcTicksV1"] = createdWithMixedOffset.UtcTicks,
                ["TemporalStorageVersion"] = 1
            });

            if (!isCompleted && isReady)
            {
                expectedReady.Add((id, createdWithMixedOffset.UtcTicks));
            }
        }

        await _collection.InsertManyAsync(documents);
        await CreateCandidateIndexesAsync();

        var filter = ClaimFilter(cutoff);
        var sort = new BsonDocument
        {
            ["CreatedAtUtcTicksV1"] = 1,
            ["_id"] = 1
        };

        var expectedIds = expectedReady
            .OrderBy(item => item.CreatedTicks)
            .ThenBy(item => item.Id)
            .Select(item => item.Id)
            .ToArray();

        var actualDocuments = await _collection
            .Find(filter)
            .Sort(sort)
            .ToListAsync();
        var actualIds = actualDocuments.Select(document => document["_id"].AsGuid).ToArray();

        Assert.Equal(expectedIds, actualIds);

        var rangeFirst = await ExplainAsync(filter, sort, CandidateRangeFirst);
        var sortFirst = await ExplainAsync(filter, sort, CandidateSortFirst);

        Assert.False(rangeFirst.IsMultiKey);
        Assert.False(sortFirst.IsMultiKey);
        Assert.Contains(CandidateRangeFirst, rangeFirst.IndexNames);
        Assert.Contains(CandidateSortFirst, sortFirst.IndexNames);
        Assert.True(rangeFirst.DocumentsExamined <= sortFirst.DocumentsExamined,
            $"Range-first candidate must not examine more documents. range={rangeFirst}; sort={sortFirst}");
        Assert.True(rangeFirst.KeysExamined < sortFirst.KeysExamined,
            $"Range-first candidate should examine fewer keys. range={rangeFirst}; sort={sortFirst}");
        Assert.True(rangeFirst.HasBlockingSort,
            $"Range-first ESR trade-off must remain explicit. Evidence={rangeFirst}");
        Assert.True(sortFirst.HasBlockingSort,
            $"The exact repository OR branches require a merge sort even for the sort-first candidate. Evidence={sortFirst}");

        Console.WriteLine($"FU02 candidate A ({CandidateRangeFirst}): {rangeFirst}");
        Console.WriteLine($"FU02 candidate B ({CandidateSortFirst}): {sortFirst}");
        Console.WriteLine(
            "FU02 measured recommendation: range-first candidate wins this representative distribution "
            + "on keys/documents examined while retaining exact scalar semantics; it pays a blocking sort. "
            + "Owner selection must explicitly accept that trade-off.");
    }

    [Fact]
    public async Task ScalarRange_WithSameInstantDifferentOffsets_ClassifiesExactlyAndTiesById()
    {
        var cutoff = new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);
        var first = GuidFromOrdinal(1);
        var second = GuidFromOrdinal(2);
        var future = GuidFromOrdinal(3);
        var sameCreatedTicks = cutoff.AddHours(-1).UtcTicks;

        await _collection.InsertManyAsync(new[]
        {
            Row(first, cutoff.ToOffset(TimeSpan.FromHours(14)), sameCreatedTicks),
            Row(second, cutoff.ToOffset(TimeSpan.FromHours(-12)), sameCreatedTicks),
            Row(future, cutoff.AddTicks(1).ToOffset(TimeSpan.FromHours(-12)), sameCreatedTicks - 1)
        });

        var rows = await _collection
            .Find(new BsonDocument
            {
                ["Status"] = 1,
                ["NextAttemptAtUtcTicksV1"] = new BsonDocument("$lte", cutoff.UtcTicks)
            })
            .Sort(new BsonDocument
            {
                ["CreatedAtUtcTicksV1"] = 1,
                ["_id"] = 1
            })
            .ToListAsync();

        Assert.Equal(new[] { first, second }, rows.Select(row => row["_id"].AsGuid));
        Assert.All(rows, row => Assert.Equal(BsonType.Int64, row["NextAttemptAtUtcTicksV1"].BsonType));
        Assert.All(rows, row => Assert.Equal(BsonType.Array, row["NextAttemptAtUtc"].BsonType));
    }

    [Theory]
    [InlineData("future-heavy", 5000, 50, 1, false, true)]
    [InlineData("ready-dense", 5000, 4500, 1, false, false)]
    [InlineData("stale-processing", 5000, 50, 2, false, true)]
    [InlineData("equal-created-ties", 1000, 500, 1, true, false)]
    public async Task CandidateMatrix_RepresentativeDistribution_RecordsSemanticAndExplainEvidence(
        string scenario,
        int total,
        int eligible,
        int status,
        bool equalCreatedTicks,
        bool readyRowsAreNewest)
    {
        var cutoff = new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);
        var documents = Enumerable.Range(0, total)
            .Select(index =>
            {
                var isEligible = readyRowsAreNewest
                    ? index >= total - eligible
                    : index < eligible;
                var next = isEligible
                    ? cutoff.AddTicks(-index - 1)
                    : cutoff.AddTicks(index + 1);
                var createdTicks = equalCreatedTicks
                    ? cutoff.AddHours(-1).UtcTicks
                    : cutoff.AddMinutes(index - total).UtcTicks;
                return Row(GuidFromOrdinal(index + 1), next.ToOffset(OffsetFor(index)), createdTicks, status);
            })
            .ToArray();
        await _collection.InsertManyAsync(documents);
        await CreateCandidateIndexesAsync();

        var filter = ClaimFilter(cutoff);
        var sort = new BsonDocument
        {
            ["CreatedAtUtcTicksV1"] = 1,
            ["_id"] = 1
        };

        var rangeRows = await FindWithHintAsync(filter, sort, CandidateRangeFirst);
        var sortRows = await FindWithHintAsync(filter, sort, CandidateSortFirst);
        var rangeFirst = await ExplainAsync(filter, sort, CandidateRangeFirst);
        var sortFirst = await ExplainAsync(filter, sort, CandidateSortFirst);
        var rangeWinner = await ExplainAsync(filter, sort, CandidateRangeFirst, limit: 1);
        var sortWinner = await ExplainAsync(filter, sort, CandidateSortFirst, limit: 1);

        Assert.Equal(eligible, rangeRows.Count);
        Assert.Equal(rangeRows.Select(row => row["_id"]), sortRows.Select(row => row["_id"]));
        Assert.Equal(eligible, rangeFirst.Returned);
        Assert.Equal(eligible, sortFirst.Returned);
        Assert.True(rangeFirst.KeysExamined <= sortFirst.KeysExamined,
            $"scenario={scenario}; range={rangeFirst}; sort={sortFirst}");
        Assert.False(rangeFirst.IsMultiKey);
        Assert.False(sortFirst.IsMultiKey);
        Assert.Equal(1, rangeWinner.Returned);
        Assert.Equal(1, sortWinner.Returned);

        Console.WriteLine($"FU02 matrix {scenario} A: {rangeFirst}");
        Console.WriteLine($"FU02 matrix {scenario} B: {sortFirst}");
        Console.WriteLine($"FU02 winner {scenario} A: {rangeWinner}");
        Console.WriteLine($"FU02 winner {scenario} B: {sortWinner}");
    }

    private async Task CreateCandidateIndexesAsync()
    {
        await _collection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys
                    .Ascending("Status")
                    .Ascending("NextAttemptAtUtcTicksV1")
                    .Ascending("CreatedAtUtcTicksV1")
                    .Ascending("_id"),
                new CreateIndexOptions { Name = CandidateRangeFirst }),
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys
                    .Ascending("Status")
                    .Ascending("CreatedAtUtcTicksV1")
                    .Ascending("_id")
                    .Ascending("NextAttemptAtUtcTicksV1"),
                new CreateIndexOptions { Name = CandidateSortFirst })
        });
    }

    private async Task DropCandidateIndexesAsync()
    {
        var names = await _collection.Indexes.List().ToListAsync();
        foreach (var name in names.Select(index => index["name"].AsString))
        {
            if (name is CandidateRangeFirst or CandidateSortFirst)
            {
                await _collection.Indexes.DropOneAsync(name);
            }
        }
    }

    private async Task<ExplainEvidence> ExplainAsync(
        BsonDocument filter,
        BsonDocument sort,
        string hint,
        int? limit = null)
    {
        var find = new BsonDocument
        {
            ["find"] = CollectionName,
            ["filter"] = filter,
            ["sort"] = sort,
            ["hint"] = hint
        };
        if (limit.HasValue)
        {
            find["limit"] = limit.Value;
        }

        var command = new BsonDocument
        {
            ["explain"] = find,
            ["verbosity"] = "executionStats"
        };

        var explain = await _harness.Database.RunCommandAsync<BsonDocument>(command);
        var executionStats = explain["executionStats"].AsBsonDocument;
        var winningPlan = explain["queryPlanner"].AsBsonDocument["winningPlan"].AsBsonDocument;
        var indexNames = Descendants(winningPlan)
            .Where(document => document.TryGetValue("indexName", out _))
            .Select(document => document["indexName"].AsString)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new ExplainEvidence(
            executionStats["totalKeysExamined"].ToInt64(),
            executionStats["totalDocsExamined"].ToInt64(),
            executionStats["nReturned"].ToInt64(),
            executionStats.GetValue("executionTimeMillis", 0).ToInt64(),
            Descendants(winningPlan).Any(document => document.GetValue("stage", "").AsString == "SORT"),
            Descendants(winningPlan).Any(document =>
                document.TryGetValue("isMultiKey", out var value) && value.IsBoolean && value.AsBoolean),
            indexNames);
    }

    private async Task<IReadOnlyList<BsonDocument>> FindWithHintAsync(
        BsonDocument filter,
        BsonDocument sort,
        string hint)
    {
        var options = new FindOptions<BsonDocument>
        {
            Hint = hint,
            Sort = sort
        };
        using var cursor = await _collection.FindAsync(filter, options);
        return await cursor.ToListAsync();
    }

    private static IEnumerable<BsonDocument> Descendants(BsonDocument root)
    {
        yield return root;
        foreach (var element in root.Elements)
        {
            if (element.Value is BsonDocument document)
            {
                foreach (var descendant in Descendants(document))
                {
                    yield return descendant;
                }
            }
            else if (element.Value is BsonArray array)
            {
                foreach (var documentValue in array.OfType<BsonDocument>())
                {
                    foreach (var descendant in Descendants(documentValue))
                    {
                        yield return descendant;
                    }
                }
            }
        }
    }

    private static BsonDocument Row(
        Guid id,
        DateTimeOffset nextAttempt,
        long createdTicks,
        int status = 1) => new()
    {
        ["_id"] = new BsonBinaryData(id, GuidRepresentation.Standard),
        ["Status"] = status,
        ["Attempts"] = 0,
        ["NextAttemptAtUtc"] = Legacy(nextAttempt),
        ["CreatedAtUtc"] = Legacy(new DateTimeOffset(createdTicks, TimeSpan.Zero)),
        ["NextAttemptAtUtcTicksV1"] = nextAttempt.UtcTicks,
        ["CreatedAtUtcTicksV1"] = createdTicks,
        ["TemporalStorageVersion"] = 1
    };

    private static BsonDocument ClaimFilter(DateTimeOffset cutoff) => new(
        "$and",
        new BsonArray
        {
            new BsonDocument(
                "$or",
                new BsonArray
                {
                    new BsonDocument
                    {
                        ["Status"] = new BsonDocument("$in", new BsonArray { 1, 4 }),
                        ["NextAttemptAtUtcTicksV1"] = new BsonDocument("$lte", cutoff.UtcTicks),
                        ["Attempts"] = new BsonDocument("$lt", 5)
                    },
                    new BsonDocument
                    {
                        ["Status"] = 2,
                        ["NextAttemptAtUtcTicksV1"] = new BsonDocument("$lte", cutoff.UtcTicks),
                        ["Attempts"] = new BsonDocument("$lt", 5)
                    }
                }),
            new BsonDocument("TemporalStorageVersion", 1),
            new BsonDocument("NextAttemptAtUtcTicksV1", new BsonDocument("$ne", BsonNull.Value)),
            new BsonDocument("CreatedAtUtcTicksV1", new BsonDocument("$ne", BsonNull.Value))
        });

    private static BsonArray Legacy(DateTimeOffset value) => new()
    {
        value.Ticks,
        (int)value.Offset.TotalMinutes
    };

    private static TimeSpan OffsetFor(int index) => (index % 3) switch
    {
        0 => TimeSpan.FromHours(14),
        1 => TimeSpan.Zero,
        _ => TimeSpan.FromHours(-12)
    };

    private static Guid GuidFromOrdinal(int ordinal)
    {
        Span<byte> bytes = stackalloc byte[16];
        bytes[15] = checked((byte)(ordinal % 256));
        bytes[14] = checked((byte)(ordinal / 256));
        return new Guid(bytes, bigEndian: true);
    }

    private sealed record ExplainEvidence(
        long KeysExamined,
        long DocumentsExamined,
        long Returned,
        long ExecutionTimeMilliseconds,
        bool HasBlockingSort,
        bool IsMultiKey,
        IReadOnlyList<string> IndexNames)
    {
        public override string ToString() =>
            $"keys={KeysExamined}, docs={DocumentsExamined}, returned={Returned}, "
            + $"executionMs={ExecutionTimeMilliseconds}, "
            + $"blockingSort={HasBlockingSort}, multikey={IsMultiKey}, "
            + $"indexes=[{string.Join(',', IndexNames)}]";
    }
}

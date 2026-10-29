using Diten.SupplyChainService.Domain.Features.SupplierPerformance;
using Diten.SupplyChainService.Persistence.Features.SupplierPerformance;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
namespace Diten.SupplyChainService.Tests.SupplierPerformance;

internal sealed class SupplierPerformanceProbe : ISupplierPerformanceCommitProbe
{
    public string? FailAt;
    public Exception Failure = new IOException("Injected supplier-performance write boundary failure.");
    public Task AtAsync(string boundary, CancellationToken ct)
    {
        var expected = Volatile.Read(ref FailAt);
        if (string.Equals(expected, boundary, StringComparison.Ordinal) && ReferenceEquals(Interlocked.CompareExchange(ref FailAt, null, expected), expected))
            throw Failure;
        return Task.CompletedTask;
    }
}

/// <summary>
/// MOD-0147 DL-1 storage on a real replica set: the two unique indexes behind §0.5 (receipt tuple) and §0.2 (one active
/// evaluation per Tenant + LE + canonical supplier + period), scope isolation and the commit boundaries.
/// </summary>
public sealed class EvaluationStorageTests : IAsyncLifetime
{
    private readonly MongoClient _client = new(SupplierPerformanceMongo.Connection);
    private readonly string _name = "sce_mod0147_storage_" + Guid.NewGuid().ToString("N")[..12];
    private IMongoDatabase _db = null!;
    private readonly SupplierPerformanceProbe _probe = new();
    private readonly SupplierPerformanceScope _scope = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    public async Task InitializeAsync()
    {
        _db = _client.GetDatabase(_name);
        await new SupplierPerformanceSchema(_db).StartAsync(CancellationToken.None);
    }

    public Task DisposeAsync() => _client.DropDatabaseAsync(_name);

    private EvaluationRepository Repository => new(_db, _probe);

    private static SupplierEvaluation Evaluation(SupplierPerformanceScope scope, string supplier = "SUP-2001", string start = "2026-07-01", string end = "2026-07-31") => new()
    {
        Id = Guid.NewGuid(), TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId, CreatedBy = scope.ActorId, CreatedAt = DateTimeOffset.UtcNow,
        Version = 1, SupplierId = supplier, PeriodStart = start, PeriodEnd = end, Metrics = [new("OTD", "90", "25")], OverallScore = "90.00",
        RiskLevel = "LOW", Status = EvaluationStatus.DRAFT, IsActivePeriod = true, PolicyRevision = ScorePolicy.Revision, CorrelationId = Guid.NewGuid()
    };

    private Task<long> Count(string collection) => _db.GetCollection<BsonDocument>(collection).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

    [Fact]
    public async Task Schema_ShouldCreateTheReceiptTupleAndTheActivePeriodUniqueIndexes()
    {
        var evaluations = await (await _db.GetCollection<BsonDocument>(SupplierPerformanceSchema.Evaluations).Indexes.ListAsync()).ToListAsync();
        var period = Assert.Single(evaluations, x => x["name"] == SupplierPerformanceSchema.ActivePeriodIndex);
        Assert.True(period["unique"].AsBoolean);
        Assert.Equal(new BsonDocument("IsActivePeriod", true), period["partialFilterExpression"]);
        Assert.Equal(["TenantId", "LegalEntityId", "SupplierId", "PeriodStart", "PeriodEnd"], period["key"].AsBsonDocument.Names);
        var receipts = await (await _db.GetCollection<BsonDocument>(SupplierPerformanceSchema.Receipts).Indexes.ListAsync()).ToListAsync();
        var tuple = Assert.Single(receipts, x => x["name"] == SupplierPerformanceSchema.ReceiptIndex);
        Assert.True(tuple["unique"].AsBoolean);
        Assert.Equal(["TenantId", "LegalEntityId", "Operation", "TargetId", "IdempotencyKey"], tuple["key"].AsBsonDocument.Names);
    }

    [Fact]
    public async Task Create_WhenCommitted_ShouldWriteTheEvaluationAndOneReceiptWithTheOriginalBody()
    {
        var evaluation = Evaluation(_scope);

        Assert.Equal(EvaluationCommitOutcome.Committed, await Repository.CreateAsync(_scope, "k1", "v1:fp", evaluation, "{\"body\":1}", CancellationToken.None));

        var receipt = await Repository.FindCreateReceiptAsync(_scope, "k1", CancellationToken.None);
        Assert.Equal(new EvaluationReceipt("v1:fp", 201, "{\"body\":1}"), receipt);
        Assert.Equal(evaluation.Id, (await Repository.GetAsync(_scope, evaluation.Id, CancellationToken.None))!.Id);
        Assert.Equal(1, await Count(SupplierPerformanceSchema.Evaluations));
    }

    // T-0147-RP-10 (storage half): the receipt tuple is unique.
    [Fact]
    public async Task Create_WhenTheSameKeyIsUsedAgainInTheSameScope_ShouldClashAndWriteNothing()
    {
        await Repository.CreateAsync(_scope, "same", "v1:a", Evaluation(_scope), "{}", CancellationToken.None);

        var second = await Repository.CreateAsync(_scope, "same", "v1:b", Evaluation(_scope, start: "2026-08-01", end: "2026-08-31"), "{}", CancellationToken.None);

        Assert.Equal(EvaluationCommitOutcome.Clash, second);
        Assert.Equal(1, await Count(SupplierPerformanceSchema.Evaluations));
        Assert.Equal(1, await Count(SupplierPerformanceSchema.Receipts));
    }

    // T-0147-RP-11 (storage half): one active evaluation per Tenant + LE + canonical supplier + period.
    [Fact]
    public async Task Create_WhenAnotherKeyNamesAnActiveSupplierPeriod_ShouldClashAndWriteNothing()
    {
        await Repository.CreateAsync(_scope, "a", "v1:a", Evaluation(_scope), "{}", CancellationToken.None);

        Assert.Equal(EvaluationCommitOutcome.Clash, await Repository.CreateAsync(_scope, "b", "v1:b", Evaluation(_scope), "{}", CancellationToken.None));
        Assert.Equal(1, await Count(SupplierPerformanceSchema.Evaluations));
        Assert.Null(await Repository.FindCreateReceiptAsync(_scope, "b", CancellationToken.None));
        Assert.True(await Repository.ActivePeriodExistsAsync(_scope, "SUP-2001", "2026-07-01", "2026-07-31", CancellationToken.None));
    }

    [Fact]
    public async Task Create_WhenTheOtherRecordIsNotActive_ShouldNotClash()
    {
        var cancelled = Evaluation(_scope); cancelled.Status = EvaluationStatus.CANCELLED; cancelled.IsActivePeriod = false;
        await Repository.CreateAsync(_scope, "a", "v1:a", cancelled, "{}", CancellationToken.None);

        Assert.Equal(EvaluationCommitOutcome.Committed, await Repository.CreateAsync(_scope, "b", "v1:b", Evaluation(_scope), "{}", CancellationToken.None));
    }

    // T-0147-RP-05 (storage half): another tenant or company may reuse the key and the period.
    [Fact]
    public async Task Create_WhenAnotherTenantOrCompanyUsesTheSameKeyAndPeriod_ShouldCommitInItsOwnScope()
    {
        var otherCompany = _scope with { LegalEntityId = Guid.NewGuid() };
        var otherTenant = _scope with { TenantId = Guid.NewGuid() };
        foreach (var scope in new[] { _scope, otherCompany, otherTenant })
            Assert.Equal(EvaluationCommitOutcome.Committed, await Repository.CreateAsync(scope, "shared", "v1:a", Evaluation(scope), "{}", CancellationToken.None));

        Assert.Equal(3, await Count(SupplierPerformanceSchema.Receipts));
        Assert.Null(await Repository.FindCreateReceiptAsync(_scope with { LegalEntityId = Guid.NewGuid() }, "shared", CancellationToken.None));
    }

    [Fact]
    public async Task Create_WhenKeysDifferOnlyByCase_ShouldTreatThemAsDifferentKeys()
    {
        await Repository.CreateAsync(_scope, "Key", "v1:a", Evaluation(_scope), "{}", CancellationToken.None);

        Assert.Equal(EvaluationCommitOutcome.Committed, await Repository.CreateAsync(_scope, "key", "v1:a", Evaluation(_scope, "SUP-2002"), "{}", CancellationToken.None));
        Assert.Equal(2, await Count(SupplierPerformanceSchema.Receipts));
    }

    // T-0147-RP-11 (concurrent): released by a barrier, exactly one commits.
    [Fact]
    public async Task Create_WhenDifferentKeysRaceForOnePeriod_ShouldCommitExactlyOne()
    {
        using var barrier = new Barrier(6);
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 6).Select(i => Task.Run(async () =>
        {
            barrier.SignalAndWait();
            return await Repository.CreateAsync(_scope, "race-" + i, "v1:" + i, Evaluation(_scope), "{}", CancellationToken.None);
        })));

        Assert.Equal(1, outcomes.Count(x => x == EvaluationCommitOutcome.Committed));
        Assert.Equal(5, outcomes.Count(x => x == EvaluationCommitOutcome.Clash));
        Assert.Equal(1, await Count(SupplierPerformanceSchema.Evaluations));
        Assert.Equal(1, await Count(SupplierPerformanceSchema.Receipts));
    }

    // T-0147-RP-01 sabotage half: the receipt is written inside the commit — a failure before it leaves nothing.
    [Theory]
    [InlineData("entity")] [InlineData("receipt")] [InlineData("beforeCommit")]
    public async Task Create_WhenAWriteBoundaryFailsBeforeTheCommit_ShouldLeaveNoEvaluationAndNoReceipt(string boundary)
    {
        _probe.FailAt = boundary;

        await Assert.ThrowsAsync<IOException>(() => Repository.CreateAsync(_scope, "k", "v1:a", Evaluation(_scope), "{}", CancellationToken.None));

        Assert.Equal(0, await Count(SupplierPerformanceSchema.Evaluations));
        Assert.Equal(0, await Count(SupplierPerformanceSchema.Receipts));
    }

    // T-0147-RP-08 (storage half): an unknown commit is reported, never written a second time.
    [Theory]
    [InlineData("commit", 0)] [InlineData("afterCommit", 1)]
    public async Task Create_WhenTheCommitOutcomeIsUnknown_ShouldReportUnknownAndWriteAtMostOnce(string boundary, int expected)
    {
        _probe.FailAt = boundary;

        Assert.Equal(EvaluationCommitOutcome.Unknown, await Repository.CreateAsync(_scope, "k", "v1:a", Evaluation(_scope), "{}", CancellationToken.None));

        Assert.Equal(expected, await Count(SupplierPerformanceSchema.Evaluations));
        Assert.Equal(expected, await Count(SupplierPerformanceSchema.Receipts));
    }

    // T-0147-ISO-01 (storage half): every read is Tenant + LE scoped and hides soft-deleted rows.
    [Fact]
    public async Task Reads_WhenTheScopeIsForeignOrTheRowIsDeleted_ShouldFindNothing()
    {
        var evaluation = Evaluation(_scope); evaluation.Status = EvaluationStatus.PUBLISHED; evaluation.ScorecardId = Guid.NewGuid(); evaluation.PublishedAt = DateTimeOffset.UtcNow;
        await _db.GetCollection<SupplierEvaluation>(SupplierPerformanceSchema.Evaluations).InsertOneAsync(evaluation);
        var deleted = Evaluation(_scope, "SUP-3000"); deleted.IsDeleted = true; deleted.DeletedAt = DateTimeOffset.UtcNow;
        await _db.GetCollection<SupplierEvaluation>(SupplierPerformanceSchema.Evaluations).InsertOneAsync(deleted);
        var empty = new EvaluationFilter(null, null, null, null);

        foreach (var foreign in new[] { _scope with { TenantId = Guid.NewGuid() }, _scope with { LegalEntityId = Guid.NewGuid() } })
        {
            Assert.Null(await Repository.GetAsync(foreign, evaluation.Id, CancellationToken.None));
            Assert.Null(await Repository.GetPublishedAsync(foreign, evaluation.ScorecardId.Value, CancellationToken.None));
            Assert.Empty((await Repository.ListAsync(foreign, empty, 1, 50, CancellationToken.None)).Items);
            Assert.Empty(await Repository.ListPublishedAsync(foreign, new(null, null, null, null), CancellationToken.None));
        }
        Assert.Null(await Repository.GetAsync(_scope, deleted.Id, CancellationToken.None));
        var (items, total) = await Repository.ListAsync(_scope, empty, 1, 50, CancellationToken.None);
        Assert.Equal(evaluation.Id, Assert.Single(items).Id);
        Assert.Equal(1, total);
        Assert.NotNull(await Repository.GetPublishedAsync(_scope, evaluation.ScorecardId.Value, CancellationToken.None));
    }
}

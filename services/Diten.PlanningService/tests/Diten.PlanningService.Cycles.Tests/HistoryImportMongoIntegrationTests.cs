using Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using Diten.PlanningService.Persistence.Features.DemandPlanning;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed class HistoryImportMongoTheoryAttribute : TheoryAttribute
{
    public HistoryImportMongoTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MOD0188_TEST_MONGO_URI")))
            Skip = "Set MOD0188_TEST_MONGO_URI to an explicit loopback test replica set.";
    }
}
// Uses only the explicit, loopback MOD-0188 test replica set and shared test DB.
public sealed class HistoryImportMongoIntegrationTests
{
    private const string DatabaseName = "mod0188_tests";
    private static readonly Guid Uploader = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Reviewer = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherReviewer = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private sealed record Fixture(DemandPlanningMongoContext Context,
        DemandHistoryImportMongoStore Store, DemandHistoryReviewAuditMongoStore Audit,
        Guid TenantId, Guid LegalEntityId, Guid BatchId);

    private sealed class Authority(Guid tenantId, Guid legalEntityId) : IHistoryImportScopeAuthority
    {
        public bool Available { get; set; } = true;
        public bool Allowed { get; set; } = true;

        public Task<Guid?> ResolveSelectedAsync(Guid tenantId, Guid actorId,
            Guid selectedLegalEntityHint, CancellationToken cancellationToken) =>
            Task.FromResult<Guid?>(Available && Allowed && tenantId == tenantIdField &&
                selectedLegalEntityHint == legalEntityIdField ? legalEntityIdField : null);

        public Task<bool?> IsAuthorizedAsync(Guid tenantId, Guid actorId,
            Guid legalEntityId, DateOnly from, DateOnly through,
            IReadOnlyList<string> warehouses, CancellationToken cancellationToken) =>
            Task.FromResult<bool?>(Available ? Allowed && tenantId == tenantIdField &&
                legalEntityId == legalEntityIdField && warehouses.SequenceEqual(["wh-01"]) : null);

        private readonly Guid tenantIdField = tenantId;
        private readonly Guid legalEntityIdField = legalEntityId;
    }

    private static (DemandPlanningMongoContext Context, DemandHistoryImportMongoStore Store,
        DemandHistoryReviewAuditMongoStore Audit, IMongoDatabase Database) Open()
    {
        var uri = Environment.GetEnvironmentVariable("MOD0188_TEST_MONGO_URI")
            ?? throw new InvalidOperationException("Explicit test Mongo URI is required.");
        var url = new MongoUrl(uri);
        var servers = url.Servers.ToArray();
        if (servers.Length != 1 || servers[0].Host != "127.0.0.1" ||
            servers[0].Port is < 31994 or > 39994 || url.ReplicaSetName != "rs-mod0188" ||
            url.Username is not null || url.Password is not null)
            throw new InvalidOperationException("Only the explicit loopback MOD-0188 replica set is allowed.");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = uri,
                ["Mongo:SupplyChainDatabaseName"] = DatabaseName
            }).Build();
        var context = new DemandPlanningMongoContext(configuration);
        var database = new MongoClient(uri).GetDatabase(DatabaseName);
        return (context, new DemandHistoryImportMongoStore(context),
            new DemandHistoryReviewAuditMongoStore(context), database);
    }

    private static async Task<Fixture> SeedAsync(string issue = "warning")
    {
        var (context, store, audit, _) = Open();
        var tenantId = Guid.NewGuid(); // DB-010: shared DB, isolated tenant per test.
        var legalEntityId = Guid.NewGuid();
        var batch = new DemandHistoryImportBatch
        {
            TenantId = tenantId, LegalEntityId = legalEntityId,
            UploadedByActorId = Uploader, SourceChannel = "File",
            SourceSystem = "fixture", SourceFileName = "fixture.csv",
            SourceFileSha256 = new string('A', 64),
            ScopeFrom = new DateOnly(2026, 9, 1),
            ScopeThrough = new DateOnly(2026, 9, 30),
            WarehouseScope = ["wh-01"], FieldScope = ["sourceRecordKey"],
            ValidationState = ImportBatchValidationState.ValidatedWithWarnings,
            ApprovalBlocked = false, IdempotencyKey = Guid.NewGuid().ToString("N"),
            RequestFingerprint = Guid.NewGuid().ToString("N"),
            Rows =
            [
                new DemandHistoryImportRow
                {
                    RowNumber = 2, SourceRecordKey = "SHIP-1",
                    WarehouseId = "wh-01", RecordKind = "Shipment",
                    Issues = [new ImportValidationIssue
                    {
                        Code = "StockoutEvidenceUnknown",
                        Severity = ImportIssueSeverity.Warning
                    }]
                }
            ],
            AuditTrail = [new DemandHistoryImportAuditAction
            {
                Action = "FileBatchValidated", ActorId = Uploader,
                OccurredAt = DateTimeOffset.UtcNow
            }]
        };
        if (issue == "file")
            batch.FileIssues.Add(new ImportValidationIssue
            { Code = "BadFile", Severity = ImportIssueSeverity.Blocking });
        if (issue == "row")
            batch.Rows[0].Issues.Add(new ImportValidationIssue
            { Code = "BadRow", Severity = ImportIssueSeverity.Blocking });
        if (issue == "quarantine")
            batch.QuarantineCases.Add(new DemandHistoryQuarantineCase
            { RowNumber = 2, State = "Open", ReasonCodes = ["Unresolved"] });
        Assert.Equal(HistoryImportInsertOutcome.Created,
            (await store.InsertOrGetAsync(batch, default)).Outcome);
        return new Fixture(context, store, audit, tenantId, legalEntityId, batch.Id);
    }

    private static ReviewHistoryImportBatchCommand Review(Fixture fixture,
        Guid actor, ImportBatchReviewState decision, string key = "review-1",
        string reason = "Reviewed evidence", Guid? tenant = null,
        Guid? legalEntity = null, bool permission = true) =>
        new(tenant ?? fixture.TenantId, actor, fixture.BatchId,
            legalEntity ?? fixture.LegalEntityId, permission, decision, reason, key);

    private static ReviewHistoryImportBatchHandler Handler(Fixture fixture,
        Authority? authority = null) =>
        new(fixture.Store, fixture.Audit,
            authority ?? new Authority(fixture.TenantId, fixture.LegalEntityId));

    private static async Task<DemandHistoryImportBatch> Persisted(Fixture fixture) =>
        Assert.IsType<DemandHistoryImportBatch>(await fixture.Store.GetForReviewAsync(
            fixture.TenantId, fixture.BatchId, default));

    [ManualDraftMongoFact]
    public async Task IndependentReviewer_ApprovesWarningOnlyBatch_WithAtomicDecisionAudit()
    {
        var fixture = await SeedAsync();
        var response = await Handler(fixture).Handle(
            Review(fixture, Reviewer, ImportBatchReviewState.Approved), default);
        Assert.Equal(200, response.StatusCode);
        var saved = await Persisted(fixture);
        Assert.Equal(ImportBatchReviewState.Approved, saved.ReviewState);
        Assert.Equal(Reviewer, saved.ReviewedByActorId);
        Assert.Equal(1, saved.Version);
        Assert.Equal("Reviewed evidence", saved.ReviewReason);
        Assert.NotNull(saved.ReviewedAt);
        Assert.Single(saved.AuditTrail, x => x.Action == "BatchApproved" &&
            x.ActorId == Reviewer && x.RequestKey == "review-1");
        Assert.Single(saved.Rows); // Approval does not create accepted demand history.
    }

    [ManualDraftMongoFact]
    public async Task Uploader_CannotApproveOwnBatch_FailedAttemptIsDurable()
    {
        var fixture = await SeedAsync();
        var handler = Handler(fixture);
        Assert.Equal(403, (await handler.Handle(
            Review(fixture, Uploader, ImportBatchReviewState.Approved), default)).StatusCode);
        Assert.Equal(403, (await handler.Handle(
            Review(fixture, Uploader, ImportBatchReviewState.Approved), default)).StatusCode);
        Assert.Equal(ImportBatchReviewState.Pending, (await Persisted(fixture)).ReviewState);
        var failure = Assert.Single(await fixture.Audit.ListAsync(
            fixture.TenantId, fixture.BatchId, default));
        Assert.Equal("SelfReviewDenied", failure.Outcome);
    }

    [HistoryImportMongoTheory]
    [InlineData("file")]
    [InlineData("row")]
    [InlineData("quarantine")]
    public async Task BlockingEvidence_RejectsApprovalEvenWhenDerivedFlagsAreStale(string issue)
    {
        var fixture = await SeedAsync(issue);
        var expected = await Persisted(fixture);
        Assert.False(expected.ApprovalBlocked);
        var direct = await fixture.Store.TryDecideAsync(expected, Reviewer,
            ImportBatchReviewState.Approved, "Reviewed evidence", "direct",
            "fingerprint", DateTimeOffset.UtcNow, default);
        Assert.Null(direct); // The Mongo conditional filter must enforce the gate too.
        var response = await Handler(fixture).Handle(
            Review(fixture, Reviewer, ImportBatchReviewState.Approved), default);
        Assert.Equal(409, response.StatusCode);
        var saved = await Persisted(fixture);
        Assert.Equal(ImportBatchReviewState.Pending, saved.ReviewState);
        Assert.Equal(0, saved.Version);
        Assert.DoesNotContain(saved.AuditTrail,
            x => x.Action is "BatchApproved" or "BatchRejected");
        Assert.Equal("BlockingValidation", Assert.Single(await fixture.Audit.ListAsync(
            fixture.TenantId, fixture.BatchId, default)).Outcome);
    }

    [ManualDraftMongoFact]
    public async Task ReasonedRejection_ReplayHasOneDecisionAndChangedRetriesHaveDistinctFailureAudit()
    {
        var fixture = await SeedAsync();
        var handler = Handler(fixture);
        var request = Review(fixture, Reviewer, ImportBatchReviewState.Rejected,
            "reject-1", "Dispatch evidence is inconsistent");

        Assert.Equal(200, (await handler.Handle(request, default)).StatusCode);
        var decided = await Persisted(fixture);
        Assert.Equal(ImportBatchReviewState.Rejected, decided.ReviewState);
        Assert.Equal(Reviewer, decided.ReviewedByActorId);
        Assert.NotNull(decided.ReviewedAt);
        Assert.Equal("Dispatch evidence is inconsistent", decided.ReviewReason);
        Assert.Equal("reject-1", decided.ReviewRequestKey);
        Assert.Equal(1, decided.Version);
        var decisionAudit = Assert.Single(decided.AuditTrail,
            x => x.Action == "BatchRejected");
        Assert.Equal(Reviewer, decisionAudit.ActorId);
        Assert.Equal("reject-1", decisionAudit.RequestKey);
        Assert.Equal("Dispatch evidence is inconsistent", decisionAudit.Reason);
        Assert.Empty(await fixture.Audit.ListAsync(fixture.TenantId, fixture.BatchId, default));

        Assert.Equal(200, (await handler.Handle(request, default)).StatusCode);
        var replayed = await Persisted(fixture);
        Assert.Equal(1, replayed.Version);
        Assert.Single(replayed.AuditTrail, x => x.Action == "BatchRejected");
        Assert.Empty(await fixture.Audit.ListAsync(fixture.TenantId, fixture.BatchId, default));

        var changedSameKey = request with { Reason = "Changed evidence" };
        var changedNewKey = request with
        { IdempotencyKey = "reject-2", Reason = "New contradictory evidence" };
        Assert.Equal(409, (await handler.Handle(changedSameKey, default)).StatusCode);
        Assert.Equal(409, (await handler.Handle(changedNewKey, default)).StatusCode);
        Assert.Equal(409, (await handler.Handle(changedNewKey, default)).StatusCode);

        var final = await Persisted(fixture);
        Assert.Equal(ImportBatchReviewState.Rejected, final.ReviewState);
        Assert.Equal("Dispatch evidence is inconsistent", final.ReviewReason);
        Assert.Equal(1, final.Version);
        Assert.Single(final.AuditTrail, x => x.Action == "BatchRejected");
        var failures = await fixture.Audit.ListAsync(fixture.TenantId, fixture.BatchId, default);
        Assert.Equal(2, failures.Count);
        Assert.Contains(failures, x => x.RequestKey == "reject-1" &&
            x.Reason == "Changed evidence" && x.Outcome == "DecisionConflict" &&
            x.ActorId == Reviewer && x.OccurredAt != default);
        Assert.Contains(failures, x => x.RequestKey == "reject-2" &&
            x.Reason == "New contradictory evidence" && x.Outcome == "DecisionConflict" &&
            x.ActorId == Reviewer && x.OccurredAt != default);
    }

    [ManualDraftMongoFact]
    public async Task ReplayedDecision_IsSingleEffect_DifferentContentConflicts()
    {
        var fixture = await SeedAsync();
        var handler = Handler(fixture);
        var request = Review(fixture, Reviewer, ImportBatchReviewState.Approved);
        Assert.Equal(200, (await handler.Handle(request, default)).StatusCode);
        Assert.Equal(200, (await handler.Handle(request, default)).StatusCode);
        Assert.Equal(409, (await handler.Handle(request with
        { Reason = "Changed rationale" }, default)).StatusCode);
        var saved = await Persisted(fixture);
        Assert.Equal(1, saved.Version);
        Assert.Single(saved.AuditTrail, x => x.Action == "BatchApproved");
        Assert.Equal("DecisionConflict", Assert.Single(await fixture.Audit.ListAsync(
            fixture.TenantId, fixture.BatchId, default)).Outcome);
    }

    [ManualDraftMongoFact]
    public async Task ConcurrentDifferentDecisions_AtMostOneCommits()
    {
        var fixture = await SeedAsync();
        var handler = Handler(fixture);
        var results = await Task.WhenAll(
            handler.Handle(Review(fixture, Reviewer, ImportBatchReviewState.Approved,
                key: "approve"), default),
            handler.Handle(Review(fixture, OtherReviewer, ImportBatchReviewState.Rejected,
                key: "reject"), default));
        Assert.Single(results, x => x.StatusCode == 200);
        Assert.Single(results, x => x.StatusCode == 409);
        var saved = await Persisted(fixture);
        Assert.Equal(1, saved.Version);
        Assert.Single(saved.AuditTrail, x =>
            x.Action is "BatchApproved" or "BatchRejected");
    }

    [ManualDraftMongoFact]
    public async Task TenantLegalEntityAndWarehouseScope_DenyWrongReadsAndDecisions()
    {
        var fixture = await SeedAsync();
        Assert.Null(await fixture.Store.GetForReviewAsync(
            Guid.NewGuid(), fixture.BatchId, default));
        Assert.Null(await fixture.Store.GetOwnAsync(
            fixture.TenantId, Reviewer, fixture.BatchId, default));
        var read = new GetReviewHistoryImportBatchHandler(fixture.Store, fixture.Audit,
            new Authority(fixture.TenantId, fixture.LegalEntityId));
        Assert.Equal(200, (await read.Handle(new(fixture.TenantId, Reviewer,
            fixture.BatchId, fixture.LegalEntityId, true), default)).StatusCode);
        Assert.Equal(404, (await read.Handle(new(Guid.NewGuid(), Reviewer,
            fixture.BatchId, fixture.LegalEntityId, true), default)).StatusCode);
        Assert.Equal(404, (await read.Handle(new(fixture.TenantId, Reviewer,
            fixture.BatchId, Guid.NewGuid(), true), default)).StatusCode);
        Assert.Equal(403, (await read.Handle(new(fixture.TenantId, Reviewer,
            fixture.BatchId, fixture.LegalEntityId, false), default)).StatusCode);
        var handler = Handler(fixture);
        Assert.Equal(404, (await handler.Handle(Review(fixture, Reviewer,
            ImportBatchReviewState.Approved, tenant: Guid.NewGuid()), default)).StatusCode);
        Assert.Equal(404, (await handler.Handle(Review(fixture, Reviewer,
            ImportBatchReviewState.Approved, legalEntity: Guid.NewGuid()), default)).StatusCode);
        var authority = new Authority(fixture.TenantId, fixture.LegalEntityId)
        { Allowed = false };
        Assert.Equal(404, (await Handler(fixture, authority).Handle(
            Review(fixture, Reviewer, ImportBatchReviewState.Approved), default)).StatusCode);
        var (_, _, _, database) = Open();
        await database.GetCollection<DemandHistoryImportBatch>(
            "mod0188_history_import_batches").UpdateOneAsync(
            x => x.TenantId == fixture.TenantId && x.Id == fixture.BatchId,
            Builders<DemandHistoryImportBatch>.Update.Set(
                x => x.WarehouseScope, new List<string> { "wh-02" }));
        Assert.Equal(404, (await read.Handle(new(fixture.TenantId, Reviewer,
            fixture.BatchId, fixture.LegalEntityId, true), default)).StatusCode);
        Assert.Equal(404, (await handler.Handle(Review(fixture, Reviewer,
            ImportBatchReviewState.Approved), default)).StatusCode);
        Assert.Equal(ImportBatchReviewState.Pending, (await Persisted(fixture)).ReviewState);
    }

    [ManualDraftMongoFact]
    public async Task MongoRejectsFailedAttemptAudit_Returns503WithoutDecision()
    {
        var fixture = await SeedAsync();
        var (_, _, _, database) = Open();
        await fixture.Audit.AppendOnceAsync(new HistoryImportReviewFailure(
            Guid.NewGuid(), fixture.LegalEntityId, fixture.BatchId, Reviewer,
            "setup", "setup", "Setup", "Setup", DateTimeOffset.UtcNow), default);
        var validator = new BsonDocument("$jsonSchema", new BsonDocument
        {
            { "bsonType", "object" },
            { "properties", new BsonDocument("Outcome",
                new BsonDocument("enum", new BsonArray { "Never" })) }
        });
        await database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            { "collMod", "mod0188_history_review_attempts" },
            { "validator", validator }
        });
        try
        {
            var result = await Handler(fixture).Handle(
                Review(fixture, Uploader, ImportBatchReviewState.Approved), default);
            Assert.Equal(503, result.StatusCode);
            var saved = await Persisted(fixture);
            Assert.Equal(ImportBatchReviewState.Pending, saved.ReviewState);
            Assert.Single(saved.AuditTrail);
            Assert.Empty(await fixture.Audit.ListAsync(
                fixture.TenantId, fixture.BatchId, default));
        }
        finally
        {
            await database.RunCommandAsync<BsonDocument>(new BsonDocument
            {
                { "collMod", "mod0188_history_review_attempts" },
                { "validator", new BsonDocument() }
            });
        }
    }
    [ManualDraftMongoFact]
    public async Task MongoRejectsEmbeddedAuditAppend_DecisionDoesNotCommit()
    {
        var fixture = await SeedAsync();
        var (_, _, _, database) = Open();
        var validator = new BsonDocument("$jsonSchema", new BsonDocument
        {
            { "bsonType", "object" },
            { "properties", new BsonDocument("AuditTrail",
                new BsonDocument("maxItems", 1)) }
        });
        await database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            { "collMod", "mod0188_history_import_batches" },
            { "validator", validator }
        });
        try
        {
            var result = await Handler(fixture).Handle(
                Review(fixture, Reviewer, ImportBatchReviewState.Approved), default);
            Assert.Equal(503, result.StatusCode);
            var saved = await Persisted(fixture);
            Assert.Equal(ImportBatchReviewState.Pending, saved.ReviewState);
            Assert.Equal(0, saved.Version);
            Assert.Single(saved.AuditTrail);
        }
        finally
        {
            await database.RunCommandAsync<BsonDocument>(new BsonDocument
            {
                { "collMod", "mod0188_history_import_batches" },
                { "validator", new BsonDocument() }
            });
        }
    }
}




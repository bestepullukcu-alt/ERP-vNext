using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.SupplyChainService.Application.Features.SupplierPerformance;
using Diten.SupplyChainService.Application.Features.SupplierPerformance.Commands;
using Diten.SupplyChainService.Application.Features.SupplierPerformance.Handlers.CommandHandlers;
using Diten.SupplyChainService.Domain.Features.SupplierPerformance;
using Diten.SupplyChainService.Persistence.Features.SupplierPerformance;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using static Diten.SupplyChainService.Tests.SupplierPerformance.SupplierPerformanceHost;
namespace Diten.SupplyChainService.Tests.SupplierPerformance;

/// <summary>MOD-0147 DL-2 submitSupplierEvaluation on the real host: §0.5 stages for submit, T-0147-RP-09, T-0147-TXN-01, T-0147-IMM-01, §21.</summary>
public sealed class SubmitSupplierEvaluationTests
{
    private static string Text(JsonNode? node) => node!.ToJsonString();
    private static async Task<string> Draft(SupplierPerformanceHost h) => (await h.Create()).Body!["evaluationId"]!.GetValue<string>();
    private static IMongoCollection<BsonDocument> Stored(SupplierPerformanceHost h) => h.Db.GetCollection<BsonDocument>(SupplierPerformanceSchema.Evaluations);

    [Fact]
    public async Task Submit_WhenTheDraftIsCurrent_ShouldPublishOneScorecardAndOneEvent()
    {
        await using var h = new SupplierPerformanceHost();
        var id = await Draft(h);
        var correlation = Guid.NewGuid().ToString();
        var body = SubmitBody(); body["comment"] = "Approved for publication";
        h.Procurement.Calls.Clear();

        var submitted = await h.Submit(id, body, correlation: correlation);

        Assert.Equal(HttpStatusCode.OK, submitted.Status);
        Assert.Equal((id, "PUBLISHED", 2, correlation, "v1"), (submitted.Body!["evaluationId"]!.GetValue<string>(), submitted.Body["status"]!.GetValue<string>(), submitted.Body["version"]!.GetValue<int>(),
            submitted.Body["correlationId"]!.GetValue<string>(), submitted.Body["contractVersion"]!.GetValue<string>()));
        var scorecardId = submitted.Body["scorecardId"]!.GetValue<string>();
        Assert.Empty(h.Procurement.Calls); // §0.0: publish does not re-read eligibility
        var evaluation = await h.Get("/evaluations/" + id);
        Assert.Equal(("PUBLISHED", 2), (evaluation.Body!["status"]!.GetValue<string>(), evaluation.Body["version"]!.GetValue<int>()));
        var scorecard = await h.Get("/scorecards/" + scorecardId);
        Assert.Equal(HttpStatusCode.OK, scorecard.Status);
        Assert.Equal((id, "85.00", "MEDIUM", correlation), (scorecard.Body!["evaluationId"]!.GetValue<string>(), scorecard.Body["overallScore"]!.GetValue<string>(), scorecard.Body["riskLevel"]!.GetValue<string>(), scorecard.Body["correlationId"]!.GetValue<string>()));
        Assert.Single((await h.Get("/scorecards")).Body!["items"]!.AsArray());
        var stored = await Stored(h).Find(new BsonDocument("_id", id)).SingleAsync();
        Assert.Equal(("Approved for publication", ScorePolicy.Revision, h.Actor.ToString(), true), (stored["SubmitComment"].AsString, stored["PolicyRevision"].AsString, stored["PublishedBy"].AsString, stored["IsActivePeriod"].AsBoolean));
        Assert.Single(await h.OutboxRows());
    }

    [Fact]
    public async Task Submit_ShouldWritePendingEventInTheContractEnvelope()
    {
        await using var h = new SupplierPerformanceHost();
        var id = await Draft(h);
        var correlation = Guid.NewGuid().ToString();

        var scorecardId = (await h.Submit(id, correlation: correlation)).Body!["scorecardId"]!.GetValue<string>();

        var row = Assert.Single(await h.OutboxRows());
        Assert.Equal(("Pending", 0, SupplierPerformanceOutboxStore.EventName), (row["Status"].AsString, row["AttemptCount"].AsInt32, row["EventName"].AsString));
        var envelope = JsonNode.Parse(row["PayloadJson"].AsString)!.AsObject();
        Assert.Equal(["contractVersion", "correlationId", "eventId", "eventType", "legalEntityId", "occurredAt", "payload", "tenantId"], envelope.Select(p => p.Key).Order(StringComparer.Ordinal));
        Assert.Equal(("SupplierEvaluationPublished", row["_id"].AsString, correlation, h.Tenant.ToString(), h.LegalEntity.ToString(), "v1"),
            (envelope["eventType"]!.GetValue<string>(), envelope["eventId"]!.GetValue<string>(), envelope["correlationId"]!.GetValue<string>(), envelope["tenantId"]!.GetValue<string>(), envelope["legalEntityId"]!.GetValue<string>(), envelope["contractVersion"]!.GetValue<string>()));
        var payload = envelope["payload"]!.AsObject();
        Assert.Equal(["evaluationId", "overallScore", "riskLevel", "scorecardId", "supplierId"], payload.Select(p => p.Key).Order(StringComparer.Ordinal));
        Assert.Equal((id, scorecardId, FakeProcurement.Code, "85.00", "MEDIUM"), (payload["evaluationId"]!.GetValue<string>(), payload["scorecardId"]!.GetValue<string>(), payload["supplierId"]!.GetValue<string>(), payload["overallScore"]!.GetValue<string>(), payload["riskLevel"]!.GetValue<string>()));
    }

    // T-0147-RP-09
    [Fact]
    public async Task RP09_SameKeyAndBody_ShouldAnswerTheOriginalWithoutASecondPublication()
    {
        await using var h = new SupplierPerformanceHost();
        var id = await Draft(h);
        var first = await h.Submit(id);
        var before = await h.Snapshot();
        var otherCorrelation = Guid.NewGuid().ToString();

        var replay = await h.Submit(id, correlation: otherCorrelation);
        var reused = await h.Submit(id, SubmitBody("2026-10-03T09:00:00Z"));

        Assert.Equal(HttpStatusCode.OK, replay.Status);
        Assert.Equal(Text(first.Body), Text(replay.Body));
        Assert.Equal(otherCorrelation, replay.Header("X-Correlation-Id"));
        Assert.Equal((HttpStatusCode.Conflict, "IDEMPOTENCY_KEY_REUSED"), (reused.Status, reused.Code));
        Assert.Equal(before, await h.Snapshot());
        Assert.Single(await h.OutboxRows());
        Assert.Equal(2, (await h.AuditRecords()).Count); // create + submit
    }

    [Fact]
    public async Task Replay_ShouldCompareSubmittedAtAsAnInstantAndIgnoreIfMatch()
    {
        await using var h = new SupplierPerformanceHost();
        var id = await Draft(h);
        var first = await h.Submit(id, SubmitBody("2026-10-02T09:00:00Z"));

        var sameInstant = await h.Submit(id, SubmitBody("2026-10-02T11:00:00.000+02:00"), ifMatch: "\"2\"");
        var otherInstant = await h.Submit(id, SubmitBody("2026-10-02T09:00:00.0000001Z"));

        Assert.Equal((HttpStatusCode.OK, Text(first.Body)), (sameInstant.Status, Text(sameInstant.Body)));
        Assert.Equal((HttpStatusCode.Conflict, "IDEMPOTENCY_KEY_REUSED"), (otherInstant.Status, otherInstant.Code));
    }

    [Fact]
    public async Task Replay_ShouldTreatAnAbsentAndANullCommentAlikeAndAnEmptyOneAsAnotherBody()
    {
        await using var h = new SupplierPerformanceHost();
        var id = await Draft(h);
        await h.Submit(id);
        var withNull = SubmitBody(); withNull["comment"] = null;
        var empty = SubmitBody(); empty["comment"] = "";

        Assert.Equal(HttpStatusCode.OK, (await h.Submit(id, withNull)).Status);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", (await h.Submit(id, empty)).Code);
    }

    [Fact]
    public async Task Permissions_ShouldMapSubmitToPublishAndNotRevealTheReceipt()
    {
        await using var h = new SupplierPerformanceHost();
        var id = await Draft(h);

        foreach (var permissions in new[] { new[] { Evaluate }, [Read], ["supplychain.loads.create"], Array.Empty<string>() })
            Assert.Equal((HttpStatusCode.Forbidden, "FORBIDDEN"), Refusal(await h.Submit(id, key: "perm", permissions: permissions)));
        Assert.Equal(1, await h.Evaluations());
        Assert.Equal(HttpStatusCode.OK, (await h.Submit(id, permissions: [Publish])).Status);
        var revoked = await h.Submit(id, permissions: [Evaluate]);
        var neverUsed = await h.Submit(id, key: "never-used", permissions: [Evaluate]);
        Assert.Equal(Refusal(neverUsed), Refusal(revoked));
        Assert.Equal((HttpStatusCode.Forbidden, "FORBIDDEN"), Refusal(revoked));
    }
    private static (HttpStatusCode, string?) Refusal(Answer a) => (a.Status, a.Code);

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"submittedAt":null}""")]
    [InlineData("""{"submittedAt":20261002}""")]
    [InlineData("""{"submittedAt":"2026-10-02"}""")]
    [InlineData("""{"submittedAt":"2026-13-02T09:00:00Z"}""")]
    [InlineData("""{"submittedAt":"2026-10-02T09:00:00"}""")]
    [InlineData("""{"submittedAt":"2026-10-02T09:00:00.00000001Z"}""")]
    [InlineData("""{"submittedAt":"2026-10-02T09:00:00Z","tenantId":"x"}""")]
    [InlineData("""{"submittedAt":"2026-10-02T09:00:00Z","comment":7}""")]
    [InlineData("""{"submittedAt":"2026-10-02T09:00:00Z","comment":"\ud800"}""")]
    [InlineData("""{"submittedAt":"2026-10-02T09:00:00Z","submittedAt":"2026-10-03T09:00:00Z"}""")]
    [InlineData("""[]""")]
    public async Task Submit_WhenTheBodyIsNotTheContractSchema_ShouldAnswer400EvenUnderAUsedKey(string body)
    {
        await using var h = new SupplierPerformanceHost();
        var id = await Draft(h);
        await h.Submit(id);
        var before = await h.Snapshot();

        var refused = await h.Submit(id, rawBody: body);

        Assert.Equal((HttpStatusCode.BadRequest, "INVALID_REQUEST"), Refusal(refused));
        Assert.Equal(before, await h.Snapshot());
    }

    [Fact]
    public async Task Create_WhenNotesHoldALoneSurrogate_ShouldAnswer400()
    {
        await using var h = new SupplierPerformanceHost();
        var body = Body(); body["notes"] = "x";
        var refused = await h.Send(HttpMethod.Post, Base + "/evaluations", rawBody: body.ToJsonString().Replace("\"x\"", "\"\\ud800\""));
        Assert.Equal((HttpStatusCode.BadRequest, "INVALID_REQUEST"), Refusal(refused));
    }

    [Fact]
    public async Task Submit_ShouldAcceptTheLongestCommentAndARfc3339LowerCaseInstant()
    {
        await using var h = new SupplierPerformanceHost();
        var id = await Draft(h);
        var longest = SubmitBody("2026-10-02t09:00:00.1234567z"); longest["comment"] = new string('x', 1000);
        var tooLong = SubmitBody(); tooLong["comment"] = new string('x', 1001);

        Assert.Equal(HttpStatusCode.BadRequest, (await h.Submit(id, tooLong)).Status);
        Assert.Equal(HttpStatusCode.OK, (await h.Submit(id, longest)).Status);
    }

    [Fact]
    public async Task Replay_WhenTheEvaluationIsAlreadyPublished_ShouldAnswer200NotALifecycleRefusal()
    {
        await using var h = new SupplierPerformanceHost();
        var id = await Draft(h);
        var first = await h.Submit(id);

        var replay = await h.Submit(id, ifMatch: "2");

        Assert.Equal((HttpStatusCode.OK, Text(first.Body)), (replay.Status, Text(replay.Body)));
    }

    // Stage 5 precedes stage 6: only a target that is gone tells them apart, the late re-read at stage 6 covers every other replay.
    [Fact]
    public async Task Replay_WhenTheEvaluationIsGone_ShouldStillAnswerTheOriginal()
    {
        await using var h = new SupplierPerformanceHost();
        var id = await Draft(h);
        var first = await h.Submit(id);
        await Stored(h).UpdateOneAsync(new BsonDocument("_id", id), new BsonDocument("$set", new BsonDocument("IsDeleted", true)));

        var replay = await h.Submit(id);

        Assert.Equal((HttpStatusCode.OK, Text(first.Body)), (replay.Status, Text(replay.Body)));
    }

    [Fact]
    public async Task Stage6_ShouldHideForeignAndDeletedTargetsBehindOne404()
    {
        await using var h = new SupplierPerformanceHost();
        var id = await Draft(h);
        var deleted = (await h.Create(Body(start: "2026-08-01", end: "2026-08-31"), "other")).Body!["evaluationId"]!.GetValue<string>();
        await Stored(h).UpdateOneAsync(new BsonDocument("_id", deleted), new BsonDocument("$set", new BsonDocument("IsDeleted", true)));

        var answers = new[]
        {
            await h.Submit(id, tenant: Guid.NewGuid()), await h.Submit(id, le: Guid.NewGuid()), await h.Submit(deleted), await h.Submit(Guid.NewGuid().ToString())
        };

        Assert.All(answers, a => Assert.Equal((HttpStatusCode.NotFound, "UNKNOWN_EVALUATION"), Refusal(a)));
        Assert.Single(answers.Select(a => a.Body!["error"]!["message"]!.GetValue<string>()).Distinct());
        Assert.Empty(await h.OutboxRows());
        Assert.Equal("DRAFT", (await h.Get("/evaluations/" + id)).Body!["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Stage5_ShouldScopeTheKeyByCompanyAndTarget()
    {
        await using var h = new SupplierPerformanceHost();
        var id = await Draft(h);
        var otherId = (await h.Create(Body(start: "2026-08-01", end: "2026-08-31"), "other")).Body!["evaluationId"]!.GetValue<string>();
        var otherCompany = Guid.NewGuid();
        var foreignId = (await h.Create(key: "other-company", le: otherCompany)).Body!["evaluationId"]!.GetValue<string>();
        await h.Submit(id);

        var sameKeyOtherTarget = await h.Submit(otherId);
        var sameKeyOtherCompany = await h.Submit(foreignId, le: otherCompany);

        Assert.Equal((HttpStatusCode.OK, otherId), (sameKeyOtherTarget.Status, sameKeyOtherTarget.Body!["evaluationId"]!.GetValue<string>()));
        Assert.Equal((HttpStatusCode.OK, foreignId), (sameKeyOtherCompany.Status, sameKeyOtherCompany.Body!["evaluationId"]!.GetValue<string>()));
        Assert.Equal(2, (await h.OutboxRows()).Count);
    }

    [Fact]
    public async Task Stage6_ShouldCheckTheVersionBeforeTheLifecycle()
    {
        await using var h = new SupplierPerformanceHost();
        var id = await Draft(h);

        var stale = await h.Submit(id, ifMatch: "7");
        Assert.Equal((HttpStatusCode.Conflict, "VERSION_CONFLICT", 1), (stale.Status, stale.Code, stale.Body!["error"]!["details"]!["currentVersion"]!.GetValue<int>()));
        await h.Submit(id);

        var publishedStale = await h.Submit(id, key: "again", ifMatch: "1");
        var publishedCurrent = await h.Submit(id, key: "again", ifMatch: "2");

        Assert.Equal((HttpStatusCode.Conflict, "VERSION_CONFLICT", 2), (publishedStale.Status, publishedStale.Code, publishedStale.Body!["error"]!["details"]!["currentVersion"]!.GetValue<int>()));
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "BUSINESS_RULE_VIOLATION"), Refusal(publishedCurrent));
        Assert.Single(await h.OutboxRows());
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("*")] [InlineData("W/\"1\"")] [InlineData("\"1\", \"2\"")] [InlineData("1,2")] [InlineData("0")] [InlineData("-1")]
    [InlineData("abc")] [InlineData("\"1")] [InlineData("\"\"")] [InlineData("1.0")] [InlineData("99999999999")]
    public async Task Stage3_WhenIfMatchIsMissingOrMalformed_ShouldAnswer400(string? ifMatch)
    {
        await using var h = new SupplierPerformanceHost();
        var id = await Draft(h);

        var refused = await h.Submit(id, ifMatch: ifMatch);

        Assert.Equal((HttpStatusCode.BadRequest, "INVALID_REQUEST"), Refusal(refused));
        Assert.Equal("DRAFT", (await h.Get("/evaluations/" + id)).Body!["status"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("1")] [InlineData("\"1\"")]
    public async Task Stage3_ShouldAcceptAVersionBareOrAsAStrongTag(string ifMatch)
    {
        await using var h = new SupplierPerformanceHost();
        Assert.Equal(HttpStatusCode.OK, (await h.Submit(await Draft(h), ifMatch: ifMatch)).Status);
    }

    [Fact]
    public async Task Stage3_ShouldNotRequireIfMatchOnCreate()
    {
        await using var h = new SupplierPerformanceHost();
        Assert.Equal(HttpStatusCode.Created, (await h.Create()).Status);
    }

    // T-0147-TXN-01
    [Theory]
    [InlineData("entity")] [InlineData("outbox")] [InlineData("receipt")] [InlineData("beforeCommit")]
    public async Task TXN01_WhenTheTransactionBreaks_ShouldLeaveNoStateReceiptAuditOrEvent(string boundary)
    {
        await using var h = new SupplierPerformanceHost();
        var id = await Draft(h);
        var before = await h.Snapshot();
        h.Probe.FailAt = boundary;

        var broken = await h.Submit(id);

        Assert.Equal((HttpStatusCode.InternalServerError, "INTERNAL_ERROR"), Refusal(broken));
        Assert.Equal(before, await h.Snapshot());
        Assert.Single(await h.AuditRecords());
        Assert.Equal(HttpStatusCode.OK, (await h.Submit(id)).Status); // the key was not consumed
        Assert.Single(await h.OutboxRows());
    }

    [Fact]
    public async Task RP08_SubmitWhoseAnswerWasLost_ShouldReadItsReceiptAndSucceed()
    {
        await using var h = new SupplierPerformanceHost();
        var id = await Draft(h);
        h.Probe.FailAt = "afterCommit";

        var submitted = await h.Submit(id);
        var replay = await h.Submit(id);

        Assert.Equal((HttpStatusCode.OK, Text(submitted.Body)), (replay.Status, Text(replay.Body)));
        Assert.Single(await h.OutboxRows());
    }

    [Fact]
    public async Task RP08_SubmitThatCannotBeProven_ShouldAnswer503AndReplayOnTheSameKey()
    {
        await using var h = new SupplierPerformanceHost();
        var id = await Draft(h);
        h.Probe.FailAt = "afterCommit"; h.Probe.FailAlsoAt = "resolve";

        var unknown = await h.Submit(id);
        var replay = await h.Submit(id);

        Assert.Equal((HttpStatusCode.ServiceUnavailable, "PERSISTENCE_UNAVAILABLE"), Refusal(unknown));
        Assert.Equal(HttpStatusCode.OK, replay.Status);
        Assert.Single(await h.OutboxRows());
    }

    // Two requests of one key share the evaluation id, so only the per-request CommitId tells whose receipt it is. The rival runs for
    // real: a request that took the rival's receipt for its own would answer a scorecard id that was never stored.
    [Fact]
    public async Task RP08_WhenTheReceiptIsAnotherRequests_ShouldNotClaimItsCommit()
    {
        await using var h = new SupplierPerformanceHost();
        var id = await Draft(h);
        Answer? rival = null;
        h.Probe.FailAt = "commit";
        h.Probe.At = async boundary =>
        {
            if (boundary != "resolve" || rival is not null) return;
            rival = await h.Submit(id);
        };

        var answer = await h.Submit(id);

        Assert.Equal((HttpStatusCode.ServiceUnavailable, "PERSISTENCE_UNAVAILABLE"), Refusal(answer));
        Assert.Equal(HttpStatusCode.OK, rival!.Status);
        Assert.Single(await h.OutboxRows());
    }

    // Review F-2: the three races of §0.5 stage 8 on the unique receipt index and the version condition.
    [Fact]
    public async Task Race_SameKeyAndBody_ShouldAnswer200ToAllAndPublishOnce()
    {
        await using var h = new SupplierPerformanceHost(rebuildIndexes: true);
        var id = await Draft(h);

        var answers = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => h.Submit(id)));

        Assert.All(answers, a => Assert.Equal(HttpStatusCode.OK, a.Status));
        Assert.Single(answers.Select(a => Text(a.Body)).Distinct());
        Assert.Single(await h.OutboxRows());
        Assert.Equal(2, (await h.AuditRecords()).Count);
    }

    [Fact]
    public async Task Race_SameKeyOtherBody_ShouldAnswerOneSuccessAndOneReuse()
    {
        await using var h = new SupplierPerformanceHost(rebuildIndexes: true);
        var id = await Draft(h);

        var answers = await Task.WhenAll(h.Submit(id), h.Submit(id, SubmitBody("2026-10-03T09:00:00Z")));

        Assert.Single(answers, a => a.Status == HttpStatusCode.OK);
        Assert.Single(answers, a => a.Code == "IDEMPOTENCY_KEY_REUSED");
        Assert.Single(await h.OutboxRows());
    }

    [Fact]
    public async Task Race_OtherKeySameVersion_ShouldAnswerOneSuccessAndOneVersionConflict()
    {
        await using var h = new SupplierPerformanceHost(rebuildIndexes: true);
        var id = await Draft(h);

        var answers = await Task.WhenAll(h.Submit(id, key: "a"), h.Submit(id, key: "b"));

        Assert.Single(answers, a => a.Status == HttpStatusCode.OK);
        Assert.Single(answers, a => a.Code == "VERSION_CONFLICT");
        Assert.Single(await h.OutboxRows());
        Assert.Equal(2, await Version(h, id));
    }
    private static async Task<int> Version(SupplierPerformanceHost h, string id) => (await h.Get("/evaluations/" + id)).Body!["version"]!.GetValue<int>();

    // T-0147-IMM-01
    [Fact]
    public async Task IMM01_APublishedEvaluation_ShouldNotChangeWhateverIsSentAfterwards()
    {
        await using var h = new SupplierPerformanceHost();
        var id = await Draft(h);
        var scorecardId = (await h.Submit(id)).Body!["scorecardId"]!.GetValue<string>();
        var before = await h.Snapshot();
        var scorecard = await h.Get("/scorecards/" + scorecardId);

        await h.Submit(id);
        await h.Submit(id, key: "new-key", ifMatch: "2");
        await h.Submit(id, key: "new-key", ifMatch: "1");
        await h.Create();
        var duplicate = await h.Create(key: "second-create");

        Assert.Equal("DUPLICATE_EVALUATION", duplicate.Code);
        Assert.Equal(before, await h.Snapshot());
        Assert.Equal(Text(scorecard.Body), Text((await h.Get("/scorecards/" + scorecardId)).Body));
    }

    [Fact]
    public async Task IMM01_AScorecard_ShouldShowItsStoredSnapshotAndNotRecompute()
    {
        await using var h = new SupplierPerformanceHost();
        var id = await Draft(h);
        var scorecardId = (await h.Submit(id)).Body!["scorecardId"]!.GetValue<string>();
        // As if an older policy revision had scored it: the policy of today must not rewrite the publication.
        await Stored(h).UpdateOneAsync(new BsonDocument("_id", id), new BsonDocument("$set", new BsonDocument { { "OverallScore", "12.34" }, { "RiskLevel", "CRITICAL" }, { "PolicyRevision", "supplier-score-policy/0" } }));

        var scorecard = await h.Get("/scorecards/" + scorecardId);

        Assert.Equal(("12.34", "CRITICAL"), (scorecard.Body!["overallScore"]!.GetValue<string>(), scorecard.Body["riskLevel"]!.GetValue<string>()));
    }

    [Fact]
    public void IMM01_TheRepository_ShouldOfferOnlyCreateAndSubmitAsWritesOfAnEvaluation()
    {
        var writes = typeof(IEvaluationRepository).GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => m.ReturnType == typeof(Task<EvaluationCommitOutcome>)).Select(m => m.Name).Order(StringComparer.Ordinal);
        Assert.Equal(["CreateAsync", "SubmitAsync"], writes);
        Assert.Equal(new[] { "FindCreateReceiptAsync", "FindSubmitReceiptAsync", "GetAsync", "GetPublishedAsync", "ListAsync", "ListPublishedAsync", "ActivePeriodExistsAsync" }.Order(StringComparer.Ordinal),
            typeof(IEvaluationRepository).GetMethods().Where(m => m.ReturnType != typeof(Task<EvaluationCommitOutcome>)).Select(m => m.Name).Order(StringComparer.Ordinal));
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(directory!.FullName, "AGENTS.md"))) directory = directory.Parent;
        var source = File.ReadAllText(Path.Combine(directory.FullName, "services", "Diten.SupplyChainService", "src", "Diten.SupplyChainService.Persistence", "Features", "SupplierPerformance", "EvaluationRepository.cs"));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(source, @"Evaluations\.\w*(Update|Replace|Delete|FindOneAnd|BulkWrite)\w*\("));
        Assert.Contains("draft.Add(\"Status\", nameof(EvaluationStatus.DRAFT))", source, StringComparison.Ordinal);
    }

    // AUD-001 path b, through the real pipeline.
    [Fact]
    public async Task Submit_ShouldWriteOneCentralAuditRecordInTheCommandTransactionAndNoneForARefusalOrReplay()
    {
        await using var h = new SupplierPerformanceHost();
        var id = await Draft(h);
        var body = SubmitBody(); body["comment"] = "private-comment-text";
        var correlation = Guid.NewGuid().ToString();

        await h.Submit(id, body, ifMatch: "9", key: "refused");
        Assert.Single(await h.AuditRecords());
        await h.Submit(id, body, correlation: correlation);
        await h.Submit(id, body);

        var record = Assert.Single(await h.AuditRecords(), r => r["Operation"].AsString == "LifecycleTransition");
        Assert.Equal(2, (await h.AuditRecords()).Count);
        Assert.Equal((h.Tenant.ToString(), h.LegalEntity.ToString(), h.Actor.ToString(), id, "supplier-performance-risk", correlation, true),
            (record["TenantId"].AsString, record["CompanyId"].AsString, record["ActorId"].AsString, record["EntityId"].AsString, record["SourceModule"].AsString, record["CorrelationId"].AsString, record["WrittenInCommandTransaction"].AsBoolean));
        Assert.DoesNotContain("private-comment-text", record.ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Errors_ShouldCarryTheSameCorrelationInHeaderAndBody()
    {
        await using var h = new SupplierPerformanceHost();
        var id = await Draft(h);
        var correlation = Guid.NewGuid().ToString();

        var stale = await h.Submit(id, ifMatch: "7", correlation: correlation);

        Assert.Equal(correlation, stale.Header("X-Correlation-Id"));
        Assert.Equal(correlation, stale.Body!["error"]!["correlationId"]!.GetValue<string>());
    }

    // A late replay must never surface as a refusal: stage 5 can miss a receipt that stage 6 already sees the effect of.
    private sealed class LateReceiptRepository(SupplierEvaluation published, EvaluationReceipt? receipt) : IEvaluationRepository
    {
        public Task<EvaluationReceipt?> FindSubmitReceiptAsync(SupplierPerformanceScope scope, Guid evaluationId, string key, CancellationToken ct, bool afterClash = false) => Task.FromResult(afterClash ? receipt : null);
        public Task<SupplierEvaluation?> GetAsync(SupplierPerformanceScope scope, Guid evaluationId, CancellationToken ct) => Task.FromResult<SupplierEvaluation?>(published);
        public Task<EvaluationCommitOutcome> SubmitAsync(SupplierPerformanceScope scope, string key, string fingerprint, EvaluationSubmission submission, string responseBody, CancellationToken ct) => throw new NotSupportedException();
        public Task<EvaluationReceipt?> FindCreateReceiptAsync(SupplierPerformanceScope scope, string key, CancellationToken ct, bool afterClash = false) => throw new NotSupportedException();
        public Task<bool> ActivePeriodExistsAsync(SupplierPerformanceScope scope, string supplierId, string periodStart, string periodEnd, CancellationToken ct) => throw new NotSupportedException();
        public Task<EvaluationCommitOutcome> CreateAsync(SupplierPerformanceScope scope, string key, string fingerprint, SupplierEvaluation evaluation, string responseBody, CancellationToken ct) => throw new NotSupportedException();
        public Task<(IReadOnlyList<SupplierEvaluation> Items, long Total)> ListAsync(SupplierPerformanceScope scope, EvaluationFilter filter, int page, int pageSize, CancellationToken ct) => throw new NotSupportedException();
        public Task<SupplierEvaluation?> GetPublishedAsync(SupplierPerformanceScope scope, Guid scorecardId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<SupplierEvaluation>> ListPublishedAsync(SupplierPerformanceScope scope, ScorecardFilter filter, CancellationToken ct) => throw new NotSupportedException();
    }

    private static async Task<Diten.SupplyChainService.Application.Common.Response<EvaluationSubmissionResult>> HandleLate(int expectedVersion, EvaluationReceipt? receipt)
    {
        var context = new SupplierPerformanceRequestContext { Scope = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CorrelationId = Guid.NewGuid(), IdempotencyKey = "k", ExpectedVersion = expectedVersion };
        var published = new SupplierEvaluation { Id = Guid.NewGuid(), Status = EvaluationStatus.PUBLISHED, Version = 2 };
        var handler = new SubmitSupplierEvaluationHandler(new LateReceiptRepository(published, receipt), context, TimeProvider.System);
        using var body = JsonDocument.Parse("""{"submittedAt":"2026-10-02T09:00:00Z"}""");
        return await handler.Handle(new SubmitSupplierEvaluationCommand(published.Id, body.RootElement.Clone()), CancellationToken.None);
    }
    private const string SameBodyFingerprint = """v1:{"submittedAt":"2026-10-02T09:00:00.0000000Z","comment":null}""";
    private const string Original = """{"evaluationId":"e","scorecardId":"s","status":"PUBLISHED","version":2,"correlationId":"4ea6d5f2-5ffc-4ad2-93d4-5fc6bf696fc5","contractVersion":"v1"}""";

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task Stage6_WhenTheWinnersReceiptAppearsLate_ShouldReplayNotRefuse(int expectedVersion)
    {
        var answer = await HandleLate(expectedVersion, new(SameBodyFingerprint, 200, Original));

        Assert.Equal((200, "s"), (answer.StatusCode, answer.Data!.ScorecardId));
    }

    [Theory]
    [InlineData(1, 409, "VERSION_CONFLICT")] [InlineData(2, 422, "BUSINESS_RULE_VIOLATION")]
    public async Task Stage6_WhenThereIsNoReceipt_ShouldRefuse(int expectedVersion, int status, string code)
    {
        var answer = await HandleLate(expectedVersion, null);

        Assert.Equal((status, code), (answer.StatusCode, answer.ErrorCode));
    }
}

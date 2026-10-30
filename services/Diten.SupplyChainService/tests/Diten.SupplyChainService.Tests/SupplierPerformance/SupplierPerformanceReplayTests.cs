using System.Net;
using System.Text.Json.Nodes;
using Diten.SupplyChainService.Domain.Features.SupplierPerformance;
using Diten.SupplyChainService.Persistence.Features.SupplierPerformance;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using static Diten.SupplyChainService.Tests.SupplierPerformance.SupplierPerformanceHost;
namespace Diten.SupplyChainService.Tests.SupplierPerformance;

/// <summary>MOD-0147 §0.5 replay receipt for createSupplierEvaluation: T-0147-RP-01…08 and 10…16 (RP-09 is submit, DL-2).</summary>
public sealed class SupplierPerformanceReplayTests
{
    private static string Text(JsonNode? node) => node!.ToJsonString();

    // T-0147-RP-01
    [Fact]
    public async Task RP01_SameKeyAndBody_ShouldAnswer201Then200WithTheSameBodyAndWriteOnce()
    {
        await using var h = new SupplierPerformanceHost();

        var first = await h.Create();
        var second = await h.Create();

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.OK), (first.Status, second.Status));
        Assert.Equal(Text(first.Body), Text(second.Body));
        Assert.Equal((1, 1), (await h.Evaluations(), await h.Receipts()));
        Assert.Single(await h.AuditRecords());
        Assert.DoesNotContain((await (await h.Db.ListCollectionNamesAsync()).ToListAsync()), n => n.StartsWith("supplier_performance_outbox", StringComparison.Ordinal));
        Assert.Single(h.Procurement.Calls);
    }

    // T-0147-RP-02
    [Fact]
    public async Task RP02_SameKeyAnotherValidBody_ShouldAnswer409AndChangeNothing()
    {
        await using var h = new SupplierPerformanceHost();
        await h.Create();
        var before = await h.Snapshot();

        foreach (var other in new[] { Body(start: "2026-08-01", end: "2026-08-31"), Body(metrics: new JsonArray(Metric("OTD", "90"), Metric("QTY_ACC", "80"), Metric("QUAL_ACC", "100"))),
                     Body(metrics: new JsonArray(Metric("QTY_ACC", "80"), Metric("OTD", "90"), Metric("QUAL_ACC", "100"), Metric("INV_MATCH", "70"))),
                     Body(metrics: new JsonArray(Metric("OTD", "90.0"), Metric("QTY_ACC", "80"), Metric("QUAL_ACC", "100"), Metric("INV_MATCH", "70"))),
                     Body(FakeProcurement.MasterId) })
        {
            var reused = await h.Create(other);
            Assert.Equal((HttpStatusCode.Conflict, "IDEMPOTENCY_KEY_REUSED"), (reused.Status, reused.Code));
        }
        Assert.Equal(before, await h.Snapshot());
        Assert.Single(await h.AuditRecords());
        Assert.Single(h.Procurement.Calls);
    }

    // T-0147-RP-03
    [Fact]
    public async Task RP03_ReplayWhileProcurementIsDown_ShouldAnswerTheOriginalWithoutCallingProcurement()
    {
        await using var h = new SupplierPerformanceHost();
        var first = await h.Create();
        h.Procurement.Throw = new HttpRequestException("down");
        h.Procurement.Answer = _ => (HttpStatusCode.NotFound, FakeProcurement.Error("UNKNOWN_SUPPLIER"));

        var replay = await h.Create();

        Assert.Equal(HttpStatusCode.OK, replay.Status);
        Assert.Equal(Text(first.Body), Text(replay.Body));
        Assert.Single(h.Procurement.Calls);
    }

    // T-0147-RP-04
    [Fact]
    public async Task RP04_PermissionRevoked_ShouldAnswer403WithoutRevealingTheReceipt()
    {
        await using var h = new SupplierPerformanceHost();
        await h.Create();

        var revoked = await h.Create(permissions: [Read]);
        var neverUsed = await h.Create(key: "never-used", permissions: [Read]);

        Assert.Equal((HttpStatusCode.Forbidden, "FORBIDDEN"), (revoked.Status, revoked.Code));
        Assert.Equal(neverUsed.Body!["error"]!["message"]!.GetValue<string>(), revoked.Body!["error"]!["message"]!.GetValue<string>());
        Assert.Null(revoked.Body["evaluationId"]);
    }

    // T-0147-RP-05
    [Fact]
    public async Task RP05_OtherCompanyOrTenantWithTheSameKeyAndBody_ShouldBeANewRequestInItsOwnScope()
    {
        await using var h = new SupplierPerformanceHost();
        var first = await h.Create();
        var otherLe = Guid.NewGuid(); var otherTenant = Guid.NewGuid();

        var company = await h.Create(le: otherLe);
        var tenant = await h.Create(tenant: otherTenant);

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.Created), (company.Status, tenant.Status));
        Assert.NotEqual(first.Body!["evaluationId"]!.GetValue<string>(), company.Body!["evaluationId"]!.GetValue<string>());
        Assert.Equal((1, 1, 1), (await h.Evaluations(), await h.Evaluations(le: otherLe), await h.Evaluations(tenant: otherTenant)));
        Assert.Equal(3, h.Procurement.Calls.Count);
    }

    // T-0147-RP-06: a failed request writes no receipt and does not consume the key.
    [Fact]
    public async Task RP06_RequestAnswered422Or503_ShouldLeaveTheKeyFreeForTheCorrectedRequest()
    {
        await using var h = new SupplierPerformanceHost();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await h.Create(Body(metrics: new JsonArray(Metric("OTD", "90"))))).Status);
        h.Procurement.Answer = _ => (HttpStatusCode.ServiceUnavailable, null);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await h.Create()).Status);
        Assert.Equal(0, await h.Receipts());
        h.Procurement.Answer = _ => (HttpStatusCode.OK, FakeProcurement.Eligibility().ToJsonString());

        Assert.Equal(HttpStatusCode.Created, (await h.Create()).Status);
        Assert.Equal((1, 1), (await h.Evaluations(), await h.Receipts()));
    }

    // T-0147-RP-07: N identical concurrent creates released by a barrier → one 201, the rest 200, never 409.
    [Fact]
    public async Task RP07_IdenticalConcurrentCreates_ShouldAnswerOne201AndTheRest200()
    {
        await using var h = new SupplierPerformanceHost(ownDatabase: true);
        h.Procurement.Delay = TimeSpan.FromMilliseconds(50);
        // Four rounds (one month each): the clash window is a race, so one round alone can miss it.
        foreach (var month in new[] { "07", "08", "09", "10" })
        {
            var body = Body(start: $"2026-{month}-01", end: $"2026-{month}-{(month is "09" ? "30" : "31")}");
            using var barrier = new Barrier(8);

            var answers = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(async () => { barrier.SignalAndWait(); return await h.Create(body, "race-" + month); })));

            var seen = string.Join(", ", answers.Select(a => (int)a.Status + " " + a.Code));
            Assert.True(answers.Count(a => a.Status == HttpStatusCode.Created) == 1 && answers.Count(a => a.Status == HttpStatusCode.OK) == 7, month + ": " + seen);
            Assert.Single(answers.Select(a => a.Body!["evaluationId"]!.GetValue<string>()).Distinct());
        }
        Assert.Equal((4, 4), (await h.Evaluations(), await h.Receipts()));
        Assert.Equal(4, (await h.AuditRecords()).Count);
    }

    // T-0147-RP-07, deterministic: an identical request commits between this request's stage 5 and its active-period check.
    // §0.5: a clash re-runs stage 5 first, so the late request is a replay (200), never 409 DUPLICATE_EVALUATION.
    [Fact]
    public async Task RP07_IdenticalRequestCommittedAfterStage5_ShouldStillBeAReplay()
    {
        await using var h = new SupplierPerformanceHost(ownDatabase: true);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Procurement.Gate = n => n == 1 ? release.Task : Task.CompletedTask;

        var late = Task.Run(() => h.Create());
        while (h.Procurement.Calls.IsEmpty) await Task.Delay(10);
        var winner = await h.Create();
        release.SetResult();
        var replay = await late;

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.OK), (winner.Status, replay.Status));
        Assert.Equal(Text(winner.Body), Text(replay.Body));
        Assert.Equal((1, 1), (await h.Evaluations(), await h.Receipts()));
    }

    // T-0147-RP-08: the answer lost after commit → replay; commit outcome unknown → 503, then replay if committed, else one new 201.
    [Theory]
    [InlineData("afterCommit", HttpStatusCode.OK)]
    [InlineData("commit", HttpStatusCode.Created)]
    public async Task RP08_UnknownCommitOutcome_ShouldAnswer503AndLetTheSameKeyResolveIt(string boundary, HttpStatusCode retry)
    {
        await using var h = new SupplierPerformanceHost();
        h.Probe.FailAt = boundary;

        var unknown = await h.Create();
        var again = await h.Create();

        Assert.Equal((HttpStatusCode.ServiceUnavailable, "PERSISTENCE_UNAVAILABLE"), (unknown.Status, unknown.Code));
        Assert.Equal(retry, again.Status);
        Assert.Equal((1, 1), (await h.Evaluations(), await h.Receipts()));
        Assert.Equal(boundary == "commit" ? 2 : 1, h.Procurement.Calls.Count);
    }

    // T-0147-RP-10: one key, two bodies, concurrent → one 201 and one 409 IDEMPOTENCY_KEY_REUSED; Procurement is not called after the clash.
    [Fact]
    public async Task RP10_SameKeyDifferentBodiesConcurrently_ShouldAnswerOne201AndOne409()
    {
        await using var h = new SupplierPerformanceHost(ownDatabase: true);
        h.Procurement.Delay = TimeSpan.FromMilliseconds(50);
        using var barrier = new Barrier(2);
        var bodies = new[] { Body(), Body(start: "2026-08-01", end: "2026-08-31") };

        var answers = await Task.WhenAll(bodies.Select(b => Task.Run(async () => { barrier.SignalAndWait(); return await h.Create(b, "contested"); })));

        Assert.Single(answers, a => a.Status == HttpStatusCode.Created);
        Assert.Single(answers, a => a.Status == HttpStatusCode.Conflict && a.Code == "IDEMPOTENCY_KEY_REUSED");
        Assert.True(h.Procurement.Calls.Count <= 2);
        Assert.Equal((1, 1), (await h.Evaluations(), await h.Receipts()));
    }

    // T-0147-RP-11: different keys, same supplier + period, concurrent → one 201 and one 409 DUPLICATE_EVALUATION.
    [Fact]
    public async Task RP11_DifferentKeysSamePeriodConcurrently_ShouldAnswerOne201AndOne409Duplicate()
    {
        await using var h = new SupplierPerformanceHost(ownDatabase: true);
        h.Procurement.Delay = TimeSpan.FromMilliseconds(50);
        using var barrier = new Barrier(2);

        var answers = await Task.WhenAll(new[] { "k-a", "k-b" }.Select(k => Task.Run(async () => { barrier.SignalAndWait(); return await h.Create(key: k); })));

        Assert.Single(answers, a => a.Status == HttpStatusCode.Created);
        Assert.Single(answers, a => a.Status == HttpStatusCode.Conflict && a.Code == "DUPLICATE_EVALUATION");
        Assert.Equal((1, 1), (await h.Evaluations(), await h.Receipts()));
    }

    // T-0147-RP-12: 1..200 code points as sent; only White_Space → 400.
    [Theory]
    [InlineData("   ", false)] [InlineData("\t", false)] [InlineData("　", false)] [InlineData(" 　\t", false)]
    [InlineData("k", true)] [InlineData(" k ", true)]
    public async Task RP12_KeyRules_ShouldRefuseOnlyWhitespaceKeys(string key, bool accepted)
    {
        await using var h = new SupplierPerformanceHost();

        var answer = await h.Create(key: key);

        Assert.Equal(accepted ? HttpStatusCode.Created : HttpStatusCode.BadRequest, answer.Status);
        if (!accepted) Assert.Equal("INVALID_REQUEST", answer.Code);
    }

    [Fact]
    public async Task RP12_KeyLength_ShouldCountCodePointsUpTo200()
    {
        await using var h = new SupplierPerformanceHost();
        var max = string.Concat(Enumerable.Repeat("😀", 200));

        Assert.Equal(HttpStatusCode.Created, (await h.Create(key: max)).Status);
        Assert.Equal(HttpStatusCode.OK, (await h.Create(key: max)).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await h.Create(Body(start: "2026-08-01", end: "2026-08-31"), key: max + "x")).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await h.Create(Body(start: "2026-08-01", end: "2026-08-31"), key: "")).Status);
        // " k " and "k" are different keys: no trim — the second is a new request, not a reuse of the first.
        Assert.Equal(HttpStatusCode.Created, (await h.Create(Body(start: "2026-09-01", end: "2026-09-30"), key: "k")).Status);
        Assert.Equal(HttpStatusCode.Created, (await h.Create(Body(start: "2026-10-01", end: "2026-10-31"), key: " k ")).Status);
        Assert.Equal(HttpStatusCode.Created, (await h.Create(Body(start: "2026-08-01", end: "2026-08-31"), key: " " + max[..2])).Status);
    }

    // T-0147-RP-13
    [Fact]
    public async Task RP13_ReplayWithAnotherCorrelation_ShouldKeepTheOriginalBodyAndEchoTheCurrentHeader()
    {
        await using var h = new SupplierPerformanceHost();
        var first = await h.Create();
        var current = Guid.NewGuid().ToString();

        var replay = await h.Create(correlation: current);

        Assert.Equal(HttpStatusCode.OK, replay.Status);
        Assert.Equal(h.Correlation, replay.Body!["correlationId"]!.GetValue<string>());
        Assert.Equal(current, replay.Header("X-Correlation-Id"));
        Assert.Equal(Text(first.Body), Text(replay.Body));
    }

    // T-0147-RP-14: a create replay after the evaluation moved on returns the original DRAFT snapshot.
    [Fact]
    public async Task RP14_ReplayAfterTheEvaluationWasSubmitted_ShouldAnswerTheOriginalDraft()
    {
        await using var h = new SupplierPerformanceHost();
        var first = await h.Create();
        var id = first.Body!["evaluationId"]!.GetValue<string>();
        await h.Db.GetCollection<SupplierEvaluation>(SupplierPerformanceSchema.Evaluations).UpdateOneAsync(new BsonDocument("_id", id),
            Builders<SupplierEvaluation>.Update.Set(x => x.Status, EvaluationStatus.PUBLISHED).Set(x => x.Version, 2).Set(x => x.ScorecardId, Guid.NewGuid()).Set(x => x.PublishedAt, DateTimeOffset.UtcNow));

        var replay = await h.Create();

        Assert.Equal(HttpStatusCode.OK, replay.Status);
        Assert.Equal(("DRAFT", 1), (replay.Body!["status"]!.GetValue<string>(), replay.Body["version"]!.GetValue<int>()));
        Assert.Equal(Text(first.Body), Text(replay.Body));
        Assert.Equal("PUBLISHED", (await h.Get("/evaluations/" + id)).Body!["status"]!.GetValue<string>());
    }

    // T-0147-RP-15: notes absent and null replay each other; "" is a different body.
    [Fact]
    public async Task RP15_Notes_ShouldTreatAbsentAndNullAsEqualAndEmptyAsDifferent()
    {
        await using var h = new SupplierPerformanceHost();
        var withNull = Body(); withNull["notes"] = null;
        var empty = Body(); empty["notes"] = "";

        Assert.Equal(HttpStatusCode.Created, (await h.Create(Body())).Status);
        Assert.Equal(HttpStatusCode.OK, (await h.Create(withNull)).Status);
        Assert.Equal((HttpStatusCode.Conflict, "IDEMPOTENCY_KEY_REUSED"), ((await h.Create(empty)).Status, (await h.Create(empty)).Code));
        var other = Body(start: "2026-08-01", end: "2026-08-31"); other["notes"] = "";
        Assert.Equal(HttpStatusCode.Created, (await h.Create(other, "notes-empty")).Status);
        var otherNull = Body(start: "2026-08-01", end: "2026-08-31"); otherNull["notes"] = null;
        Assert.Equal(HttpStatusCode.Conflict, (await h.Create(otherNull, "notes-empty")).Status);
    }

    // T-0147-RP-16: an invalid body under a used key is 400 and the receipt is unchanged.
    [Fact]
    public async Task RP16_InvalidBodyUnderAUsedKey_ShouldAnswer400AndLeaveTheReceipt()
    {
        await using var h = new SupplierPerformanceHost();
        await h.Create();
        var before = await h.Snapshot();
        var invalid = Body(); invalid.Remove("metrics");

        var answer = await h.Create(invalid);

        Assert.Equal((HttpStatusCode.BadRequest, "INVALID_REQUEST"), (answer.Status, answer.Code));
        Assert.Equal(before, await h.Snapshot());
        Assert.Equal(HttpStatusCode.OK, (await h.Create()).Status);
    }
}

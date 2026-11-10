using System.Net;
using System.Text.Json.Nodes;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Domain.Features.SupplierPerformance;
using Diten.SupplyChainService.Persistence.Features.SupplierPerformance;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using static Diten.SupplyChainService.Tests.SupplierPerformance.SupplierPerformanceHost;
namespace Diten.SupplyChainService.Tests.SupplierPerformance;

/// <summary>
/// MOD-0147 DL-1 through the real host: the create contract and policy (SCORE / MISS / CODE / PER at the wire), the §14 permission
/// mapping (T-0147-V1-PERM-01), the §0.5 stage precedence of the context gate, the D2 eligibility table (T-0147-V1-ELIG-01),
/// Tenant + LE isolation of the four reads (T-0147-ISO-01) and the central-audit record (AUD-001).
/// </summary>
public sealed class SupplierPerformanceHttpTests
{
    private static JsonArray Metrics(params (string Code, string Value)[] metrics) => new(metrics.Select(m => (JsonNode)Metric(m.Code, m.Value)).ToArray());

    [Fact]
    public async Task Create_WhenTheRequestIsValid_ShouldAnswer201WithTheDraftEvaluationOfTheFrozenContract()
    {
        await using var h = new SupplierPerformanceHost();

        var created = await h.Create();

        Assert.Equal(HttpStatusCode.Created, created.Status);
        var b = created.Body!;
        Assert.True(Guid.TryParseExact(b["evaluationId"]!.GetValue<string>(), "D", out _));
        Assert.Equal(("SUP-2001", "2026-07-01", "2026-07-31", "DRAFT", "85.00", "MEDIUM", 1, "v1"),
            (b["supplierId"]!.GetValue<string>(), b["periodStart"]!.GetValue<string>(), b["periodEnd"]!.GetValue<string>(), b["status"]!.GetValue<string>(),
             b["overallScore"]!.GetValue<string>(), b["riskLevel"]!.GetValue<string>(), b["version"]!.GetValue<int>(), b["contractVersion"]!.GetValue<string>()));
        Assert.Equal(h.Correlation, b["correlationId"]!.GetValue<string>());
        Assert.Equal(h.Correlation, created.Header("X-Correlation-Id"));
        Assert.Equal(["OTD", "QTY_ACC", "QUAL_ACC", "INV_MATCH"], b["metrics"]!.AsArray().Select(m => m!["metricCode"]!.GetValue<string>()));
        Assert.All(b["metrics"]!.AsArray(), m => Assert.Equal("25", m!["weight"]!.GetValue<string>()));
        var stored = await h.Db.GetCollection<SupplierEvaluation>(SupplierPerformanceSchema.Evaluations).Find(new BsonDocument("_id", b["evaluationId"]!.GetValue<string>())).SingleAsync();
        Assert.Equal((h.Tenant, h.LegalEntity, h.Actor, ScorePolicy.Revision, true), (stored.TenantId, stored.LegalEntityId, stored.CreatedBy, stored.PolicyRevision, stored.IsActivePeriod));
    }

    // T-0147-V1-SCORE-01 / MISS-01 / BAND-01 at the wire.
    [Fact]
    public async Task Create_WhenAMetricIsAbsent_ShouldScoreTheSentMetricsAndShowTheAbsence()
    {
        await using var h = new SupplierPerformanceHost();

        var created = await h.Create(Body(metrics: Metrics(("OTD", "90"), ("QTY_ACC", "80"), ("INV_MATCH", "71"))));

        Assert.Equal(HttpStatusCode.Created, created.Status);
        Assert.Equal(("80.33", "MEDIUM"), (created.Body!["overallScore"]!.GetValue<string>(), created.Body["riskLevel"]!.GetValue<string>()));
        Assert.DoesNotContain(created.Body["metrics"]!.AsArray(), m => m!["metricCode"]!.GetValue<string>() == "QUAL_ACC");
    }

    // T-0147-V1-MISS-01, CODE-01, PER-01: a policy refusal is 422 and writes nothing — not a draft, a receipt or an audit record.
    public static TheoryData<string, JsonObject> PolicyRefusals => new()
    {
        { "two metrics", Body(metrics: Metrics(("OTD", "90"), ("QTY_ACC", "80"))) },
        { "contract example codes", Body(metrics: new JsonArray(Metric("ON_TIME_DELIVERY", "94.00", "60.00"), Metric("QUALITY_ACCEPTANCE", "98.00", "40.00"))) },
        { "fifth metric", Body(metrics: Metrics(("OTD", "90"), ("QTY_ACC", "80"), ("QUAL_ACC", "70"), ("INV_MATCH", "60"), ("OTD", "50"))) },
        { "duplicate code", Body(metrics: Metrics(("OTD", "90"), ("OTD", "80"), ("QUAL_ACC", "70"))) },
        { "weight 33.3333", Body(metrics: new JsonArray(Metric("OTD", "90", "33.3333"), Metric("QTY_ACC", "80", "33.3333"), Metric("QUAL_ACC", "70", "33.3334"))) },
        { "weight 40", Body(metrics: new JsonArray(Metric("OTD", "90", "40"), Metric("QTY_ACC", "80"), Metric("QUAL_ACC", "70"))) },
        { "negative value", Body(metrics: Metrics(("OTD", "-1"), ("QTY_ACC", "80"), ("QUAL_ACC", "70"))) },
        { "value above 100", Body(metrics: Metrics(("OTD", "100.0001"), ("QTY_ACC", "80"), ("QUAL_ACC", "70"))) },
        { "five fractional digits", Body(metrics: Metrics(("OTD", "50.12345"), ("QTY_ACC", "80"), ("QUAL_ACC", "70"))) },
        { "quarter", Body(start: "2026-07-01", end: "2026-09-30") },
        { "not from the first", Body(start: "2026-07-02", end: "2026-07-31") },
    };

    [Theory, MemberData(nameof(PolicyRefusals))]
    public async Task Create_WhenAV1PolicyRuleIsBroken_ShouldAnswer422AndWriteNothing(string because, JsonObject body)
    {
        await using var h = new SupplierPerformanceHost();

        var refused = await h.Create(body);

        Assert.True(HttpStatusCode.UnprocessableEntity == refused.Status, because);
        Assert.Equal("BUSINESS_RULE_VIOLATION", refused.Code);
        Assert.Equal(h.Correlation, refused.Body!["error"]!["correlationId"]!.GetValue<string>());
        Assert.Equal(0, await h.Evaluations()); Assert.Equal(0, await h.Receipts()); Assert.Empty(await h.AuditRecords());
    }

    // Stage 4: a body that is not the CreateEvaluationRequest schema is 400, before any receipt or Procurement call.
    public static TheoryData<string, string> SchemaRefusals => new()
    {
        { "no supplierId", "{\"periodStart\":\"2026-07-01\",\"periodEnd\":\"2026-07-31\",\"metrics\":[{\"metricCode\":\"OTD\",\"measuredValue\":\"1\",\"weight\":\"25\"}]}" },
        { "no metrics", "{\"supplierId\":\"SUP-2001\",\"periodStart\":\"2026-07-01\",\"periodEnd\":\"2026-07-31\"}" },
        { "empty metrics", "{\"supplierId\":\"SUP-2001\",\"periodStart\":\"2026-07-01\",\"periodEnd\":\"2026-07-31\",\"metrics\":[]}" },
        { "number value", "{\"supplierId\":\"SUP-2001\",\"periodStart\":\"2026-07-01\",\"periodEnd\":\"2026-07-31\",\"metrics\":[{\"metricCode\":\"OTD\",\"measuredValue\":90,\"weight\":\"25\"}]}" },
        { "decimal pattern", "{\"supplierId\":\"SUP-2001\",\"periodStart\":\"2026-07-01\",\"periodEnd\":\"2026-07-31\",\"metrics\":[{\"metricCode\":\"OTD\",\"measuredValue\":\"9e1\",\"weight\":\"25\"}]}" },
        { "bad date", "{\"supplierId\":\"SUP-2001\",\"periodStart\":\"2026-02-30\",\"periodEnd\":\"2026-07-31\",\"metrics\":[{\"metricCode\":\"OTD\",\"measuredValue\":\"1\",\"weight\":\"25\"}]}" },
        { "tenant in body", "{\"tenantId\":\"x\",\"supplierId\":\"SUP-2001\",\"periodStart\":\"2026-07-01\",\"periodEnd\":\"2026-07-31\",\"metrics\":[{\"metricCode\":\"OTD\",\"measuredValue\":\"1\",\"weight\":\"25\"}]}" },
        { "duplicate member", "{\"supplierId\":\"SUP-2001\",\"supplierId\":\"SUP-2002\",\"periodStart\":\"2026-07-01\",\"periodEnd\":\"2026-07-31\",\"metrics\":[{\"metricCode\":\"OTD\",\"measuredValue\":\"1\",\"weight\":\"25\"}]}" },
        { "notes too long", "{\"supplierId\":\"SUP-2001\",\"periodStart\":\"2026-07-01\",\"periodEnd\":\"2026-07-31\",\"metrics\":[{\"metricCode\":\"OTD\",\"measuredValue\":\"1\",\"weight\":\"25\"}],\"notes\":\"" + new string('n', 2001) + "\"}" },
        { "malformed json", "{\"supplierId\":" },
        { "array", "[]" },
    };

    [Theory, MemberData(nameof(SchemaRefusals))]
    public async Task Create_WhenTheBodyIsNotTheContractSchema_ShouldAnswer400BeforeProcurement(string because, string raw)
    {
        await using var h = new SupplierPerformanceHost();

        var refused = await h.Send(HttpMethod.Post, Base + "/evaluations", rawBody: raw);

        Assert.True(HttpStatusCode.BadRequest == refused.Status, because);
        Assert.Equal("INVALID_REQUEST", refused.Code);
        Assert.Equal("v1", refused.Body!["contractVersion"]!.GetValue<string>());
        Assert.Empty(h.Procurement.Calls);
        Assert.Equal(0, await h.Evaluations());
    }

    [Fact]
    public async Task Create_WhenNotesAre2000CodePoints_ShouldAccept()
    {
        await using var h = new SupplierPerformanceHost();
        var body = Body(); body["notes"] = string.Concat(Enumerable.Repeat("😀", 2000));

        Assert.Equal(HttpStatusCode.Created, (await h.Create(body)).Status);
    }

    // T-0147-V1-PERM-01: create → .evaluate; the four GETs → .read; .publish alone opens nothing in DL-1.
    [Fact]
    public async Task Permissions_ShouldMapCreateToEvaluateAndTheFourReadsToRead()
    {
        await using var h = new SupplierPerformanceHost();
        var created = await h.Create(permissions: [Evaluate]);
        Assert.Equal(HttpStatusCode.Created, created.Status);
        var id = created.Body!["evaluationId"]!.GetValue<string>();
        var reads = new[] { "/evaluations", "/evaluations/" + id, "/scorecards", "/scorecards/" + Guid.NewGuid() };

        foreach (var permissions in new[] { new[] { Read }, ["supplychain.supplier-performance.publish"], ["supplychain.loads.create"], Array.Empty<string>() })
        {
            var refused = await h.Create(Body(start: "2026-08-01", end: "2026-08-31"), "perm-" + string.Join("", permissions), permissions: permissions);
            Assert.Equal((HttpStatusCode.Forbidden, "FORBIDDEN"), (refused.Status, refused.Code));
        }
        foreach (var path in reads)
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await h.Get(path, permissions: [Evaluate])).Status);
            Assert.NotEqual(HttpStatusCode.Forbidden, (await h.Get(path, permissions: [Read])).Status);
        }
        Assert.Equal(1, await h.Evaluations());
        Assert.Single(h.Procurement.Calls);
    }

    // §0.5 stages 1 → 2 → 3 → 3a → 3b, first failure wins.
    [Fact]
    public async Task Gate_ShouldApplyTheStagesInOrder()
    {
        await using var h = new SupplierPerformanceHost();

        var anonymous = await h.Send(HttpMethod.Post, Base + "/evaluations", Body(), edit: r => r.Headers.Remove("Authorization"));
        Assert.Equal((HttpStatusCode.Unauthorized, "UNAUTHENTICATED", "Bearer"), (anonymous.Status, anonymous.Code, anonymous.Header("WWW-Authenticate")));
        // 2 before 3: no permission and a broken header → 403.
        var forbidden = await h.Send(HttpMethod.Post, Base + "/evaluations", Body(), key: null, permissions: [Read]);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.Status);
        // 3: no key, a correlation that is not a UUID (the answer carries one generated UUID in header and body).
        Assert.Equal(HttpStatusCode.BadRequest, (await h.Create(key: null)).Status);
        var badCorrelation = await h.Create(correlation: "not-a-uuid");
        Assert.Equal(HttpStatusCode.BadRequest, badCorrelation.Status);
        Assert.Equal(badCorrelation.Header("X-Correlation-Id"), badCorrelation.Body!["error"]!["correlationId"]!.GetValue<string>());
        Assert.True(Guid.TryParse(badCorrelation.Header("X-Correlation-Id"), out _));
        Assert.Equal(HttpStatusCode.BadRequest, (await h.Send(HttpMethod.Post, Base + "/evaluations", Body(), edit: r => r.Headers.Remove("X-Legal-Entity-Id"))).Status);
        // 3a: header tenant ≠ signed tenant → the operation's non-disclosing 404, MDM not asked.
        var calls = h.LegalEntities.Calls.Count;
        foreach (var (method, path, code) in new[] { (HttpMethod.Post, "/evaluations", "UNKNOWN_SUPPLIER"), (HttpMethod.Get, "/evaluations", "NOT_FOUND"),
            (HttpMethod.Get, "/evaluations/" + Guid.NewGuid(), "UNKNOWN_EVALUATION"), (HttpMethod.Get, "/scorecards", "NOT_FOUND"), (HttpMethod.Get, "/scorecards/" + Guid.NewGuid(), "UNKNOWN_SCORECARD") })
        {
            var answer = await h.Send(method, Base + path, method == HttpMethod.Post ? Body() : null, headerTenant: Guid.NewGuid());
            Assert.Equal((HttpStatusCode.NotFound, code), (answer.Status, answer.Code));
        }
        Assert.Equal(calls, h.LegalEntities.Calls.Count);
        // 4: path, scope query and media type.
        Assert.Equal(HttpStatusCode.BadRequest, (await h.Get("/evaluations/not-a-uuid")).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await h.Get("/evaluations?tenantId=" + h.Tenant)).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await h.Send(HttpMethod.Post, Base + "/evaluations", Body(), contentType: "text/plain")).Status);
        Assert.Empty(h.Procurement.Calls);
        Assert.Equal(0, await h.Evaluations());
    }

    // §0.5 stage 3b: MDM says the company is not the tenant's → non-disclosing 404; MDM unreachable or MDM 403 (the caller lacks
    // mdm.legal-entities.read) → 503 DEPENDENCY_UNAVAILABLE at SupplyChain's own check — Procurement is never called.
    [Theory]
    [InlineData(LegalEntityScopeOutcome.NotReferenceable, HttpStatusCode.NotFound, "UNKNOWN_SUPPLIER")]
    [InlineData(LegalEntityScopeOutcome.Unavailable, HttpStatusCode.ServiceUnavailable, "DEPENDENCY_UNAVAILABLE")]
    public async Task Create_WhenMdmRefusesTheCompany_ShouldAnswerBeforeProcurement(LegalEntityScopeOutcome outcome, HttpStatusCode status, string code)
    {
        await using var h = new SupplierPerformanceHost();
        h.LegalEntities.Outcome = outcome;

        var refused = await h.Create();

        Assert.Equal((status, code), (refused.Status, refused.Code));
        Assert.Empty(h.Procurement.Calls);
        Assert.Equal(0, await h.Evaluations());
        var call = Assert.Single(h.LegalEntities.Calls);
        Assert.Equal((h.Tenant, h.LegalEntity), (call.TenantId, call.LegalEntityId));
    }

    // T-0147-V1-ELIG-01 through the host: the request reaches Procurement with the caller's token and LE, and the answer is mapped.
    [Fact]
    public async Task Create_ShouldReadEligibilityWithTheCallersTokenAndCompany()
    {
        await using var h = new SupplierPerformanceHost();
        var token = h.Token(null, null, Evaluate);

        Assert.Equal(HttpStatusCode.Created, (await h.Send(HttpMethod.Post, Base + "/evaluations", Body(), token: token)).Status);

        var call = Assert.Single(h.Procurement.Calls);
        Assert.Equal(("/api/suppliers/SUP-2001/eligibility", "Bearer " + token, h.Tenant.ToString(), h.LegalEntity.ToString(), h.Correlation),
            (call.Path, call.Authorization, call.Tenant, call.LegalEntity, call.Correlation));
    }

    public static TheoryData<string, HttpStatusCode, string?, HttpStatusCode, string> EligibilityTable => new()
    {
        { "no relationship", HttpStatusCode.NotFound, FakeProcurement.Error("UNKNOWN_SUPPLIER"), HttpStatusCode.NotFound, "UNKNOWN_SUPPLIER" },
        { "LE lookup forbidden", HttpStatusCode.Forbidden, FakeProcurement.Error("LEGAL_ENTITY_LOOKUP_FORBIDDEN"), HttpStatusCode.Forbidden, "FORBIDDEN" },
        { "no eligibility permission", HttpStatusCode.Forbidden, null, HttpStatusCode.Forbidden, "FORBIDDEN" },
        { "unknown LE", HttpStatusCode.NotFound, FakeProcurement.Error("UNKNOWN_LEGAL_ENTITY"), HttpStatusCode.ServiceUnavailable, "SUPPLIER_BASE_UNAVAILABLE" },
        { "route 404 without body", HttpStatusCode.NotFound, null, HttpStatusCode.ServiceUnavailable, "SUPPLIER_BASE_UNAVAILABLE" },
        { "Procurement 401", HttpStatusCode.Unauthorized, null, HttpStatusCode.ServiceUnavailable, "SUPPLIER_BASE_UNAVAILABLE" },
        { "Procurement 503", HttpStatusCode.ServiceUnavailable, FakeProcurement.Error("DEPENDENCY_UNAVAILABLE"), HttpStatusCode.ServiceUnavailable, "SUPPLIER_BASE_UNAVAILABLE" },
        { "Procurement 500", HttpStatusCode.InternalServerError, null, HttpStatusCode.ServiceUnavailable, "SUPPLIER_BASE_UNAVAILABLE" },
        { "null status", HttpStatusCode.OK, FakeProcurement.Eligibility(status: null).ToJsonString(), HttpStatusCode.ServiceUnavailable, "SUPPLIER_BASE_UNAVAILABLE" },
        { "malformed 200", HttpStatusCode.OK, "{\"supplierId\":\"SUP-2001\"}", HttpStatusCode.ServiceUnavailable, "SUPPLIER_BASE_UNAVAILABLE" },
        { "other supplier", HttpStatusCode.OK, FakeProcurement.Eligibility(supplierId: "SUP-9999").ToJsonString(), HttpStatusCode.ServiceUnavailable, "SUPPLIER_BASE_UNAVAILABLE" },
    };

    [Theory, MemberData(nameof(EligibilityTable))]
    public async Task Create_WhenProcurementAnswers_ShouldMapAsD2AndWriteNoDraftUnlessEligible(string because, HttpStatusCode procurement, string? body, HttpStatusCode status, string code)
    {
        await using var h = new SupplierPerformanceHost();
        h.Procurement.Answer = _ => (procurement, body);

        var answer = await h.Create();

        Assert.True((status, code) == (answer.Status, answer.Code), because);
        Assert.Equal(0, await h.Evaluations()); Assert.Equal(0, await h.Receipts()); Assert.Empty(await h.AuditRecords());
    }

    [Theory]
    [InlineData("Active", "Approved")] [InlineData("OnHold", "Approved")] [InlineData("Blocked", "Approved")] [InlineData("Inactive", "Approved")] [InlineData("Active", "InReview")] [InlineData("Active", null)]
    public async Task Create_WhenTheRelationshipIsNotActiveOrNotApproved_ShouldStillCreate(string status, string? onboarding)
    {
        await using var h = new SupplierPerformanceHost();
        h.Procurement.Answer = _ => (HttpStatusCode.OK, FakeProcurement.Eligibility(status: status, onboarding: onboarding).ToJsonString());

        Assert.Equal(HttpStatusCode.Created, (await h.Create()).Status);
    }

    // Natig P1, review F-2: a legacy code is 422 SUPPLIER_CODE_NOT_CANONICAL with no Procurement call — including the contract's example.
    [Theory]
    [InlineData("sup-0140-8821")] [InlineData("LEGACY-17")] [InlineData("SUP-2001 ")] [InlineData("{0b6f3c2e-6d1a-4c8e-9a51-2f4e7d9b1a10}")]
    public async Task Create_WhenTheSupplierIdIsALegacyCode_ShouldAnswer422WithoutCallingProcurement(string supplierId)
    {
        await using var h = new SupplierPerformanceHost();

        var refused = await h.Create(Body(supplierId));

        Assert.Equal((HttpStatusCode.UnprocessableEntity, "BUSINESS_RULE_VIOLATION", "SUPPLIER_CODE_NOT_CANONICAL"),
            (refused.Status, refused.Code, refused.Body!["error"]!["details"]!["code"]!.GetValue<string>()));
        Assert.Empty(h.Procurement.Calls);
        Assert.Equal(0, await h.Evaluations());
    }

    // §0.0 D2: the canonical code is stored, so a master uuid and its code are one series — a second create is 409.
    [Fact]
    public async Task Create_WhenTheMasterUuidAndTheCodeNameOneSupplier_ShouldStoreTheCodeAndRefuseTheSecondPeriod()
    {
        await using var h = new SupplierPerformanceHost();

        var byUuid = await h.Create(Body(FakeProcurement.MasterId), "by-uuid");
        var byCode = await h.Create(Body(FakeProcurement.Code), "by-code");

        Assert.Equal(HttpStatusCode.Created, byUuid.Status);
        Assert.Equal(FakeProcurement.Code, byUuid.Body!["supplierId"]!.GetValue<string>());
        Assert.Equal("/api/suppliers/0b6f3c2e-6d1a-4c8e-9a51-2f4e7d9b1a10/eligibility", h.Procurement.Calls.First().Path);
        Assert.Equal((HttpStatusCode.Conflict, "DUPLICATE_EVALUATION"), (byCode.Status, byCode.Code));
        Assert.Equal(HttpStatusCode.Conflict, (await h.Create(Body(FakeProcurement.MasterId), "by-uuid-again")).Status);
        Assert.Equal(1, await h.Evaluations());
        // The list filter matches the stored canonical code exactly; a uuid in the filter is not resolved and Procurement is not asked.
        var calls = h.Procurement.Calls.Count;
        Assert.Equal(1, (await h.Get("/evaluations?supplierId=SUP-2001")).Body!["total"]!.GetValue<int>());
        Assert.Equal(0, (await h.Get("/evaluations?supplierId=" + FakeProcurement.MasterId)).Body!["total"]!.GetValue<int>());
        Assert.Equal(calls, h.Procurement.Calls.Count);
    }

    private static async Task<SupplierEvaluation> Publish(SupplierPerformanceHost h, string evaluationId)
    {
        var collection = h.Db.GetCollection<SupplierEvaluation>(SupplierPerformanceSchema.Evaluations);
        var update = Builders<SupplierEvaluation>.Update.Set(x => x.Status, EvaluationStatus.PUBLISHED).Set(x => x.ScorecardId, Guid.NewGuid())
            .Set(x => x.PublishedAt, new DateTimeOffset(2026, 8, 2, 9, 0, 0, TimeSpan.Zero)).Set(x => x.Version, 2);
        // DL-2 owns submit; DL-1 reads what it will write, so the test seeds it.
        return await collection.FindOneAndUpdateAsync(new BsonDocument("_id", evaluationId), update, new FindOneAndUpdateOptions<SupplierEvaluation> { ReturnDocument = ReturnDocument.After });
    }

    // T-0147-ISO-01: every read with a foreign Tenant or LE → 404 with no existence detail; lists show nothing.
    [Fact]
    public async Task Reads_WhenTheTenantOrCompanyIsForeign_ShouldAnswerLikeAMissingRecord()
    {
        await using var h = new SupplierPerformanceHost();
        var id = (await h.Create()).Body!["evaluationId"]!.GetValue<string>();
        var scorecardId = (await Publish(h, id)).ScorecardId!.Value.ToString();

        Assert.Equal(HttpStatusCode.OK, (await h.Get("/evaluations/" + id)).Status);
        var scorecard = await h.Get("/scorecards/" + scorecardId);
        Assert.Equal(HttpStatusCode.OK, scorecard.Status);
        Assert.Equal((id, "PUBLISHED"), ((await h.Get("/evaluations/" + id)).Body!["evaluationId"]!.GetValue<string>(), (await h.Get("/evaluations/" + id)).Body!["status"]!.GetValue<string>()));
        Assert.Equal(("2026-08-02T09:00:00Z", "85.00", id), (scorecard.Body!["publishedAt"]!.GetValue<string>(), scorecard.Body["overallScore"]!.GetValue<string>(), scorecard.Body["evaluationId"]!.GetValue<string>()));

        var foreignTenant = Guid.NewGuid();
        foreach (var (tenant, le) in new[] { (h.Tenant, Guid.NewGuid()), (foreignTenant, h.LegalEntity), (foreignTenant, Guid.NewGuid()) })
        {
            var evaluation = await h.Get("/evaluations/" + id, tenant, le);
            var missing = await h.Get("/evaluations/" + Guid.NewGuid(), tenant, le);
            Assert.Equal((HttpStatusCode.NotFound, "UNKNOWN_EVALUATION"), (evaluation.Status, evaluation.Code));
            Assert.Equal(missing.Body!["error"]!["message"]!.GetValue<string>(), evaluation.Body!["error"]!["message"]!.GetValue<string>());
            Assert.Null(evaluation.Body["error"]!["details"]);
            Assert.Equal((HttpStatusCode.NotFound, "UNKNOWN_SCORECARD"), ((await h.Get("/scorecards/" + scorecardId, tenant, le)).Status, (await h.Get("/scorecards/" + scorecardId, tenant, le)).Code));
            Assert.Equal(0, (await h.Get("/evaluations", tenant, le)).Body!["total"]!.GetValue<int>());
            Assert.Equal(0, (await h.Get("/scorecards", tenant, le)).Body!["total"]!.GetValue<int>());
        }
        Assert.Equal(1, (await h.Get("/scorecards")).Body!["total"]!.GetValue<int>());
    }

    [Fact]
    public async Task ListEvaluations_ShouldFilterPageAndValidateTheQuery()
    {
        await using var h = new SupplierPerformanceHost();
        foreach (var month in new[] { "07", "08", "09" })
        {
            var end = month == "09" ? "30" : "31";
            Assert.Equal(HttpStatusCode.Created, (await h.Create(Body(start: $"2026-{month}-01", end: $"2026-{month}-{end}"), "list-" + month)).Status);
        }

        var page = await h.Get("/evaluations?page=2&pageSize=2");
        Assert.Equal((2, 2, 3), (page.Body!["page"]!.GetValue<int>(), page.Body["pageSize"]!.GetValue<int>(), page.Body["total"]!.GetValue<int>()));
        Assert.Equal("2026-09-01", Assert.Single(page.Body["items"]!.AsArray())!["periodStart"]!.GetValue<string>());
        Assert.Equal(h.Correlation, page.Body["correlationId"]!.GetValue<string>());
        Assert.Equal(50, (await h.Get("/evaluations")).Body!["pageSize"]!.GetValue<int>());
        Assert.Equal(2, (await h.Get("/evaluations?periodFrom=2026-08-01")).Body!["total"]!.GetValue<int>());
        Assert.Equal(1, (await h.Get("/evaluations?periodFrom=2026-08-01&periodTo=2026-08-31")).Body!["total"]!.GetValue<int>());
        Assert.Equal(3, (await h.Get("/evaluations?status=DRAFT")).Body!["total"]!.GetValue<int>());
        Assert.Equal(0, (await h.Get("/evaluations?status=PUBLISHED")).Body!["total"]!.GetValue<int>());
        foreach (var bad in new[] { "status=draft", "status=X", "periodFrom=2026-13-01", "page=0", "pageSize=201", "pageSize=-1", "page=a", "status=DRAFT&status=DRAFT" })
            Assert.Equal((HttpStatusCode.BadRequest, "INVALID_REQUEST"), ((await h.Get("/evaluations?" + bad)).Status, (await h.Get("/evaluations?" + bad)).Code));
        foreach (var bad in new[] { "riskLevel=low", "periodTo=2026-02-30" })
            Assert.Equal(HttpStatusCode.BadRequest, (await h.Get("/scorecards?" + bad)).Status);
    }

    // AUD-001 (testing-agent rule 4): the central record is WRITTEN — actor, tenant, company, entity type + id, operation — and a
    // refused command files none (the shared forwarder records successes only; ledger "ret kayıt bırakmaz").
    [Fact]
    public async Task Create_ShouldWriteOneCentralAuditRecordInTheCommandTransactionAndNoneForARefusal()
    {
        await using var h = new SupplierPerformanceHost();

        var id = (await h.Create()).Body!["evaluationId"]!.GetValue<string>();
        await h.Create(Body(metrics: Metrics(("OTD", "90"))), "refused");

        var record = Assert.Single(await h.AuditRecords());
        Assert.Equal((h.Tenant.ToString(), h.LegalEntity.ToString(), h.Actor.ToString(), id, "Create", "supplier-performance-risk", SupplyChainCentralAudit.Category, h.Correlation, true),
            (record["TenantId"].AsString, record["CompanyId"].AsString, record["ActorId"].AsString, record["EntityId"].AsString, record["Operation"].AsString,
             record["SourceModule"].AsString, record["Category"].AsString, record["CorrelationId"].AsString, record["WrittenInCommandTransaction"].AsBoolean));
        Assert.DoesNotContain("notes", record.ToJson(), StringComparison.OrdinalIgnoreCase);
    }
}

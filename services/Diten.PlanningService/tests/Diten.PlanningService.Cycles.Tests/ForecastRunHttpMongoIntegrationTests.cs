using System.Net;
using System.Text;
using System.Text.Json;
using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;
using Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using Diten.PlanningService.Persistence.Features.DemandPlanning;
using MongoDB.Driver;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed partial class PublishedRevisionMongoIntegrationTests
{
    private sealed class ForecastAssignmentAuthority(Guid company) : IManualDraftAuthority
    {
        public bool Available { get; set; } = true;
        public bool Scope { get; set; } = true;

        public Task<Guid?> ResolveSelectedAsync(Guid tenantId, Guid actorId,
            Guid selectedLegalEntityHint, CancellationToken cancellationToken)
        {
            if (!Available) throw new IOException("Fixture authority outage");
            return Task.FromResult<Guid?>(selectedLegalEntityHint == company
                ? company : null);
        }

        public Task<IReadOnlyList<VerifiedDraftSeriesReference>?> VerifyCreateSeriesAsync(
            Guid tenantId, Guid actorId, Guid legalEntityId,
            IReadOnlyList<DraftSeriesKey> requested, CancellationToken cancellationToken)
        {
            if (!Available) throw new IOException("Fixture authority outage");
            IReadOnlyList<VerifiedDraftSeriesReference>? result = Scope
                ? requested.Select(item => new VerifiedDraftSeriesReference(
                    item.SkuId, item.WarehouseId, "EA", "Gsku")).ToArray()
                : null;
            return Task.FromResult(result);
        }

        public Task<bool?> CanAccessAsync(Guid tenantId, Guid actorId,
            Guid legalEntityId, IReadOnlyList<DraftSeriesKey> series,
            CancellationToken cancellationToken)
        {
            if (!Available) throw new IOException("Fixture authority outage");
            return Task.FromResult<bool?>(Scope);
        }
    }

    [ManualDraftMongoFact]
    public async Task ForecastHttp_ReevaluatesAcceptedHistoryAndWritesRunDraftAndAudits()
    {
        var (_, _, context) = Open();
        var tenant = Guid.NewGuid();
        var company = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var sku = Guid.NewGuid();
        var template = Draft(tenant, company, actor, FirstWeek);
        var cycle = new PlanningCycle
        {
            Id = template.PlanningCycleId, TenantId = tenant,
            LegalEntityId = company, AsOfDate = template.AsOfDate,
            CalendarId = template.CalendarId,
            CalendarVersion = template.CalendarVersion,
            TimeZoneId = template.TimeZoneId,
            PlanningPeriodKey = template.PlanningPeriodKey,
            HorizonStart = template.HorizonStart,
            HorizonEnd = template.HorizonEnd,
            Weeks = template.Weeks.ToList()
        };
        await context.PlanningCycles.InsertOneAsync(cycle);
        await SeedAcceptedForecastHistory(context, tenant, company, sku);
        var authority = new ForecastAssignmentAuthority(company);
        await using var host = await ReadHttpHost.StartAsync(template,
            assignmentAuthority: authority);
        var evaluationPath = $"/api/v2/demand/forecast-runs/evaluation?planningCycleId={cycle.Id:D}&skuId={sku:D}&warehouseId=WH-1";
        using (var response = await host.Client.GetAsync(evaluationPath))
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using (var request = host.Request(evaluationPath, tenant, company, actor,
            permissions: []))
        using (var response = await host.Client.SendAsync(request))
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using (var request = host.Request(evaluationPath, Guid.NewGuid(),
            company, actor, ["demand.forecast-runs.create"]))
        using (var response = await host.Client.SendAsync(request))
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        authority.Scope = false;
        using (var request = host.Request(evaluationPath, tenant, company,
            actor, ["demand.forecast-runs.create"]))
        using (var response = await host.Client.SendAsync(request))
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        authority.Scope = true;
        authority.Available = false;
        using (var request = host.Request(evaluationPath, tenant, company,
            actor, ["demand.forecast-runs.create"]))
        using (var response = await host.Client.SendAsync(request))
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        authority.Available = true;
        using (var request = host.Request(evaluationPath, tenant, company,
            actor, ["demand.forecast-runs.create"]))
        using (var response = await host.Client.SendAsync(request))
        {
            var data = await SuccessData(response);
            var naive = data.GetProperty("candidates").EnumerateArray().Single(
                row => row.GetProperty("method").GetString() == "Naive");
            Assert.True(naive.GetProperty("eligible").GetBoolean());
            Assert.True(naive.GetProperty("unitVerificationPending").GetBoolean());
            Assert.Equal("CASE", naive.GetProperty("forecastUomId").GetString());
        }

        async Task<HttpResponseMessage> Create(string key, string method,
            string reason = "Planner selected method", string[]? permissions = null,
            Guid? requestTenant = null)
        {
            using var request = host.Request("/api/v2/demand/forecast-runs",
                requestTenant ?? tenant, company, actor,
                permissions ?? ["demand.forecast-runs.create", "demand.drafts.create"]);
            request.Method = HttpMethod.Post;
            request.Headers.Add("Idempotency-Key", key);
            request.Content = new StringContent(JsonSerializer.Serialize(new
            {
                planningCycleId = cycle.Id, skuId = sku, warehouseId = "WH-1",
                method, reason, expectedCycleVersion = cycle.Version
            }), Encoding.UTF8, "application/json");
            return await host.Client.SendAsync(request);
        }
        using (var response = await Create("bad-method", "HoltWinters"))
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using (var response = await Create("no-draft", "Naive",
            permissions: ["demand.forecast-runs.create"]))
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using (var response = await Create("no-run", "Naive",
            permissions: ["demand.drafts.create"]))
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using (var response = await Create("other-tenant", "Naive",
            requestTenant: Guid.NewGuid()))
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        authority.Scope = false;
        using (var response = await Create("scope-revoked", "Naive"))
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        authority.Scope = true;
        authority.Available = false;
        using (var response = await Create("authority-outage", "Naive"))
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        authority.Available = true;
        await AssertNoForecastWrites(context, tenant);
        Guid revisionId;
        using (var response = await Create("chosen-naive", "Naive"))
        {
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var data = json.RootElement.GetProperty("data");
            revisionId = data.GetProperty("draft").GetProperty("revisionId").GetGuid();
            Assert.Equal("Naive", data.GetProperty("method").GetString());
            Assert.False(data.GetProperty("replayed").GetBoolean());
        }
        using (var response = await Create("chosen-naive", "Naive"))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(revisionId, json.RootElement.GetProperty("data")
                .GetProperty("draft").GetProperty("revisionId").GetGuid());
        }
        using (var response = await Create("chosen-naive", "Naive", "Changed"))
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        // Replays must reauthorize instead of returning a previously visible Draft.
        authority.Scope = false;
        using (var response = await Create("chosen-naive", "Naive"))
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        authority.Scope = true;
        authority.Available = false;
        using (var response = await Create("chosen-naive", "Naive"))
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        authority.Available = true;
        Assert.Single(await context.ForecastRuns.Find(x => x.TenantId == tenant)
            .ToListAsync());
        Assert.Single(await context.ForecastSelectionAudit.Find(x =>
            x.TenantId == tenant).ToListAsync());
        Assert.Single(await context.ManualDraftManifests.Find(x =>
            x.TenantId == tenant && x.Id == revisionId).ToListAsync());
        Assert.Single(await context.ManualDraftAudit.Find(x =>
            x.TenantId == tenant && x.RevisionId == revisionId &&
            x.Action == "Created").ToListAsync());
    }

    [ManualDraftMongoFact]
    public async Task ForecastHttp_EitherMandatoryAuditInsertFailure_RollsBackAllWrites()
    {
        // Each collision is scoped to a fresh fixture tenant in the explicit test DB.
        // No production Mongo validator, connection or authentication is weakened.
        foreach (var selectionFailure in new[] { true, false })
        {
            var (_, _, context) = Open();
            var tenant = Guid.NewGuid();
            var company = Guid.NewGuid();
            var actor = Guid.NewGuid();
            var sku = Guid.NewGuid();
            var template = Draft(tenant, company, actor, FirstWeek);
            var cycle = new PlanningCycle
            {
                Id = template.PlanningCycleId, TenantId = tenant,
                LegalEntityId = company, AsOfDate = template.AsOfDate,
                CalendarId = template.CalendarId, CalendarVersion = template.CalendarVersion,
                TimeZoneId = template.TimeZoneId, PlanningPeriodKey = template.PlanningPeriodKey,
                HorizonStart = template.HorizonStart, HorizonEnd = template.HorizonEnd,
                Weeks = template.Weeks.ToList()
            };
            await context.PlanningCycles.InsertOneAsync(cycle);
            await SeedAcceptedForecastHistory(context, tenant, company, sku);
            await using var host = await ReadHttpHost.StartAsync(template,
                assignmentAuthority: new ForecastAssignmentAuthority(company));
            var key = "blocked-http-audit";
            var sentinel = Guid.NewGuid();
            var indexName = "mod0188_http_audit_failure";
            if (selectionFailure)
            {
                await context.ForecastSelectionAudit.Indexes.CreateOneAsync(
                    new CreateIndexModel<ForecastSelectionAudit>(
                        Builders<ForecastSelectionAudit>.IndexKeys
                            .Ascending(x => x.TenantId).Ascending(x => x.RequestKey),
                        new CreateIndexOptions<ForecastSelectionAudit>
                        {
                            Name = indexName, Unique = true,
                            PartialFilterExpression = Builders<ForecastSelectionAudit>.Filter
                                .Eq(x => x.TenantId, tenant)
                        }));
                await context.ForecastSelectionAudit.InsertOneAsync(new ForecastSelectionAudit
                {
                    Id = sentinel, TenantId = tenant, LegalEntityId = company,
                    RequestKey = key, ActorId = actor, Reason = "Test collision",
                    NewMethod = "Naive", OccurredAt = Now
                });
            }
            else
            {
                await context.ManualDraftAudit.Indexes.CreateOneAsync(
                    new CreateIndexModel<ManualDraftAuditRecord>(
                        Builders<ManualDraftAuditRecord>.IndexKeys
                            .Ascending(x => x.TenantId).Ascending(x => x.RequestKey),
                        new CreateIndexOptions<ManualDraftAuditRecord>
                        {
                            Name = indexName, Unique = true,
                            PartialFilterExpression = Builders<ManualDraftAuditRecord>.Filter
                                .Eq(x => x.TenantId, tenant)
                        }));
                await context.ManualDraftAudit.InsertOneAsync(new ManualDraftAuditRecord
                {
                    Id = sentinel, TenantId = tenant, LegalEntityId = company,
                    RequestKey = key, ActorId = actor, Action = "TestCollision",
                    Reason = "Test collision", OccurredAt = Now
                });
            }
            async Task<HttpResponseMessage> Send()
            {
                using var request = host.Request("/api/v2/demand/forecast-runs",
                    tenant, company, actor,
                    ["demand.forecast-runs.create", "demand.drafts.create"]);
                request.Method = HttpMethod.Post;
                request.Headers.Add("Idempotency-Key", key);
                request.Content = new StringContent(JsonSerializer.Serialize(new
                {
                    planningCycleId = cycle.Id, skuId = sku, warehouseId = "WH-1",
                    method = "Naive", reason = "Explicit method selection",
                    expectedCycleVersion = cycle.Version
                }), Encoding.UTF8, "application/json");
                return await host.Client.SendAsync(request);
            }
            try
            {
                using var failed = await Send();
                Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
                await AssertNoForecastWrites(context, tenant, sentinel);
                // Prove the planted audit remains the sole record, not a replacement.
                Assert.Equal(selectionFailure ? 1 : 0,
                    await context.ForecastSelectionAudit.CountDocumentsAsync(x => x.Id == sentinel));
                Assert.Equal(selectionFailure ? 0 : 1,
                    await context.ManualDraftAudit.CountDocumentsAsync(x => x.Id == sentinel));
            }
            finally
            {
                if (selectionFailure)
                    await context.ForecastSelectionAudit.Indexes.DropOneAsync(indexName);
                else
                    await context.ManualDraftAudit.Indexes.DropOneAsync(indexName);
            }
            // Removing only the fixture collision permits the same HTTP command.
            using var retry = await Send();
            Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
            Assert.Equal(1, await context.ForecastRuns.CountDocumentsAsync(x => x.TenantId == tenant));
            Assert.Equal(1, await context.ManualDraftManifests.CountDocumentsAsync(x => x.TenantId == tenant));
            Assert.Equal(1, await context.ForecastSelectionAudit.CountDocumentsAsync(x =>
                x.TenantId == tenant && x.Id != sentinel));
            Assert.Equal(1, await context.ManualDraftAudit.CountDocumentsAsync(x =>
                x.TenantId == tenant && x.Id != sentinel));
        }
    }

    private static async Task AssertNoForecastWrites(
        Diten.PlanningService.Persistence.Features.DemandPlanning.DemandPlanningMongoContext context,
        Guid tenant, Guid? sentinel = null)
    {
        Assert.Equal(0, await context.ForecastRuns.CountDocumentsAsync(x => x.TenantId == tenant));
        Assert.Equal(0, await context.ManualDraftManifests.CountDocumentsAsync(x => x.TenantId == tenant));
        Assert.Equal(0, await context.ManualDraftSeriesParts.CountDocumentsAsync(x => x.TenantId == tenant));
        Assert.Equal(0, await context.ManualDraftAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.Id != sentinel));
        Assert.Equal(0, await context.ForecastSelectionAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.Id != sentinel));
    }
    private static async Task SeedAcceptedForecastHistory(
        Diten.PlanningService.Persistence.Features.DemandPlanning.DemandPlanningMongoContext context,
        Guid tenant, Guid company, Guid sku)
    {
        var first = FirstWeek.AddDays(-26 * 7);
        var last = FirstWeek.AddDays(-7);
        var sourceKey = Guid.NewGuid().ToString("N");
        var reviewer = Guid.NewGuid();
        var batch = new DemandHistoryImportBatch
        {
            TenantId = tenant, LegalEntityId = company,
            UploadedByActorId = Guid.NewGuid(), SourceSystem = "fixture",
            SourceFileSha256 = new string('A', 64),
            CoverageFileSha256 = new string('B', 64),
            ScopeFrom = first, ScopeThrough = last.AddDays(6),
            WarehouseScope = ["WH-1"], ReviewState = ImportBatchReviewState.Approved,
            ReviewedByActorId = reviewer,
            CoverageWeeks = Enumerable.Range(0, 26).Select(i => new ImportWeekCoverage
            {
                SkuId = sku, SkuLevel = "Gsku", WarehouseId = "WH-1",
                UomId = "CASE", WeekStart = first.AddDays(i * 7),
                StockState = ImportWeekStockState.InStock
            }).ToList(),
            Rows = [new DemandHistoryImportRow
            {
                RowNumber = 1, SourceRecordKey = sourceKey, SkuId = sku,
                SkuLevel = "Gsku", WarehouseId = "WH-1",
                OccurredOn = last.AddDays(2), OriginalQuantity = 5,
                OriginalUomId = "CASE", RecordKind = "Shipment",
                UnitVerificationPending = true
            }]
        };
        batch.CoverageContentSha256 = ImportCoverageFingerprint.Compute(batch.CoverageWeeks);
        batch.AuditTrail.Add(new DemandHistoryImportAuditAction
        {
            Action = "BatchApproved", ActorId = reviewer, OccurredAt = Now,
            Detail = "coverage=" + batch.CoverageFileSha256 +
                ";content=" + batch.CoverageContentSha256
        });
        await context.HistoryImportBatches.InsertOneAsync(batch);
        var acceptedAt = Now.AddMinutes(1);
        var row = new AcceptedDemandHistoryRow
        {
            TenantId = tenant, LegalEntityId = company, BatchId = batch.Id,
            BatchRowNumber = 1, SourceSystem = batch.SourceSystem,
            SourceRecordKey = sourceKey, SourceFileSha256 = batch.SourceFileSha256,
            SkuId = sku, SkuLevel = "Gsku", WarehouseId = "WH-1",
            OccurredOn = last.AddDays(2), OriginalQuantity = 5,
            OriginalUomId = "CASE", AcceptedQuantity = 5,
            AcceptedUomId = "CASE", UnitVerificationPending = true,
            AcceptedByActorId = Guid.NewGuid(), AcceptedAt = acceptedAt,
            AcceptanceReason = "Verified", RequestKey = Guid.NewGuid().ToString("N"),
            RequestFingerprint = new string('C', 64)
        };
        row.ContentSha256 = AcceptedHistoryFingerprint.Compute(row);
        await context.AcceptedHistoryRows.InsertOneAsync(row);
        await context.HistoryAcceptanceAudit.InsertOneAsync(new DemandHistoryAcceptanceAudit
        {
            TenantId = tenant, LegalEntityId = company, AcceptedRowId = row.Id,
            BatchId = batch.Id, BatchRowNumber = 1, SourceRecordKey = sourceKey,
            ActorId = row.AcceptedByActorId, OccurredAt = acceptedAt,
            Reason = row.AcceptanceReason, ContentSha256 = row.ContentSha256
        });
    }
}

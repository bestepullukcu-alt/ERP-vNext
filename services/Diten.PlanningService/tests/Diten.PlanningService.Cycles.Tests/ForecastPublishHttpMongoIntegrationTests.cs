using System.Net;
using System.Text;
using System.Text.Json;
using Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using Diten.PlanningService.Persistence.Features.DemandPlanning;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed partial class PublishedRevisionMongoIntegrationTests
{
    private sealed class ForecastE2EAuthority(Guid company, Guid otherCompany) : IManualDraftAuthority
    {
        private readonly ForecastAssignmentAuthority _inner = new(company);
        public Task<Guid?> ResolveSelectedAsync(Guid tenantId, Guid actorId,
            Guid selectedLegalEntityHint, CancellationToken cancellationToken) =>
            Task.FromResult<Guid?>(selectedLegalEntityHint == company || selectedLegalEntityHint == otherCompany
                ? selectedLegalEntityHint : null);
        public Task<IReadOnlyList<VerifiedDraftSeriesReference>?> VerifyCreateSeriesAsync(
            Guid tenantId, Guid actorId, Guid legalEntityId,
            IReadOnlyList<DraftSeriesKey> requested, CancellationToken cancellationToken) =>
            _inner.VerifyCreateSeriesAsync(tenantId, actorId, legalEntityId, requested, cancellationToken);
        public Task<bool?> CanAccessAsync(Guid tenantId, Guid actorId, Guid legalEntityId,
            IReadOnlyList<DraftSeriesKey> series, CancellationToken cancellationToken) =>
            _inner.CanAccessAsync(tenantId, actorId, legalEntityId, series, cancellationToken);
    }

    [ManualDraftMongoFact]
    public async Task ForecastHttp_ToPublished_PreservesProvenanceSoDAndAtomicAudit()
    {
        // Arrange: only accepted file history and reference authorities are fixtures.
        // The Draft under test is created exclusively by the HTTP/JWT ForecastRun command.
        var (drafts, published, context) = Open();
        var tenant = Guid.NewGuid();
        var company = Guid.NewGuid();
        var otherCompany = Guid.NewGuid();
        var planner = Guid.NewGuid();
        var reviewer = Guid.NewGuid();
        var sku = Guid.NewGuid();
        var cycle = new PlanningCycle
        {
            Id = Guid.NewGuid(), TenantId = tenant, LegalEntityId = company,
            AsOfDate = FirstWeek.AddDays(-3), CalendarId = "ISO-8601",
            CalendarVersion = "1", TimeZoneId = "Asia/Baku",
            PlanningPeriodKey = FirstWeek.ToString("yyyy-MM-dd"),
            HorizonStart = FirstWeek, HorizonEnd = FirstWeek.AddDays(363),
            Weeks = Enumerable.Range(0, 52).Select(index => new PlanningWeek
            {
                Number = index + 1, WeekStart = FirstWeek.AddDays(index * 7),
                WeekEnd = FirstWeek.AddDays(index * 7 + 6)
            }).ToList()
        };
        await context.PlanningCycles.InsertOneAsync(cycle);
        await SeedAcceptedForecastHistory(context, tenant, company, sku);
        // Host metadata is never persisted or used to construct the forecast Draft.
        var hostMetadata = new DemandRevisionDraft { TenantId = tenant, LegalEntityId = company };
        await using var host = await ReadHttpHost.StartAsync(hostMetadata,
            assignmentAuthority: new ForecastE2EAuthority(company, otherCompany),
            publishAuthority: new FixtureAuthority(new(true, true, true, false)),
            allowPublishInTest: true);
        Guid revisionId;
        Guid runId;
        using (var request = host.Request("/api/v2/demand/forecast-runs", tenant,
            company, planner, ["demand.forecast-runs.create", "demand.drafts.create"]))
        {
            request.Method = HttpMethod.Post;
            request.Headers.Add("Idempotency-Key", "e2e-method-choice");
            request.Content = new StringContent(JsonSerializer.Serialize(new
            {
                planningCycleId = cycle.Id, skuId = sku, warehouseId = "WH-1",
                method = "Naive", reason = "Explicit E2E Naive selection",
                expectedCycleVersion = cycle.Version
            }), Encoding.UTF8, "application/json");
            using var response = await host.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var data = json.RootElement.GetProperty("data");
            runId = data.GetProperty("forecastRunId").GetGuid();
            revisionId = data.GetProperty("draft").GetProperty("revisionId").GetGuid();
        }
        var draft = Assert.IsType<DemandRevisionDraft>(
            await drafts.ReadAsync(tenant, company, revisionId, default));
        var run = Assert.Single(await context.ForecastRuns.Find(x => x.TenantId == tenant).ToListAsync());
        var runBefore = run.ToBsonDocument();
        Assert.Equal(runId, run.Id);
        Assert.Equal(revisionId, run.RevisionId);
        Assert.Equal(company, run.LegalEntityId);
        Assert.Equal(cycle.Id, run.PlanningCycleId);
        Assert.Equal(sku, run.SkuId);
        Assert.Equal("WH-1", run.WarehouseId);
        Assert.Equal(planner, run.SelectedBy);
        Assert.Equal("Naive", run.Method);
        Assert.Equal("CASE", run.BaseUomId);
        Assert.True(run.UnitVerificationPending);
        Assert.Equal(0, draft.Version);
        Assert.Equal(0, draft.StateVersion);
        Assert.Equal(DemandRevisionState.Draft, draft.State);
        var series = Assert.Single(draft.Series);
        Assert.Equal(sku, series.SkuId);
        Assert.Equal("WH-1", series.WarehouseId);
        Assert.Equal("CASE", series.BaseUomId);
        Assert.Equal(52, series.Weeks.Count);
        var origin = Assert.IsType<MethodWeekProvenance>(series.Weeks[0].MethodProvenance);
        Assert.Equal(run.Id, origin.ForecastRunId);
        Assert.Equal(run.HistoryContentSha256, origin.HistoryContentSha256);
        Assert.Equal(run.PolicyVersion, origin.PolicyVersion);
        Assert.Equal(run.HistoryAsOfWeekStart, origin.HistoryAsOfWeekStart);
        Assert.Equal(run.Parameters.OrderBy(x => x.Key), origin.Parameters.OrderBy(x => x.Key));
        Assert.All(series.Weeks, week =>
        {
            Assert.Equal(5m, week.Quantity);
            Assert.Equal(DraftWeekSource.Method, week.Source);
            Assert.True(origin.HasSameContent(week.MethodProvenance));
        });
        var selection = Assert.Single(await context.ForecastSelectionAudit.Find(x => x.TenantId == tenant).ToListAsync());
        Assert.Equal(run.Id, selection.ForecastRunId);
        Assert.Equal(revisionId, selection.RevisionId);
        Assert.Equal(company, selection.LegalEntityId);
        Assert.Equal(cycle.Id, selection.PlanningCycleId);
        Assert.Equal(sku, selection.SkuId);
        Assert.Equal("WH-1", selection.WarehouseId);
        Assert.Equal(planner, selection.ActorId);
        Assert.Equal(run.Reason, selection.Reason);
        Assert.Equal(run.PolicyVersion, selection.PolicyVersion);
        Assert.Equal(run.Method, selection.NewMethod);
        Assert.Equal(run.SelectedAt, selection.OccurredAt);
        var creationAudit = Assert.Single(await context.ManualDraftAudit.Find(x => x.TenantId == tenant).ToListAsync());
        Assert.Equal(revisionId, creationAudit.RevisionId);
        Assert.Equal(company, creationAudit.LegalEntityId);
        Assert.Equal(planner, creationAudit.ActorId);
        Assert.Equal("Created", creationAudit.Action);
        Assert.Equal(run.SelectedAt, creationAudit.OccurredAt);

        async Task<HttpResponseMessage> Transition(string action, Guid actor, string permission,
            string key, int stateVersion, int contentVersion = 0)
        {
            object body = action == "publish"
                ? new { expectedContentVersion = contentVersion, expectedStateVersion = stateVersion }
                : new { expectedContentVersion = contentVersion, expectedStateVersion = stateVersion,
                    reason = "Independent complete content review" };
            using var request = CommandRequest(host, $"/api/v2/demand/revisions/{revisionId:D}/{action}",
                draft, actor, permission, key, body);
            return await host.Client.SendAsync(request);
        }
        async Task AssertState(DemandRevisionState state, int version)
        {
            var current = Assert.IsType<DemandRevisionDraft>(await drafts.ReadAsync(tenant, company, revisionId, default));
            Assert.Equal(state, current.State);
            Assert.Equal(version, current.StateVersion);
            Assert.Equal(0, current.Version);
        }

        // Act: all lifecycle commands retain this HTTP-created revision and its version fences.
        using (var response = await Transition("submit-review", planner, "demand.drafts.update", "e2e-submit", 0))
            await SuccessData(response);
        await AssertState(DemandRevisionState.InReview, 1);
        using (var response = await Transition("approve", planner, "demand.plans.review", "e2e-self-review", 1))
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using (var request = host.Request($"/api/v2/demand/revisions/{revisionId:D}", Guid.NewGuid(), company, reviewer, ["demand.plans.read"]))
        using (var response = await host.Client.SendAsync(request))
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using (var request = CommandRequest(host, $"/api/v2/demand/revisions/{revisionId:D}/approve", draft,
            reviewer, "demand.plans.review", "e2e-wrong-company", new
            { expectedContentVersion = 0, expectedStateVersion = 1, reason = "Wrong company" }, otherCompany))
        using (var response = await host.Client.SendAsync(request))
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using (var response = await Transition("approve", reviewer, "demand.plans.review", "e2e-stale-review", 0))
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertState(DemandRevisionState.InReview, 1);
        Assert.Equal(1, await context.ManualDraftReviewAudit.CountDocumentsAsync(x => x.TenantId == tenant));
        using (var response = await Transition("approve", reviewer, "demand.plans.review", "e2e-approve", 1))
            await SuccessData(response);
        await AssertState(DemandRevisionState.Approved, 2);
        var reviewAudits = await context.ManualDraftReviewAudit.Find(x => x.TenantId == tenant)
            .SortBy(x => x.StateVersionAfter).ToListAsync();
        Assert.Equal(new[] { DraftReviewAction.Submitted, DraftReviewAction.Approved }, reviewAudits.Select(x => x.Action));
        Assert.Equal(new[] { planner, reviewer }, reviewAudits.Select(x => x.ActorId));
        Assert.Equal(new[] { 1, 2 }, reviewAudits.Select(x => x.StateVersionAfter));
        Assert.All(reviewAudits, audit =>
        {
            Assert.Equal(company, audit.LegalEntityId);
            Assert.Equal(revisionId, audit.RevisionId);
            Assert.Equal(0, audit.ContentVersion);
        });
        using (var response = await Transition("publish", planner, "demand.plans.publish", "e2e-self-publish", 2))
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using (var response = await Transition("publish", reviewer, "demand.plans.publish", "e2e-stale-state", 1))
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using (var response = await Transition("publish", reviewer, "demand.plans.publish", "e2e-stale-content", 2, 1))
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        // A stronger, tenant-only fixture index rejects the final mandatory general audit.
        // It neither alters production validators nor weakens a security guard.
        var indexName = "mod0188_e2e_audit_" + tenant.ToString("N");
        var sentinel = new DemandAuditEntry
        {
            TenantId = tenant, LegalEntityId = company, ActorId = reviewer,
            Action = "Published", EvidenceReference = "Fixture collision sentinel",
            OccurredAt = Now
        };
        await context.AuditEntries.InsertOneAsync(sentinel);
        await context.AuditEntries.Indexes.CreateOneAsync(new CreateIndexModel<DemandAuditEntry>(
            Builders<DemandAuditEntry>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.Action),
            new CreateIndexOptions<DemandAuditEntry>
            {
                Name = indexName, Unique = true,
                PartialFilterExpression = Builders<DemandAuditEntry>.Filter.Eq(x => x.TenantId, tenant)
            }));
        try
        {
            using var response = await Transition("publish", reviewer, "demand.plans.publish", "e2e-publish", 2);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            await AssertState(DemandRevisionState.Approved, 2);
            Assert.Equal(0, await context.PublishedRevisionManifests.CountDocumentsAsync(x => x.TenantId == tenant));
            Assert.Equal(0, await context.PublishedRevisionParts.CountDocumentsAsync(x => x.TenantId == tenant));
            Assert.Equal(0, await context.PublishedBaselineSlots.CountDocumentsAsync(x => x.TenantId == tenant));
            Assert.Equal(0, await context.Outbox.CountDocumentsAsync(x => x.TenantId == tenant));
            Assert.Equal(0, await context.ManualDraftPublicationAudit.CountDocumentsAsync(x => x.TenantId == tenant));
            Assert.Equal(sentinel.Id, Assert.Single(await context.AuditEntries.Find(x => x.TenantId == tenant).ToListAsync()).Id);
            Assert.Equal(revisionId, Assert.Single(await context.ManualDraftReviewSlots.Find(x => x.TenantId == tenant).ToListAsync()).RevisionId);
        }
        finally
        {
            await context.AuditEntries.Indexes.DropOneAsync(indexName);
        }
        using (var response = await Transition("publish", reviewer, "demand.plans.publish", "e2e-publish", 2))
            Assert.Equal("Published", (await SuccessData(response)).GetProperty("outcome").GetString());
        await AssertState(DemandRevisionState.Published, 3);

        // Assert: checksum, selected scope, method lineage and durable side effects agree.
        var (manifest, parts) = await ReadPersisted(tenant, company, revisionId, context);
        Assert.Equal(company, manifest.LegalEntityId);
        Assert.Equal(cycle.Id, manifest.PlanningCycleId);
        Assert.Equal(0, manifest.ContentVersion);
        Assert.Equal(3, manifest.StateVersion);
        Assert.Equal(planner, manifest.CreatedBy);
        Assert.Equal(reviewer, manifest.ReviewedBy);
        Assert.Equal(reviewer, manifest.PublishedBy);
        Assert.Equal(1, manifest.ExpectedPartCount);
        Assert.Equal(52, manifest.ExpectedRowCount);
        Assert.Equal("MOD0188-CANONICAL-2/SHA-256", manifest.ChecksumScheme);
        Assert.Equal(manifest.Checksum, ContractDigest(manifest, parts));
        var part = Assert.Single(parts);
        Assert.Equal(sku, part.SkuId);
        Assert.Equal("WH-1", part.WarehouseId);
        Assert.Equal("CASE", part.BaseUomId);
        Assert.Equal(Enumerable.Range(1, 52), part.Rows.Select(x => x.Number));
        Assert.All(part.Rows, row =>
        {
            Assert.Equal(5m, row.Quantity);
            Assert.Equal(DraftWeekSource.Method, row.Source);
            Assert.True(origin.HasSameContent(row.MethodProvenance));
        });
        var persistedPartBefore = part.ToBsonDocument();
        // Sabotage only a detached read object: the independent contract hash must detect it.
        var originalRow = part.Rows[0];
        part.Rows[0] = originalRow with { MethodProvenance = origin with { ForecastRunId = Guid.NewGuid() } };
        Assert.NotEqual(manifest.Checksum, ContractDigest(manifest, parts));
        part.Rows[0] = originalRow;
        var publicationAudit = Assert.Single(await context.ManualDraftPublicationAudit.Find(x => x.TenantId == tenant).ToListAsync());
        Assert.Equal(revisionId, publicationAudit.RevisionId);
        Assert.Equal(company, publicationAudit.LegalEntityId);
        Assert.Equal(reviewer, publicationAudit.ActorId);
        Assert.Equal(DemandRevisionState.Published, publicationAudit.NewState);
        Assert.Equal(0, publicationAudit.ContentVersion);
        Assert.Equal(3, publicationAudit.StateVersionAfter);
        Assert.Equal(manifest.PublishedAt, publicationAudit.OccurredAt);
        var generalAudit = Assert.Single(await context.AuditEntries.Find(x => x.TenantId == tenant && x.Id != sentinel.Id).ToListAsync());
        Assert.Equal($"{revisionId:D}:{manifest.Checksum}", generalAudit.EvidenceReference);
        Assert.Equal(reviewer, generalAudit.ActorId);
        Assert.Equal(company, generalAudit.LegalEntityId);
        var outbox = Assert.Single(await context.Outbox.Find(x => x.TenantId == tenant).ToListAsync());
        Assert.Equal(revisionId, outbox.RevisionId);
        Assert.Equal(company, outbox.LegalEntityId);
        Assert.Equal(3, outbox.StateVersion);
        Assert.Null(outbox.DispatchedAt);
        using (var payload = JsonDocument.Parse(outbox.Payload))
        {
            var data = payload.RootElement;
            Assert.Equal(outbox.EventId, data.GetProperty("eventId").GetGuid());
            Assert.Equal(tenant, data.GetProperty("tenantId").GetGuid());
            Assert.Equal(company, data.GetProperty("legalEntityId").GetGuid());
            Assert.Equal(cycle.Id, data.GetProperty("planningCycleId").GetGuid());
            Assert.Equal(revisionId, data.GetProperty("revisionId").GetGuid());
            Assert.Equal(3, data.GetProperty("stateVersion").GetInt32());
            Assert.Equal(manifest.Checksum, data.GetProperty("integritySummary").GetProperty("digest").GetString());
            Assert.False(data.TryGetProperty("rows", out _));
        }
        using (var response = await Transition("publish", reviewer, "demand.plans.publish", "e2e-publish", 2))
            Assert.Equal("Replayed", (await SuccessData(response)).GetProperty("outcome").GetString());
        using (var request = CommandRequest(host, $"/api/v2/demand/revisions/{revisionId:D}/manual-weeks/{sku:D}/WH-1/1",
            draft, planner, "demand.drafts.update", "e2e-edit-published", new
            { valueKind = DraftWeekValueKind.Known, quantity = 99m, reason = "Must remain immutable", expectedContentVersion = 0 }))
        {
            request.Method = HttpMethod.Patch;
            using var response = await host.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        var snapshot = (await published.ReadSnapshotAsync(tenant, company, revisionId, default))!.Value;
        Assert.Equal(manifest.Checksum, snapshot.Manifest.Checksum);
        Assert.Equal(persistedPartBefore, Assert.Single(snapshot.Parts).ToBsonDocument());
        Assert.Equal(runBefore, Assert.Single(await context.ForecastRuns.Find(x => x.TenantId == tenant).ToListAsync()).ToBsonDocument());
        Assert.Equal(1, await context.Outbox.CountDocumentsAsync(x => x.TenantId == tenant));
        Assert.Equal(1, await context.ManualDraftPublicationAudit.CountDocumentsAsync(x => x.TenantId == tenant));
        Assert.Equal(0, await context.ManualDraftReviewSlots.CountDocumentsAsync(x => x.TenantId == tenant));
    }
}

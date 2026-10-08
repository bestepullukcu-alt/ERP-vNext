using System.Net;
using System.Text;
using System.Text.Json;
using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using Diten.PlanningService.Persistence.Features.DemandPlanning;
using MongoDB.Driver;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed partial class PublishedRevisionMongoIntegrationTests
{
    private sealed class CommandAssignmentAuthority(Guid legalEntityId)
        : IManualDraftAuthority
    {
        public bool Available { get; set; } = true;
        public bool Assigned { get; set; } = true;

        public Task<Guid?> ResolveSelectedAsync(Guid tenantId, Guid actorId,
            Guid selectedLegalEntityHint, CancellationToken cancellationToken)
        {
            if (!Available) throw new InvalidOperationException("Fixture authority unavailable.");
            return Task.FromResult<Guid?>(Assigned &&
                selectedLegalEntityHint == legalEntityId ? legalEntityId : null);
        }

        public Task<IReadOnlyList<VerifiedDraftSeriesReference>?> VerifyCreateSeriesAsync(
            Guid tenantId, Guid actorId, Guid legalEntityId,
            IReadOnlyList<DraftSeriesKey> requested, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool?> CanAccessAsync(Guid tenantId, Guid actorId,
            Guid legalEntityId, IReadOnlyList<DraftSeriesKey> series,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private static HttpRequestMessage CommandRequest(ReadHttpHost host, string path,
        DemandRevisionDraft draft, Guid actor, string permission, string key,
        object body, Guid? selectedLegalEntity = null)
    {
        var request = host.Request(path, draft.TenantId,
            selectedLegalEntity ?? draft.LegalEntityId, actor, [permission]);
        request.Method = HttpMethod.Post;
        request.Headers.Add("Idempotency-Key", key);
        request.Content = new StringContent(JsonSerializer.Serialize(body),
            Encoding.UTF8, "application/json");
        return request;
    }

    [ManualDraftMongoFact]
    public async Task V2Commands_FixturePublishAndInvalidate_AreAtomicAndReplayOnce()
    {
        var (drafts, _, context) = Open();
        var draft = Draft(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), FirstWeek);
        var publisher = Guid.NewGuid();
        await Approve(drafts, draft, draft.CreatedBy, publisher);
        var assignment = new CommandAssignmentAuthority(draft.LegalEntityId);
        await using var host = await ReadHttpHost.StartAsync(draft,
            assignmentAuthority: assignment,
            publishAuthority: new FixtureAuthority(new(true, true, true, false)),
            invalidationAuthority: new InvalidationAuthority(draft),
            allowPublishInTest: true);
        var path = $"/api/v2/demand/revisions/{draft.Id:D}";
        async Task<HttpResponseMessage> Publish(string key, int version = 2) =>
            await host.Client.SendAsync(CommandRequest(host, path + "/publish",
                draft, publisher, "demand.plans.publish", key,
                new { expectedContentVersion = 0, expectedStateVersion = version }));
        using (var response = await Publish("pub-1"))
        {
            var data = await SuccessData(response);
            Assert.Equal("Published", data.GetProperty("outcome").GetString());
            Assert.Equal(3, data.GetProperty("stateVersion").GetInt32());
        }
        using (var response = await Publish("pub-1"))
        {
            var data = await SuccessData(response);
            Assert.Equal("Replayed", data.GetProperty("outcome").GetString());
        }
        using (var response = await Publish("pub-1", 1))
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1, await context.Outbox.CountDocumentsAsync(x =>
            x.TenantId == draft.TenantId && x.RevisionId == draft.Id));

        async Task<HttpResponseMessage> Invalidate(string key, int version = 3) =>
            await host.Client.SendAsync(CommandRequest(host, path + "/invalidate",
                draft, publisher, "demand.plans.invalidate", key,
                new { expectedContentVersion = 0, expectedStateVersion = version,
                    impactCode = "ContentIntegrity", reason = "Wrong source quantity",
                    evidenceReference = "verified-mrp-impact-fixture" }));
        using (var response = await Invalidate("inv-1"))
        {
            var data = await SuccessData(response);
            Assert.Equal("Invalidated", data.GetProperty("outcome").GetString());
            Assert.Equal(4, data.GetProperty("stateVersion").GetInt32());
        }
        using (var response = await Invalidate("inv-1"))
        {
            var data = await SuccessData(response);
            Assert.Equal("Replayed", data.GetProperty("outcome").GetString());
        }
        Assert.Equal(2, await context.Outbox.CountDocumentsAsync(x =>
            x.TenantId == draft.TenantId && x.RevisionId == draft.Id));
        Assert.Equal(2, await context.ManualDraftPublicationAudit.CountDocumentsAsync(x =>
            x.TenantId == draft.TenantId && x.RevisionId == draft.Id));
        using var statusRequest = host.Request(path + "/status", draft.TenantId,
            draft.LegalEntityId, publisher);
        using var statusResponse = await host.Client.SendAsync(statusRequest);
        Assert.Equal("Invalidated", (await SuccessData(statusResponse))
            .GetProperty("state").GetString());
    }

    [ManualDraftMongoFact]
    public async Task V2Commands_DefaultPolicyAndAuthorityFailure_StayClosed()
    {
        var (drafts, _, context) = Open();
        var draft = Draft(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), FirstWeek);
        var publisher = Guid.NewGuid();
        await Approve(drafts, draft, draft.CreatedBy, publisher);
        var assignment = new CommandAssignmentAuthority(draft.LegalEntityId);
        var path = $"/api/v2/demand/revisions/{draft.Id:D}/publish";
        await using (var productionPolicy = await ReadHttpHost.StartAsync(draft,
            assignmentAuthority: assignment,
            publishAuthority: new FixtureAuthority(new(true, true, true, false))))
        using (var request = CommandRequest(productionPolicy, path, draft,
            publisher, "demand.plans.publish", "default-deny",
            new { expectedContentVersion = 0, expectedStateVersion = 2 }))
        using (var response = await productionPolicy.Client.SendAsync(request))
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var host = await ReadHttpHost.StartAsync(draft,
            assignmentAuthority: assignment,
            publishAuthority: new FixtureAuthority(new(true, true, true, false)),
            allowPublishInTest: true);
        using (var anonymous = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent("{\"expectedContentVersion\":0,\"expectedStateVersion\":2}",
                Encoding.UTF8, "application/json")
        })
        using (var response = await host.Client.SendAsync(anonymous))
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using (var noPermission = host.Request(path, draft.TenantId,
            draft.LegalEntityId, publisher, []))
        {
            noPermission.Method = HttpMethod.Post;
            noPermission.Headers.Add("Idempotency-Key", "no-permission");
            noPermission.Content = new StringContent(
                "{\"expectedContentVersion\":0,\"expectedStateVersion\":2}",
                Encoding.UTF8, "application/json");
            using var response = await host.Client.SendAsync(noPermission);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        assignment.Available = false;
        using (var request = CommandRequest(host, path, draft, publisher,
            "demand.plans.publish", "outage",
            new { expectedContentVersion = 0, expectedStateVersion = 2 }))
        using (var response = await host.Client.SendAsync(request))
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        assignment.Available = true;
        assignment.Assigned = false;
        using (var request = CommandRequest(host, path, draft, publisher,
            "demand.plans.publish", "hidden",
            new { expectedContentVersion = 0, expectedStateVersion = 2 }))
        using (var response = await host.Client.SendAsync(request))
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        assignment.Assigned = true;
        using (var request = CommandRequest(host, path, draft,
            draft.CreatedBy, "demand.plans.publish", "creator",
            new { expectedContentVersion = 0, expectedStateVersion = 2 }))
        using (var response = await host.Client.SendAsync(request))
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using (var request = CommandRequest(host, path, draft, publisher,
            "demand.plans.publish", "missing-key",
            new { expectedContentVersion = 0, expectedStateVersion = 2 }))
        {
            request.Headers.Remove("Idempotency-Key");
            using var response = await host.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using (var request = CommandRequest(host, path, draft, publisher,
            "demand.plans.publish", "client-flags",
            new { expectedContentVersion = 0, expectedStateVersion = 2,
                actorId = draft.CreatedBy, verifiedImpact = true }))
        using (var response = await host.Client.SendAsync(request))
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using (var integrationActor = await ReadHttpHost.StartAsync(draft,
            assignmentAuthority: assignment,
            publishAuthority: new FixtureAuthority(new(true, true, true, true)),
            allowPublishInTest: true))
        using (var request = CommandRequest(integrationActor, path, draft, publisher,
            "demand.plans.publish", "integration-actor",
            new { expectedContentVersion = 0, expectedStateVersion = 2 }))
        using (var response = await integrationActor.Client.SendAsync(request))
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await context.Outbox.CountDocumentsAsync(x =>
            x.TenantId == draft.TenantId && x.RevisionId == draft.Id));
    }

    [ManualDraftMongoFact]
    public async Task V2Commands_StaleScopeAndMaterialEvidence_ReturnDistinctFailures()
    {
        var (draft, _, published, context) = await PublishedFixture(secondSeries: false);
        var actor = Guid.NewGuid();
        var assignment = new CommandAssignmentAuthority(draft.LegalEntityId);
        var path = $"/api/v2/demand/revisions/{draft.Id:D}/invalidate";
        await using var host = await ReadHttpHost.StartAsync(draft,
            assignmentAuthority: assignment,
            invalidationAuthority: new InvalidationAuthority(draft,
                materialImpact: false));
        async Task<HttpResponseMessage> Send(string impact, string key,
            int stateVersion = 3) => await host.Client.SendAsync(
            CommandRequest(host, path, draft, actor,
                "demand.plans.invalidate", key,
                new { expectedContentVersion = 0, expectedStateVersion = stateVersion,
                    impactCode = impact, reason = "Wrong quantity",
                    evidenceReference = "unverified-fixture" }));
        using (var response = await Send("ForecastDeviation", "deviation"))
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using (var response = await Send("ContentIntegrity", "no-impact"))
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using (var request = CommandRequest(host, path, draft, actor,
            "demand.plans.invalidate", "wrong-company",
            new { expectedContentVersion = 0, expectedStateVersion = 3,
                impactCode = "ContentIntegrity", reason = "Wrong quantity",
                evidenceReference = "unverified-fixture" }, Guid.NewGuid()))
        using (var response = await host.Client.SendAsync(request))
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using (var request = CommandRequest(host, path, draft, actor,
            "demand.plans.publish", "no-invalidate-permission",
            new { expectedContentVersion = 0, expectedStateVersion = 3,
                impactCode = "ContentIntegrity", reason = "Wrong quantity",
                evidenceReference = "unverified-fixture" }))
        using (var response = await host.Client.SendAsync(request))
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using (var verified = await ReadHttpHost.StartAsync(draft,
            assignmentAuthority: assignment,
            invalidationAuthority: new InvalidationAuthority(draft)))
        using (var request = CommandRequest(verified, path, draft, actor,
            "demand.plans.invalidate", "stale",
            new { expectedContentVersion = 0, expectedStateVersion = 2,
                impactCode = "ContentIntegrity", reason = "Wrong quantity",
                evidenceReference = "verified-mrp-impact-fixture" }))
        using (var response = await verified.Client.SendAsync(request))
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(DemandRevisionState.Published,
            (await published.ReadSnapshotAsync(draft.TenantId, draft.LegalEntityId,
                draft.Id, default))?.Manifest.State);
        Assert.Equal(0, await context.ManualDraftPublicationAudit.CountDocumentsAsync(x =>
            x.TenantId == draft.TenantId && x.RevisionId == draft.Id &&
            x.NewState == DemandRevisionState.Invalidated));
    }

    [ManualDraftMongoFact]
    public async Task V2Commands_PublishOutboxCollision_RollsBackOverHttp()
    {
        var (drafts, published, context) = Open();
        var draft = Draft(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), FirstWeek);
        var publisher = Guid.NewGuid();
        await Approve(drafts, draft, draft.CreatedBy, publisher);
        await published.EnsureIndexesAsync();
        await context.Outbox.InsertOneAsync(new DemandOutboxMessage
        {
            TenantId = draft.TenantId, LegalEntityId = draft.LegalEntityId,
            RevisionId = Guid.NewGuid(),
            EventId = PublishedRevisionMongoStore.DeterministicEventId(
                draft.TenantId, draft.LegalEntityId, draft.Id),
            EventType = "test-blocker", Payload = "{}", OccurredAt = Now
        });
        await using var host = await ReadHttpHost.StartAsync(draft,
            assignmentAuthority: new CommandAssignmentAuthority(draft.LegalEntityId),
            publishAuthority: new FixtureAuthority(new(true, true, true, false)),
            allowPublishInTest: true);
        using var request = CommandRequest(host,
            $"/api/v2/demand/revisions/{draft.Id:D}/publish", draft,
            publisher, "demand.plans.publish", "outbox-failure",
            new { expectedContentVersion = 0, expectedStateVersion = 2 });
        using var response = await host.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(DemandRevisionState.Approved,
            (await drafts.ReadAsync(draft.TenantId, draft.LegalEntityId,
                draft.Id, default))?.State);
        Assert.Equal(0, await context.PublishedRevisionManifests.CountDocumentsAsync(x =>
            x.TenantId == draft.TenantId && x.RevisionId == draft.Id));
        Assert.Equal(0, await context.ManualDraftPublicationAudit.CountDocumentsAsync(x =>
            x.TenantId == draft.TenantId && x.RevisionId == draft.Id));
    }

    [ManualDraftMongoFact]
    public async Task V2Commands_InvalidateAuditCollision_RollsBackOverHttp()
    {
        var (draft, _, published, context) = await PublishedFixture(secondSeries: false);
        var indexName = $"mod0188_v2_command_audit_{draft.TenantId:N}";
        await context.AuditEntries.Indexes.CreateOneAsync(
            new CreateIndexModel<DemandAuditEntry>(
                Builders<DemandAuditEntry>.IndexKeys.Ascending(x => x.Action),
                new CreateIndexOptions<DemandAuditEntry>
                {
                    Unique = true, Name = indexName,
                    PartialFilterExpression = Builders<DemandAuditEntry>.Filter
                        .Eq(x => x.TenantId, draft.TenantId)
                }));
        try
        {
            await context.AuditEntries.InsertOneAsync(new DemandAuditEntry
            {
                TenantId = draft.TenantId, LegalEntityId = draft.LegalEntityId,
                ActorId = Guid.NewGuid(), Action = "Invalidated",
                EvidenceReference = "test-blocker", OccurredAt = Now
            });
            await using var host = await ReadHttpHost.StartAsync(draft,
                assignmentAuthority: new CommandAssignmentAuthority(draft.LegalEntityId),
                invalidationAuthority: new InvalidationAuthority(draft));
            using var request = CommandRequest(host,
                $"/api/v2/demand/revisions/{draft.Id:D}/invalidate", draft,
                Guid.NewGuid(), "demand.plans.invalidate", "audit-failure",
                new { expectedContentVersion = 0, expectedStateVersion = 3,
                    impactCode = "ContentIntegrity", reason = "Wrong quantity",
                    evidenceReference = "verified-mrp-impact-fixture" });
            using var response = await host.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal(DemandRevisionState.Published,
                (await published.ReadSnapshotAsync(draft.TenantId,
                    draft.LegalEntityId, draft.Id, default))?.Manifest.State);
            Assert.Equal(0, await context.ManualDraftPublicationAudit.CountDocumentsAsync(x =>
                x.TenantId == draft.TenantId && x.RevisionId == draft.Id &&
                x.NewState == DemandRevisionState.Invalidated));
            Assert.Equal(0, await context.Outbox.CountDocumentsAsync(x =>
                x.TenantId == draft.TenantId && x.RevisionId == draft.Id &&
                x.EventType == "demand.revision.invalidated.v2"));
        }
        finally
        {
            await context.AuditEntries.Indexes.DropOneAsync(indexName);
        }
    }

    [ManualDraftMongoFact]
    public async Task V2Commands_CorruptDraft_ReturnsUnavailableWithoutPublication()
    {
        var (drafts, published, context) = Open();
        var draft = Draft(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), FirstWeek);
        var publisher = Guid.NewGuid();
        await Approve(drafts, draft, draft.CreatedBy, publisher);
        var part = await context.ManualDraftSeriesParts.Find(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.RevisionId == draft.Id).FirstAsync();
        part.SkuId = Guid.NewGuid();
        await context.ManualDraftSeriesParts.ReplaceOneAsync(x => x.Id == part.Id, part);
        await using var host = await ReadHttpHost.StartAsync(draft,
            assignmentAuthority: new CommandAssignmentAuthority(draft.LegalEntityId),
            publishAuthority: new FixtureAuthority(new(true, true, true, false)),
            allowPublishInTest: true);
        using var request = CommandRequest(host,
            $"/api/v2/demand/revisions/{draft.Id:D}/publish", draft,
            publisher, "demand.plans.publish", "corrupt-draft",
            new { expectedContentVersion = 0, expectedStateVersion = 2 });
        using var response = await host.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(0, await context.PublishedRevisionManifests.CountDocumentsAsync(x =>
            x.TenantId == draft.TenantId && x.RevisionId == draft.Id));
        Assert.Equal(0, await context.Outbox.CountDocumentsAsync(x =>
            x.TenantId == draft.TenantId && x.RevisionId == draft.Id));
    }
}

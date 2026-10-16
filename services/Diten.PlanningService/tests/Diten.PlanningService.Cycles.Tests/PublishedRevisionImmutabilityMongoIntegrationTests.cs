using System.Net;
using System.Text;
using System.Text.Json;
using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using MongoDB.Driver;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed partial class PublishedRevisionMongoIntegrationTests
{
    private sealed class ImmutabilityAssignmentAuthority(Guid tenantId, Guid legalEntityId)
        : IManualDraftAuthority
    {
        public Task<Guid?> ResolveSelectedAsync(Guid tenant, Guid actor,
            Guid selectedLegalEntityHint, CancellationToken cancellationToken) =>
            Task.FromResult<Guid?>(tenant == tenantId &&
                selectedLegalEntityHint == legalEntityId ? legalEntityId : null);

        public Task<IReadOnlyList<VerifiedDraftSeriesReference>?> VerifyCreateSeriesAsync(
            Guid tenant, Guid actor, Guid legalEntity,
            IReadOnlyList<DraftSeriesKey> requested, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<VerifiedDraftSeriesReference>?>(null);

        public Task<bool?> CanAccessAsync(Guid tenant, Guid actor, Guid legalEntity,
            IReadOnlyList<DraftSeriesKey> series, CancellationToken cancellationToken) =>
            Task.FromResult<bool?>(tenant == tenantId &&
                legalEntity == legalEntityId && series.Count > 0);
    }

    [ManualDraftMongoFact]
    public async Task PublishedAndSuperseded_DirectVersionRacesCannotChangeContentOrAudit()
    {
        var (drafts, published, context) = Open();
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var creator = Guid.NewGuid();
        var reviewer = Guid.NewGuid();
        var old = Draft(tenant, legalEntity, creator, FirstWeek, secondSeries: true);
        var current = Draft(tenant, legalEntity, creator, FirstWeek,
            calendarVersion: "2", quantity: 7m, secondSeries: true);
        await Approve(drafts, old, creator, reviewer);
        Assert.Equal(PublishOutcome.Published, (await Publish(published, old, reviewer)).Outcome);
        await Approve(drafts, current, creator, reviewer);
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, current, reviewer)).Outcome);

        foreach (var (revision, expectedState, stateVersion) in new[]
        {
            (old, DemandRevisionState.Superseded, 4),
            (current, DemandRevisionState.Published, 3)
        })
        {
            var before = (await published.ReadSnapshotAsync(tenant, legalEntity,
                revision.Id, default))!.Value;
            var partJson = JsonSerializer.Serialize(before.Parts);
            var auditCount = await context.ManualDraftAudit.CountDocumentsAsync(x =>
                x.TenantId == tenant && x.LegalEntityId == legalEntity &&
                x.RevisionId == revision.Id);
            var reviewCount = await context.ManualDraftReviewAudit.CountDocumentsAsync(x =>
                x.TenantId == tenant && x.LegalEntityId == legalEntity &&
                x.RevisionId == revision.Id);
            var publicationCount = await context.ManualDraftPublicationAudit.CountDocumentsAsync(x =>
                x.TenantId == tenant && x.LegalEntityId == legalEntity &&
                x.RevisionId == revision.Id);
            var series = revision.Series[0];
            var edits = await Task.WhenAll(Enumerable.Range(0, 2).Select(index =>
                drafts.EditWeekAsync(tenant, legalEntity, revision.Id, creator,
                    series.SkuId, series.WarehouseId, 1, DraftWeekValueKind.Known,
                    99m, "Attempt to change frozen plan", $"ac20-edit-{revision.Id:D}-{index}",
                    0, Now.AddMinutes(5), default)));
            Assert.All(edits, result => Assert.Equal(ManualDraftStoreOutcome.Conflict,
                result.Outcome));
            Assert.Equal(ManualDraftStoreOutcome.Conflict,
                (await drafts.ExcludeSeriesAsync(tenant, legalEntity, revision.Id,
                    creator, series.SkuId, series.WarehouseId, "Attempt to exclude",
                    $"ac20-exclude-{revision.Id:D}", 0, stateVersion,
                    Now.AddMinutes(6), default)).Outcome);
            Assert.Equal(ManualDraftStoreOutcome.Conflict,
                (await drafts.TransitionReviewAsync(tenant, legalEntity, revision.Id,
                    creator, DraftReviewAction.Reopened, "Attempt to reopen",
                    $"ac20-reopen-{revision.Id:D}", 0, stateVersion,
                    Now.AddMinutes(7), default)).Outcome);
            Assert.Equal(PublishOutcome.Conflict,
                (await Publish(published, revision, reviewer,
                    $"ac20-republish-{revision.Id:D}", 0, stateVersion)).Outcome);

            var after = (await published.ReadSnapshotAsync(tenant, legalEntity,
                revision.Id, default))!.Value;
            Assert.Equal(expectedState, after.Manifest.State);
            Assert.Equal(stateVersion, after.Manifest.StateVersion);
            Assert.Equal(before.Manifest.Checksum, after.Manifest.Checksum);
            Assert.Equal(partJson, JsonSerializer.Serialize(after.Parts));
            var draftAfter = (await drafts.ReadAsync(tenant, legalEntity,
                revision.Id, default))!;
            Assert.Equal(expectedState, draftAfter.State);
            Assert.Equal(0, draftAfter.Version);
            Assert.Empty(draftAfter.Changes);
            Assert.Empty(draftAfter.Exclusions);
            Assert.Equal(auditCount, await context.ManualDraftAudit.CountDocumentsAsync(x =>
                x.TenantId == tenant && x.LegalEntityId == legalEntity &&
                x.RevisionId == revision.Id));
            Assert.Equal(reviewCount, await context.ManualDraftReviewAudit.CountDocumentsAsync(x =>
                x.TenantId == tenant && x.LegalEntityId == legalEntity &&
                x.RevisionId == revision.Id));
            Assert.Equal(publicationCount,
                await context.ManualDraftPublicationAudit.CountDocumentsAsync(x =>
                    x.TenantId == tenant && x.LegalEntityId == legalEntity &&
                    x.RevisionId == revision.Id));
        }
        Assert.Equal(2, await context.Outbox.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity));
    }

    [ManualDraftMongoFact]
    public async Task PublishedAndSuperseded_HttpDraftMutationsAreHiddenWithoutEffect()
    {
        var (drafts, published, context) = Open();
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var creator = Guid.NewGuid();
        var reviewer = Guid.NewGuid();
        var old = Draft(tenant, legalEntity, creator, FirstWeek, secondSeries: true);
        var current = Draft(tenant, legalEntity, creator, FirstWeek,
            calendarVersion: "2", quantity: 7m, secondSeries: true);
        await Approve(drafts, old, creator, reviewer);
        Assert.Equal(PublishOutcome.Published, (await Publish(published, old, reviewer)).Outcome);
        await Approve(drafts, current, creator, reviewer);
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, current, reviewer)).Outcome);
        await using var host = await ReadHttpHost.StartAsync(old,
            assignmentAuthority: new ImmutabilityAssignmentAuthority(tenant, legalEntity));

        foreach (var (revision, stateVersion) in new[] { (old, 4), (current, 3) })
        {
            var before = (await published.ReadSnapshotAsync(tenant, legalEntity,
                revision.Id, default))!.Value;
            var partJson = JsonSerializer.Serialize(before.Parts);
            var series = revision.Series[0];
            async Task<HttpStatusCode> Send(HttpMethod method, string path,
                object body, string key)
            {
                using var request = host.Request(path, tenant, legalEntity, creator,
                    ["demand.drafts.update"]);
                request.Method = method;
                request.Headers.Add("Idempotency-Key", key);
                request.Content = new StringContent(JsonSerializer.Serialize(body),
                    Encoding.UTF8, "application/json");
                using var response = await host.Client.SendAsync(request);
                return response.StatusCode;
            }
            var root = $"/api/v2/demand/revisions/{revision.Id:D}";
            Assert.Equal(HttpStatusCode.NotFound, await Send(HttpMethod.Patch,
                $"{root}/manual-weeks/{series.SkuId:D}/{series.WarehouseId}/1",
                new { valueKind = 0, quantity = 99m,
                    reason = "Attempt to change frozen plan", expectedContentVersion = 0 },
                $"ac20-http-edit-{revision.Id:D}"));
            Assert.Equal(HttpStatusCode.NotFound, await Send(HttpMethod.Post,
                $"{root}/reopen", new { expectedContentVersion = 0,
                    expectedStateVersion = stateVersion, reason = "Attempt to reopen" },
                $"ac20-http-reopen-{revision.Id:D}"));
            Assert.Equal(HttpStatusCode.NotFound, await Send(HttpMethod.Post,
                $"{root}/excluded-series/{series.SkuId:D}/{series.WarehouseId}",
                new { reason = "Attempt to exclude", expectedContentVersion = 0,
                    expectedStateVersion = stateVersion },
                $"ac20-http-exclude-{revision.Id:D}"));
            var after = (await published.ReadSnapshotAsync(tenant, legalEntity,
                revision.Id, default))!.Value;
            Assert.Equal(before.Manifest.Checksum, after.Manifest.Checksum);
            Assert.Equal(partJson, JsonSerializer.Serialize(after.Parts));
            Assert.Equal(stateVersion, after.Manifest.StateVersion);
            Assert.Equal(0, (await drafts.ReadAsync(tenant, legalEntity,
                revision.Id, default))!.Version);
        }
        Assert.Equal(2, await context.Outbox.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity));
    }
}

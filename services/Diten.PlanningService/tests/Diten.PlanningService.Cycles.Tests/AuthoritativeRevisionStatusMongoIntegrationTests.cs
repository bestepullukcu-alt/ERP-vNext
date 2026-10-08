using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using Diten.PlanningService.Persistence.Features.DemandPlanning;
using MongoDB.Driver;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed partial class PublishedRevisionMongoIntegrationTests
{
    private sealed class StatusAuthority(DemandRevisionDraft draft,
        bool sourceAvailable = true, bool permitted = true,
        bool inScope = true, bool throws = false)
        : IInternalRevisionStatusAuthority
    {
        public int Calls { get; private set; }

        public Task<RevisionStatusAuthorityEvidence> VerifyAsync(Guid tenantId,
            Guid legalEntityId, Guid revisionId, Guid actorId,
            CancellationToken cancellationToken)
        {
            Calls++;
            if (throws) throw new InvalidOperationException("Fixture authority outage.");
            var scope = draft.Series.Where(x => x.Exclusion is null)
                .Select(x => (x.SkuId, x.WarehouseId)).ToArray();
            return Task.FromResult(new RevisionStatusAuthorityEvidence(sourceAvailable,
                permitted, inScope && draft.TenantId == tenantId &&
                draft.LegalEntityId == legalEntityId && draft.Id == revisionId,
                tenantId, legalEntityId, revisionId, scope));
        }
    }

    private static AuthoritativeRevisionStatusReader StatusReader(
        DemandPlanningMongoContext context, DemandRevisionDraft draft,
        StatusAuthority? authority = null) =>
        new(context, authority ?? new StatusAuthority(draft));

    [ManualDraftMongoFact]
    public async Task AuthoritativeStatus_PublishedSupersededInvalidated_MonotonicAndContentFree()
    {
        var (draft, drafts, published, context) = await PublishedFixture(secondSeries: false);
        var reader = StatusReader(context, draft);
        var actor = Guid.NewGuid();
        var first = await reader.ReadAsync(draft.TenantId, draft.LegalEntityId,
            draft.Id, actor, default);
        Assert.Equal(AuthoritativeStatusOutcome.Found, first.Outcome);
        Assert.Equal(DemandRevisionState.Published, first.Status!.State);
        Assert.Equal(3, first.Status.StateVersion);
        Assert.Equal(draft.Id, first.Status.RevisionId);

        var replacement = Draft(draft.TenantId, draft.LegalEntityId,
            Guid.NewGuid(), FirstWeek);
        await Approve(drafts, replacement, replacement.CreatedBy, Guid.NewGuid());
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, replacement, Guid.NewGuid())).Outcome);
        var second = await reader.ReadAsync(draft.TenantId, draft.LegalEntityId,
            draft.Id, actor, default);
        Assert.Equal(AuthoritativeStatusOutcome.Found, second.Outcome);
        Assert.Equal(DemandRevisionState.Superseded, second.Status!.State);
        Assert.Equal(4, second.Status.StateVersion);

        Assert.Equal(InvalidationOutcome.Invalidated,
            (await Invalidate(Invalidator(draft, drafts, published, context),
                draft, stateVersion: 4)).Outcome);
        var third = await reader.ReadAsync(draft.TenantId, draft.LegalEntityId,
            draft.Id, actor, default);
        Assert.Equal(AuthoritativeStatusOutcome.Found, third.Outcome);
        Assert.Equal(DemandRevisionState.Invalidated, third.Status!.State);
        Assert.Equal(5, third.Status.StateVersion);
        Assert.Equal(draft.PlanningCycleId, third.Status.PlanningCycleId);
        Assert.Equal(draft.PlanningPeriodKey, third.Status.PlanningPeriodKey);
        Assert.DoesNotContain(typeof(AuthoritativeRevisionStatus).GetProperties(),
            x => x.Name.Contains("Row", StringComparison.OrdinalIgnoreCase) ||
                x.Name.Contains("Quantity", StringComparison.OrdinalIgnoreCase) ||
                x.Name.Contains("Checksum", StringComparison.OrdinalIgnoreCase));
    }

    [ManualDraftMongoFact]
    public async Task AuthoritativeStatus_CorruptInvalidatedPart_StillReturnsInvalidated()
    {
        var (draft, drafts, published, context) = await PublishedFixture(secondSeries: false);
        Assert.Equal(InvalidationOutcome.Invalidated,
            (await Invalidate(Invalidator(draft, drafts, published, context), draft)).Outcome);
        var part = await context.PublishedRevisionParts.Find(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.RevisionId == draft.Id).SingleAsync();
        part.Rows[0] = part.Rows[0] with { Quantity = 1000m };
        await context.PublishedRevisionParts.ReplaceOneAsync(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.Id == part.Id, part);
        var result = await StatusReader(context, draft).ReadAsync(draft.TenantId,
            draft.LegalEntityId, draft.Id, Guid.NewGuid(), default);
        Assert.Equal(AuthoritativeStatusOutcome.Found, result.Outcome);
        Assert.Equal(DemandRevisionState.Invalidated, result.Status!.State);
        await AssertAllReads(Reader(published, draft), draft, Guid.NewGuid(),
            SnapshotReadOutcome.InvalidSnapshot);
    }

    [ManualDraftMongoFact]
    public async Task AuthoritativeStatus_MissingOrMismatchedAudit_FailsClosed()
    {
        var (draft, _, _, context) = await PublishedFixture(secondSeries: false);
        var reader = StatusReader(context, draft);
        var actor = Guid.NewGuid();
        var audit = await context.ManualDraftPublicationAudit.Find(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.RevisionId == draft.Id).SingleAsync();
        audit.StateVersionAfter++;
        await context.ManualDraftPublicationAudit.ReplaceOneAsync(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.Id == audit.Id, audit);
        Assert.Equal(AuthoritativeStatusOutcome.Inconsistent,
            (await reader.ReadAsync(draft.TenantId, draft.LegalEntityId,
                draft.Id, actor, default)).Outcome);
        await context.ManualDraftPublicationAudit.DeleteOneAsync(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.Id == audit.Id);
        Assert.Equal(AuthoritativeStatusOutcome.Inconsistent,
            (await reader.ReadAsync(draft.TenantId, draft.LegalEntityId,
                draft.Id, actor, default)).Outcome);
    }

    [ManualDraftMongoFact]
    public async Task AuthoritativeStatus_ManifestDisagreementFails_OutboxRetentionIsNotStatusAuthority()
    {
        var (draft, _, _, context) = await PublishedFixture(secondSeries: false);
        var reader = StatusReader(context, draft);
        var manifest = await context.PublishedRevisionManifests.Find(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.RevisionId == draft.Id).SingleAsync();
        manifest.StateVersion++;
        await context.PublishedRevisionManifests.ReplaceOneAsync(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.Id == manifest.Id, manifest);
        Assert.Equal(AuthoritativeStatusOutcome.Inconsistent,
            (await reader.ReadAsync(draft.TenantId, draft.LegalEntityId,
                draft.Id, Guid.NewGuid(), default)).Outcome);
        manifest.StateVersion--;
        await context.PublishedRevisionManifests.ReplaceOneAsync(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.Id == manifest.Id, manifest);
        await context.Outbox.DeleteOneAsync(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.RevisionId == draft.Id &&
            x.EventType == "demand.revision.published.v2");
        Assert.Equal(AuthoritativeStatusOutcome.Found,
            (await reader.ReadAsync(draft.TenantId, draft.LegalEntityId,
                draft.Id, Guid.NewGuid(), default)).Outcome);
    }

    [ManualDraftMongoFact]
    public async Task AuthoritativeStatus_InvalidationAuditMismatch_NeverGuessesInvalidated()
    {
        var (draft, drafts, published, context) = await PublishedFixture(secondSeries: false);
        Assert.Equal(InvalidationOutcome.Invalidated,
            (await Invalidate(Invalidator(draft, drafts, published, context), draft)).Outcome);
        var reader = StatusReader(context, draft);
        var invalidation = await context.ManualDraftPublicationAudit.Find(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.RevisionId == draft.Id &&
            x.NewState == DemandRevisionState.Invalidated).SingleAsync();
        invalidation.EvidenceReference = "different-evidence";
        await context.ManualDraftPublicationAudit.ReplaceOneAsync(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.Id == invalidation.Id, invalidation);
        var result = await reader.ReadAsync(draft.TenantId, draft.LegalEntityId,
            draft.Id, Guid.NewGuid(), default);
        Assert.Equal(AuthoritativeStatusOutcome.Inconsistent, result.Outcome);
        Assert.Null(result.Status);
    }

    [ManualDraftMongoFact]
    public async Task AuthoritativeStatus_OutOfScopeRecordsHideAllStatesAndCorruption()
    {
        var (draft, drafts, published, context) = await PublishedFixture(secondSeries: false);
        var actor = Guid.NewGuid();
        var denied = StatusReader(context, draft,
            new StatusAuthority(draft, inScope: false));
        Assert.Equal(AuthoritativeStatusOutcome.NotFound,
            (await denied.ReadAsync(draft.TenantId, draft.LegalEntityId,
                draft.Id, actor, default)).Outcome);
        Assert.Equal(InvalidationOutcome.Invalidated,
            (await Invalidate(Invalidator(draft, drafts, published, context), draft)).Outcome);
        Assert.Equal(AuthoritativeStatusOutcome.NotFound,
            (await denied.ReadAsync(draft.TenantId, draft.LegalEntityId,
                draft.Id, actor, default)).Outcome);
        var publishedManifest = await context.PublishedRevisionManifests.Find(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.RevisionId == draft.Id).SingleAsync();
        publishedManifest.StateVersion++;
        await context.PublishedRevisionManifests.ReplaceOneAsync(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.Id == publishedManifest.Id, publishedManifest);
        Assert.Equal(AuthoritativeStatusOutcome.NotFound,
            (await denied.ReadAsync(draft.TenantId, draft.LegalEntityId,
                draft.Id, actor, default)).Outcome);
        Assert.Equal(AuthoritativeStatusOutcome.Inconsistent,
            (await StatusReader(context, draft).ReadAsync(draft.TenantId,
                draft.LegalEntityId, draft.Id, actor, default)).Outcome);
    }

    [ManualDraftMongoFact]
    public async Task AuthoritativeStatus_PermissionScopeAndAuthorityAreIndependent()
    {
        var (draft, _, _, context) = await PublishedFixture(secondSeries: false);
        var actor = Guid.NewGuid();
        foreach (var (authority, expected) in new[]
        {
            (new StatusAuthority(draft, permitted: false),
                AuthoritativeStatusOutcome.PermissionDenied),
            (new StatusAuthority(draft, sourceAvailable: false),
                AuthoritativeStatusOutcome.AuthorityUnavailable),
            (new StatusAuthority(draft, throws: true),
                AuthoritativeStatusOutcome.AuthorityUnavailable)
        })
        {
            var result = await StatusReader(context, draft, authority).ReadAsync(
                draft.TenantId, draft.LegalEntityId, draft.Id, actor, default);
            Assert.Equal(expected, result.Outcome);
            Assert.Null(result.Status);
            Assert.Equal(1, authority.Calls);
        }
        Assert.Equal(AuthoritativeStatusOutcome.NotFound,
            (await StatusReader(context, draft).ReadAsync(Guid.NewGuid(),
                draft.LegalEntityId, draft.Id, actor, default)).Outcome);
        Assert.Equal(AuthoritativeStatusOutcome.NotFound,
            (await StatusReader(context, draft).ReadAsync(draft.TenantId,
                Guid.NewGuid(), draft.Id, actor, default)).Outcome);
    }

    [ManualDraftMongoFact]
    public async Task AuthoritativeStatus_ConcurrentInvalidation_ObservesOnlyCommittedVersions()
    {
        var (draft, drafts, published, context) = await PublishedFixture(secondSeries: false);
        var reader = StatusReader(context, draft);
        var actor = Guid.NewGuid();
        var transition = Invalidate(Invalidator(draft, drafts, published, context), draft);
        var reads = Enumerable.Range(0, 12).Select(_ => reader.ReadAsync(
            draft.TenantId, draft.LegalEntityId, draft.Id, actor, default)).ToArray();
        await Task.WhenAll(reads.Cast<Task>().Append(transition));
        Assert.Equal(InvalidationOutcome.Invalidated, transition.Result.Outcome);
        Assert.All(reads, task =>
        {
            var result = task.Result;
            Assert.Equal(AuthoritativeStatusOutcome.Found, result.Outcome);
            Assert.True(result.Status!.State is DemandRevisionState.Published or
                DemandRevisionState.Invalidated);
            Assert.Equal(result.Status.State == DemandRevisionState.Published ? 3 : 4,
                result.Status.StateVersion);
        });
        var final = await reader.ReadAsync(draft.TenantId, draft.LegalEntityId,
            draft.Id, actor, default);
        Assert.Equal(DemandRevisionState.Invalidated, final.Status!.State);
        Assert.Equal(4, final.Status.StateVersion);
    }
}

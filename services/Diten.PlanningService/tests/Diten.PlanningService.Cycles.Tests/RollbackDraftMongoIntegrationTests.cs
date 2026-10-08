using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;
using Diten.PlanningService.Application.Features.DemandPlanning.RevisionCommands;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using Diten.PlanningService.Persistence.Features.DemandPlanning;
using MongoDB.Driver;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed partial class PublishedRevisionMongoIntegrationTests
{
    private sealed class RollbackReadAuthority : IInternalSnapshotReadAuthority
    {
        public IReadOnlyList<(Guid SkuId, string WarehouseId)> Series { get; set; } = [];
        public bool SourceAvailable { get; set; } = true;
        public bool HasReadPermission { get; set; } = true;
        public bool ScopeVerified { get; set; } = true;

        public Task<SnapshotReadAuthorityEvidence> VerifyAsync(Guid tenantId,
            Guid legalEntityId, Guid revisionId, Guid actorId,
            CancellationToken cancellationToken) => Task.FromResult(new
                SnapshotReadAuthorityEvidence(SourceAvailable, HasReadPermission,
                    ScopeVerified, tenantId, legalEntityId, revisionId, Series));
    }

    private sealed class RollbackDraftAuthority : IManualDraftAuthority
    {
        public bool Scope { get; set; } = true;
        public Func<Task>? BeforeAccessResult { get; set; }
        public Task<Guid?> ResolveSelectedAsync(Guid tenantId, Guid actorId,
            Guid selectedLegalEntityHint, CancellationToken cancellationToken) =>
            Task.FromResult<Guid?>(selectedLegalEntityHint);

        public Task<IReadOnlyList<VerifiedDraftSeriesReference>?> VerifyCreateSeriesAsync(
            Guid tenantId, Guid actorId, Guid legalEntityId,
            IReadOnlyList<DraftSeriesKey> requested, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<VerifiedDraftSeriesReference>?>(
                requested.Select(x => new VerifiedDraftSeriesReference(
                    x.SkuId, x.WarehouseId, "EA")).ToArray());

        public async Task<bool?> CanAccessAsync(Guid tenantId, Guid actorId,
            Guid legalEntityId, IReadOnlyList<DraftSeriesKey> series,
            CancellationToken cancellationToken)
        {
            if (BeforeAccessResult is not null) await BeforeAccessResult();
            return Scope;
        }
    }

    private sealed class RollbackStoreSpy : IRollbackDraftStore
    {
        public int Calls { get; private set; }
        public Task<RollbackDraftResult> CreateAsync(Guid tenantId, Guid legalEntityId,
            Guid sourceRevisionId, Guid actorId, string reason, string requestKey,
            int expectedSourceStateVersion, DateTimeOffset occurredAt,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new RollbackDraftResult(RollbackDraftOutcome.Created));
        }
    }

    [Fact]
    public async Task Rollback_CreatePermissionMissing_DoesNotCallStore()
    {
        var spy = new RollbackStoreSpy();
        var handler = new RollbackDemandRevisionHandler(new RollbackDraftAuthority(),
            spy, TimeProvider.System);
        var response = await handler.Handle(new RollbackDemandRevisionCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            false, "reason", "request-key", 3), default);
        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
        Assert.Equal(0, spy.Calls);
    }

    private static (RollbackDraftMongoStore Store, RollbackReadAuthority Read,
        RollbackDraftAuthority Draft) Rollback(
        DemandPlanningMongoContext context, PublishedRevisionMongoStore published,
        ManualDraftMongoStore drafts, PublishedRevisionManifest source)
    {
        var read = new RollbackReadAuthority
        {
            Series = source.SelectedSeries.Select(x => (x.SkuId, x.WarehouseId))
                .Concat(source.ExcludedSeries.Select(x => (x.SkuId, x.WarehouseId)))
                .ToArray()
        };
        var authority = new RollbackDraftAuthority();
        return (new RollbackDraftMongoStore(context, published, drafts, read, authority),
            read, authority);
    }

    [ManualDraftMongoFact]
    public async Task Rollback_HttpJwtRouteCreatesDraftAndRejectsMissingPermission()
    {
        var (drafts, published, context) = Open();
        var sourceDraft = Draft(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), FirstWeek);
        var reviewer = Guid.NewGuid();
        await Approve(drafts, sourceDraft, sourceDraft.CreatedBy, reviewer);
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, sourceDraft, reviewer)).Outcome);
        var source = (await published.ReadSnapshotAsync(sourceDraft.TenantId,
            sourceDraft.LegalEntityId, sourceDraft.Id, default))!.Value.Manifest;
        var (_, read, assignment) = Rollback(context, published, drafts, source);
        await using var host = await ReadHttpHost.StartAsync(sourceDraft,
            contentAuthority: read, assignmentAuthority: assignment);
        var path = $"/api/v2/demand/revisions/{sourceDraft.Id:D}/rollback-drafts";
        var actor = Guid.NewGuid();
        using (var denied = CommandRequest(host, path, sourceDraft, actor,
            "demand.plans.read", "http-denied", new
            {
                reason = "Reopen manual source",
                expectedSourceStateVersion = source.StateVersion
            }))
        using (var response = await host.Client.SendAsync(denied))
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var allowed = CommandRequest(host, path, sourceDraft, actor,
            "demand.drafts.create", "http-rollback", new
            {
                reason = "Reopen manual source",
                expectedSourceStateVersion = source.StateVersion
            });
        using var created = await host.Client.SendAsync(allowed);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("isSuccessful").GetBoolean());
        var newId = json.RootElement.GetProperty("data").GetProperty("revisionId").GetGuid();
        Assert.NotEqual(sourceDraft.Id, newId);
        Assert.NotNull(await drafts.ReadAsync(sourceDraft.TenantId,
            sourceDraft.LegalEntityId, newId, default));
    }

    [ManualDraftMongoFact]
    public async Task Rollback_Published_ClonesManualWeeksAndRequiresFullReview()
    {
        var (drafts, published, context) = Open();
        var tenant = Guid.NewGuid(); var legalEntity = Guid.NewGuid();
        var sourceCreator = Guid.NewGuid(); var reviewer = Guid.NewGuid();
        var newCreator = Guid.NewGuid();
        var sourceDraft = Draft(tenant, legalEntity, sourceCreator, FirstWeek);
        await Approve(drafts, sourceDraft, sourceCreator, reviewer);
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, sourceDraft, reviewer)).Outcome);
        var source = (await published.ReadSnapshotAsync(tenant, legalEntity,
            sourceDraft.Id, default))!.Value;
        var (store, _, _) = Rollback(context, published, drafts, source.Manifest);
        var result = await store.CreateAsync(tenant, legalEntity, sourceDraft.Id,
            newCreator, "Return to checked baseline", "rollback-1",
            source.Manifest.StateVersion, Now.AddMinutes(4), default);
        Assert.Equal(RollbackDraftOutcome.Created, result.Outcome);
        var copy = Assert.IsType<DemandRevisionDraft>(result.Draft);
        Assert.NotEqual(sourceDraft.Id, copy.Id);
        Assert.Equal(sourceDraft.PlanningCycleId, copy.PlanningCycleId);
        Assert.Equal(DemandRevisionState.Draft, copy.State);
        Assert.Equal(newCreator, copy.CreatedBy);
        Assert.Equal(source.Manifest.Weeks.Select(x => (x.WeekStart, x.WeekEnd)),
            copy.Weeks.Select(x => (x.WeekStart, x.WeekEnd)));
        Assert.Equal(source.Parts[0].Rows.Select(x => x.Quantity),
            copy.Series[0].Weeks.Select(x => x.Quantity));
        Assert.All(copy.Series[0].Weeks, week =>
        {
            Assert.Equal(DraftWeekSource.Manual, week.Source);
            Assert.Equal(sourceCreator, week.ManualActorId);
            Assert.Equal("Verified manual fixture", week.ManualReason);
            Assert.Equal(Now, week.ManualAt);
        });
        var durable = await drafts.ReadAsync(tenant, legalEntity, copy.Id, default);
        Assert.NotNull(durable);
        Assert.Equal(sourceCreator, durable.Series[0].Weeks[0].ManualActorId);
        var audit = await context.ManualDraftAudit.Find(x => x.TenantId == tenant &&
            x.LegalEntityId == legalEntity && x.RevisionId == copy.Id)
            .SingleAsync();
        Assert.Equal(sourceDraft.Id, audit.SourceRevisionId);
        Assert.Equal(DemandRevisionState.Published, audit.SourceState);
        Assert.Equal(source.Manifest.Checksum, audit.SourceChecksum);
        Assert.Equal(newCreator, audit.ActorId);
        Assert.Equal("Return to checked baseline", audit.Reason);
        Assert.NotNull(audit.CopiedScopeJson);
        Assert.Equal(DemandRevisionState.Published,
            (await published.ReadSnapshotAsync(tenant, legalEntity,
                sourceDraft.Id, default))!.Value.Manifest.State);
        Assert.Equal(source.Manifest.Checksum,
            (await published.ReadSnapshotAsync(tenant, legalEntity,
                sourceDraft.Id, default))!.Value.Manifest.Checksum);
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await drafts.TransitionReviewAsync(tenant, legalEntity, copy.Id,
                newCreator, DraftReviewAction.Submitted, null, "submit-copy",
                0, 0, Now.AddMinutes(5), default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.SeparationDenied,
            (await drafts.TransitionReviewAsync(tenant, legalEntity, copy.Id,
                newCreator, DraftReviewAction.Approved, "self", "self-copy",
                0, 1, Now.AddMinutes(6), default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.SeparationDenied,
            (await drafts.TransitionReviewAsync(tenant, legalEntity, copy.Id,
                sourceCreator, DraftReviewAction.Approved, "source author",
                "source-author-approve", 0, 1, Now.AddMinutes(6), default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await drafts.TransitionReviewAsync(tenant, legalEntity, copy.Id,
                reviewer, DraftReviewAction.Approved, "checked", "approve-copy",
                0, 1, Now.AddMinutes(6), default)).Outcome);
        Assert.Equal(PublishOutcome.SeparationDenied,
            (await published.PublishAsync(tenant, legalEntity, copy.Id,
                sourceCreator, "source-author-publish", 0, 2,
                Now.AddMinutes(7), default)).Outcome);
        Assert.Equal(PublishOutcome.SeparationDenied,
            (await published.PublishAsync(tenant, legalEntity, copy.Id,
                newCreator, "creator-publish", 0, 2,
                Now.AddMinutes(7), default)).Outcome);
        Assert.Equal(PublishOutcome.Published,
            (await published.PublishAsync(tenant, legalEntity, copy.Id, reviewer,
                "publish-copy", 0, 2, Now.AddMinutes(7), default)).Outcome);
        var publishedCopy = (await published.ReadSnapshotAsync(tenant, legalEntity,
            copy.Id, default))!.Value.Manifest;
        Assert.Contains(sourceCreator, publishedCopy.SignificantEditorIds);
    }

    [ManualDraftMongoFact]
    public async Task Rollback_Superseded_ReplaysAndRejectsDifferentReasonOrScope()
    {
        var (drafts, published, context) = Open();
        var tenant = Guid.NewGuid(); var legalEntity = Guid.NewGuid();
        var creator = Guid.NewGuid(); var reviewer = Guid.NewGuid();
        var first = Draft(tenant, legalEntity, creator, FirstWeek);
        var second = Draft(tenant, legalEntity, creator, FirstWeek,
            quantity: 20m);
        await Approve(drafts, first, creator, reviewer);
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, first, reviewer)).Outcome);
        await Approve(drafts, second, creator, reviewer);
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, second, reviewer, "publish-second")).Outcome);
        var source = (await published.ReadSnapshotAsync(tenant, legalEntity,
            first.Id, default))!.Value.Manifest;
        Assert.Equal(DemandRevisionState.Superseded, source.State);
        var (store, read, access) = Rollback(context, published, drafts, source);
        var actor = Guid.NewGuid();
        var result = await store.CreateAsync(tenant, legalEntity, first.Id, actor,
            "restore prior", "rollback-old", source.StateVersion,
            Now.AddMinutes(8), default);
        Assert.Equal(RollbackDraftOutcome.Created, result.Outcome);
        var again = await store.CreateAsync(tenant, legalEntity, first.Id, actor,
            "restore prior", "rollback-old", source.StateVersion,
            Now.AddMinutes(9), default);
        Assert.Equal(RollbackDraftOutcome.Replayed, again.Outcome);
        Assert.Equal(result.Draft!.Id, again.Draft!.Id);
        Assert.Equal(RollbackDraftOutcome.Conflict,
            (await store.CreateAsync(tenant, legalEntity, first.Id, actor,
                "different", "rollback-old", source.StateVersion,
                Now.AddMinutes(9), default)).Outcome);
        read.ScopeVerified = false;
        Assert.Equal(RollbackDraftOutcome.NotFound,
            (await store.CreateAsync(tenant, legalEntity, first.Id, actor,
                "restore prior", "other", source.StateVersion,
                Now.AddMinutes(9), default)).Outcome);
        read.ScopeVerified = true;
        access.Scope = false;
        Assert.Equal(RollbackDraftOutcome.NotFound,
            (await store.CreateAsync(tenant, legalEntity, first.Id, actor,
                "restore prior", "other", source.StateVersion,
                Now.AddMinutes(9), default)).Outcome);
    }

    [ManualDraftMongoFact]
    public async Task Rollback_ReplayAfterSupersedeUsesOriginalRequest_InvalidateBlocksLaterReplay()
    {
        var (drafts, published, context) = Open();
        var tenant = Guid.NewGuid(); var legalEntity = Guid.NewGuid();
        var creator = Guid.NewGuid(); var reviewer = Guid.NewGuid();
        var first = Draft(tenant, legalEntity, creator, FirstWeek);
        var replacement = Draft(tenant, legalEntity, creator, FirstWeek, quantity: 20m);
        await Approve(drafts, first, creator, reviewer);
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, first, reviewer)).Outcome);
        var before = (await published.ReadSnapshotAsync(tenant, legalEntity,
            first.Id, default))!.Value.Manifest;
        var sourceAuditBefore = await context.ManualDraftAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.RevisionId == first.Id);
        var publicationAuditBefore = await context.ManualDraftPublicationAudit
            .CountDocumentsAsync(x => x.TenantId == tenant &&
                x.LegalEntityId == legalEntity && x.RevisionId == first.Id);
        var (store, read, _) = Rollback(context, published, drafts, before);
        var actor = Guid.NewGuid();
        var created = await store.CreateAsync(tenant, legalEntity, first.Id,
            actor, "return verified manual plan", "same-after-state-change",
            before.StateVersion, Now.AddMinutes(5), default);
        Assert.Equal(RollbackDraftOutcome.Created, created.Outcome);
        var afterCreate = (await published.ReadSnapshotAsync(tenant, legalEntity,
            first.Id, default))!.Value.Manifest;
        Assert.Equal(before.RollbackFenceVersion + 1, afterCreate.RollbackFenceVersion);
        Assert.Equal(before.Checksum, afterCreate.Checksum);
        Assert.Equal(before.State, afterCreate.State);
        Assert.Equal(sourceAuditBefore, await context.ManualDraftAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.RevisionId == first.Id));
        Assert.Equal(publicationAuditBefore, await context.ManualDraftPublicationAudit
            .CountDocumentsAsync(x => x.TenantId == tenant &&
                x.LegalEntityId == legalEntity && x.RevisionId == first.Id));
        await Approve(drafts, replacement, creator, reviewer);
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, replacement, reviewer, "replace-for-replay")).Outcome);
        var superseded = (await published.ReadSnapshotAsync(tenant, legalEntity,
            first.Id, default))!.Value.Manifest;
        Assert.Equal(DemandRevisionState.Superseded, superseded.State);
        Assert.Equal(before.Checksum, superseded.Checksum);
        var replay = await store.CreateAsync(tenant, legalEntity, first.Id,
            actor, "return verified manual plan", "same-after-state-change",
            before.StateVersion, Now.AddMinutes(6), default);
        Assert.Equal(RollbackDraftOutcome.Replayed, replay.Outcome);
        Assert.Equal(created.Draft!.Id, replay.Draft!.Id);
        Assert.Equal(afterCreate.RollbackFenceVersion,
            (await published.ReadSnapshotAsync(tenant, legalEntity,
                first.Id, default))!.Value.Manifest.RollbackFenceVersion);
        Assert.Equal(RollbackDraftOutcome.Conflict,
            (await store.CreateAsync(tenant, legalEntity, first.Id,
                actor, "different request", "same-after-state-change",
                before.StateVersion, Now.AddMinutes(7), default)).Outcome);
        read.ScopeVerified = false;
        Assert.Equal(RollbackDraftOutcome.NotFound,
            (await store.CreateAsync(tenant, legalEntity, first.Id,
                actor, "return verified manual plan", "same-after-state-change",
                before.StateVersion, Now.AddMinutes(7), default)).Outcome);
        read.ScopeVerified = true;
        Assert.Equal(InvalidationOutcome.Invalidated,
            (await Invalidate(Invalidator(first, drafts, published, context), first,
                stateVersion: superseded.StateVersion)).Outcome);
        Assert.Equal(RollbackDraftOutcome.InvalidSource,
            (await store.CreateAsync(tenant, legalEntity, first.Id,
                actor, "return verified manual plan", "same-after-state-change",
                before.StateVersion, Now.AddMinutes(8), default)).Outcome);
        read.ScopeVerified = false;
        Assert.Equal(RollbackDraftOutcome.NotFound,
            (await store.CreateAsync(tenant, legalEntity, first.Id,
                actor, "return verified manual plan", "same-after-state-change",
                before.StateVersion, Now.AddMinutes(8), default)).Outcome);
        Assert.Equal(1, await context.ManualDraftManifests.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.SourceRevisionId == first.Id));
    }

    [ManualDraftMongoFact]
    public async Task Rollback_ConcurrentInvalidationAfterReadCannotCreateDraft() =>
        await Rollback_ConcurrentSourceTransitionCannotCreateDraft(invalidate: true);

    [ManualDraftMongoFact]
    public async Task Rollback_ConcurrentSupersedeAfterReadCannotCreateDraft() =>
        await Rollback_ConcurrentSourceTransitionCannotCreateDraft(invalidate: false);

    private static async Task Rollback_ConcurrentSourceTransitionCannotCreateDraft(bool invalidate)
    {
        var (drafts, published, context) = Open();
        var tenant = Guid.NewGuid(); var legalEntity = Guid.NewGuid();
        var creator = Guid.NewGuid(); var reviewer = Guid.NewGuid();
        var first = Draft(tenant, legalEntity, creator, FirstWeek);
        await Approve(drafts, first, creator, reviewer);
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, first, reviewer)).Outcome);
        var before = (await published.ReadSnapshotAsync(tenant, legalEntity,
            first.Id, default))!.Value.Manifest;
        var (store, _, access) = Rollback(context, published, drafts, before);
        var readReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        access.BeforeAccessResult = async () =>
        {
            readReached.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10));
        };
        var pending = store.CreateAsync(tenant, legalEntity, first.Id,
            Guid.NewGuid(), "race with source state", "race-source",
            before.StateVersion, Now.AddMinutes(5), default);
        try
        {
            await readReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if (invalidate)
            {
                Assert.Equal(InvalidationOutcome.Invalidated,
                    (await Invalidate(Invalidator(first, drafts, published, context),
                        first)).Outcome);
            }
            else
            {
                var replacement = Draft(tenant, legalEntity, creator,
                    FirstWeek, quantity: 20m);
                await Approve(drafts, replacement, creator, reviewer);
                Assert.Equal(PublishOutcome.Published,
                    (await Publish(published, replacement, reviewer,
                        "race-replacement")).Outcome);
            }
        }
        finally
        {
            release.TrySetResult();
        }
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Contains(result.Outcome, new[] { RollbackDraftOutcome.Conflict,
            RollbackDraftOutcome.InvalidSource });
        Assert.Equal(0, await context.ManualDraftManifests.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.SourceRevisionId == first.Id));
        var after = (await published.ReadSnapshotAsync(tenant, legalEntity,
            first.Id, default))!.Value.Manifest;
        Assert.Equal(before.Checksum, after.Checksum);
        Assert.Equal(invalidate ? DemandRevisionState.Invalidated :
            DemandRevisionState.Superseded, after.State);
    }

    [ManualDraftMongoFact]
    public async Task Rollback_ExcludedSeriesRetainsLineageWithoutInventingWeeks()
    {
        var (drafts, published, context) = Open();
        var tenant = Guid.NewGuid(); var legalEntity = Guid.NewGuid();
        var creator = Guid.NewGuid(); var reviewer = Guid.NewGuid();
        var sourceExcluder = Guid.NewGuid();
        var sourceDraft = Draft(tenant, legalEntity, creator,
            FirstWeek, secondSeries: true);
        Assert.Equal(ManualDraftStoreOutcome.Created,
            (await drafts.CreateAsync(sourceDraft, "source-create", default)).Outcome);
        var excluded = sourceDraft.Series[1];
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await drafts.ExcludeSeriesAsync(tenant, legalEntity, sourceDraft.Id,
                sourceExcluder, excluded.SkuId, excluded.WarehouseId,
                "No approved warehouse coverage", "exclude-source", 0, 0,
                Now.AddMinutes(1), default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await drafts.TransitionReviewAsync(tenant, legalEntity, sourceDraft.Id,
                creator, DraftReviewAction.Submitted, null, "source-submit",
                1, 0, Now.AddMinutes(2), default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await drafts.TransitionReviewAsync(tenant, legalEntity, sourceDraft.Id,
                reviewer, DraftReviewAction.Approved, "checked", "source-approve",
                1, 1, Now.AddMinutes(3), default)).Outcome);
        Assert.Equal(PublishOutcome.Published,
            (await published.PublishAsync(tenant, legalEntity, sourceDraft.Id,
                reviewer, "source-publish", 1, 2, Now.AddMinutes(4), default)).Outcome);
        var source = (await published.ReadSnapshotAsync(tenant, legalEntity,
            sourceDraft.Id, default))!.Value;
        Assert.Single(source.Parts);
        Assert.Single(source.Manifest.ExcludedSeries);
        var (store, _, _) = Rollback(context, published, drafts, source.Manifest);
        var result = await store.CreateAsync(tenant, legalEntity, sourceDraft.Id,
            Guid.NewGuid(), "copy selected and exclusion lineage", "rollback-exclusion",
            source.Manifest.StateVersion, Now.AddMinutes(5), default);
        Assert.Equal(RollbackDraftOutcome.Created, result.Outcome);
        var draft = Assert.IsType<DemandRevisionDraft>(result.Draft);
        Assert.Equal(2, draft.Series.Count);
        Assert.Single(draft.Exclusions);
        Assert.Equal(source.Manifest.ExcludedSeries[0], draft.Exclusions[0]);
        var excludedCopy = Assert.Single(draft.Series, x => x.Exclusion is not null);
        Assert.Empty(excludedCopy.Weeks);
        Assert.Single(draft.Series, x => x.Exclusion is null);
        var reloaded = await drafts.ReadAsync(tenant, legalEntity, draft.Id, default);
        Assert.NotNull(reloaded);
        Assert.Empty(Assert.Single(reloaded.Series, x => x.Exclusion is not null).Weeks);
        Assert.Equal(source.Manifest.ExcludedSeries[0], Assert.Single(reloaded.Exclusions));
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await drafts.TransitionReviewAsync(tenant, legalEntity, draft.Id,
                draft.CreatedBy, DraftReviewAction.Submitted, null,
                "submit-with-exclusion", 0, 0, Now.AddMinutes(6), default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.SeparationDenied,
            (await drafts.TransitionReviewAsync(tenant, legalEntity, draft.Id,
                sourceExcluder, DraftReviewAction.Approved, "source exclusion actor",
                "excluder-approve", 0, 1, Now.AddMinutes(7), default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await drafts.TransitionReviewAsync(tenant, legalEntity, draft.Id,
                reviewer, DraftReviewAction.Approved, "independent", "approve-exclusion",
                0, 1, Now.AddMinutes(7), default)).Outcome);
        Assert.Equal(PublishOutcome.SeparationDenied,
            (await published.PublishAsync(tenant, legalEntity, draft.Id,
                sourceExcluder, "excluder-publish", 0, 2,
                Now.AddMinutes(8), default)).Outcome);
        Assert.Equal(PublishOutcome.Published,
            (await published.PublishAsync(tenant, legalEntity, draft.Id,
                reviewer, "publish-exclusion-copy", 0, 2,
                Now.AddMinutes(8), default)).Outcome);
        Assert.Contains(sourceExcluder,
            (await published.ReadSnapshotAsync(tenant, legalEntity, draft.Id,
                default))!.Value.Manifest.SignificantEditorIds);
    }

    [ManualDraftMongoFact]
    public async Task Rollback_NewSignificantEditorAndOriginalAuthorRemainSeparated()
    {
        var (drafts, published, context) = Open();
        var tenant = Guid.NewGuid(); var legalEntity = Guid.NewGuid();
        var originalAuthor = Guid.NewGuid(); var reviewer = Guid.NewGuid();
        var editor = Guid.NewGuid();
        var sourceDraft = Draft(tenant, legalEntity, originalAuthor, FirstWeek);
        await Approve(drafts, sourceDraft, originalAuthor, reviewer);
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, sourceDraft, reviewer)).Outcome);
        var source = (await published.ReadSnapshotAsync(tenant, legalEntity,
            sourceDraft.Id, default))!.Value.Manifest;
        var (store, _, _) = Rollback(context, published, drafts, source);
        var copy = Assert.IsType<DemandRevisionDraft>((await store.CreateAsync(
            tenant, legalEntity, sourceDraft.Id, Guid.NewGuid(), "new review",
            "editor-copy", source.StateVersion, Now.AddMinutes(5), default)).Draft);
        var series = copy.Series[0];
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await drafts.EditWeekAsync(tenant, legalEntity, copy.Id,
                editor, series.SkuId, series.WarehouseId, 1,
                DraftWeekValueKind.Known, 12m, "manual correction",
                "editor-change", 0, Now.AddMinutes(6), default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await drafts.TransitionReviewAsync(tenant, legalEntity, copy.Id,
                copy.CreatedBy, DraftReviewAction.Submitted, null,
                "editor-submit", 1, 0, Now.AddMinutes(7), default)).Outcome);
        foreach (var actor in new[] { originalAuthor, editor })
            Assert.Equal(ManualDraftStoreOutcome.SeparationDenied,
                (await drafts.TransitionReviewAsync(tenant, legalEntity, copy.Id,
                    actor, DraftReviewAction.Approved, "self approval",
                    $"deny-approve-{actor:D}", 1, 1,
                    Now.AddMinutes(8), default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await drafts.TransitionReviewAsync(tenant, legalEntity, copy.Id,
                reviewer, DraftReviewAction.Approved, "independent",
                "editor-approve", 1, 1, Now.AddMinutes(8), default)).Outcome);
        foreach (var actor in new[] { originalAuthor, editor })
            Assert.Equal(PublishOutcome.SeparationDenied,
                (await published.PublishAsync(tenant, legalEntity, copy.Id,
                    actor, $"deny-publish-{actor:D}", 1, 2,
                    Now.AddMinutes(9), default)).Outcome);
        Assert.Equal(PublishOutcome.Published,
            (await published.PublishAsync(tenant, legalEntity, copy.Id,
                reviewer, "editor-independent-publish", 1, 2,
                Now.AddMinutes(9), default)).Outcome);
        Assert.Equal(1, await context.ManualDraftPublicationAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.RevisionId == copy.Id && x.NewState == DemandRevisionState.Published));
    }

    [ManualDraftMongoFact]
    public async Task Rollback_MissingOriginalWeekActorCannotBeLoadedForReview()
    {
        var (drafts, published, context) = Open();
        var tenant = Guid.NewGuid(); var legalEntity = Guid.NewGuid();
        var sourceAuthor = Guid.NewGuid(); var reviewer = Guid.NewGuid();
        var sourceDraft = Draft(tenant, legalEntity, sourceAuthor, FirstWeek);
        await Approve(drafts, sourceDraft, sourceAuthor, reviewer);
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, sourceDraft, reviewer)).Outcome);
        var source = (await published.ReadSnapshotAsync(tenant, legalEntity,
            sourceDraft.Id, default))!.Value.Manifest;
        var (store, _, _) = Rollback(context, published, drafts, source);
        var copy = Assert.IsType<DemandRevisionDraft>((await store.CreateAsync(
            tenant, legalEntity, sourceDraft.Id, Guid.NewGuid(), "verified source",
            "bad-origin-copy", source.StateVersion, Now.AddMinutes(5), default)).Draft);
        var part = await context.ManualDraftSeriesParts.Find(x => x.TenantId == tenant &&
            x.LegalEntityId == legalEntity && x.RevisionId == copy.Id).SingleAsync();
        var originalJson = Assert.IsType<string>(part.SourceSeriesJson);
        Assert.Contains(sourceAuthor.ToString("D"), originalJson,
            StringComparison.OrdinalIgnoreCase);
        part.SourceSeriesJson = originalJson.Replace(sourceAuthor.ToString("D"),
            Guid.Empty.ToString("D"), StringComparison.OrdinalIgnoreCase);
        var manifest = await context.ManualDraftManifests.Find(x => x.TenantId == tenant &&
            x.LegalEntityId == legalEntity && x.Id == copy.Id).SingleAsync();
        manifest.CreateFingerprint = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(manifest.CycleJson + "\n" + part.SourceSeriesJson +
                "\n" + manifest.SourceRevisionId!.Value.ToString("D") + "\n" +
                manifest.SourceState!.Value + "\n" +
                manifest.SourceStateVersion!.Value + "\n" +
                manifest.SourceChecksum + "\n" + manifest.CopiedScopeJson +
                "\n" + manifest.CreatedBy.ToString("D") + "\n" +
                manifest.CreationReason.Trim())));
        var audit = await context.ManualDraftAudit.Find(x => x.TenantId == tenant &&
            x.LegalEntityId == legalEntity && x.RevisionId == copy.Id).SingleAsync();
        audit.Fingerprint = manifest.CreateFingerprint;
        await context.ManualDraftSeriesParts.ReplaceOneAsync(x => x.Id == part.Id &&
            x.TenantId == tenant && x.LegalEntityId == legalEntity, part);
        await context.ManualDraftManifests.ReplaceOneAsync(x => x.Id == manifest.Id &&
            x.TenantId == tenant && x.LegalEntityId == legalEntity, manifest);
        await context.ManualDraftAudit.ReplaceOneAsync(x => x.Id == audit.Id &&
            x.TenantId == tenant && x.LegalEntityId == legalEntity, audit);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            drafts.ReadAsync(tenant, legalEntity, copy.Id, default));
    }

    [ManualDraftMongoFact]
    public async Task Rollback_InvalidatedAndWrongTenantAreRejected_ConcurrentCreateIsSingle()
    {
        var (drafts, published, context) = Open();
        var tenant = Guid.NewGuid(); var legalEntity = Guid.NewGuid();
        var creator = Guid.NewGuid(); var reviewer = Guid.NewGuid();
        var sourceDraft = Draft(tenant, legalEntity, creator, FirstWeek);
        await Approve(drafts, sourceDraft, creator, reviewer);
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, sourceDraft, reviewer)).Outcome);
        var source = (await published.ReadSnapshotAsync(tenant, legalEntity,
            sourceDraft.Id, default))!.Value.Manifest;
        var (store, _, _) = Rollback(context, published, drafts, source);
        var actor = Guid.NewGuid();
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            store.CreateAsync(tenant, legalEntity, sourceDraft.Id, actor,
                "same command", "parallel-copy", source.StateVersion,
                Now.AddMinutes(5), default)));
        Assert.Contains(results, x => x.Outcome == RollbackDraftOutcome.Created);
        Assert.All(results, x => Assert.Contains(x.Outcome,
            new[] { RollbackDraftOutcome.Created, RollbackDraftOutcome.Replayed,
                RollbackDraftOutcome.Conflict }));
        Assert.Equal(1, await context.ManualDraftManifests.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.SourceRevisionId == sourceDraft.Id));
        Assert.Equal(RollbackDraftOutcome.NotFound,
            (await store.CreateAsync(Guid.NewGuid(), legalEntity, sourceDraft.Id,
                actor, "wrong tenant", "wrong-tenant", source.StateVersion,
                Now.AddMinutes(6), default)).Outcome);
        Assert.Equal(RollbackDraftOutcome.NotFound,
            (await store.CreateAsync(tenant, Guid.NewGuid(), sourceDraft.Id,
                actor, "wrong company", "wrong-company", source.StateVersion,
                Now.AddMinutes(6), default)).Outcome);
        var invalidator = Invalidator(sourceDraft, drafts, published, context);
        Assert.Equal(InvalidationOutcome.Invalidated,
            (await Invalidate(invalidator, sourceDraft)).Outcome);
        Assert.Equal(RollbackDraftOutcome.InvalidSource,
            (await store.CreateAsync(tenant, legalEntity, sourceDraft.Id,
                actor, "invalid source", "invalid-copy", 4,
                Now.AddMinutes(7), default)).Outcome);
    }

    [ManualDraftMongoFact]
    public async Task Rollback_AuditWriteFailure_RollsBackAllNewDocuments()
    {
        var (drafts, published, context) = Open();
        var tenant = Guid.NewGuid(); var legalEntity = Guid.NewGuid();
        var creator = Guid.NewGuid(); var reviewer = Guid.NewGuid();
        var sourceDraft = Draft(tenant, legalEntity, creator, FirstWeek);
        Assert.Equal(ManualDraftStoreOutcome.Created,
            (await drafts.CreateAsync(sourceDraft, "source-create", default)).Outcome);
        var firstSeries = sourceDraft.Series[0];
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await drafts.EditWeekAsync(tenant, legalEntity, sourceDraft.Id,
                creator, firstSeries.SkuId, firstSeries.WarehouseId, 1,
                DraftWeekValueKind.Known, 11m, "source adjustment",
                "rollback-audit", 0, Now.AddMinutes(1), default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await drafts.TransitionReviewAsync(tenant, legalEntity, sourceDraft.Id,
                creator, DraftReviewAction.Submitted, null, "source-submit",
                1, 0, Now.AddMinutes(2), default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await drafts.TransitionReviewAsync(tenant, legalEntity, sourceDraft.Id,
                reviewer, DraftReviewAction.Approved, "checked", "source-approve",
                1, 1, Now.AddMinutes(3), default)).Outcome);
        Assert.Equal(PublishOutcome.Published,
            (await published.PublishAsync(tenant, legalEntity, sourceDraft.Id,
                reviewer, "source-publish", 1, 2, Now.AddMinutes(4), default)).Outcome);
        var source = (await published.ReadSnapshotAsync(tenant, legalEntity,
            sourceDraft.Id, default))!.Value.Manifest;
        var (store, _, _) = Rollback(context, published, drafts, source);
        var indexName = "rollback-audit-failure-" + tenant.ToString("N");
        var keys = Builders<ManualDraftAuditRecord>.IndexKeys
            .Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId)
            .Ascending(x => x.RequestKey);
        await context.ManualDraftAudit.Indexes.CreateOneAsync(
            new CreateIndexModel<ManualDraftAuditRecord>(keys,
                new CreateIndexOptions<ManualDraftAuditRecord>
                {
                    Name = indexName, Unique = true,
                    PartialFilterExpression = Builders<ManualDraftAuditRecord>
                        .Filter.Eq(x => x.TenantId, tenant)
                }));
        try
        {
            var result = await store.CreateAsync(tenant, legalEntity,
                sourceDraft.Id, Guid.NewGuid(), "copy with audit failure",
                "rollback-audit", source.StateVersion, Now.AddMinutes(5), default);
            Assert.Equal(RollbackDraftOutcome.AuthorityUnavailable, result.Outcome);
            Assert.Equal(0, await context.ManualDraftManifests.CountDocumentsAsync(x =>
                x.TenantId == tenant && x.SourceRevisionId == sourceDraft.Id));
            Assert.Equal(0, await context.ManualDraftSeriesParts.CountDocumentsAsync(x =>
                x.TenantId == tenant && x.RevisionId != sourceDraft.Id));
            Assert.Equal(0, await context.ManualDraftAudit.CountDocumentsAsync(x =>
                x.TenantId == tenant && x.SourceRevisionId == sourceDraft.Id));
        }
        finally
        {
            await context.ManualDraftAudit.Indexes.DropOneAsync(indexName);
        }
    }

    [ManualDraftMongoFact]
    public async Task Rollback_SourcePermissionOrAuthorityLoss_DoesNotCreateDraft()
    {
        var (drafts, published, context) = Open();
        var tenant = Guid.NewGuid(); var legalEntity = Guid.NewGuid();
        var creator = Guid.NewGuid(); var reviewer = Guid.NewGuid();
        var sourceDraft = Draft(tenant, legalEntity, creator, FirstWeek);
        await Approve(drafts, sourceDraft, creator, reviewer);
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, sourceDraft, reviewer)).Outcome);
        var source = (await published.ReadSnapshotAsync(tenant, legalEntity,
            sourceDraft.Id, default))!.Value.Manifest;
        var (store, read, _) = Rollback(context, published, drafts, source);
        var actor = Guid.NewGuid();
        read.HasReadPermission = false;
        Assert.Equal(RollbackDraftOutcome.PermissionDenied,
            (await store.CreateAsync(tenant, legalEntity, sourceDraft.Id, actor,
                "not permitted", "no-read", source.StateVersion,
                Now.AddMinutes(5), default)).Outcome);
        read.HasReadPermission = true;
        read.SourceAvailable = false;
        Assert.Equal(RollbackDraftOutcome.AuthorityUnavailable,
            (await store.CreateAsync(tenant, legalEntity, sourceDraft.Id, actor,
                "authority down", "no-source", source.StateVersion,
                Now.AddMinutes(5), default)).Outcome);
        Assert.Equal(0, await context.ManualDraftManifests.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.SourceRevisionId == sourceDraft.Id));
    }

    [ManualDraftMongoFact]
    public async Task Rollback_UnverifiableMethodOrigin_FailsClosed()
    {
        var (drafts, published, context) = Open();
        var tenant = Guid.NewGuid(); var legalEntity = Guid.NewGuid();
        var creator = Guid.NewGuid(); var reviewer = Guid.NewGuid();
        var sourceDraft = Draft(tenant, legalEntity, creator, FirstWeek);
        await Approve(drafts, sourceDraft, creator, reviewer);
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, sourceDraft, reviewer)).Outcome);
        var source = (await published.ReadSnapshotAsync(tenant, legalEntity,
            sourceDraft.Id, default))!.Value.Manifest;
        var part = await context.PublishedRevisionParts.Find(x =>
            x.TenantId == tenant && x.RevisionId == sourceDraft.Id).SingleAsync();
        part.Rows[0] = part.Rows[0] with { Source = DraftWeekSource.Method };
        await context.PublishedRevisionParts.ReplaceOneAsync(x => x.Id == part.Id &&
            x.TenantId == tenant && x.LegalEntityId == legalEntity, part);
        var (store, _, _) = Rollback(context, published, drafts, source);
        var result = await store.CreateAsync(tenant, legalEntity, sourceDraft.Id,
            Guid.NewGuid(), "unverifiable method", "method-copy",
            source.StateVersion, Now.AddMinutes(5), default);
        Assert.Equal(RollbackDraftOutcome.InvalidSnapshot, result.Outcome);
        Assert.Equal(0, await context.ManualDraftManifests.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.SourceRevisionId == sourceDraft.Id));
    }
}

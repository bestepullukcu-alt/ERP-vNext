using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using Diten.PlanningService.Persistence.Features.DemandPlanning;
using MongoDB.Driver;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed partial class PublishedRevisionMongoIntegrationTests
{
    [ManualDraftMongoFact]
    public async Task AcD15_ApprovedContentChangeRequiresReopenAndFreshIndependentApproval()
    {
        var (drafts, published, context) = Open();
        var tenant = Guid.NewGuid(); var legalEntity = Guid.NewGuid();
        var planner = Guid.NewGuid(); var reviewer = Guid.NewGuid();
        var draft = Draft(tenant, legalEntity, planner, FirstWeek, secondSeries: true);
        await Approve(drafts, draft, planner, reviewer);
        var first = draft.Series[0]; var second = draft.Series[1];
        Assert.Equal(ManualDraftStoreOutcome.Conflict,
            (await drafts.EditWeekAsync(tenant, legalEntity, draft.Id, planner,
                first.SkuId, first.WarehouseId, 1, DraftWeekValueKind.Known,
                12m, "cannot edit approved", "blocked-approved-edit", 0,
                Now.AddMinutes(4), default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Conflict,
            (await drafts.ExcludeSeriesAsync(tenant, legalEntity, draft.Id,
                planner, second.SkuId, second.WarehouseId, "cannot exclude approved",
                "blocked-approved-exclusion", 0, 2,
                Now.AddMinutes(4), default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.ScopeDenied,
            (await drafts.TransitionReviewAsync(Guid.NewGuid(), legalEntity,
                draft.Id, planner, DraftReviewAction.Reopened, "scope denied",
                "wrong-tenant-reopen", 0, 2, Now.AddMinutes(4), default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.ScopeDenied,
            (await drafts.TransitionReviewAsync(tenant, Guid.NewGuid(),
                draft.Id, planner, DraftReviewAction.Reopened, "scope denied",
                "wrong-company-reopen", 0, 2, Now.AddMinutes(4), default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await drafts.TransitionReviewAsync(tenant, legalEntity, draft.Id,
                planner, DraftReviewAction.Reopened, "important quantity change",
                "reopen-for-edit", 0, 2, Now.AddMinutes(4), default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Replayed,
            (await drafts.TransitionReviewAsync(tenant, legalEntity, draft.Id,
                planner, DraftReviewAction.Reopened, "important quantity change",
                "reopen-for-edit", 0, 2, Now.AddMinutes(5), default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Conflict,
            (await drafts.TransitionReviewAsync(tenant, legalEntity, draft.Id,
                planner, DraftReviewAction.Reopened, "different reason",
                "reopen-for-edit", 0, 2, Now.AddMinutes(5), default)).Outcome);
        var reopened = Assert.IsType<DemandRevisionDraft>(
            await drafts.ReadAsync(tenant, legalEntity, draft.Id, default));
        Assert.Equal(DemandRevisionState.Draft, reopened.State);
        Assert.Null(reopened.ReviewedBy);
        Assert.Equal(3, reopened.StateVersion);
        Assert.Equal(PublishOutcome.Conflict,
            (await published.PublishAsync(tenant, legalEntity, draft.Id,
                reviewer, "stale-approved", 0, 2, Now.AddMinutes(5), default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await drafts.EditWeekAsync(tenant, legalEntity, draft.Id, planner,
                first.SkuId, first.WarehouseId, 1, DraftWeekValueKind.Known,
                12m, "important quantity change", "edit-after-reopen", 0,
                Now.AddMinutes(5), default)).Outcome);
        Assert.Equal(PublishOutcome.Conflict,
            (await published.PublishAsync(tenant, legalEntity, draft.Id,
                reviewer, "stale-content-version", 0, 2,
                Now.AddMinutes(6), default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await drafts.TransitionReviewAsync(tenant, legalEntity, draft.Id,
                planner, DraftReviewAction.Submitted, null, "resubmit",
                1, 3, Now.AddMinutes(6), default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Conflict,
            (await drafts.TransitionReviewAsync(tenant, legalEntity, draft.Id,
                reviewer, DraftReviewAction.Approved, "old approval",
                "stale-review", 0, 1, Now.AddMinutes(7), default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await drafts.TransitionReviewAsync(tenant, legalEntity, draft.Id,
                reviewer, DraftReviewAction.Approved, "new independent review",
                "fresh-approval", 1, 4, Now.AddMinutes(7), default)).Outcome);
        Assert.Equal(PublishOutcome.Conflict,
            (await published.PublishAsync(tenant, legalEntity, draft.Id,
                reviewer, "old-version-after-review", 0, 2,
                Now.AddMinutes(8), default)).Outcome);
        Assert.Equal(PublishOutcome.Published,
            (await published.PublishAsync(tenant, legalEntity, draft.Id,
                reviewer, "fresh-version-publish", 1, 5,
                Now.AddMinutes(8), default)).Outcome);
        Assert.Equal(5, await context.ManualDraftReviewAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.RevisionId == draft.Id));
        Assert.Equal(1, await context.ManualDraftPublicationAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.RevisionId == draft.Id && x.NewState == DemandRevisionState.Published));
    }

    [ManualDraftMongoFact]
    public async Task AcD15_InReviewExclusionAtomicallyInvalidatesReview()
    {
        var (drafts, published, context) = Open();
        var tenant = Guid.NewGuid(); var legalEntity = Guid.NewGuid();
        var planner = Guid.NewGuid(); var reviewer = Guid.NewGuid();
        var draft = Draft(tenant, legalEntity, planner, FirstWeek, secondSeries: true);
        Assert.Equal(ManualDraftStoreOutcome.Created,
            (await drafts.CreateAsync(draft, "create-for-exclusion", default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await drafts.TransitionReviewAsync(tenant, legalEntity, draft.Id,
                planner, DraftReviewAction.Submitted, null, "submit-for-exclusion",
                0, 0, Now.AddMinutes(1), default)).Outcome);
        var excluded = draft.Series[1];
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await drafts.ExcludeSeriesAsync(tenant, legalEntity, draft.Id,
                planner, excluded.SkuId, excluded.WarehouseId, "scope changed",
                "exclude-in-review", 0, 1, Now.AddMinutes(2), default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Replayed,
            (await drafts.ExcludeSeriesAsync(tenant, legalEntity, draft.Id,
                planner, excluded.SkuId, excluded.WarehouseId, "scope changed",
                "exclude-in-review", 0, 1, Now.AddMinutes(3), default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Conflict,
            (await drafts.TransitionReviewAsync(tenant, legalEntity, draft.Id,
                reviewer, DraftReviewAction.Approved, "stale review",
                "stale-exclusion-approval", 0, 1, Now.AddMinutes(3), default)).Outcome);
        Assert.Equal(PublishOutcome.Conflict,
            (await published.PublishAsync(tenant, legalEntity, draft.Id,
                reviewer, "stale-exclusion-publish", 0, 1,
                Now.AddMinutes(3), default)).Outcome);
        var current = Assert.IsType<DemandRevisionDraft>(
            await drafts.ReadAsync(tenant, legalEntity, draft.Id, default));
        Assert.Equal(DemandRevisionState.Draft, current.State);
        Assert.Equal(1, current.Version);
        Assert.Equal(2, current.StateVersion);
        Assert.Null(current.ReviewedBy);
        Assert.Single(current.Exclusions);
        Assert.Equal(0, await context.ManualDraftReviewSlots.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.RevisionId == draft.Id));
        Assert.Equal(2, await context.ManualDraftReviewAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.RevisionId == draft.Id));
    }

    [ManualDraftMongoFact]
    public async Task AcD15_ReopenAuditFailureLeavesOldApprovalAndSlotIntact()
    {
        var (drafts, published, context) = Open();
        var tenant = Guid.NewGuid(); var legalEntity = Guid.NewGuid();
        var planner = Guid.NewGuid(); var reviewer = Guid.NewGuid();
        var draft = Draft(tenant, legalEntity, planner, FirstWeek);
        await Approve(drafts, draft, planner, reviewer);
        var indexName = "acd15-reopen-audit-" + tenant.ToString("N");
        await context.ManualDraftReviewAudit.Indexes.CreateOneAsync(
            new CreateIndexModel<ManualDraftReviewAuditRecord>(
                Builders<ManualDraftReviewAuditRecord>.IndexKeys
                    .Ascending(x => x.TenantId).Ascending(x => x.ActorId),
                new CreateIndexOptions<ManualDraftReviewAuditRecord>
                {
                    Name = indexName, Unique = true,
                    PartialFilterExpression = Builders<ManualDraftReviewAuditRecord>
                        .Filter.Eq(x => x.TenantId, tenant)
                }));
        try
        {
            Assert.Equal(ManualDraftStoreOutcome.Conflict,
                (await drafts.TransitionReviewAsync(tenant, legalEntity, draft.Id,
                    planner, DraftReviewAction.Reopened, "must rework",
                    "reopen-audit-failure", 0, 2, Now.AddMinutes(4), default)).Outcome);
            var stillApproved = Assert.IsType<DemandRevisionDraft>(
                await drafts.ReadAsync(tenant, legalEntity, draft.Id, default));
            Assert.Equal(DemandRevisionState.Approved, stillApproved.State);
            Assert.Equal(reviewer, stillApproved.ReviewedBy);
            Assert.Equal(2, stillApproved.StateVersion);
            Assert.Equal(1, await context.ManualDraftReviewSlots.CountDocumentsAsync(x =>
                x.TenantId == tenant && x.LegalEntityId == legalEntity &&
                x.RevisionId == draft.Id));
            Assert.Equal(2, await context.ManualDraftReviewAudit.CountDocumentsAsync(x =>
                x.TenantId == tenant && x.LegalEntityId == legalEntity &&
                x.RevisionId == draft.Id));
        }
        finally
        {
            await context.ManualDraftReviewAudit.Indexes.DropOneAsync(indexName);
        }
        Assert.Equal(PublishOutcome.Published,
            (await published.PublishAsync(tenant, legalEntity, draft.Id,
                reviewer, "publish-after-failed-reopen", 0, 2,
                Now.AddMinutes(5), default)).Outcome);
    }

    [ManualDraftMongoFact]
    public async Task AcD15_ConcurrentReopenAllowsOnlyOneStateTransition()
    {
        var (drafts, published, context) = Open();
        var tenant = Guid.NewGuid(); var legalEntity = Guid.NewGuid();
        var planner = Guid.NewGuid(); var reviewer = Guid.NewGuid();
        var draft = Draft(tenant, legalEntity, planner, FirstWeek);
        await Approve(drafts, draft, planner, reviewer);

        var attempts = await Task.WhenAll(
            drafts.TransitionReviewAsync(tenant, legalEntity, draft.Id,
                planner, DraftReviewAction.Reopened, "first rework",
                "concurrent-reopen-a", 0, 2, Now.AddMinutes(4), default),
            drafts.TransitionReviewAsync(tenant, legalEntity, draft.Id,
                planner, DraftReviewAction.Reopened, "second rework",
                "concurrent-reopen-b", 0, 2, Now.AddMinutes(4), default));
        Assert.Single(attempts, x => x.Outcome == ManualDraftStoreOutcome.Changed);
        Assert.Single(attempts, x => x.Outcome == ManualDraftStoreOutcome.Conflict);
        var reopened = Assert.IsType<DemandRevisionDraft>(
            await drafts.ReadAsync(tenant, legalEntity, draft.Id, default));
        Assert.Equal(DemandRevisionState.Draft, reopened.State);
        Assert.Equal(3, reopened.StateVersion);
        Assert.Null(reopened.ReviewedBy);
        Assert.Equal(0, await context.ManualDraftReviewSlots.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.RevisionId == draft.Id));
        Assert.Equal(3, await context.ManualDraftReviewAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.RevisionId == draft.Id));
        Assert.Equal(PublishOutcome.Conflict,
            (await published.PublishAsync(tenant, legalEntity, draft.Id,
                reviewer, "publish-after-reopen-race", 0, 2,
                Now.AddMinutes(5), default)).Outcome);
    }
}

using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using Diten.PlanningService.Persistence.Features.DemandPlanning;
using MongoDB.Driver;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed partial class PublishedRevisionMongoIntegrationTests
{
    private sealed class InvalidationAuthority(DemandRevisionDraft draft,
        bool sourceAvailable = true, bool permitted = true,
        bool scopeVerified = true, bool materialImpact = true,
        string? businessImpact = "Verified material MRP impact in test evidence")
        : IInternalInvalidationAuthority, IInternalInvalidatedHistoryAuthority
    {
        private (Guid SkuId, string WarehouseId)[] Series => draft.Series
            .Where(x => x.Exclusion is null)
            .Select(x => (x.SkuId, x.WarehouseId)).ToArray();

        public Task<InvalidationAuthorityEvidence> VerifyAsync(Guid tenantId,
            Guid legalEntityId, Guid revisionId, Guid actorId,
            InvalidationImpactCode impactCode, string evidenceReference,
            CancellationToken cancellationToken) => Task.FromResult(new
            InvalidationAuthorityEvidence(sourceAvailable, permitted,
                scopeVerified && draft.TenantId == tenantId &&
                draft.LegalEntityId == legalEntityId && draft.Id == revisionId,
                materialImpact, tenantId, legalEntityId, revisionId, impactCode,
                evidenceReference, Series, businessImpact));

        public Task<InvalidatedHistoryAuthorityEvidence> VerifyAsync(Guid tenantId,
            Guid legalEntityId, Guid revisionId, Guid actorId,
            CancellationToken cancellationToken) => Task.FromResult(new
            InvalidatedHistoryAuthorityEvidence(sourceAvailable, permitted,
                scopeVerified && draft.TenantId == tenantId &&
                draft.LegalEntityId == legalEntityId && draft.Id == revisionId,
                tenantId, legalEntityId, revisionId, Series));
    }

    private static InvalidationMongoStore Invalidator(DemandRevisionDraft draft,
        ManualDraftMongoStore drafts, PublishedRevisionMongoStore published,
        DemandPlanningMongoContext context, InvalidationAuthority? authority = null) =>
        new(context, drafts, published, authority ?? new InvalidationAuthority(draft),
            new TestClock(Now.AddMinutes(4)));

    private static Task<InvalidationResult> Invalidate(InvalidationMongoStore store,
        DemandRevisionDraft draft, string key = "invalidate", string reason = "Wrong source quantity",
        int stateVersion = 3, InvalidationImpactCode code = InvalidationImpactCode.ContentIntegrity,
        string evidenceReference = "verified-mrp-impact-fixture") =>
        store.InvalidateAsync(draft.TenantId, draft.LegalEntityId, draft.Id,
            Guid.NewGuid(), code, reason, evidenceReference, key, 0, stateVersion,
            default);

    [ManualDraftMongoFact]
    public async Task Invalidate_CurrentPublished_IsAtomicImmutableAndAuditOnly()
    {
        var (draft, drafts, published, context) = await PublishedFixture();
        var authority = new InvalidationAuthority(draft);
        var invalidator = Invalidator(draft, drafts, published, context, authority);
        var actor = Guid.NewGuid();
        var before = (await published.ReadSnapshotAsync(draft.TenantId,
            draft.LegalEntityId, draft.Id, default))!.Value;
        var result = await invalidator.InvalidateAsync(draft.TenantId,
            draft.LegalEntityId, draft.Id, actor, InvalidationImpactCode.ContentIntegrity,
            "Wrong source quantity", "verified-mrp-impact-fixture", "invalidate", 0, 3, default);
        Assert.Equal(InvalidationOutcome.Invalidated, result.Outcome);
        Assert.Equal(4, result.StateVersion);
        var after = (await published.ReadSnapshotAsync(draft.TenantId,
            draft.LegalEntityId, draft.Id, default))!.Value;
        Assert.Equal(DemandRevisionState.Invalidated, after.Manifest.State);
        Assert.Equal(4, after.Manifest.StateVersion);
        Assert.Equal(before.Manifest.Checksum, after.Manifest.Checksum);
        Assert.Equal(before.Manifest.ExpectedPartCount, after.Manifest.ExpectedPartCount);
        Assert.Equal(before.Manifest.ExpectedRowCount, after.Manifest.ExpectedRowCount);
        Assert.Equal(DemandRevisionState.Invalidated,
            (await drafts.ReadAsync(draft.TenantId, draft.LegalEntityId, draft.Id, default))!.State);
        var slot = await context.PublishedBaselineSlots.Find(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.PlanningPeriodKey == draft.PlanningPeriodKey).SingleAsync();
        Assert.Equal(draft.Id, slot.RevisionId);
        Assert.False(slot.IsAvailable);
        var audit = await context.ManualDraftPublicationAudit.Find(x =>
            x.TenantId == draft.TenantId && x.RevisionId == draft.Id &&
            x.NewState == DemandRevisionState.Invalidated).SingleAsync();
        Assert.Equal("Wrong source quantity", audit.Reason);
        Assert.Equal("verified-mrp-impact-fixture", audit.EvidenceReference);
        Assert.Equal(actor, audit.ActorId);
        Assert.Equal(4, audit.StateVersionAfter);
        Assert.NotEqual(default, audit.OccurredAt);
        Assert.Equal(1, await context.AuditEntries.CountDocumentsAsync(x =>
            x.TenantId == draft.TenantId && x.Action == "Invalidated"));
        Assert.Equal(1, await context.Outbox.CountDocumentsAsync(x =>
            x.TenantId == draft.TenantId && x.RevisionId == draft.Id &&
            x.EventType == "demand.revision.invalidated.v2"));
        await AssertAllReads(Reader(published, draft), draft, actor,
            SnapshotReadOutcome.StateDenied);
        var history = await new InvalidatedHistoryReader(published, authority).ReadAsync(
            draft.TenantId, draft.LegalEntityId, draft.Id, actor, default);
        Assert.Equal(SnapshotReadOutcome.Found, history.Outcome);
        Assert.Contains("planlama için kullanılamaz", history.Data!.Warning);
        Assert.Equal(104, history.Data.Rows.Count);
    }

    [ManualDraftMongoFact]
    public async Task Invalidate_ReplayIsSingleEffect_ChangedContentConflicts()
    {
        var (draft, drafts, published, context) = await PublishedFixture(secondSeries: false);
        var store = Invalidator(draft, drafts, published, context);
        var actor = Guid.NewGuid();
        Task<InvalidationResult> Call(string reason) => store.InvalidateAsync(
            draft.TenantId, draft.LegalEntityId, draft.Id, actor,
            InvalidationImpactCode.ContentIntegrity, reason,
            "verified-mrp-impact-fixture", "same-key", 0, 3, default);
        Assert.Equal(InvalidationOutcome.Invalidated, (await Call("bad unit")).Outcome);
        Assert.Equal(InvalidationOutcome.Replayed, (await Call("bad unit")).Outcome);
        Assert.Equal(InvalidationOutcome.Conflict, (await Call("different reason")).Outcome);
        Assert.Equal(1, await context.ManualDraftPublicationAudit.CountDocumentsAsync(x =>
            x.TenantId == draft.TenantId && x.RevisionId == draft.Id &&
            x.NewState == DemandRevisionState.Invalidated));
        Assert.Equal(1, await context.Outbox.CountDocumentsAsync(x =>
            x.TenantId == draft.TenantId && x.RevisionId == draft.Id &&
            x.EventType == "demand.revision.invalidated.v2"));
    }

    [ManualDraftMongoFact]
    public async Task Invalidate_RequiresIndependentMaterialEvidenceAndPermission()
    {
        var (draft, drafts, published, context) = await PublishedFixture(secondSeries: false);
        foreach (var (authority, expected) in new[]
        {
            (new InvalidationAuthority(draft, materialImpact: false),
                InvalidationOutcome.MaterialImpactMissing),
            (new InvalidationAuthority(draft, permitted: false),
                InvalidationOutcome.PermissionDenied),
            (new InvalidationAuthority(draft, scopeVerified: false),
                InvalidationOutcome.NotFound),
            (new InvalidationAuthority(draft, sourceAvailable: false),
                InvalidationOutcome.AuthorityUnavailable)
        })
        {
            var result = await Invalidate(Invalidator(draft, drafts, published,
                context, authority), draft);
            Assert.Equal(expected, result.Outcome);
        }
        Assert.Equal(InvalidationOutcome.InvalidRequest,
            (await Invalidate(Invalidator(draft, drafts, published, context),
                draft, code: InvalidationImpactCode.ForecastDeviation)).Outcome);
        Assert.Equal(DemandRevisionState.Published,
            (await published.ReadSnapshotAsync(draft.TenantId,
                draft.LegalEntityId, draft.Id, default))!.Value.Manifest.State);
        Assert.Equal(0, await context.AuditEntries.CountDocumentsAsync(x =>
            x.TenantId == draft.TenantId && x.Action == "Invalidated"));
    }

    [ManualDraftMongoFact]
    public async Task Invalidate_Superseded_DoesNotChangeCurrentSlot()
    {
        var (draft, drafts, published, context) = await PublishedFixture(secondSeries: false);
        var replacement = Draft(draft.TenantId, draft.LegalEntityId,
            Guid.NewGuid(), FirstWeek);
        await Approve(drafts, replacement, replacement.CreatedBy, Guid.NewGuid());
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, replacement, Guid.NewGuid())).Outcome);
        Assert.Equal(InvalidationOutcome.Invalidated,
            (await Invalidate(Invalidator(draft, drafts, published, context),
                draft, stateVersion: 4)).Outcome);
        Assert.Equal(DemandRevisionState.Invalidated,
            (await published.ReadSnapshotAsync(draft.TenantId,
                draft.LegalEntityId, draft.Id, default))!.Value.Manifest.State);
        var slot = await context.PublishedBaselineSlots.Find(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.PlanningPeriodKey == draft.PlanningPeriodKey).SingleAsync();
        Assert.True(slot.IsAvailable);
        Assert.Equal(replacement.Id, slot.RevisionId);
    }

    [ManualDraftMongoFact]
    public async Task Invalidate_CurrentLeavesNoFallback_NewApprovedNeedsNormalPublish()
    {
        var (draft, drafts, published, context) = await PublishedFixture(secondSeries: false);
        var old = Draft(draft.TenantId, draft.LegalEntityId,
            Guid.NewGuid(), FirstWeek);
        // Replace the original, then invalidate the current replacement.
        await Approve(drafts, old, old.CreatedBy, Guid.NewGuid());
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, old, Guid.NewGuid())).Outcome);
        Assert.Equal(InvalidationOutcome.Invalidated,
            (await Invalidate(Invalidator(old, drafts, published, context), old)).Outcome);
        var slot = await context.PublishedBaselineSlots.Find(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.PlanningPeriodKey == draft.PlanningPeriodKey).SingleAsync();
        Assert.False(slot.IsAvailable);
        Assert.Equal(old.Id, slot.RevisionId);
        Assert.Equal(DemandRevisionState.Superseded,
            (await published.ReadSnapshotAsync(draft.TenantId,
                draft.LegalEntityId, draft.Id, default))!.Value.Manifest.State);
        var next = Draft(draft.TenantId, draft.LegalEntityId,
            Guid.NewGuid(), FirstWeek);
        await Approve(drafts, next, next.CreatedBy, Guid.NewGuid());
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, next, Guid.NewGuid())).Outcome);
        slot = await context.PublishedBaselineSlots.Find(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.PlanningPeriodKey == draft.PlanningPeriodKey).SingleAsync();
        Assert.True(slot.IsAvailable);
        Assert.Equal(next.Id, slot.RevisionId);
        Assert.Equal(DemandRevisionState.Invalidated,
            (await published.ReadSnapshotAsync(old.TenantId,
                old.LegalEntityId, old.Id, default))!.Value.Manifest.State);
    }

    [ManualDraftMongoFact]
    public async Task Invalidate_OutboxCollision_RollsBackStateSlotAndAudit()
    {
        var (draft, drafts, published, context) = await PublishedFixture(secondSeries: false);
        await published.EnsureIndexesAsync();
        await context.Outbox.InsertOneAsync(new DemandOutboxMessage
        {
            TenantId = draft.TenantId, LegalEntityId = draft.LegalEntityId,
            RevisionId = Guid.NewGuid(),
            EventId = InvalidationMongoStore.DeterministicEventId(
                draft.TenantId, draft.LegalEntityId, draft.Id),
            EventType = "test-blocker", Payload = "{}", OccurredAt = Now
        });
        Assert.Equal(InvalidationOutcome.Conflict,
            (await Invalidate(Invalidator(draft, drafts, published, context), draft)).Outcome);
        Assert.Equal(DemandRevisionState.Published,
            (await published.ReadSnapshotAsync(draft.TenantId,
                draft.LegalEntityId, draft.Id, default))!.Value.Manifest.State);
        Assert.True((await context.PublishedBaselineSlots.Find(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.PlanningPeriodKey == draft.PlanningPeriodKey).SingleAsync()).IsAvailable);
        Assert.Equal(0, await context.ManualDraftPublicationAudit.CountDocumentsAsync(x =>
            x.TenantId == draft.TenantId && x.RevisionId == draft.Id &&
            x.NewState == DemandRevisionState.Invalidated));
        Assert.Equal(0, await context.AuditEntries.CountDocumentsAsync(x =>
            x.TenantId == draft.TenantId && x.Action == "Invalidated"));
    }

    [ManualDraftMongoFact]
    public async Task Invalidate_AuditCollision_RollsBackStateSlotAndOutbox()
    {
        var (draft, drafts, published, context) = await PublishedFixture(secondSeries: false);
        var indexName = $"mod0188_invalidate_audit_{draft.TenantId:N}";
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
            Assert.Equal(InvalidationOutcome.Conflict,
                (await Invalidate(Invalidator(draft, drafts, published, context), draft)).Outcome);
            Assert.Equal(DemandRevisionState.Published,
                (await published.ReadSnapshotAsync(draft.TenantId,
                    draft.LegalEntityId, draft.Id, default))!.Value.Manifest.State);
            Assert.True((await context.PublishedBaselineSlots.Find(x =>
                x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
                x.PlanningPeriodKey == draft.PlanningPeriodKey).SingleAsync()).IsAvailable);
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
    public async Task Invalidate_ConcurrentSameRequest_CreatesAtMostOneEffect()
    {
        var (draft, drafts, published, context) = await PublishedFixture(secondSeries: false);
        var store = Invalidator(draft, drafts, published, context);
        var actor = Guid.NewGuid();
        Task<InvalidationResult> Call() => store.InvalidateAsync(draft.TenantId,
            draft.LegalEntityId, draft.Id, actor, InvalidationImpactCode.ContentIntegrity,
            "Wrong source quantity", "verified-mrp-impact-fixture", "race-key",
            0, 3, default);
        var results = await Task.WhenAll(Call(), Call());
        Assert.Single(results, x => x.Outcome == InvalidationOutcome.Invalidated);
        Assert.Contains(results, x => x.Outcome is InvalidationOutcome.Replayed or
            InvalidationOutcome.Conflict);
        Assert.Equal(1, await context.ManualDraftPublicationAudit.CountDocumentsAsync(x =>
            x.TenantId == draft.TenantId && x.RevisionId == draft.Id &&
            x.NewState == DemandRevisionState.Invalidated));
        Assert.Equal(1, await context.Outbox.CountDocumentsAsync(x =>
            x.TenantId == draft.TenantId && x.RevisionId == draft.Id &&
            x.EventType == "demand.revision.invalidated.v2"));
    }

    [ManualDraftMongoFact]
    public async Task Invalidate_StaleVersionAndWrongScope_LeaveCurrentUntouched()
    {
        var (draft, drafts, published, context) = await PublishedFixture(secondSeries: false);
        var store = Invalidator(draft, drafts, published, context);
        Assert.Equal(InvalidationOutcome.Conflict,
            (await Invalidate(store, draft, stateVersion: 2)).Outcome);
        Assert.Equal(InvalidationOutcome.NotFound,
            (await store.InvalidateAsync(Guid.NewGuid(), draft.LegalEntityId,
                draft.Id, Guid.NewGuid(), InvalidationImpactCode.ContentIntegrity,
                "Wrong source quantity", "verified-mrp-impact-fixture",
                "wrong-tenant", 0, 3, default)).Outcome);
        Assert.Equal(InvalidationOutcome.NotFound,
            (await store.InvalidateAsync(draft.TenantId, Guid.NewGuid(),
                draft.Id, Guid.NewGuid(), InvalidationImpactCode.ContentIntegrity,
                "Wrong source quantity", "verified-mrp-impact-fixture",
                "wrong-company", 0, 3, default)).Outcome);
        Assert.Equal(DemandRevisionState.Published,
            (await published.ReadSnapshotAsync(draft.TenantId,
                draft.LegalEntityId, draft.Id, default))!.Value.Manifest.State);
        Assert.Equal(0, await context.ManualDraftPublicationAudit.CountDocumentsAsync(x =>
            x.TenantId == draft.TenantId && x.RevisionId == draft.Id &&
            x.NewState == DemandRevisionState.Invalidated));
    }

    [ManualDraftMongoFact]
    public async Task InvalidatedHistory_RequiresAuditAuthorityAndRevisionScope()
    {
        var (draft, drafts, published, context) = await PublishedFixture(secondSeries: false);
        Assert.Equal(InvalidationOutcome.Invalidated,
            (await Invalidate(Invalidator(draft, drafts, published, context), draft)).Outcome);
        foreach (var (authority, expected) in new[]
        {
            (new InvalidationAuthority(draft, permitted: false),
                SnapshotReadOutcome.PermissionDenied),
            (new InvalidationAuthority(draft, scopeVerified: false),
                SnapshotReadOutcome.NotFound),
            (new InvalidationAuthority(draft, sourceAvailable: false),
                SnapshotReadOutcome.AuthorityUnavailable)
        })
        {
            var result = await new InvalidatedHistoryReader(published, authority).ReadAsync(
                draft.TenantId, draft.LegalEntityId, draft.Id, Guid.NewGuid(), default);
            Assert.Equal(expected, result.Outcome);
            Assert.Null(result.Data);
        }
    }

    [ManualDraftMongoFact]
    public async Task Invalidate_RacingNewPublish_NeverRevivesOrPartiallyChangesOldRevision()
    {
        var (draft, drafts, published, context) = await PublishedFixture(secondSeries: false);
        var replacement = Draft(draft.TenantId, draft.LegalEntityId,
            Guid.NewGuid(), FirstWeek);
        await Approve(drafts, replacement, replacement.CreatedBy, Guid.NewGuid());
        var invalidate = Invalidate(Invalidator(draft, drafts, published, context), draft);
        var publish = Publish(published, replacement, Guid.NewGuid());
        await Task.WhenAll(invalidate, publish);
        Assert.Contains(invalidate.Result.Outcome,
            new[] { InvalidationOutcome.Invalidated, InvalidationOutcome.Conflict });
        Assert.Contains(publish.Result.Outcome,
            new[] { PublishOutcome.Published, PublishOutcome.Conflict });
        var old = (await published.ReadSnapshotAsync(draft.TenantId,
            draft.LegalEntityId, draft.Id, default))!.Value.Manifest;
        var slot = await context.PublishedBaselineSlots.Find(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.PlanningPeriodKey == draft.PlanningPeriodKey).SingleAsync();
        if (old.State == DemandRevisionState.Published)
        {
            Assert.True(slot.IsAvailable);
            Assert.Equal(draft.Id, slot.RevisionId);
        }
        if (invalidate.Result.Outcome == InvalidationOutcome.Invalidated)
        {
            Assert.Equal(DemandRevisionState.Invalidated, old.State);
            Assert.NotEqual(draft.Id, slot.IsAvailable ? slot.RevisionId : Guid.Empty);
        }
        if (publish.Result.Outcome == PublishOutcome.Published)
        {
            Assert.True(slot.IsAvailable);
            Assert.Equal(replacement.Id, slot.RevisionId);
            Assert.Equal(DemandRevisionState.Published,
                (await published.ReadSnapshotAsync(replacement.TenantId,
                    replacement.LegalEntityId, replacement.Id, default))!.Value.Manifest.State);
        }
    }
}

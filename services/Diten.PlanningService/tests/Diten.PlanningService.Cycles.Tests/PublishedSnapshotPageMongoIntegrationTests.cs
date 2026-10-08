using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using Diten.PlanningService.Persistence.Features.DemandPlanning;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed partial class PublishedRevisionMongoIntegrationTests
{
    private sealed class ReadAuthority(IReadOnlyList<DemandRevisionDraft> authorizedDrafts,
        bool sourceAvailable = true, bool hasPermission = true, bool inScope = true)
        : IInternalSnapshotReadAuthority
    {
        public int Calls { get; private set; }

        public Task<SnapshotReadAuthorityEvidence> VerifyAsync(Guid tenantId,
            Guid legalEntityId, Guid revisionId, Guid actorId,
            CancellationToken cancellationToken)
        {
            Calls++;
            var draft = authorizedDrafts.SingleOrDefault(x =>
                x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                x.Id == revisionId);
            return Task.FromResult(new SnapshotReadAuthorityEvidence(sourceAvailable,
                hasPermission, inScope && draft is not null,
                tenantId, legalEntityId, revisionId,
                draft?.Series.Where(x => x.Exclusion is null)
                    .Select(x => (x.SkuId, x.WarehouseId)).ToArray() ?? []));
        }
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static PublishedSnapshotPageReader Reader(PublishedRevisionMongoStore published,
        DemandRevisionDraft draft, bool sourceAvailable = true,
        bool hasPermission = true, bool inScope = true,
        ReadAuthority? authority = null, TestClock? clock = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["DemandPlanning:SnapshotCursorSigningKey"] =
                    "MOD0188-test-only-cursor-signing-key-32-bytes"
            }).Build();
        return new PublishedSnapshotPageReader(published,
            authority ?? new ReadAuthority([draft], sourceAvailable, hasPermission, inScope),
            config, clock ?? new TestClock(Now));
    }

    private static async Task<(DemandRevisionDraft Draft, ManualDraftMongoStore Drafts,
        PublishedRevisionMongoStore Published, DemandPlanningMongoContext Context)>
        PublishedFixture(decimal quantity = 0m, bool secondSeries = true)
    {
        var (drafts, published, context) = Open();
        var creator = Guid.NewGuid();
        var reviewer = Guid.NewGuid();
        var draft = Draft(Guid.NewGuid(), Guid.NewGuid(), creator, FirstWeek,
            quantity: quantity, secondSeries: secondSeries);
        await Approve(drafts, draft, creator, reviewer);
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, draft, reviewer)).Outcome);
        return (draft, drafts, published, context);
    }

    private static async Task AssertAllReads(PublishedSnapshotPageReader reader,
        DemandRevisionDraft draft, Guid actorId, SnapshotReadOutcome expected)
    {
        var status = await reader.ReadStatusAsync(draft.TenantId,
            draft.LegalEntityId, draft.Id, actorId, default);
        var manifest = await reader.ReadManifestAsync(draft.TenantId,
            draft.LegalEntityId, draft.Id, actorId, default);
        var page = await reader.ReadPageAsync(draft.TenantId,
            draft.LegalEntityId, draft.Id, actorId, 10, null, default);
        Assert.Equal(expected, status.Outcome);
        Assert.Equal(expected, manifest.Outcome);
        Assert.Equal(expected, page.Outcome);
        Assert.Null(status.Data);
        Assert.Null(manifest.Data);
        Assert.Null(page.Data);
    }

    [ManualDraftMongoFact]
    public async Task Read_OutOfScopeHealthySupersededAndCorrupt_AllHideExistence()
    {
        var (first, drafts, published, context) = await PublishedFixture();
        var actor = Guid.NewGuid();
        await AssertAllReads(Reader(published, first, inScope: false),
            first, actor, SnapshotReadOutcome.NotFound);

        var reviewer = Guid.NewGuid();
        var replacement = Draft(first.TenantId, first.LegalEntityId,
            Guid.NewGuid(), FirstWeek);
        await Approve(drafts, replacement, replacement.CreatedBy, reviewer);
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, replacement, reviewer)).Outcome);
        await AssertAllReads(Reader(published, first, inScope: false),
            first, actor, SnapshotReadOutcome.NotFound);

        var part = await context.PublishedRevisionParts.Find(x =>
            x.TenantId == replacement.TenantId &&
            x.LegalEntityId == replacement.LegalEntityId &&
            x.RevisionId == replacement.Id).FirstAsync();
        part.Rows.RemoveAt(0);
        await context.PublishedRevisionParts.ReplaceOneAsync(x =>
            x.TenantId == replacement.TenantId &&
            x.LegalEntityId == replacement.LegalEntityId && x.Id == part.Id, part);
        await AssertAllReads(Reader(published, replacement, inScope: false),
            replacement, actor, SnapshotReadOutcome.NotFound);
        await AssertAllReads(Reader(published, replacement),
            replacement, actor, SnapshotReadOutcome.InvalidSnapshot);
    }

    [ManualDraftMongoFact]
    public async Task Read_MissingPermissionAndAuthorityFailureAreDistinctOnEverySurface()
    {
        var (draft, _, published, _) = await PublishedFixture();
        var actor = Guid.NewGuid();
        await AssertAllReads(Reader(published, draft, hasPermission: false),
            draft, actor, SnapshotReadOutcome.PermissionDenied);
        await AssertAllReads(Reader(published, draft, sourceAvailable: false),
            draft, actor, SnapshotReadOutcome.AuthorityUnavailable);
    }

    [ManualDraftMongoFact]
    public async Task Read_PagesStayOrderedAndPreserveExplicitZero()
    {
        var (draft, _, published, _) = await PublishedFixture();
        var authority = new ReadAuthority([draft]);
        var reader = Reader(published, draft, authority: authority);
        var actor = Guid.NewGuid();
        var first = await reader.ReadPageAsync(draft.TenantId, draft.LegalEntityId,
            draft.Id, actor, 17, null, default);
        Assert.Equal(SnapshotReadOutcome.Found, first.Outcome);
        Assert.Equal(17, first.Data!.Rows.Count);
        Assert.All(first.Data.Rows, row =>
        {
            Assert.Equal(DraftWeekValueKind.Known, row.ValueKind);
            Assert.Equal(0m, row.Quantity);
        });
        var rows = new List<InternalSnapshotRow>(first.Data.Rows);
        var next = first.Data.NextCursor;
        while (next is not null)
        {
            var page = await reader.ReadPageAsync(draft.TenantId,
                draft.LegalEntityId, draft.Id, actor, 17, next, default);
            Assert.Equal(SnapshotReadOutcome.Found, page.Outcome);
            rows.AddRange(page.Data!.Rows);
            next = page.Data.NextCursor;
        }
        Assert.Equal(104, rows.Count);
        Assert.Equal(104, rows.Select(row => (row.SkuId, row.WarehouseId,
            row.WeekNumber)).Distinct().Count());
        Assert.True(string.CompareOrdinal(rows[0].SkuId.ToString("D"),
            rows[^1].SkuId.ToString("D")) <= 0);
        Assert.Equal(7, authority.Calls);
        Assert.Equal(SnapshotReadOutcome.Found,
            (await reader.ReadStatusAsync(draft.TenantId, draft.LegalEntityId,
                draft.Id, actor, default)).Outcome);
        var manifest = await reader.ReadManifestAsync(draft.TenantId,
            draft.LegalEntityId, draft.Id, actor, default);
        Assert.Equal(52, manifest.Data!.Weeks.Count);
        Assert.Equal(104, manifest.Data.Status.ExpectedRowCount);
    }

    [ManualDraftMongoFact]
    public async Task Read_CursorCannotCrossScopeRevisionOrPageSize()
    {
        var (draft, _, published, _) = await PublishedFixture();
        var reader = Reader(published, draft);
        var actor = Guid.NewGuid();
        var first = await reader.ReadPageAsync(draft.TenantId, draft.LegalEntityId,
            draft.Id, actor, 10, null, default);
        var cursor = Assert.IsType<string>(first.Data!.NextCursor);
        Assert.Equal(SnapshotReadOutcome.NotFound,
            (await reader.ReadPageAsync(Guid.NewGuid(), draft.LegalEntityId,
                draft.Id, actor, 10, cursor, default)).Outcome);
        Assert.Equal(SnapshotReadOutcome.NotFound,
            (await reader.ReadPageAsync(draft.TenantId, Guid.NewGuid(),
                draft.Id, actor, 10, cursor, default)).Outcome);
        Assert.Equal(SnapshotReadOutcome.NotFound,
            (await reader.ReadPageAsync(draft.TenantId, draft.LegalEntityId,
                Guid.NewGuid(), actor, 10, cursor, default)).Outcome);
        Assert.Equal(SnapshotReadOutcome.InvalidCursor,
            (await reader.ReadPageAsync(draft.TenantId, draft.LegalEntityId,
                draft.Id, actor, 11, cursor, default)).Outcome);
        var other = Draft(draft.TenantId, draft.LegalEntityId, Guid.NewGuid(),
            FirstWeek.AddDays(7));
        var reviewer = Guid.NewGuid();
        var (drafts, _, _) = Open();
        await Approve(drafts, other, other.CreatedBy, reviewer);
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, other, reviewer)).Outcome);
        Assert.Equal(SnapshotReadOutcome.InvalidCursor,
            (await Reader(published, other).ReadPageAsync(draft.TenantId, draft.LegalEntityId,
                other.Id, actor, 10, cursor, default)).Outcome);
    }

    [ManualDraftMongoFact]
    public async Task Read_TamperedAndExpiredCursorFailClosed()
    {
        var (draft, _, published, _) = await PublishedFixture();
        var clock = new TestClock(Now);
        var reader = Reader(published, draft, clock: clock);
        var actor = Guid.NewGuid();
        var first = await reader.ReadPageAsync(draft.TenantId, draft.LegalEntityId,
            draft.Id, actor, 10, null, default);
        var cursor = Assert.IsType<string>(first.Data!.NextCursor);
        var index = cursor.IndexOf('.');
        var tampered = cursor[..(index + 1)] +
            (cursor[index + 1] == 'A' ? "B" : "A") + cursor[(index + 2)..];
        Assert.True(index > 0);
        Assert.Equal(SnapshotReadOutcome.InvalidCursor,
            (await reader.ReadPageAsync(draft.TenantId, draft.LegalEntityId,
                draft.Id, actor, 10, tampered, default)).Outcome);
        clock.Now = Now.AddMinutes(31);
        Assert.Equal(SnapshotReadOutcome.InvalidCursor,
            (await reader.ReadPageAsync(draft.TenantId, draft.LegalEntityId,
                draft.Id, actor, 10, cursor, default)).Outcome);
    }

    [ManualDraftMongoFact]
    public async Task Read_SupersededAndInvalidatedAreNotNormalPlanningReads()
    {
        var (first, drafts, published, context) = await PublishedFixture();
        var reviewer = Guid.NewGuid();
        var replacement = Draft(first.TenantId, first.LegalEntityId,
            Guid.NewGuid(), FirstWeek);
        await Approve(drafts, replacement, replacement.CreatedBy, reviewer);
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, replacement, reviewer)).Outcome);
        var reader = Reader(published, first);
        Assert.Equal(SnapshotReadOutcome.StateDenied,
            (await reader.ReadPageAsync(first.TenantId, first.LegalEntityId,
                first.Id, Guid.NewGuid(), 10, null, default)).Outcome);
        Assert.Equal(SnapshotReadOutcome.StateDenied,
            (await reader.ReadManifestAsync(first.TenantId, first.LegalEntityId,
                first.Id, Guid.NewGuid(), default)).Outcome);
        // Invalidation is not implemented: a deliberately altered state must
        // still never expose content through the normal planning reader.
        await context.PublishedRevisionManifests.UpdateOneAsync(x =>
            x.TenantId == first.TenantId && x.LegalEntityId == first.LegalEntityId &&
            x.RevisionId == replacement.Id,
            Builders<PublishedRevisionManifest>.Update.Set(x => x.State,
                DemandRevisionState.Invalidated));
        var denied = await Reader(published, replacement).ReadPageAsync(
            first.TenantId, first.LegalEntityId,
            replacement.Id, Guid.NewGuid(), 10, null, default);
        Assert.NotEqual(SnapshotReadOutcome.Found, denied.Outcome);
        Assert.Null(denied.Data);
    }

    [ManualDraftMongoFact]
    public async Task Read_AuthorityIsCheckedOnEveryPageAndFailsClosed()
    {
        var (draft, _, published, _) = await PublishedFixture();
        var actor = Guid.NewGuid();
        var first = await Reader(published, draft).ReadPageAsync(draft.TenantId,
            draft.LegalEntityId, draft.Id, actor, 10, null, default);
        var cursor = first.Data!.NextCursor;
        Assert.Equal(SnapshotReadOutcome.AuthorityUnavailable,
            (await Reader(published, draft, sourceAvailable: false).ReadPageAsync(
                draft.TenantId, draft.LegalEntityId, draft.Id, actor, 10,
                cursor, default)).Outcome);
        Assert.Equal(SnapshotReadOutcome.PermissionDenied,
            (await Reader(published, draft, hasPermission: false).ReadPageAsync(
                draft.TenantId, draft.LegalEntityId, draft.Id, actor, 10,
                cursor, default)).Outcome);
        Assert.Equal(SnapshotReadOutcome.NotFound,
            (await Reader(published, draft, inScope: false).ReadPageAsync(
                draft.TenantId, draft.LegalEntityId, draft.Id, actor, 10,
                cursor, default)).Outcome);
    }

    [ManualDraftMongoFact]
    public async Task Read_MissingPartOrDuplicateRowNeverReturnsPartialPage()
    {
        var (draft, _, published, context) = await PublishedFixture();
        var reader = Reader(published, draft);
        var actor = Guid.NewGuid();
        var first = await reader.ReadPageAsync(draft.TenantId, draft.LegalEntityId,
            draft.Id, actor, 7, null, default);
        Assert.Equal(SnapshotReadOutcome.Found, first.Outcome);
        var part = await context.PublishedRevisionParts.Find(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.RevisionId == draft.Id).FirstAsync();
        var originalRows = part.Rows.ToList();
        part.Rows[1] = part.Rows[0];
        await context.PublishedRevisionParts.ReplaceOneAsync(x => x.Id == part.Id &&
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId, part);
        Assert.Equal(SnapshotReadOutcome.InvalidSnapshot,
            (await reader.ReadPageAsync(draft.TenantId, draft.LegalEntityId,
                draft.Id, actor, 7, first.Data!.NextCursor, default)).Outcome);
        part.Rows = originalRows;
        await context.PublishedRevisionParts.ReplaceOneAsync(x => x.Id == part.Id &&
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId, part);
        await context.PublishedRevisionParts.DeleteOneAsync(x => x.Id == part.Id &&
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId);
        Assert.Equal(SnapshotReadOutcome.InvalidSnapshot,
            (await reader.ReadPageAsync(draft.TenantId, draft.LegalEntityId,
                draft.Id, actor, 7, null, default)).Outcome);
    }

    [ManualDraftMongoFact]
    public async Task Read_CorruptChecksumAndScopeFailClosed()
    {
        var (draft, _, published, context) = await PublishedFixture();
        var reader = Reader(published, draft);
        var actor = Guid.NewGuid();
        var manifest = await context.PublishedRevisionManifests.Find(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.RevisionId == draft.Id).FirstAsync();
        var checksum = manifest.Checksum;
        await context.PublishedRevisionManifests.UpdateOneAsync(x => x.Id == manifest.Id &&
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId,
            Builders<PublishedRevisionManifest>.Update.Set(x => x.Checksum, "BAD"));
        Assert.Equal(SnapshotReadOutcome.InvalidSnapshot,
            (await reader.ReadPageAsync(draft.TenantId, draft.LegalEntityId,
                draft.Id, actor, 10, null, default)).Outcome);
        await context.PublishedRevisionManifests.UpdateOneAsync(x => x.Id == manifest.Id &&
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId,
            Builders<PublishedRevisionManifest>.Update.Set(x => x.Checksum, checksum));
        var part = await context.PublishedRevisionParts.Find(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.RevisionId == draft.Id).FirstAsync();
        part.SkuId = Guid.NewGuid();
        await context.PublishedRevisionParts.ReplaceOneAsync(x => x.Id == part.Id &&
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId, part);
        Assert.Equal(SnapshotReadOutcome.InvalidSnapshot,
            (await reader.ReadPageAsync(draft.TenantId, draft.LegalEntityId,
                draft.Id, actor, 10, null, default)).Outcome);
    }

    [ManualDraftMongoFact]
    public async Task Read_UnexpectedPartOrMissingWeekNeverReturnsPage()
    {
        var (draft, _, published, context) = await PublishedFixture();
        var reader = Reader(published, draft);
        var actor = Guid.NewGuid();
        var part = await context.PublishedRevisionParts.Find(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.RevisionId == draft.Id).FirstAsync();
        var extra = new PublishedRevisionPart
        {
            TenantId = draft.TenantId, LegalEntityId = draft.LegalEntityId,
            RevisionId = draft.Id, SkuId = Guid.NewGuid(),
            WarehouseId = "OTHER-WH", BaseUomId = part.BaseUomId,
            Rows = part.Rows.ToList(), CreatedAt = Now
        };
        await context.PublishedRevisionParts.InsertOneAsync(extra);
        Assert.Equal(SnapshotReadOutcome.InvalidSnapshot,
            (await reader.ReadPageAsync(draft.TenantId, draft.LegalEntityId,
                draft.Id, actor, 10, null, default)).Outcome);
        await context.PublishedRevisionParts.DeleteOneAsync(x => x.Id == extra.Id &&
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId);
        part.Rows.RemoveAt(51);
        await context.PublishedRevisionParts.ReplaceOneAsync(x => x.Id == part.Id &&
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId, part);
        Assert.Equal(SnapshotReadOutcome.InvalidSnapshot,
            (await reader.ReadPageAsync(draft.TenantId, draft.LegalEntityId,
                draft.Id, actor, 10, null, default)).Outcome);
    }
}

using System.Net;
using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using Diten.PlanningService.Persistence.Features.DemandPlanning;
using MongoDB.Driver;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed partial class PublishedRevisionMongoIntegrationTests
{
    private sealed class PeriodAuthority(IReadOnlyList<DemandRevisionDraft> drafts,
        bool available = true, bool permitted = true, bool inScope = true,
        bool narrowSeries = false) : IInternalCurrentPublishedAuthority
    {
        public DemandRevisionDraft? SelectedDraft { get; set; }
        public Task<CurrentPublishedAuthorityEvidence> VerifyAsync(Guid tenantId,
            Guid legalEntityId, string planningPeriodKey, Guid actorId,
            CancellationToken cancellationToken)
        {
            var draft = (SelectedDraft is null ? drafts : [SelectedDraft])
                .FirstOrDefault(x => x.TenantId == tenantId &&
                x.LegalEntityId == legalEntityId &&
                x.PlanningPeriodKey == planningPeriodKey);
            var scope = draft?.Series.Where(x => x.Exclusion is null)
                .Select(x => (x.SkuId, x.WarehouseId)).ToArray() ?? [];
            if (narrowSeries && scope.Length > 0) scope = scope.Skip(1).ToArray();
            return Task.FromResult(new CurrentPublishedAuthorityEvidence(
                available, permitted, inScope && draft is not null,
                tenantId, legalEntityId, planningPeriodKey, scope));
        }
    }

    private sealed class MultiStatusAuthority(IReadOnlyList<DemandRevisionDraft> drafts)
        : IInternalRevisionStatusAuthority
    {
        public Task<RevisionStatusAuthorityEvidence> VerifyAsync(Guid tenantId,
            Guid legalEntityId, Guid revisionId, Guid actorId,
            CancellationToken cancellationToken)
        {
            var draft = drafts.FirstOrDefault(x => x.TenantId == tenantId &&
                x.LegalEntityId == legalEntityId && x.Id == revisionId);
            return Task.FromResult(new RevisionStatusAuthorityEvidence(true, true,
                draft is not null, tenantId, legalEntityId, revisionId,
                draft?.Series.Where(x => x.Exclusion is null)
                    .Select(x => (x.SkuId, x.WarehouseId)).ToArray() ?? []));
        }
    }

    private static CurrentPublishedRevisionReader PeriodReader(
        PublishedRevisionMongoStore published, DemandPlanningMongoContext context,
        IReadOnlyList<DemandRevisionDraft> drafts,
        IInternalCurrentPublishedAuthority? authority = null,
        IInternalAuthoritativeRevisionStatusReader? statusReader = null) =>
        new(context, published, authority ?? new PeriodAuthority(drafts),
            statusReader ?? new AuthoritativeRevisionStatusReader(context,
                new MultiStatusAuthority(drafts)));

    private static DemandRevisionDraft SameScopeDraft(DemandRevisionDraft source,
        Guid creator)
    {
        var cycle = new PlanningCycle
        {
            Id = Guid.NewGuid(), TenantId = source.TenantId,
            LegalEntityId = source.LegalEntityId, AsOfDate = source.AsOfDate,
            CalendarId = source.CalendarId,
            CalendarVersion = source.CalendarVersion,
            TimeZoneId = source.TimeZoneId,
            HorizonStart = source.HorizonStart,
            HorizonEnd = source.HorizonEnd,
            PlanningPeriodKey = source.PlanningPeriodKey,
            Weeks = source.Weeks.Select(x => new PlanningWeek
            {
                Number = x.Number, WeekStart = x.WeekStart, WeekEnd = x.WeekEnd
            }).ToList()
        };
        var selected = source.Series.Where(x => x.Exclusion is null)
            .Select(x => new VerifiedDraftSeries(x.SkuId, x.WarehouseId,
                x.BaseUomId, x.Weeks.Select(week => new ManualDraftWeekInput(
                    week.Number, week.WeekStart, week.WeekEnd,
                    week.ValueKind, week.Quantity + 1m)).ToArray())).ToArray();
        return Assert.IsType<DemandRevisionDraft>(ManualDraftFactory.Create(cycle,
            source.TenantId, source.LegalEntityId, creator,
            "Verified replacement fixture", Now, selected).Data);
    }

    private sealed class GatedStatusReader(
        IInternalAuthoritativeRevisionStatusReader inner)
        : IInternalAuthoritativeRevisionStatusReader
    {
        private readonly TaskCompletionSource _entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Entered => _entered.Task;
        public void Release() => _release.TrySetResult();

        public async Task<AuthoritativeStatusResult> ReadAsync(Guid tenantId,
            Guid legalEntityId, Guid revisionId, Guid actorId,
            CancellationToken cancellationToken)
        {
            _entered.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            return await inner.ReadAsync(tenantId, legalEntityId,
                revisionId, actorId, cancellationToken);
        }
    }

    [ManualDraftMongoFact]
    public async Task CurrentPeriod_PublishReplacementThenInvalidate_NoOldFallback()
    {
        var (first, drafts, published, context) = await PublishedFixture(secondSeries: false);
        var actor = Guid.NewGuid();
        var reader = PeriodReader(published, context, [first]);
        var initial = await reader.ReadAsync(first.TenantId, first.LegalEntityId,
            first.PlanningPeriodKey, actor, default);
        Assert.Equal(CurrentPublishedOutcome.Found, initial.Outcome);
        Assert.Equal(first.Id, initial.Revision!.RevisionId);

        var next = Draft(first.TenantId, first.LegalEntityId,
            Guid.NewGuid(), FirstWeek, calendarVersion: "2");
        await Approve(drafts, next, next.CreatedBy, Guid.NewGuid());
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, next, Guid.NewGuid())).Outcome);
        reader = PeriodReader(published, context, [next, first]);
        var replaced = await reader.ReadAsync(first.TenantId, first.LegalEntityId,
            first.PlanningPeriodKey, actor, default);
        Assert.Equal(CurrentPublishedOutcome.Found, replaced.Outcome);
        Assert.Equal(next.Id, replaced.Revision!.RevisionId);
        // StateVersion belongs to each revision; the slot's identity is the
        // period-wide fence and can change while both revisions have version 3.
        Assert.True(replaced.Revision.StateVersion > 0);

        Assert.Equal(InvalidationOutcome.Invalidated,
            (await Invalidate(Invalidator(next, drafts, published, context), next)).Outcome);
        var invalidated = await reader.ReadAsync(first.TenantId, first.LegalEntityId,
            first.PlanningPeriodKey, actor, default);
        Assert.Equal(CurrentPublishedOutcome.NotFound, invalidated.Outcome);
        Assert.Null(invalidated.Revision);
    }

    [ManualDraftMongoFact]
    public async Task CurrentPeriod_ScopeAuthorityAndCorruption_FailClosed()
    {
        var (draft, _, published, context) = await PublishedFixture(secondSeries: true);
        var actor = Guid.NewGuid();
        async Task<CurrentPublishedOutcome> Read(Guid tenant, Guid company,
            IInternalCurrentPublishedAuthority? authority = null) =>
            (await PeriodReader(published, context, [draft], authority).ReadAsync(
                tenant, company, draft.PlanningPeriodKey, actor, default)).Outcome;

        Assert.Equal(CurrentPublishedOutcome.NotFound,
            await Read(Guid.NewGuid(), draft.LegalEntityId));
        Assert.Equal(CurrentPublishedOutcome.NotFound,
            await Read(draft.TenantId, Guid.NewGuid()));
        Assert.Equal(CurrentPublishedOutcome.NotFound,
            await Read(draft.TenantId, draft.LegalEntityId,
                new PeriodAuthority([draft], narrowSeries: true)));
        Assert.Equal(CurrentPublishedOutcome.PermissionDenied,
            await Read(draft.TenantId, draft.LegalEntityId,
                new PeriodAuthority([draft], permitted: false)));
        Assert.Equal(CurrentPublishedOutcome.AuthorityUnavailable,
            await Read(draft.TenantId, draft.LegalEntityId,
                new PeriodAuthority([draft], available: false)));
        Assert.Equal(CurrentPublishedOutcome.NotFound,
            (await PeriodReader(published, context, [draft]).ReadAsync(
                draft.TenantId, draft.LegalEntityId, "2026-10-12", actor,
                default)).Outcome);

        var part = await context.PublishedRevisionParts.Find(x =>
            x.RevisionId == draft.Id && x.TenantId == draft.TenantId &&
            x.LegalEntityId == draft.LegalEntityId).FirstAsync();
        part.Rows.RemoveAt(0);
        await context.PublishedRevisionParts.ReplaceOneAsync(x => x.Id == part.Id,
            part);
        Assert.Equal(CurrentPublishedOutcome.Unverifiable,
            await Read(draft.TenantId, draft.LegalEntityId));
        Assert.Equal(CurrentPublishedOutcome.NotFound,
            await Read(draft.TenantId, draft.LegalEntityId,
                new PeriodAuthority([draft], narrowSeries: true)));
    }

    [ManualDraftMongoFact]
    public async Task CurrentPeriod_HttpJwtPermissionAndVerifiedJson()
    {
        var (draft, _, _, _) = await PublishedFixture(secondSeries: false);
        await using var host = await ReadHttpHost.StartAsync(draft,
            currentAuthority: new PeriodAuthority([draft]));
        var path = "/api/v2/demand/revisions/current?planningPeriodKey=" +
            draft.PlanningPeriodKey;
        var actor = Guid.NewGuid();
        using (var response = await host.Client.GetAsync(path))
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using (var request = host.Request(path, draft.TenantId,
            draft.LegalEntityId, actor, permissions: []))
        using (var response = await host.Client.SendAsync(request))
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using (var request = host.Request(path, draft.TenantId,
            Guid.NewGuid(), actor))
        using (var response = await host.Client.SendAsync(request))
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using (var request = host.Request(path, draft.TenantId,
            draft.LegalEntityId, actor))
        using (var response = await host.Client.SendAsync(request))
        {
            var data = await SuccessData(response);
            Assert.Equal(draft.Id.ToString("D"),
                data.GetProperty("revisionId").GetString());
            Assert.Equal(draft.PlanningPeriodKey,
                data.GetProperty("planningPeriodKey").GetString());
            Assert.Equal("Published", data.GetProperty("state").GetString());
            Assert.Equal("Verified", data.GetProperty("integrityState").GetString());
            Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("integrity")
                .GetProperty("digest").GetString()));
        }
    }

    [ManualDraftMongoFact]
    public async Task CurrentPeriod_HttpOutageCorruptSnapshotAndInvalidation_AreClosed()
    {
        var (draft, drafts, published, context) =
            await PublishedFixture(secondSeries: false);
        var actor = Guid.NewGuid();
        var path = "/api/v2/demand/revisions/current?planningPeriodKey=" +
            draft.PlanningPeriodKey;
        await using (var closed = await ReadHttpHost.StartAsync(draft))
        using (var request = closed.Request(path, draft.TenantId,
            draft.LegalEntityId, actor))
        using (var response = await closed.Client.SendAsync(request))
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        await using var host = await ReadHttpHost.StartAsync(draft,
            currentAuthority: new PeriodAuthority([draft]));
        var part = await context.PublishedRevisionParts.Find(x =>
            x.RevisionId == draft.Id && x.TenantId == draft.TenantId &&
            x.LegalEntityId == draft.LegalEntityId).FirstAsync();
        var original = part.Rows.ToArray();
        part.Rows.RemoveAt(0);
        await context.PublishedRevisionParts.ReplaceOneAsync(x => x.Id == part.Id,
            part);
        using (var request = host.Request(path, draft.TenantId,
            draft.LegalEntityId, actor))
        using (var response = await host.Client.SendAsync(request))
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        part.Rows = original.ToList();
        await context.PublishedRevisionParts.ReplaceOneAsync(x => x.Id == part.Id,
            part);
        Assert.Equal(InvalidationOutcome.Invalidated,
            (await Invalidate(Invalidator(draft, drafts, published, context), draft)).Outcome);
        using (var request = host.Request(path, draft.TenantId,
            draft.LegalEntityId, actor))
        using (var response = await host.Client.SendAsync(request))
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [ManualDraftMongoFact]
    public async Task CurrentPeriod_ConcurrentReplacement_NeverReturnsMixedRevision()
    {
        var (first, drafts, published, context) =
            await PublishedFixture(secondSeries: false);
        var next = SameScopeDraft(first, Guid.NewGuid());
        await Approve(drafts, next, next.CreatedBy, Guid.NewGuid());
        var authority = new PeriodAuthority([first]);
        var actor = Guid.NewGuid();
        var before = await PeriodReader(published, context, [first, next], authority)
            .ReadAsync(first.TenantId, first.LegalEntityId,
                first.PlanningPeriodKey, actor, default);
        Assert.Equal(CurrentPublishedOutcome.Found, before.Outcome);
        var firstManifest = await context.PublishedRevisionManifests.Find(x =>
            x.TenantId == first.TenantId && x.LegalEntityId == first.LegalEntityId &&
            x.RevisionId == first.Id).SingleAsync();
        Assert.Equal(first.Id, before.Revision!.RevisionId);
        Assert.Equal(firstManifest.StateVersion, before.Revision.StateVersion);
        Assert.Equal(firstManifest.Checksum, before.Revision.Checksum);
        var statuses = new AuthoritativeRevisionStatusReader(context,
            new MultiStatusAuthority([first, next]));
        var gate = new GatedStatusReader(statuses);
        var reader = PeriodReader(published, context, [first, next],
            authority, gate);
        var inFlight = reader.ReadAsync(first.TenantId, first.LegalEntityId,
            first.PlanningPeriodKey, actor, default);
        await gate.Entered.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            Assert.Equal(PublishOutcome.Published,
                (await Publish(published, next, Guid.NewGuid())).Outcome);
        }
        finally
        {
            gate.Release();
        }
        var raced = await inFlight;
        Assert.Equal(CurrentPublishedOutcome.Unverifiable, raced.Outcome);
        Assert.Null(raced.Revision);

        reader = PeriodReader(published, context, [first, next], authority);
        var reads = Enumerable.Range(0, 20).Select(_ => reader.ReadAsync(
            first.TenantId, first.LegalEntityId, first.PlanningPeriodKey,
            actor, default)).ToArray();
        var results = await Task.WhenAll(reads);
        Assert.All(results, result =>
        {
            Assert.Equal(CurrentPublishedOutcome.Found, result.Outcome);
            Assert.Equal(next.Id, result.Revision!.RevisionId);
        });
        var settled = await reader.ReadAsync(first.TenantId,
            first.LegalEntityId, first.PlanningPeriodKey, actor, default);
        Assert.Equal(CurrentPublishedOutcome.Found, settled.Outcome);
        Assert.Equal(next.Id, settled.Revision!.RevisionId);
        var manifest = await context.PublishedRevisionManifests.Find(x =>
            x.TenantId == first.TenantId && x.LegalEntityId == first.LegalEntityId &&
            x.RevisionId == next.Id).SingleAsync();
        Assert.Equal(manifest.StateVersion, settled.Revision.StateVersion);
        Assert.Equal(manifest.Checksum, settled.Revision.Checksum);
        Assert.All(results, result =>
        {
            Assert.Equal(manifest.StateVersion, result.Revision!.StateVersion);
            Assert.Equal(manifest.Checksum, result.Revision.Checksum);
        });
    }
}

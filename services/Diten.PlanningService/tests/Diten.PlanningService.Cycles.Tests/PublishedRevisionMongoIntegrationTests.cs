using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using Diten.PlanningService.Persistence.Features.DemandPlanning;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed partial class PublishedRevisionMongoIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly FirstWeek = new(2026, 10, 5);

    private sealed class FixtureAuthority(PublishAuthorityEvidence evidence) : IInternalPublishAuthority
    {
        public Task<PublishAuthorityEvidence> VerifyAsync(Guid tenantId, Guid legalEntityId,
            Guid actorId, IReadOnlyList<(Guid SkuId, string WarehouseId)> selectedScope,
            CancellationToken cancellationToken) => Task.FromResult(evidence);
    }

    private static (ManualDraftMongoStore Drafts, PublishedRevisionMongoStore Published,
        DemandPlanningMongoContext Context) Open(PublishAuthorityEvidence? evidence = null)
    {
        var uri = Environment.GetEnvironmentVariable("MOD0188_TEST_MONGO_URI")
            ?? throw new InvalidOperationException("An explicit test Mongo URI is required.");
        var url = new MongoUrl(uri);
        var servers = url.Servers.ToArray();
        if (servers.Length != 1 || servers[0].Host != "127.0.0.1" ||
            servers[0].Port is < 31994 or > 39994 || url.Username is not null ||
            url.Password is not null || url.ReplicaSetName != "rs-mod0188")
            throw new InvalidOperationException("Only the explicit local rs-mod0188 test replica set is allowed.");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = uri,
                ["Mongo:SupplyChainDatabaseName"] = "mod0188_tests"
            }).Build();
        var context = new DemandPlanningMongoContext(configuration);
        var drafts = new ManualDraftMongoStore(context);
        var published = new PublishedRevisionMongoStore(context, drafts,
            new FixtureAuthority(evidence ?? new PublishAuthorityEvidence(true, true, true, false)));
        return (drafts, published, context);
    }

    private static DemandRevisionDraft Draft(Guid tenant, Guid legalEntity, Guid creator,
        DateOnly firstWeek, string calendarVersion = "1", decimal quantity = 10m,
        bool secondSeries = false, DateOnly? asOfDate = null)
    {
        var cycle = new PlanningCycle
        {
            Id = Guid.NewGuid(), TenantId = tenant, LegalEntityId = legalEntity,
            AsOfDate = asOfDate ?? firstWeek.AddDays(-3), CalendarId = "ISO-8601",
            CalendarVersion = calendarVersion, TimeZoneId = "Asia/Baku",
            HorizonStart = firstWeek, HorizonEnd = firstWeek.AddDays(363),
            PlanningPeriodKey = firstWeek.ToString("yyyy-MM-dd"),
            Weeks = Enumerable.Range(0, 52).Select(index => new PlanningWeek
            {
                Number = index + 1, WeekStart = firstWeek.AddDays(index * 7),
                WeekEnd = firstWeek.AddDays(index * 7 + 6)
            }).ToList()
        };
        VerifiedDraftSeries Series(string warehouse) => new(Guid.NewGuid(), warehouse, "EA",
            Enumerable.Range(1, 52).Select(number => new ManualDraftWeekInput(
                number, firstWeek.AddDays((number - 1) * 7),
                firstWeek.AddDays((number - 1) * 7 + 6),
                DraftWeekValueKind.Known, quantity)).ToArray());
        var series = secondSeries
            ? new[] { Series("WH-1"), Series("WH-2") }
            : new[] { Series("WH-1") };
        return Assert.IsType<DemandRevisionDraft>(ManualDraftFactory.Create(cycle,
            tenant, legalEntity, creator, "Verified manual fixture", Now,
            series).Data);
    }

    private static async Task Approve(ManualDraftMongoStore store,
        DemandRevisionDraft draft, Guid creator, Guid reviewer)
    {
        Assert.Equal(ManualDraftStoreOutcome.Created,
            (await store.CreateAsync(draft, $"create-{draft.Id:D}", default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await store.TransitionReviewAsync(draft.TenantId, draft.LegalEntityId,
                draft.Id, creator, DraftReviewAction.Submitted, null,
                $"submit-{draft.Id:D}", 0, 0, Now.AddMinutes(1), default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await store.TransitionReviewAsync(draft.TenantId, draft.LegalEntityId,
                draft.Id, reviewer, DraftReviewAction.Approved, "Checked all weeks",
                $"approve-{draft.Id:D}", 0, 1, Now.AddMinutes(2), default)).Outcome);
    }

    private static Task<PublishResult> Publish(PublishedRevisionMongoStore store,
        DemandRevisionDraft draft, Guid actor, string key = "publish",
        int contentVersion = 0, int stateVersion = 2) =>
        store.PublishAsync(draft.TenantId, draft.LegalEntityId, draft.Id, actor,
            key, contentVersion, stateVersion, Now.AddMinutes(3), default);

    [ManualDraftMongoFact]
    public async Task AcceptedHistory_ToForecastDraft_IndependentReviewAndPublishedSnapshot()
    {
        var (drafts, published, context) = Open();
        var tenant = Guid.NewGuid(); var legalEntity = Guid.NewGuid();
        var creator = Guid.NewGuid(); var reviewer = Guid.NewGuid();
        var sku = Guid.NewGuid();
        var firstHistoryWeek = FirstWeek.AddDays(-26 * 7);
        var lastHistoryWeek = FirstWeek.AddDays(-7);
        var sourceKey = Guid.NewGuid().ToString("N");
        var batch = new DemandHistoryImportBatch
        {
            TenantId = tenant, LegalEntityId = legalEntity,
            UploadedByActorId = Guid.NewGuid(), SourceSystem = "fixture",
            SourceFileSha256 = new string('A', 64),
            CoverageFileSha256 = new string('B', 64),
            ScopeFrom = firstHistoryWeek, ScopeThrough = lastHistoryWeek.AddDays(6),
            WarehouseScope = ["WH-1"],
            ReviewState = ImportBatchReviewState.Approved,
            ReviewedByActorId = reviewer,
            CoverageWeeks = Enumerable.Range(0, 26).Select(index =>
                new ImportWeekCoverage
                {
                    SkuId = sku, SkuLevel = "Gsku", WarehouseId = "WH-1",
                    UomId = "CASE", WeekStart = firstHistoryWeek.AddDays(index * 7),
                    StockState = ImportWeekStockState.InStock
                }).ToList(),
            Rows = [new DemandHistoryImportRow
            {
                RowNumber = 1, SourceRecordKey = sourceKey,
                SkuId = sku, SkuLevel = "Gsku", WarehouseId = "WH-1",
                OccurredOn = lastHistoryWeek.AddDays(2), OriginalQuantity = 5,
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
        var accepted = new AcceptedDemandHistoryRow
        {
            TenantId = tenant, LegalEntityId = legalEntity,
            BatchId = batch.Id, BatchRowNumber = 1,
            SourceSystem = batch.SourceSystem, SourceRecordKey = sourceKey,
            SourceFileSha256 = batch.SourceFileSha256, SkuId = sku,
            SkuLevel = "Gsku", WarehouseId = "WH-1",
            OccurredOn = lastHistoryWeek.AddDays(2), OriginalQuantity = 5,
            OriginalUomId = "CASE", AcceptedQuantity = 5,
            AcceptedUomId = "CASE", UnitVerificationPending = true,
            AcceptedByActorId = Guid.NewGuid(), AcceptedAt = acceptedAt,
            AcceptanceReason = "Verified", RequestKey = Guid.NewGuid().ToString("N"),
            RequestFingerprint = new string('C', 64)
        };
        accepted.ContentSha256 = AcceptedHistoryFingerprint.Compute(accepted);
        await context.AcceptedHistoryRows.InsertOneAsync(accepted);
        await context.HistoryAcceptanceAudit.InsertOneAsync(new DemandHistoryAcceptanceAudit
        {
            TenantId = tenant, LegalEntityId = legalEntity,
            AcceptedRowId = accepted.Id, BatchId = batch.Id, BatchRowNumber = 1,
            SourceRecordKey = sourceKey, ActorId = accepted.AcceptedByActorId,
            OccurredAt = acceptedAt, Reason = accepted.AcceptanceReason,
            ContentSha256 = accepted.ContentSha256
        });

        var template = Draft(tenant, legalEntity, creator, FirstWeek);
        var cycle = new PlanningCycle
        {
            Id = template.PlanningCycleId, TenantId = tenant, LegalEntityId = legalEntity,
            AsOfDate = template.AsOfDate, CalendarId = template.CalendarId,
            CalendarVersion = template.CalendarVersion, TimeZoneId = template.TimeZoneId,
            PlanningPeriodKey = template.PlanningPeriodKey,
            HorizonStart = template.HorizonStart, HorizonEnd = template.HorizonEnd,
            Weeks = template.Weeks.ToList()
        };
        var builder = new AcceptedHistoryForecastDraftBuilder(
            new AcceptedWeeklyHistoryMongoReader(context));
        var draft = Assert.IsType<DemandRevisionDraft>(await builder.BuildAsync(cycle,
            creator, sku, "Gsku", "WH-1", ForecastMethodEligibility.Method.Naive,
            ForecastMethodEligibility.FixturePolicy.Mvp("pilot-1"),
            "Explicit Naive selection", Now, default));
        var moving = Assert.IsType<DemandRevisionDraft>(await builder.BuildAsync(cycle,
            creator, sku, "Gsku", "WH-1", ForecastMethodEligibility.Method.MovingAverage,
            ForecastMethodEligibility.FixturePolicy.Mvp("pilot-1"),
            "Explicit moving average selection", Now, default,
            new FixtureForecastEngine.MethodSpecification(
                ForecastMethodEligibility.Method.MovingAverage, "trailing-flat",
                "method-1", 3, null, null, null)));
        var movingOrigin = Assert.Single(moving.Series).Weeks[0].MethodProvenance;
        Assert.Equal("trailing-flat", movingOrigin?.Variant);
        Assert.Equal("method-1", movingOrigin?.Parameters["methodPolicyVersion"]);
        Assert.Equal("CASE", Assert.Single(draft.Series).BaseUomId);
        Assert.All(Assert.Single(draft.Series).Weeks, week => Assert.Equal(5m, week.Quantity));
        await Approve(drafts, draft, creator, reviewer);
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, draft, reviewer)).Outcome);
        var snapshot = (await published.ReadSnapshotAsync(tenant, legalEntity,
            draft.Id, default))!.Value;
        Assert.Equal("MOD0188-CANONICAL-2/SHA-256", snapshot.Manifest.ChecksumScheme);
        Assert.All(Assert.Single(snapshot.Parts).Rows, row =>
        {
            Assert.Equal(5m, row.Quantity);
            Assert.Equal("Naive", row.MethodProvenance?.Method);
            Assert.True(row.MethodProvenance?.UnitVerificationPending);
        });
    }

    [ManualDraftMongoFact]
    public async Task MethodDraft_RoundTripsThroughIndependentReviewAndVerifiedPublication()
    {
        var (drafts, published, _) = Open();
        var tenant = Guid.NewGuid(); var legalEntity = Guid.NewGuid();
        var creator = Guid.NewGuid(); var reviewer = Guid.NewGuid();
        var template = Draft(tenant, legalEntity, creator, FirstWeek);
        var selected = Assert.Single(template.Series);
        var provenance = new MethodWeekProvenance(Guid.NewGuid(), "Naive", "last-value",
            "policy-1", FirstWeek.AddDays(-7), new string('A', 64),
            new Dictionary<string, string> { ["horizon"] = "52" }, true);
        var cycle = new PlanningCycle
        {
            Id = template.PlanningCycleId, TenantId = tenant, LegalEntityId = legalEntity,
            AsOfDate = template.AsOfDate, CalendarId = template.CalendarId,
            CalendarVersion = template.CalendarVersion, TimeZoneId = template.TimeZoneId,
            PlanningPeriodKey = template.PlanningPeriodKey,
            HorizonStart = template.HorizonStart, HorizonEnd = template.HorizonEnd,
            Weeks = template.Weeks.ToList()
        };
        var draft = Assert.IsType<DemandRevisionDraft>(ManualDraftFactory.Create(cycle,
            tenant, legalEntity, creator, "Forecast reviewed", Now,
            [new VerifiedDraftSeries(selected.SkuId, selected.WarehouseId,
                selected.BaseUomId, selected.Weeks.Select(week =>
                    new ManualDraftWeekInput(week.Number, week.WeekStart, week.WeekEnd,
                        week.ValueKind, week.Quantity)).ToArray(), provenance)]).Data);
        await Approve(drafts, draft, creator, reviewer);
        var restored = Assert.IsType<DemandRevisionDraft>(await drafts.ReadAsync(
            tenant, legalEntity, draft.Id, default));
        Assert.All(Assert.Single(restored.Series).Weeks, week =>
        {
            Assert.Equal(DraftWeekSource.Method, week.Source);
            Assert.True(provenance.HasSameContent(week.MethodProvenance));
        });
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, draft, reviewer)).Outcome);
        var snapshot = (await published.ReadSnapshotAsync(tenant, legalEntity,
            draft.Id, default))!.Value;
        Assert.Equal("MOD0188-CANONICAL-2/SHA-256", snapshot.Manifest.ChecksumScheme);
        Assert.Equal(snapshot.Manifest.Checksum,
            PublishedSnapshotIntegrity.Calculate(snapshot.Manifest, snapshot.Parts));
        Assert.All(Assert.Single(snapshot.Parts).Rows, row =>
            Assert.True(provenance.HasSameContent(row.MethodProvenance)));
        snapshot.Parts[0].Rows[0] = snapshot.Parts[0].Rows[0] with
        {
            MethodProvenance = provenance with { PolicyVersion = "tampered" }
        };
        Assert.NotEqual(snapshot.Manifest.Checksum,
            PublishedSnapshotIntegrity.Calculate(snapshot.Manifest, snapshot.Parts));
    }

    [ManualDraftMongoFact]
    public async Task Publish_SamePeriodAcrossCyclesAndCalendars_SupersedesAtomically()
    {
        var (drafts, published, context) = Open();
        var tenant = Guid.NewGuid(); var legalEntity = Guid.NewGuid();
        var creator = Guid.NewGuid(); var reviewer = Guid.NewGuid();
        var first = Draft(tenant, legalEntity, creator, FirstWeek, secondSeries: true);
        await Approve(drafts, first, creator, reviewer);
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, first, reviewer)).Outcome);
        var original = Assert.IsType<PublishedRevisionManifest>(
            (await published.ReadSnapshotAsync(tenant, legalEntity, first.Id, default))?.Manifest);
        Assert.Equal(2, original.ExpectedPartCount);
        Assert.Equal(104, original.ExpectedRowCount);
        Assert.Equal(64, original.Checksum.Length);
        Assert.All((await published.ReadSnapshotAsync(tenant, legalEntity,
            first.Id, default))!.Value.Parts, part =>
        {
            Assert.Equal(52, part.Rows.Count);
            Assert.All(part.Rows, row =>
            {
                Assert.Equal(DraftWeekSource.Manual, row.Source);
                Assert.Equal(creator, row.ManualActorId);
                Assert.False(string.IsNullOrWhiteSpace(row.ManualReason));
                Assert.NotEqual(default, row.ManualAt);
            });
        });
        Assert.Equal(DemandRevisionState.Published,
            (await drafts.ReadAsync(tenant, legalEntity, first.Id, default))?.State);

        var replacement = Draft(tenant, legalEntity, creator, FirstWeek,
            calendarVersion: "2", quantity: 7m);
        await Approve(drafts, replacement, creator, reviewer);
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, replacement, reviewer)).Outcome);
        var old = Assert.IsType<PublishedRevisionManifest>(
            (await published.ReadSnapshotAsync(tenant, legalEntity, first.Id, default))?.Manifest);
        Assert.Equal(DemandRevisionState.Superseded, old.State);
        Assert.Equal(original.Checksum, old.Checksum);
        Assert.Equal(DemandRevisionState.Superseded,
            (await drafts.ReadAsync(tenant, legalEntity, first.Id, default))?.State);
        Assert.Equal(replacement.Id, (await context.PublishedBaselineSlots.Find(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.PlanningPeriodKey == "2026-10-05").FirstAsync()).RevisionId);
        Assert.Equal(2, await context.Outbox.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity));

        var changedAsOf = Draft(tenant, legalEntity, creator, FirstWeek,
            calendarVersion: "2", asOfDate: FirstWeek.AddDays(-1));
        await Approve(drafts, changedAsOf, creator, reviewer);
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, changedAsOf, reviewer)).Outcome);
        Assert.Equal(DemandRevisionState.Superseded,
            (await published.ReadSnapshotAsync(tenant, legalEntity, replacement.Id,
                default))?.Manifest.State);
        Assert.Equal(changedAsOf.Id, (await context.PublishedBaselineSlots.Find(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.PlanningPeriodKey == "2026-10-05").FirstAsync()).RevisionId);

        var overlappingHorizon = Draft(tenant, legalEntity, creator,
            FirstWeek.AddDays(7));
        await Approve(drafts, overlappingHorizon, creator, reviewer);
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, overlappingHorizon, reviewer)).Outcome);
        Assert.Equal(2, await context.PublishedBaselineSlots.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity));
    }

    [ManualDraftMongoFact]
    public async Task Publish_RetryAndStaleVersion_DoNotCreateSecondEffect()
    {
        var (drafts, published, context) = Open();
        var draft = Draft(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), FirstWeek);
        var reviewer = Guid.NewGuid();
        await Approve(drafts, draft, draft.CreatedBy, reviewer);
        Assert.Equal(PublishOutcome.Conflict,
            (await Publish(published, draft, reviewer, stateVersion: 1)).Outcome);
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, draft, reviewer)).Outcome);
        Assert.Equal(PublishOutcome.Replayed,
            (await Publish(published, draft, reviewer)).Outcome);
        Assert.Equal(PublishOutcome.Conflict,
            (await Publish(published, draft, reviewer, contentVersion: 1)).Outcome);
        Assert.Equal(1, await context.Outbox.CountDocumentsAsync(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId));
        Assert.Equal(1, await context.ManualDraftPublicationAudit.CountDocumentsAsync(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.RevisionId == draft.Id));
    }

    [ManualDraftMongoFact]
    public async Task Publish_ConcurrentSameRequest_LeavesOneEventAndOneSnapshot()
    {
        var (drafts, published, context) = Open();
        var draft = Draft(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), FirstWeek);
        var reviewer = Guid.NewGuid();
        await Approve(drafts, draft, draft.CreatedBy, reviewer);
        var results = await Task.WhenAll(Publish(published, draft, reviewer),
            Publish(published, draft, reviewer));
        Assert.Single(results, x => x.Outcome == PublishOutcome.Published);
        Assert.Single(results, x => x.Outcome is PublishOutcome.Replayed or PublishOutcome.Conflict);
        Assert.Equal(PublishOutcome.Replayed,
            (await Publish(published, draft, reviewer)).Outcome);
        Assert.Equal(1, await context.Outbox.CountDocumentsAsync(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId));
        Assert.Equal(1, await context.PublishedRevisionManifests.CountDocumentsAsync(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId));
    }

    [ManualDraftMongoFact]
    public async Task Publish_SignificantEditorAndCrossScope_AreDenied()
    {
        var (drafts, published, context) = Open();
        var draft = Draft(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), FirstWeek);
        var editor = Guid.NewGuid(); var reviewer = Guid.NewGuid();
        await drafts.CreateAsync(draft, "create-editor-test", default);
        var series = draft.Series[0];
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await drafts.EditWeekAsync(draft.TenantId, draft.LegalEntityId,
                draft.Id, editor, series.SkuId, series.WarehouseId, 1,
                DraftWeekValueKind.Known, 11m, "Material manual edit",
                "edit-before-review", 0, Now.AddMinutes(1), default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await drafts.TransitionReviewAsync(draft.TenantId, draft.LegalEntityId,
                draft.Id, draft.CreatedBy, DraftReviewAction.Submitted, null,
                "submit-after-edit", 1, 0, Now.AddMinutes(2), default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await drafts.TransitionReviewAsync(draft.TenantId, draft.LegalEntityId,
                draft.Id, reviewer, DraftReviewAction.Approved, "Reviewed",
                "approve-after-edit", 1, 1, Now.AddMinutes(3), default)).Outcome);
        Assert.Equal(PublishOutcome.SeparationDenied,
            (await Publish(published, draft, editor, contentVersion: 1)).Outcome);
        Assert.Equal(PublishOutcome.ScopeDenied,
            (await published.PublishAsync(Guid.NewGuid(), draft.LegalEntityId,
                draft.Id, reviewer, "cross-tenant", 1, 2,
                Now.AddMinutes(4), default)).Outcome);
        Assert.Equal(PublishOutcome.ScopeDenied,
            (await published.PublishAsync(draft.TenantId, Guid.NewGuid(),
                draft.Id, reviewer, "cross-company", 1, 2,
                Now.AddMinutes(4), default)).Outcome);
        Assert.Equal(0, await context.Outbox.CountDocumentsAsync(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId));
    }

    [ManualDraftMongoFact]
    public async Task Publish_ContributorAndIntegrationActor_AreDenied()
    {
        var tenant = Guid.NewGuid(); var legalEntity = Guid.NewGuid();
        var creator = Guid.NewGuid(); var reviewer = Guid.NewGuid();
        var (drafts, published, context) = Open();
        var draft = Draft(tenant, legalEntity, creator, FirstWeek);
        await Approve(drafts, draft, creator, reviewer);
        Assert.Equal(PublishOutcome.SeparationDenied,
            (await Publish(published, draft, creator)).Outcome);
        var integration = Open(new PublishAuthorityEvidence(true, true, true, true)).Published;
        Assert.Equal(PublishOutcome.PermissionDenied,
            (await Publish(integration, draft, reviewer)).Outcome);
        var unavailable = Open(new PublishAuthorityEvidence(false, true, true, false)).Published;
        Assert.Equal(PublishOutcome.AuthorityUnavailable,
            (await Publish(unavailable, draft, reviewer)).Outcome);
        var noPermission = Open(new PublishAuthorityEvidence(true, false, true, false)).Published;
        Assert.Equal(PublishOutcome.PermissionDenied,
            (await Publish(noPermission, draft, reviewer)).Outcome);
        var noScope = Open(new PublishAuthorityEvidence(true, true, false, false)).Published;
        Assert.Equal(PublishOutcome.ScopeDenied,
            (await Publish(noScope, draft, reviewer)).Outcome);
        Assert.Equal(0, await context.PublishedRevisionManifests.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity));
    }

    [ManualDraftMongoFact]
    public async Task Publish_CorruptPart_DoesNotWriteSnapshotOrOutbox()
    {
        var (drafts, published, context) = Open();
        var draft = Draft(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), FirstWeek);
        var reviewer = Guid.NewGuid();
        await Approve(drafts, draft, draft.CreatedBy, reviewer);
        var part = await context.ManualDraftSeriesParts.Find(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.RevisionId == draft.Id).FirstAsync();
        part.SkuId = Guid.NewGuid();
        await context.ManualDraftSeriesParts.ReplaceOneAsync(x => x.Id == part.Id, part);
        Assert.Equal(PublishOutcome.InvalidSnapshot,
            (await Publish(published, draft, reviewer)).Outcome);
        Assert.Equal(0, await context.PublishedRevisionManifests.CountDocumentsAsync(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId));
        Assert.Equal(0, await context.Outbox.CountDocumentsAsync(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId));
    }

    [ManualDraftMongoFact]
    public async Task Publish_ConcurrentCyclesForOnePeriod_KeepOneCurrentBaseline()
    {
        var (drafts, published, context) = Open();
        var tenant = Guid.NewGuid(); var legalEntity = Guid.NewGuid();
        var creator = Guid.NewGuid(); var reviewer = Guid.NewGuid();
        var first = Draft(tenant, legalEntity, creator, FirstWeek);
        var second = Draft(tenant, legalEntity, creator, FirstWeek,
            calendarVersion: "3", asOfDate: FirstWeek.AddDays(-1));
        await Approve(drafts, first, creator, reviewer);
        await Approve(drafts, second, creator, reviewer);
        var results = await Task.WhenAll(Publish(published, first, reviewer),
            Publish(published, second, reviewer));
        Assert.Single(results, x => x.Outcome == PublishOutcome.Published);
        Assert.Single(results, x => x.Outcome == PublishOutcome.Conflict);
        Assert.Equal(1, await context.PublishedBaselineSlots.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity));
        Assert.Equal(1, await context.PublishedRevisionManifests.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.State == DemandRevisionState.Published));
    }

    [ManualDraftMongoFact]
    public async Task Publish_OutboxFailure_RollsBackEveryWrite()
    {
        var (drafts, published, context) = Open();
        var draft = Draft(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), FirstWeek);
        var reviewer = Guid.NewGuid();
        var previous = Draft(draft.TenantId, draft.LegalEntityId,
            draft.CreatedBy, FirstWeek);
        await Approve(drafts, previous, previous.CreatedBy, reviewer);
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, previous, reviewer)).Outcome);
        await Approve(drafts, draft, draft.CreatedBy, reviewer);
        var eventId = PublishedRevisionMongoStore.DeterministicEventId(
            draft.TenantId, draft.LegalEntityId, draft.Id);
        await context.Outbox.InsertOneAsync(new DemandOutboxMessage
        {
            TenantId = draft.TenantId, LegalEntityId = draft.LegalEntityId,
            RevisionId = Guid.NewGuid(), EventId = eventId,
            EventType = "test-blocker", Payload = "{}", OccurredAt = Now
        });
        Assert.Equal(PublishOutcome.Conflict,
            (await Publish(published, draft, reviewer)).Outcome);
        Assert.Equal(DemandRevisionState.Approved,
            (await drafts.ReadAsync(draft.TenantId, draft.LegalEntityId, draft.Id, default))?.State);
        Assert.Equal(DemandRevisionState.Published,
            (await published.ReadSnapshotAsync(previous.TenantId,
                previous.LegalEntityId, previous.Id, default))?.Manifest.State);
        Assert.Equal(0, await context.PublishedRevisionManifests.CountDocumentsAsync(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.RevisionId == draft.Id));
        Assert.Equal(0, await context.ManualDraftPublicationAudit.CountDocumentsAsync(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.RevisionId == draft.Id));
        Assert.Equal(previous.Id, (await context.PublishedBaselineSlots.Find(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.PlanningPeriodKey == draft.PlanningPeriodKey).FirstAsync()).RevisionId);
    }

    [ManualDraftMongoFact]
    public async Task Publish_AuditFailure_RollsBackStateSnapshotSlotAndOutbox()
    {
        var (drafts, published, context) = Open();
        var draft = Draft(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), FirstWeek);
        var reviewer = Guid.NewGuid();
        await Approve(drafts, draft, draft.CreatedBy, reviewer);
        var keys = Builders<DemandAuditEntry>.IndexKeys;
        await context.AuditEntries.Indexes.CreateOneAsync(
            new CreateIndexModel<DemandAuditEntry>(keys.Ascending(x => x.Action),
                new CreateIndexOptions<DemandAuditEntry>
                {
                    Unique = true, Name = "mod0188_test_publish_audit_failure",
                    PartialFilterExpression = Builders<DemandAuditEntry>.Filter
                        .Eq(x => x.TenantId, draft.TenantId)
                }));
        try
        {
            await context.AuditEntries.InsertOneAsync(new DemandAuditEntry
            {
                TenantId = draft.TenantId, LegalEntityId = draft.LegalEntityId,
                ActorId = Guid.NewGuid(), Action = "Published",
                EvidenceReference = "test-blocker", OccurredAt = Now
            });
            Assert.Equal(PublishOutcome.Conflict,
                (await Publish(published, draft, reviewer)).Outcome);
            Assert.Equal(DemandRevisionState.Approved,
                (await drafts.ReadAsync(draft.TenantId, draft.LegalEntityId,
                    draft.Id, default))?.State);
            Assert.Equal(0, await context.PublishedRevisionManifests.CountDocumentsAsync(x =>
                x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId));
            Assert.Equal(0, await context.PublishedBaselineSlots.CountDocumentsAsync(x =>
                x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId));
            Assert.Equal(0, await context.Outbox.CountDocumentsAsync(x =>
                x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId));
            Assert.Equal(0, await context.ManualDraftPublicationAudit.CountDocumentsAsync(x =>
                x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
                x.RevisionId == draft.Id));
        }
        finally
        {
            await context.AuditEntries.Indexes.DropOneAsync(
                "mod0188_test_publish_audit_failure");
        }
    }

    [ManualDraftMongoFact]
    public async Task PublishedSnapshot_CorruptRowCannotBeReadPartially()
    {
        var (drafts, published, context) = Open();
        var draft = Draft(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), FirstWeek,
            secondSeries: true);
        var reviewer = Guid.NewGuid();
        await Approve(drafts, draft, draft.CreatedBy, reviewer);
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, draft, reviewer)).Outcome);
        var snapshot = (await published.ReadSnapshotAsync(draft.TenantId,
            draft.LegalEntityId, draft.Id, default))!.Value;
        Assert.Equal(snapshot.Manifest.Checksum,
            PublishedSnapshotIntegrity.Calculate(snapshot.Manifest,
                snapshot.Parts.Reverse().ToArray()));
        Assert.Throws<InvalidDataException>(() => PublishedSnapshotIntegrity.Calculate(
            snapshot.Manifest, snapshot.Parts.Take(1).ToArray()));
        Assert.Throws<InvalidDataException>(() => PublishedSnapshotIntegrity.Calculate(
            snapshot.Manifest, new[] { snapshot.Parts[0], snapshot.Parts[0] }));
        var part = snapshot.Parts[0];
        part.Rows[0] = part.Rows[0] with { Number = 2 };
        await context.PublishedRevisionParts.ReplaceOneAsync(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.Id == part.Id, part);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            published.ReadSnapshotAsync(draft.TenantId, draft.LegalEntityId,
                draft.Id, default));
        Assert.Null(await published.ReadSnapshotAsync(Guid.NewGuid(),
            draft.LegalEntityId, draft.Id, default));
    }
}

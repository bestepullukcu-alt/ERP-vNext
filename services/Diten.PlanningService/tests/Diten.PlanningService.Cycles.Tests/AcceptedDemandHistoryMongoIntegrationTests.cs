using Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using Diten.PlanningService.Persistence.Features.DemandPlanning;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed class AcceptedDemandHistoryMongoIntegrationTests
{
    private sealed class FixedBaseUnitChecker(string baseUnit) : IHistoryRowReferenceChecker
    {
        public Task<HistoryReferenceResult> CheckAsync(Guid tenantId, Guid legalEntityId,
            Guid skuId, string skuLevel, string warehouseId, DateOnly occurredOn,
            string originalUomId, CancellationToken cancellationToken) => Task.FromResult(
            new HistoryReferenceResult(HistoryReferenceState.Verified, baseUnit, 1m,
                "fixture", "v1.1"));
    }

    private sealed class FailingAuditWriter : IHistoryAcceptanceAuditWriter
    {
        public Task AppendAsync(IClientSessionHandle session, DemandHistoryAcceptanceAudit audit,
            CancellationToken cancellationToken) => throw new IOException("Injected audit failure");
    }

    private sealed class SilentAuditWriter : IHistoryAcceptanceAuditWriter
    {
        public Task AppendAsync(IClientSessionHandle session, DemandHistoryAcceptanceAudit audit,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static (DemandPlanningMongoContext Context, IMongoDatabase Database) Open()
    {
        var uri = Environment.GetEnvironmentVariable("MOD0188_TEST_MONGO_URI")
            ?? throw new InvalidOperationException("Explicit test Mongo URI is required.");
        var url = new MongoUrl(uri);
        var servers = url.Servers.ToArray();
        if (servers.Length != 1 || servers[0].Host != "127.0.0.1" ||
            servers[0].Port is < 31994 or > 39994 || url.ReplicaSetName != "rs-mod0188" ||
            url.Username is not null || url.Password is not null)
            throw new InvalidOperationException("Only the explicit loopback replica set is allowed.");
        const string databaseName = "mod0188_tests";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = uri,
                ["Mongo:SupplyChainDatabaseName"] = databaseName
            }).Build();
        return (new DemandPlanningMongoContext(configuration),
            new MongoClient(uri).GetDatabase(databaseName));
    }

    private static async Task<(DemandHistoryImportBatch Batch, AcceptedDemandHistoryRow Row,
        DemandHistoryAcceptanceAudit Audit)> SeedAsync(DemandPlanningMongoContext context,
        Guid? tenant = null, Guid? legalEntity = null, Guid? sku = null,
        string unit = "CASE", bool pending = false, bool includeZeroWeek = false)
    {
        var tenantId = tenant ?? Guid.NewGuid();
        var legalEntityId = legalEntity ?? Guid.NewGuid();
        var skuId = sku ?? Guid.NewGuid();
        var reviewer = Guid.NewGuid();
        var batch = new DemandHistoryImportBatch
        {
            TenantId = tenantId, LegalEntityId = legalEntityId,
            UploadedByActorId = Guid.NewGuid(), SourceSystem = "fixture",
            SourceFileSha256 = new string('A', 64),
            CoverageFileSha256 = new string('B', 64),
            CoverageWeeks = [new ImportWeekCoverage
            {
                SkuId = skuId, SkuLevel = "Gsku", WarehouseId = "wh-01", UomId = unit,
                WeekStart = new DateOnly(2026, 10, 5)
            }],
            ScopeFrom = new DateOnly(2026, 10, 1),
            ScopeThrough = new DateOnly(2026, 10, 31),
            WarehouseScope = ["wh-01"],
            ReviewState = ImportBatchReviewState.Approved,
            ReviewedByActorId = reviewer,
            Rows = [new DemandHistoryImportRow
            {
                RowNumber = 2, SourceRecordKey = Guid.NewGuid().ToString("N"),
                SkuId = skuId, SkuLevel = "Gsku", WarehouseId = "wh-01",
                OccurredOn = new DateOnly(2026, 10, 7), OriginalQuantity = 5,
                OriginalUomId = unit, BaseQuantity = pending ? null : 50,
                BaseUomId = pending ? null : "EA",
                ConversionFactor = pending ? null : 10,
                ConversionContractId = pending ? null : "fixture-uom",
                ConversionContractVersion = pending ? null : "1", RecordKind = "Shipment",
                UnitVerificationPending = pending
            }],
            AuditTrail = [new DemandHistoryImportAuditAction
            {
                Action = "BatchApproved", ActorId = reviewer, OccurredAt = DateTimeOffset.UtcNow,
                Detail = string.Empty
            }]
        };
        if (includeZeroWeek)
            batch.CoverageWeeks.Add(new ImportWeekCoverage
            {
                SkuId = skuId, SkuLevel = "Gsku", WarehouseId = "wh-01", UomId = unit,
                WeekStart = new DateOnly(2026, 10, 12)
            });
        batch.CoverageContentSha256 = ImportCoverageFingerprint.Compute(batch.CoverageWeeks);
        batch.AuditTrail[0].Detail = "coverage=" + batch.CoverageFileSha256 +
            ";content=" + batch.CoverageContentSha256;
        await context.HistoryImportBatches.InsertOneAsync(batch);
        var source = batch.Rows[0];
        var acceptedAt = DateTimeOffset.UtcNow;
        var row = new AcceptedDemandHistoryRow
        {
            TenantId = tenantId, LegalEntityId = legalEntityId,
            BatchId = batch.Id, BatchRowNumber = source.RowNumber,
            SourceSystem = batch.SourceSystem, SourceRecordKey = source.SourceRecordKey,
            SourceFileSha256 = batch.SourceFileSha256, SkuId = source.SkuId!.Value,
            SkuLevel = source.SkuLevel, WarehouseId = source.WarehouseId,
            OccurredOn = source.OccurredOn!.Value, OriginalQuantity = 5,
            OriginalUomId = unit, BaseQuantity = pending ? 0 : 50,
            BaseUomId = pending ? string.Empty : "EA",
            ConversionFactor = pending ? 0 : 10,
            ConversionContractId = pending ? string.Empty : "fixture-uom",
            ConversionContractVersion = pending ? string.Empty : "1",
            AcceptedQuantity = pending ? 5 : 50,
            AcceptedUomId = pending ? unit : "EA", UnitVerificationPending = pending,
            AcceptedByActorId = Guid.NewGuid(), AcceptedAt = acceptedAt,
            AcceptanceReason = "Verified source row", RequestKey = Guid.NewGuid().ToString("N"),
            RequestFingerprint = new string('C', 64)
        };
        row.ContentSha256 = AcceptedHistoryFingerprint.Compute(row);
        var audit = new DemandHistoryAcceptanceAudit
        {
            TenantId = tenantId, LegalEntityId = legalEntityId,
            AcceptedRowId = row.Id, BatchId = batch.Id,
            BatchRowNumber = row.BatchRowNumber, SourceRecordKey = row.SourceRecordKey,
            ActorId = row.AcceptedByActorId, OccurredAt = acceptedAt,
            Reason = row.AcceptanceReason, ContentSha256 = row.ContentSha256
        };
        return (batch, row, audit);
    }

    [ManualDraftMongoFact]
    public async Task Acceptance_and_audit_commit_together_and_retry_has_one_effect()
    {
        var (context, _) = Open();
        var (batch, row, audit) = await SeedAsync(context);
        var store = new AcceptedDemandHistoryMongoStore(context,
            new HistoryAcceptanceAuditMongoWriter(context));
        var created = await store.AcceptAsync(batch, row, audit, default);
        Assert.Equal(HistoryAcceptanceOutcome.Created, created.Outcome);
        var same = await store.AcceptAsync(batch, row, audit, default);
        Assert.Equal(HistoryAcceptanceOutcome.Existing, same.Outcome);
        Assert.Equal(row.Id, same.Row?.Id);
        var view = await store.GetAsync(batch.TenantId, batch.LegalEntityId, row.Id, default);
        Assert.Equal(row.Id, view?.Audit.AcceptedRowId);
        Assert.Equal("RowAccepted", view?.Audit.Action);
        Assert.Equal("Unknown", view?.Row.StockoutEvidenceState);
        Assert.Equal(1, await context.AcceptedHistoryRows.CountDocumentsAsync(
            x => x.TenantId == batch.TenantId));
        Assert.Equal(1, await context.HistoryAcceptanceAudit.CountDocumentsAsync(
            x => x.TenantId == batch.TenantId));
    }

    [ManualDraftMongoFact]
    public async Task Audit_failure_rolls_back_row_and_batch_fence()
    {
        var (context, _) = Open();
        var (batch, row, audit) = await SeedAsync(context);
        var store = new AcceptedDemandHistoryMongoStore(context, new FailingAuditWriter());
        var result = await store.AcceptAsync(batch, row, audit, default);
        Assert.Equal(HistoryAcceptanceOutcome.Unavailable, result.Outcome);
        Assert.Equal(0, await context.AcceptedHistoryRows.CountDocumentsAsync(
            x => x.TenantId == batch.TenantId));
        Assert.Equal(0, await context.HistoryAcceptanceAudit.CountDocumentsAsync(
            x => x.TenantId == batch.TenantId));
        var unchanged = await context.HistoryImportBatches.Find(x => x.Id == batch.Id &&
            x.TenantId == batch.TenantId).SingleAsync();
        Assert.Equal(batch.Version, unchanged.Version);
    }

    [ManualDraftMongoFact]
    public async Task Silent_audit_loss_cannot_commit_accepted_history()
    {
        var (context, _) = Open();
        var (batch, row, audit) = await SeedAsync(context);
        var store = new AcceptedDemandHistoryMongoStore(context, new SilentAuditWriter());
        var result = await store.AcceptAsync(batch, row, audit, default);
        Assert.Equal(HistoryAcceptanceOutcome.Unavailable, result.Outcome);
        Assert.Equal(0, await context.AcceptedHistoryRows.CountDocumentsAsync(
            x => x.TenantId == batch.TenantId));
        Assert.Equal(0, await context.HistoryAcceptanceAudit.CountDocumentsAsync(
            x => x.TenantId == batch.TenantId));
        var unchanged = await context.HistoryImportBatches.Find(x => x.Id == batch.Id &&
            x.TenantId == batch.TenantId).SingleAsync();
        Assert.Equal(batch.Version, unchanged.Version);
    }

    [ManualDraftMongoFact]
    public async Task PendingUnitRows_KeepUnitMarker_AndRejectDifferentUnitInLaterBatch()
    {
        var (context, _) = Open();
        var store = new AcceptedDemandHistoryMongoStore(context,
            new HistoryAcceptanceAuditMongoWriter(context));
        var first = await SeedAsync(context, pending: true);
        Assert.Equal(HistoryAcceptanceOutcome.Created,
            (await store.AcceptAsync(first.Batch, first.Row, first.Audit, default)).Outcome);
        var view = await store.GetAsync(first.Batch.TenantId, first.Batch.LegalEntityId,
            first.Row.Id, default);
        Assert.True(view!.Row.UnitVerificationPending);
        Assert.Equal("CASE", view.Row.AcceptedUomId);
        Assert.Equal(5m, view.Row.AcceptedQuantity);
        Assert.Equal(string.Empty, view.Row.BaseUomId);

        var page = await store.ListAsync(first.Batch.TenantId, first.Batch.LegalEntityId,
            0, 50, default);
        Assert.True(Assert.Single(page).Row.UnitVerificationPending);
        Assert.Equal(first.Row.Id, Assert.Single(page).Audit.AcceptedRowId);

        var same = await SeedAsync(context, first.Batch.TenantId, first.Batch.LegalEntityId,
            first.Row.SkuId, unit: "CASE", pending: true);
        Assert.Equal(HistoryAcceptanceOutcome.Created,
            (await store.AcceptAsync(same.Batch, same.Row, same.Audit, default)).Outcome);

        var different = await SeedAsync(context, first.Batch.TenantId, first.Batch.LegalEntityId,
            first.Row.SkuId, unit: "EA", pending: true);
        Assert.Equal(HistoryAcceptanceOutcome.Ineligible,
            (await store.AcceptAsync(different.Batch, different.Row, different.Audit, default)).Outcome);
        Assert.Equal(2, await context.AcceptedHistoryRows.CountDocumentsAsync(
            x => x.TenantId == first.Batch.TenantId));
        Assert.Equal(2, await context.HistoryAcceptanceAudit.CountDocumentsAsync(
            x => x.TenantId == first.Batch.TenantId));
    }

    [ManualDraftMongoFact]
    public async Task ConcurrentDifferentUnits_NeverBothEnterAcceptedHistory()
    {
        var (context, _) = Open();
        var store = new AcceptedDemandHistoryMongoStore(context,
            new HistoryAcceptanceAuditMongoWriter(context));
        var first = await SeedAsync(context, pending: true);
        var second = await SeedAsync(context, first.Batch.TenantId,
            first.Batch.LegalEntityId, first.Row.SkuId, unit: "EA", pending: true);

        var results = await Task.WhenAll(
            store.AcceptAsync(first.Batch, first.Row, first.Audit, default),
            store.AcceptAsync(second.Batch, second.Row, second.Audit, default));

        Assert.Single(results, x => x.Outcome == HistoryAcceptanceOutcome.Created);
        Assert.Equal(1, await context.AcceptedHistoryRows.CountDocumentsAsync(
            x => x.TenantId == first.Batch.TenantId));
        Assert.Equal(1, await context.HistoryAcceptanceAudit.CountDocumentsAsync(
            x => x.TenantId == first.Batch.TenantId));
        Assert.Equal(1, await context.AcceptedHistoryUnitSlots.CountDocumentsAsync(
            x => x.TenantId == first.Batch.TenantId));
    }

    [ManualDraftMongoFact]
    public async Task AcceptedHistoryList_FailsClosedWhenAuditIsMissing()
    {
        var (context, _) = Open();
        var store = new AcceptedDemandHistoryMongoStore(context,
            new HistoryAcceptanceAuditMongoWriter(context));
        var (batch, row, audit) = await SeedAsync(context, pending: true);
        Assert.Equal(HistoryAcceptanceOutcome.Created,
            (await store.AcceptAsync(batch, row, audit, default)).Outcome);
        await context.HistoryAcceptanceAudit.DeleteOneAsync(x => x.Id == audit.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.ListAsync(batch.TenantId, batch.LegalEntityId, 0, 50, default));
    }

    [ManualDraftMongoFact]
    public async Task WeeklyReader_RequiresSeparateRowAcceptanceBeforeDemandOrZero()
    {
        var (context, _) = Open();
        var (batch, row, audit) = await SeedAsync(context, pending: true, includeZeroWeek: true);
        var reader = new AcceptedWeeklyHistoryMongoReader(context);
        var before = await reader.ReadAsync(batch.TenantId, batch.LegalEntityId,
            row.SkuId, row.SkuLevel, row.WarehouseId, new DateOnly(2026, 10, 19), 2, default);
        Assert.Equal(AcceptedWeekState.IncompleteAcceptance, before.Weeks[0].State);
        Assert.Equal(AcceptedWeekState.VerifiedZeroDemand, before.Weeks[1].State);

        var store = new AcceptedDemandHistoryMongoStore(context,
            new HistoryAcceptanceAuditMongoWriter(context));
        Assert.Equal(HistoryAcceptanceOutcome.Created,
            (await store.AcceptAsync(batch, row, audit, default)).Outcome);
        var after = await reader.ReadAsync(batch.TenantId, batch.LegalEntityId,
            row.SkuId, row.SkuLevel, row.WarehouseId, new DateOnly(2026, 10, 19), 2, default);
        Assert.Equal(AcceptedWeekState.VerifiedDemand, after.Weeks[0].State);
        Assert.Equal(row.Id, Assert.Single(after.Weeks[0].Events).AcceptedRowId);
        Assert.True(after.Weeks[0].UnitVerificationPending);
        Assert.Equal(AcceptedWeekState.VerifiedZeroDemand, after.Weeks[1].State);
        Assert.Empty(after.Weeks[1].Events);
    }

    [ManualDraftMongoFact]
    public async Task ConvertedRow_PersistsOriginalAndRoundedBaseWithAudit()
    {
        var (context, _) = Open();
        var (batch, row, audit) = await SeedAsync(context);
        batch.Rows[0].OriginalQuantity = 1m;
        batch.Rows[0].BaseQuantity = 0.3333m;
        batch.Rows[0].ConversionFactor = 1m / 3m;
        batch.Rows[0].ConversionNumerator = 1m;
        batch.Rows[0].ConversionDenominator = 3m;
        batch.Rows[0].ConversionContractId = "PRODUCT-MASTER-BUNDLE/getSkuUom/test";
        batch.Rows[0].ConversionContractVersion = "v1.1";
        await context.HistoryImportBatches.ReplaceOneAsync(x => x.Id == batch.Id, batch);
        row.OriginalQuantity = 1m;
        row.BaseQuantity = 0.3333m;
        row.AcceptedQuantity = 0.3333m;
        row.ConversionFactor = 1m / 3m;
        row.ConversionNumerator = 1m;
        row.ConversionDenominator = 3m;
        row.ConversionContractId = batch.Rows[0].ConversionContractId!;
        row.ConversionContractVersion = "v1.1";
        row.ContentSha256 = AcceptedHistoryFingerprint.Compute(row);
        audit.ContentSha256 = row.ContentSha256;
        var store = new AcceptedDemandHistoryMongoStore(context,
            new HistoryAcceptanceAuditMongoWriter(context));

        Assert.Equal(HistoryAcceptanceOutcome.Created,
            (await store.AcceptAsync(batch, row, audit, default)).Outcome);
        var saved = await store.GetAsync(batch.TenantId, batch.LegalEntityId, row.Id, default);
        Assert.NotNull(saved);
        Assert.Equal(1m, saved.Row.OriginalQuantity);
        Assert.Equal("CASE", saved.Row.OriginalUomId);
        Assert.Equal(0.3333m, saved.Row.AcceptedQuantity);
        Assert.Equal("EA", saved.Row.AcceptedUomId);
        Assert.Equal(1m, saved.Row.ConversionNumerator);
        Assert.Equal(3m, saved.Row.ConversionDenominator);
        Assert.Equal(row.ContentSha256, saved.Audit.ContentSha256);
    }

    [ManualDraftMongoFact]
    public async Task LegacyRow_BaseComparisonChangesEffectiveWarningWithoutRewritingAcceptance()
    {
        var (context, _) = Open();
        var (batch, row, audit) = await SeedAsync(context, pending: true);
        var store = new AcceptedDemandHistoryMongoStore(context,
            new HistoryAcceptanceAuditMongoWriter(context));
        Assert.Equal(HistoryAcceptanceOutcome.Created,
            (await store.AcceptAsync(batch, row, audit, default)).Outcome);
        var originalHash = row.ContentSha256;

        var matching = await new AcceptedWeeklyHistoryMongoReader(context,
            new FixedBaseUnitChecker("CASE")).ReadAsync(batch.TenantId, batch.LegalEntityId,
            row.SkuId, row.SkuLevel, row.WarehouseId, new DateOnly(2026, 10, 12), 1, default);
        var different = await new AcceptedWeeklyHistoryMongoReader(context,
            new FixedBaseUnitChecker("EA")).ReadAsync(batch.TenantId, batch.LegalEntityId,
            row.SkuId, row.SkuLevel, row.WarehouseId, new DateOnly(2026, 10, 12), 1, default);

        Assert.False(Assert.Single(matching.Weeks).UnitVerificationPending);
        Assert.True(Assert.Single(different.Weeks).UnitVerificationPending);
        Assert.Equal(AcceptedWeekState.VerifiedDemand, different.Weeks[0].State);
        var persisted = await store.GetAsync(batch.TenantId, batch.LegalEntityId, row.Id, default);
        Assert.True(persisted!.Row.UnitVerificationPending);
        Assert.Equal(originalHash, persisted.Row.ContentSha256);
        Assert.Equal(originalHash, persisted.Audit.ContentSha256);
    }

    [ManualDraftMongoFact]
    public async Task DifferentOriginalUnitsWithSameVerifiedBaseShareOneUnitSlot()
    {
        var (context, _) = Open();
        var store = new AcceptedDemandHistoryMongoStore(context,
            new HistoryAcceptanceAuditMongoWriter(context));
        var first = await SeedAsync(context);
        Assert.Equal(HistoryAcceptanceOutcome.Created,
            (await store.AcceptAsync(first.Batch, first.Row, first.Audit, default)).Outcome);

        var second = await SeedAsync(context, first.Batch.TenantId,
            first.Batch.LegalEntityId, first.Row.SkuId, unit: "EA");
        second.Batch.Rows[0].BaseQuantity = 5m;
        second.Batch.Rows[0].ConversionFactor = 1m;
        await context.HistoryImportBatches.ReplaceOneAsync(x => x.Id == second.Batch.Id,
            second.Batch);
        second.Row.BaseQuantity = 5m;
        second.Row.AcceptedQuantity = 5m;
        second.Row.ConversionFactor = 1m;
        second.Row.ContentSha256 = AcceptedHistoryFingerprint.Compute(second.Row);
        second.Audit.ContentSha256 = second.Row.ContentSha256;

        Assert.Equal(HistoryAcceptanceOutcome.Created,
            (await store.AcceptAsync(second.Batch, second.Row, second.Audit, default)).Outcome);
        var page = await store.ListAsync(first.Batch.TenantId, first.Batch.LegalEntityId,
            0, 50, default);
        Assert.Equal(2, page.Count);
        Assert.Equal(new[] { "CASE", "EA" }, page.Select(x => x.Row.OriginalUomId)
            .OrderBy(x => x).ToArray());
        Assert.All(page, item => Assert.Equal("EA", item.Row.AcceptedUomId));
        Assert.Equal(1, await context.AcceptedHistoryUnitSlots.CountDocumentsAsync(x =>
            x.TenantId == first.Batch.TenantId && x.LegalEntityId == first.Batch.LegalEntityId &&
            x.SkuId == first.Row.SkuId));
    }

    [ManualDraftMongoFact]
    public async Task WeeklyReader_UnknownStockOrTamperedCoverageNeverBecomesZero()
    {
        var (context, _) = Open();
        var (batch, row, _) = await SeedAsync(context, pending: true, includeZeroWeek: true);
        var reader = new AcceptedWeeklyHistoryMongoReader(context);
        await context.HistoryImportBatches.UpdateOneAsync(x => x.Id == batch.Id,
            Builders<DemandHistoryImportBatch>.Update.Set("CoverageWeeks.1.StockState",
                ImportWeekStockState.Unknown));
        var tampered = await reader.ReadAsync(batch.TenantId, batch.LegalEntityId,
            row.SkuId, row.SkuLevel, row.WarehouseId, new DateOnly(2026, 10, 19), 2, default);
        Assert.Equal(AcceptedWeekState.Quarantined, tampered.Weeks[1].State);

        var saved = await context.HistoryImportBatches.Find(x => x.Id == batch.Id).SingleAsync();
        saved.CoverageContentSha256 = ImportCoverageFingerprint.Compute(saved.CoverageWeeks);
        saved.AuditTrail[0].Detail = "coverage=" + saved.CoverageFileSha256 +
            ";content=" + saved.CoverageContentSha256;
        await context.HistoryImportBatches.ReplaceOneAsync(x => x.Id == batch.Id, saved);
        var unknown = await reader.ReadAsync(batch.TenantId, batch.LegalEntityId,
            row.SkuId, row.SkuLevel, row.WarehouseId, new DateOnly(2026, 10, 19), 2, default);
        Assert.Equal(AcceptedWeekState.StockoutUnresolved, unknown.Weeks[1].State);
    }

    [ManualDraftMongoFact]
    public async Task WeeklyReader_UndeclaredWeeksAndOtherTenantStayUnknown()
    {
        var (context, _) = Open();
        var (batch, row, _) = await SeedAsync(context, pending: true, includeZeroWeek: true);
        var reader = new AcceptedWeeklyHistoryMongoReader(context);
        var history = await reader.ReadAsync(batch.TenantId, batch.LegalEntityId,
            row.SkuId, row.SkuLevel, row.WarehouseId, new DateOnly(2026, 10, 26), 4, default);
        Assert.Equal(new[] { AcceptedWeekState.Unknown, AcceptedWeekState.IncompleteAcceptance,
            AcceptedWeekState.VerifiedZeroDemand, AcceptedWeekState.Unknown },
            history.Weeks.Select(week => week.State));

        var otherTenant = await reader.ReadAsync(Guid.NewGuid(), batch.LegalEntityId,
            row.SkuId, row.SkuLevel, row.WarehouseId, new DateOnly(2026, 10, 26), 4, default);
        Assert.All(otherTenant.Weeks, week => Assert.Equal(AcceptedWeekState.Unknown, week.State));
    }
}

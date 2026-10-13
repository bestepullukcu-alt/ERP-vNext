using Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using Diten.PlanningService.Persistence.Features.DemandPlanning;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed class AcceptedDemandHistoryMongoIntegrationTests
{
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
        DemandHistoryAcceptanceAudit Audit)> SeedAsync(DemandPlanningMongoContext context)
    {
        var tenantId = Guid.NewGuid();
        var legalEntityId = Guid.NewGuid();
        var reviewer = Guid.NewGuid();
        var batch = new DemandHistoryImportBatch
        {
            TenantId = tenantId, LegalEntityId = legalEntityId,
            UploadedByActorId = Guid.NewGuid(), SourceSystem = "fixture",
            SourceFileSha256 = new string('A', 64),
            ScopeFrom = new DateOnly(2026, 10, 1),
            ScopeThrough = new DateOnly(2026, 10, 31),
            WarehouseScope = ["wh-01"],
            ReviewState = ImportBatchReviewState.Approved,
            ReviewedByActorId = reviewer,
            Rows = [new DemandHistoryImportRow
            {
                RowNumber = 2, SourceRecordKey = Guid.NewGuid().ToString("N"),
                SkuId = Guid.NewGuid(), SkuLevel = "Gsku", WarehouseId = "wh-01",
                OccurredOn = new DateOnly(2026, 10, 7), OriginalQuantity = 5,
                OriginalUomId = "CASE", BaseQuantity = 50, BaseUomId = "EA",
                ConversionFactor = 10, ConversionContractId = "fixture-uom",
                ConversionContractVersion = "1", RecordKind = "Shipment"
            }],
            AuditTrail = [new DemandHistoryImportAuditAction
            {
                Action = "BatchApproved", ActorId = reviewer, OccurredAt = DateTimeOffset.UtcNow
            }]
        };
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
            OriginalUomId = "CASE", BaseQuantity = 50, BaseUomId = "EA",
            ConversionFactor = 10, ConversionContractId = "fixture-uom",
            ConversionContractVersion = "1",
            AcceptedByActorId = Guid.NewGuid(), AcceptedAt = acceptedAt,
            AcceptanceReason = "Verified source row", RequestKey = "request-1",
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
}

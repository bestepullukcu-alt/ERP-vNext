using Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using MongoDB.Driver;

namespace Diten.PlanningService.Persistence.Features.DemandPlanning;

public interface IHistoryAcceptanceAuditWriter
{
    Task AppendAsync(IClientSessionHandle session, DemandHistoryAcceptanceAudit audit,
        CancellationToken cancellationToken);
}

public sealed class HistoryAcceptanceAuditMongoWriter : IHistoryAcceptanceAuditWriter
{
    private readonly IMongoCollection<DemandHistoryAcceptanceAudit> _audit;
    public HistoryAcceptanceAuditMongoWriter(DemandPlanningMongoContext context) =>
        _audit = context.HistoryAcceptanceAudit;

    public Task AppendAsync(IClientSessionHandle session, DemandHistoryAcceptanceAudit audit,
        CancellationToken cancellationToken) =>
        _audit.InsertOneAsync(session, audit, cancellationToken: cancellationToken);
}

public sealed class AcceptedDemandHistoryMongoStore : IAcceptedDemandHistoryStore
{
    private readonly DemandPlanningMongoContext _context;
    private readonly IMongoCollection<DemandHistoryImportBatch> _batches;
    private readonly IMongoCollection<AcceptedDemandHistoryRow> _rows;
    private readonly IMongoCollection<DemandHistoryAcceptanceAudit> _audit;
    private readonly IHistoryAcceptanceAuditWriter _auditWriter;
    private readonly SemaphoreSlim _indexGate = new(1, 1);
    private bool _indexReady;

    public AcceptedDemandHistoryMongoStore(DemandPlanningMongoContext context,
        IHistoryAcceptanceAuditWriter auditWriter)
    {
        _context = context;
        _batches = context.HistoryImportBatches;
        _rows = context.AcceptedHistoryRows;
        _audit = context.HistoryAcceptanceAudit;
        _auditWriter = auditWriter;
    }

    public async Task<HistoryAcceptanceResult> AcceptAsync(DemandHistoryImportBatch expectedBatch,
        AcceptedDemandHistoryRow row, DemandHistoryAcceptanceAudit audit,
        CancellationToken cancellationToken)
    {
        if (row.StockoutEvidenceState != "Unknown" ||
            row.ContentSha256 != AcceptedHistoryFingerprint.Compute(row) ||
            audit.TenantId != row.TenantId || audit.LegalEntityId != row.LegalEntityId ||
            audit.AcceptedRowId != row.Id || audit.BatchId != row.BatchId ||
            audit.BatchRowNumber != row.BatchRowNumber || audit.SourceRecordKey != row.SourceRecordKey ||
            audit.Action != "RowAccepted" || audit.ActorId != row.AcceptedByActorId ||
            audit.OccurredAt != row.AcceptedAt || audit.Reason != row.AcceptanceReason ||
            audit.ContentSha256 != row.ContentSha256)
            return new HistoryAcceptanceResult(HistoryAcceptanceOutcome.Ineligible, null);
        await EnsureIndexesAsync(cancellationToken);
        using var session = await _context.StartSessionAsync(cancellationToken);
        try
        {
            session.StartTransaction(new TransactionOptions(
                readConcern: ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority));
            var batchFilter = Builders<DemandHistoryImportBatch>.Filter.Eq(x => x.TenantId, row.TenantId) &
                              Builders<DemandHistoryImportBatch>.Filter.Eq(x => x.LegalEntityId, row.LegalEntityId) &
                              Builders<DemandHistoryImportBatch>.Filter.Eq(x => x.Id, row.BatchId) &
                              Builders<DemandHistoryImportBatch>.Filter.Eq(x => x.IsDeleted, false) &
                              Builders<DemandHistoryImportBatch>.Filter.Eq(x => x.Version, expectedBatch.Version) &
                              Builders<DemandHistoryImportBatch>.Filter.Eq(x => x.ReviewState, ImportBatchReviewState.Approved);
            var current = await _batches.Find(session, batchFilter).FirstOrDefaultAsync(cancellationToken);
            if (current is null || !current.IsEligibleForAcceptedHistory() || !Matches(current, row))
            {
                await session.AbortTransactionAsync(cancellationToken);
                return await ResolveExistingAsync(row, HistoryAcceptanceOutcome.Ineligible,
                    cancellationToken);
            }
            var reusedKey = await _rows.Find(session, x => x.TenantId == row.TenantId &&
                x.LegalEntityId == row.LegalEntityId &&
                x.AcceptedByActorId == row.AcceptedByActorId &&
                x.RequestKey == row.RequestKey).FirstOrDefaultAsync(cancellationToken);
            if (reusedKey is not null)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return await ResolveExistingAsync(row, HistoryAcceptanceOutcome.Conflict,
                    cancellationToken);
            }
            var fence = await _batches.UpdateOneAsync(session, batchFilter,
                Builders<DemandHistoryImportBatch>.Update.Inc(x => x.Version, 1),
                cancellationToken: cancellationToken);
            if (fence.ModifiedCount != 1)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return new HistoryAcceptanceResult(HistoryAcceptanceOutcome.Ineligible, null);
            }
            await _rows.InsertOneAsync(session, row, cancellationToken: cancellationToken);
            await _auditWriter.AppendAsync(session, audit, cancellationToken);
            var writtenAudit = await _audit.Find(session, x => x.TenantId == row.TenantId &&
                x.LegalEntityId == row.LegalEntityId && x.AcceptedRowId == row.Id &&
                x.BatchId == row.BatchId && x.BatchRowNumber == row.BatchRowNumber &&
                x.SourceRecordKey == row.SourceRecordKey && x.ActorId == row.AcceptedByActorId &&
                x.OccurredAt == row.AcceptedAt && x.Action == "RowAccepted" &&
                x.Reason == row.AcceptanceReason && x.ContentSha256 == row.ContentSha256)
                .ToListAsync(cancellationToken);
            if (writtenAudit.Count != 1)
                throw new InvalidOperationException("Acceptance audit was not durably written.");
            await session.CommitTransactionAsync(cancellationToken);
            return new HistoryAcceptanceResult(HistoryAcceptanceOutcome.Created, row);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            if (session.IsInTransaction)
                await session.AbortTransactionAsync(CancellationToken.None);
            return await ResolveExistingAsync(row, HistoryAcceptanceOutcome.Unavailable,
                cancellationToken);
        }
    }

    private async Task<HistoryAcceptanceResult> ResolveExistingAsync(AcceptedDemandHistoryRow row,
        HistoryAcceptanceOutcome whenMissing, CancellationToken cancellationToken)
    {
        var existing = await _rows.Find(x => x.TenantId == row.TenantId &&
            x.LegalEntityId == row.LegalEntityId && x.SourceSystem == row.SourceSystem &&
            x.SourceRecordKey == row.SourceRecordKey).FirstOrDefaultAsync(cancellationToken);
        if (existing is null)
        {
            var reusedKey = await _rows.Find(x => x.TenantId == row.TenantId &&
                x.LegalEntityId == row.LegalEntityId &&
                x.AcceptedByActorId == row.AcceptedByActorId &&
                x.RequestKey == row.RequestKey).FirstOrDefaultAsync(cancellationToken);
            return new HistoryAcceptanceResult(reusedKey is null ? whenMissing :
                HistoryAcceptanceOutcome.Conflict, null);
        }
        if (existing.ContentSha256 != AcceptedHistoryFingerprint.Compute(existing))
            return new HistoryAcceptanceResult(HistoryAcceptanceOutcome.Unavailable, null);
        var matchingRequest = existing.BatchId == row.BatchId &&
            existing.BatchRowNumber == row.BatchRowNumber &&
            existing.AcceptedByActorId == row.AcceptedByActorId &&
            existing.RequestKey == row.RequestKey &&
            existing.RequestFingerprint == row.RequestFingerprint &&
            existing.ContentSha256 == row.ContentSha256;
        var matchingAudit = await _audit.Find(x => x.TenantId == row.TenantId &&
            x.LegalEntityId == row.LegalEntityId && x.AcceptedRowId == existing.Id &&
            x.Action == "RowAccepted" && x.ContentSha256 == existing.ContentSha256)
            .FirstOrDefaultAsync(cancellationToken);
        if (matchingAudit is null)
            return new HistoryAcceptanceResult(HistoryAcceptanceOutcome.Unavailable, null);
        return new HistoryAcceptanceResult(matchingRequest ? HistoryAcceptanceOutcome.Existing :
            HistoryAcceptanceOutcome.Conflict, matchingRequest ? existing : null);
    }

    public async Task<AcceptedDemandHistoryView?> GetAsync(Guid tenantId, Guid legalEntityId,
        Guid acceptedRowId, CancellationToken cancellationToken)
    {
        var row = await _rows.Find(x => x.TenantId == tenantId &&
            x.LegalEntityId == legalEntityId && x.Id == acceptedRowId && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null) return null;
        if (row.ContentSha256 != AcceptedHistoryFingerprint.Compute(row))
            throw new InvalidOperationException("Accepted row content is inconsistent.");
        var audit = await _audit.Find(x => x.TenantId == tenantId &&
            x.LegalEntityId == legalEntityId && x.AcceptedRowId == row.Id &&
            x.BatchId == row.BatchId && x.BatchRowNumber == row.BatchRowNumber &&
            x.SourceRecordKey == row.SourceRecordKey && x.Action == "RowAccepted" &&
            x.ActorId == row.AcceptedByActorId && x.OccurredAt == row.AcceptedAt &&
            x.ContentSha256 == row.ContentSha256).ToListAsync(cancellationToken);
        if (audit.Count != 1) throw new InvalidOperationException("Accepted row audit is missing or duplicated.");
        return new AcceptedDemandHistoryView(row, audit[0]);
    }

    private static bool Matches(DemandHistoryImportBatch batch, AcceptedDemandHistoryRow row)
    {
        if (batch.SourceChannel != "File" || batch.SourceSystem != row.SourceSystem ||
            batch.SourceFileSha256 != row.SourceFileSha256 || batch.Id != row.BatchId ||
            batch.Rows.Count(x => x.RowNumber == row.BatchRowNumber) != 1)
            return false;
        var source = batch.Rows.Single(x => x.RowNumber == row.BatchRowNumber);
        return source.RecordKind == "Shipment" && !source.IsQuarantined &&
               !batch.QuarantineCases.Any(x => x.RowNumber == source.RowNumber && x.State == "Open") &&
               source.SourceRecordKey == row.SourceRecordKey && source.SkuId == row.SkuId &&
               source.SkuLevel == row.SkuLevel && source.WarehouseId == row.WarehouseId &&
               source.OccurredOn == row.OccurredOn && source.OriginalQuantity == row.OriginalQuantity &&
               source.OriginalUomId == row.OriginalUomId && source.BaseQuantity == row.BaseQuantity &&
               source.BaseUomId == row.BaseUomId && source.ConversionFactor == row.ConversionFactor &&
               source.ConversionContractId == row.ConversionContractId &&
               source.ConversionContractVersion == row.ConversionContractVersion;
    }

    private async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        if (_indexReady) return;
        await _indexGate.WaitAsync(cancellationToken);
        try
        {
            if (_indexReady) return;
            await _rows.Indexes.CreateOneAsync(new CreateIndexModel<AcceptedDemandHistoryRow>(
                Builders<AcceptedDemandHistoryRow>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.LegalEntityId).Ascending(x => x.SourceSystem)
                    .Ascending(x => x.SourceRecordKey),
                new CreateIndexOptions { Name = "ux_mod0188_accepted_source", Unique = true }),
                cancellationToken: cancellationToken);
            await _audit.Indexes.CreateOneAsync(new CreateIndexModel<DemandHistoryAcceptanceAudit>(
                Builders<DemandHistoryAcceptanceAudit>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.LegalEntityId).Ascending(x => x.AcceptedRowId),
                new CreateIndexOptions { Name = "ux_mod0188_acceptance_audit_row", Unique = true }),
                cancellationToken: cancellationToken);
            await _rows.Indexes.CreateOneAsync(new CreateIndexModel<AcceptedDemandHistoryRow>(
                Builders<AcceptedDemandHistoryRow>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.LegalEntityId).Ascending(x => x.AcceptedByActorId)
                    .Ascending(x => x.RequestKey),
                new CreateIndexOptions { Name = "ux_mod0188_acceptance_request", Unique = true }),
                cancellationToken: cancellationToken);
            _indexReady = true;
        }
        finally { _indexGate.Release(); }
    }
}

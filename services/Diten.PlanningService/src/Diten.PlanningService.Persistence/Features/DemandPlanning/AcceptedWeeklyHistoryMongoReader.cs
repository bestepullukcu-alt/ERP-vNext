using Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using MongoDB.Driver;

namespace Diten.PlanningService.Persistence.Features.DemandPlanning;

public sealed class AcceptedWeeklyHistoryMongoReader : IAcceptedWeeklyHistoryReader
{
    private readonly DemandPlanningMongoContext _context;
    private readonly IHistoryRowReferenceChecker? _references;

    public AcceptedWeeklyHistoryMongoReader(DemandPlanningMongoContext context) => _context = context;

    public AcceptedWeeklyHistoryMongoReader(DemandPlanningMongoContext context,
        IHistoryRowReferenceChecker references)
    { _context = context; _references = references; }

    public async Task<AcceptedSeriesHistory> ReadAsync(Guid tenantId, Guid legalEntityId,
        Guid skuId, string skuLevel, string warehouseId, DateOnly asOfWeekStart,
        int weekCount, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || legalEntityId == Guid.Empty || skuId == Guid.Empty ||
            skuLevel is not ("Gsku" or "Lsku" or "FinishedGood") ||
            string.IsNullOrWhiteSpace(warehouseId) || asOfWeekStart.DayOfWeek != DayOfWeek.Monday ||
            weekCount is < 1 or > 156)
            throw new ArgumentException("Accepted history requires a complete series and ISO week window.");

        var from = asOfWeekStart.AddDays(-7 * weekCount);
        using var session = await _context.StartSessionAsync(cancellationToken);
        session.StartTransaction(new TransactionOptions(readConcern: ReadConcern.Snapshot));
        try
        {
            var batches = await _context.HistoryImportBatches.Find(session, x =>
                x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                x.ReviewState == ImportBatchReviewState.Approved && !x.IsDeleted)
                .Limit(1001).ToListAsync(cancellationToken);
            if (batches.Count > 1000)
                throw new InvalidOperationException("Accepted history batch limit exceeded.");
            var rows = await _context.AcceptedHistoryRows.Find(session, x =>
                x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                x.SkuId == skuId && x.WarehouseId == warehouseId &&
                x.OccurredOn >= from && x.OccurredOn < asOfWeekStart && !x.IsDeleted)
                .Limit(10001).ToListAsync(cancellationToken);
            if (rows.Count > 10000)
                throw new InvalidOperationException("Accepted history row limit exceeded.");
            var verifiedUnits = rows.Where(row => !row.UnitVerificationPending)
                .Select(row => row.BaseUomId).Distinct(StringComparer.Ordinal).ToArray();
            if (verifiedUnits.Length > 1)
                throw new InvalidOperationException("Accepted history has conflicting base units.");
            var verifiedUnit = verifiedUnits.SingleOrDefault();
            var rowIds = rows.Select(x => x.Id).ToArray();
            var audits = rowIds.Length == 0 ? [] :
                await _context.HistoryAcceptanceAudit.Find(session, x =>
                    x.TenantId == tenantId && x.LegalEntityId == legalEntityId &&
                    rowIds.Contains(x.AcceptedRowId)).ToListAsync(cancellationToken);
            await session.CommitTransactionAsync(cancellationToken);

            var pendingUnitStatus = "UnitVerificationPending";
            var pendingRow = rows.FirstOrDefault(row => row.UnitVerificationPending);
            if (pendingRow is not null && _references is not null)
                pendingUnitStatus = await AcceptedHistoryUnitProjection.CheckAsync(pendingRow,
                    _references, cancellationToken);

            var weeks = new List<AcceptedWeekObservation>(weekCount);
            for (var index = 0; index < weekCount; index++)
            {
                var start = from.AddDays(7 * index);
                var claims = batches.SelectMany(batch => batch.CoverageWeeks
                    .Where(week => week.SkuId == skuId && week.WarehouseId == warehouseId &&
                        week.WeekStart == start)
                    .Select(week => (Batch: batch, Coverage: week))).ToArray();
                if (claims.Length == 0)
                {
                    weeks.Add(new(start, AcceptedWeekState.Unknown, null, false, null, []));
                    continue;
                }
                if (claims.Length != 1 || claims[0].Coverage.SkuLevel != skuLevel ||
                    !claims[0].Batch.IsEligibleForAcceptedHistory())
                {
                    weeks.Add(new(start, AcceptedWeekState.Quarantined, null, false, null, []));
                    continue;
                }

                var (batch, claim) = claims[0];
                var source = batch.Rows.Where(row => row.SkuId == skuId &&
                    row.WarehouseId == warehouseId && row.OccurredOn >= start &&
                    row.OccurredOn < start.AddDays(7)).ToArray();
                var accepted = rows.Where(row => row.OccurredOn >= start &&
                    row.OccurredOn < start.AddDays(7)).ToArray();
                if (accepted.Any(row => row.BatchId != batch.Id || row.SkuLevel != skuLevel ||
                    row.OriginalUomId != claim.UomId ||
                    row.AcceptedUomId != (row.UnitVerificationPending ? claim.UomId : row.BaseUomId) ||
                    row.ContentSha256 != AcceptedHistoryFingerprint.Compute(row) ||
                    audits.Count(audit => audit.AcceptedRowId == row.Id &&
                        audit.BatchId == row.BatchId && audit.BatchRowNumber == row.BatchRowNumber &&
                        audit.SourceRecordKey == row.SourceRecordKey && audit.Action == "RowAccepted" &&
                        audit.ActorId == row.AcceptedByActorId && audit.OccurredAt == row.AcceptedAt &&
                        audit.ContentSha256 == row.ContentSha256) != 1) ||
                    source.Any(item => item.SkuLevel != skuLevel || item.OriginalUomId != claim.UomId ||
                        item.IsQuarantined || accepted.Count(row => row.BatchRowNumber == item.RowNumber &&
                            row.SourceRecordKey == item.SourceRecordKey &&
                            row.OriginalQuantity == item.OriginalQuantity) > 1))
                {
                    weeks.Add(new(start, AcceptedWeekState.Quarantined, null, false, null, []));
                    continue;
                }
                if (source.Length != accepted.Length || source.Any(item =>
                    !accepted.Any(row => row.BatchRowNumber == item.RowNumber &&
                        row.SourceRecordKey == item.SourceRecordKey &&
                        row.OriginalQuantity == item.OriginalQuantity)))
                {
                    weeks.Add(new(start, AcceptedWeekState.IncompleteAcceptance,
                        verifiedUnit ?? claim.UomId, false, batch.CoverageFileSha256, []));
                    continue;
                }
                if (claim.StockState is ImportWeekStockState.OutOfStock or ImportWeekStockState.Unknown)
                {
                    weeks.Add(new(start, AcceptedWeekState.StockoutUnresolved,
                        accepted.Any(row => row.UnitVerificationPending) ? claim.UomId :
                        verifiedUnit ?? claim.UomId,
                        accepted.Any(row => row.UnitVerificationPending) &&
                        pendingUnitStatus != "VerifiedBase",
                        batch.CoverageFileSha256, []));
                    continue;
                }
                var events = accepted.OrderBy(row => row.OccurredOn).ThenBy(row => row.Id)
                    .Select(row => new AcceptedWeekEvent(row.Id, row.OccurredOn,
                        row.AcceptedQuantity)).ToArray();
                var pending = accepted.Any(row => row.UnitVerificationPending) &&
                    pendingUnitStatus != "VerifiedBase";
                var originalPending = accepted.Any(row => row.UnitVerificationPending);
                weeks.Add(new(start, events.Length == 0 ? AcceptedWeekState.VerifiedZeroDemand :
                    AcceptedWeekState.VerifiedDemand, originalPending ? claim.UomId :
                    verifiedUnit ?? claim.UomId, pending, batch.CoverageFileSha256,
                    Array.AsReadOnly(events)));
            }

            return new AcceptedSeriesHistory(tenantId, legalEntityId, skuId, skuLevel,
                warehouseId, asOfWeekStart, weeks.AsReadOnly());
        }
        catch
        {
            if (session.IsInTransaction)
                await session.AbortTransactionAsync(cancellationToken);
            throw;
        }
    }
}

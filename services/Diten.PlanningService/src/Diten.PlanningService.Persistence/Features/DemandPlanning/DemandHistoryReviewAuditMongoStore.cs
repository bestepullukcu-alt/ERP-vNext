using System.Security.Cryptography;
using System.Text;
using Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using MongoDB.Driver;

namespace Diten.PlanningService.Persistence.Features.DemandPlanning;

public sealed class DemandHistoryReviewAuditMongoStore : IHistoryImportReviewAuditStore
{
    private readonly IMongoCollection<DemandHistoryReviewAttempt> _attempts;
    private readonly SemaphoreSlim _indexGate = new(1, 1);
    private bool _indexReady;

    public DemandHistoryReviewAuditMongoStore(DemandPlanningMongoContext context) =>
        _attempts = context.HistoryReviewAttempts;

    public async Task AppendOnceAsync(HistoryImportReviewFailure failure,
        CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);
        var dedupInput = $"{failure.BatchId:D}|{failure.ActorId:D}|{failure.RequestKey}|" +
                         $"{failure.Fingerprint}|{failure.Outcome}";
        var attempt = new DemandHistoryReviewAttempt
        {
            TenantId = failure.TenantId, LegalEntityId = failure.LegalEntityId,
            BatchId = failure.BatchId, ActorId = failure.ActorId,
            RequestKey = failure.RequestKey, Fingerprint = failure.Fingerprint,
            Outcome = failure.Outcome, Reason = failure.Reason,
            DedupKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(dedupInput))),
            OccurredAt = failure.OccurredAt
        };
        try { await _attempts.InsertOneAsync(attempt, cancellationToken: cancellationToken); }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        { /* Same failed request was already audited. */ }
    }

    public async Task<IReadOnlyList<HistoryImportReviewFailure>> ListAsync(Guid tenantId,
        Guid batchId, CancellationToken cancellationToken)
    {
        var filter = Builders<DemandHistoryReviewAttempt>.Filter.Eq(x => x.TenantId, tenantId) &
                     Builders<DemandHistoryReviewAttempt>.Filter.Eq(x => x.IsDeleted, false) &
                     Builders<DemandHistoryReviewAttempt>.Filter.Eq(x => x.BatchId, batchId);
        var attempts = await _attempts.Find(filter).SortBy(x => x.OccurredAt)
            .ToListAsync(cancellationToken);
        return attempts.Select(x => new HistoryImportReviewFailure(
            x.TenantId, x.LegalEntityId, x.BatchId, x.ActorId,
            x.RequestKey, x.Fingerprint, x.Outcome, x.Reason, x.OccurredAt)).ToList();
    }

    private async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        if (_indexReady) return;
        await _indexGate.WaitAsync(cancellationToken);
        try
        {
            if (_indexReady) return;
            var unique = Builders<DemandHistoryReviewAttempt>.IndexKeys
                .Ascending(x => x.TenantId).Ascending(x => x.IsDeleted)
                .Ascending(x => x.DedupKey);
            await _attempts.Indexes.CreateOneAsync(
                new CreateIndexModel<DemandHistoryReviewAttempt>(unique,
                    new CreateIndexOptions { Name = "ux_mod0188_review_failure_dedup", Unique = true }),
                cancellationToken: cancellationToken);
            var byBatch = Builders<DemandHistoryReviewAttempt>.IndexKeys
                .Ascending(x => x.TenantId).Ascending(x => x.IsDeleted)
                .Ascending(x => x.BatchId).Ascending(x => x.OccurredAt);
            await _attempts.Indexes.CreateOneAsync(
                new CreateIndexModel<DemandHistoryReviewAttempt>(byBatch,
                    new CreateIndexOptions { Name = "ix_mod0188_review_failures_by_batch" }),
                cancellationToken: cancellationToken);
            _indexReady = true;
        }
        finally { _indexGate.Release(); }
    }
}

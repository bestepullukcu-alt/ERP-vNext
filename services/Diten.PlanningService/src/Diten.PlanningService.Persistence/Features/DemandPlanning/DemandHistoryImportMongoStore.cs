using Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using MongoDB.Driver;

namespace Diten.PlanningService.Persistence.Features.DemandPlanning;

public sealed class DemandHistoryImportMongoStore : IHistoryImportBatchStore
{
    private readonly IMongoCollection<DemandHistoryImportBatch> _batches;
    private readonly SemaphoreSlim _indexGate = new(1, 1);
    private bool _indexReady;

    public DemandHistoryImportMongoStore(DemandPlanningMongoContext context) =>
        _batches = context.HistoryImportBatches;

    public async Task<HistoryImportInsertResult> InsertOrGetAsync(
        DemandHistoryImportBatch batch, CancellationToken cancellationToken)
    {
        await EnsureIndexAsync(cancellationToken);
        var filter = Builders<DemandHistoryImportBatch>.Filter.Eq(x => x.TenantId, batch.TenantId) &
                     Builders<DemandHistoryImportBatch>.Filter.Eq(x => x.IsDeleted, false) &
                     Builders<DemandHistoryImportBatch>.Filter.Eq(x => x.LegalEntityId, batch.LegalEntityId) &
                     Builders<DemandHistoryImportBatch>.Filter.Eq(x => x.UploadedByActorId, batch.UploadedByActorId) &
                     Builders<DemandHistoryImportBatch>.Filter.Eq(x => x.IdempotencyKey, batch.IdempotencyKey);
        try
        {
            await _batches.InsertOneAsync(batch, cancellationToken: cancellationToken);
            return new HistoryImportInsertResult(HistoryImportInsertOutcome.Created, batch);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            var existing = await _batches.Find(filter).FirstOrDefaultAsync(cancellationToken);
            if (existing is null || existing.RequestFingerprint != batch.RequestFingerprint)
                return new HistoryImportInsertResult(HistoryImportInsertOutcome.Conflict, null);
            return new HistoryImportInsertResult(HistoryImportInsertOutcome.Existing, existing);
        }
    }

    public async Task<DemandHistoryImportBatch?> GetOwnAsync(Guid tenantId, Guid actorId,
        Guid batchId, CancellationToken cancellationToken)
    {
        var filter = Builders<DemandHistoryImportBatch>.Filter.Eq(x => x.TenantId, tenantId) &
                     Builders<DemandHistoryImportBatch>.Filter.Eq(x => x.IsDeleted, false) &
                     Builders<DemandHistoryImportBatch>.Filter.Eq(x => x.UploadedByActorId, actorId) &
                     Builders<DemandHistoryImportBatch>.Filter.Eq(x => x.Id, batchId);
        return await _batches.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<DemandHistoryImportBatch?> GetForReviewAsync(Guid tenantId,
        Guid batchId, CancellationToken cancellationToken)
    {
        var filter = Builders<DemandHistoryImportBatch>.Filter.Eq(x => x.TenantId, tenantId) &
                     Builders<DemandHistoryImportBatch>.Filter.Eq(x => x.IsDeleted, false) &
                     Builders<DemandHistoryImportBatch>.Filter.Eq(x => x.Id, batchId);
        return await _batches.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<DemandHistoryImportBatch?> TryDecideAsync(DemandHistoryImportBatch expected,
        Guid reviewerId, ImportBatchReviewState decision, string reason,
        string requestKey, string fingerprint, DateTimeOffset decidedAt,
        CancellationToken cancellationToken)
    {
        var filter = Builders<DemandHistoryImportBatch>.Filter.Eq(x => x.TenantId, expected.TenantId) &
                     Builders<DemandHistoryImportBatch>.Filter.Eq(x => x.IsDeleted, false) &
                     Builders<DemandHistoryImportBatch>.Filter.Eq(x => x.LegalEntityId, expected.LegalEntityId) &
                     Builders<DemandHistoryImportBatch>.Filter.Eq(x => x.Id, expected.Id) &
                     Builders<DemandHistoryImportBatch>.Filter.Eq(x => x.Version, expected.Version) &
                     Builders<DemandHistoryImportBatch>.Filter.Eq(x => x.ReviewState, ImportBatchReviewState.Pending) &
                     Builders<DemandHistoryImportBatch>.Filter.Ne(x => x.UploadedByActorId, reviewerId);
        if (decision == ImportBatchReviewState.Approved)
            filter &= BuildApprovalEligibilityFilter();

        var action = new DemandHistoryImportAuditAction
        {
            Action = decision == ImportBatchReviewState.Approved ? "BatchApproved" : "BatchRejected",
            ActorId = reviewerId, OccurredAt = decidedAt,
            Detail = $"state={decision};version={expected.Version + 1}",
            RequestKey = requestKey, Reason = reason
        };
        var update = Builders<DemandHistoryImportBatch>.Update
            .Set(x => x.ReviewState, decision)
            .Set(x => x.ReviewedByActorId, reviewerId)
            .Set(x => x.ReviewedAt, decidedAt)
            .Set(x => x.ReviewReason, reason)
            .Set(x => x.ReviewRequestKey, requestKey)
            .Set(x => x.ReviewRequestFingerprint, fingerprint)
            .Set(x => x.UpdatedAt, decidedAt)
            .Inc(x => x.Version, 1)
            .Push(x => x.AuditTrail, action);
        return await _batches.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<DemandHistoryImportBatch>
            { ReturnDocument = ReturnDocument.After }, cancellationToken);
    }

    // Recheck every domain eligibility input against the current Mongo document.
    // Version alone is insufficient if legacy or external writes leave derived flags stale.
    public static FilterDefinition<DemandHistoryImportBatch> BuildApprovalEligibilityFilter()
    {
        var batch = Builders<DemandHistoryImportBatch>.Filter;
        var blockingIssue = Builders<ImportValidationIssue>.Filter.Eq(
            x => x.Severity, ImportIssueSeverity.Blocking);
        var rowWithBlockingIssue = Builders<DemandHistoryImportRow>.Filter.ElemMatch(
            x => x.Issues, blockingIssue);
        return batch.Eq(x => x.ApprovalBlocked, false) &
               batch.Ne(x => x.ValidationState, ImportBatchValidationState.Blocked) &
               batch.Not(batch.ElemMatch(x => x.FileIssues, blockingIssue)) &
               batch.Not(batch.ElemMatch(x => x.Rows, rowWithBlockingIssue)) &
               batch.Not(batch.ElemMatch(x => x.QuarantineCases, x => x.State == "Open"));
    }

    private async Task EnsureIndexAsync(CancellationToken cancellationToken)
    {
        if (_indexReady) return;
        await _indexGate.WaitAsync(cancellationToken);
        try
        {
            if (_indexReady) return;
            var keys = Builders<DemandHistoryImportBatch>.IndexKeys
                .Ascending(x => x.TenantId).Ascending(x => x.IsDeleted)
                .Ascending(x => x.LegalEntityId).Ascending(x => x.UploadedByActorId)
                .Ascending(x => x.IdempotencyKey);
            await _batches.Indexes.CreateOneAsync(
                new CreateIndexModel<DemandHistoryImportBatch>(keys,
                    new CreateIndexOptions { Name = "ux_mod0188_import_idempotency_scope", Unique = true }),
                cancellationToken: cancellationToken);
            _indexReady = true;
        }
        finally { _indexGate.Release(); }
    }
}


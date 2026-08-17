using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.PayrollIntegrationGovernance;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class PayrollIntegrationGovernanceRepository : TenantRepository<PayrollIntegrationRun>, IPayrollIntegrationGovernanceRepository
{
    private readonly IMongoCollection<PayrollIntegrationSourceLink> _sourceLinks;
    private readonly IMongoCollection<PayrollIntegrationMappingControl> _mappingControls;
    private readonly IMongoCollection<PayrollReconciliationControl> _reconciliationControls;
    private readonly IMongoCollection<PayrollIntegrationException> _exceptions;
    private readonly IMongoCollection<PayrollIntegrationRetryReplayRequest> _retryReplayRequests;
    private readonly IMongoCollection<PayrollIntegrationEvidenceExportReference> _evidenceExports;
    private readonly IMongoCollection<PayrollIntegrationHealthSnapshot> _healthSnapshots;

    public PayrollIntegrationGovernanceRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PayrollIntegrationGovernanceCollectionNames.Runs)
    {
        _sourceLinks = dbContext.GetCollection<PayrollIntegrationSourceLink>(PayrollIntegrationGovernanceCollectionNames.SourceLinks);
        _mappingControls = dbContext.GetCollection<PayrollIntegrationMappingControl>(PayrollIntegrationGovernanceCollectionNames.MappingControls);
        _reconciliationControls = dbContext.GetCollection<PayrollReconciliationControl>(PayrollIntegrationGovernanceCollectionNames.ReconciliationControls);
        _exceptions = dbContext.GetCollection<PayrollIntegrationException>(PayrollIntegrationGovernanceCollectionNames.Exceptions);
        _retryReplayRequests = dbContext.GetCollection<PayrollIntegrationRetryReplayRequest>(PayrollIntegrationGovernanceCollectionNames.RetryReplayRequests);
        _evidenceExports = dbContext.GetCollection<PayrollIntegrationEvidenceExportReference>(PayrollIntegrationGovernanceCollectionNames.EvidenceExportReferences);
        _healthSnapshots = dbContext.GetCollection<PayrollIntegrationHealthSnapshot>(PayrollIntegrationGovernanceCollectionNames.HealthSnapshots);
    }

    public Task<PayrollIntegrationRun> CreateRunAsync(PayrollIntegrationRun run, CancellationToken ct = default) => CreateAsync(run, ct);

    public Task<PayrollIntegrationRun?> GetRunByIdAsync(Guid id, CancellationToken ct = default) => GetByIdAsync(id, ct);

    public Task<IReadOnlyList<PayrollIntegrationRun>> GetRunsAsync(CancellationToken ct = default) => GetAllAsync(ct);

    public Task<bool> ExistsActiveRunCodeAsync(string runCode, Guid? excludeId = null, CancellationToken ct = default) =>
        ExistsRunAsync(Builders<PayrollIntegrationRun>.Filter.Eq(x => x.RunCode, runCode), excludeId, ct);

    public Task<bool> ExistsActiveRunIdempotencyKeyAsync(string idempotencyKey, Guid? excludeId = null, CancellationToken ct = default) =>
        ExistsRunAsync(Builders<PayrollIntegrationRun>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey), excludeId, ct);

    public async Task UpdateRunAsync(PayrollIntegrationRun run, CancellationToken ct = default)
    {
        run.UpdatedAt = DateTimeOffset.UtcNow;
        await Collection.ReplaceOneAsync(
            Builders<PayrollIntegrationRun>.Filter.And(ExecutionFilter, Builders<PayrollIntegrationRun>.Filter.Eq(x => x.Id, run.Id)),
            run,
            cancellationToken: ct);
    }

    public async Task<bool> ArchiveRunAsync(Guid id, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var result = await Collection.UpdateOneAsync(
            Builders<PayrollIntegrationRun>.Filter.And(ExecutionFilter, Builders<PayrollIntegrationRun>.Filter.Eq(x => x.Id, id)),
            Builders<PayrollIntegrationRun>.Update.Set(x => x.IsDeleted, true).Set(x => x.DeletedAt, now).Set(x => x.UpdatedAt, now),
            cancellationToken: ct);
        return result.ModifiedCount > 0;
    }

    public async Task<PayrollIntegrationSourceLink> CreateSourceLinkAsync(PayrollIntegrationSourceLink sourceLink, CancellationToken ct = default)
    {
        await _sourceLinks.InsertOneAsync(sourceLink, cancellationToken: ct);
        return sourceLink;
    }

    public async Task<IReadOnlyList<PayrollIntegrationSourceLink>> GetSourceLinksAsync(Guid runId, CancellationToken ct = default) =>
        await _sourceLinks.Find(TenantFilter<PayrollIntegrationSourceLink>(Builders<PayrollIntegrationSourceLink>.Filter.Eq(x => x.RunId, runId))).ToListAsync(ct);

    public async Task<PayrollIntegrationMappingControl?> GetMappingControlByIdAsync(Guid runId, Guid controlId, CancellationToken ct = default) =>
        await _mappingControls.Find(TenantFilter<PayrollIntegrationMappingControl>(
            Builders<PayrollIntegrationMappingControl>.Filter.Eq(x => x.RunId, runId),
            Builders<PayrollIntegrationMappingControl>.Filter.Eq(x => x.Id, controlId))).FirstOrDefaultAsync(ct);

    public async Task UpsertMappingControlAsync(PayrollIntegrationMappingControl control, CancellationToken ct = default)
    {
        control.UpdatedAt = DateTimeOffset.UtcNow;
        var result = await _mappingControls.ReplaceOneAsync(
            TenantFilter<PayrollIntegrationMappingControl>(Builders<PayrollIntegrationMappingControl>.Filter.Eq(x => x.Id, control.Id)),
            control,
            cancellationToken: ct);
        if (result.MatchedCount == 0)
        {
            await _mappingControls.InsertOneAsync(control, cancellationToken: ct);
        }
    }

    public async Task<IReadOnlyList<PayrollIntegrationMappingControl>> GetMappingControlsAsync(Guid runId, CancellationToken ct = default) =>
        await _mappingControls.Find(TenantFilter<PayrollIntegrationMappingControl>(Builders<PayrollIntegrationMappingControl>.Filter.Eq(x => x.RunId, runId))).ToListAsync(ct);

    public async Task<PayrollReconciliationControl> CreateReconciliationControlAsync(PayrollReconciliationControl control, CancellationToken ct = default)
    {
        await _reconciliationControls.InsertOneAsync(control, cancellationToken: ct);
        return control;
    }

    public async Task<IReadOnlyList<PayrollReconciliationControl>> GetReconciliationControlsAsync(Guid runId, CancellationToken ct = default) =>
        await _reconciliationControls.Find(TenantFilter<PayrollReconciliationControl>(Builders<PayrollReconciliationControl>.Filter.Eq(x => x.RunId, runId))).ToListAsync(ct);

    public async Task<PayrollIntegrationException> CreateExceptionAsync(PayrollIntegrationException exception, CancellationToken ct = default)
    {
        await _exceptions.InsertOneAsync(exception, cancellationToken: ct);
        return exception;
    }

    public async Task<PayrollIntegrationException?> GetExceptionByIdAsync(Guid runId, Guid exceptionId, CancellationToken ct = default) =>
        await _exceptions.Find(TenantFilter<PayrollIntegrationException>(
            Builders<PayrollIntegrationException>.Filter.Eq(x => x.RunId, runId),
            Builders<PayrollIntegrationException>.Filter.Eq(x => x.Id, exceptionId))).FirstOrDefaultAsync(ct);

    public async Task UpdateExceptionAsync(PayrollIntegrationException exception, CancellationToken ct = default)
    {
        exception.UpdatedAt = DateTimeOffset.UtcNow;
        await _exceptions.ReplaceOneAsync(
            TenantFilter<PayrollIntegrationException>(Builders<PayrollIntegrationException>.Filter.Eq(x => x.Id, exception.Id)),
            exception,
            cancellationToken: ct);
    }

    public async Task<IReadOnlyList<PayrollIntegrationException>> GetExceptionsAsync(Guid runId, CancellationToken ct = default) =>
        await _exceptions.Find(TenantFilter<PayrollIntegrationException>(Builders<PayrollIntegrationException>.Filter.Eq(x => x.RunId, runId))).ToListAsync(ct);

    public async Task<bool> ExistsActiveReplayIdempotencyKeyAsync(Guid runId, string idempotencyKey, CancellationToken ct = default) =>
        await _retryReplayRequests.Find(TenantFilter<PayrollIntegrationRetryReplayRequest>(
            Builders<PayrollIntegrationRetryReplayRequest>.Filter.Eq(x => x.RunId, runId),
            Builders<PayrollIntegrationRetryReplayRequest>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey))).AnyAsync(ct);

    public async Task<PayrollIntegrationRetryReplayRequest> CreateRetryReplayRequestAsync(PayrollIntegrationRetryReplayRequest request, CancellationToken ct = default)
    {
        await _retryReplayRequests.InsertOneAsync(request, cancellationToken: ct);
        return request;
    }

    public async Task<IReadOnlyList<PayrollIntegrationRetryReplayRequest>> GetRetryReplayRequestsAsync(Guid runId, CancellationToken ct = default) =>
        await _retryReplayRequests.Find(TenantFilter<PayrollIntegrationRetryReplayRequest>(Builders<PayrollIntegrationRetryReplayRequest>.Filter.Eq(x => x.RunId, runId))).ToListAsync(ct);

    public async Task<PayrollIntegrationEvidenceExportReference> CreateEvidenceExportReferenceAsync(PayrollIntegrationEvidenceExportReference reference, CancellationToken ct = default)
    {
        await _evidenceExports.InsertOneAsync(reference, cancellationToken: ct);
        return reference;
    }

    public async Task<IReadOnlyList<PayrollIntegrationEvidenceExportReference>> GetEvidenceExportReferencesAsync(Guid runId, CancellationToken ct = default) =>
        await _evidenceExports.Find(TenantFilter<PayrollIntegrationEvidenceExportReference>(Builders<PayrollIntegrationEvidenceExportReference>.Filter.Eq(x => x.RunId, runId))).ToListAsync(ct);

    public async Task<PayrollIntegrationHealthSnapshot> CreateHealthSnapshotAsync(PayrollIntegrationHealthSnapshot snapshot, CancellationToken ct = default)
    {
        await _healthSnapshots.InsertOneAsync(snapshot, cancellationToken: ct);
        return snapshot;
    }

    public async Task<PayrollIntegrationHealthSnapshot?> GetLatestHealthSnapshotAsync(Guid? runId, CancellationToken ct = default)
    {
        var filter = runId.HasValue
            ? TenantFilter<PayrollIntegrationHealthSnapshot>(Builders<PayrollIntegrationHealthSnapshot>.Filter.Eq(x => x.RunId, runId.Value))
            : TenantFilter<PayrollIntegrationHealthSnapshot>();
        return await _healthSnapshots.Find(filter).SortByDescending(x => x.CheckedAt).FirstOrDefaultAsync(ct);
    }

    private async Task<bool> ExistsRunAsync(FilterDefinition<PayrollIntegrationRun> filter, Guid? excludeId, CancellationToken ct)
    {
        var filters = new List<FilterDefinition<PayrollIntegrationRun>> { ExecutionFilter, filter };
        if (excludeId.HasValue)
        {
            filters.Add(Builders<PayrollIntegrationRun>.Filter.Ne(x => x.Id, excludeId.Value));
        }

        return await Collection.Find(Builders<PayrollIntegrationRun>.Filter.And(filters)).AnyAsync(ct);
    }

    private FilterDefinition<TEntity> TenantFilter<TEntity>(params FilterDefinition<TEntity>[] filters)
        where TEntity : TenantScopedEntity
    {
        var allFilters = new List<FilterDefinition<TEntity>>
        {
            Builders<TEntity>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<TEntity>.Filter.Eq(x => x.IsDeleted, false)
        };
        allFilters.AddRange(filters);
        return Builders<TEntity>.Filter.And(allFilters);
    }
}

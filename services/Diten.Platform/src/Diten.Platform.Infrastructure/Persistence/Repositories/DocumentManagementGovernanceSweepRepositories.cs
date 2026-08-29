using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

// MOD-0029-FU32 — tenant-scoped Mongo repository for governance sweep run history. Append-only: there is no delete,
// and the single ReplaceOne exists solely to close out a run that was opened at start. Only governance metadata
// (counters, subject ids, warnings) is persisted — no regulated document content ever reaches this collection.

public sealed class DocumentGovernanceSweepRunRepository
    : TenantRepository<DocumentGovernanceSweepRun>, IDocumentGovernanceSweepRunRepository
{
    public DocumentGovernanceSweepRunRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.DocumentManagementGovernanceSweepRuns) { }

    public new Task<DocumentGovernanceSweepRun> CreateAsync(DocumentGovernanceSweepRun run, CancellationToken ct = default) =>
        base.CreateAsync(run, ct);

    public async Task<DocumentGovernanceSweepRun?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentGovernanceSweepRun>.Filter.And(
                ExecutionFilter,
                Builders<DocumentGovernanceSweepRun>.Filter.Eq(x => x.Id, id)))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<DocumentGovernanceSweepRun>> GetAllForTenantAsync(CancellationToken ct = default) =>
        await Collection.Find(ExecutionFilter).SortByDescending(x => x.StartedAt).ToListAsync(ct);

    public async Task<DocumentGovernanceSweepRun?> GetLatestBySweepKeyAsync(string sweepKey, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentGovernanceSweepRun>.Filter.And(
                ExecutionFilter,
                Builders<DocumentGovernanceSweepRun>.Filter.Eq(x => x.SweepKey, sweepKey)))
            .SortByDescending(x => x.StartedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<bool> UpdateAsync(DocumentGovernanceSweepRun run, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            Builders<DocumentGovernanceSweepRun>.Filter.And(
                ExecutionFilter,
                Builders<DocumentGovernanceSweepRun>.Filter.Eq(x => x.Id, run.Id)),
            run, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}

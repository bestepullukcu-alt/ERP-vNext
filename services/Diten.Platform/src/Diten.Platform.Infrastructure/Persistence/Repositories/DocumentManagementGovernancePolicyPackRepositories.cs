using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

// MOD-0029-FU31A — tenant-scoped Mongo repository for governance policy pack application history. Append-only:
// there is no delete and no update. Only governance metadata (counts, policy keys, warnings) is persisted — no
// regulated document content ever reaches this collection.

public sealed class DocumentGovernancePolicyPackApplicationRepository
    : TenantRepository<DocumentGovernancePolicyPackApplication>, IDocumentGovernancePolicyPackApplicationRepository
{
    public DocumentGovernancePolicyPackApplicationRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.DocumentManagementGovernancePolicyPackApplications) { }

    public new Task<DocumentGovernancePolicyPackApplication> CreateAsync(
        DocumentGovernancePolicyPackApplication application, CancellationToken ct = default) =>
        base.CreateAsync(application, ct);

    public async Task<DocumentGovernancePolicyPackApplication?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentGovernancePolicyPackApplication>.Filter.And(
                ExecutionFilter,
                Builders<DocumentGovernancePolicyPackApplication>.Filter.Eq(x => x.Id, id)))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<DocumentGovernancePolicyPackApplication>> GetAllForTenantAsync(CancellationToken ct = default) =>
        await Collection.Find(ExecutionFilter).SortByDescending(x => x.AppliedAt).ToListAsync(ct);

    public async Task<DocumentGovernancePolicyPackApplication?> GetLatestByPackKeyAsync(string packKey, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentGovernancePolicyPackApplication>.Filter.And(
                ExecutionFilter,
                Builders<DocumentGovernancePolicyPackApplication>.Filter.Eq(x => x.PackKey, packKey)))
            .SortByDescending(x => x.AppliedAt)
            .FirstOrDefaultAsync(ct);
}

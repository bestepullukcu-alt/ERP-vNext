using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

// MOD-0029-FU08 — tenant-scoped Mongo repository for controlled document lifecycle transition records. No hard delete.

public sealed class DocumentLifecycleTransitionRecordRepository
    : TenantRepository<DocumentLifecycleTransitionRecord>, IDocumentLifecycleTransitionRecordRepository
{
    public DocumentLifecycleTransitionRecordRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.DocumentManagementLifecycleTransitions) { }

    public new Task<DocumentLifecycleTransitionRecord> CreateAsync(DocumentLifecycleTransitionRecord record, CancellationToken ct = default) =>
        base.CreateAsync(record, ct);

    public async Task<IReadOnlyList<DocumentLifecycleTransitionRecord>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentLifecycleTransitionRecord>.Filter.And(
                ExecutionFilter,
                Builders<DocumentLifecycleTransitionRecord>.Filter.Eq(x => x.RegisterEntryId, registerEntryId)))
            .SortByDescending(x => x.PerformedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentLifecycleTransitionRecord>> GetAllForTenantAsync(CancellationToken ct = default) =>
        await Collection.Find(ExecutionFilter).SortByDescending(x => x.PerformedAt).ToListAsync(ct);
}

using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

// MOD-0029-FU22 — tenant-scoped Mongo repositories for the quality event / deviation / CAPA bridge. No delete
// operation on any of them; only governance metadata and reference strings are persisted — never document bytes.

public sealed class DocumentQualityEventRepository
    : TenantRepository<DocumentQualityEvent>, IDocumentQualityEventRepository
{
    public DocumentQualityEventRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.DocumentManagementQualityEvents) { }

    public new Task<DocumentQualityEvent> CreateAsync(DocumentQualityEvent e, CancellationToken ct = default) =>
        base.CreateAsync(e, ct);

    public async Task<IReadOnlyList<DocumentQualityEvent>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentQualityEvent>.Filter.And(
                ExecutionFilter, Builders<DocumentQualityEvent>.Filter.Eq(x => x.RegisterEntryId, registerEntryId)))
            .SortByDescending(x => x.DetectedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentQualityEvent>> GetOpenAsync(CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentQualityEvent>.Filter.And(
                ExecutionFilter,
                Builders<DocumentQualityEvent>.Filter.Nin(x => x.EventStatus,
                    new[] { QualityEventStatus.Closed, QualityEventStatus.Cancelled })))
            .SortByDescending(x => x.DetectedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentQualityEvent>> GetAllForTenantAsync(CancellationToken ct = default) =>
        await Collection.Find(ExecutionFilter).SortByDescending(x => x.DetectedAt).ToListAsync(ct);

    public async Task<bool> UpdateAsync(DocumentQualityEvent e, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            Builders<DocumentQualityEvent>.Filter.And(ExecutionFilter,
                Builders<DocumentQualityEvent>.Filter.Eq(x => x.Id, e.Id)),
            e, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}

public sealed class DocumentDeviationRepository
    : TenantRepository<DocumentDeviation>, IDocumentDeviationRepository
{
    public DocumentDeviationRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.DocumentManagementQualityDeviations) { }

    public new Task<DocumentDeviation> CreateAsync(DocumentDeviation d, CancellationToken ct = default) =>
        base.CreateAsync(d, ct);

    public async Task<IReadOnlyList<DocumentDeviation>> GetByQualityEventAsync(Guid qualityEventId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentDeviation>.Filter.And(
                ExecutionFilter, Builders<DocumentDeviation>.Filter.Eq(x => x.QualityEventId, qualityEventId)))
            .SortBy(x => x.DetectedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentDeviation>> GetAllForTenantAsync(CancellationToken ct = default) =>
        await Collection.Find(ExecutionFilter).SortByDescending(x => x.DetectedAt).ToListAsync(ct);

    public async Task<bool> UpdateAsync(DocumentDeviation d, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            Builders<DocumentDeviation>.Filter.And(ExecutionFilter,
                Builders<DocumentDeviation>.Filter.Eq(x => x.Id, d.Id)),
            d, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}

public sealed class DocumentCAPAActionRepository
    : TenantRepository<DocumentCAPAAction>, IDocumentCAPAActionRepository
{
    public DocumentCAPAActionRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.DocumentManagementCapaActions) { }

    public new Task<DocumentCAPAAction> CreateAsync(DocumentCAPAAction a, CancellationToken ct = default) =>
        base.CreateAsync(a, ct);

    public async Task<IReadOnlyList<DocumentCAPAAction>> GetByQualityEventAsync(Guid qualityEventId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentCAPAAction>.Filter.And(
                ExecutionFilter, Builders<DocumentCAPAAction>.Filter.Eq(x => x.QualityEventId, qualityEventId)))
            .SortBy(x => x.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentCAPAAction>> GetByDeviationAsync(Guid deviationId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentCAPAAction>.Filter.And(
                ExecutionFilter, Builders<DocumentCAPAAction>.Filter.Eq(x => x.DeviationId, deviationId)))
            .SortBy(x => x.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentCAPAAction>> GetAllForTenantAsync(CancellationToken ct = default) =>
        await Collection.Find(ExecutionFilter).SortByDescending(x => x.CreatedAt).ToListAsync(ct);

    public async Task<bool> UpdateAsync(DocumentCAPAAction a, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            Builders<DocumentCAPAAction>.Filter.And(ExecutionFilter,
                Builders<DocumentCAPAAction>.Filter.Eq(x => x.Id, a.Id)),
            a, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}

public sealed class DocumentQualityEventSourceLinkRepository
    : TenantRepository<DocumentQualityEventSourceLink>, IDocumentQualityEventSourceLinkRepository
{
    public DocumentQualityEventSourceLinkRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.DocumentManagementQualityEventSourceLinks) { }

    public new Task<DocumentQualityEventSourceLink> CreateAsync(DocumentQualityEventSourceLink l, CancellationToken ct = default) =>
        base.CreateAsync(l, ct);

    public async Task<IReadOnlyList<DocumentQualityEventSourceLink>> GetByQualityEventAsync(Guid qualityEventId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentQualityEventSourceLink>.Filter.And(
                ExecutionFilter, Builders<DocumentQualityEventSourceLink>.Filter.Eq(x => x.QualityEventId, qualityEventId)))
            .SortBy(x => x.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentQualityEventSourceLink>> GetBySourceAsync(
        QualityEventSourceType sourceType, Guid sourceId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentQualityEventSourceLink>.Filter.And(
                ExecutionFilter,
                Builders<DocumentQualityEventSourceLink>.Filter.Eq(x => x.SourceType, sourceType),
                Builders<DocumentQualityEventSourceLink>.Filter.Eq(x => x.SourceId, sourceId)))
            .SortBy(x => x.CreatedAt).ToListAsync(ct);

    public async Task<bool> UpdateAsync(DocumentQualityEventSourceLink l, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            Builders<DocumentQualityEventSourceLink>.Filter.And(ExecutionFilter,
                Builders<DocumentQualityEventSourceLink>.Filter.Eq(x => x.Id, l.Id)),
            l, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}

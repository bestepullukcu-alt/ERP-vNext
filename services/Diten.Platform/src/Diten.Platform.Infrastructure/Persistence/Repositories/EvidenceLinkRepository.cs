using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.EvidenceLinking;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

/// <summary>
/// MOD-0031 slice 1 — tenant-scoped evidence-link persistence over the shared <see cref="TenantRepository{TEntity}"/>
/// (tenant + soft-delete execution filter). Writes join the active Platform transaction so the link and its outbox
/// intent commit atomically. No hard delete; removal is a status stamp.
/// </summary>
public sealed class EvidenceLinkRepository : TenantRepository<EvidenceLink>, IEvidenceLinkRepository
{
    private readonly IPlatformDbContext _dbContext;

    public EvidenceLinkRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.EvidenceLinks)
        => _dbContext = dbContext;

    private static FilterDefinitionBuilder<EvidenceLink> F => Builders<EvidenceLink>.Filter;

    public async Task<IReadOnlyList<EvidenceLink>> ListByObjectAsync(
        string module, string objectType, string objectId, string? objectVersion, bool includeRemoved,
        CancellationToken ct = default)
    {
        var conditions = new List<FilterDefinition<EvidenceLink>>
        {
            ExecutionFilter,
            F.Eq(x => x.ObjectRef.Module, module),
            F.Eq(x => x.ObjectRef.ObjectType, objectType),
            F.Eq(x => x.ObjectRef.ObjectId, objectId)
        };
        if (objectVersion is not null)
        {
            conditions.Add(F.Eq(x => x.ObjectRef.ObjectVersion, objectVersion));
        }

        if (!includeRemoved)
        {
            conditions.Add(F.Eq(x => x.Status, EvidenceLinkStatuses.Active));
        }

        return await Collection.Find(F.And(conditions)).SortByDescending(x => x.LinkedAt).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<EvidenceLink>> ListActiveByDocumentAsync(
        Guid documentId, Guid? documentVersionId, CancellationToken ct = default)
    {
        var filter = F.And(ExecutionFilter,
            F.Eq(x => x.DocumentId, documentId),
            F.Eq(x => x.Status, EvidenceLinkStatuses.Active));
        if (documentVersionId is { } versionId)
        {
            filter &= F.Eq(x => x.DocumentVersionId, versionId);
        }

        return await Collection.Find(filter).SortByDescending(x => x.LinkedAt).ToListAsync(ct);
    }

    public async Task<EvidenceLink?> FindActiveByDedupKeyAsync(string activeDedupKey, CancellationToken ct = default)
        => await Collection.Find(F.And(ExecutionFilter,
                F.Eq(x => x.ActiveDedupKey, activeDedupKey),
                F.Eq(x => x.Status, EvidenceLinkStatuses.Active)))
            .FirstOrDefaultAsync(ct);

    public async Task InsertAsync(IPlatformTransactionSession session, EvidenceLink link, CancellationToken ct = default)
    {
        if (link.TenantId != TenantContext.TenantId)
        {
            throw new InvalidOperationException("An evidence link can only be written for the resolved tenant.");
        }

        var handle = PlatformMongoTransactionSession.Require(session, _dbContext);
        try
        {
            await Collection.InsertOneAsync(handle, link, cancellationToken: ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new EvidenceLinkDuplicateException(ex);
        }
    }

    public async Task<bool> MarkRemovedAsync(
        IPlatformTransactionSession session, EvidenceLink link, CancellationToken ct = default)
    {
        var handle = PlatformMongoTransactionSession.Require(session, _dbContext);
        var update = Builders<EvidenceLink>.Update
            .Set(x => x.Status, EvidenceLinkStatuses.Removed)
            .Set(x => x.RemovedAt, link.RemovedAt)
            .Set(x => x.RemovedBy, link.RemovedBy)
            .Set(x => x.RemovedByUserId, link.RemovedByUserId)
            .Set(x => x.RemovalReason, link.RemovalReason)
            .Set(x => x.UpdatedAt, link.UpdatedAt)
            .Set(x => x.UpdatedBy, link.UpdatedBy)
            .Inc(x => x.Version, 1);
        var result = await Collection.UpdateOneAsync(handle,
            F.And(ExecutionFilter, F.Eq(x => x.Id, link.Id), F.Eq(x => x.Status, EvidenceLinkStatuses.Active)),
            update, cancellationToken: ct);
        return result.ModifiedCount == 1;
    }
}

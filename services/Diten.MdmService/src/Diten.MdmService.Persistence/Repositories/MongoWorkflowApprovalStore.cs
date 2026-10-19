using Diten.MdmService.Application.Features.Approvals;
using Diten.MdmService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

/// <summary>
/// WP-VW-W4 (W4-BE-b1) — the explicit-tenant, compare-and-swap store of an approvable MDM record (the applier and the
/// event consumer run without an HTTP request, so the tenant can never come from <c>ITenantContext</c>).
/// <list type="bullet">
/// <item><b>Load</b> filters on the GIVEN tenant + id + not deleted: another tenant's record is not found.</item>
/// <item><b>Replace</b> matches tenant + id + the EXPECTED version and writes version + 1; a lost race matches nothing and
/// leaves the in-memory version as it was (the caller reloads / answers 409).</item>
/// </list>
/// Added beside <see cref="RepositoryBase{TEntity}"/> on purpose: the HTTP-tenant base and its version-blind
/// <c>UpdateAsync</c> (Brand / Product / …) are NOT changed.
/// </summary>
public sealed class MongoWorkflowApprovalStore<T> : IWorkflowApprovalStore<T> where T : EntityBase, IWorkflowApprovable
{
    private readonly IMongoCollection<T> _collection;

    public MongoWorkflowApprovalStore(IMongoDatabase database, string collectionName)
        => _collection = database.GetCollection<T>(collectionName);

    public async Task<T?> GetAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        if (tenantId == Guid.Empty)
        {
            return null;
        }

        var filter = Builders<T>.Filter.And(
            Builders<T>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<T>.Filter.Eq(x => x.Id, id),
            Builders<T>.Filter.Eq(x => x.IsDeleted, false));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ReplaceAsync(T record, int expectedVersion, CancellationToken ct)
    {
        if (record.TenantId == Guid.Empty)
        {
            return false;
        }

        var filter = Builders<T>.Filter.And(
            Builders<T>.Filter.Eq(x => x.TenantId, record.TenantId),
            Builders<T>.Filter.Eq(x => x.Id, record.Id),
            Builders<T>.Filter.Eq(x => x.Version, expectedVersion));
        record.Version = expectedVersion + 1;
        var result = await _collection.ReplaceOneAsync(filter, record, cancellationToken: ct);
        if (result.MatchedCount == 1)
        {
            return true;
        }

        record.Version = expectedVersion;
        return false;
    }
}

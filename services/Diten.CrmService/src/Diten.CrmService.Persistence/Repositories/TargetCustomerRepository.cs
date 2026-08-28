using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// MOD-0167 FU02 TargetCustomer persistence — its OWN collection (<c>target_customers</c>), deliberately not embedded in
/// the segment: a static segment membership is unbounded (the 16MB document limit), rows need row-level concurrency,
/// and they are queried independently ("which segments has this person been added to by hand?").
/// <para>Tenant scoped, soft-delete aware, <b>no delete method</b>. Uniqueness over
/// (tenant, segment, subject type, subject id) among live rows is enforced in the handler rather than by a partial
/// index with a <c>$ne</c> filter, which crash-loops the service at startup.</para>
/// </summary>
public sealed class TargetCustomerRepository : ITargetCustomerRepository
{
    public const string CollectionName = "target_customers";

    private readonly IMongoCollection<TargetCustomer> _collection;

    public TargetCustomerRepository(IMongoDatabase database)
        => _collection = database.GetCollection<TargetCustomer>(CollectionName);

    private static FilterDefinition<TargetCustomer> Tenant(Guid tenantId)
        => Builders<TargetCustomer>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public async Task<TargetCustomer?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection.Find(Tenant(tenantId) & Builders<TargetCustomer>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<TargetCustomer>> ListBySegmentAsync(
        Guid tenantId, Guid segmentId, CancellationToken cancellationToken)
    {
        var rows = await _collection
            .Find(Tenant(tenantId) & Builders<TargetCustomer>.Filter.Eq(x => x.SegmentId, segmentId))
            .ToListAsync(cancellationToken);

        // Ordered in memory: EffectiveFrom / EffectiveTo are DateTimeOffset (BSON array) and are never sort keys.
        return rows.OrderBy(x => x.MembershipMode).ThenBy(x => x.SubjectId).ToList();
    }

    public async Task<IReadOnlyList<TargetCustomer>> ListBySubjectAsync(
        Guid tenantId, string subjectType, Guid subjectId, CancellationToken cancellationToken)
    {
        var rows = await _collection
            .Find(Tenant(tenantId)
                  & Builders<TargetCustomer>.Filter.Eq(x => x.SubjectType, subjectType)
                  & Builders<TargetCustomer>.Filter.Eq(x => x.SubjectId, subjectId))
            .ToListAsync(cancellationToken);
        return rows.OrderBy(x => x.SegmentId).ToList();
    }

    public async Task InsertAsync(TargetCustomer entity, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task<bool> ReplaceAsync(
        TargetCustomer entity, int expectedVersion, CancellationToken cancellationToken)
    {
        entity.Version = expectedVersion + 1;
        var result = await _collection.ReplaceOneAsync(
            Builders<TargetCustomer>.Filter.Where(
                x => x.Id == entity.Id && x.TenantId == entity.TenantId && x.Version == expectedVersion),
            entity, cancellationToken: cancellationToken);
        return result.IsAcknowledged && result.MatchedCount == 1;
    }
}

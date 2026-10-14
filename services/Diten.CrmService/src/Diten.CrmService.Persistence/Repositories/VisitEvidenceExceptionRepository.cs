using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// WP-VW-W3 (W3-BE-c) — the time-boxed evidence exceptions: tenant scoped, soft-delete aware, optimistic replace on the
/// technical version. StartDate / EndDate are DateOnly "yyyy-MM-dd" strings (sortable, no DateTimeOffset array trap).
/// </summary>
public sealed class VisitEvidenceExceptionRepository : IVisitEvidenceExceptionRepository
{
    public const string CollectionName = "visit_evidence_exceptions";

    private readonly IMongoCollection<VisitEvidenceException> _collection;

    public VisitEvidenceExceptionRepository(IMongoDatabase database)
        => _collection = database.GetCollection<VisitEvidenceException>(CollectionName);

    private static FilterDefinition<VisitEvidenceException> Tenant(Guid tenantId)
        => Builders<VisitEvidenceException>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public async Task<VisitEvidenceException?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection.Find(Tenant(tenantId) & Builders<VisitEvidenceException>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<VisitEvidenceException>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
        => Order(await _collection.Find(Tenant(tenantId)).ToListAsync(cancellationToken));

    public async Task<IReadOnlyList<VisitEvidenceException>> ListByResourceAsync(
        Guid tenantId, string resourceId, CancellationToken cancellationToken)
        => Order(await _collection.Find(Tenant(tenantId) & Builders<VisitEvidenceException>.Filter.Eq(x => x.ResourceId, resourceId))
            .ToListAsync(cancellationToken));

    public Task InsertAsync(VisitEvidenceException entity, CancellationToken cancellationToken)
        => _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task<bool> ReplaceAsync(VisitEvidenceException entity, int expectedVersion, CancellationToken cancellationToken)
    {
        entity.Version = expectedVersion + 1;
        var result = await _collection.ReplaceOneAsync(
            Builders<VisitEvidenceException>.Filter.Where(
                x => x.Id == entity.Id && x.TenantId == entity.TenantId && x.Version == expectedVersion),
            entity, cancellationToken: cancellationToken);
        if (result.IsAcknowledged && result.MatchedCount == 1)
        {
            return true;
        }

        entity.Version = expectedVersion;
        return false;
    }

    // Newest start first, then the newest record (in memory: CreatedAt is a DateTimeOffset BSON array).
    private static IReadOnlyList<VisitEvidenceException> Order(List<VisitEvidenceException> rows)
        => rows.OrderByDescending(x => x.StartDate).ThenByDescending(x => x.CreatedAt).ToList();
}

/// <summary>WP-VW-W3 (W3-BE-c) — the append-only evidence policy history. Ordered newest first in memory (At is a
/// DateTimeOffset BSON array — never a server-side sort key); a tenant's history is small (one row per changed field).</summary>
public sealed class VisitEvidencePolicyChangeRepository : IVisitEvidencePolicyChangeRepository
{
    public const string CollectionName = "visit_evidence_policy_changes";

    private readonly IMongoCollection<VisitEvidencePolicyChange> _collection;

    public VisitEvidencePolicyChangeRepository(IMongoDatabase database)
        => _collection = database.GetCollection<VisitEvidencePolicyChange>(CollectionName);

    public Task InsertManyAsync(IReadOnlyCollection<VisitEvidencePolicyChange> rows, CancellationToken cancellationToken)
        => rows.Count == 0 ? Task.CompletedTask : _collection.InsertManyAsync(rows, cancellationToken: cancellationToken);

    public async Task<(IReadOnlyList<VisitEvidencePolicyChange> Items, long Total)> ListAsync(
        Guid tenantId, int skip, int take, CancellationToken cancellationToken)
    {
        var rows = await _collection.Find(Builders<VisitEvidencePolicyChange>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted))
            .ToListAsync(cancellationToken);
        var ordered = rows.OrderByDescending(x => x.At).ThenByDescending(x => x.CreatedAt).ToList();
        return (ordered.Skip(skip).Take(take).ToList(), ordered.Count);
    }
}

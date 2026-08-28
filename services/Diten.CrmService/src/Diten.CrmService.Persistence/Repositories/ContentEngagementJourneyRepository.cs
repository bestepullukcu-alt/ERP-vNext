using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// MOD-0162 FU05 ContentEngagementJourney persistence — one collection (<c>content_engagement_journeys</c>); stages are
/// embedded (S2). Same rules as the FU02/FU03/FU04 knowledge repositories: tenant scoped, soft-delete aware, no delete
/// method (closing is the soft archive lifecycle). EffectiveFrom / EffectiveTo / ArchivedAt / StageSetFrozenAt
/// (DateTimeOffset → BSON array) are never sorted server-side nor used as index keys — two DateTimeOffset fields are
/// never indexed or sorted together (the CRM parallel-array trap); ordering happens in memory. Code uniqueness is
/// enforced in the handler (an archived code is reusable → no partial <c>$ne</c> filter, which crash-loops a partial
/// index). Every write is a single-document replace guarded by the optimistic <see cref="EntityBase.Version"/> token,
/// so no multi-document transaction is needed. The embedded Guid members take the string-Guid class-map convention
/// (see Persistence DI) so filters never silently return nothing (the AccountTerritoryAssignment lesson).
/// </summary>
public sealed class ContentEngagementJourneyRepository : IContentEngagementJourneyRepository
{
    public const string CollectionName = "content_engagement_journeys";

    private readonly IMongoCollection<ContentEngagementJourney> _collection;

    public ContentEngagementJourneyRepository(IMongoDatabase database)
        => _collection = database.GetCollection<ContentEngagementJourney>(CollectionName);

    private static FilterDefinition<ContentEngagementJourney> Tenant(Guid tenantId)
        => Builders<ContentEngagementJourney>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public async Task<ContentEngagementJourney?> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId) & Builders<ContentEngagementJourney>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ContentEngagementJourney>> ListAsync(
        Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await _collection.Find(Tenant(tenantId)).ToListAsync(cancellationToken);
        return rows
            .OrderBy(x => x.JourneyCode)
            .ThenBy(x => x.JourneyVersion)
            .ThenByDescending(x => x.CreatedAt)
            .ToList();
    }

    public async Task<IReadOnlyList<ContentEngagementJourney>> ListByCodeAsync(
        Guid tenantId, string journeyCode, CancellationToken cancellationToken)
    {
        var rows = await _collection
            .Find(Tenant(tenantId) & Builders<ContentEngagementJourney>.Filter.Eq(x => x.JourneyCode, journeyCode))
            .ToListAsync(cancellationToken);
        return rows.OrderBy(x => x.JourneyVersion).ThenByDescending(x => x.CreatedAt).ToList();
    }

    public async Task InsertAsync(ContentEngagementJourney entity, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task<bool> ReplaceAsync(
        ContentEngagementJourney entity, int expectedVersion, CancellationToken cancellationToken)
    {
        entity.Version = expectedVersion + 1;
        var result = await _collection.ReplaceOneAsync(
            Builders<ContentEngagementJourney>.Filter.Where(
                x => x.Id == entity.Id && x.TenantId == entity.TenantId && x.Version == expectedVersion),
            entity, cancellationToken: cancellationToken);
        return result.IsAcknowledged && result.MatchedCount == 1;
    }
}

using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// WP-SB-3b — <see cref="JourneyProgress"/> persistence: ONE collection (<c>journey_progress</c>), tenant scoped,
/// soft-delete aware, no delete. The key (TenantId, ContactId, ProductId, JourneyId) is unique in the database
/// (<see cref="KeyIndex"/>); the filters are public so a test can prove the tenant is part of every one of them.
/// Nothing sorts or indexes on a DateTimeOffset (the parallel-arrays trap).
/// </summary>
public sealed class JourneyProgressRepository : IJourneyProgressRepository
{
    public const string CollectionName = "journey_progress";

    private readonly IMongoCollection<JourneyProgress> _collection;

    public JourneyProgressRepository(IMongoDatabase database)
        => _collection = database.GetCollection<JourneyProgress>(CollectionName);

    public static FilterDefinition<JourneyProgress> ContactFilter(Guid tenantId, Guid contactId)
        => Builders<JourneyProgress>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted && x.ContactId == contactId);

    public static FilterDefinition<JourneyProgress> KeyFilter(Guid tenantId, Guid contactId, Guid productId, Guid journeyId)
        => ContactFilter(tenantId, contactId)
           & Builders<JourneyProgress>.Filter.Where(x => x.ProductId == productId && x.JourneyId == journeyId);

    /// <summary>The unique key index (live rows). The partial filter is an equality — never <c>$ne</c>, which
    /// crash-loops the service at startup.</summary>
    public static CreateIndexModel<JourneyProgress> KeyIndex()
        => new(
            Builders<JourneyProgress>.IndexKeys
                .Ascending(x => x.TenantId).Ascending(x => x.ContactId)
                .Ascending(x => x.ProductId).Ascending(x => x.JourneyId),
            new CreateIndexOptions<JourneyProgress>
            {
                Unique = true,
                Name = "ux_journey_progress_tenant_contact_product_journey",
                PartialFilterExpression = Builders<JourneyProgress>.Filter.Eq(x => x.IsDeleted, false)
            });

    public async Task<JourneyProgress?> GetByKeyAsync(
        Guid tenantId, Guid contactId, Guid productId, Guid journeyId, CancellationToken cancellationToken)
        => await _collection.Find(KeyFilter(tenantId, contactId, productId, journeyId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<JourneyProgress>> ListByContactAsync(
        Guid tenantId, Guid contactId, CancellationToken cancellationToken)
        => await _collection.Find(ContactFilter(tenantId, contactId)).ToListAsync(cancellationToken);

    public async Task<bool> UpsertAsync(JourneyProgress entity, int expectedVersion, CancellationToken cancellationToken)
    {
        entity.Version = expectedVersion + 1;
        if (expectedVersion == 0)
        {
            try
            {
                await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
                return true;
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                entity.Version = expectedVersion;
                return false;
            }
        }

        var result = await _collection.ReplaceOneAsync(
            Builders<JourneyProgress>.Filter.Where(
                x => x.Id == entity.Id && x.TenantId == entity.TenantId && x.Version == expectedVersion),
            entity, cancellationToken: cancellationToken);
        return result.IsAcknowledged && result.MatchedCount == 1;
    }
}

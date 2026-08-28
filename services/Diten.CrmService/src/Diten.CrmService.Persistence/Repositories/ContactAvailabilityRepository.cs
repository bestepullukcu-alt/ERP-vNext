using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// MOD-0150 FU07 availability master persistence. Soft-delete aware and tenant scoped. There is no delete method:
/// closing a row is a status update (inactive/archived), so history stays readable.
/// </summary>
public sealed class ContactAvailabilityRepository : IContactAvailabilityRepository
{
    private readonly IMongoCollection<ContactAvailability> _collection;

    public ContactAvailabilityRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<ContactAvailability>("contact_availabilities");
    }

    private static FilterDefinition<ContactAvailability> Tenant(Guid tenantId)
        => Builders<ContactAvailability>.Filter.Where(a => a.TenantId == tenantId && !a.IsDeleted);

    public async Task<ContactAvailability?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId) & Builders<ContactAvailability>.Filter.Eq(a => a.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ContactAvailability>> ListByLinkAsync(Guid tenantId, Guid linkId, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId) & Builders<ContactAvailability>.Filter.Eq(a => a.AccountContactLinkId, linkId))
            .SortBy(a => a.Weekday).ThenBy(a => a.StartTime)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ContactAvailability>> ListByContactAsync(Guid tenantId, Guid contactId, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId) & Builders<ContactAvailability>.Filter.Eq(a => a.ContactId, contactId))
            .SortBy(a => a.Weekday).ThenBy(a => a.StartTime)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ContactAvailability>> ListByAccountAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId) & Builders<ContactAvailability>.Filter.Eq(a => a.AccountId, accountId))
            .SortBy(a => a.Weekday).ThenBy(a => a.StartTime)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ContactAvailability>> ListByLinkIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> linkIds, CancellationToken cancellationToken)
    {
        if (linkIds is null || linkIds.Count == 0)
        {
            return [];
        }

        return await _collection
            .Find(Tenant(tenantId) & Builders<ContactAvailability>.Filter.In(a => a.AccountContactLinkId, linkIds))
            .ToListAsync(cancellationToken);
    }

    public async Task InsertAsync(ContactAvailability availability, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(availability, cancellationToken: cancellationToken);

    public async Task UpdateAsync(ContactAvailability availability, CancellationToken cancellationToken)
    {
        var filter = Builders<ContactAvailability>.Filter.Where(a => a.Id == availability.Id && a.TenantId == availability.TenantId);
        await _collection.ReplaceOneAsync(filter, availability, cancellationToken: cancellationToken);
    }
}

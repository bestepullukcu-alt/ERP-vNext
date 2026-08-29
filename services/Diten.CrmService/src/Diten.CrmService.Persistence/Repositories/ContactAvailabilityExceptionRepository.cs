using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// MOD-0150 FU07 date-specific availability exception persistence. Same rules as the availability master: tenant
/// scoped, soft-delete aware, no delete method (closing = status update).
/// </summary>
public sealed class ContactAvailabilityExceptionRepository : IContactAvailabilityExceptionRepository
{
    private readonly IMongoCollection<ContactAvailabilityException> _collection;

    public ContactAvailabilityExceptionRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<ContactAvailabilityException>("contact_availability_exceptions");
    }

    private static FilterDefinition<ContactAvailabilityException> Tenant(Guid tenantId)
        => Builders<ContactAvailabilityException>.Filter.Where(e => e.TenantId == tenantId && !e.IsDeleted);

    public async Task<ContactAvailabilityException?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId) & Builders<ContactAvailabilityException>.Filter.Eq(e => e.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ContactAvailabilityException>> ListByLinkAsync(Guid tenantId, Guid linkId, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId) & Builders<ContactAvailabilityException>.Filter.Eq(e => e.AccountContactLinkId, linkId))
            .SortBy(e => e.Date)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ContactAvailabilityException>> ListByContactAsync(Guid tenantId, Guid contactId, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId) & Builders<ContactAvailabilityException>.Filter.Eq(e => e.ContactId, contactId))
            .SortBy(e => e.Date)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ContactAvailabilityException>> ListByAccountAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId) & Builders<ContactAvailabilityException>.Filter.Eq(e => e.AccountId, accountId))
            .SortBy(e => e.Date)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ContactAvailabilityException>> ListByLinkIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> linkIds, CancellationToken cancellationToken)
    {
        if (linkIds is null || linkIds.Count == 0)
        {
            return [];
        }

        return await _collection
            .Find(Tenant(tenantId) & Builders<ContactAvailabilityException>.Filter.In(e => e.AccountContactLinkId, linkIds))
            .ToListAsync(cancellationToken);
    }

    public async Task InsertAsync(ContactAvailabilityException exception, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(exception, cancellationToken: cancellationToken);

    public async Task UpdateAsync(ContactAvailabilityException exception, CancellationToken cancellationToken)
    {
        var filter = Builders<ContactAvailabilityException>.Filter.Where(e => e.Id == exception.Id && e.TenantId == exception.TenantId);
        await _collection.ReplaceOneAsync(filter, exception, cancellationToken: cancellationToken);
    }
}

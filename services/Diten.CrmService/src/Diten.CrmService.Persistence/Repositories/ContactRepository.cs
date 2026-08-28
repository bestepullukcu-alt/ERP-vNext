using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

public sealed class ContactRepository : IContactRepository
{
    private readonly IMongoCollection<Contact> _collection;

    public ContactRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<Contact>("contacts");
    }

    private static FilterDefinition<Contact> ActiveTenant(Guid tenantId)
        => Builders<Contact>.Filter.Where(c => c.TenantId == tenantId && !c.IsDeleted);

    public async Task<Contact?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        var filter = ActiveTenant(tenantId) & Builders<Contact>.Filter.Eq(c => c.Id, id);
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Contact> Items, long Total)> ListAsync(
        Guid tenantId, string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var filter = ActiveTenant(tenantId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var regex = Builders<Contact>.Filter.Regex(c => c.DisplayName, new MongoDB.Bson.BsonRegularExpression(term, "i"))
                        | Builders<Contact>.Filter.Regex(c => c.FirstName, new MongoDB.Bson.BsonRegularExpression(term, "i"))
                        | Builders<Contact>.Filter.Regex(c => c.LastName, new MongoDB.Bson.BsonRegularExpression(term, "i"))
                        | Builders<Contact>.Filter.Regex(c => c.Email, new MongoDB.Bson.BsonRegularExpression(term, "i"));
            filter &= regex;
        }

        var total = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _collection.Find(filter)
            .SortBy(c => c.DisplayName)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<IReadOnlyList<Contact>> ListAllAsync(Guid tenantId, CancellationToken cancellationToken)
        => await _collection.Find(ActiveTenant(tenantId)).SortBy(c => c.DisplayName).ToListAsync(cancellationToken);

    public async Task InsertAsync(Contact contact, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(contact, cancellationToken: cancellationToken);

    public async Task UpdateAsync(Contact contact, CancellationToken cancellationToken)
    {
        var filter = Builders<Contact>.Filter.Where(c => c.Id == contact.Id && c.TenantId == contact.TenantId);
        await _collection.ReplaceOneAsync(filter, contact, cancellationToken: cancellationToken);
    }
}

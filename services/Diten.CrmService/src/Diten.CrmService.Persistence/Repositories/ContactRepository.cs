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

    public async Task<IReadOnlyList<Contact>> ListByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids is null || ids.Count == 0)
        {
            return Array.Empty<Contact>();
        }

        var filter = ActiveTenant(tenantId) & Builders<Contact>.Filter.In(c => c.Id, ids);
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Contact> Items, long Total, long UnfilteredTotal)> ListAsync(
        Guid tenantId, string? search, int page, int pageSize, string? sortBy, string? sortDir,
        IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? contactTypes, CancellationToken cancellationToken)
    {
        var tenantFilter = ActiveTenant(tenantId);
        var filter = tenantFilter;
        var hasSearch = !string.IsNullOrWhiteSpace(search);
        if (hasSearch)
        {
            var term = search!.Trim();
            var regex = Builders<Contact>.Filter.Regex(c => c.DisplayName, new MongoDB.Bson.BsonRegularExpression(term, "i"))
                        | Builders<Contact>.Filter.Regex(c => c.FirstName, new MongoDB.Bson.BsonRegularExpression(term, "i"))
                        | Builders<Contact>.Filter.Regex(c => c.LastName, new MongoDB.Bson.BsonRegularExpression(term, "i"))
                        | Builders<Contact>.Filter.Regex(c => c.Email, new MongoDB.Bson.BsonRegularExpression(term, "i"));
            filter &= regex;
        }

        // Inline-filter chips: Status / ContactType are plain stored fields, so these are cheap equality (IN)
        // predicates ANDed onto the tenant filter. The {TenantId, ...} prefix narrows first and equality does not
        // trigger MongoDB's 32MB in-memory sort, so no extra index is required. Multi-select ⇒ Filter.In.
        var hasStatusFilter = statuses is { Count: > 0 };
        var hasTypeFilter = contactTypes is { Count: > 0 };
        if (hasStatusFilter)
        {
            filter &= Builders<Contact>.Filter.In(c => c.Status, statuses!);
        }
        if (hasTypeFilter)
        {
            filter &= Builders<Contact>.Filter.In(c => c.ContactType, contactTypes!);
        }

        // recordsFiltered (respects search + chip filters) and recordsTotal (tenant-wide, ignores both). When nothing
        // narrows the set the two are identical, so avoid the extra count round-trip.
        var total = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var unfilteredTotal = (hasSearch || hasStatusFilter || hasTypeFilter)
            ? await _collection.CountDocumentsAsync(tenantFilter, cancellationToken: cancellationToken)
            : total;

        var items = await _collection.Find(filter)
            .Sort(BuildSort(sortBy, sortDir))
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total, unfilteredTotal);
    }

    // Only DisplayName/ContactType are allowed sort keys: each is the second field of a {TenantId, field} index, so
    // both ascending and descending are served as an index scan and never trigger MongoDB's 32MB in-memory sort on
    // the full tenant set. Any other (or missing) column falls back to DisplayName ascending.
    private static SortDefinition<Contact> BuildSort(string? sortBy, string? sortDir)
    {
        var descending = string.Equals(sortDir?.Trim(), "desc", StringComparison.OrdinalIgnoreCase);
        return (sortBy?.Trim().ToLowerInvariant()) switch
        {
            "contacttype" => descending
                ? Builders<Contact>.Sort.Descending(c => c.ContactType)
                : Builders<Contact>.Sort.Ascending(c => c.ContactType),
            "displayname" => descending
                ? Builders<Contact>.Sort.Descending(c => c.DisplayName)
                : Builders<Contact>.Sort.Ascending(c => c.DisplayName),
            _ => Builders<Contact>.Sort.Ascending(c => c.DisplayName)
        };
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

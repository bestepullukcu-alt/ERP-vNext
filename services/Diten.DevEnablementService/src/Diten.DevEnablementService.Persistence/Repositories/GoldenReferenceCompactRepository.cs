using Diten.DevEnablementService.Domain.Entities;
using Diten.DevEnablementService.Domain.Repositories;
using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.DevEnablementService.Persistence.Repositories;

public sealed class GoldenReferenceCompactRepository : IGoldenReferenceCompactRepository
{
    private readonly IMongoCollection<GoldenReferenceCompact> _collection;
    private readonly Guid _tenantId;

    public GoldenReferenceCompactRepository(IMongoDatabase database, Application.Common.ITenantContext tenantContext)
    {
        _collection = database.GetCollection<GoldenReferenceCompact>("golden_reference_compact");
        _tenantId = tenantContext.TenantId;
    }

    public async Task<IReadOnlyList<GoldenReferenceCompact>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _collection.Find(TenantFilter()).SortBy(x => x.Code).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The server-mode list query (WP-UI-LIST-SERVER-01, BL-440 package 3). Every count and every row is bounded by
    /// <see cref="TenantFilter"/> FIRST — search and filters only ever narrow the caller's own tenant. `Total` is the
    /// tenant's live list, `FilteredTotal` the same after search + filters; the rows are one page of the latter.
    /// The sort always ends on `Id` so two rows with the same sort value cannot trade places between two page
    /// requests (without it, a row can appear on page 1 AND page 2 while another appears on neither).
    /// </summary>
    public async Task<GoldenReferenceCompactListPage> QueryAsync(GoldenReferenceCompactListCriteria criteria, CancellationToken cancellationToken = default)
    {
        var f = Builders<GoldenReferenceCompact>.Filter;
        var tenant = TenantFilter();
        var clauses = new List<FilterDefinition<GoldenReferenceCompact>> { tenant };

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            // Escaped: the search box is text, never a pattern the caller controls.
            var pattern = new BsonRegularExpression(Regex.Escape(criteria.Search.Trim()), "i");
            clauses.Add(f.Or(f.Regex(x => x.Code, pattern), f.Regex(x => x.Name, pattern)));
        }

        if (criteria.IsActive is { Count: > 0 } active)
            clauses.Add(f.In(x => x.IsActive, active.Distinct()));
        if (criteria.ReferenceTypes is { Count: > 0 } types)
            clauses.Add(f.In(x => x.ReferenceType, types));
        if (criteria.Categories is { Count: > 0 } categories)
            clauses.Add(f.In(x => x.Category, categories));
        if (criteria.Owners is { Count: > 0 } owners)
            clauses.Add(f.In(x => x.Owner, owners));
        if (criteria.Priority is { } priority)
            clauses.Add(f.Eq(x => x.Priority, priority));

        var filtered = f.And(clauses);
        var total = await _collection.CountDocumentsAsync(tenant, cancellationToken: cancellationToken);
        var filteredTotal = clauses.Count == 1
            ? total
            : await _collection.CountDocumentsAsync(filtered, cancellationToken: cancellationToken);

        var find = _collection.Find(filtered).Sort(SortOf(criteria.SortField, criteria.Descending));
        if (criteria.Start > 0)
            find = find.Skip(criteria.Start);
        if (criteria.Length is { } length)
            find = find.Limit(length);

        var items = await find.ToListAsync(cancellationToken);
        return new GoldenReferenceCompactListPage(items, total, filteredTotal);
    }

    private static SortDefinition<GoldenReferenceCompact> SortOf(GoldenReferenceCompactSortField field, bool descending)
    {
        var s = Builders<GoldenReferenceCompact>.Sort;
        SortDefinition<GoldenReferenceCompact> primary = field switch
        {
            GoldenReferenceCompactSortField.Code => descending ? s.Descending(x => x.Code) : s.Ascending(x => x.Code),
            GoldenReferenceCompactSortField.Name => descending ? s.Descending(x => x.Name) : s.Ascending(x => x.Name),
            GoldenReferenceCompactSortField.ReferenceType => descending ? s.Descending(x => x.ReferenceType) : s.Ascending(x => x.ReferenceType),
            GoldenReferenceCompactSortField.Category => descending ? s.Descending(x => x.Category) : s.Ascending(x => x.Category),
            GoldenReferenceCompactSortField.Owner => descending ? s.Descending(x => x.Owner) : s.Ascending(x => x.Owner),
            GoldenReferenceCompactSortField.Version => descending ? s.Descending(x => x.Version) : s.Ascending(x => x.Version),
            GoldenReferenceCompactSortField.Priority => descending ? s.Descending(x => x.Priority) : s.Ascending(x => x.Priority),
            GoldenReferenceCompactSortField.IsActive => descending ? s.Descending(x => x.IsActive) : s.Ascending(x => x.IsActive),
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Not a sortable Golden Compact column.")
        };
        return s.Combine(primary, s.Ascending(x => x.Id));
    }

    public async Task<GoldenReferenceCompact?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<GoldenReferenceCompact>.Filter.And(
            TenantFilter(),
            Builders<GoldenReferenceCompact>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<GoldenReferenceCompact> CreateAsync(GoldenReferenceCompact entity, CancellationToken cancellationToken = default)
    {
        entity.TenantId = _tenantId;
        entity.CreatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.IsDeleted = false;
        await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
        return entity;
    }

    public async Task<bool> UpdateAsync(GoldenReferenceCompact entity, CancellationToken cancellationToken = default)
    {
        entity.TenantId = _tenantId;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<GoldenReferenceCompact>.Filter.And(
            TenantFilter(),
            Builders<GoldenReferenceCompact>.Filter.Eq(x => x.Id, entity.Id));
        var result = await _collection.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<GoldenReferenceCompact>.Filter.And(
            TenantFilter(),
            Builders<GoldenReferenceCompact>.Filter.Eq(x => x.Id, id));

        var update = Builders<GoldenReferenceCompact>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);

        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<int> BulkDeleteAsync(List<Guid> ids, CancellationToken cancellationToken = default)
    {
        var filter = Builders<GoldenReferenceCompact>.Filter.And(
            TenantFilter(),
            Builders<GoldenReferenceCompact>.Filter.In(x => x.Id, ids));

        var update = Builders<GoldenReferenceCompact>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);

        var result = await _collection.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
        return (int)result.ModifiedCount;
    }

    private FilterDefinition<GoldenReferenceCompact> TenantFilter()
    {
        return Builders<GoldenReferenceCompact>.Filter.And(
            Builders<GoldenReferenceCompact>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId),
            Builders<GoldenReferenceCompact>.Filter.Eq(x => x.IsDeleted, false));
    }

}

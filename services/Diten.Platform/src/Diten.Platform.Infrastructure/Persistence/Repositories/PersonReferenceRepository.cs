using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class PersonReferenceRepository : TenantRepository<PersonReference>, IPersonReferenceRepository
{
    public const string CollectionName = "person_references";

    public PersonReferenceRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, CollectionName)
    {
    }

    public async Task<IReadOnlyList<PersonReference>> SearchAsync(
        string? query,
        PersonReferenceStatus? status,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<PersonReference>> { ExecutionFilter };

        if (status.HasValue)
        {
            filters.Add(Builders<PersonReference>.Filter.Eq(x => x.Status, status.Value));
        }

        var normalizedQuery = query?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            var expression = new MongoDB.Bson.BsonRegularExpression(
                System.Text.RegularExpressions.Regex.Escape(normalizedQuery),
                "i");
            filters.Add(Builders<PersonReference>.Filter.Or(
                Builders<PersonReference>.Filter.Regex(x => x.DisplayName, expression),
                Builders<PersonReference>.Filter.Regex(x => x.ReferenceCode, expression)));
        }

        return await Collection
            .Find(Builders<PersonReference>.Filter.And(filters))
            .SortBy(x => x.DisplayName)
            .Skip(skip)
            .Limit(take)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PersonReference>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var filter = Builders<PersonReference>.Filter.And(
            ExecutionFilter,
            Builders<PersonReference>.Filter.In(x => x.Id, ids));

        return await Collection.Find(filter).ToListAsync(ct);
    }
}

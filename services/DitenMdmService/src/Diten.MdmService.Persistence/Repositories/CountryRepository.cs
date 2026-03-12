using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class CountryRepository : RepositoryBase<Country>, ICountryRepository
{
    public CountryRepository(IMongoDatabase database, ITenantContext tenantContext)
        : base(database, tenantContext, "countries")
    {
    }

    public Task<Country?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => FindByIdAsync(id, cancellationToken);

    public async Task<IEnumerable<Country>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var result = await FindAllAsync(cancellationToken);
        return result;
    }

    public Task<Country> CreateAsync(Country entity, CancellationToken cancellationToken = default)
        => InsertAsync(entity, cancellationToken);

    public async Task<bool> UpdateAsync(Country entity, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Country>.Filter.And(
            TenantFilter,
            Builders<Country>.Filter.Eq(e => e.Id, entity.Id));

        var result = await Collection.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Country>.Filter.And(
            TenantFilter,
            Builders<Country>.Filter.Eq(e => e.Id, id));

        var update = Builders<Country>.Update
            .Set(e => e.IsDeleted, true)
            .Set(e => e.DeletedAt, DateTimeOffset.UtcNow);

        await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task<bool> ExistsByIso2Async(string iso2, CancellationToken cancellationToken = default)
    {
        iso2 = (iso2 ?? string.Empty).Trim().ToUpperInvariant();

        var filter = Builders<Country>.Filter.And(
            Builders<Country>.Filter.Eq(e => e.TenantId, TenantContext.TenantId),
            Builders<Country>.Filter.Eq(e => e.Iso2Code, iso2),
            Builders<Country>.Filter.Eq(e => e.IsDeleted, false));

        return await Collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task<(int insertedCount, long modifiedCount)> UpsertSeedAsync(
        IEnumerable<Country> seedCountries,
        CancellationToken cancellationToken = default)
    {
        var models = new List<WriteModel<Country>>();
        var now = DateTimeOffset.UtcNow;

        foreach (var c in seedCountries)
        {
            var iso2 = (c.Iso2Code ?? string.Empty).Trim().ToUpperInvariant();
            var iso3 = (c.Iso3Code ?? string.Empty).Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(c.Name) || iso2.Length != 2 || iso3.Length != 3)
            {
                continue; // Skip invalid seed row
            }

            // Match by Tenant + Iso2 regardless of soft delete; we want to "revive" if needed.
            var filter = Builders<Country>.Filter.And(
                Builders<Country>.Filter.Eq(e => e.TenantId, TenantContext.TenantId),
                Builders<Country>.Filter.Eq(e => e.Iso2Code, iso2));

            var update = Builders<Country>.Update
                .Set(e => e.Name, c.Name.Trim())
                .Set(e => e.Iso2Code, iso2)
                .Set(e => e.Iso3Code, iso3)
                .Set(e => e.PhoneCode, c.PhoneCode)
                .Set(e => e.IsActive, true)
                .Set(e => e.IsDeleted, false)
                .Set(e => e.DeletedAt, null)
                .Set(e => e.UpdatedAt, now)
                .SetOnInsert(e => e.Id, Guid.NewGuid())
                .SetOnInsert(e => e.TenantId, TenantContext.TenantId)
                .SetOnInsert(e => e.CreatedAt, now);

            models.Add(new UpdateOneModel<Country>(filter, update) { IsUpsert = true });
        }

        if (models.Count == 0)
        {
            return (0, 0);
        }

        var result = await Collection.BulkWriteAsync(models, cancellationToken: cancellationToken);
        return (result.Upserts.Count, result.ModifiedCount);
    }
}


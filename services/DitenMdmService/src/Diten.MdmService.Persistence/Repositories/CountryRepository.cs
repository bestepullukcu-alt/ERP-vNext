using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public class CountryRepository : RepositoryBase<Country>, ICountryRepository
{
    public CountryRepository(IMongoDatabase database, ITenantContext tenantContext)
        : base(database, tenantContext, "countries")
    {
    }

    public async Task<Country> CreateAsync(Country entity, CancellationToken cancellationToken = default)
    {
        return await InsertAsync(entity, cancellationToken);
    }

    public async Task<Country?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await FindByIdAsync(id, cancellationToken);
    }

    public async Task<IEnumerable<Country>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await FindAllAsync(cancellationToken);
    }

    public async Task<bool> UpdateAsync(Country entity, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Country>.Filter.And(
            TenantFilter,
            Builders<Country>.Filter.Eq(e => e.Id, entity.Id));

        entity.UpdatedAt = DateTimeOffset.UtcNow;
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
        var filter = Builders<Country>.Filter.And(
            TenantFilter,
            Builders<Country>.Filter.Eq(e => e.Iso2Code, iso2));

        return await Collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task<(int insertedCount, long modifiedCount)> UpsertSeedAsync(
        IEnumerable<Country> seedCountries,
        CancellationToken cancellationToken = default)
    {
        int inserted = 0;
        long modified = 0;

        foreach (var country in seedCountries)
        {
            var filter = Builders<Country>.Filter.And(
                TenantFilter,
                Builders<Country>.Filter.Eq(e => e.Iso2Code, country.Iso2Code));

            var updateOptions = new ReplaceOptions { IsUpsert = true };
            country.TenantId = TenantContext.TenantId;

            var result = await Collection.ReplaceOneAsync(filter, country, updateOptions, cancellationToken);

            if (result.UpsertedId != null) inserted++;
            else modified += result.ModifiedCount;
        }

        return (inserted, modified);
    }
}

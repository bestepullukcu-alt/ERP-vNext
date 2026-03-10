using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

/// <summary>
/// Country repository implementation with TenantId and Soft-Delete enforcement.
/// </summary>
public sealed class CountryRepository : RepositoryBase<Country>, ICountryRepository
{
    public CountryRepository(IMongoDatabase database, ITenantContext tenantContext)
        : base(database, tenantContext, "countries")
    {
    }

    public Task<Country?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => FindByIdAsync(id, cancellationToken);

    public new Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        => base.ExistsAsync(id, cancellationToken);

    public async Task<IEnumerable<Country>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var result = await FindAllAsync(cancellationToken);
        return result;
    }

    public async Task<Country?> GetByIso2CodeAsync(string iso2Code, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Country>.Filter.And(
            TenantFilter,
            Builders<Country>.Filter.Eq(c => c.Iso2Code, iso2Code.ToUpperInvariant()));

        return await Collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Country?> GetByIso3CodeAsync(string iso3Code, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Country>.Filter.And(
            TenantFilter,
            Builders<Country>.Filter.Eq(c => c.Iso3Code, iso3Code.ToUpperInvariant()));

        return await Collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public Task<Country> CreateAsync(Country entity, CancellationToken cancellationToken = default)
        => InsertAsync(entity, cancellationToken);

    public async Task<bool> UpdateAsync(Country entity, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Country>.Filter.And(
            TenantFilter,
            Builders<Country>.Filter.Eq(c => c.Id, entity.Id));

        entity.UpdatedAt = DateTimeOffset.UtcNow;
        var result = await Collection.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Country>.Filter.And(
            TenantFilter,
            Builders<Country>.Filter.Eq(c => c.Id, id));

        var update = Builders<Country>.Update
            .Set(c => c.IsDeleted, true)
            .Set(c => c.DeletedAt, DateTimeOffset.UtcNow);

        await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }
}
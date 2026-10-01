using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.Repositories;
using Diten.AuthService.Application.Features.ServiceIdentityTokens;
using MongoDB.Driver;
using MongoDB.Bson;

namespace Diten.AuthService.Persistence.Repositories;

public sealed class ServiceClientTenantGrantRepository : IServiceClientTenantGrantRepository
{
    private readonly IMongoCollection<ServiceClientTenantGrant> _collection;

    public ServiceClientTenantGrantRepository(IMongoDatabase database) =>
        _collection = database.GetCollection<ServiceClientTenantGrant>("serviceClientTenantGrants");

    public async Task<bool> HasEnabledGrantAsync(Guid tenantId, Guid serviceClientIdentityId, string audience, CancellationToken ct)
    {
        var filter = Builders<ServiceClientTenantGrant>.Filter.And(
            Builders<ServiceClientTenantGrant>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<ServiceClientTenantGrant>.Filter.Eq(x => x.ServiceClientIdentityId, serviceClientIdentityId),
            Builders<ServiceClientTenantGrant>.Filter.Eq(x => x.Audience, audience),
            Builders<ServiceClientTenantGrant>.Filter.Eq(x => x.IsEnabled, true),
            Builders<ServiceClientTenantGrant>.Filter.Eq(x => x.IsDeleted, false));
        try
        {
            return await _collection.CountDocumentsAsync(filter, new CountOptions { Limit = 1 }, ct) == 1;
        }
        catch (MongoException ex)
        {
            throw new ServiceIdentityPersistenceUnavailableException(ex);
        }
        catch (BsonSerializationException ex)
        {
            throw new ServiceIdentityPersistenceUnavailableException(ex);
        }
        catch (FormatException ex)
        {
            throw new ServiceIdentityPersistenceUnavailableException(ex);
        }
    }
}

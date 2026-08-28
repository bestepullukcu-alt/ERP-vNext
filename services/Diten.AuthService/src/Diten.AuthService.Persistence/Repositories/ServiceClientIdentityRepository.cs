using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.Repositories;
using Diten.AuthService.Application.Features.ServiceIdentityTokens;
using MongoDB.Driver;
using MongoDB.Bson;

namespace Diten.AuthService.Persistence.Repositories;

public sealed class ServiceClientIdentityRepository : IServiceClientIdentityRepository
{
    private readonly IMongoCollection<ServiceClientIdentity> _collection;

    public ServiceClientIdentityRepository(IMongoDatabase database) =>
        _collection = database.GetCollection<ServiceClientIdentity>("serviceClientIdentities");

    public async Task<ServiceClientIdentity?> GetByClientCodeAsync(string clientCode, CancellationToken ct)
    {
        try
        {
            return await _collection.Find(Builders<ServiceClientIdentity>.Filter.And(
                Builders<ServiceClientIdentity>.Filter.Eq(x => x.ClientCode, clientCode),
                Builders<ServiceClientIdentity>.Filter.Eq(x => x.IsDeleted, false)))
                .FirstOrDefaultAsync(ct);
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

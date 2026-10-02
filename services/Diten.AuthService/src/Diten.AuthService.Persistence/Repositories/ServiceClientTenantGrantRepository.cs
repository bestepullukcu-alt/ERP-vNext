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

    public Task<ServiceClientTenantGrant?> FindIncludingDeletedAsync(
        Guid tenantId, Guid serviceClientIdentityId, string audience, CancellationToken ct) =>
        ExecuteAsync(async () => (ServiceClientTenantGrant?)await _collection
            .Find(Builders<ServiceClientTenantGrant>.Filter.And(
                Builders<ServiceClientTenantGrant>.Filter.Eq(x => x.TenantId, tenantId),
                Builders<ServiceClientTenantGrant>.Filter.Eq(x => x.ServiceClientIdentityId, serviceClientIdentityId),
                Builders<ServiceClientTenantGrant>.Filter.Eq(x => x.Audience, audience)))
            .SortBy(x => x.IsDeleted)
            .FirstOrDefaultAsync(ct));

    // MOD-0033-FU02 §F — insert only; an existing grant (enabled or disabled) is never touched from here.
    public Task<bool> CreateIfAbsentAsync(ServiceClientTenantGrant grant, CancellationToken ct) => ExecuteAsync(async () =>
    {
        ArgumentNullException.ThrowIfNull(grant);
        try
        {
            await _collection.InsertOneAsync(grant, cancellationToken: ct);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    });

    private static async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try { return await action(); }
        catch (Exception ex) when (ex is MongoException or BsonSerializationException or FormatException)
        {
            throw new ServiceIdentityPersistenceUnavailableException(ex);
        }
    }
}

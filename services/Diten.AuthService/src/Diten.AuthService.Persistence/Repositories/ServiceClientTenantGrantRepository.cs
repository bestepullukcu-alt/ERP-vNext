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

    public async Task<ServiceClientTenantGrant?> GetAsync(
        Guid tenantId, Guid serviceClientIdentityId, string audience, CancellationToken ct)
    {
        try
        {
            return await _collection.Find(ExactFilter(tenantId, serviceClientIdentityId, audience))
                .FirstOrDefaultAsync(ct);
        }
        catch (Exception ex) when (ex is MongoException or BsonSerializationException or FormatException)
        {
            throw new ServiceIdentityPersistenceUnavailableException(ex);
        }
    }

    public async Task<OperationalMutationResult<ServiceClientTenantGrant>> SetEnabledOperationalAsync(
        ServiceClientTenantGrant grant, bool enabled, long expectedVersion, Guid commandId,
        string fingerprint, DateTimeOffset nowUtc, string actorId, CancellationToken ct)
    {
        try
        {
            var filter = Builders<ServiceClientTenantGrant>.Filter.And(
                ExactFilter(grant.TenantId, grant.ServiceClientIdentityId, grant.Audience),
                Builders<ServiceClientTenantGrant>.Filter.Eq(x => x.OperationalVersion, expectedVersion));
            var update = Builders<ServiceClientTenantGrant>.Update
                .SetOnInsert(x => x.Id, grant.Id)
                .SetOnInsert(x => x.TenantId, grant.TenantId)
                .SetOnInsert(x => x.ServiceClientIdentityId, grant.ServiceClientIdentityId)
                .SetOnInsert(x => x.Audience, grant.Audience)
                .SetOnInsert(x => x.CreatedAt, grant.CreatedAt)
                .SetOnInsert(x => x.CreatedBy, grant.CreatedBy)
                .SetOnInsert(x => x.IsDeleted, false)
                .Set(x => x.IsEnabled, enabled)
                .Set(x => x.LastOperationalCommandId, commandId)
                .Set(x => x.LastOperationalCommandFingerprint, fingerprint)
                .Set(x => x.UpdatedAt, nowUtc)
                .Set(x => x.UpdatedBy, actorId)
                .Inc(x => x.OperationalVersion, 1);
            var updated = await _collection.FindOneAndUpdateAsync(filter, update,
                new FindOneAndUpdateOptions<ServiceClientTenantGrant>
                {
                    IsUpsert = enabled && expectedVersion == 0,
                    ReturnDocument = ReturnDocument.After
                }, ct);
            if (updated is not null) return new(OperationalMutationStatus.Applied, updated);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Resolve below through an exact tuple re-read.
        }
        catch (MongoCommandException ex) when (ex.Code == 11000)
        {
            // Resolve below through an exact tuple re-read.
        }
        catch (MongoException ex)
        {
            throw new ServiceIdentityPersistenceUnavailableException(ex);
        }

        var current = await GetAsync(grant.TenantId, grant.ServiceClientIdentityId, grant.Audience, ct);
        if (current is null) return new(OperationalMutationStatus.NotFound, null);
        return current.LastOperationalCommandId == commandId
            && string.Equals(current.LastOperationalCommandFingerprint, fingerprint, StringComparison.Ordinal)
                ? new(OperationalMutationStatus.Replayed, current)
                : new(OperationalMutationStatus.Conflict, current);
    }

    private static FilterDefinition<ServiceClientTenantGrant> ExactFilter(
        Guid tenantId, Guid serviceClientIdentityId, string audience) =>
        Builders<ServiceClientTenantGrant>.Filter.And(
            Builders<ServiceClientTenantGrant>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<ServiceClientTenantGrant>.Filter.Eq(x => x.ServiceClientIdentityId, serviceClientIdentityId),
            Builders<ServiceClientTenantGrant>.Filter.Eq(x => x.Audience, audience),
            Builders<ServiceClientTenantGrant>.Filter.Eq(x => x.IsDeleted, false));
}

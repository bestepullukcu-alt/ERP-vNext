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

    public async Task<ServiceClientIdentity?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await ExecuteReadAsync(
            Builders<ServiceClientIdentity>.Filter.And(
                Builders<ServiceClientIdentity>.Filter.Eq(x => x.Id, id),
                Builders<ServiceClientIdentity>.Filter.Eq(x => x.IsDeleted, false)), ct);

    public async Task<OperationalMutationResult<ServiceClientIdentity>> CreateOperationalAsync(
        ServiceClientIdentity identity, Guid commandId, string fingerprint, CancellationToken ct)
    {
        try
        {
            await _collection.InsertOneAsync(identity, cancellationToken: ct);
            return new(OperationalMutationStatus.Applied, identity);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var current = await GetByClientCodeAsync(identity.ClientCode, ct);
            return IsReplay(current, commandId, fingerprint)
                ? new(OperationalMutationStatus.Replayed, current)
                : new(OperationalMutationStatus.Conflict, current);
        }
        catch (MongoException ex)
        {
            throw new ServiceIdentityPersistenceUnavailableException(ex);
        }
    }

    public async Task<OperationalMutationResult<ServiceClientIdentity>> RotateCredentialOperationalAsync(
        Guid id, long expectedVersion, Guid commandId, string fingerprint,
        string previousCredentialHash, string previousCredentialVersion,
        string credentialHash, string credentialVersion, DateTimeOffset previousValidUntilUtc,
        DateTimeOffset nowUtc, string actorId, CancellationToken ct)
    {
        var filter = VersionedFilter(id, expectedVersion);
        var update = Builders<ServiceClientIdentity>.Update
            .Set(x => x.PreviousCredentialHash, previousCredentialHash)
            .Set(x => x.PreviousCredentialVersion, previousCredentialVersion)
            .Set(x => x.PreviousValidUntilUtc, previousValidUntilUtc)
            .Set(x => x.ActiveCredentialHash, credentialHash)
            .Set(x => x.ActiveCredentialVersion, credentialVersion)
            .Set(x => x.LastOperationalCommandId, commandId)
            .Set(x => x.LastOperationalCommandFingerprint, fingerprint)
            .Set(x => x.UpdatedAt, nowUtc)
            .Set(x => x.UpdatedBy, actorId)
            .Inc(x => x.OperationalVersion, 1);
        return await MutateAsync(id, commandId, fingerprint, filter, update, ct);
    }

    public async Task<OperationalMutationResult<ServiceClientIdentity>> RevokeOperationalAsync(
        Guid id, long expectedVersion, Guid commandId, string fingerprint,
        DateTimeOffset nowUtc, string actorId, CancellationToken ct)
    {
        var filter = VersionedFilter(id, expectedVersion);
        var update = Builders<ServiceClientIdentity>.Update
            .Set(x => x.IsRevoked, true)
            .Set(x => x.PreviousCredentialHash, null)
            .Set(x => x.PreviousCredentialVersion, null)
            .Set(x => x.PreviousValidUntilUtc, null)
            .Set(x => x.LastOperationalCommandId, commandId)
            .Set(x => x.LastOperationalCommandFingerprint, fingerprint)
            .Set(x => x.UpdatedAt, nowUtc)
            .Set(x => x.UpdatedBy, actorId)
            .Inc(x => x.OperationalVersion, 1);
        return await MutateAsync(id, commandId, fingerprint, filter, update, ct);
    }

    private FilterDefinition<ServiceClientIdentity> VersionedFilter(Guid id, long expectedVersion) =>
        Builders<ServiceClientIdentity>.Filter.And(
            Builders<ServiceClientIdentity>.Filter.Eq(x => x.Id, id),
            Builders<ServiceClientIdentity>.Filter.Eq(x => x.OperationalVersion, expectedVersion),
            Builders<ServiceClientIdentity>.Filter.Eq(x => x.IsDeleted, false));

    private async Task<OperationalMutationResult<ServiceClientIdentity>> MutateAsync(
        Guid id, Guid commandId, string fingerprint, FilterDefinition<ServiceClientIdentity> filter,
        UpdateDefinition<ServiceClientIdentity> update, CancellationToken ct)
    {
        try
        {
            var updated = await _collection.FindOneAndUpdateAsync(
                filter, update, new FindOneAndUpdateOptions<ServiceClientIdentity>
                {
                    ReturnDocument = ReturnDocument.After
                }, ct);
            if (updated is not null) return new(OperationalMutationStatus.Applied, updated);
            var current = await GetByIdAsync(id, ct);
            if (current is null) return new(OperationalMutationStatus.NotFound, null);
            return IsReplay(current, commandId, fingerprint)
                ? new(OperationalMutationStatus.Replayed, current)
                : new(OperationalMutationStatus.Conflict, current);
        }
        catch (MongoException ex)
        {
            throw new ServiceIdentityPersistenceUnavailableException(ex);
        }
    }

    private async Task<ServiceClientIdentity?> ExecuteReadAsync(
        FilterDefinition<ServiceClientIdentity> filter, CancellationToken ct)
    {
        try
        {
            return await _collection.Find(filter).FirstOrDefaultAsync(ct);
        }
        catch (Exception ex) when (ex is MongoException or BsonSerializationException or FormatException)
        {
            throw new ServiceIdentityPersistenceUnavailableException(ex);
        }
    }

    private static bool IsReplay(ServiceClientIdentity? identity, Guid commandId, string fingerprint) =>
        identity is not null
        && identity.LastOperationalCommandId == commandId
        && string.Equals(identity.LastOperationalCommandFingerprint, fingerprint, StringComparison.Ordinal);
}

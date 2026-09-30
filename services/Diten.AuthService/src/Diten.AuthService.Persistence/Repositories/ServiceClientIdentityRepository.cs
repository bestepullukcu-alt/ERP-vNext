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

    public Task<ServiceClientIdentity?> GetByIdAsync(Guid id, CancellationToken ct) => ExecuteAsync(async () =>
        await _collection.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync(ct));

    public Task<OperationalMutationResult<ServiceClientIdentity>> RotateCredentialOperationalAsync(
        ServiceClientCredentialRotation mutation, CancellationToken ct) => ExecuteAsync(async () =>
    {
        ArgumentNullException.ThrowIfNull(mutation);
        if (mutation.ExpectedOperationalVersion < 0 || mutation.ExpectedOperationalVersion == long.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(mutation));

        var f = Builders<ServiceClientIdentity>.Filter;
        var version = mutation.ExpectedOperationalVersion == 0
            ? f.Or(f.Exists(x => x.OperationalVersion, false), f.Eq(x => x.OperationalVersion, 0))
            : f.Eq(x => x.OperationalVersion, mutation.ExpectedOperationalVersion);
        var filter = f.And(
            f.Eq(x => x.Id, mutation.Id), f.Eq(x => x.ClientCode, mutation.ClientCode),
            f.Eq(x => x.ServiceName, mutation.ServiceName),
            f.Eq(x => x.AllowedAudience, mutation.PersistedAllowedAudience),
            f.Eq(x => x.IsDeleted, false), f.Eq(x => x.IsRevoked, false), version,
            f.Eq(x => x.ActiveCredentialHash, mutation.ExpectedActiveCredentialHash),
            f.Eq(x => x.ActiveCredentialVersion, mutation.ExpectedActiveCredentialVersion));
        var update = Builders<ServiceClientIdentity>.Update
            .Set(x => x.PreviousCredentialHash, mutation.ExpectedActiveCredentialHash)
            .Set(x => x.PreviousCredentialVersion, mutation.ExpectedActiveCredentialVersion)
            .Set(x => x.PreviousValidUntilUtc, mutation.PreviousValidUntilUtc)
            .Set(x => x.ActiveCredentialHash, mutation.CredentialHash)
            .Set(x => x.ActiveCredentialVersion, mutation.CredentialVersion)
            .Set(x => x.LastOperationalCommandId, mutation.CommandId)
            .Set(x => x.LastOperationalCommandFingerprint, mutation.CommandFingerprint)
            .Set(x => x.UpdatedAt, mutation.NowUtc).Set(x => x.UpdatedBy, mutation.ActorId)
            .Inc(x => x.OperationalVersion, 1);
        // No upsert and no retry. A lost acknowledgement remains ambiguous to the caller.
        var updated = await _collection.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<ServiceClientIdentity>
            {
                IsUpsert = false, ReturnDocument = ReturnDocument.After, Collation = Collation.Simple
            }, ct);
        if (updated is not null)
            return new OperationalMutationResult<ServiceClientIdentity>(OperationalMutationStatus.Applied, updated);
        var current = await GetByIdAsync(mutation.Id, ct);
        return new OperationalMutationResult<ServiceClientIdentity>(
            current is null ? OperationalMutationStatus.NotFound : OperationalMutationStatus.Conflict, current);
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

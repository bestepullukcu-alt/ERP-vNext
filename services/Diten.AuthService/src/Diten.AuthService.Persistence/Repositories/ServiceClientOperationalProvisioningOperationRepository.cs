using Diten.AuthService.Application.Features.ServiceIdentityTokens;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Repositories;

public sealed class ServiceClientOperationalProvisioningOperationRepository
    : IServiceClientOperationalProvisioningOperationRepository
{
    private readonly IMongoCollection<ServiceClientOperationalProvisioningOperation> _collection;

    public ServiceClientOperationalProvisioningOperationRepository(IMongoDatabase database) =>
        _collection = database.GetCollection<ServiceClientOperationalProvisioningOperation>(
            "serviceClientOperationalProvisioningOperations");

    public Task<ServiceClientOperationalProvisioningOperation?> GetByCommandIdAsync(
        Guid commandId, CancellationToken ct) => ExecuteAsync<ServiceClientOperationalProvisioningOperation?>(async () =>
            await _collection.Find(Builders<ServiceClientOperationalProvisioningOperation>.Filter.And(
                    Builders<ServiceClientOperationalProvisioningOperation>.Filter.Eq(x => x.CommandId, commandId),
                    Builders<ServiceClientOperationalProvisioningOperation>.Filter.Eq(x => x.IsDeleted, false)))
                .FirstOrDefaultAsync(ct));

    public async Task<bool> TryReserveAsync(
        ServiceClientOperationalProvisioningOperation operation, CancellationToken ct)
    {
        try
        {
            await _collection.InsertOneAsync(operation, cancellationToken: ct);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
        catch (MongoException ex)
        {
            throw new ServiceIdentityPersistenceUnavailableException(ex);
        }
    }

    public Task<bool> TryAdvanceAsync(
        Guid commandId, string fingerprint,
        ServiceClientOperationalProvisioningCheckpoint expectedCheckpoint,
        ServiceClientOperationalProvisioningCheckpoint nextCheckpoint,
        ServiceClientOperationalProvisioningState nextState,
        ServiceClientOperationalProvisioningEvidence evidence,
        CancellationToken ct) => ExecuteAsync(async () =>
    {
        var filter = Builders<ServiceClientOperationalProvisioningOperation>.Filter.And(
            Builders<ServiceClientOperationalProvisioningOperation>.Filter.Eq(x => x.CommandId, commandId),
            Builders<ServiceClientOperationalProvisioningOperation>.Filter.Eq(x => x.CommandFingerprint, fingerprint),
            Builders<ServiceClientOperationalProvisioningOperation>.Filter.Eq(x => x.Checkpoint, expectedCheckpoint),
            Builders<ServiceClientOperationalProvisioningOperation>.Filter.Eq(x => x.IsDeleted, false));
        var update = Builders<ServiceClientOperationalProvisioningOperation>.Update
            .Set(x => x.Checkpoint, nextCheckpoint)
            .Set(x => x.State, nextState)
            .Set(x => x.UpdatedAt, evidence.OccurredAtUtc)
            .Inc(x => x.EvidenceSequence, 1)
            .Push(x => x.Evidence, evidence);
        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        return result.ModifiedCount == 1;
    });

    public Task<bool> TryMarkRecoveryRequiredAsync(
        Guid commandId, string fingerprint,
        ServiceClientOperationalProvisioningCheckpoint checkpoint,
        CancellationToken ct) => ExecuteAsync(async () =>
    {
        var filter = Builders<ServiceClientOperationalProvisioningOperation>.Filter.And(
            Builders<ServiceClientOperationalProvisioningOperation>.Filter.Eq(x => x.CommandId, commandId),
            Builders<ServiceClientOperationalProvisioningOperation>.Filter.Eq(x => x.CommandFingerprint, fingerprint),
            Builders<ServiceClientOperationalProvisioningOperation>.Filter.Eq(x => x.Checkpoint, checkpoint),
            Builders<ServiceClientOperationalProvisioningOperation>.Filter.Ne(
                x => x.State, ServiceClientOperationalProvisioningState.Completed));
        var result = await _collection.UpdateOneAsync(filter,
            Builders<ServiceClientOperationalProvisioningOperation>.Update
                .Set(x => x.State, ServiceClientOperationalProvisioningState.RecoveryRequired),
            cancellationToken: ct);
        return result.ModifiedCount == 1;
    });

    private static async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (ex is MongoException or BsonSerializationException or FormatException)
        {
            throw new ServiceIdentityPersistenceUnavailableException(ex);
        }
    }
}

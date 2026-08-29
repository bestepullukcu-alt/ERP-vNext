using Diten.AuthService.Domain.S2S;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.S2S;

public interface IS2SMongoContext
{
    string DatabaseName { get; }
    IMongoCollection<ServicePrincipal> ServicePrincipals { get; }
    IMongoCollection<ServiceCredentialDescriptor> ServiceCredentialDescriptors { get; }
    IMongoCollection<S2SReplayReceipt> ReplayReceipts { get; }
    IMongoCollection<BsonDocument> GetAllowlistedRawCollection(string collectionName);
    Task<IClientSessionHandle> StartSessionAsync(CancellationToken cancellationToken);
    Task EnsureCompatibleAsync(CancellationToken cancellationToken);
}

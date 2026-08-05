using Diten.AuthService.Domain.S2S;
using Diten.AuthService.Persistence.Repositories;
using Diten.AuthService.Persistence.S2S;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Configurations;

public static class S2SMongoIndexInitializer
{
    public static async Task EnsureAsync(IS2SMongoContext context, CancellationToken cancellationToken = default)
    {
        await context.EnsureCompatibleAsync(cancellationToken);
        await context.ServicePrincipals.Indexes.CreateOneAsync(new CreateIndexModel<ServicePrincipal>(
            Builders<ServicePrincipal>.IndexKeys.Ascending(x => x.ClientId),
            new CreateIndexOptions { Unique = true, Name = ServicePrincipalRepository.ClientIdUniqueIndexName }), cancellationToken: cancellationToken);
        await context.ServiceCredentialDescriptors.Indexes.CreateManyAsync([
            new(Builders<ServiceCredentialDescriptor>.IndexKeys.Ascending(x => x.CredentialId), new CreateIndexOptions { Unique = true, Name = ServiceCredentialDescriptorRepository.CredentialIdUniqueIndexName }),
            new(Builders<ServiceCredentialDescriptor>.IndexKeys.Ascending(x => x.Kid), new CreateIndexOptions { Unique = true, Name = ServiceCredentialDescriptorRepository.KidUniqueIndexName }),
            new(Builders<ServiceCredentialDescriptor>.IndexKeys.Ascending(x => x.ServicePrincipalId).Ascending(x => x.Generation))
        ], cancellationToken: cancellationToken);
        await context.ReplayReceipts.Indexes.CreateManyAsync([
            new(Builders<S2SReplayReceipt>.IndexKeys.Ascending(x => x.Issuer).Ascending(x => x.Jti), new CreateIndexOptions { Unique = true, Name = S2SReplayReceiptStore.IssuerJtiUniqueIndexName }),
            new(Builders<S2SReplayReceipt>.IndexKeys.Ascending(x => x.Issuer).Ascending(x => x.Nonce), new CreateIndexOptions { Unique = true, Name = S2SReplayReceiptStore.IssuerNonceUniqueIndexName })
        ], cancellationToken: cancellationToken);
    }
}

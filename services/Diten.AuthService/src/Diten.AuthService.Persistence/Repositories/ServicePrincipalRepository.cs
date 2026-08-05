using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.S2S;
using Diten.AuthService.Persistence.S2S;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Repositories;

public sealed class ServicePrincipalRepository : IServicePrincipalRepository
{
    public const string CollectionName = "servicePrincipals";
    public const string ClientIdUniqueIndexName = "ux_service_principals_client_id";

    private readonly IMongoCollection<ServicePrincipal> _collection;
    private readonly IS2SMongoContext _context;

    public ServicePrincipalRepository(IS2SMongoContext context)
    {
        _context = context;
        _collection = context.ServicePrincipals;
    }

    public async Task<bool> TryCreateAsync(ServicePrincipal principal, CancellationToken cancellationToken)
    {
        await _context.EnsureCompatibleAsync(cancellationToken);
        try
        {
            await _collection.InsertOneAsync(principal, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public async Task<ServicePrincipal?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken)
    {
        await _context.EnsureCompatibleAsync(cancellationToken);
        return await _collection.Find(Builders<ServicePrincipal>.Filter.And(
                Builders<ServicePrincipal>.Filter.Eq(x => x.ClientId, clientId),
                Builders<ServicePrincipal>.Filter.Eq(x => x.IsDeleted, false)))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> TryReplaceAsync(ServicePrincipal principal, long expectedVersion, CancellationToken cancellationToken)
    {
        await _context.EnsureCompatibleAsync(cancellationToken);
        if (principal.PrincipalVersion != expectedVersion + 1)
            throw new ArgumentException("Principal must contain exactly the next version.", nameof(principal));

        var result = await _collection.UpdateOneAsync(
            Builders<ServicePrincipal>.Filter.And(
                Builders<ServicePrincipal>.Filter.Eq(x => x.ClientId, principal.ClientId),
                Builders<ServicePrincipal>.Filter.Eq("PrincipalVersion", expectedVersion),
                Builders<ServicePrincipal>.Filter.Eq("IsDeleted", false)),
            Builders<ServicePrincipal>.Update
                .Set(x => x.Status, principal.Status)
                .Set(x => x.PrincipalVersion, principal.PrincipalVersion)
                .Set(x => x.CredentialGeneration, principal.CredentialGeneration)
                .Set(x => x.UpdatedAt, principal.UpdatedAt)
                .Set(x => x.UpdatedBy, principal.UpdatedBy),
            cancellationToken: cancellationToken);
        return result.MatchedCount == 1;
    }
}

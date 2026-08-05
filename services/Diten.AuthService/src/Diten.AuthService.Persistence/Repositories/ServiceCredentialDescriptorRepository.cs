using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.S2S;
using Diten.AuthService.Persistence.S2S;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Repositories;

public sealed class ServiceCredentialDescriptorRepository : IServiceCredentialDescriptorRepository
{
    public const string CollectionName = "serviceCredentialDescriptors";
    public const string CredentialIdUniqueIndexName = "ux_service_credentials_credential_id";
    public const string KidUniqueIndexName = "ux_service_credentials_kid";

    private readonly IMongoCollection<ServiceCredentialDescriptor> _collection;
    private readonly IS2SMongoContext _context;

    public ServiceCredentialDescriptorRepository(IS2SMongoContext context)
    {
        _context = context;
        _collection = context.ServiceCredentialDescriptors;
    }

    public async Task<bool> TryCreateAsync(ServiceCredentialDescriptor descriptor, CancellationToken cancellationToken)
    {
        await _context.EnsureCompatibleAsync(cancellationToken);
        try
        {
            await _collection.InsertOneAsync(descriptor, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<ServiceCredentialDescriptor>> GetAcceptedAsync(Guid servicePrincipalId, DateTimeOffset atUtc, CancellationToken cancellationToken)
    {
        await _context.EnsureCompatibleAsync(cancellationToken);
        var filter = Builders<ServiceCredentialDescriptor>.Filter.And(
            Builders<ServiceCredentialDescriptor>.Filter.Eq(x => x.ServicePrincipalId, servicePrincipalId),
            Builders<ServiceCredentialDescriptor>.Filter.In(x => x.Status, new[] { ServiceCredentialStatus.Active, ServiceCredentialStatus.Previous }),
            Builders<ServiceCredentialDescriptor>.Filter.Lte(x => x.NotBeforeUtc, atUtc),
            Builders<ServiceCredentialDescriptor>.Filter.Gt(x => x.ExpiresAtUtc, atUtc),
            Builders<ServiceCredentialDescriptor>.Filter.Eq(x => x.IsDeleted, false),
            Builders<ServiceCredentialDescriptor>.Filter.Or(
                Builders<ServiceCredentialDescriptor>.Filter.Eq(x => x.Status, ServiceCredentialStatus.Active),
                Builders<ServiceCredentialDescriptor>.Filter.Gt(x => x.OverlapValidUntilUtc, atUtc)));
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }
}

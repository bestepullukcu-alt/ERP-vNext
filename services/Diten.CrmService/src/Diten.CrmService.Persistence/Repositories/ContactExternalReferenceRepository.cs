using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

public sealed class ContactExternalReferenceRepository : IContactExternalReferenceRepository
{
    private readonly IMongoCollection<ContactExternalReference> _collection;

    public ContactExternalReferenceRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<ContactExternalReference>("contact_external_references");
    }

    public async Task<bool> ExistsBySourceExternalAsync(
        Guid tenantId, string sourceSystem, string externalId, Guid? excludeId, CancellationToken cancellationToken)
    {
        var filter = Builders<ContactExternalReference>.Filter.Where(r =>
            r.TenantId == tenantId && !r.IsDeleted && r.SourceSystem == sourceSystem && r.ExternalId == externalId);
        if (excludeId is { } id)
        {
            filter &= Builders<ContactExternalReference>.Filter.Ne(r => r.Id, id);
        }

        return await _collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ContactExternalReference>> ListByContactAsync(Guid tenantId, Guid contactId, CancellationToken cancellationToken)
    {
        var filter = Builders<ContactExternalReference>.Filter.Where(r =>
            r.TenantId == tenantId && !r.IsDeleted && r.ContactId == contactId);
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ContactExternalReference>> ListAllAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var filter = Builders<ContactExternalReference>.Filter.Where(r => r.TenantId == tenantId && !r.IsDeleted);
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<ContactExternalReference?> GetBySourceExternalAsync(Guid tenantId, string sourceSystem, string externalId, CancellationToken cancellationToken)
    {
        var filter = Builders<ContactExternalReference>.Filter.Where(r =>
            r.TenantId == tenantId && !r.IsDeleted && r.SourceSystem == sourceSystem && r.ExternalId == externalId);
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task InsertAsync(ContactExternalReference reference, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(reference, cancellationToken: cancellationToken);
}

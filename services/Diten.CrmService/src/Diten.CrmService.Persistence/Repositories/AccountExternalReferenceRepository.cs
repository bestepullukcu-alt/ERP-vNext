using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

public sealed class AccountExternalReferenceRepository : IAccountExternalReferenceRepository
{
    private readonly IMongoCollection<AccountExternalReference> _collection;

    public AccountExternalReferenceRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<AccountExternalReference>("account_external_references");
    }

    public async Task<bool> ExistsBySourceExternalAsync(
        Guid tenantId, string sourceSystem, string externalId, Guid? excludeId, CancellationToken cancellationToken)
    {
        var filter = Builders<AccountExternalReference>.Filter.Where(r =>
            r.TenantId == tenantId && !r.IsDeleted && r.SourceSystem == sourceSystem && r.ExternalId == externalId);
        if (excludeId is { } id)
        {
            filter &= Builders<AccountExternalReference>.Filter.Ne(r => r.Id, id);
        }

        return await _collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AccountExternalReference>> ListByAccountAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken)
    {
        var filter = Builders<AccountExternalReference>.Filter.Where(r =>
            r.TenantId == tenantId && !r.IsDeleted && r.AccountId == accountId);
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task InsertAsync(AccountExternalReference reference, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(reference, cancellationToken: cancellationToken);
}

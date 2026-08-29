using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

public sealed class AccountAttributeValueRepository : IAccountAttributeValueRepository
{
    private readonly IMongoCollection<AccountAttributeValue> _collection;

    public AccountAttributeValueRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<AccountAttributeValue>("account_attribute_values");
    }

    public async Task<IReadOnlyList<AccountAttributeValue>> ListByAccountAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken)
    {
        var filter = Builders<AccountAttributeValue>.Filter.Where(a =>
            a.TenantId == tenantId && !a.IsDeleted && a.AccountId == accountId);
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task UpsertAsync(AccountAttributeValue attribute, CancellationToken cancellationToken)
    {
        var filter = Builders<AccountAttributeValue>.Filter.Where(a =>
            a.TenantId == attribute.TenantId && a.AccountId == attribute.AccountId && a.AttributeCode == attribute.AttributeCode);

        var update = Builders<AccountAttributeValue>.Update
            .Set(a => a.Value, attribute.Value)
            .Set(a => a.UpdatedAt, DateTimeOffset.UtcNow)
            .Set(a => a.IsDeleted, false)
            .SetOnInsert(a => a.Id, attribute.Id)
            .SetOnInsert(a => a.TenantId, attribute.TenantId)
            .SetOnInsert(a => a.AccountId, attribute.AccountId)
            .SetOnInsert(a => a.AttributeCode, attribute.AttributeCode)
            .SetOnInsert(a => a.CreatedAt, DateTimeOffset.UtcNow);

        await _collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
    }
}

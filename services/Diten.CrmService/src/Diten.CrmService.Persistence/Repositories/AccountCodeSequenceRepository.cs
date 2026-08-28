using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

public sealed class AccountCodeSequenceRepository : IAccountCodeSequenceRepository
{
    private readonly IMongoCollection<AccountCodeSequence> _collection;

    public AccountCodeSequenceRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<AccountCodeSequence>("account_code_sequences");
    }

    public async Task<long> NextAsync(Guid tenantId, int year, CancellationToken cancellationToken)
    {
        var filter = Builders<AccountCodeSequence>.Filter.Where(s => s.TenantId == tenantId && s.Year == year);
        var update = Builders<AccountCodeSequence>.Update
            .Inc(s => s.Current, 1L)
            .SetOnInsert(s => s.Id, Guid.NewGuid())
            .SetOnInsert(s => s.TenantId, tenantId)
            .SetOnInsert(s => s.Year, year)
            .SetOnInsert(s => s.CreatedAt, DateTimeOffset.UtcNow);

        var options = new FindOneAndUpdateOptions<AccountCodeSequence>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After
        };

        var result = await _collection.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
        return result.Current;
    }
}

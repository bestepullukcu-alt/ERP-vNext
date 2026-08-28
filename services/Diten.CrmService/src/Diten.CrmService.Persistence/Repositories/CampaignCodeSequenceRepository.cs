using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// MOD-0165 FU10 — the CampaignCode sequence, as a single upserting find-and-modify. The same shape as the account
/// sequence repository: one atomic round trip, so two concurrent creates can never receive the same number.
/// </summary>
public sealed class CampaignCodeSequenceRepository : ICampaignCodeSequenceRepository
{
    private readonly IMongoCollection<CampaignCodeSequence> _collection;

    public CampaignCodeSequenceRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<CampaignCodeSequence>("campaign_code_sequences");
    }

    public async Task<long> NextAsync(Guid tenantId, int year, CancellationToken cancellationToken)
    {
        var filter = Builders<CampaignCodeSequence>.Filter.Where(s => s.TenantId == tenantId && s.Year == year);
        var update = Builders<CampaignCodeSequence>.Update
            .Inc(s => s.Current, 1L)
            .SetOnInsert(s => s.Id, Guid.NewGuid())
            .SetOnInsert(s => s.TenantId, tenantId)
            .SetOnInsert(s => s.Year, year)
            .SetOnInsert(s => s.CreatedAt, DateTimeOffset.UtcNow);

        var options = new FindOneAndUpdateOptions<CampaignCodeSequence>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After
        };

        var result = await _collection.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
        return result.Current;
    }

    /// <summary>
    /// A plain find — no upsert, no <c>$inc</c>. Opening a create form must not create the sequence document either,
    /// or "peek" would quietly become "reserve" for the very first campaign of a year.
    /// </summary>
    public async Task<long> PeekNextAsync(Guid tenantId, int year, CancellationToken cancellationToken)
    {
        var filter = Builders<CampaignCodeSequence>.Filter.Where(s => s.TenantId == tenantId && s.Year == year);
        var existing = await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        return (existing?.Current ?? 0L) + 1L;
    }
}

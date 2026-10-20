using System.Text.RegularExpressions;
using Diten.CrmService.Domain.Entities.Commercial;
using Diten.CrmService.Domain.Repositories.Commercial;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories.Commercial;

// WP-VW-W6 W6-BE-a — MOD-0168 Mongo stores (commercial_pre_order* collections). Every filter starts with the tenant and the
// live rows; a replace is optimistic on Version; no filter compares two DateTimeOffset fields (the CRM array trap) — the
// link's expiry is checked in code right before its one conditional consumption.

public sealed class PreOrderRepository : IPreOrderRepository
{
    public const string CollectionName = "commercial_pre_orders";

    private readonly IMongoCollection<PreOrder> _collection;

    public PreOrderRepository(IMongoDatabase database) => _collection = database.GetCollection<PreOrder>(CollectionName);

    public Task InsertAsync(PreOrder order, CancellationToken cancellationToken) => _collection.InsertOneAsync(order, cancellationToken: cancellationToken);

    public async Task<PreOrder?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection.Find(Builders<PreOrder>.Filter.And(PricingFilters.Live<PreOrder>(tenantId), Builders<PreOrder>.Filter.Eq(o => o.Id, id)))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> ReplaceAsync(PreOrder order, int expectedVersion, CancellationToken cancellationToken)
        => PricingFilters.ReplaceAsync(_collection, order, expectedVersion, cancellationToken);

    public Task<(IReadOnlyList<PreOrder> Items, long Total)> ListAsync(Guid tenantId, PreOrderFilter f, int skip, int take, CancellationToken cancellationToken)
    {
        var b = Builders<PreOrder>.Filter;
        var filter = PricingFilters.Live<PreOrder>(tenantId);
        if (f.RepUserIds is not null)
        {
            var scope = b.In(o => o.Rep.UserId, f.RepUserIds);
            if (f.OrApproverUserId is { } approver)
            {
                scope = b.Or(scope, b.AnyEq(o => o.ApprovalRequirement!.ApproverUserIds, approver));
            }

            filter &= scope;
        }

        if (f.Statuses is { Count: > 0 } statuses)
        {
            filter &= b.In(o => o.Status, statuses);
        }

        if (f.CreatedFrom is { } from)
        {
            filter &= b.Gte(o => o.CreatedAt, from);
        }

        if (f.CreatedTo is { } to)
        {
            filter &= b.Lt(o => o.CreatedAt, to);
        }

        if (f.PharmacyAccountId is { } pharmacy)
        {
            filter &= b.Eq(o => o.Pharmacy.AccountId, pharmacy);
        }

        if (f.DepotAccountId is { } depot)
        {
            filter &= b.Eq(o => o.Depot!.AccountId, depot);
        }

        if (f.SkuId is { } sku)
        {
            filter &= b.ElemMatch(o => o.Lines, l => l.SkuId == sku);
        }

        if (f.LegalEntityId is { } legalEntity)
        {
            filter &= b.Eq(o => o.Seller.LegalEntityId, legalEntity);
        }

        if (!string.IsNullOrWhiteSpace(f.CountryCode))
        {
            filter &= b.Eq(o => o.CountryCode, f.CountryCode);
        }

        if (f.AwaitingApproverUserId is { } awaiting)
        {
            filter &= b.Eq(o => o.Status, PreOrderStatuses.PendingApproval) & b.AnyEq(o => o.ApprovalRequirement!.ApproverUserIds, awaiting);
        }

        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var text = new BsonRegularExpression(Regex.Escape(f.Search.Trim()), "i");
            filter &= b.Or(b.Regex(o => o.Code, text), b.Regex(o => o.Pharmacy.Name, text), b.Regex(o => o.Depot!.Name, text));
        }

        // CreatedAt is a DateTimeOffset (an array): the sort runs on the one indexed field, never on two together
        return PricingFilters.PageAsync(_collection, filter, Builders<PreOrder>.Sort.Descending(o => o.CreatedAt), skip, take, cancellationToken);
    }
}

public sealed class PreOrderSettingsRepository : IPreOrderSettingsRepository
{
    public const string CollectionName = "commercial_pre_order_settings";

    private readonly IMongoCollection<PreOrderSettings> _collection;

    public PreOrderSettingsRepository(IMongoDatabase database) => _collection = database.GetCollection<PreOrderSettings>(CollectionName);

    public async Task<PreOrderSettings?> GetAsync(Guid tenantId, CancellationToken cancellationToken)
        => await _collection.Find(PricingFilters.Live<PreOrderSettings>(tenantId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> SaveAsync(PreOrderSettings settings, int? expectedVersion, CancellationToken cancellationToken)
    {
        if (expectedVersion is { } expected)
        {
            return await PricingFilters.ReplaceAsync(_collection, settings, expected, cancellationToken);
        }

        try
        {
            settings.Version = 1;
            await _collection.InsertOneAsync(settings, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            settings.Version = 0;
            return false; // a concurrent first save won
        }
    }
}

public sealed class PreOrderApprovalLinkRepository : IPreOrderApprovalLinkRepository
{
    public const string CollectionName = "commercial_pre_order_approval_links";

    private readonly IMongoCollection<PreOrderApprovalLink> _collection;

    public PreOrderApprovalLinkRepository(IMongoDatabase database) => _collection = database.GetCollection<PreOrderApprovalLink>(CollectionName);

    public Task InsertAsync(PreOrderApprovalLink link, CancellationToken cancellationToken) => _collection.InsertOneAsync(link, cancellationToken: cancellationToken);

    public async Task<PreOrderApprovalLink?> GetByHashAsync(Guid tenantId, string tokenHash, CancellationToken cancellationToken)
        => await _collection.Find(Builders<PreOrderApprovalLink>.Filter.And(PricingFilters.Live<PreOrderApprovalLink>(tenantId),
            Builders<PreOrderApprovalLink>.Filter.Eq(l => l.TokenHash, tokenHash))).FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> TryConsumeAsync(Guid tenantId, Guid linkId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var b = Builders<PreOrderApprovalLink>.Filter;
        var result = await _collection.UpdateOneAsync(
            PricingFilters.Live<PreOrderApprovalLink>(tenantId) & b.Eq(l => l.Id, linkId) & b.Eq(l => l.ConsumedAt, null) & b.Eq(l => l.SupersededAt, null),
            Builders<PreOrderApprovalLink>.Update.Set(l => l.ConsumedAt, now).Set(l => l.ConsumedByUserId, userId).Inc(l => l.Version, 1),
            cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    public Task SupersedeAsync(Guid tenantId, Guid preOrderId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var b = Builders<PreOrderApprovalLink>.Filter;
        return _collection.UpdateManyAsync(
            PricingFilters.Live<PreOrderApprovalLink>(tenantId) & b.Eq(l => l.PreOrderId, preOrderId) & b.Eq(l => l.SupersededAt, null),
            Builders<PreOrderApprovalLink>.Update.Set(l => l.SupersededAt, now).Inc(l => l.Version, 1), cancellationToken: cancellationToken);
    }

    public Task DeleteAsync(Guid tenantId, Guid linkId, CancellationToken cancellationToken)
        => _collection.DeleteOneAsync(PricingFilters.Live<PreOrderApprovalLink>(tenantId) & Builders<PreOrderApprovalLink>.Filter.Eq(l => l.Id, linkId),
            cancellationToken);
}

/// <summary>RCS-001 §5.8: one <c>findOneAndUpdate</c> + <c>$inc</c> per number (upsert on the first one; a concurrent first
/// upsert that loses the unique index simply retries and increments the winner's document).</summary>
public sealed class PreOrderCodeSequenceRepository : IPreOrderCodeSequenceRepository
{
    public const string CollectionName = "commercial_pre_order_code_sequences";

    private readonly IMongoCollection<PreOrderCodeSequence> _collection;

    public PreOrderCodeSequenceRepository(IMongoDatabase database) => _collection = database.GetCollection<PreOrderCodeSequence>(CollectionName);

    public async Task<long> NextAsync(Guid tenantId, string prefix, CancellationToken cancellationToken)
    {
        var filter = Builders<PreOrderCodeSequence>.Filter.Eq(s => s.TenantId, tenantId) & Builders<PreOrderCodeSequence>.Filter.Eq(s => s.Prefix, prefix);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var update = Builders<PreOrderCodeSequence>.Update.Inc(s => s.Current, 1L)
                    .SetOnInsert(s => s.Id, Guid.NewGuid()).SetOnInsert(s => s.TenantId, tenantId).SetOnInsert(s => s.Prefix, prefix)
                    .SetOnInsert(s => s.CreatedAt, DateTimeOffset.UtcNow);
                var next = await _collection.FindOneAndUpdateAsync(filter, update,
                    new FindOneAndUpdateOptions<PreOrderCodeSequence> { IsUpsert = true, ReturnDocument = ReturnDocument.After }, cancellationToken);
                return next.Current;
            }
            catch (MongoCommandException ex) when (ex.Code == 11000 && attempt < 3)
            {
                // the concurrent first upsert won: increment its document
            }
        }
    }

    public async Task<bool> ExistsAsync(Guid tenantId, string prefix, CancellationToken cancellationToken)
        => await _collection.Find(Builders<PreOrderCodeSequence>.Filter.Eq(s => s.TenantId, tenantId) & Builders<PreOrderCodeSequence>.Filter.Eq(s => s.Prefix, prefix))
            .AnyAsync(cancellationToken);
}

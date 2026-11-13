using System.Text.RegularExpressions;
using Diten.CrmService.Domain.Entities.Commercial;
using Diten.CrmService.Domain.Repositories.Commercial;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories.Commercial;

// WP-VW-W6 W6-BE-b — MOD-0156 Mongo stores (commercial_* collections). Every filter starts with the tenant and the live
// rows (soft delete); a replace is optimistic on Version (the ObjectionResponseRepository pattern).

internal static class PricingFilters
{
    public static FilterDefinition<T> Live<T>(Guid tenantId) where T : Domain.Entities.EntityBase
        => Builders<T>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public static BsonRegularExpression Contains(string text) => new(Regex.Escape(text), "i");

    public static async Task<bool> ReplaceAsync<T>(IMongoCollection<T> collection, T entity, int expectedVersion, CancellationToken ct)
        where T : Domain.Entities.EntityBase
    {
        entity.Version = expectedVersion + 1;
        var result = await collection.ReplaceOneAsync(
            Builders<T>.Filter.Where(x => x.Id == entity.Id && x.TenantId == entity.TenantId && x.Version == expectedVersion), entity,
            cancellationToken: ct);
        if (result.MatchedCount == 1)
        {
            return true;
        }

        entity.Version = expectedVersion;
        return false;
    }

    public static async Task<(IReadOnlyList<T> Items, long Total)> PageAsync<T>(IMongoCollection<T> collection, FilterDefinition<T> filter,
        SortDefinition<T> sort, int skip, int take, CancellationToken ct)
    {
        var total = await collection.CountDocumentsAsync(filter, cancellationToken: ct);
        var items = await collection.Find(filter).Sort(sort).Skip(Math.Max(0, skip)).Limit(Math.Max(1, take)).ToListAsync(ct);
        return (items, total);
    }
}

public sealed class PriceListRepository : IPriceListRepository
{
    public const string CollectionName = "commercial_price_lists";
    private readonly IMongoCollection<PriceList> _collection;

    public PriceListRepository(IMongoDatabase database) => _collection = database.GetCollection<PriceList>(CollectionName);

    public Task InsertAsync(PriceList list, CancellationToken cancellationToken)
        => _collection.InsertOneAsync(list, cancellationToken: cancellationToken);

    public Task<bool> ReplaceAsync(PriceList list, int expectedVersion, CancellationToken cancellationToken)
        => PricingFilters.ReplaceAsync(_collection, list, expectedVersion, cancellationToken);

    public async Task<PriceList?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection.Find(PricingFilters.Live<PriceList>(tenantId) & Builders<PriceList>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<(IReadOnlyList<PriceList> Items, long Total)> ListAsync(Guid tenantId, PriceListFilter filter, int skip, int take,
        CancellationToken cancellationToken)
    {
        var b = Builders<PriceList>.Filter;
        var f = PricingFilters.Live<PriceList>(tenantId);
        if (filter.LegalEntityId is { } le) f &= b.Eq(x => x.LegalEntityId, le);
        if (filter.CountryCode is { } country) f &= b.Eq(x => x.CountryCode, country);
        if (filter.Currency is { } currency) f &= b.Eq(x => x.Currency, currency);
        if (filter.Kind is { } kind) f &= b.Eq(x => x.Kind, kind);
        if (filter.Statuses is { Count: > 0 } statuses) f &= b.In(x => x.Status, statuses);
        if (filter.Search is { } search) f &= b.Regex(x => x.Name, PricingFilters.Contains(search));
        return PricingFilters.PageAsync(_collection, f, Builders<PriceList>.Sort.Descending(x => x.ValidFrom).Descending(x => x.CreatedAt), skip, take,
            cancellationToken);
    }

    public async Task<IReadOnlyList<PriceList>> ListPublishedAsync(Guid tenantId, Guid legalEntityId, string countryCode, CancellationToken cancellationToken)
        => await _collection.Find(PricingFilters.Live<PriceList>(tenantId)
                                  & Builders<PriceList>.Filter.Eq(x => x.LegalEntityId, legalEntityId)
                                  & Builders<PriceList>.Filter.Eq(x => x.CountryCode, countryCode)
                                  & Builders<PriceList>.Filter.Eq(x => x.Status, PriceListStatuses.Published))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PriceList>> ListPublishedForAsync(Guid tenantId, IReadOnlyCollection<(Guid LegalEntityId, string CountryCode)> keys,
        CancellationToken cancellationToken)
    {
        if (keys.Count == 0)
        {
            return Array.Empty<PriceList>();
        }

        var b = Builders<PriceList>.Filter;
        var any = b.Or(keys.Select(k => b.Eq(x => x.LegalEntityId, k.LegalEntityId) & b.Eq(x => x.CountryCode, k.CountryCode)));
        return await _collection.Find(PricingFilters.Live<PriceList>(tenantId) & b.Eq(x => x.Status, PriceListStatuses.Published) & any)
            .ToListAsync(cancellationToken);
    }
}

public sealed class PriceListItemRepository : IPriceListItemRepository
{
    public const string CollectionName = "commercial_price_list_items";
    private readonly IMongoCollection<PriceListItem> _collection;

    public PriceListItemRepository(IMongoDatabase database) => _collection = database.GetCollection<PriceListItem>(CollectionName);

    public Task InsertManyAsync(IReadOnlyList<PriceListItem> items, CancellationToken cancellationToken)
        => items.Count == 0 ? Task.CompletedTask : _collection.InsertManyAsync(items, new InsertManyOptions { IsOrdered = true }, cancellationToken);

    public Task InsertAsync(PriceListItem item, CancellationToken cancellationToken)
        => _collection.InsertOneAsync(item, cancellationToken: cancellationToken);

    public Task<bool> ReplaceAsync(PriceListItem item, int expectedVersion, CancellationToken cancellationToken)
        => PricingFilters.ReplaceAsync(_collection, item, expectedVersion, cancellationToken);

    public async Task<PriceListItem?> GetByIdAsync(Guid tenantId, Guid priceListId, Guid id, CancellationToken cancellationToken)
        => await _collection.Find(Of(tenantId, priceListId) & Builders<PriceListItem>.Filter.Eq(x => x.Id, id)).FirstOrDefaultAsync(cancellationToken);

    public async Task<PriceListItem?> GetBySkuAsync(Guid tenantId, Guid priceListId, Guid skuId, CancellationToken cancellationToken)
        => await _collection.Find(Of(tenantId, priceListId) & Builders<PriceListItem>.Filter.Eq(x => x.SkuId, skuId)).FirstOrDefaultAsync(cancellationToken);

    public Task<(IReadOnlyList<PriceListItem> Items, long Total)> ListAsync(Guid tenantId, Guid priceListId, string? search, int skip, int take,
        CancellationToken cancellationToken)
    {
        var f = Of(tenantId, priceListId);
        if (search is { } text)
        {
            var b = Builders<PriceListItem>.Filter;
            var pattern = PricingFilters.Contains(text);
            f &= b.Regex(x => x.SkuCode, pattern) | b.Regex(x => x.SkuName, pattern) | b.Regex(x => x.Gtin, pattern);
        }

        return PricingFilters.PageAsync(_collection, f, Builders<PriceListItem>.Sort.Ascending(x => x.SkuCode).Ascending(x => x.Id), skip, take,
            cancellationToken);
    }

    public async Task<IReadOnlyList<PriceListItem>> ListAllAsync(Guid tenantId, Guid priceListId, CancellationToken cancellationToken)
        => await _collection.Find(Of(tenantId, priceListId)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PriceListItem>> ListBySkusAsync(Guid tenantId, Guid priceListId, IReadOnlyCollection<Guid> skuIds,
        CancellationToken cancellationToken)
        => skuIds.Count == 0
            ? Array.Empty<PriceListItem>()
            : await _collection.Find(Of(tenantId, priceListId) & Builders<PriceListItem>.Filter.In(x => x.SkuId, skuIds)).ToListAsync(cancellationToken);

    public async Task<int> CountAsync(Guid tenantId, Guid priceListId, CancellationToken cancellationToken)
        => (int)await _collection.CountDocumentsAsync(Of(tenantId, priceListId), cancellationToken: cancellationToken);

    private static FilterDefinition<PriceListItem> Of(Guid tenantId, Guid priceListId)
        => PricingFilters.Live<PriceListItem>(tenantId) & Builders<PriceListItem>.Filter.Eq(x => x.PriceListId, priceListId);
}

public sealed class PriceListImportRepository : IPriceListImportRepository
{
    public const string CollectionName = "commercial_price_list_imports";
    public const string RowsCollectionName = "commercial_price_list_import_rows";
    private readonly IMongoCollection<PriceListImport> _imports;
    private readonly IMongoCollection<PriceListImportRow> _rows;

    public PriceListImportRepository(IMongoDatabase database)
    {
        _imports = database.GetCollection<PriceListImport>(CollectionName);
        _rows = database.GetCollection<PriceListImportRow>(RowsCollectionName);
    }

    public Task InsertAsync(PriceListImport import, CancellationToken cancellationToken)
        => _imports.InsertOneAsync(import, cancellationToken: cancellationToken);

    public Task<bool> ReplaceAsync(PriceListImport import, int expectedVersion, CancellationToken cancellationToken)
        => PricingFilters.ReplaceAsync(_imports, import, expectedVersion, cancellationToken);

    public async Task<PriceListImport?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _imports.Find(PricingFilters.Live<PriceListImport>(tenantId) & Builders<PriceListImport>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task InsertRowsAsync(IReadOnlyList<PriceListImportRow> rows, CancellationToken cancellationToken)
    {
        foreach (var chunk in rows.Chunk(1000))
        {
            await _rows.InsertManyAsync(chunk, new InsertManyOptions { IsOrdered = true }, cancellationToken);
        }
    }

    public Task<(IReadOnlyList<PriceListImportRow> Items, long Total)> ListRowsAsync(Guid tenantId, Guid importId,
        IReadOnlyCollection<string>? rowStatuses, string? diffKind, int skip, int take, CancellationToken cancellationToken)
    {
        var b = Builders<PriceListImportRow>.Filter;
        var f = PricingFilters.Live<PriceListImportRow>(tenantId) & b.Eq(x => x.ImportId, importId);
        if (rowStatuses is { Count: > 0 } statuses) f &= b.In(x => x.RowStatus, statuses);
        if (diffKind is { } diff) f &= b.Eq(x => x.DiffKind, diff);
        return PricingFilters.PageAsync(_rows, f, Builders<PriceListImportRow>.Sort.Ascending(x => x.RowNo), skip, take, cancellationToken);
    }

    public async Task<IReadOnlyList<PriceListImportRow>> ListRowsAsync(Guid tenantId, Guid importId, string rowStatus, CancellationToken cancellationToken)
        => await _rows.Find(PricingFilters.Live<PriceListImportRow>(tenantId)
                            & Builders<PriceListImportRow>.Filter.Eq(x => x.ImportId, importId)
                            & Builders<PriceListImportRow>.Filter.Eq(x => x.RowStatus, rowStatus))
            .Sort(Builders<PriceListImportRow>.Sort.Ascending(x => x.RowNo)).ToListAsync(cancellationToken);
}

public sealed class PricingRuleRepository : IPricingRuleRepository
{
    public const string CollectionName = "commercial_pricing_rules";
    private readonly IMongoCollection<PricingRule> _collection;

    public PricingRuleRepository(IMongoDatabase database) => _collection = database.GetCollection<PricingRule>(CollectionName);

    public Task InsertAsync(PricingRule rule, CancellationToken cancellationToken)
        => _collection.InsertOneAsync(rule, cancellationToken: cancellationToken);

    public Task<bool> ReplaceAsync(PricingRule rule, int expectedVersion, CancellationToken cancellationToken)
        => PricingFilters.ReplaceAsync(_collection, rule, expectedVersion, cancellationToken);

    public async Task<PricingRule?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection.Find(PricingFilters.Live<PricingRule>(tenantId) & Builders<PricingRule>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<(IReadOnlyList<PricingRule> Items, long Total)> ListAsync(Guid tenantId, PricingRuleFilter filter, int skip, int take,
        CancellationToken cancellationToken)
    {
        var b = Builders<PricingRule>.Filter;
        var f = PricingFilters.Live<PricingRule>(tenantId);
        if (filter.LegalEntityId is { } le) f &= b.Eq(x => x.LegalEntityId, le);
        if (filter.SkuId is { } sku) f &= b.Eq(x => x.SkuId, sku);
        if (filter.SegmentCode is { } segment) f &= b.Eq(x => x.SegmentCode, segment);
        if (filter.Statuses is { Count: > 0 } statuses) f &= b.In(x => x.Status, statuses);
        if (filter.Search is { } search)
        {
            var pattern = PricingFilters.Contains(search);
            f &= b.Regex(x => x.SkuCode, pattern) | b.Regex(x => x.SkuName, pattern);
        }

        return PricingFilters.PageAsync(_collection, f, Builders<PricingRule>.Sort.Ascending(x => x.SkuCode).Descending(x => x.RuleVersion), skip, take,
            cancellationToken);
    }

    public async Task<IReadOnlyList<PricingRule>> ListByRuleKeyAsync(Guid tenantId, Guid ruleKey, CancellationToken cancellationToken)
        => await _collection.Find(PricingFilters.Live<PricingRule>(tenantId) & Builders<PricingRule>.Filter.Eq(x => x.RuleKey, ruleKey))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PricingRule>> ListActiveAsync(Guid tenantId, Guid legalEntityId, IReadOnlyCollection<Guid> skuIds,
        CancellationToken cancellationToken)
        => skuIds.Count == 0
            ? Array.Empty<PricingRule>()
            : await _collection.Find(PricingFilters.Live<PricingRule>(tenantId)
                                     & Builders<PricingRule>.Filter.Eq(x => x.LegalEntityId, legalEntityId)
                                     & Builders<PricingRule>.Filter.In(x => x.SkuId, skuIds)
                                     & Builders<PricingRule>.Filter.Eq(x => x.Status, PricingRuleStatuses.Active))
                .ToListAsync(cancellationToken);
}

public sealed class FxRateRepository : IFxRateRepository
{
    public const string CollectionName = "commercial_fx_rates";
    private readonly IMongoCollection<FxRate> _collection;

    public FxRateRepository(IMongoDatabase database) => _collection = database.GetCollection<FxRate>(CollectionName);

    public async Task<bool> TryInsertAsync(FxRate rate, CancellationToken cancellationToken)
    {
        try
        {
            await _collection.InsertOneAsync(rate, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public Task<bool> ReplaceAsync(FxRate rate, int expectedVersion, CancellationToken cancellationToken)
        => PricingFilters.ReplaceAsync(_collection, rate, expectedVersion, cancellationToken);

    public async Task<FxRate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection.Find(PricingFilters.Live<FxRate>(tenantId) & Builders<FxRate>.Filter.Eq(x => x.Id, id)).FirstOrDefaultAsync(cancellationToken);

    public async Task<FxRate?> GetByKeyAsync(Guid tenantId, Guid legalEntityId, string from, string to, DateOnly rateDate, CancellationToken cancellationToken)
        => await _collection.Find(Pair(tenantId, legalEntityId, from, to) & Builders<FxRate>.Filter.Eq(x => x.RateDate, rateDate))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<(IReadOnlyList<FxRate> Items, long Total)> ListAsync(Guid tenantId, FxRateFilter filter, int skip, int take, CancellationToken cancellationToken)
    {
        var b = Builders<FxRate>.Filter;
        var f = PricingFilters.Live<FxRate>(tenantId);
        if (filter.LegalEntityId is { } le) f &= b.Eq(x => x.LegalEntityId, le);
        if (filter.FromCurrency is { } from) f &= b.Eq(x => x.FromCurrency, from);
        if (filter.ToCurrency is { } to) f &= b.Eq(x => x.ToCurrency, to);
        if (filter.From is { } day) f &= b.Gte(x => x.RateDate, day);
        if (filter.To is { } last) f &= b.Lte(x => x.RateDate, last);
        return PricingFilters.PageAsync(_collection, f, Builders<FxRate>.Sort.Descending(x => x.RateDate).Ascending(x => x.FromCurrency), skip, take,
            cancellationToken);
    }

    public async Task<FxRate?> GetLatestAsync(Guid tenantId, Guid legalEntityId, string from, string to, DateOnly asOf, CancellationToken cancellationToken)
        => await _collection.Find(Pair(tenantId, legalEntityId, from, to) & Builders<FxRate>.Filter.Lte(x => x.RateDate, asOf))
            .Sort(Builders<FxRate>.Sort.Descending(x => x.RateDate)).FirstOrDefaultAsync(cancellationToken);

    private static FilterDefinition<FxRate> Pair(Guid tenantId, Guid legalEntityId, string from, string to)
        => PricingFilters.Live<FxRate>(tenantId)
           & Builders<FxRate>.Filter.Eq(x => x.LegalEntityId, legalEntityId)
           & Builders<FxRate>.Filter.Eq(x => x.FromCurrency, from)
           & Builders<FxRate>.Filter.Eq(x => x.ToCurrency, to);
}

public sealed class PricingChangeLogRepository : IPricingChangeLogRepository
{
    public const string CollectionName = "commercial_pricing_change_log";
    private readonly IMongoCollection<PricingChangeLog> _collection;

    public PricingChangeLogRepository(IMongoDatabase database) => _collection = database.GetCollection<PricingChangeLog>(CollectionName);

    public Task AppendAsync(IReadOnlyList<PricingChangeLog> entries, CancellationToken cancellationToken)
        => entries.Count == 0 ? Task.CompletedTask : _collection.InsertManyAsync(entries, cancellationToken: cancellationToken);

    public Task<(IReadOnlyList<PricingChangeLog> Items, long Total)> ListAsync(Guid tenantId, PricingChangeLogFilter filter, int skip, int take,
        CancellationToken cancellationToken)
    {
        var b = Builders<PricingChangeLog>.Filter;
        var f = PricingFilters.Live<PricingChangeLog>(tenantId);
        if (filter.EntityTypes is { Count: > 0 } types) f &= b.In(x => x.EntityType, types);
        if (filter.EntityId is { } id) f &= b.Eq(x => x.EntityId, id);
        if (filter.ParentId is { } parent) f &= b.Eq(x => x.ParentId, parent);
        if (filter.ByUserId is { } by) f &= b.Eq(x => x.ByUserId, by);
        if (filter.From is { } from) f &= b.Gte(x => x.At, from);
        if (filter.To is { } to) f &= b.Lte(x => x.At, to);
        return PricingFilters.PageAsync(_collection, f, Builders<PricingChangeLog>.Sort.Descending(x => x.At), skip, take, cancellationToken);
    }
}

using System.Globalization;
using System.Text.RegularExpressions;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

// MOD-0290-FU04 S4: tenant-bounded batch reads for the product-master contract. The live rule of each kind is its own
// repository's: items and Finished Goods IsDeleted ≠ true (a legacy document without the field is live), GSKUs and
// LSKUs (and revisions) IsDeleted = false.
public sealed class ProductMasterReadStore : IProductMasterReadStore
{
    /// <summary>The comment every search aggregation carries (the profiler / a test reads the queries by it).</summary>
    public const string SearchComment = "product-master:stockable-items";

    private readonly IMongoCollection<Item> _items;
    private readonly IMongoCollection<Gsku> _gskus;
    private readonly IMongoCollection<Lsku> _lskus;
    private readonly IMongoCollection<FinishedGood> _finishedGoods;
    private readonly Guid _tenantId;

    public ProductMasterReadStore(IMongoDatabase database, ITenantContext tenantContext)
    {
        _items = database.GetCollection<Item>(ItemRepository.CollectionName);
        _gskus = database.GetCollection<Gsku>("mdm_gskus");
        _lskus = database.GetCollection<Lsku>("mdm_lskus");
        _finishedGoods = database.GetCollection<FinishedGood>("mdm_finished_goods");
        _tenantId = tenantContext.TenantId;
    }

    private FilterDefinition<Item> LiveItems =>
        Builders<Item>.Filter.Eq(x => x.TenantId, _tenantId) & Builders<Item>.Filter.Ne(x => x.IsDeleted, true);

    private FilterDefinition<Gsku> LiveGskus =>
        Builders<Gsku>.Filter.Eq(x => x.TenantId, _tenantId) & Builders<Gsku>.Filter.Eq(x => x.IsDeleted, false);

    private FilterDefinition<Lsku> LiveLskus =>
        Builders<Lsku>.Filter.Eq(x => x.TenantId, _tenantId) & Builders<Lsku>.Filter.Eq(x => x.IsDeleted, false);

    private FilterDefinition<FinishedGood> LiveFinishedGoods =>
        Builders<FinishedGood>.Filter.Eq(x => x.TenantId, _tenantId)
        & Builders<FinishedGood>.Filter.Ne(x => x.IsDeleted, true);

    public async Task<IReadOnlyList<Item>> GetItemsByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        ids.Count == 0
            ? []
            : await _items.Find(LiveItems & Builders<Item>.Filter.In(x => x.Id, ids)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Lsku>> GetLskusByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        ids.Count == 0
            ? []
            : await _lskus.Find(LiveLskus & Builders<Lsku>.Filter.In(x => x.Id, ids)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FinishedGood>> GetFinishedGoodsByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        ids.Count == 0
            ? []
            : await _finishedGoods.Find(LiveFinishedGoods & Builders<FinishedGood>.Filter.In(x => x.Id, ids))
                .ToListAsync(cancellationToken);

    // S4 FIX1: one projected GSKU read (id, status, lot), then the children's serial flags projected.
    public async Task<ProductMasterProductFlags> GetLiveSkuFlagsOfGlobalProductAsync(
        Guid globalProductId, CancellationToken cancellationToken = default)
    {
        var revisionIds = await _gskus.Database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions")
            .Find(Builders<ProductDefinitionRevision>.Filter.Eq(x => x.TenantId, _tenantId)
                  & Builders<ProductDefinitionRevision>.Filter.Eq(x => x.IsDeleted, false)
                  & Builders<ProductDefinitionRevision>.Filter.Eq(x => x.GlobalProductId, globalProductId))
            .Project(x => x.Id)
            .ToListAsync(cancellationToken);
        if (revisionIds.Count == 0) return new([], []);
        var gskus = await _gskus.Find(LiveGskus & Builders<Gsku>.Filter.In(x => x.ProductDefinitionRevisionId, revisionIds))
            .Project(x => new { x.Id, x.LifecycleStatus, x.LotControlled })
            .ToListAsync(cancellationToken);
        var gskuIds = gskus.Select(x => x.Id).ToArray();
        var lots = gskus.Where(x => x.LifecycleStatus != ProductIdentityLifecycleStatus.Retired)
            .Select(x => x.LotControlled).ToList();
        if (gskuIds.Length == 0) return new(lots, []);
        // The LSKUs and Finished Goods of every GSKU of the product (a retired GSKU's live children still count).
        var lskuSerials = await _lskus.Find(LiveLskus
                & Builders<Lsku>.Filter.In(x => x.GskuId, gskuIds)
                & Builders<Lsku>.Filter.Ne(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.Retired))
            .Project(x => x.SerialControlled)
            .ToListAsync(cancellationToken);
        var finishedGoodSerials = await _finishedGoods.Find(LiveFinishedGoods
                & Builders<FinishedGood>.Filter.In(x => x.GskuId, gskuIds)
                & Builders<FinishedGood>.Filter.Ne(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.Retired))
            .Project(x => x.SerialControlled)
            .ToListAsync(cancellationToken);
        return new(lots, [.. lskuSerials, .. finishedGoodSerials]);
    }

    // ------------------------------------------------------------------------------------------- stockable search

    public async Task<ProductMasterStockablePage> SearchStockableAsync(
        ProductMasterStockableSearch search, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(search);
        if (search.Window is < 1) throw new ArgumentOutOfRangeException(nameof(search), "The window is at least one row.");
        var rows = new List<ProductMasterStockableRow>();
        long total = 0;
        var unreadable = 0;

        async Task RunAsync<T>(IMongoCollection<T> collection, FilterDefinition<T> match, IEnumerable<BsonDocument> stages,
            Func<BsonDocument, ProductMasterStockableRow> read)
        {
            var pipeline = new List<BsonDocument>(stages);
            if (search.Query is { Length: > 0 } query)
            {
                var escaped = Regex.Escape(query);
                pipeline.Add(new BsonDocument("$match", new BsonDocument("$or", new BsonArray
                {
                    new BsonDocument("CanonicalCode", new BsonRegularExpression("^" + escaped, "i")),
                    new BsonDocument("SearchName", new BsonRegularExpression(escaped, "i"))
                })));
            }
            pipeline.Add(new BsonDocument("$sort", new BsonDocument { { "CanonicalCode", 1 }, { "_id", 1 } }));
            pipeline.Add(new BsonDocument("$facet", new BsonDocument
            {
                { "rows", new BsonArray { new BsonDocument("$limit", search.Window) } },
                { "total", new BsonArray { new BsonDocument("$count", "n") } }
            }));
            var fluent = collection.Aggregate(new AggregateOptions { AllowDiskUse = true, Comment = SearchComment })
                .Match(match);
            var shaped = pipeline.Aggregate((IAggregateFluent<BsonDocument>)fluent.As<BsonDocument>(),
                (current, stage) => current.AppendStage<BsonDocument>(stage));
            var result = await shaped.FirstOrDefaultAsync(cancellationToken);
            if (result is null) return;
            total += result["total"].AsBsonArray.FirstOrDefault()?.AsBsonDocument["n"].ToInt64() ?? 0;
            foreach (var row in result["rows"].AsBsonArray)
            {
                try
                {
                    rows.Add(read(row.AsBsonDocument));
                }
                catch (Exception exception) when (exception is InvalidCastException or FormatException
                                                      or KeyNotFoundException or OverflowException or ArgumentException
                                                      or InvalidOperationException)
                {
                    // A row the store cannot read is skipped and counted (the caller logs it by name); it never fails
                    // the whole search and is never answered half-read.
                    unreadable++;
                }
            }
        }

        if (search.Items)
            await RunAsync(_items, ItemMatch(search), ItemStages(), ReadItem);
        if (search.ProductScope is { } scope)
        {
            if (search.Gskus)
                await RunAsync(_gskus, GskuMatch(search), GskuStages(scope), row => ReadProduct(row, ProductMasterRowKind.Gsku));
            if (search.Lskus)
                await RunAsync(_lskus, LskuMatch(search), LskuStages(scope), row => ReadProduct(row, ProductMasterRowKind.Lsku));
            if (search.FinishedGoods)
                await RunAsync(_finishedGoods, FinishedGoodMatch(search), FinishedGoodStages(scope),
                    row => ReadProduct(row, ProductMasterRowKind.FinishedGood));
        }
        return new(rows, total, unreadable);
    }

    // §16.3 inverse — the wanted published statuses as stored ones (a PendingActivation item by its activation kind).
    private FilterDefinition<Item> ItemMatch(ProductMasterStockableSearch search)
    {
        var filter = LiveItems & Builders<Item>.Filter.Eq(x => x.Stockable, true);
        if (search.MaterialType is { } type) filter &= Builders<Item>.Filter.Eq(x => x.MaterialType, type);
        var status = Builders<Item>.Filter.In(x => x.LifecycleStatus, search.ItemStatuses);
        if (search.PendingFirstActivation)
            status |= Builders<Item>.Filter.Eq(x => x.LifecycleStatus, ItemLifecycleStatus.PendingActivation)
                      & Builders<Item>.Filter.Ne(x => x.ActivationKind, ItemActivationKind.Reactivation);
        if (search.PendingReactivation)
            status |= Builders<Item>.Filter.Eq(x => x.LifecycleStatus, ItemLifecycleStatus.PendingActivation)
                      & Builders<Item>.Filter.Eq(x => x.ActivationKind, ItemActivationKind.Reactivation);
        return filter & status;
    }

    private static IEnumerable<BsonDocument> ItemStages()
    {
        yield return new BsonDocument("$project", new BsonDocument
        {
            { "CanonicalCode", 1 }, { "Name", 1 }, { "SearchName", "$Name" }, { "MaterialType", 1 },
            { "LifecycleStatus", 1 }, { "ActivationKind", 1 }, { "BaseUomCode", 1 }, { "LotControlled", 1 }
        });
    }

    // StockBehaviourRules.GskuIsComplete in the store: base unit, shelf life, a storage condition and the lot flag.
    private static BsonDocument GskuComplete() => new()
    {
        { "Stockable", true },
        { "BaseUomCode", new BsonDocument("$nin", new BsonArray { BsonNull.Value, "" }) },
        { "ShelfLifeDays", new BsonDocument("$ne", BsonNull.Value) },
        { "StorageConditionCodes.0", new BsonDocument("$exists", true) },
        { "LotControlled", new BsonDocument("$ne", BsonNull.Value) }
    };

    private FilterDefinition<Gsku> GskuMatch(ProductMasterStockableSearch search) =>
        LiveGskus & (FilterDefinition<Gsku>)GskuComplete() & Builders<Gsku>.Filter.In(x => x.LifecycleStatus, search.ProductStatuses);

    private IEnumerable<BsonDocument> GskuStages(ProductMasterProductScope scope)
    {
        yield return new BsonDocument("$project", new BsonDocument
        {
            { "CanonicalCode", 1 }, { "LifecycleStatus", 1 }, { "BaseUomCode", 1 }, { "LotControlled", 1 },
            { "PackQuantity", 1 }, { "PackUomCode", 1 }, { "ProductDefinitionRevisionId", 1 }
        });
        foreach (var stage in ProductStages("$ProductDefinitionRevisionId", scope,
                     new BsonArray { "$Gp.GlobalProductName", " · ", "$PackQuantity", " ", "$PackUomCode" }))
            yield return stage;
    }

    private FilterDefinition<Lsku> LskuMatch(ProductMasterStockableSearch search) =>
        LiveLskus
        & Builders<Lsku>.Filter.Ne(x => x.SerialControlled, null)
        & Builders<Lsku>.Filter.In(x => x.LifecycleStatus, search.ProductStatuses);

    // O-3: an LSKU is stockable exactly when its GSKU is and its own serial flag is set.
    private IEnumerable<BsonDocument> LskuStages(ProductMasterProductScope scope)
    {
        yield return new BsonDocument("$project", new BsonDocument
        {
            { "CanonicalCode", 1 }, { "LifecycleStatus", 1 }, { "SerialControlled", 1 }, { "MarketCode", 1 }, { "GskuId", 1 }
        });
        yield return ParentGsku(GskuComplete());
        yield return new BsonDocument("$match", new BsonDocument("G", new BsonDocument("$size", 1)));
        yield return new BsonDocument("$set", new BsonDocument
        {
            { "G", new BsonDocument("$arrayElemAt", new BsonArray { "$G", 0 }) }
        });
        yield return new BsonDocument("$set", new BsonDocument
        {
            { "BaseUomCode", "$G.BaseUomCode" }, { "LotControlled", "$G.LotControlled" },
            { "PackQuantity", "$G.PackQuantity" }, { "PackUomCode", "$G.PackUomCode" }
        });
        foreach (var stage in ProductStages("$G.ProductDefinitionRevisionId", scope,
                     new BsonArray { "$Gp.GlobalProductName", " · ", "$PackQuantity", " ", "$PackUomCode", " · ", "$MarketCode" }))
            yield return stage;
    }

    // StockBehaviourRules.FinishedGoodIsComplete in the store (own fields here, the GSKU's base unit / shelf life after
    // the join).
    private FilterDefinition<FinishedGood> FinishedGoodMatch(ProductMasterStockableSearch search) =>
        LiveFinishedGoods
        & new BsonDocument
        {
            { "Stockable", true },
            { "LotControlled", new BsonDocument("$ne", BsonNull.Value) },
            { "SerialControlled", new BsonDocument("$ne", BsonNull.Value) },
            { "Gtin", new BsonDocument("$nin", new BsonArray { BsonNull.Value, "" }) },
            { "StorageConditionCodes.0", new BsonDocument("$exists", true) },
            { "UomConversions", new BsonDocument("$elemMatch", new BsonDocument("$or", new BsonArray
                {
                    new BsonDocument { { "FromUom", StockBehaviourRules.CaseUom }, { "ToUom", StockBehaviourRules.BoxUom } },
                    new BsonDocument { { "FromUom", StockBehaviourRules.BoxUom }, { "ToUom", StockBehaviourRules.CaseUom } }
                })) }
        }
        & Builders<FinishedGood>.Filter.In(x => x.LifecycleStatus, search.ProductStatuses);

    private IEnumerable<BsonDocument> FinishedGoodStages(ProductMasterProductScope scope)
    {
        yield return new BsonDocument("$project", new BsonDocument
        {
            { "CanonicalCode", 1 }, { "LifecycleStatus", 1 }, { "LotControlled", 1 }, { "SerialControlled", 1 },
            { "ShelfLifeDays", 1 }, { "GskuId", 1 }
        });
        yield return ParentGsku(new BsonDocument("BaseUomCode", new BsonDocument("$nin", new BsonArray { BsonNull.Value, "" })));
        yield return new BsonDocument("$match", new BsonDocument("G", new BsonDocument("$size", 1)));
        yield return new BsonDocument("$set", new BsonDocument
        {
            { "G", new BsonDocument("$arrayElemAt", new BsonArray { "$G", 0 }) }
        });
        // D-2: the Finished Good's own shelf life or, when it has none, the GSKU's.
        yield return new BsonDocument("$match", new BsonDocument("$or", new BsonArray
        {
            new BsonDocument("ShelfLifeDays", new BsonDocument("$ne", BsonNull.Value)),
            new BsonDocument("G.ShelfLifeDays", new BsonDocument("$ne", BsonNull.Value))
        }));
        yield return new BsonDocument("$set", new BsonDocument
        {
            { "BaseUomCode", "$G.BaseUomCode" }, { "PackQuantity", "$G.PackQuantity" }, { "PackUomCode", "$G.PackUomCode" }
        });
        foreach (var stage in ProductStages("$G.ProductDefinitionRevisionId", scope,
                     new BsonArray { "$Gp.GlobalProductName", " · ", "$PackQuantity", " ", "$PackUomCode" }))
            yield return stage;
    }

    private BsonDocument ParentGsku(BsonDocument conditions)
    {
        var match = new BsonDocument(conditions)
        {
            { "IsDeleted", false },
            { "$expr", new BsonDocument("$and", new BsonArray
                {
                    new BsonDocument("$eq", new BsonArray { "$TenantId", ProductLegalEntityScopeAggregation.GuidBson(_tenantId) }),
                    new BsonDocument("$eq", new BsonArray { "$_id", "$$gskuId" })
                }) }
        };
        return new BsonDocument("$lookup", new BsonDocument
        {
            { "from", "mdm_gskus" },
            { "let", new BsonDocument("gskuId", "$GskuId") },
            { "pipeline", new BsonArray
                {
                    new BsonDocument("$match", match),
                    new BsonDocument("$project", new BsonDocument
                    {
                        { "BaseUomCode", 1 }, { "ShelfLifeDays", 1 }, { "LotControlled", 1 }, { "PackQuantity", 1 },
                        { "PackUomCode", 1 }, { "ProductDefinitionRevisionId", 1 }
                    })
                } },
            { "as", "G" }
        });
    }

    // The revision → Global Product chain, the product scope (the same stages the registers' enforced lists use) and
    // the O-5 search name.
    private IEnumerable<BsonDocument> ProductStages(string revisionIdExpression, ProductMasterProductScope scope,
        BsonArray nameParts)
    {
        var tenant = ProductLegalEntityScopeAggregation.GuidBson(_tenantId);
        yield return new BsonDocument("$lookup", new BsonDocument
        {
            { "from", "mdm_product_definition_revisions" },
            { "let", new BsonDocument("revisionId", revisionIdExpression) },
            { "pipeline", new BsonArray
                {
                    new BsonDocument("$match", new BsonDocument("$expr", new BsonDocument("$and", new BsonArray
                    {
                        new BsonDocument("$eq", new BsonArray { "$TenantId", tenant }),
                        new BsonDocument("$eq", new BsonArray { "$IsDeleted", false }),
                        new BsonDocument("$eq", new BsonArray { "$_id", "$$revisionId" })
                    }))),
                    new BsonDocument("$project", new BsonDocument("GlobalProductId", 1))
                } },
            { "as", "R" }
        });
        yield return new BsonDocument("$match", new BsonDocument("R", new BsonDocument("$size", 1)));
        yield return new BsonDocument("$set", new BsonDocument(ProductLegalEntityScopeAggregation.ResolvedGlobalProductIdField,
            new BsonDocument("$getField", new BsonDocument
            {
                { "field", "GlobalProductId" },
                { "input", new BsonDocument("$arrayElemAt", new BsonArray { "$R", 0 }) }
            })));
        if (!scope.Unrestricted)
            foreach (var stage in ProductLegalEntityScopeAggregation.CreateAccessStages(
                         _tenantId, scope.CandidateLegalEntityIds, scope.ServerNowUtc))
                yield return stage;
        yield return new BsonDocument("$lookup", new BsonDocument
        {
            { "from", "mdm_global_products" },
            { "let", new BsonDocument("globalProductId", "$" + ProductLegalEntityScopeAggregation.ResolvedGlobalProductIdField) },
            { "pipeline", new BsonArray
                {
                    new BsonDocument("$match", new BsonDocument("$expr", new BsonDocument("$and", new BsonArray
                    {
                        new BsonDocument("$eq", new BsonArray { "$TenantId", tenant }),
                        new BsonDocument("$eq", new BsonArray { "$IsDeleted", false }),
                        new BsonDocument("$eq", new BsonArray { "$_id", "$$globalProductId" })
                    }))),
                    new BsonDocument("$project", new BsonDocument("GlobalProductName", 1))
                } },
            { "as", "Gp" }
        });
        yield return new BsonDocument("$match", new BsonDocument("Gp", new BsonDocument("$size", 1)));
        yield return new BsonDocument("$set", new BsonDocument("Gp", new BsonDocument("$arrayElemAt", new BsonArray { "$Gp", 0 })));
        yield return new BsonDocument("$set", new BsonDocument("SearchName", new BsonDocument("$concat", new BsonArray(
            nameParts.Select(part => part is BsonString { Value: var text } && text.StartsWith('$')
                ? new BsonDocument("$toString", new BsonDocument("$ifNull", new BsonArray { part, "" }))
                : part)))));
        yield return new BsonDocument("$project", new BsonDocument
        {
            { "CanonicalCode", 1 }, { "SearchName", 1 }, { "LifecycleStatus", 1 }, { "BaseUomCode", 1 },
            { "LotControlled", 1 }, { "SerialControlled", 1 }, { "PackQuantity", 1 }, { "PackUomCode", 1 },
            { "MarketCode", 1 }, { "ProductName", "$Gp.GlobalProductName" },
            { "GlobalProductId", "$" + ProductLegalEntityScopeAggregation.ResolvedGlobalProductIdField }
        });
    }

    private static ProductMasterStockableRow ReadItem(BsonDocument row)
    {
        var id = row["_id"].AsBsonBinaryData.ToGuid();
        return new(ProductMasterRowKind.Item, id, id, row["CanonicalCode"].AsString, row["Name"].AsString,
            Enum<ItemMaterialType>(row["MaterialType"]), Enum<ItemLifecycleStatus>(row["LifecycleStatus"]),
            row.TryGetValue("ActivationKind", out var kind) && !kind.IsBsonNull ? Enum<ItemActivationKind>(kind) : null,
            null, null, null, null, null,
            OptionalString(row, "BaseUomCode"), OptionalBool(row, "LotControlled"), false);
    }

    private static ProductMasterStockableRow ReadProduct(BsonDocument row, ProductMasterRowKind kind) =>
        new(kind, row["_id"].AsBsonBinaryData.ToGuid(), row["GlobalProductId"].AsBsonBinaryData.ToGuid(),
            row["CanonicalCode"].AsString, null, null, null, null,
            Enum<ProductIdentityLifecycleStatus>(row["LifecycleStatus"]),
            row["ProductName"].AsString,
            decimal.Parse(row["PackQuantity"].ToString()!, NumberStyles.Number, CultureInfo.InvariantCulture),
            row["PackUomCode"].AsString,
            OptionalString(row, "MarketCode"),
            OptionalString(row, "BaseUomCode"),
            OptionalBool(row, "LotControlled"),
            OptionalBool(row, "SerialControlled"));

    private static T Enum<T>(BsonValue value) where T : struct, Enum
    {
        var parsed = value.IsString
            ? System.Enum.Parse<T>(value.AsString, ignoreCase: false)
            : (T)System.Enum.ToObject(typeof(T), value.ToInt32());
        return System.Enum.IsDefined(parsed) ? parsed : throw new FormatException($"{typeof(T).Name} {value} is not known.");
    }

    private static string? OptionalString(BsonDocument row, string name) =>
        row.TryGetValue(name, out var value) && !value.IsBsonNull ? value.AsString : null;

    private static bool? OptionalBool(BsonDocument row, string name) =>
        row.TryGetValue(name, out var value) && !value.IsBsonNull ? value.AsBoolean : null;
}

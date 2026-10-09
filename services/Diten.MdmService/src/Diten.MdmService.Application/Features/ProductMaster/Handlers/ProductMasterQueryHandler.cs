using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ItemMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductMaster.Queries;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using static Diten.MdmService.Application.Features.ProductMaster.ProductMasterContract;

namespace Diten.MdmService.Application.Features.ProductMaster.Handlers;

/// <summary>
/// MOD-0290-FU04 S4 — the eight product-master-bundle v1.1 operations (pack §16, §13, §8.5). Reads only: nothing here
/// writes and nothing is audited (AUD-001 §21: queries are not audited). A store that cannot be reached is a coded 503,
/// never a silent empty answer (§16.4, fail closed).
/// </summary>
public sealed class ProductMasterQueryHandler :
    IRequestHandler<GetProductMasterProductQuery, Response<JsonObject>>,
    IRequestHandler<GetProductMasterItemQuery, Response<JsonObject>>,
    IRequestHandler<GetProductMasterSkuQuery, Response<JsonObject>>,
    IRequestHandler<GetProductMasterSkuProductQuery, Response<JsonObject>>,
    IRequestHandler<GetProductMasterSkuUomQuery, Response<JsonObject>>,
    IRequestHandler<SearchProductMasterStockableItemsQuery, Response<JsonObject>>,
    IRequestHandler<BulkReadProductMasterSkusQuery, Response<JsonObject>>,
    IRequestHandler<ValidateProductMasterRefsQuery, Response<JsonObject>>
{
    private readonly IItemRepository _items;
    private readonly IGlobalProductRepository _globalProducts;
    private readonly IProductDefinitionRevisionRepository _revisions;
    private readonly IGskuRepository _gskus;
    private readonly ILskuRepository _lskus;
    private readonly IFinishedGoodRepository _finishedGoods;
    private readonly IProductMasterReadStore _store;
    private readonly IProductLegalEntityScopeRolloutStateRepository _rolloutStates;
    private readonly IProductLegalEntityScopePolicyRepository _policies;
    private readonly ProductLegalEntityScopeCandidateFacade _candidates;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ProductMasterQueryHandler> _logger;

    public ProductMasterQueryHandler(
        IItemRepository items,
        IGlobalProductRepository globalProducts,
        IProductDefinitionRevisionRepository revisions,
        IGskuRepository gskus,
        ILskuRepository lskus,
        IFinishedGoodRepository finishedGoods,
        IProductMasterReadStore store,
        IProductLegalEntityScopeRolloutStateRepository rolloutStates,
        IProductLegalEntityScopePolicyRepository policies,
        ProductLegalEntityScopeCandidateFacade candidates,
        ITenantContext tenantContext,
        ILogger<ProductMasterQueryHandler>? logger = null)
    {
        _items = items;
        _globalProducts = globalProducts;
        _revisions = revisions;
        _gskus = gskus;
        _lskus = lskus;
        _finishedGoods = finishedGoods;
        _store = store;
        _rolloutStates = rolloutStates;
        _policies = policies;
        _candidates = candidates;
        _tenantContext = tenantContext;
        _logger = logger ?? NullLogger<ProductMasterQueryHandler>.Instance;
    }

    // ------------------------------------------------------------------------------------------- getProduct / getMaterialItem

    // v1.0 getProduct answers the Global Product; v1.1 also the item (ids are UUIDs and never collide).
    public Task<Response<JsonObject>> Handle(GetProductMasterProductQuery query, CancellationToken cancellationToken) =>
        RunAsync(async reader =>
        {
            if (!TryParseId(query.ItemId, out var id)) return Fail(InvalidUuid, 400);
            if (await reader.ItemAsync(id, cancellationToken) is { } item) return Ok(ItemProductRef(item));
            if (await reader.GlobalProductAsync(id, cancellationToken) is not { } product) return Fail(UnknownItem, 404);
            var flags = await _store.GetLiveSkuFlagsOfGlobalProductAsync(product.Id, cancellationToken);
            return Ok(GlobalProductRef(product, TrueInDoubt(flags.GskuLots), TrueInDoubt(flags.ChildSerials)));
        }, cancellationToken, "getProduct");

    // The item shortcut: a Global Product id is 404 here.
    public Task<Response<JsonObject>> Handle(GetProductMasterItemQuery query, CancellationToken cancellationToken) =>
        RunAsync(async reader =>
        {
            if (!TryParseId(query.ItemId, out var id)) return Fail(InvalidUuid, 400);
            return await reader.ItemAsync(id, cancellationToken) is { } item
                ? Ok(ItemProductRef(item))
                : Fail(UnknownItem, 404);
        }, cancellationToken, "getMaterialItem");

    // ------------------------------------------------------------------------------------------- getSku / resolveSkuProduct

    // X-08 (draft.6): `level` is optional — see ResolveLevelAsync.
    public Task<Response<JsonObject>> Handle(GetProductMasterSkuQuery query, CancellationToken cancellationToken) =>
        RunAsync(async reader =>
        {
            if (!TryParseOptionalLevel(query.Level, out var asked)) return Fail(InvalidParameter, 400);
            if (!TryParseId(query.SkuId, out var id)) return Fail(InvalidUuid, 400);
            var level = await ResolveLevelAsync(reader, id, asked, cancellationToken);
            var sku = level is null ? null : await SkuRefAsync(reader, id, level, cancellationToken);
            return sku is null ? Fail(UnknownSku, 404) : Ok(sku);
        }, cancellationToken, "getSku");

    // D-SKU-LINK: an item links to itself; a GSKU / LSKU / Finished Good to its Global Product (FG → GSKU → GP, D-2).
    public Task<Response<JsonObject>> Handle(GetProductMasterSkuProductQuery query, CancellationToken cancellationToken) =>
        RunAsync(async reader =>
        {
            if (!TryParseOptionalLevel(query.Level, out var asked)) return Fail(InvalidParameter, 400);
            if (!TryParseId(query.SkuId, out var id)) return Fail(InvalidUuid, 400);
            var level = await ResolveLevelAsync(reader, id, asked, cancellationToken);
            var hit = level is null ? null : await HitAsync(reader, id, level, cancellationToken);
            return hit is null
                ? Fail(UnknownSku, 404)
                : Ok(SkuProductLink(id, hit.Level, hit.ItemId, hit.ItemKind));
        }, cancellationToken, "resolveSkuProduct");

    // ------------------------------------------------------------------------------------------- getSkuUom

    // Draft.5 / X-08: `level` is optional and read exactly as getSku reads it (ResolveLevelAsync).
    public Task<Response<JsonObject>> Handle(GetProductMasterSkuUomQuery query, CancellationToken cancellationToken) =>
        RunAsync(async reader =>
        {
            if (!TryParseOptionalLevel(query.Level, out var asked)) return Fail(InvalidParameter, 400);
            if (!TryParseId(query.SkuId, out var id)) return Fail(InvalidUuid, 400);
            switch (await ResolveLevelAsync(reader, id, asked, cancellationToken))
            {
                case LevelItem when await reader.ItemAsync(id, cancellationToken) is { } item:
                    return Ok(UomInfo(item.BaseUomCode, item.UomConversions));
                case LevelGsku when await reader.GskuAsync(id, cancellationToken) is { } gsku:
                    return Ok(UomInfo(gsku.Gsku.BaseUomCode, GskuConversions(gsku.Gsku)));
                // O-3: an LSKU's units are its GSKU's.
                case LevelLsku when await reader.LskuAsync(id, cancellationToken) is { } lsku:
                    return Ok(UomInfo(lsku.Gsku.BaseUomCode, GskuConversions(lsku.Gsku)));
                // The FG's own entries (case ↔ box) and the GSKU's chain (box ↔ pack unit), on the GSKU's base unit (O-14).
                case LevelFinishedGood when await reader.FinishedGoodAsync(id, cancellationToken) is { } finishedGood:
                    return Ok(UomInfo(finishedGood.Gsku.BaseUomCode,
                        (finishedGood.Record.UomConversions ?? []).Concat(GskuConversions(finishedGood.Gsku))));
                default:
                    return Fail(UnknownSku, 404);
            }
        }, cancellationToken, "getSkuUom");

    // ------------------------------------------------------------------------------------------- searchStockableItems

    // S4 FIX1 (R4): the filters, the product scope, the order and the page window run in the store (projected rows, the
    // policies read in the aggregation, no per-row query); here only the sources' windows are merged and cut. The window
    // is bounded: page × pageSize ≤ MaximumSearchWindow, past it 400 INVALID_PARAMETER (proposed to the contract).
    public Task<Response<JsonObject>> Handle(SearchProductMasterStockableItemsQuery query, CancellationToken cancellationToken) =>
        RunAsync(async reader =>
        {
            string? level = null;
            ItemMaterialType? materialType = null;
            if (!TryParseInt(query.Page, 1, out var page) || page is < 1 or > MaximumPage
                || !TryParseInt(query.PageSize, DefaultPageSize, out var pageSize) || pageSize is < 1 or > MaximumPageSize
                || (long)page * pageSize > MaximumSearchWindow
                || query.Q?.Length > MaximumQueryLength
                // draft.13 (S4 FIX2 R4): a control character (U+0000–U+001F) is not text — 400, never a store pattern.
                || query.Q is not null && query.Q.Any(char.IsControl)
                || query.SkuLevel is not null && !TryParseLevel(query.SkuLevel, out level)
                || query.MaterialType is not null && !TryParseMaterialType(query.MaterialType, out materialType)
                || query.Status is not null && !LifecycleStatuses.Contains(query.Status))
                return Fail(InvalidParameter, 400);

            // q is trimmed; only spaces = no text filter.
            var q = string.IsNullOrWhiteSpace(query.Q) ? null : query.Q.Trim();
            var itemsOnly = materialType is not null || level == LevelItem;
            ProductMasterProductScope? productScope = null;
            if (!itemsOnly)
            {
                // B1: the scope before any product table; Preparation shows every product row, Enforced its candidates,
                // any other rollout none.
                var scope = await reader.ScopeAsync(cancellationToken);
                productScope = scope.RolloutMode switch
                {
                    ProductLegalEntityScopeRolloutMode.Preparation => new(true, [], scope.ServerNowUtc),
                    ProductLegalEntityScopeRolloutMode.Enforced =>
                        new(false, scope.EffectiveCandidateLegalEntityIds, scope.ServerNowUtc.ToUniversalTime()),
                    _ => null
                };
            }
            var statuses = SearchStatuses.For(query.Status);
            var found = await _store.SearchStockableAsync(new ProductMasterStockableSearch(
                q,
                Items: level is null or LevelItem,
                Gskus: !itemsOnly && level is null or LevelGsku,
                Lskus: !itemsOnly && level is null or LevelLsku,
                FinishedGoods: !itemsOnly && level is null or LevelFinishedGood,
                materialType,
                statuses.Items,
                statuses.PendingFirstActivation,
                statuses.PendingReactivation,
                statuses.Products,
                productScope,
                page * pageSize), cancellationToken);
            // S4 FIX2 (R1): every skipped row by kind and id (never its content).
            foreach (var skipped in found.Unreadable)
                _logger.LogWarning(
                    "PRODUCT_MASTER_SEARCH_ROW_UNREADABLE: a {Kind} row ({Id}) could not be read and was skipped.",
                    skipped.Kind, skipped.Id);

            // Stable order: the canonical code (ordinal; codes are unique per kind), then the kind.
            var pageRows = found.Rows
                .OrderBy(x => x.CanonicalCode, StringComparer.Ordinal).ThenBy(x => x.Kind)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(row => (JsonNode?)StockableRow(row))
                .ToArray();
            // S4 FIX2 (R1): nothing readable but rows were there → the store cannot answer this page (a systematic read
            // failure must not look like an empty catalogue). At least one row read: 200, the skipped ones logged.
            if (pageRows.Length == 0 && found.Unreadable.Count > 0) return Fail(Unavailable, 503);
            return Ok(new JsonObject
            {
                ["items"] = new JsonArray(pageRows),
                ["page"] = page,
                ["pageSize"] = pageSize,
                ["totalCount"] = found.Total,
                ["contractVersion"] = ContractVersion
            });
        }, cancellationToken, "searchStockableItems");

    private static JsonObject StockableRow(ProductMasterStockableRow row)
    {
        if (row.Kind == ProductMasterRowKind.Item)
        {
            var status = Status(row.ItemStatus!.Value, row.ActivationKind);
            return StockableRow(row.SkuId, LevelItem, row.SkuId, KindItem, row.CanonicalCode, row.ItemName!,
                ItemRules.Code(row.MaterialType!.Value), status, row.BaseUomCode, row.LotControlled, false);
        }
        var level = row.Kind switch
        {
            ProductMasterRowKind.Gsku => LevelGsku,
            ProductMasterRowKind.Lsku => LevelLsku,
            _ => LevelFinishedGood
        };
        // O-5, the same composition getSku answers.
        var name = $"{row.ProductName} · {DecimalText(row.PackQuantity!.Value)} {row.PackUomCode}"
                   + (row.Kind == ProductMasterRowKind.Lsku ? $" · {row.MarketCode}" : string.Empty);
        return StockableRow(row.SkuId, level, row.ItemId, KindGlobalProduct, row.CanonicalCode, name, null,
            Status(row.ProductStatus!.Value), row.BaseUomCode, row.LotControlled,
            row.Kind == ProductMasterRowKind.Gsku ? null : row.SerialControlled);
    }

    /// <summary>§16.3 inverse: the stored statuses a published status filter means (none given: all but Retired).</summary>
    private sealed record SearchStatuses(
        IReadOnlyCollection<ItemLifecycleStatus> Items, bool PendingFirstActivation, bool PendingReactivation,
        IReadOnlyCollection<ProductIdentityLifecycleStatus> Products)
    {
        public static SearchStatuses For(string? published) => published switch
        {
            null => new([ItemLifecycleStatus.Draft, ItemLifecycleStatus.Active, ItemLifecycleStatus.Deactivated], true, true,
                [ProductIdentityLifecycleStatus.Draft, ProductIdentityLifecycleStatus.PendingIdentityApproval,
                    ProductIdentityLifecycleStatus.IdentityApproved]),
            "Draft" => new([ItemLifecycleStatus.Draft], true, false,
                [ProductIdentityLifecycleStatus.Draft, ProductIdentityLifecycleStatus.PendingIdentityApproval]),
            "Active" => new([ItemLifecycleStatus.Active], false, false, [ProductIdentityLifecycleStatus.IdentityApproved]),
            "Deactivated" => new([ItemLifecycleStatus.Deactivated], false, true, []),
            "Retired" => new([ItemLifecycleStatus.Retired], false, false, [ProductIdentityLifecycleStatus.Retired]),
            // Archived is never published (O-4): nothing has it.
            _ => new([], false, false, [])
        };
    }

    private static JsonObject StockableRow(Guid skuId, string level, Guid itemId, string kind, string code, string name,
        string? materialType, string status, string? baseUom, bool? lot, bool? serial) => new()
    {
        ["skuId"] = skuId.ToString("D"),
        ["skuLevel"] = level,
        ["itemId"] = itemId.ToString("D"),
        ["itemKind"] = kind,
        ["canonicalCode"] = code,
        ["name"] = name,
        ["materialType"] = materialType,
        ["lifecycleStatus"] = status,
        ["baseUomId"] = string.IsNullOrEmpty(baseUom) ? null : baseUom,
        ["lotControlled"] = lot,
        ["serialControlled"] = serial
    };

    // ------------------------------------------------------------------------------------------- bulkReadSkus

    // Up to 200 ids, answered in request order; Draft / Deactivated / Retired are found too; a Global Product id and
    // anything not found (another tenant's, out of scope) is in notFound. S4 FIX1 (R3, draft.10): an id sent twice is
    // answered once, where it first appears; each row carries its itemId and itemKind.
    public Task<Response<JsonObject>> Handle(BulkReadProductMasterSkusQuery query, CancellationToken cancellationToken) =>
        RunAsync(async reader =>
        {
            if (!TryParseObject(query.Body, out var body)) return Fail(InvalidParameter, 400);
            if (!body.TryGetProperty("skuIds", out var list) || list.ValueKind != JsonValueKind.Array)
                return Fail(InvalidParameter, 400);
            var count = list.GetArrayLength();
            if (count is 0 or > MaximumBatch) return Fail(BatchLimitExceeded, 400);
            var ids = new List<Guid>(count);
            foreach (var entry in list.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.String || !TryParseId(entry.GetString(), out var id))
                    return Fail(InvalidUuid, 400);
                ids.Add(id);
            }

            ids = ids.Distinct().ToList();
            await reader.PrefetchAsync(ids, cancellationToken);
            var items = new JsonArray();
            var notFound = new JsonArray();
            foreach (var id in ids)
            {
                var hit = await AnySkuAsync(reader, id, cancellationToken);
                if (hit is null)
                {
                    notFound.Add(id.ToString("D"));
                    continue;
                }
                items.Add(new JsonObject
                {
                    ["skuId"] = id.ToString("D"),
                    ["skuLevel"] = hit.Level,
                    ["itemId"] = hit.ItemId.ToString("D"),
                    ["itemKind"] = hit.ItemKind,
                    ["canonicalCode"] = hit.Code,
                    ["name"] = hit.Name,
                    ["lifecycleStatus"] = hit.Status
                });
            }
            return Ok(new JsonObject { ["items"] = items, ["notFound"] = notFound, ["contractVersion"] = ContractVersion });
        }, cancellationToken, "bulkReadSkus");

    // ------------------------------------------------------------------------------------------- validateRefs

    // Valid rows (draft.6): (a) itemId alone · (b) skuId + skuLevel · (b') skuId alone · (c') itemId + skuId · (c) all
    // three. A sku row resolves its level by the same rule as getSku (ResolveLevelAsync); a level or link mismatch is
    // exists: false; any other shape (skuLevel alone, an empty row) is 400 INVALID_REF; more than 200 refs is 400
    // BATCH_LIMIT_EXCEEDED.
    public Task<Response<JsonObject>> Handle(ValidateProductMasterRefsQuery query, CancellationToken cancellationToken) =>
        RunAsync(async reader =>
        {
            if (!TryParseObject(query.Body, out var body)
                || !body.TryGetProperty("refs", out var list) || list.ValueKind != JsonValueKind.Array)
                return Fail(InvalidRef, 400);
            // No ref, or more than 200: 400 (S4 FIX1 — an empty list is a request that asks nothing).
            if (list.GetArrayLength() is 0 or > MaximumBatch) return Fail(BatchLimitExceeded, 400);
            var refs = new List<(Guid? ItemId, Guid? SkuId, string? Level)>();
            foreach (var entry in list.EnumerateArray())
            {
                if (!TryReadRef(entry, out var reference)) return Fail(InvalidRef, 400);
                refs.Add(reference);
            }

            await reader.PrefetchAsync(
                refs.SelectMany(x => new[] { x.ItemId, x.SkuId }).OfType<Guid>().ToArray(), cancellationToken);
            var results = new JsonArray();
            foreach (var (itemId, skuId, level) in refs)
            {
                var result = new JsonObject();
                if (itemId is not null) result["itemId"] = itemId.Value.ToString("D");
                Hit? hit;
                if (skuId is not null)
                {
                    result["skuId"] = skuId.Value.ToString("D");
                    var resolved = await ResolveLevelAsync(reader, skuId.Value, level, cancellationToken);
                    hit = resolved is null ? null : await HitAsync(reader, skuId.Value, resolved, cancellationToken);
                    // (b') / (c'): the row carried no level — the resolved one is answered (when the SKU exists).
                    if (level is not null || hit is not null) result["skuLevel"] = level ?? hit!.Level;
                }
                else
                {
                    hit = await ItemIdHitAsync(reader, itemId!.Value, cancellationToken);
                }
                if (hit is not null && itemId is not null && skuId is not null && hit.ItemId != itemId.Value) hit = null;
                result["exists"] = hit is not null;
                if (hit is not null)
                {
                    result["itemKind"] = hit.ItemKind;
                    result["lifecycleStatus"] = hit.Status;
                }
                results.Add(result);
            }
            return Ok(new JsonObject { ["results"] = results, ["contractVersion"] = ContractVersion });
        }, cancellationToken, "validateRefs");

    private static bool TryReadRef(JsonElement entry, out (Guid? ItemId, Guid? SkuId, string? Level) reference)
    {
        reference = default;
        if (entry.ValueKind != JsonValueKind.Object) return false;
        Guid? itemId = null, skuId = null;
        string? level = null;
        foreach (var property in entry.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Null) continue;
            switch (property.Name)
            {
                case "itemId" when property.Value.ValueKind == JsonValueKind.String
                                   && TryParseId(property.Value.GetString(), out var item):
                    itemId = item;
                    break;
                case "skuId" when property.Value.ValueKind == JsonValueKind.String
                                  && TryParseId(property.Value.GetString(), out var sku):
                    skuId = sku;
                    break;
                case "skuLevel" when property.Value.ValueKind == JsonValueKind.String
                                     && TryParseLevel(property.Value.GetString(), out var parsed):
                    level = parsed;
                    break;
                default:
                    return false;
            }
        }
        // A row names a SKU (with or without its level, with or without the itemId it must link to) or an itemId alone.
        var valid = skuId is not null || itemId is not null && level is null;
        reference = (itemId, skuId, level);
        return valid;
    }

    // ------------------------------------------------------------------------------------------- resolution

    private sealed record Hit(string Level, Guid ItemId, string ItemKind, string Code, string Name, string Status);

    /// <summary>
    /// X-08 — the ONE level rule of getSku, getSkuUom, resolveSkuProduct and validateRefs: a given level must be the
    /// record's (else null → 404 / exists: false); without one the level is resolved from the id (SKU ids never collide
    /// across levels) and answered in <c>skuLevel</c>. Order rows carry no level and use this path.
    /// </summary>
    private static async Task<string?> ResolveLevelAsync(ProductMasterReader reader, Guid id, string? level, CancellationToken ct)
    {
        // The item first: a level-less item call is one item read, and never asks the product scope.
        IEnumerable<string> candidates = level is null ? LevelResolutionOrder : new[] { level };
        foreach (var candidate in candidates)
            if (await HitAsync(reader, id, candidate, ct) is not null)
                return candidate;
        return null;
    }

    // Absent is fine (X-08); present must be one of the SkuLevel values (an unknown request value is 400).
    private static bool TryParseOptionalLevel(string? text, out string? level)
    {
        level = null;
        if (text is null) return true;
        if (!TryParseLevel(text, out var parsed)) return false;
        level = parsed;
        return true;
    }

    private static async Task<JsonObject?> SkuRefAsync(ProductMasterReader reader, Guid id, string level, CancellationToken ct) =>
        level switch
        {
            LevelItem => await reader.ItemAsync(id, ct) is { } item ? ItemSkuRef(item) : null,
            LevelGsku => await reader.GskuAsync(id, ct) is { } gsku ? GskuSkuRef(gsku.Product, gsku.Gsku) : null,
            LevelLsku => await reader.LskuAsync(id, ct) is { } lsku ? LskuSkuRef(lsku.Product, lsku.Gsku, lsku.Record) : null,
            LevelFinishedGood => await reader.FinishedGoodAsync(id, ct) is { } fg
                ? FinishedGoodSkuRef(fg.Product, fg.Gsku, fg.Record)
                : null,
            _ => null
        };

    private static async Task<Hit?> HitAsync(ProductMasterReader reader, Guid id, string level, CancellationToken ct) =>
        level switch
        {
            LevelItem => await reader.ItemAsync(id, ct) is { } item
                ? new Hit(LevelItem, item.Id, KindItem, item.CanonicalCode, item.Name, Status(item))
                : null,
            LevelGsku => await reader.GskuAsync(id, ct) is { } gsku
                ? new Hit(LevelGsku, gsku.Product.Id, KindGlobalProduct, gsku.Gsku.CanonicalCode,
                    GskuName(gsku.Product, gsku.Gsku), Status(gsku.Gsku.LifecycleStatus))
                : null,
            LevelLsku => await reader.LskuAsync(id, ct) is { } lsku
                ? new Hit(LevelLsku, lsku.Product.Id, KindGlobalProduct, lsku.Record.CanonicalCode,
                    LskuName(lsku.Product, lsku.Gsku, lsku.Record), Status(lsku.Record.LifecycleStatus))
                : null,
            LevelFinishedGood => await reader.FinishedGoodAsync(id, ct) is { } fg
                ? new Hit(LevelFinishedGood, fg.Product.Id, KindGlobalProduct, fg.Record.CanonicalCode,
                    FinishedGoodName(fg.Product, fg.Gsku), Status(fg.Record.LifecycleStatus))
                : null,
            _ => null
        };

    // A bare id of any SKU kind (bulk read): an item, then the product SKU levels. A Global Product is not a SKU.
    private static async Task<Hit?> AnySkuAsync(ProductMasterReader reader, Guid id, CancellationToken ct)
    {
        foreach (var level in LevelResolutionOrder)
            if (await HitAsync(reader, id, level, ct) is { } hit)
                return hit;
        return null;
    }

    // `itemId` resolves either kind: the item itself or the Global Product.
    private static async Task<Hit?> ItemIdHitAsync(ProductMasterReader reader, Guid id, CancellationToken ct)
    {
        if (await reader.ItemAsync(id, ct) is { } item)
            return new Hit(LevelItem, item.Id, KindItem, item.CanonicalCode, item.Name, Status(item));
        return await reader.GlobalProductAsync(id, ct) is { } product
            ? new Hit(string.Empty, product.Id, KindGlobalProduct, product.CanonicalCode, product.GlobalProductName,
                Status(product.LifecycleStatus))
            : null;
    }

    // ------------------------------------------------------------------------------------------- plumbing

    private async Task<Response<JsonObject>> RunAsync(
        Func<ProductMasterReader, Task<Response<JsonObject>>> read, CancellationToken cancellationToken, string operation)
    {
        if (!_tenantContext.IsResolved || _tenantContext.TenantId == Guid.Empty) return Fail(TenantContextRequired, 400);
        var reader = new ProductMasterReader(_items, _globalProducts, _revisions, _gskus, _lskus, _finishedGoods, _store,
            new ProductLegalEntityScopeConsumerGuard(_rolloutStates, _policies, _candidates, _tenantContext));
        try
        {
            return await read(reader);
        }
        catch (ProductMasterRefusal refusal)
        {
            return Fail(refusal.Code, refusal.StatusCode);
        }
        // The store could not be reached (timeout, driver failure) or a stored record could not be read (S4 FIX2 M4: only
        // a DATA read failure — the driver's / serializer's own exceptions; a defect elsewhere stays a 500): a coded 503,
        // never an empty answer. A read cancelled by its own caller propagates.
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested && IsStoreReadFailure(exception))
        {
            _logger.LogWarning("PRODUCT_MASTER_READ_FAILED: {Code} on {Operation} — {Failure}.",
                Unavailable, operation, exception.GetType().FullName);
            return Fail(Unavailable, 503);
        }
    }

    /// <summary>
    /// A failure of the store or of a stored document: a timeout, the driver's MongoException family, the BSON library's
    /// BsonException family, or a FormatException / InvalidCastException the BSON library itself raised (a document the
    /// serializer cannot map). (The Application layer does not reference the driver: the families are recognised by name.)
    /// </summary>
    public static bool IsStoreReadFailure(Exception exception)
    {
        if (exception is TimeoutException) return true;
        for (var type = exception.GetType(); type is not null; type = type.BaseType)
            if (type.FullName is "MongoDB.Driver.MongoException" or "MongoDB.Bson.BsonException")
                return true;
        return exception is FormatException or InvalidCastException
               && exception.Source is { } source && source.StartsWith("MongoDB.", StringComparison.Ordinal);
    }

    private static Response<JsonObject> Ok(JsonObject body) => Response<JsonObject>.Success(body);

    private static Response<JsonObject> Fail(string code, int status) => Response<JsonObject>.Fail(code, status);

    private static bool TryParseLevel(string? text, out string level)
    {
        level = SkuLevels.FirstOrDefault(x => string.Equals(x, text, StringComparison.Ordinal)) ?? string.Empty;
        return level.Length > 0;
    }

    private static bool TryParseMaterialType(string text, out ItemMaterialType? type)
    {
        type = ItemRules.TryParseMaterialType(text, out var parsed) && ItemRules.Code(parsed) == text ? parsed : null;
        return type is not null;
    }

    private static bool TryParseInt(string? text, int fallback, out int value)
    {
        if (text is null)
        {
            value = fallback;
            return true;
        }
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseObject(string? text, out JsonElement body)
    {
        body = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return false;
            body = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

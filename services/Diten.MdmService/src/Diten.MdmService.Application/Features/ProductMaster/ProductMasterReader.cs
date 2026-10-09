using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;

namespace Diten.MdmService.Application.Features.ProductMaster;

/// <summary>
/// MOD-0290-FU04 S4 — one request's view of the product master. Items are tenant-wide (Decision 6); every product row
/// (Global Product, GSKU, LSKU, Finished Good) passes the product read guard the screens' reads use.
/// <para>
/// S4 FIX1 (B1) — the scope is resolved BEFORE any product table is read, never after a row is found: when the guard
/// cannot answer (403 / 503 …), an id that exists and an id that does not get the SAME answer, so the refusal tells
/// nothing about existence. Only an item hit stays independent of the product scope (an item is looked up first; the
/// scope is asked only when the id is not an item or the level is not Item). A row out of scope is "none"
/// (404 / notFound / exists: false, pack §8.5).
/// </para>
/// </summary>
public sealed class ProductMasterReader
{
    private readonly IItemRepository _items;
    private readonly IGlobalProductRepository _globalProducts;
    private readonly IProductDefinitionRevisionRepository _revisions;
    private readonly IGskuRepository _gskus;
    private readonly ILskuRepository _lskus;
    private readonly IFinishedGoodRepository _finishedGoods;
    private readonly IProductMasterReadStore _store;
    private readonly ProductLegalEntityScopeConsumerGuard _guard;

    private ProductLegalEntityScopeConsumerContext? _scope;
    private readonly Dictionary<Guid, bool> _allowed = [];
    private readonly Dictionary<Guid, Item?> _itemCache = [];
    private readonly Dictionary<Guid, GlobalProduct?> _productCache = [];
    private readonly Dictionary<Guid, ProductDefinitionRevision?> _revisionCache = [];
    private readonly Dictionary<Guid, Gsku?> _gskuCache = [];
    private readonly Dictionary<Guid, Lsku?> _lskuCache = [];
    private readonly Dictionary<Guid, FinishedGood?> _finishedGoodCache = [];

    public ProductMasterReader(
        IItemRepository items,
        IGlobalProductRepository globalProducts,
        IProductDefinitionRevisionRepository revisions,
        IGskuRepository gskus,
        ILskuRepository lskus,
        IFinishedGoodRepository finishedGoods,
        IProductMasterReadStore store,
        ProductLegalEntityScopeConsumerGuard guard)
    {
        _items = items;
        _globalProducts = globalProducts;
        _revisions = revisions;
        _gskus = gskus;
        _lskus = lskus;
        _finishedGoods = finishedGoods;
        _store = store;
        _guard = guard;
    }

    public IProductMasterReadStore Store => _store;

    // ------------------------------------------------------------------------------------------- scope

    /// <summary>
    /// The product scope of this request, resolved once and BEFORE any product table is read; a refusal stops the
    /// request (fail closed) with the same answer whatever the ids. The provider's 401 / 504 are an outage to the
    /// consumer (a 401 here is not "your token expired"): 503 PRODUCT_MASTER_UNAVAILABLE.
    /// </summary>
    public async Task<ProductLegalEntityScopeConsumerContext> ScopeAsync(CancellationToken cancellationToken)
    {
        if (_scope is not null) return _scope;
        var resolved = await _guard.ResolveContextAsync(ProductMasterContract.ReadPermission, cancellationToken);
        if (!resolved.IsSuccessful)
            throw resolved.StatusCode is 401 or 504
                ? new ProductMasterRefusal(ProductMasterContract.Unavailable, 503)
                : new ProductMasterRefusal(resolved.FailureCode!, resolved.StatusCode);
        return _scope = resolved.Context!;
    }

    private async Task<bool> InScopeAsync(Guid globalProductId, CancellationToken cancellationToken)
    {
        if (_allowed.TryGetValue(globalProductId, out var known)) return known;
        var decision = await _guard.EvaluateAsync(await ScopeAsync(cancellationToken), globalProductId, cancellationToken);
        return _allowed[globalProductId] = decision.Allowed;
    }

    // ------------------------------------------------------------------------------------------- single reads

    public async Task<Item?> ItemAsync(Guid id, CancellationToken cancellationToken)
    {
        if (_itemCache.TryGetValue(id, out var cached)) return cached;
        return _itemCache[id] = await _items.GetByIdAsync(id, cancellationToken);
    }

    /// <summary>The Global Product, or null when it does not exist or is out of the caller's scope.</summary>
    public async Task<GlobalProduct?> GlobalProductAsync(Guid id, CancellationToken cancellationToken)
    {
        await ScopeAsync(cancellationToken);
        if (!_productCache.TryGetValue(id, out var product))
            product = _productCache[id] = await _globalProducts.GetByIdAsync(id, cancellationToken);
        return product is not null && await InScopeAsync(product.Id, cancellationToken) ? product : null;
    }

    public async Task<ProductRow<Gsku>?> GskuAsync(Guid id, CancellationToken cancellationToken)
    {
        var gsku = await RawGskuAsync(id, cancellationToken);
        if (gsku is null) return null;
        var product = await ProductOfGskuAsync(gsku, cancellationToken);
        return product is null ? null : new(product, gsku, gsku);
    }

    public async Task<ProductRow<Lsku>?> LskuAsync(Guid id, CancellationToken cancellationToken)
    {
        await ScopeAsync(cancellationToken);
        if (!_lskuCache.TryGetValue(id, out var lsku))
            lsku = _lskuCache[id] = await _lskus.GetByIdAsync(id, cancellationToken);
        if (lsku is null) return null;
        var gsku = await RawGskuAsync(lsku.GskuId, cancellationToken);
        var product = gsku is null ? null : await ProductOfGskuAsync(gsku, cancellationToken);
        return product is null ? null : new(product, gsku!, lsku);
    }

    public async Task<ProductRow<FinishedGood>?> FinishedGoodAsync(Guid id, CancellationToken cancellationToken)
    {
        await ScopeAsync(cancellationToken);
        if (!_finishedGoodCache.TryGetValue(id, out var finishedGood))
            finishedGood = _finishedGoodCache[id] = await _finishedGoods.GetByIdAsync(id, cancellationToken);
        if (finishedGood is null) return null;
        var gsku = await RawGskuAsync(finishedGood.GskuId, cancellationToken);
        var product = gsku is null ? null : await ProductOfGskuAsync(gsku, cancellationToken);
        return product is null ? null : new(product, gsku!, finishedGood);
    }

    private async Task<Gsku?> RawGskuAsync(Guid id, CancellationToken cancellationToken)
    {
        await ScopeAsync(cancellationToken);
        if (_gskuCache.TryGetValue(id, out var cached)) return cached;
        return _gskuCache[id] = await _gskus.GetByIdAsync(id, cancellationToken);
    }

    private async Task<GlobalProduct?> ProductOfGskuAsync(Gsku gsku, CancellationToken cancellationToken)
    {
        if (!_revisionCache.TryGetValue(gsku.ProductDefinitionRevisionId, out var revision))
            revision = _revisionCache[gsku.ProductDefinitionRevisionId] =
                await _revisions.GetByIdAsync(gsku.ProductDefinitionRevisionId, cancellationToken);
        return revision is null ? null : await GlobalProductAsync(revision.GlobalProductId, cancellationToken);
    }

    // ------------------------------------------------------------------------------------------- batch warm-up

    /// <summary>
    /// Loads every record a batch may touch in a few queries, so the per-id answers come from memory. Items first and
    /// without the scope; when any id is not an item the scope is resolved (B1) before the product tables are read, and
    /// the scope of every Global Product reached is decided with ONE policy read (R4). Ids that match nothing are
    /// remembered as "none".
    /// </summary>
    public async Task PrefetchAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var distinct = ids.Distinct().ToArray();
        if (distinct.Length == 0) return;
        Remember(_itemCache, distinct, await _store.GetItemsByIdsAsync(distinct, cancellationToken), x => x.Id);
        var rest = distinct.Where(id => _itemCache[id] is null).ToArray();
        if (rest.Length == 0) return;

        var scope = await ScopeAsync(cancellationToken);
        Remember(_productCache, rest, await _globalProducts.GetByIdsAsync(rest, cancellationToken), x => x.Id);
        Remember(_gskuCache, rest, await _gskus.GetByIdsAsync(rest, cancellationToken), x => x.Id);
        Remember(_lskuCache, rest, await _store.GetLskusByIdsAsync(rest, cancellationToken), x => x.Id);
        Remember(_finishedGoodCache, rest, await _store.GetFinishedGoodsByIdsAsync(rest, cancellationToken), x => x.Id);
        var parents = _lskuCache.Values.OfType<Lsku>().Select(x => x.GskuId)
            .Concat(_finishedGoodCache.Values.OfType<FinishedGood>().Select(x => x.GskuId))
            .Where(id => !_gskuCache.ContainsKey(id)).Distinct().ToArray();
        if (parents.Length > 0)
            Remember(_gskuCache, parents, await _gskus.GetByIdsAsync(parents, cancellationToken), x => x.Id);
        var revisionIds = _gskuCache.Values.OfType<Gsku>().Select(x => x.ProductDefinitionRevisionId)
            .Where(id => !_revisionCache.ContainsKey(id)).Distinct().ToArray();
        if (revisionIds.Length > 0)
            Remember(_revisionCache, revisionIds, await _revisions.GetByIdsAsync(revisionIds, cancellationToken), x => x.Id);
        var productIds = _revisionCache.Values.OfType<ProductDefinitionRevision>().Select(x => x.GlobalProductId)
            .Where(id => !_productCache.ContainsKey(id)).Distinct().ToArray();
        if (productIds.Length > 0)
            Remember(_productCache, productIds, await _globalProducts.GetByIdsAsync(productIds, cancellationToken), x => x.Id);

        var undecided = _productCache.Values.OfType<GlobalProduct>().Select(x => x.Id)
            .Where(id => !_allowed.ContainsKey(id)).ToArray();
        if (undecided.Length > 0)
            foreach (var (id, decision) in await _guard.EvaluateManyAsync(scope, undecided, cancellationToken))
                _allowed[id] = decision.Allowed;
    }

    private static void Remember<T>(Dictionary<Guid, T?> cache, IEnumerable<Guid> asked, IEnumerable<T> found, Func<T, Guid> id)
        where T : class
    {
        var byId = found.GroupBy(id).ToDictionary(group => group.Key, group => group.First());
        foreach (var key in asked) cache[key] = byId.GetValueOrDefault(key);
    }
}

public sealed record ProductRow<T>(GlobalProduct Product, Gsku Gsku, T Record);

/// <summary>The request cannot be answered (the product scope refused or could not be read): coded, fail closed.</summary>
public sealed class ProductMasterRefusal(string code, int statusCode) : Exception(code)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}

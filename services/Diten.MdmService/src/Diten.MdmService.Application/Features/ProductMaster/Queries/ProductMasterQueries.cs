using System.Text.Json.Nodes;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductMaster.Queries;

// MOD-0290-FU04 S4: the product-master-bundle v1.1 reads. Every input arrives as text so that a malformed id, level
// or body answers the contract's own 400 / 404 (never a framework binding error).

public sealed record GetProductMasterProductQuery(string? ItemId) : IRequest<Response<JsonObject>>;

public sealed record GetProductMasterItemQuery(string? ItemId) : IRequest<Response<JsonObject>>;

public sealed record GetProductMasterSkuQuery(string? SkuId, string? Level) : IRequest<Response<JsonObject>>;

public sealed record GetProductMasterSkuProductQuery(string? SkuId, string? Level) : IRequest<Response<JsonObject>>;

public sealed record GetProductMasterSkuUomQuery(string? SkuId, string? Level = null) : IRequest<Response<JsonObject>>;

public sealed record SearchProductMasterStockableItemsQuery(
    string? Q, string? SkuLevel, string? MaterialType, string? Status, string? Page, string? PageSize)
    : IRequest<Response<JsonObject>>;

public sealed record BulkReadProductMasterSkusQuery(string? Body) : IRequest<Response<JsonObject>>;

public sealed record ValidateProductMasterRefsQuery(string? Body) : IRequest<Response<JsonObject>>;

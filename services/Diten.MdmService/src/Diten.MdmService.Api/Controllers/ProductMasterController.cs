using System.Text;
using System.Text.Json.Nodes;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Features.ProductMaster;
using Diten.MdmService.Application.Features.ProductMaster.Queries;
using Diten.MdmService.Infrastructure.Authorization;
using Diten.Shared.Core;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

/// <summary>
/// MOD-0290-FU04 S4 — product-master-bundle v1.1 (server URL <c>/api/product-master</c>): the five v1.0 reads, which
/// now answer the material item too, and the three v1.1 additions. The answer is the contract's bare JSON (not the MDM
/// envelope); an error is the contract's <c>Error</c> shape. A tenant user's token with
/// <c>mdm.product-master.read</c> (O-13); no service identity reads here (S4 measurement: Auth issues no such token).
/// </summary>
[Authorize]
[ApiController]
[Route("api/product-master")]
[HasPermission(ProductMasterContract.ReadPermission)]
public sealed class ProductMasterController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IRequestCorrelation _correlation;

    public ProductMasterController(IMediator mediator, IRequestCorrelation correlation)
    {
        _mediator = mediator;
        _correlation = correlation;
    }

    [HttpGet("products/{itemId}")]
    public async Task<IActionResult> GetProduct(string itemId, CancellationToken cancellationToken) =>
        Answer(await _mediator.Send(new GetProductMasterProductQuery(itemId), cancellationToken));

    [HttpGet("skus/{skuId}")]
    public async Task<IActionResult> GetSku(string skuId, [FromQuery] string? level, CancellationToken cancellationToken) =>
        Answer(await _mediator.Send(new GetProductMasterSkuQuery(skuId, level), cancellationToken));

    [HttpGet("skus/{skuId}/product")]
    public async Task<IActionResult> ResolveSkuProduct(
        string skuId, [FromQuery] string? level, CancellationToken cancellationToken) =>
        Answer(await _mediator.Send(new GetProductMasterSkuProductQuery(skuId, level), cancellationToken));

    [HttpGet("skus/{skuId}/uom")]
    public async Task<IActionResult> GetSkuUom(string skuId, [FromQuery] string? level, CancellationToken cancellationToken) =>
        Answer(await _mediator.Send(new GetProductMasterSkuUomQuery(skuId, level), cancellationToken));

    [HttpGet("items/{itemId}")]
    public async Task<IActionResult> GetMaterialItem(string itemId, CancellationToken cancellationToken) =>
        Answer(await _mediator.Send(new GetProductMasterItemQuery(itemId), cancellationToken));

    [HttpGet("stockable-items")]
    public async Task<IActionResult> SearchStockableItems(
        [FromQuery] string? q,
        [FromQuery] string? skuLevel,
        [FromQuery] string? materialType,
        [FromQuery] string? status,
        [FromQuery] string? page,
        [FromQuery] string? pageSize,
        CancellationToken cancellationToken) =>
        Answer(await _mediator.Send(
            new SearchProductMasterStockableItemsQuery(q, skuLevel, materialType, status, page, pageSize),
            cancellationToken));

    [HttpPost("skus/bulk-read")]
    public async Task<IActionResult> BulkReadSkus(CancellationToken cancellationToken) =>
        await BodyAsync(cancellationToken) is { } body
            ? Answer(await _mediator.Send(new BulkReadProductMasterSkusQuery(body), cancellationToken))
            : TooLarge();

    [HttpPost("validate")]
    public async Task<IActionResult> ValidateRefs(CancellationToken cancellationToken) =>
        await BodyAsync(cancellationToken) is { } body
            ? Answer(await _mediator.Send(new ValidateProductMasterRefsQuery(body), cancellationToken))
            : TooLarge();

    /// <summary>
    /// The body as text (a malformed one answers the contract's 400, not a framework binding error), read up to
    /// <see cref="ProductMasterContract.MaximumBodyBytes"/> — S4 FIX1 (R5): a declared or a streamed body past it is null
    /// (413), never read to the end into memory. Kestrel's own default (30 MB) is not the limit here.
    /// </summary>
    private async Task<string?> BodyAsync(CancellationToken cancellationToken)
    {
        if (Request.ContentLength > ProductMasterContract.MaximumBodyBytes) return null;
        // S4 FIX2 (M8): the 64 KB (+1) window is rented, not allocated per request.
        var limit = ProductMasterContract.MaximumBodyBytes + 1;
        var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(limit);
        try
        {
            var length = 0;
            int read;
            while (length < limit
                   && (read = await Request.Body.ReadAsync(buffer.AsMemory(length, limit - length), cancellationToken)) > 0)
                length += read;
            return length > ProductMasterContract.MaximumBodyBytes ? null : Encoding.UTF8.GetString(buffer, 0, length);
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private IActionResult TooLarge() => Json(StatusCodes.Status413PayloadTooLarge, ProductMasterContract.Error(
        ProductMasterContract.RequestTooLarge, ProductMasterContract.Message(ProductMasterContract.RequestTooLarge),
        _correlation.CorrelationId));

    private IActionResult Answer(Response<JsonObject> response)
    {
        if (response.IsSuccessful && response.Data is { } body)
            return Json(response.StatusCode, body);
        var code = response.Errors?.FirstOrDefault() ?? ProductMasterContract.Unavailable;
        var status = response.StatusCode is >= 400 and < 600 ? response.StatusCode : 500;
        return Json(status, ProductMasterContract.Error(code, ProductMasterContract.Message(code), _correlation.CorrelationId));
    }

    private static ContentResult Json(int status, JsonObject body) => new()
    {
        StatusCode = status,
        ContentType = "application/json; charset=utf-8",
        Content = body.ToJsonString()
    };
}

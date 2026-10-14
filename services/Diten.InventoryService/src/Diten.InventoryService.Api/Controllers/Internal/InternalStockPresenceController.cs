using Diten.InventoryService.Api.InternalAccess;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.StockPresence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.InventoryService.Api.Controllers.Internal;

/// <summary>
/// INVENTORY-BUNDLE 1.1.0 <c>getStockPresence</c> (WP-0173-PM2, MOD-0290 R-12) — service-to-service, on this service
/// directly; the Gateway has no route for <c>/api/internal/inventory/**</c> (route-coverage test). No user token: the
/// JWT authorization is not used (<see cref="AllowAnonymousAttribute"/>) and the scope middleware does not cover this base
/// path (it scopes only <c>/api/inventory</c>, <c>/api/locations</c>, <c>/api/trace</c>, <c>/api/quarantine</c>). The ONLY
/// gate is <see cref="InternalServiceKeyAttribute"/>. Read only; the one write is the audit row.
/// </summary>
[Route("api/internal/inventory")]
[AllowAnonymous]
public sealed class InternalStockPresenceController : CustomBaseController
{
    private readonly IMediator _mediator;

    public InternalStockPresenceController(IMediator mediator) => _mediator = mediator;

    [HttpGet("stock-presence")]
    [InternalServiceKey(StockPresenceContract.OperationId)]
    public async Task<IActionResult> GetStockPresence(CancellationToken cancellationToken)
    {
        var tenant = Request.Headers[InventoryContract.TenantHeader];
        var sku = Request.Query["skuId"];
        var level = Request.Query["skuLevel"];
        var key = (string)HttpContext.Items[InternalServiceKeyFilter.KeyIdentifierItem]!;
        return CreateActionResultInstance(await _mediator.Send(
            new GetStockPresenceQuery(
                key,
                tenant.Count == 1 ? tenant[0] : null,
                sku.Count == 1 ? sku[0] : null,
                level.Count == 1 ? level[0] : null),
            cancellationToken));
    }
}

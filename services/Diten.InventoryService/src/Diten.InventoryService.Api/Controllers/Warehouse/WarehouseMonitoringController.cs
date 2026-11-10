using Diten.InventoryService.Application.Features.WarehouseMonitoring.Queries;
using Diten.InventoryService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.InventoryService.Api.Controllers.Warehouse;

/// <summary>
/// WMS-BUNDLE v1 (draft §9) — MOD-0178 S3 monitoring reads under <c>/api/warehouse</c> (DL-7, pack §8.23): the exception box, the activity trace
/// and the integration monitor. Read-only (GET only, no body, no key); every action carries its own <c>warehouse.*</c> read key; the scope is the
/// token's tenant + the selected company (another company's or tenant's row never appears). No answer carries the ship-to (Q-10a). No business
/// rule here.
/// </summary>
[Route("api/warehouse")]
[Authorize]
public sealed class WarehouseMonitoringController : CustomBaseController
{
    private readonly IMediator _mediator;

    public WarehouseMonitoringController(IMediator mediator) => _mediator = mediator;

    [HttpGet("exceptions")]
    [HasPermission(WarehousePermissions.ExceptionsRead)]
    public async Task<IActionResult> Exceptions([FromQuery] string? kind, [FromQuery] string? warehouseId, [FromQuery] string? page, [FromQuery] string? pageSize,
        CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetWarehouseExceptionListQuery(kind, warehouseId, page, pageSize), ct));

    [HttpGet("activity")]
    [HasPermission(WarehousePermissions.ActivityRead)]
    public async Task<IActionResult> Activity([FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? warehouseId, [FromQuery] string? kind,
        [FromQuery] string? page, [FromQuery] string? pageSize, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetWarehouseActivityListQuery(from, to, warehouseId, kind, page, pageSize), ct));

    [HttpGet("integration/status")]
    [HasPermission(WarehousePermissions.IntegrationMonitorRead)]
    public async Task<IActionResult> IntegrationStatus(CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetWarehouseIntegrationStatusQuery(), ct));
}

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Diten.SupplyChainService.Application.Features.Shipments;
using Diten.SupplyChainService.Application.Features.Shipments.Commands;
using Diten.SupplyChainService.Application.Features.Shipments.Queries;
using Diten.SupplyChainService.Infrastructure.Authorization;
namespace Diten.SupplyChainService.Api.Controllers;
[Authorize]
[Route("api/shipment-bundle/shipments")]
public sealed class ShipmentsController(ISender sender) : CustomBaseController
{
    [HttpGet]
    [HasPermission(ShipmentPermissions.Read)]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? sourceDocumentId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
     => Wire(await sender.Send(new GetShipmentListQuery(status, sourceDocumentId, page, pageSize), ct));
    [HttpPost]
    [HasPermission(ShipmentPermissions.Create)]
    public async Task<IActionResult> Create(ShipmentModels.Create body, CancellationToken ct)
     => Wire(await sender.Send(new CreateShipmentCommand(body), ct));
    [HttpGet("{shipmentId:guid}")]
    [HasPermission(ShipmentPermissions.Read)]
    public async Task<IActionResult> Detail(Guid shipmentId, CancellationToken ct)
     => Wire(await sender.Send(new GetShipmentByIdQuery(shipmentId), ct));
    [HttpPost("{shipmentId:guid}/transition")]
    [HasPermission(ShipmentPermissions.Dispatch, ShipmentPermissions.Cancel)]
    public async Task<IActionResult> Transition(Guid shipmentId, ShipmentModels.Transition body, CancellationToken ct)
     => Wire(await sender.Send(new TransitionShipmentCommand(shipmentId, body), ct));
    [HttpPost("{shipmentId:guid}/pod")]
    [HasPermission(ShipmentPermissions.CapturePod)]
    public async Task<IActionResult> Pod(Guid shipmentId, ShipmentModels.Pod body, CancellationToken ct)
     => Wire(await sender.Send(new CapturePodCommand(shipmentId, body), ct));
}

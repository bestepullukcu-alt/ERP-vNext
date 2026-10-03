using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MediatR;
using Diten.SupplyChainService.Api.Controllers;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.Carriers;
using Diten.SupplyChainService.Application.Features.Carriers.Commands;
using Diten.SupplyChainService.Application.Features.Carriers.Queries;
using Diten.SupplyChainService.Infrastructure.Features.Carriers;
namespace Diten.SupplyChainService.Api.Features.Carriers;
[Authorize]
[Route("api/shipment-bundle/carriers")]
public sealed class CarriersController(ISender sender, CarrierRequestContext context, ILogger<CarriersController> logger) : CustomBaseController
{
    [HttpGet, CarrierPermission(CarrierPermissions.Read)]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct) => CarrierWire(await sender.Send(new GetCarrierListQuery(status), ct));
    [HttpPost, CarrierPermission(CarrierPermissions.Create)]
    public async Task<IActionResult> Create([FromBody] CreateCarrierRequest body, CancellationToken ct) => CarrierWire(await sender.Send(new CreateCarrierCommand(body), ct));
    [HttpPost("{carrierId}/status"), CarrierPermission(CarrierPermissions.ChangeStatus)]
    public async Task<IActionResult> Status(string carrierId, [FromBody] ChangeCarrierStatusRequest body, CancellationToken ct) =>
        CarrierWire(await sender.Send(new ChangeCarrierStatusCommand(Guid.Parse(carrierId), body), ct));
    private IActionResult CarrierWire<T>(Response<T> result)
    {
        logger.LogInformation("Carrier outcome {TenantId} {LegalEntityId} {CorrelationId} {Status} {ErrorCode} {IdempotentReplay}",
            context.Scope.TenantId, context.Scope.LegalEntityId, context.CorrelationId, result.StatusCode, result.ErrorCode,
            result.Data is CarrierResponse carrier && carrier.IdempotentReplay);
        var code = result.StatusCode == 500 ? "INTERNAL_ERROR" : result.ErrorCode;
        return StatusCode(result.StatusCode, code is null ? result.Data : CarrierContractError.Create(code, result.StatusCode, context.CorrelationId));
    }
}

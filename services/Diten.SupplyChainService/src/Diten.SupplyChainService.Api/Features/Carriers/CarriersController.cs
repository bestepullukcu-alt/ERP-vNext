using System.Diagnostics;
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
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct) => CarrierWire("queryCarriers", Stopwatch.GetTimestamp(), await sender.Send(new GetCarrierListQuery(status), ct));
    [HttpPost, CarrierPermission(CarrierPermissions.Create)]
    public async Task<IActionResult> Create([FromBody] CreateCarrierRequest body, CancellationToken ct) => CarrierWire("createCarrier", Stopwatch.GetTimestamp(), await sender.Send(new CreateCarrierCommand(body), ct));
    [HttpPost("{carrierId}/status"), CarrierPermission(CarrierPermissions.ChangeStatus)]
    public async Task<IActionResult> Status(string carrierId, [FromBody] ChangeCarrierStatusRequest body, CancellationToken ct) =>
        CarrierWire("changeCarrierStatus", Stopwatch.GetTimestamp(), await sender.Send(new ChangeCarrierStatusCommand(Guid.Parse(carrierId), body), ct));
    // MOD-0184 observability (pack :300-302): scoped operation, outcome, correlation, replay, and — for this module only —
    // list size and timings. The started timestamp is taken before the handler runs (arguments evaluate left to right).
    private IActionResult CarrierWire<T>(string operation, long started, Response<T> result)
    {
        logger.LogInformation("Carrier outcome {Operation} {TenantId} {LegalEntityId} {CorrelationId} {Status} {ErrorCode} {IdempotentReplay} {ListSize} {ElapsedMs}",
            operation, context.Scope.TenantId, context.Scope.LegalEntityId, context.CorrelationId, result.StatusCode, result.ErrorCode,
            result.Data is CarrierResponse carrier && carrier.IdempotentReplay, result.Data is CarrierListResponse list ? list.Items.Count : (int?)null,
            Math.Round(Stopwatch.GetElapsedTime(started).TotalMilliseconds, 1));
        var code = result.StatusCode == 500 ? "INTERNAL_ERROR" : result.ErrorCode;
        return StatusCode(result.StatusCode, code is null ? result.Data : CarrierContractError.Create(code, result.StatusCode, context.CorrelationId));
    }
}

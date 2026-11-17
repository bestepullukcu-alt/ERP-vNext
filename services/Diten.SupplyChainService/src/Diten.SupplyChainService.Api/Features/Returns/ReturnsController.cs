using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MediatR;
using Diten.SupplyChainService.Api.Controllers;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.Returns;
using Diten.SupplyChainService.Application.Features.Returns.Commands;
using Diten.SupplyChainService.Application.Features.Returns.Queries;
using Diten.SupplyChainService.Domain.Features.Returns;
using Diten.SupplyChainService.Infrastructure.Features.Returns;
namespace Diten.SupplyChainService.Api.Features.Returns;
[Authorize, Route("api/shipment-bundle/returns")]
public sealed class ReturnsController(ISender sender, ReturnRequestContext context, ILogger<ReturnsController> logger) : CustomBaseController
{
    [HttpGet, ReturnPermission(ReturnPermissions.Read)]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? shipmentId, CancellationToken ct) => ReturnWireResponse("queryReturns", await sender.Send(new GetReturnListQuery(status is null ? null : Enum.Parse<ReturnStatus>(status), shipmentId is null ? null : Guid.Parse(shipmentId)), ct));
    // SHIPMENT-BUNDLE 3.5.0 getReturn (MOD-0186 r10 §35.6, DL-4). A malformed id is 400, as on the other Returns operations.
    [HttpGet("{returnId}"), ReturnPermission(ReturnPermissions.Read)]
    public async Task<IActionResult> Get(string returnId, CancellationToken ct) =>
        Guid.TryParse(returnId, out var id) && ReturnWire.Uuid(returnId)
            ? ReturnWireResponse("getReturn", await sender.Send(new GetReturnByIdQuery(id), ct))
            : ReturnWireResponse("getReturn", Response<ReturnDetail>.Fail("INVALID_REQUEST", 400));
    [HttpPost, ReturnPermission(ReturnPermissions.Create)]
    public async Task<IActionResult> Create([FromBody] JsonElement body, CancellationToken ct) => ReturnWireResponse("createReturn", await sender.Send(new CreateReturnCommand(body), ct));
    [HttpPost("{returnId}/transition"), ReturnPermission(ReturnPermissions.Transition)]
    public async Task<IActionResult> Transition(string returnId, [FromBody] JsonElement body, CancellationToken ct) => ReturnWireResponse("transitionReturn", await sender.Send(new TransitionReturnCommand(Guid.Parse(returnId), body), ct));
    // Pack :437: logs carry operation/result/correlation/replay. The correlation (the Shipment root on mutations) is added by
    // CorrelationIdEnricher (Program.cs).
    private IActionResult ReturnWireResponse<T>(string operation, Response<T> response)
    {
        logger.LogInformation("Return outcome {Operation} {Status} {ErrorCode} {IdempotentReplay}",
            operation, response.StatusCode, response.ErrorCode, response.Data is ReturnResponse result && result.IdempotentReplay);
        return StatusCode(response.StatusCode, response.ErrorCode is null ? response.Data : ReturnContractError.Create(response.StatusCode == 500 ? "INTERNAL_ERROR" : response.ErrorCode, response.StatusCode, context.CorrelationId, response.StatusCode == 500 ? null : response.Details));
    }
}

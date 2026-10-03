using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MediatR;
using Diten.SupplyChainService.Api.Controllers;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.Claims;
using Diten.SupplyChainService.Application.Features.Claims.Commands;
using Diten.SupplyChainService.Application.Features.Claims.Queries;
using Diten.SupplyChainService.Infrastructure.Features.Claims;
namespace Diten.SupplyChainService.Api.Features.Claims;
[Authorize, Route("api/shipment-bundle/claims")]
public sealed class ClaimsController(ISender sender, ClaimRequestContext context) : CustomBaseController
{
    [HttpGet, ClaimPermission(ClaimPermissions.Read)]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? shipmentId, CancellationToken ct)
        => ClaimWireResponse(await sender.Send(new GetClaimListQuery(status, shipmentId), ct));
    [HttpPost, ClaimPermission(ClaimPermissions.Create)]
    public async Task<IActionResult> Create([FromBody] JsonElement body, CancellationToken ct)
        => ClaimWireResponse(await sender.Send(new CreateClaimCommand(body), ct));
    [HttpPost("{claimId}/transition"), ClaimPermission(ClaimPermissions.Create, ClaimPermissions.Investigate, ClaimPermissions.Decide, ClaimPermissions.Settle)]
    public async Task<IActionResult> Transition(string claimId, [FromBody] JsonElement body, CancellationToken ct)
        => ClaimWireResponse(await sender.Send(new TransitionClaimCommand(Guid.Parse(claimId), body), ct));
    private IActionResult ClaimWireResponse<T>(Response<T> result) => StatusCode(result.StatusCode,
        result.ErrorCode is null ? result.Data : ClaimContractError.Create(result.StatusCode == 500 ? "INTERNAL_ERROR" : result.ErrorCode, result.StatusCode, context.CorrelationId));
}

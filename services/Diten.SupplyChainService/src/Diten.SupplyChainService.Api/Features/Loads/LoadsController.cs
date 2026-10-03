using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MediatR;
using Diten.SupplyChainService.Api.Controllers;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.Loads;
using Diten.SupplyChainService.Application.Features.Loads.Commands;
using Diten.SupplyChainService.Application.Features.Loads.Queries;
using Diten.SupplyChainService.Infrastructure.Features.Loads;
namespace Diten.SupplyChainService.Api.Features.Loads;
[Authorize,Route("api/shipment-bundle/loads")]
public sealed class LoadsController(ISender sender,LoadRequestContext context):CustomBaseController
{
 [HttpGet,LoadPermission(LoadPermissions.Read)] public async Task<IActionResult> List([FromQuery]string? status,[FromQuery]string? carrierId,CancellationToken ct)=>LoadWireResponse(await sender.Send(new GetLoadListQuery(status,carrierId),ct));
 [HttpPost,LoadPermission(LoadPermissions.Create)] public async Task<IActionResult> Create([FromBody]JsonElement body,CancellationToken ct)=>LoadWireResponse(await sender.Send(new CreateLoadCommand(body),ct));
 [HttpPost("{loadId}/transition"),LoadPermission(LoadPermissions.Transition)] public async Task<IActionResult> Transition(string loadId,[FromBody]JsonElement body,CancellationToken ct)=>LoadWireResponse(await sender.Send(new TransitionLoadCommand(Guid.Parse(loadId),body),ct));
 private IActionResult LoadWireResponse<T>(Response<T> r)=>StatusCode(r.StatusCode,r.ErrorCode is null?r.Data:LoadContractError.Create(r.StatusCode==500?"INTERNAL_ERROR":r.ErrorCode,r.StatusCode,context.CorrelationId));
}

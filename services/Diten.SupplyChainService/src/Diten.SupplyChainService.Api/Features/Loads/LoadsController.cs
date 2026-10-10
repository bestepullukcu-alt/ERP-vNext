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
public sealed class LoadsController(ISender sender,LoadRequestContext context,ILogger<LoadsController> logger):CustomBaseController
{
 [HttpGet,LoadPermission(LoadPermissions.Read)] public async Task<IActionResult> List([FromQuery]string? status,[FromQuery]string? carrierId,CancellationToken ct)=>LoadWireResponse("queryLoads",await sender.Send(new GetLoadListQuery(status,carrierId),ct));
 [HttpPost,LoadPermission(LoadPermissions.Create)] public async Task<IActionResult> Create([FromBody]JsonElement body,CancellationToken ct)=>LoadWireResponse("createLoadPlan",await sender.Send(new CreateLoadCommand(body),ct));
 [HttpPost("{loadId}/transition"),LoadPermission(LoadPermissions.Transition)] public async Task<IActionResult> Transition(string loadId,[FromBody]JsonElement body,CancellationToken ct)=>LoadWireResponse("transitionLoad",await sender.Send(new TransitionLoadCommand(Guid.Parse(loadId),body),ct));
 // Pack :432: logs carry operation/result/correlation/replay. The correlation is added by CorrelationIdEnricher (Program.cs).
 private IActionResult LoadWireResponse<T>(string operation,Response<T> r)
 {
  logger.LogInformation("Load outcome {Operation} {Status} {ErrorCode} {IdempotentReplay}",operation,r.StatusCode,r.ErrorCode,r.Data is LoadResponse load&&load.IdempotentReplay);
  return StatusCode(r.StatusCode,r.ErrorCode is null?r.Data:LoadContractError.Create(r.StatusCode==500?"INTERNAL_ERROR":r.ErrorCode,r.StatusCode,context.CorrelationId));
 }
}

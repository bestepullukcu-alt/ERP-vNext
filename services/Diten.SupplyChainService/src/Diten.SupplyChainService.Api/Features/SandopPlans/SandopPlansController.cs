using System.Text.Json;using MediatR;using Microsoft.AspNetCore.Mvc;using Microsoft.AspNetCore.Authorization;
using Diten.SupplyChainService.Application.Features.SandopPlans;using Diten.SupplyChainService.Application.Features.SandopPlans.Commands;using Diten.SupplyChainService.Application.Features.SandopPlans.Queries;using Diten.SupplyChainService.Domain.Features.SandopPlans;using Diten.SupplyChainService.Infrastructure.Features.SandopPlans;
namespace Diten.SupplyChainService.Api.Features.SandopPlans;
[Authorize,Route("api/supply-chain/sandop-plans")]
public sealed class SandopPlansController(ISender sender):ControllerBase
{
 IActionResult Result(SandopResult r,Guid correlation){Response.Headers["X-Correlation-Id"]=correlation.ToString();return r.Code is null?new ContentResult{StatusCode=r.Status,ContentType="application/json",Content=r.Body}:StatusCode(r.Status,SandopContractError.Create(r.Code,correlation));}
 IActionResult Gate(SandopContextMiddleware.Resolution c)=>Result(SandopResult.Error(c.Status,c.ErrorCode!),c.Correlation);
 [HttpPost,SandopPermission(SandopPermissions.Create)] public async Task<IActionResult> Create([FromBody]JsonElement body,CancellationToken ct)
 {var c=SandopContextMiddleware.Resolve(HttpContext,SandopPermissions.Create,true);if(c.ErrorCode is not null)return Gate(c);return Result(await sender.Send(new CreateSandopPlanCommand(new(c.Scope,c.Key,c.Correlation),null,body),ct),c.Correlation);}
 [HttpGet("{sandopPlanId:guid}"),SandopPermission(SandopPermissions.Read)] public async Task<IActionResult> Get(Guid sandopPlanId,CancellationToken ct)
 {var c=SandopContextMiddleware.Resolve(HttpContext,SandopPermissions.Read,false);if(c.ErrorCode is not null)return Gate(c);return Result(await sender.Send(new GetSandopPlanQuery(new(c.Scope,c.Correlation),sandopPlanId),ct),c.Correlation);}
 [HttpPost("{sandopPlanId:guid}/snapshots"),SandopPermission(SandopPermissions.Capture)] public async Task<IActionResult> Capture(Guid sandopPlanId,[FromBody]JsonElement body,CancellationToken ct)
 {var c=SandopContextMiddleware.Resolve(HttpContext,SandopPermissions.Capture,true);if(c.ErrorCode is not null)return Gate(c);return Result(await sender.Send(new CaptureSandopSnapshotCommand(new(c.Scope,c.Key,c.Correlation),sandopPlanId,body),ct),c.Correlation);}
 [HttpGet("{sandopPlanId:guid}/snapshots"),SandopPermission(SandopPermissions.Read)] public async Task<IActionResult> ListSnapshots(Guid sandopPlanId,CancellationToken ct)
 {var c=SandopContextMiddleware.Resolve(HttpContext,SandopPermissions.Read,false);if(c.ErrorCode is not null)return Gate(c);return Result(await sender.Send(new ListSandopSnapshotsQuery(new(c.Scope,c.Correlation),sandopPlanId),ct),c.Correlation);}
 [HttpPost("{sandopPlanId:guid}/sign-offs"),SandopPermission(SandopPermissions.SignOff)] public async Task<IActionResult> SignOff(Guid sandopPlanId,[FromBody]JsonElement body,CancellationToken ct)
 {var c=SandopContextMiddleware.Resolve(HttpContext,SandopPermissions.SignOff,true);if(c.ErrorCode is not null)return Gate(c);return Result(await sender.Send(new RecordSandopSignOffCommand(new(c.Scope,c.Key,c.Correlation),sandopPlanId,body),ct),c.Correlation);}
 [HttpGet("{sandopPlanId:guid}/sign-offs"),SandopPermission(SandopPermissions.Read)] public async Task<IActionResult> ListSignOffs(Guid sandopPlanId,CancellationToken ct)
 {var c=SandopContextMiddleware.Resolve(HttpContext,SandopPermissions.Read,false);if(c.ErrorCode is not null)return Gate(c);return Result(await sender.Send(new ListSandopSignOffsQuery(new(c.Scope,c.Correlation),sandopPlanId),ct),c.Correlation);}
}

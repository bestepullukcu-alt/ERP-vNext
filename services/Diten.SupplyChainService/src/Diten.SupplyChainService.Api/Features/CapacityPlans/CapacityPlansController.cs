using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using Diten.SupplyChainService.Api.Controllers;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.CapacityPlans;
using Diten.SupplyChainService.Application.Features.CapacityPlans.Commands;
using Diten.SupplyChainService.Application.Features.CapacityPlans.Queries;
using Diten.SupplyChainService.Infrastructure.Features.CapacityPlans;
namespace Diten.SupplyChainService.Api.Features.CapacityPlans;
[Authorize]
[Route("api/supply-chain/capacity-plans")]
public sealed class CapacityPlansController(ISender sender,CapacityRequestContext context) : CustomBaseController
{
    [HttpPost,CapacityPermission(CapacityPermissions.Create)]
    public async Task<IActionResult> Create([FromBody] CreateCapacityPlanRequest body,CancellationToken ct) =>
        CapacityWire(await sender.Send(new CreateCapacityPlanCommand(body),ct));
    [HttpGet("{capacityPlanId}"),CapacityPermission(CapacityPermissions.Read)]
    public async Task<IActionResult> GetPlan(Guid capacityPlanId,CancellationToken ct) =>
        CapacityWire(await sender.Send(new GetCapacityPlanQuery(capacityPlanId),ct));
    [HttpPost("{capacityPlanId}/scenarios"),CapacityPermission(CapacityPermissions.ScenarioCreate)]
    public async Task<IActionResult> CreateScenario(Guid capacityPlanId,[FromBody] CreateCapacityScenarioRequest body,CancellationToken ct) =>
        CapacityWire(await sender.Send(new CreateCapacityScenarioCommand(capacityPlanId,body),ct));
    [HttpGet("{capacityPlanId}/scenarios/{scenarioId}"),CapacityPermission(CapacityPermissions.Read)]
    public async Task<IActionResult> GetScenario(Guid capacityPlanId,Guid scenarioId,CancellationToken ct) =>
        CapacityWire(await sender.Send(new GetCapacityScenarioQuery(capacityPlanId,scenarioId),ct));
    [HttpPost("{capacityPlanId}/scenarios/{scenarioId}/evaluations"),CapacityPermission(CapacityPermissions.Evaluate)]
    public async Task<IActionResult> Evaluate(Guid capacityPlanId,Guid scenarioId,[FromBody] EvaluateCapacityScenarioRequest body,CancellationToken ct) =>
        CapacityWire(await sender.Send(new EvaluateCapacityScenarioCommand(capacityPlanId,scenarioId,body),ct));
    [HttpGet("{capacityPlanId}/evaluations/{evaluationId}"),CapacityPermission(CapacityPermissions.Read)]
    public async Task<IActionResult> GetEvaluation(Guid capacityPlanId,Guid evaluationId,CancellationToken ct) =>
        CapacityWire(await sender.Send(new GetCapacityEvaluationQuery(capacityPlanId,evaluationId),ct));
    private IActionResult CapacityWire<T>(Response<T> result) =>
        StatusCode(result.StatusCode,result.ErrorCode is null ? result.Data : CapacityContractError.Create(result.ErrorCode,result.StatusCode,context.CorrelationId));
}

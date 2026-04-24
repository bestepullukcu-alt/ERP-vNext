using Asp.Versioning;
using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Shared;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using Diten.WebAPI.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Diten.WebAPI.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/enterprise-strategy/planning")]
public sealed class EnterpriseStrategyPlanningController : EnterpriseStrategyApiControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContextAccessor _correlation;

    public EnterpriseStrategyPlanningController(IMediator mediator, ICorrelationContextAccessor correlation)
    {
        _mediator = mediator;
        _correlation = correlation;
    }

    [HttpGet("cycles")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.PlanningCycleView)]
    public async Task<ActionResult<Response<IReadOnlyList<PlanningCycleDto>>>> ListPlanningCycles([FromQuery] string? search, [FromQuery] string? status, CancellationToken ct)
        => HandleResult(await _mediator.Send(new ListPlanningCyclesQuery { Search = search, Status = status }, ct), _correlation.CorrelationId);

    [HttpGet("cycles/{planningCycleId}")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.PlanningCycleView)]
    public async Task<ActionResult<Response<PlanningCycleDto>>> GetPlanningCycle(string planningCycleId, CancellationToken ct)
        => HandleResult(await _mediator.Send(new GetPlanningCycleByIdQuery { PlanningCycleId = planningCycleId }, ct), _correlation.CorrelationId);

    [HttpPost("cycles")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.PlanningCycleCreate)]
    public async Task<ActionResult<Response<PlanningCycleDto>>> CreatePlanningCycle([FromBody] PlanningCycleDto body, CancellationToken ct)
        => HandleResult(await _mediator.Send(new CreatePlanningCycleCommand
        {
            PlanningCycle = body,
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct), _correlation.CorrelationId);

    [HttpPut("cycles/{planningCycleId}")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.PlanningCycleCreate)]
    public async Task<ActionResult<Response<PlanningCycleDto>>> UpdatePlanningCycle(string planningCycleId, [FromBody] PlanningCycleDto body, CancellationToken ct)
        => HandleResult(await _mediator.Send(new UpdatePlanningCycleCommand
        {
            PlanningCycleId = planningCycleId,
            PlanningCycle = body,
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct), _correlation.CorrelationId);

    [HttpPatch("cycles/{planningCycleId}/status")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.PlanningCycleCreate)]
    public async Task<ActionResult<Response<PlanningCycleDto>>> ChangePlanningCycleStatus(string planningCycleId, [FromBody] StatusChangeRequestDto body, CancellationToken ct)
        => HandleResult(await _mediator.Send(new ChangePlanningCycleStatusCommand
        {
            PlanningCycleId = planningCycleId,
            Status = body.Status,
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct), _correlation.CorrelationId);

    [HttpGet("strategy-periods")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.StrategyPeriodView)]
    public async Task<ActionResult<Response<IReadOnlyList<StrategyPeriodDto>>>> ListStrategyPeriods([FromQuery] string? planningCycleId, [FromQuery] string? search, [FromQuery] string? status, CancellationToken ct)
        => HandleResult(await _mediator.Send(new ListStrategyPeriodsQuery { PlanningCycleId = planningCycleId, Search = search, Status = status }, ct), _correlation.CorrelationId);

    [HttpGet("strategy-periods/{strategyPeriodId}")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.StrategyPeriodView)]
    public async Task<ActionResult<Response<StrategyPeriodDto>>> GetStrategyPeriod(string strategyPeriodId, CancellationToken ct)
        => HandleResult(await _mediator.Send(new GetStrategyPeriodByIdQuery { StrategyPeriodId = strategyPeriodId }, ct), _correlation.CorrelationId);

    [HttpPost("strategy-periods")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.StrategyPeriodCreate)]
    public async Task<ActionResult<Response<StrategyPeriodDto>>> CreateStrategyPeriod([FromBody] StrategyPeriodDto body, CancellationToken ct)
        => HandleResult(await _mediator.Send(new CreateStrategyPeriodCommand
        {
            StrategyPeriod = body,
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct), _correlation.CorrelationId);

    [HttpPut("strategy-periods/{strategyPeriodId}")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.StrategyPeriodCreate)]
    public async Task<ActionResult<Response<StrategyPeriodDto>>> UpdateStrategyPeriod(string strategyPeriodId, [FromBody] StrategyPeriodDto body, CancellationToken ct)
        => HandleResult(await _mediator.Send(new UpdateStrategyPeriodCommand
        {
            StrategyPeriodId = strategyPeriodId,
            StrategyPeriod = body,
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct), _correlation.CorrelationId);

    [HttpPatch("strategy-periods/{strategyPeriodId}/status")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.StrategyPeriodCreate)]
    public async Task<ActionResult<Response<StrategyPeriodDto>>> ChangeStrategyPeriodStatus(string strategyPeriodId, [FromBody] StatusChangeRequestDto body, CancellationToken ct)
        => HandleResult(await _mediator.Send(new ChangeStrategyPeriodStatusCommand
        {
            StrategyPeriodId = strategyPeriodId,
            Status = body.Status,
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct), _correlation.CorrelationId);

    [HttpGet("strategy-periods/default")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.StrategyPeriodView)]
    public async Task<ActionResult<Response<StrategyPeriodDto>>> ResolveDefaultStrategyPeriod(
        [FromQuery] string companyId,
        [FromQuery] string? businessUnitId,
        [FromQuery] string? regionId,
        CancellationToken ct)
        => HandleResult(await _mediator.Send(new ResolveDefaultStrategyPeriodQuery
        {
            CompanyId = companyId,
            BusinessUnitId = businessUnitId,
            RegionId = regionId
        }, ct), _correlation.CorrelationId);

    [HttpGet("strategy-periods/{id}/usage-summary")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.StrategyPeriodView)]
    public async Task<ActionResult<Response<StrategyPeriodUsageSummaryDto>>> GetStrategyPeriodUsageSummary(string id, CancellationToken ct)
        => HandleResult(await _mediator.Send(new GetStrategyPeriodUsageSummaryQuery { StrategyPeriodId = id }, ct), _correlation.CorrelationId);
}

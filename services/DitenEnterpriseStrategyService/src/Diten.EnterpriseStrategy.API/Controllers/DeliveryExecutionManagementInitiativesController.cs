using Asp.Versioning;
using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.DeliveryExecutionManagement.Shared;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Shared;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using Diten.WebAPI.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Diten.WebAPI.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/delivery-execution/initiatives")]
// Legacy compatibility aliases during the ES&BP -> Delivery transition.
[Route("api/v{version:apiVersion}/delivery-execution-management/initiatives")]
[Route("api/v{version:apiVersion}/enterprise-strategy/initiatives")]
public sealed class DeliveryExecutionManagementInitiativesController : EnterpriseStrategyApiControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContextAccessor _correlation;

    public DeliveryExecutionManagementInitiativesController(IMediator mediator, ICorrelationContextAccessor correlation)
    {
        _mediator = mediator;
        _correlation = correlation;
    }

    [HttpGet]
    [DeliveryExecutionManagementPermission(DeliveryExecutionManagementPermissions.InitiativeView)]
    public async Task<ActionResult<Response<PagedResponseDto<InitiativeStrategyLinkViewDto>>>> List([FromQuery] PagedRequestDto request, CancellationToken ct)
        => HandleResult(await _mediator.Send(new ListInitiativesQuery { Request = request }, ct), _correlation.CorrelationId);

    [HttpGet("{initiativeId}")]
    [DeliveryExecutionManagementPermission(DeliveryExecutionManagementPermissions.InitiativeView)]
    public async Task<ActionResult<Response<InitiativeDetailDto>>> Get(string initiativeId, CancellationToken ct)
        => HandleResult(await _mediator.Send(new GetInitiativeByIdQuery { InitiativeId = initiativeId }, ct), _correlation.CorrelationId);

    [HttpPost]
    [DeliveryExecutionManagementPermission(DeliveryExecutionManagementPermissions.InitiativeLink)]
    public async Task<ActionResult<Response<InitiativeStrategyLinkViewDto>>> Create([FromBody] InitiativeStrategyLinkViewDto body, CancellationToken ct)
        => HandleResult(await _mediator.Send(new CreateInitiativeCommand
        {
            Initiative = body,
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct), _correlation.CorrelationId);

    [HttpPut("{initiativeId}")]
    [DeliveryExecutionManagementPermission(DeliveryExecutionManagementPermissions.InitiativeLink)]
    public async Task<ActionResult<Response<InitiativeStrategyLinkViewDto>>> Update(string initiativeId, [FromBody] InitiativeStrategyLinkViewDto body, [FromHeader(Name = "If-Match")] int expectedVersion, CancellationToken ct)
        => HandleResult(await _mediator.Send(new UpdateInitiativeCommand
        {
            InitiativeId = initiativeId,
            Initiative = body,
            ExpectedVersion = expectedVersion,
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct), _correlation.CorrelationId);

    [HttpPut("{initiativeId}/strategy-link")]
    [DeliveryExecutionManagementPermission(DeliveryExecutionManagementPermissions.InitiativeLink)]
    public async Task<ActionResult<Response<InitiativeStrategyLinkViewDto>>> UpsertLink(string initiativeId, [FromBody] InitiativeStrategyLinkViewDto body, [FromHeader(Name = "If-Match")] int expectedVersion, CancellationToken ct)
        => HandleResult(await _mediator.Send(new UpsertInitiativeStrategyLinkCommand
        {
            InitiativeId = initiativeId,
            Link = body,
            ExpectedVersion = expectedVersion,
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct), _correlation.CorrelationId);

    [HttpPatch("{initiativeId}/strategy-link/status")]
    [DeliveryExecutionManagementPermission(DeliveryExecutionManagementPermissions.InitiativeLink)]
    public async Task<ActionResult<Response<InitiativeStrategyLinkViewDto>>> ChangeLinkStatus(string initiativeId, [FromBody] StatusChangeRequestDto body, CancellationToken ct)
        => HandleResult(await _mediator.Send(new ChangeInitiativeStrategyLinkStatusCommand
        {
            InitiativeId = initiativeId,
            Status = body.Status,
            ExpectedVersion = body.ExpectedVersion,
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct), _correlation.CorrelationId);

    [HttpDelete("{initiativeId}/strategy-link")]
    [DeliveryExecutionManagementPermission(DeliveryExecutionManagementPermissions.InitiativeUnlink)]
    public async Task<ActionResult<Response<bool>>> DeleteLink(string initiativeId, CancellationToken ct)
        => HandleResult(await _mediator.Send(new DeleteInitiativeStrategyLinkCommand
        {
            InitiativeId = initiativeId,
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct), _correlation.CorrelationId);

    [HttpPost("sync")]
    [DeliveryExecutionManagementPermission(DeliveryExecutionManagementPermissions.InitiativeSync)]
    public async Task<ActionResult<Response<SyncResultDto>>> Sync(CancellationToken ct)
        => HandleResult(await _mediator.Send(new SyncInitiativesCommand
        {
            CorrelationId = _correlation.CorrelationId,
            Actor = User?.Identity?.Name ?? "anonymous"
        }, ct), _correlation.CorrelationId);

    [HttpGet("{initiativeId}/projects")]
    [DeliveryExecutionManagementPermission(DeliveryExecutionManagementPermissions.ProjectView)]
    public async Task<ActionResult<Response<IReadOnlyList<ProjectStrategyLinkViewDto>>>> Projects(string initiativeId, CancellationToken ct)
        => HandleResult(await _mediator.Send(new GetInitiativeProjectsQuery { InitiativeId = initiativeId }, ct), _correlation.CorrelationId);

    [HttpGet("{initiativeId}/traceability")]
    [DeliveryExecutionManagementPermission(DeliveryExecutionManagementPermissions.InitiativeView)]
    public async Task<ActionResult<Response<string>>> Traceability(string initiativeId, CancellationToken ct)
        => HandleResult(await _mediator.Send(new GetInitiativeTraceabilityQuery { InitiativeId = initiativeId }, ct), _correlation.CorrelationId);
}

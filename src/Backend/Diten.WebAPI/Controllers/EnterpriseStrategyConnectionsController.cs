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
[Route("api/v{version:apiVersion}/enterprise-strategy/connections")]
public sealed class EnterpriseStrategyConnectionsController : EnterpriseStrategyApiControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContextAccessor _correlation;

    public EnterpriseStrategyConnectionsController(IMediator mediator, ICorrelationContextAccessor correlation)
    {
        _mediator = mediator;
        _correlation = correlation;
    }

    [HttpGet]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.ConnectionView)]
    public async Task<ActionResult<Response<PagedResponseDto<StrategyConnectionDto>>>> List([FromQuery] PagedRequestDto request, CancellationToken ct)
        => HandleResult(await _mediator.Send(new ListConnectionsQuery { Request = request }, ct), _correlation.CorrelationId);

    [HttpPost]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.ConnectionCreate)]
    public async Task<ActionResult<Response<StrategyConnectionDto>>> Create([FromBody] StrategyConnectionDto body, CancellationToken ct)
        => HandleResult(await _mediator.Send(new CreateConnectionCommand
        {
            Connection = body,
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct), _correlation.CorrelationId);

    [HttpGet("{connectionId}")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.ConnectionView)]
    public async Task<ActionResult<Response<StrategyConnectionDto>>> Get(string connectionId, CancellationToken ct)
        => HandleResult(await _mediator.Send(new GetConnectionByIdQuery { ConnectionId = connectionId }, ct), _correlation.CorrelationId);

    [HttpPut("{connectionId}")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.ConnectionEdit)]
    public async Task<ActionResult<Response<StrategyConnectionDto>>> Update(string connectionId, [FromBody] StrategyConnectionDto body, [FromHeader(Name = "If-Match")] int expectedVersion, CancellationToken ct)
        => HandleResult(await _mediator.Send(new UpdateConnectionCommand
        {
            ConnectionId = connectionId,
            Connection = body,
            ExpectedVersion = expectedVersion,
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct), _correlation.CorrelationId);

    [HttpPatch("{connectionId}/status")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.ConnectionEdit)]
    public async Task<ActionResult<Response<StrategyConnectionDto>>> ChangeStatus(string connectionId, [FromBody] StatusChangeRequestDto body, CancellationToken ct)
        => HandleResult(await _mediator.Send(new ChangeConnectionStatusCommand
        {
            ConnectionId = connectionId,
            Status = body.Status,
            ExpectedVersion = body.ExpectedVersion,
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct), _correlation.CorrelationId);

    [HttpDelete("{connectionId}")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.ConnectionDelete)]
    public async Task<ActionResult<Response<bool>>> Delete(string connectionId, CancellationToken ct)
        => HandleResult(await _mediator.Send(new DeleteConnectionCommand { ConnectionId = connectionId }, ct), _correlation.CorrelationId);

    [HttpGet("tree")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.ConnectionView)]
    public async Task<ActionResult<Response<IReadOnlyList<ConnectionTreeNodeDto>>>> Tree(CancellationToken ct)
        => HandleResult(await _mediator.Send(new GetConnectionTreeQuery(), ct), _correlation.CorrelationId);

    [HttpGet("graph")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.ConnectionView)]
    public async Task<ActionResult<Response<ConnectionGraphViewDto>>> Graph(CancellationToken ct)
        => HandleResult(await _mediator.Send(new GetConnectionGraphQuery(), ct), _correlation.CorrelationId);

    [HttpGet("matrix")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.ConnectionView)]
    public async Task<ActionResult<Response<IReadOnlyList<ConnectionMatrixCellDto>>>> Matrix([FromQuery] string mode, CancellationToken ct)
        => HandleResult(await _mediator.Send(new GetConnectionMatrixQuery { Mode = mode }, ct), _correlation.CorrelationId);

    [HttpGet("coverage-gaps")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.ConnectionView)]
    public async Task<ActionResult<Response<IReadOnlyList<CoverageGapDto>>>> CoverageGaps(CancellationToken ct)
        => HandleResult(await _mediator.Send(new GetConnectionCoverageGapsQuery(), ct), _correlation.CorrelationId);

    [HttpPost("validate-graph")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.ConnectionValidate)]
    public async Task<ActionResult<Response<ConnectionGraphViewDto>>> ValidateGraph(CancellationToken ct)
        => HandleResult(await _mediator.Send(new ValidateConnectionGraphQuery(), ct), _correlation.CorrelationId);
}

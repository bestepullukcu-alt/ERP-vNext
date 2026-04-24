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
[Route("api/v{version:apiVersion}/enterprise-strategy/objectives")]
public sealed class EnterpriseStrategyObjectivesController : EnterpriseStrategyApiControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContextAccessor _correlation;

    public EnterpriseStrategyObjectivesController(IMediator mediator, ICorrelationContextAccessor correlation)
    {
        _mediator = mediator;
        _correlation = correlation;
    }

    [HttpGet]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.ObjectiveView)]
    public async Task<ActionResult<Response<PagedResponseDto<ObjectiveDto>>>> List([FromQuery] PagedRequestDto request, CancellationToken ct)
        => HandleResult(await _mediator.Send(new ListObjectivesQuery { Request = request }, ct), _correlation.CorrelationId);

    [HttpPost]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.ObjectiveCreate)]
    public async Task<ActionResult<Response<ObjectiveDto>>> Create([FromBody] ObjectiveDto body, CancellationToken ct)
        => HandleResult(await _mediator.Send(new CreateObjectiveCommand
        {
            Objective = body,
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct), _correlation.CorrelationId);

    [HttpGet("{objectiveId}")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.ObjectiveView)]
    public async Task<ActionResult<Response<ObjectiveDetailDto>>> Get(string objectiveId, CancellationToken ct)
        => HandleResult(await _mediator.Send(new GetObjectiveByIdQuery { ObjectiveId = objectiveId }, ct), _correlation.CorrelationId);

    [HttpPut("{objectiveId}")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.ObjectiveEdit)]
    public async Task<ActionResult<Response<ObjectiveDto>>> Update(string objectiveId, [FromBody] ObjectiveDto body, [FromHeader(Name = "If-Match")] int expectedVersion, CancellationToken ct)
        => HandleResult(await _mediator.Send(new UpdateObjectiveCommand
        {
            ObjectiveId = objectiveId,
            Objective = body,
            ExpectedVersion = expectedVersion,
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct), _correlation.CorrelationId);

    [HttpPatch("{objectiveId}/status")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.ObjectiveActivate)]
    public async Task<ActionResult<Response<ObjectiveDto>>> ChangeStatus(string objectiveId, [FromBody] StatusChangeRequestDto body, CancellationToken ct)
        => HandleResult(await _mediator.Send(new ChangeObjectiveStatusCommand
        {
            ObjectiveId = objectiveId,
            Status = body.Status,
            ExpectedVersion = body.ExpectedVersion,
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct), _correlation.CorrelationId);

    [HttpPost("{objectiveId}/archive")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.ObjectiveArchive)]
    public async Task<ActionResult<Response<ObjectiveDto>>> Archive(string objectiveId, [FromBody] MutationMetadataDto body, CancellationToken ct)
        => HandleResult(await _mediator.Send(new ArchiveObjectiveCommand
        {
            ObjectiveId = objectiveId,
            ExpectedVersion = body.ExpectedVersion,
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct), _correlation.CorrelationId);

    [HttpPost("{objectiveId}/restore")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.ObjectiveEdit)]
    public async Task<ActionResult<Response<ObjectiveDto>>> Restore(string objectiveId, [FromBody] MutationMetadataDto body, CancellationToken ct)
        => HandleResult(await _mediator.Send(new RestoreObjectiveCommand
        {
            ObjectiveId = objectiveId,
            ExpectedVersion = body.ExpectedVersion,
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct), _correlation.CorrelationId);

    [HttpGet("{objectiveId}/initiatives")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.ObjectiveView)]
    public async Task<ActionResult<Response<IReadOnlyList<InitiativeStrategyLinkViewDto>>>> Initiatives(string objectiveId, CancellationToken ct)
        => HandleResult(await _mediator.Send(new GetObjectiveInitiativesQuery { ObjectiveId = objectiveId }, ct), _correlation.CorrelationId);

    [HttpGet("{objectiveId}/projects")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.ObjectiveView)]
    public async Task<ActionResult<Response<IReadOnlyList<ProjectStrategyLinkViewDto>>>> Projects(string objectiveId, CancellationToken ct)
        => HandleResult(await _mediator.Send(new GetObjectiveProjectsQuery { ObjectiveId = objectiveId }, ct), _correlation.CorrelationId);

    [HttpGet("{objectiveId}/alignment-summary")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.ObjectiveView)]
    public async Task<ActionResult<Response<ObjectiveAlignmentSummaryDto>>> AlignmentSummary(string objectiveId, CancellationToken ct)
        => HandleResult(await _mediator.Send(new GetObjectiveAlignmentSummaryQuery { ObjectiveId = objectiveId }, ct), _correlation.CorrelationId);
}

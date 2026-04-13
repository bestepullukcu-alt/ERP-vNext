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
[Route("api/v{version:apiVersion}/delivery-execution/projects")]
// Legacy compatibility aliases during the ES&BP -> Delivery transition.
[Route("api/v{version:apiVersion}/delivery-execution-management/projects")]
[Route("api/v{version:apiVersion}/enterprise-strategy/projects")]
public sealed class DeliveryExecutionManagementProjectsController : EnterpriseStrategyApiControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContextAccessor _correlation;

    public DeliveryExecutionManagementProjectsController(IMediator mediator, ICorrelationContextAccessor correlation)
    {
        _mediator = mediator;
        _correlation = correlation;
    }

    [HttpGet]
    [DeliveryExecutionManagementPermission(DeliveryExecutionManagementPermissions.ProjectView)]
    public async Task<ActionResult<Response<PagedResponseDto<ProjectStrategyLinkViewDto>>>> List([FromQuery] PagedRequestDto request, CancellationToken ct)
        => HandleResult(await _mediator.Send(new ListProjectsQuery { Request = request }, ct), _correlation.CorrelationId);

    [HttpGet("{projectId}")]
    [DeliveryExecutionManagementPermission(DeliveryExecutionManagementPermissions.ProjectView)]
    public async Task<ActionResult<Response<ProjectDetailDto>>> Get(string projectId, CancellationToken ct)
        => HandleResult(await _mediator.Send(new GetProjectByIdQuery { ProjectId = projectId }, ct), _correlation.CorrelationId);

    [HttpGet("templates/compatible")]
    [DeliveryExecutionManagementPermission(DeliveryExecutionManagementPermissions.ProjectView)]
    public async Task<ActionResult<Response<IReadOnlyList<ProjectCreationTemplateDto>>>> CompatibleTemplates([FromQuery] string parentType, [FromQuery] string entityScope, CancellationToken ct)
        => HandleResult(await _mediator.Send(new GetCompatibleProjectTemplatesQuery { ParentType = parentType, EntityScope = entityScope }, ct), _correlation.CorrelationId);

    [HttpGet("{projectId}/audit-trail")]
    [DeliveryExecutionManagementPermission(DeliveryExecutionManagementPermissions.ProjectView)]
    public async Task<ActionResult<Response<IReadOnlyList<EnterpriseStrategyAuditEventDto>>>> AuditTrail(string projectId, CancellationToken ct)
        => HandleResult(await _mediator.Send(new GetProjectAuditTrailQuery { ProjectId = projectId }, ct), _correlation.CorrelationId);

    [HttpPost]
    [DeliveryExecutionManagementPermission(DeliveryExecutionManagementPermissions.ProjectLink)]
    public async Task<ActionResult<Response<ProjectStrategyLinkViewDto>>> Create([FromBody] ProjectStrategyLinkViewDto body, CancellationToken ct)
        => HandleResult(await _mediator.Send(new CreateProjectCommand { Project = body, Actor = User?.Identity?.Name ?? "anonymous", CorrelationId = _correlation.CorrelationId }, ct), _correlation.CorrelationId);

    [HttpPut("{projectId}")]
    [DeliveryExecutionManagementPermission(DeliveryExecutionManagementPermissions.ProjectLink)]
    public async Task<ActionResult<Response<ProjectStrategyLinkViewDto>>> Update(string projectId, [FromBody] ProjectStrategyLinkViewDto body, [FromHeader(Name = "If-Match")] int expectedVersion, CancellationToken ct)
        => HandleResult(await _mediator.Send(new UpdateProjectCommand { ProjectId = projectId, Project = body, ExpectedVersion = expectedVersion, Actor = User?.Identity?.Name ?? "anonymous", CorrelationId = _correlation.CorrelationId }, ct), _correlation.CorrelationId);

    [HttpPut("{projectId}/strategy-link")]
    [DeliveryExecutionManagementPermission(DeliveryExecutionManagementPermissions.ProjectLink)]
    public async Task<ActionResult<Response<ProjectStrategyLinkViewDto>>> UpsertLink(string projectId, [FromBody] ProjectStrategyLinkViewDto body, [FromHeader(Name = "If-Match")] int expectedVersion, CancellationToken ct)
        => HandleResult(await _mediator.Send(new UpsertProjectStrategyLinkCommand
        {
            ProjectId = projectId,
            Link = body,
            ExpectedVersion = expectedVersion,
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct), _correlation.CorrelationId);

    [HttpPatch("{projectId}/strategy-link/status")]
    [DeliveryExecutionManagementPermission(DeliveryExecutionManagementPermissions.ProjectLink)]
    public async Task<ActionResult<Response<ProjectStrategyLinkViewDto>>> ChangeLinkStatus(string projectId, [FromBody] StatusChangeRequestDto body, CancellationToken ct)
        => HandleResult(await _mediator.Send(new ChangeProjectStrategyLinkStatusCommand
        {
            ProjectId = projectId,
            Status = body.Status,
            ExpectedVersion = body.ExpectedVersion,
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct), _correlation.CorrelationId);

    [HttpDelete("{projectId}/strategy-link")]
    [DeliveryExecutionManagementPermission(DeliveryExecutionManagementPermissions.ProjectUnlink)]
    public async Task<ActionResult<Response<bool>>> DeleteLink(string projectId, CancellationToken ct)
        => HandleResult(await _mediator.Send(new DeleteProjectStrategyLinkCommand
        {
            ProjectId = projectId,
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct), _correlation.CorrelationId);

    [HttpPost("strategy-linked")]
    [DeliveryExecutionManagementPermission(DeliveryExecutionManagementPermissions.ProjectLink)]
    public async Task<ActionResult<Response<ProjectStrategyLinkViewDto>>> CreateStrategyLinked(
        [FromBody] CreateStrategyLinkedProjectCommand command, CancellationToken ct)
    {
        command.Actor = User?.Identity?.Name ?? "anonymous";
        command.CorrelationId = _correlation.CorrelationId;
        return HandleResult(await _mediator.Send(command, ct), _correlation.CorrelationId);
    }

    [HttpPost("sync")]
    [DeliveryExecutionManagementPermission(DeliveryExecutionManagementPermissions.ProjectSync)]
    public async Task<ActionResult<Response<SyncResultDto>>> Sync(CancellationToken ct)
        => HandleResult(await _mediator.Send(new SyncProjectsCommand
        {
            CorrelationId = _correlation.CorrelationId,
            Actor = User?.Identity?.Name ?? "anonymous"
        }, ct), _correlation.CorrelationId);

    [HttpGet("{projectId}/traceability")]
    [DeliveryExecutionManagementPermission(DeliveryExecutionManagementPermissions.ProjectView)]
    public async Task<ActionResult<Response<string>>> Traceability(string projectId, CancellationToken ct)
        => HandleResult(await _mediator.Send(new GetProjectTraceabilityQuery { ProjectId = projectId }, ct), _correlation.CorrelationId);

    [HttpGet("{projectId}/upstream-lineage")]
    [DeliveryExecutionManagementPermission(DeliveryExecutionManagementPermissions.ProjectView)]
    public async Task<ActionResult<Response<string>>> UpstreamLineage(string projectId, CancellationToken ct)
        => HandleResult(await _mediator.Send(new GetProjectUpstreamLineageQuery { ProjectId = projectId }, ct), _correlation.CorrelationId);
}

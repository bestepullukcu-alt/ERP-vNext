using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Application.Features.Workflow.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

[ApiController]
[Route("api/platform/tenants/{tenantId:guid}/workflow/definitions")]
[Authorize(Policy = "PlatformAdminOnly")]
public sealed class PlatformTenantWorkflowDefinitionsController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly IPlatformTenantWorkflowDefinitionRequestExecutor _executor;
    private readonly ICorrelationContext _correlationContext;

    public PlatformTenantWorkflowDefinitionsController(
        IMediator mediator,
        IPlatformTenantWorkflowDefinitionRequestExecutor executor,
        ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _executor = executor;
        _correlationContext = correlationContext;
    }

    [HttpPost]
    [HasPermission(WorkflowPermissions.DefinitionsManage)]
    public Task<IActionResult> Create(Guid tenantId, [FromBody] CreateWorkflowDefinitionRequest request, CancellationToken ct) =>
        Execute(tenantId, ct, token => _mediator.Send(new CreateWorkflowDefinitionCommand(request, CorrelationId), token));

    [HttpGet]
    [HasPermission(WorkflowPermissions.DefinitionsView)]
    public Task<IActionResult> GetList(Guid tenantId, CancellationToken ct) =>
        Execute(tenantId, ct, token => _mediator.Send(new GetWorkflowDefinitionListQuery(CorrelationId), token));

    [HttpGet("{id:guid}")]
    [HasPermission(WorkflowPermissions.DefinitionsView)]
    public Task<IActionResult> GetById(Guid tenantId, Guid id, CancellationToken ct) =>
        Execute(tenantId, ct, token => _mediator.Send(new GetWorkflowDefinitionByIdQuery(id, CorrelationId), token));

    [HttpGet("{id:guid}/versions")]
    [HasPermission(WorkflowPermissions.DefinitionsView)]
    public Task<IActionResult> GetVersions(Guid tenantId, Guid id, CancellationToken ct) =>
        Execute(tenantId, ct, token => _mediator.Send(new GetWorkflowDefinitionVersionsQuery(id, CorrelationId), token));

    [HttpGet("{id:guid}/versions/{versionId:guid}")]
    [HasPermission(WorkflowPermissions.DefinitionsView)]
    public Task<IActionResult> GetVersionById(Guid tenantId, Guid id, Guid versionId, CancellationToken ct) =>
        Execute(tenantId, ct, token => _mediator.Send(
            new GetWorkflowDefinitionVersionByIdQuery(id, versionId, CorrelationId), token));

    [HttpPost("{id:guid}/publish")]
    [HasPermission(WorkflowPermissions.DefinitionsPublish)]
    public Task<IActionResult> Publish(
        Guid tenantId,
        Guid id,
        [FromBody] PublishWorkflowDefinitionRequest request,
        CancellationToken ct) =>
        Execute(tenantId, ct, token => _mediator.Send(
            new PublishWorkflowDefinitionCommand(id, request, CorrelationId), token));

    private Task<IActionResult> Execute<T>(
        Guid tenantId,
        CancellationToken ct,
        Func<CancellationToken, Task<Response<T>>> action) =>
        _executor.ExecuteAsync(
            HttpContext,
            tenantId,
            ct,
            async token => CreateActionResultInstance(await action(token)),
            Failure);

    private IActionResult Failure(int statusCode, string reasonCode) =>
        CreateActionResultInstance(Response<NoContent>.Fail(reasonCode, statusCode, reasonCode, CorrelationId));

    private string CorrelationId => string.IsNullOrWhiteSpace(_correlationContext.CorrelationId)
        ? HttpContext.TraceIdentifier
        : _correlationContext.CorrelationId!;
}

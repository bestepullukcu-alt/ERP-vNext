using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.TenantOrganization.Queries;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Application.Features.Workflow.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

// MOD-0023 — thin controller for workflow definitions and runtime instance reads. Task transitions are
// owned by later batches. The route stays version-explicit under api/v1/workflow; gateway routing is a
// separate integration-agent task.
[ApiController]
[Route("api/v1/workflow")]
[Authorize]
public sealed class WorkflowDefinitionsController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public WorkflowDefinitionsController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    [HttpPost("definitions")]
    [HasPermission(WorkflowPermissions.DefinitionsManage)]
    public async Task<IActionResult> Create([FromBody] CreateWorkflowDefinitionRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateWorkflowDefinitionCommand(request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("definitions")]
    [HasPermission(WorkflowPermissions.DefinitionsView)]
    public async Task<IActionResult> GetList(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetWorkflowDefinitionListQuery(CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("definitions/{id:guid}")]
    [HasPermission(WorkflowPermissions.DefinitionsView)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetWorkflowDefinitionByIdQuery(id, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("definitions/{id:guid}/publish")]
    [HasPermission(WorkflowPermissions.DefinitionsPublish)]
    public async Task<IActionResult> Publish(
        Guid id,
        [FromBody] PublishWorkflowDefinitionRequest request,
        CancellationToken ct)
    {
        var response = await _mediator.Send(new PublishWorkflowDefinitionCommand(id, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("definitions/{id:guid}/versions")]
    [HasPermission(WorkflowPermissions.DefinitionsView)]
    public async Task<IActionResult> GetVersions(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetWorkflowDefinitionVersionsQuery(id, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("definitions/{id:guid}/versions/{versionId:guid}")]
    [HasPermission(WorkflowPermissions.DefinitionsView)]
    public async Task<IActionResult> GetVersionById(Guid id, Guid versionId, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new GetWorkflowDefinitionVersionByIdQuery(id, versionId, CorrelationId),
            ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("instances")]
    [HasPermission(WorkflowPermissions.InstancesStart)]
    public async Task<IActionResult> StartInstance([FromBody] StartWorkflowInstanceRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new StartWorkflowInstanceCommand(request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("instances/{id:guid}")]
    [HasPermission(WorkflowPermissions.InstancesView)]
    public async Task<IActionResult> GetInstanceById(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetWorkflowInstanceByIdQuery(id, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("instances")]
    [HasPermission(WorkflowPermissions.InstancesView)]
    public async Task<IActionResult> GetInstances(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetWorkflowInstanceListQuery(CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("tasks")]
    [HasPermission(WorkflowPermissions.InstancesView)]
    public async Task<IActionResult> GetTasks(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetWorkflowTaskListQuery(CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    // MOD-0023 — the current user's actionable tasks (WorkCenter inbox foundation).
    [HttpGet("tasks/mine")]
    [HasPermission(WorkflowPermissions.InstancesView)]
    public async Task<IActionResult> GetMyTasks(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetMyWorkflowTasksQuery(CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("lookups/positions")]
    [HasPermission(WorkflowPermissions.DefinitionsView)]
    public async Task<IActionResult> GetPositionLookup(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetPositionsQuery(), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("transitions/evaluate")]
    [HasPermission(WorkflowPermissions.TransitionsEvaluate)]
    public async Task<IActionResult> EvaluateTransitionGate(
        [FromBody] EvaluateWorkflowTransitionGateRequest request,
        CancellationToken ct)
    {
        var response = await _mediator.Send(new EvaluateWorkflowTransitionGateQuery(request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("sla-rules")]
    [HasPermission(WorkflowPermissions.EscalationsManage)]
    public async Task<IActionResult> CreateSlaRule(
        [FromBody] CreateSlaEscalationRuleRequest request,
        CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateSlaEscalationRuleCommand(request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("sla-rules")]
    [HasPermission(WorkflowPermissions.EscalationsManage)]
    public async Task<IActionResult> GetSlaRules([FromQuery] Guid? templateId, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetSlaEscalationRulesQuery(templateId, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("escalations/run")]
    [HasPermission(WorkflowPermissions.EscalationsRun)]
    public async Task<IActionResult> RunEscalations(
        [FromBody] RunWorkflowEscalationsRequest request,
        CancellationToken ct)
    {
        var response = await _mediator.Send(new RunWorkflowEscalationsCommand(request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("tasks/{taskId:guid}/approve")]
    [HasPermission(WorkflowPermissions.TasksApprove)]
    public async Task<IActionResult> ApproveTask(
        Guid taskId,
        [FromBody] ApproveWorkflowTaskRequest request,
        CancellationToken ct)
    {
        var response = await _mediator.Send(new ApproveWorkflowTaskCommand(taskId, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("tasks/{taskId:guid}/reject")]
    [HasPermission(WorkflowPermissions.TasksReject)]
    public async Task<IActionResult> RejectTask(
        Guid taskId,
        [FromBody] RejectWorkflowTaskRequest request,
        CancellationToken ct)
    {
        var response = await _mediator.Send(new RejectWorkflowTaskCommand(taskId, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("tasks/{taskId:guid}/delegate")]
    [HasPermission(WorkflowPermissions.TasksDelegate)]
    public async Task<IActionResult> DelegateTask(
        Guid taskId,
        [FromBody] DelegateWorkflowTaskRequest request,
        CancellationToken ct)
    {
        var response = await _mediator.Send(new DelegateWorkflowTaskCommand(taskId, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("tasks/{taskId:guid}/request-info")]
    [HasPermission(WorkflowPermissions.TasksRequestInfo)]
    public async Task<IActionResult> RequestInfoTask(
        Guid taskId,
        [FromBody] RequestInfoWorkflowTaskRequest request,
        CancellationToken ct)
    {
        var response = await _mediator.Send(new RequestInfoWorkflowTaskCommand(taskId, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("tasks/{taskId:guid}/cancel")]
    [HasPermission(WorkflowPermissions.TasksCancel)]
    public async Task<IActionResult> CancelTask(
        Guid taskId,
        [FromBody] CancelWorkflowTaskRequest request,
        CancellationToken ct)
    {
        var response = await _mediator.Send(new CancelWorkflowTaskCommand(taskId, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId)
            ? HttpContext.TraceIdentifier
            : _correlationContext.CorrelationId!;
}

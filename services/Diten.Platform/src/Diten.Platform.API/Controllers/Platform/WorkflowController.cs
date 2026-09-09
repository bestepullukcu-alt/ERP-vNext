using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.WorkflowDesigner;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.WorkflowDesigner.Commands;
using Diten.Platform.Application.Features.WorkflowDesigner.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

/// <summary>
/// MOD-0023 Workflow Designer — versioned approval workflow definitions, runtime instances,
/// approval-task inbox and run-history timeline. Approvals-focused (no BPMN). Approve/reject/delegate
/// actions are RBAC-gated and written to the MOD-0021 audit trail via the audit pipeline behavior.
/// </summary>
[ApiController]
[Route("api/platform/workflow")]
[Authorize(Policy = "PlatformActor")]
public sealed class WorkflowController : CustomBaseController
{
    private readonly IMediator _mediator;

    public WorkflowController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ---- Definitions ----

    [HttpGet("definitions")]
    [HasPermission("platform.workflow.read")]
    public async Task<IActionResult> ListDefinitions(
        [FromQuery(Name = "search")] string? search,
        [FromQuery(Name = "status")] string? status,
        [FromQuery(Name = "page")] int page = 1,
        [FromQuery(Name = "page_size")] int pageSize = 25,
        CancellationToken ct = default)
        => CreateActionResultInstance(await _mediator.Send(new GetWorkflowDefinitionsQuery(search, status, page, pageSize), ct));

    [HttpGet("definitions/{id:guid}")]
    [HasPermission("platform.workflow.read")]
    public async Task<IActionResult> GetDefinition(Guid id, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetWorkflowDefinitionByIdQuery(id), ct));

    [HttpPost("definitions")]
    [HasPermission("platform.workflow.create")]
    public async Task<IActionResult> CreateDefinition([FromBody] CreateWorkflowDefinitionRequest request, CancellationToken ct)
    {
        var steps = request.Steps.Select(s => new WorkflowStepInput(s.Name, s.ApproverRole, s.RequiresEvidence)).ToList();
        var rules = request.SlaRules.Select(r => new SlaRuleInput(r.StepSequence, r.SlaHours, r.EscalationAction, r.EscalateToRole)).ToList();
        return CreateActionResultInstance(await _mediator.Send(
            new CreateWorkflowDefinitionCommand(request.Code, request.Name, request.Description, steps, rules), ct));
    }

    [HttpPatch("definitions/{id:guid}")]
    [HasPermission("platform.workflow.update")]
    public async Task<IActionResult> UpdateDefinition(Guid id, [FromBody] UpdateWorkflowDefinitionRequest request, CancellationToken ct)
    {
        var steps = request.Steps.Select(s => new WorkflowStepInput(s.Name, s.ApproverRole, s.RequiresEvidence)).ToList();
        var rules = request.SlaRules.Select(r => new SlaRuleInput(r.StepSequence, r.SlaHours, r.EscalationAction, r.EscalateToRole)).ToList();
        return CreateActionResultInstance(await _mediator.Send(
            new UpdateWorkflowDefinitionCommand(id, request.Name, request.Description, steps, rules, request.ExpectedRowVersion), ct));
    }

    [HttpPost("definitions/{code}/versions")]
    [HasPermission("platform.workflow.create")]
    public async Task<IActionResult> CreateDefinitionVersion(string code, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new CreateWorkflowDefinitionVersionCommand(code), ct));

    [HttpPost("definitions/{id:guid}/publish")]
    [HasPermission("platform.workflow.publish")]
    public async Task<IActionResult> PublishDefinition(Guid id, [FromBody] PublishWorkflowDefinitionRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new PublishWorkflowDefinitionCommand(id, request.ExpectedRowVersion), ct));

    // ---- Instances ----

    [HttpPost("instances/start")]
    [HasPermission("platform.workflow.start")]
    public async Task<IActionResult> StartInstance([FromBody] StartWorkflowInstanceRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new StartWorkflowInstanceCommand(request.DefinitionCode, request.SubjectReference), ct));

    [HttpGet("instances")]
    [HasPermission("platform.workflow.read")]
    public async Task<IActionResult> ListInstances(
        [FromQuery(Name = "search")] string? search,
        [FromQuery(Name = "status")] string? status,
        [FromQuery(Name = "page")] int page = 1,
        [FromQuery(Name = "page_size")] int pageSize = 25,
        CancellationToken ct = default)
        => CreateActionResultInstance(await _mediator.Send(new GetWorkflowInstancesQuery(search, status, page, pageSize), ct));

    [HttpGet("instances/{id:guid}")]
    [HasPermission("platform.workflow.read")]
    public async Task<IActionResult> GetInstance(Guid id, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetWorkflowInstanceByIdQuery(id), ct));

    // ---- Approval tasks (inbox) ----

    [HttpGet("tasks")]
    [HasPermission("platform.workflow.read")]
    public async Task<IActionResult> ListTasks(
        [FromQuery(Name = "status")] string? status,
        [FromQuery(Name = "assignee_role")] string? assigneeRole,
        [FromQuery(Name = "assignee_id")] string? assigneeId,
        [FromQuery(Name = "instance_id")] Guid? instanceId,
        [FromQuery(Name = "page")] int page = 1,
        [FromQuery(Name = "page_size")] int pageSize = 25,
        CancellationToken ct = default)
        => CreateActionResultInstance(await _mediator.Send(new GetApprovalTasksQuery(status, assigneeRole, assigneeId, instanceId, page, pageSize), ct));

    [HttpGet("tasks/{id:guid}")]
    [HasPermission("platform.workflow.read")]
    public async Task<IActionResult> GetTask(Guid id, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetApprovalTaskByIdQuery(id), ct));

    [HttpPost("tasks/{id:guid}/approve")]
    [HasPermission("platform.workflow.approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveApprovalTaskRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new ApproveApprovalTaskCommand(id, request.EvidenceReference, request.Note), ct));

    [HttpPost("tasks/{id:guid}/reject")]
    [HasPermission("platform.workflow.approve")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectApprovalTaskRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new RejectApprovalTaskCommand(id, request.Note), ct));

    [HttpPost("tasks/{id:guid}/delegate")]
    [HasPermission("platform.workflow.approve")]
    public async Task<IActionResult> Delegate(Guid id, [FromBody] DelegateApprovalTaskRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new DelegateApprovalTaskCommand(id, request.ToAssigneeId, request.Note), ct));
}

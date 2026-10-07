using System.IdentityModel.Tokens.Jwt;
using Diten.Platform.Application.Common;
using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.Workflow;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.Tasks.Recovery;
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

    // WP-CL-BE-3a — transition history with comments / rejection reasons (existing view permission; other tenant → 404).
    [HttpGet("instances/{id:guid}/history")]
    [HasPermission(WorkflowPermissions.InstancesView)]
    public async Task<IActionResult> GetInstanceHistory(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetWorkflowInstanceHistoryQuery(id, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("instances")]
    [HasPermission(WorkflowPermissions.InstancesView)]
    public async Task<IActionResult> GetInstances(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetWorkflowInstanceListQuery(CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    // WP-CL-BE-3 — batch status read for cross-service reconciliation (≤100 ids; comma-separated or repeated).
    [HttpGet("instances/by-objects")]
    [HasPermission(WorkflowPermissions.InstancesView)]
    public async Task<IActionResult> GetInstancesByObjects(
        [FromQuery] string? objectType,
        [FromQuery] string[]? objectIds,
        CancellationToken ct)
    {
        var response = await _mediator.Send(
            new GetWorkflowInstancesByObjectsQuery(objectType, objectIds, CorrelationId), ct);
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

    /// <summary>
    /// BL-540 (1) — the operator's orphan recovery. DRY RUN unless the body says <c>apply: true</c>: count and list only. The key
    /// is non-human (Auth refuses it to any human role); it is meant for a service client an operator runs on purpose.
    /// </summary>
    [HttpPost("operator/orphans")]
    [HasPermission(WorkflowPermissions.OrphansRecover)]
    public async Task<IActionResult> SweepOrphans([FromBody] WorkflowOrphanSweepRequest? request, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new SweepWorkflowOrphansCommand(request?.Apply ?? false, request?.MaxItems ?? 0, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("tasks/{taskId:guid}/approve")]
    [HasPermission(WorkflowPermissions.TasksApprove)]
    public async Task<IActionResult> ApproveTask(
        Guid taskId,
        [FromBody] ApproveWorkflowTaskTransportRequest request,
        CancellationToken ct)
    {
        if (!TryBindActor(request.ActorId, out var actor, out var refusal))
        {
            return refusal!;
        }

        var response = await _mediator.Send(new ApproveWorkflowTaskCommand(
            taskId,
            new ApproveWorkflowTaskRequest(
                actor,
                request.ReasonCode,
                request.IdempotencyKey,
                request.Comment,
                request.EvidenceRef),
            CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("tasks/{taskId:guid}/reject")]
    [HasPermission(WorkflowPermissions.TasksReject)]
    public async Task<IActionResult> RejectTask(
        Guid taskId,
        [FromBody] RejectWorkflowTaskTransportRequest request,
        CancellationToken ct)
    {
        if (!TryBindActor(request.ActorId, out var actor, out var refusal))
        {
            return refusal!;
        }

        var response = await _mediator.Send(new RejectWorkflowTaskCommand(
            taskId,
            new RejectWorkflowTaskRequest(
                actor,
                request.ReasonCode,
                request.IdempotencyKey,
                request.Comment,
                request.EvidenceRef),
            CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("tasks/{taskId:guid}/delegate")]
    [HasPermission(WorkflowPermissions.TasksDelegate)]
    public async Task<IActionResult> DelegateTask(
        Guid taskId,
        [FromBody] DelegateWorkflowTaskRequest request,
        CancellationToken ct)
    {
        if (!TryBindActor(request.ActorId, out var actor, out var refusal))
        {
            return refusal!;
        }

        var response = await _mediator.Send(new DelegateWorkflowTaskCommand(taskId, request with { ActorId = actor }, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("tasks/{taskId:guid}/request-info")]
    [HasPermission(WorkflowPermissions.TasksRequestInfo)]
    public async Task<IActionResult> RequestInfoTask(
        Guid taskId,
        [FromBody] RequestInfoWorkflowTaskRequest request,
        CancellationToken ct)
    {
        if (!TryBindActor(request.ActorId, out var actor, out var refusal))
        {
            return refusal!;
        }

        var response = await _mediator.Send(new RequestInfoWorkflowTaskCommand(taskId, request with { ActorId = actor }, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("tasks/{taskId:guid}/cancel")]
    [HasPermission(WorkflowPermissions.TasksCancel)]
    public async Task<IActionResult> CancelTask(
        Guid taskId,
        [FromBody] CancelWorkflowTaskRequest request,
        CancellationToken ct)
    {
        if (!TryBindActor(request.ActorId, out var actor, out var refusal))
        {
            return refusal!;
        }

        var response = await _mediator.Send(new CancelWorkflowTaskCommand(taskId, request with { ActorId = actor }, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>
    /// B4 — the actor of a task action is ALWAYS the signed-in user: the token's single <c>sub</c>, never the body. A
    /// token with no subject, a malformed one, or more than one is 401 <c>WORKFLOW_ACTOR_UNAUTHENTICATED</c>. A body may
    /// omit <c>actorId</c> (the server fills it in) or repeat the caller's own id; naming anybody else is refused with
    /// 403 <c>WORKFLOW_ACTOR_MISMATCH</c> — before, a person holding the approve permission could type someone else's
    /// id and decide that person's approval. One rule for all five task actions.
    /// </summary>
    private bool TryBindActor(string? claimed, out string actor, out IActionResult? refusal)
    {
        refusal = null;
        if (!TryResolveAuthenticatedActor(out actor))
        {
            refusal = CreateActionResultInstance(Response<WorkflowTaskTransitionResponse>.Fail(
                "WORKFLOW_ACTOR_UNAUTHENTICATED",
                StatusCodes.Status401Unauthorized,
                "WORKFLOW_ACTOR_UNAUTHENTICATED",
                CorrelationId));
            return false;
        }

        if (!string.IsNullOrWhiteSpace(claimed)
            && !(Guid.TryParse(claimed.Trim(), out var named) && named.ToString("D") == actor))
        {
            refusal = CreateActionResultInstance(Response<WorkflowTaskTransitionResponse>.Fail(
                "A task action is always taken as the signed-in user.",
                403,
                WorkflowReasonCodes.WorkflowActorMismatch,
                CorrelationId));
            return false;
        }

        return true;
    }

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId)
            ? HttpContext.TraceIdentifier
            : _correlationContext.CorrelationId!;

    private bool TryResolveAuthenticatedActor(out string actorId)
    {
        actorId = string.Empty;
        if (User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var subjects = User.Claims
            .Where(claim => string.Equals(claim.Type, JwtRegisteredClaimNames.Sub, StringComparison.Ordinal))
            .Select(claim => claim.Value)
            .ToArray();
        if (subjects.Length != 1
            || !Guid.TryParseExact(subjects[0], "D", out var userId)
            || userId == Guid.Empty)
        {
            return false;
        }

        actorId = userId.ToString("D");
        return true;
    }
}

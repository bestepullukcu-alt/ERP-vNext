using System.Security.Claims;
using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Queries;
using Diten.Platform.Application.Features.Workflow;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

// WC-1 (DCP-004) — read-only personal work-item aggregation. Backend-only slice; the frontend
// mock → API wiring is WC-1b. The route stays version-explicit; gateway routing is a separate integration-agent
// task. No command endpoint lives here — approve/reject/delegate stay on MOD-0023's existing endpoints.
[ApiController]
[Route("api/v1/work-items")]
[Authorize]
public sealed class WorkItemsController : CustomBaseController
{
    // The workflow-task permissions whose grant state gates the projected approval actions[]. Evaluated from
    // the caller's claims here (API layer) via the existing PermissionClaimEvaluator seam and passed as data
    // into the read query, so the Application handler stays pure and the browser is never an authority.
    private static readonly string[] ActionPermissionKeys =
    [
        WorkflowPermissions.TasksApprove,
        WorkflowPermissions.TasksReject,
        WorkflowPermissions.TasksRequestInfo,
        WorkflowPermissions.TasksDelegate
    ];

    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public WorkItemsController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    [HttpGet("mine")]
    [HasPermission(WorkAggregationPermissions.InboxView)]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        var isPlatformActor = IsPlatformActor(User);

        // Platform actors pass every permission; otherwise evaluate each action key against the principal's
        // claims using the same side-effect-free evaluator the enforcement filter uses.
        var granted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!isPlatformActor)
        {
            foreach (var key in ActionPermissionKeys)
            {
                if (PermissionClaimEvaluator.Evaluate(User.Claims, key).IsSatisfied)
                {
                    granted.Add(key);
                }
            }
        }

        var response = await _mediator.Send(
            new GetMyWorkItemsQuery(isPlatformActor, granted, CorrelationId),
            ct);
        return CreateActionResultInstance(response);
    }

    private static bool IsPlatformActor(ClaimsPrincipal user)
    {
        var actorType = user.FindFirst("actor_type")?.Value;
        return string.Equals(actorType, "platform_admin", StringComparison.OrdinalIgnoreCase)
               || string.Equals(actorType, "partner_admin", StringComparison.OrdinalIgnoreCase);
    }

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId)
            ? HttpContext.TraceIdentifier
            : _correlationContext.CorrelationId!;
}

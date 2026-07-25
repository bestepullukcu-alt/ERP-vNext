using System.Security.Claims;
using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Providers;
using Diten.Platform.Application.Features.WorkAggregation.Queries;
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
    // The permission keys that gate projected actions[] are NOT listed here: every provider declares its own
    // (IWorkItemProvider.RequiredActionPermissions) and this controller evaluates the union against the caller's
    // claims via the existing PermissionClaimEvaluator seam, passing the granted set as data into the read query.
    // The Application handler stays pure and the browser is never an authority.
    //
    // A hardcoded list here is what broke MOD-0024: it collected only the four workflow keys, so every
    // platform.tasks.* check returned false and every task action was projected as PERMISSION_DENIED even though
    // the caller held the permission (proven live — the same action returned 409, not 403, when invoked).
    // Deriving the set from the providers means adding a third provider cannot reintroduce that.
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;
    private readonly IEnumerable<IWorkItemProvider> _providers;

    public WorkItemsController(
        IMediator mediator,
        ICorrelationContext correlationContext,
        IEnumerable<IWorkItemProvider> providers)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
        _providers = providers;
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
            foreach (var key in RequiredActionPermissions())
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

    /// <summary>
    /// The union of every bound provider's declared action permissions, de-duplicated case-insensitively.
    /// </summary>
    private IEnumerable<string> RequiredActionPermissions()
        => _providers
            .SelectMany(provider => provider.RequiredActionPermissions ?? [])
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase);

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

using System.Security.Claims;
using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Dispatch;
using Diten.Platform.Application.Features.WorkAggregation.Providers;
using Diten.Platform.Application.Features.WorkAggregation.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

// WC-1 (DCP-004) — personal work-item aggregation. The route stays version-explicit.
//
// ── THIS FILE'S HEADER USED TO SAY (and the decision was real): ────────────────────────────────────────────
//
//     "No command endpoint lives here — approve/reject/delegate stay on MOD-0023's existing endpoints."
//
// WC-D2 REVERSES IT DELIBERATELY, and the reason is what that sentence left unsaid: the endpoints stayed where
// they were, and NOTHING ROUTED TO THEM. The projection emits an authoritative actions[] carrying code, label,
// enabled and disabledReasonCode — and no endpoint, no method, no permission key. So each action's ADDRESS had
// to be hardcoded in the browser, and it was hardcoded for exactly one provider:
//
//     const isRealTaskItem = (item) =>
//         item && item.provenance !== 'fixture' && item.source?.providerCode === 'tasks';
//
// Every other provider's button ran a browser-side animation and logged "no backend owns it". MOD-0023 has had
// four live approval endpoints behind its items since WC-1 and not one button ever reached them.
//
// So ONE write endpoint lives here now — POST {itemId}/actions/{actionCode} — and Platform resolves where it
// goes, through IWorkItemActionDispatcher. This is NOT a second home for approval logic: the dispatcher forwards
// to the module's existing command, and MOD-0024 still never decides an approval (charter Binding A). What moved
// is the ADDRESS BOOK, from the browser to the server, so that a provider bound tomorrow inherits a working
// button instead of a dead one.
//
// The reversal is deliberately narrow: /Tasks and MOD-0023's own routes are untouched and keep serving their own
// screens. This slice ADDS an address; it does not migrate one.
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
    private readonly IEnumerable<IWorkItemActionDispatcher> _dispatchers;
    private readonly ICurrentUserContext _currentUser;

    public WorkItemsController(
        IMediator mediator,
        ICorrelationContext correlationContext,
        IEnumerable<IWorkItemProvider> providers,
        IEnumerable<IWorkItemActionDispatcher> dispatchers,
        ICurrentUserContext currentUser)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
        _providers = providers;
        _dispatchers = dispatchers;
        _currentUser = currentUser;
    }

    /// <summary>
    /// BL-023 — <c>?scope=team</c> lists the caller's SUBORDINATES' own work instead of their own.
    ///
    /// <para>A query parameter rather than a second endpoint, because it is the same question about a different
    /// owner; and the same permission, because seeing your team's load is not a different capability — WHO your
    /// team is comes from the org chart and is already scope-limited (BL-057), so no extra grant can widen it.
    /// An unrecognised value binds to <see cref="WorkItemScope.Self"/>, which is the fail-safe direction.</para>
    /// </summary>
    [HttpGet("mine")]
    [HasPermission(WorkAggregationPermissions.InboxView)]
    public async Task<IActionResult> GetMine(
        CancellationToken ct,
        [FromQuery] WorkItemScope scope = WorkItemScope.Self)
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
            new GetMyWorkItemsQuery(isPlatformActor, granted, CorrelationId, scope),
            ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>
    /// BL-023 — whether the caller has anybody reporting to them, so the scope control can be DISABLED with a
    /// reason instead of offering a view that will always be empty. Same permission as the list: who your team
    /// is comes from the org chart and is already scope-limited, so this widens nothing.
    /// </summary>
    [HttpGet("team-availability")]
    [HasPermission(WorkAggregationPermissions.InboxView)]
    public async Task<IActionResult> GetTeamAvailability(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetMyTeamAvailabilityQuery(CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>
    /// WC-D2 — THE ONE ADDRESS THE BROWSER WRITES TO. Platform resolves which module carries out the action.
    /// </summary>
    /// <remarks>
    /// <para><b>The permission is decided HERE, from claims.</b> Each dispatcher names the key its underlying
    /// endpoint requires — always one of the module's own constants, and always one the matching provider already
    /// declares in <c>RequiredActionPermissions</c> (WorkItemActionDispatchTests pins that containment, so no new
    /// permission list can grow at this seam). The key is evaluated through the same side-effect-free
    /// <c>PermissionClaimEvaluator</c> the enforcement filter uses. The module's own handler re-checks its own
    /// rules underneath — being permitted to press <c>cancel</c> is not the same as being the requester — so this
    /// is the outer gate, never the only one. The browser is authority for nothing.</para>
    ///
    /// <para><b>No [HasPermission] attribute on the method.</b> The required key depends on the ACTION, which is
    /// a route value; a fixed attribute would have to name one key for eleven different verbs. Attributing
    /// <c>inbox.view</c> here would be worse than nothing: it would gate a write behind a read.</para>
    ///
    /// <para><b>Every refusal is explicit and stable.</b> A provider nobody bound, a provider with no dispatcher,
    /// an action code the dispatcher does not publish, a permission the caller lacks — each answers its own
    /// reason code. Silent success is the one outcome that is forbidden: it is exactly what the browser-side
    /// "transition" did, and it is why this endpoint exists.</para>
    /// </remarks>
    [HttpPost("{itemId:guid}/actions/{actionCode}")]
    public async Task<IActionResult> DispatchAction(
        Guid itemId,
        string actionCode,
        [FromBody] WorkItemActionRequestDto request,
        CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ProviderCode))
        {
            return CreateActionResultInstance(Fail(
                "A providerCode is required.", 400, WorkItemActionReasonCodes.ProviderUnknown));
        }

        // "Which source is this?" is answered against the BOUND providers first, so "there is no such source"
        // and "that source cannot be written to" stay two different answers to two different questions.
        var isBoundProvider = _providers.Any(p =>
            string.Equals(p.ProviderCode, request.ProviderCode, StringComparison.OrdinalIgnoreCase));

        var dispatcher = _dispatchers.FirstOrDefault(d =>
            string.Equals(d.ProviderCode, request.ProviderCode, StringComparison.OrdinalIgnoreCase));

        if (dispatcher is null)
        {
            return CreateActionResultInstance(isBoundProvider
                ? Fail(
                    $"Provider '{request.ProviderCode}' publishes actions but has no dispatcher.",
                    501,
                    WorkItemActionReasonCodes.ProviderNotDispatchable)
                : Fail(
                    $"Provider '{request.ProviderCode}' is not bound.",
                    404,
                    WorkItemActionReasonCodes.ProviderUnknown));
        }

        if (!dispatcher.CanDispatch(actionCode))
        {
            return CreateActionResultInstance(Fail(
                $"Action '{actionCode}' is not published by provider '{dispatcher.ProviderCode}'.",
                400,
                WorkItemActionReasonCodes.ActionUnknown));
        }

        var isPlatformActor = IsPlatformActor(User);
        var required = dispatcher.RequiredPermission(actionCode);
        if (!isPlatformActor
            && !string.IsNullOrWhiteSpace(required)
            && !PermissionClaimEvaluator.Evaluate(User.Claims, required!).IsSatisfied)
        {
            return CreateActionResultInstance(Fail(
                "The caller does not hold the permission this action requires.",
                403,
                WorkItemActionReasonCodes.ActionForbidden));
        }

        /*
         * The SAME actor the read path builds, and built the same way: the union of the providers' declared keys,
         * evaluated against this caller's claims. Not just the one key this action needs — a dispatcher may have
         * to pass a SECOND authority to its handler as data (cancel carries "may cancel any task"), and an actor
         * carrying only the action's own key would silently answer false for it.
         */
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

        // UserId is resolved server-side, never read off the payload.
        var actor = new WorkItemActor(_currentUser.UserId, isPlatformActor, granted);

        var response = await dispatcher.DispatchAsync(
            new WorkItemActionDispatchRequest(
                itemId,
                actionCode,
                request.Payload ?? new WorkItemActionPayloadDto(),
                actor,
                CorrelationId),
            ct);

        return CreateActionResultInstance(response);
    }

    private Response<WorkItemActionResultDto> Fail(string error, int statusCode, string reasonCode)
        => Response<WorkItemActionResultDto>.Fail(error, statusCode, reasonCode, CorrelationId);

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

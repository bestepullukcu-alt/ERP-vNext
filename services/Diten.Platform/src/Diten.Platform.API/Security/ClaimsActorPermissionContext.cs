using System.Security.Claims;
using Diten.Platform.Application.Contracts;

namespace Diten.Platform.API.Security;

/// <summary>
/// The one implementation of <see cref="IActorPermissionContext"/> — claims in, yes/no out.
///
/// <para><b>It lives in the API project on purpose.</b> <see cref="PermissionClaimEvaluator"/> is here, and it
/// owns the canonical + legacy-alias dual read that the enforcement filter uses. Re-implementing that matching in
/// Infrastructure — or reading <c>ITenantAuthorizationContext.PermissionKeys</c> directly, which is the raw claim
/// list with no alias expansion — would give field authorization slightly DIFFERENT semantics from every
/// <c>[HasPermission]</c> on the same controller. A security rule that is nearly the same as the one beside it is
/// the drift this codebase has already paid for with the seat directory and the active-window rule.</para>
///
/// <para><b>Nothing is cached.</b> The question is re-answered from the request's own claims each time it is
/// asked, so a field definition edited a second ago is honoured by the very next request. (What the claims
/// themselves say is a different clock: permissions are minted into the access token at login and there is no
/// revocation channel, so a GRANT change waits for the token to turn over. That is a platform-wide property, not
/// something this seam introduces or can fix — see the note on BL-024.)</para>
/// </summary>
public sealed class ClaimsActorPermissionContext : IActorPermissionContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ClaimsActorPermissionContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    /// <summary>
    /// Mirrors <c>HasPermissionAttribute</c>'s bypass and <c>WorkItemsController.IsPlatformActor</c> exactly: a
    /// platform or partner admin passes every key. Deriving "platform" differently here would let a field be
    /// hidden from an actor the endpoint beside it lets straight through.
    /// </summary>
    public bool IsPlatformActor
    {
        get
        {
            var actorType = Principal?.FindFirst("actor_type")?.Value;
            return string.Equals(actorType, "platform_admin", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actorType, "partner_admin", StringComparison.OrdinalIgnoreCase);
        }
    }

    public bool Has(string? permissionKey)
    {
        // No key = nothing to check. An unrestricted field is not a permission question, and answering "denied"
        // here would hide every field that nobody ever restricted.
        if (string.IsNullOrWhiteSpace(permissionKey))
        {
            return true;
        }

        if (IsPlatformActor)
        {
            return true;
        }

        // FAIL-CLOSED with no principal. A handler reached without one is already a bug; it must not also be a
        // caller who reads everything.
        var principal = Principal;
        return principal is not null
               && PermissionClaimEvaluator.Evaluate(principal.Claims, permissionKey).IsSatisfied;
    }
}

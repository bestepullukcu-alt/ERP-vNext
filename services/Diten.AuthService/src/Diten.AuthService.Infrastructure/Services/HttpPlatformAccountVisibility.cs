using Diten.AuthService.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Diten.AuthService.Infrastructure.Services;

/// <summary>
/// BL-521 FIX6 item 13 (D') / FIX8 item 2 — the request's answer to "may this caller see platform accounts?". THE RULE IS NOW
/// THE OTHER WAY ROUND: within a request, ONLY a platform-door actor (<c>actor_type</c> <c>platform_admin</c> or
/// <c>partner_admin</c>) sees them; EVERY OTHER authenticated request hides them — a tenant actor, but also a token with no
/// actor type, an empty one, or one this service does not know (FIX6 hid only <c>tenant_user</c>, so an authenticated request
/// carrying anything else saw every account: a silent fail-open). What is not changed: an unauthenticated request (the doors
/// themselves — sign-in, MFA verification, refresh and the platform door — are anonymous and must find the account to answer),
/// the internal service calls (they authenticate by the internal key, not by a principal) and anything outside a request
/// (background work) are not hidden from anything. See <see cref="IPlatformAccountVisibility"/>.
/// </summary>
public sealed class HttpPlatformAccountVisibility : IPlatformAccountVisibility
{
    // the only two actor types the platform door mints (the same pair RefreshTokenCommandHandler and PlatformAuthController know)
    private static readonly string[] PlatformDoorActorTypes = ["platform_admin", "partner_admin"];

    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpPlatformAccountVisibility(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool HidesPlatformAccounts
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            var actorType = user.FindFirst("actor_type")?.Value;
            return actorType is null || !PlatformDoorActorTypes.Contains(actorType, StringComparer.OrdinalIgnoreCase);
        }
    }
}

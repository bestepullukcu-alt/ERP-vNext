using Diten.AuthService.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Diten.AuthService.Infrastructure.Services;

/// <summary>
/// BL-521 FIX6 item 13 (D') — the request's answer to "may this caller see platform accounts?": NO when the authenticated
/// principal is a tenant actor (<c>actor_type=tenant_user</c>); yes for a platform-door actor (platform_admin /
/// partner_admin), for an unauthenticated request and outside any request (internal service calls, background work).
/// See <see cref="IPlatformAccountVisibility"/>.
/// </summary>
public sealed class HttpPlatformAccountVisibility : IPlatformAccountVisibility
{
    private const string TenantUserActorType = "tenant_user";

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
            return user?.Identity?.IsAuthenticated == true
                   && string.Equals(user.FindFirst("actor_type")?.Value, TenantUserActorType, StringComparison.OrdinalIgnoreCase);
        }
    }
}

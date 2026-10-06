using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Resources;
using Diten.CrmService.Infrastructure.Authorization;
using Microsoft.AspNetCore.Http;

namespace Diten.CrmService.Infrastructure;

/// <summary>
/// WP-VP-2 (B-1) — <see cref="ICallerScope"/> off the caller principal: the resource id is the same stable user id
/// <c>resources/me</c> returns (<see cref="MyResourceIdentity.ResolveUserId"/>), and a permission is the same claim match
/// the <c>[HasPermission]</c> handler uses (<see cref="PermissionClaims"/>). No request value is ever read.
/// </summary>
public sealed class HttpCallerScope : ICallerScope
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCallerScope(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public string? CallerResourceId => MyResourceIdentity.ResolveUserId(_httpContextAccessor.HttpContext?.User);

    public bool HasPermission(string permissionKey)
        => _httpContextAccessor.HttpContext?.User is { } user && PermissionClaims.HasPermission(user, permissionKey);
}

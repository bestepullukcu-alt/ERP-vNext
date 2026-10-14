using Diten.MdmService.Application.Contracts.ReferenceData;
using Microsoft.AspNetCore.Http;

namespace Diten.MdmService.Infrastructure.Security;

/// <summary>WP-VW-W4 (W4-BE-b2) — the MDM PermissionAuthorizationHandler rule as a question: a platform admin, or a
/// <c>permission</c> claim with the key. Fail-closed without a principal.</summary>
public sealed class MdmPermissionContext : IMdmPermissionContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MdmPermissionContext(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public bool Has(string permission)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user is null) return false;
        if (string.Equals(user.FindFirst("actor_type")?.Value, "platform_admin", StringComparison.OrdinalIgnoreCase)) return true;
        return user.Claims.Any(c => string.Equals(c.Type, "permission", StringComparison.OrdinalIgnoreCase)
                                    && string.Equals(c.Value, permission, StringComparison.OrdinalIgnoreCase));
    }
}

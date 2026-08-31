using System.Security.Claims;
using Diten.PpmService.Application.Common;
using Microsoft.AspNetCore.Http;

namespace Diten.PpmService.Infrastructure.Authorization;

public sealed class JwtRequestContext(IHttpContextAccessor accessor) : ITenantContext, ICurrentActorContext
{
    public Guid TenantId => ParseRequiredGuid("tenant_id");
    public Guid ActorId => ParseRequiredGuid("sub", ClaimTypes.NameIdentifier);

    private Guid ParseRequiredGuid(params string[] claimTypes)
    {
        var user = accessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return Guid.Empty;
        }

        foreach (var claimType in claimTypes)
        {
            if (Guid.TryParse(user.FindFirst(claimType)?.Value, out var parsed) && parsed != Guid.Empty)
            {
                return parsed;
            }
        }

        return Guid.Empty;
    }
}

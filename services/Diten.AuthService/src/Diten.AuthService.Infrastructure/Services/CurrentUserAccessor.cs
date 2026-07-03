using System.Security.Claims;
using Diten.AuthService.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Diten.AuthService.Infrastructure.Services;

/// <summary>FEAT-AUDIT-RBAC — resolves the current request's actor id from the authenticated principal's
/// NameIdentifier/sub claim (see <see cref="ICurrentUserAccessor"/>).</summary>
public sealed class CurrentUserAccessor : ICurrentUserAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var raw = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user?.FindFirst("sub")?.Value;
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }
}

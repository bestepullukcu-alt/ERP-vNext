using System.Security.Claims;
using Diten.DevEnablementService.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Diten.DevEnablementService.Infrastructure.UserContext;

public sealed class CurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid UserId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User?.Claims
                .FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier || x.Type == "sub")?.Value;

            return Guid.TryParse(value, out var guid) ? guid : Guid.Empty;
        }
    }

    public string UserName => _httpContextAccessor.HttpContext?.User?.Claims
        .FirstOrDefault(x => x.Type == ClaimTypes.Name || x.Type == "name" || x.Type == "preferred_username")?.Value ?? "unknown";
}

using System.Security.Claims;
using Diten.MdmService.Application.Contracts;
using Microsoft.AspNetCore.Http;

namespace Diten.MdmService.Infrastructure.Security;

public sealed class ProductIdentityActorContext : IProductIdentityActorContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ProductIdentityActorContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string ActorId
    {
        get
        {
            var principal = _httpContextAccessor.HttpContext?.User;
            var actorId = principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal?.FindFirstValue("sub");
            return !string.IsNullOrWhiteSpace(actorId)
                ? actorId
                : throw new InvalidOperationException("Trusted product-identity actor is unavailable.");
        }
    }
}

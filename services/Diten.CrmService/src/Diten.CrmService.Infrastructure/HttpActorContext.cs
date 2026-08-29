using System.Security.Claims;
using Diten.CrmService.Application.Common;
using Microsoft.AspNetCore.Http;

namespace Diten.CrmService.Infrastructure;

/// <summary>
/// Reads the acting user off the caller principal for provenance fields. Prefers a stable identifier
/// (<c>sub</c> / NameIdentifier), falling back to email/name. Returns null when nothing usable is present — the
/// provenance field then stays null instead of carrying a fabricated actor. No PII beyond the identity already in
/// the token is copied, and nothing is ever taken from the request body.
/// </summary>
public sealed class HttpActorContext : IActorContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpActorContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? ActorName
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var value = user.FindFirstValue("sub")
                ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue(ClaimTypes.Email)
                ?? user.FindFirstValue("email")
                ?? user.Identity.Name;

            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}

using System.Security.Claims;
using Diten.ManufacturingService.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Diten.ManufacturingService.Infrastructure.UserContext;

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

    /// <summary>
    /// The name written into the BOM history. AuthService tokens carry no <c>name</c> claim — they carry
    /// <c>given_name</c> / <c>family_name</c> / <c>email</c> — so those follow, in the same order Platform's
    /// CurrentUserContext uses for its display name. "unknown" only when the token names nobody.
    /// </summary>
    public string UserName
    {
        get
        {
            var name = Find(ClaimTypes.Name, "name", "preferred_username");
            if (name is not null)
            {
                return name;
            }

            var fullName = string.Join(' ', new[] { Find("given_name", ClaimTypes.GivenName), Find("family_name", ClaimTypes.Surname) }
                .Where(part => part is not null));
            return fullName.Length > 0 ? fullName : Find("email", ClaimTypes.Email) ?? "unknown";
        }
    }

    private string? Find(params string[] claimTypes) => _httpContextAccessor.HttpContext?.User?.Claims
        .FirstOrDefault(x => claimTypes.Contains(x.Type) && !string.IsNullOrWhiteSpace(x.Value))?.Value.Trim();
}

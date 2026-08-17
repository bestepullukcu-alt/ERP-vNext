using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Microsoft.AspNetCore.Http;

namespace Diten.Platform.Infrastructure.Services;

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
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null)
            {
                return Guid.Empty;
            }

            var subject = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(subject, out var userId) ? userId : Guid.Empty;
        }
    }

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public string? Email => FindClaimValue(
        JwtRegisteredClaimNames.Email,
        ClaimTypes.Email,
        "email");

    public string? DisplayName
    {
        get
        {
            var name = FindClaimValue(ClaimTypes.Name, "name", "preferred_username");
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            var givenName = FindClaimValue(JwtRegisteredClaimNames.GivenName, ClaimTypes.GivenName);
            var familyName = FindClaimValue(JwtRegisteredClaimNames.FamilyName, ClaimTypes.Surname);
            var fullName = string.Join(' ', new[] { givenName, familyName }.Where(part => !string.IsNullOrWhiteSpace(part)));
            return string.IsNullOrWhiteSpace(fullName) ? null : fullName;
        }
    }

    public string ActorName
    {
        get
        {
            if (!IsAuthenticated)
            {
                return "system";
            }

            return Email
                ?? DisplayName
                ?? (UserId != Guid.Empty ? UserId.ToString() : "system");
        }
    }

    private string? FindClaimValue(params string[] claimTypes)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user == null)
        {
            return null;
        }

        foreach (var claimType in claimTypes)
        {
            var value = user.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}

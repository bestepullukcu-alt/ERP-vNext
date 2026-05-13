using System.Security.Claims;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Common.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions);
    string GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes);
    string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions);
    string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes);
    string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes, bool requiresPasswordChange);
    string GenerateRefreshToken();
    ClaimsPrincipal GetPrincipalFromExpiredToken(string token);
}

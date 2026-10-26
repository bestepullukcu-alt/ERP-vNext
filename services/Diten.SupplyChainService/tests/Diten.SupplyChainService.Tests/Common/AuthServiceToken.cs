using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
namespace Diten.SupplyChainService.Tests.Common;

/// <summary>
/// H2 (plan v10.0 §4). The one place the test hosts mint a bearer token, and it mints the token AuthService
/// actually issues to a tenant user — <c>TokenService.GenerateAccessToken</c>
/// (services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Services/TokenService.cs:31-68): issuer
/// <c>diten-auth-service</c>, audience <c>diten-erp</c> (Api/appsettings.json), claims sub, email, given_name,
/// family_name, actor_type=tenant_user, tenant_id, pwd_change_required, one <c>role</c> per role name and one
/// <c>permission</c> per key, HS256, expiry only (no iat/nbf).
///
/// It carries NO legal_entity_id: R-2 removed it from AuthService's token, and the legal entity travels in
/// X-Legal-Entity-Id. Before this helper every host minted its own issuer and most added legal_entity_id, so a
/// host suite could pass with a token no real user holds — the shape SandopCapacityHttpHost's own comment records
/// for the gates that refused every real user until 2026-10-18.
/// </summary>
public static class AuthServiceToken
{
    public const string Issuer = "diten-auth-service";
    public const string Audience = "diten-erp";

    /// <summary>The JwtSettings a host must give the service so it accepts <see cref="Mint"/>'s tokens.</summary>
    public static IEnumerable<KeyValuePair<string, string?>> JwtSettings(string secret) =>
    [
        new("JwtSettings:Secret", secret),
        new("JwtSettings:Issuer", Issuer),
        new("JwtSettings:Audience", Audience)
    ];

    /// <param name="extraClaims">Only for tests about a malformed or forged token; a real user's token has none.</param>
    public static string Mint(string secret, Guid tenant, Guid actor, IEnumerable<string> permissions,
        IEnumerable<string>? roles = null, IEnumerable<Claim>? extraClaims = null, TimeSpan? lifetime = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, actor.ToString()),
            new(JwtRegisteredClaimNames.Email, "test-user@example.test"),
            new(JwtRegisteredClaimNames.GivenName, "Test"),
            new(JwtRegisteredClaimNames.FamilyName, "User"),
            new("actor_type", "tenant_user"),
            new("tenant_id", tenant.ToString()),
            new("pwd_change_required", "false")
        };
        // AuthService adds ClaimTypes.Role; JwtSecurityTokenHandler's outbound map writes it as "role".
        claims.AddRange((roles ?? ["Admin"]).Select(r => new Claim("role", r)));
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        if (extraClaims is not null) claims.AddRange(extraClaims);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(Issuer, Audience, claims,
            expires: DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(10)),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256)));
    }
}

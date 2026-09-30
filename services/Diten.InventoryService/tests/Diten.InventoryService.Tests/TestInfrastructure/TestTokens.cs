using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>Mints HS256 access tokens the service under test accepts (same issuer / audience / key it is configured with).</summary>
public static class TestTokens
{
    public const string ReadPermission = "supplychain.inventory.read";
    public const string PostPermission = "supplychain.inventory.movements.post";

    public static readonly string[] AllPermissions = [ReadPermission, PostPermission];

    public static string Mint(
        Guid? tenantId,
        Guid? legalEntityId,
        Guid? subject,
        IEnumerable<string> permissions,
        IEnumerable<Claim>? extraClaims = null,
        string? signingSecret = null,
        DateTime? expires = null)
    {
        var claims = new List<Claim>();
        if (subject.HasValue) claims.Add(new Claim("sub", subject.Value.ToString()));
        if (tenantId.HasValue) claims.Add(new Claim("tenant_id", tenantId.Value.ToString()));
        if (legalEntityId.HasValue) claims.Add(new Claim("legal_entity_id", legalEntityId.Value.ToString()));
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        if (extraClaims is not null) claims.AddRange(extraClaims);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingSecret ?? TestSettings.JwtSecret));
        var notBefore = expires.HasValue && expires.Value < DateTime.UtcNow ? expires.Value.AddMinutes(-10) : DateTime.UtcNow.AddMinutes(-1);
        var token = new JwtSecurityToken(
            TestSettings.Issuer,
            TestSettings.Audience,
            claims,
            notBefore: notBefore,
            expires: expires ?? DateTime.UtcNow.AddMinutes(15),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

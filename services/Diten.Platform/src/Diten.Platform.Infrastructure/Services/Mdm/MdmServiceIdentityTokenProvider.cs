using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Diten.Platform.Infrastructure.Services.Mdm;

public interface IMdmServiceIdentityTokenProvider
{
    string? Create(Guid tenantId, Guid actorId, Guid legalEntityId);
}

public sealed class MdmServiceIdentityTokenProvider : IMdmServiceIdentityTokenProvider
{
    private readonly MdmServiceIdentityOptions _options;

    public MdmServiceIdentityTokenProvider(IOptions<MdmServiceIdentityOptions> options)
    {
        _options = options.Value;
    }

    public string? Create(Guid tenantId, Guid actorId, Guid legalEntityId)
    {
        if (!_options.Enabled
            || tenantId == Guid.Empty
            || actorId == Guid.Empty
            || legalEntityId == Guid.Empty
            || string.IsNullOrWhiteSpace(_options.Issuer)
            || string.IsNullOrWhiteSpace(_options.Audience)
            || string.IsNullOrWhiteSpace(_options.CallerId)
            || string.IsNullOrWhiteSpace(_options.KeyId)
            || string.IsNullOrWhiteSpace(_options.Secret)
            || Encoding.UTF8.GetByteCount(_options.Secret) < 32
            || _options.TokenLifetimeSeconds is < 5 or > 60)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, _options.CallerId),
            new Claim("scope", MdmServiceIdentityOptions.RequiredScope),
            new Claim("tenant_id", tenantId.ToString("D")),
            new Claim("actor_id", actorId.ToString("D")),
            new Claim("legal_entity_id", legalEntityId.ToString("D")),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("D")),
            new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: now.AddSeconds(_options.TokenLifetimeSeconds).UtcDateTime,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret)),
                SecurityAlgorithms.HmacSha256));
        token.Header[JwtHeaderParameterNames.Kid] = _options.KeyId;

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

using System.IdentityModel.Tokens.Jwt;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.S2S;
using Diten.AuthService.Infrastructure.Services;
using Diten.AuthService.Infrastructure.Settings;
using Diten.BuildingBlocks.Security.Secrets;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Diten.AuthService.Application.Tests.S2S;

public sealed class TokenFamilyIsolationTests
{
    [Fact]
    public void Existing_user_token_remains_hs256_and_is_not_an_s2s_family_token()
    {
        var service = new TokenService(Options.Create(new JwtSettings
        {
            Secret = new string('x', 48),
            Issuer = "user-issuer",
            Audience = "user-audience",
            AccessTokenExpirationMinutes = 5
        }), new NoOpRotationResolver());
        var token = new JwtSecurityTokenHandler().ReadJwtToken(service.GenerateAccessToken(
            new User("user@example.test", "hash", "User", "Test", Guid.NewGuid()), ["Admin"], ["ppm.project.read"]));

        Assert.Equal(SecurityAlgorithms.HmacSha256, token.Header.Alg);
        Assert.False(S2STokenFamilyProfile.Accepts(token.Header.Alg, token.Header.Typ));
        Assert.True(S2STokenFamilyProfile.Accepts("RS256", DelegatedActorProofV1.ExactType));
        Assert.False(S2STokenFamilyProfile.Accepts("HS256", DelegatedActorProofV1.ExactType));
        Assert.False(S2STokenFamilyProfile.Accepts("RS256", "JWT"));
    }

    private sealed class NoOpRotationResolver : ISecretRotationResolver
    {
        public SecurityKey GetCurrentSigningKey() => throw new NotSupportedException();
        public IReadOnlyList<SecurityKey> GetValidationKeys() => [];
    }
}

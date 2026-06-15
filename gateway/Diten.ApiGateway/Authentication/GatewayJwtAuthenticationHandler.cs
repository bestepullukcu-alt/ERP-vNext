using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Diten.BuildingBlocks.Security.Secrets;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Diten.ApiGateway.Authentication;

public sealed class GatewayJwtAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IConfiguration _configuration;

    public GatewayJwtAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = ReadBearerToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var issuer = _configuration["JwtSettings:Issuer"];
        var audience = _configuration["JwtSettings:Audience"];
        var rotationResolver = new JwtSecretRotationResolver(_configuration);
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = rotationResolver.GetValidationKeys(),
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(token, validationParameters, out _);
            var identity = principal.Identity as ClaimsIdentity;
            if (identity is not null && string.IsNullOrWhiteSpace(identity.AuthenticationType))
            {
                identity = new ClaimsIdentity(identity.Claims, Scheme.Name, identity.NameClaimType, identity.RoleClaimType);
                principal = new ClaimsPrincipal(identity);
            }

            var actorType = principal.Claims
                .FirstOrDefault(c => string.Equals(c.Type, "actor_type", StringComparison.OrdinalIgnoreCase))?.Value;
            Logger.LogDebug("Gateway JWT authentication succeeded. ActorType={ActorType}, ClaimsCount={ClaimsCount}",
                actorType, principal.Claims.Count());

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Gateway JWT authentication failed. TokenLength={TokenLength}", token?.Length ?? 0);
            return Task.FromResult(AuthenticateResult.Fail("Invalid bearer token."));
        }
    }

    private string? ReadBearerToken()
    {
        var authorization = Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(authorization) &&
            authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authorization["Bearer ".Length..].Trim();
        }

        return AuthTokenCookies.GetAccessToken(Request);
    }
}

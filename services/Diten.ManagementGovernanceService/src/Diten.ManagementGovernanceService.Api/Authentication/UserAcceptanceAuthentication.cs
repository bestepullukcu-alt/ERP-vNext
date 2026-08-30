using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Diten.ManagementGovernanceService.Api.Authentication;

public static class UserAcceptanceAuthentication
{
    public static IServiceCollection AddUserAcceptanceJwt(this IServiceCollection services, IConfiguration configuration)
    {
        var issuer = Required(configuration, "JwtSettings:Issuer");
        var audience = Required(configuration, "JwtSettings:Audience");
        var current = Required(configuration, "JwtSettings:Secret");
        var previous = configuration.GetSection("JwtSettings:PreviousSecrets").Get<string[]>() ?? [];
        var keys = new[] { current }.Concat(previous)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .Select(x => new SymmetricSecurityKey(Encoding.UTF8.GetBytes(x)))
            .ToArray();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(
            LocalTestSecurity.LocalTestAuthenticationHandler.SchemeName,
            options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKeys = keys,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub"
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var principal = context.Principal;
                        if (principal is null || !string.Equals(principal.FindFirst("actor_type")?.Value, "tenant_user", StringComparison.Ordinal) ||
                            principal.HasClaim(x => x.Type is "service_principal_id" or "delegated_actor_id" or "protocol_scope" or "token_family"))
                            context.Fail("user_session_token_required");
                        return Task.CompletedTask;
                    }
                };
            });
        return services;
    }

    private static string Required(IConfiguration configuration, string key) =>
        string.IsNullOrWhiteSpace(configuration[key])
            ? throw new InvalidOperationException($"Required configuration '{key}' is missing.")
            : configuration[key]!;
}

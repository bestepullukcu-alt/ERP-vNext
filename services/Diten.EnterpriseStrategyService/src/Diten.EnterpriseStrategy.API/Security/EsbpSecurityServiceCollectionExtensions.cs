using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace Diten.WebAPI.Security;

public static class EsbpSecurityServiceCollectionExtensions
{
    public static IServiceCollection AddEsbpSecurityPerimeter(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<EsbpTenantContext>();

        var issuer = configuration["JwtSettings:Issuer"] ?? "diten-auth-service";
        var audience = configuration["JwtSettings:Audience"] ?? "diten-erp";
        var secret = configuration["JwtSettings:Secret"];

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = string.IsNullOrWhiteSpace(secret)
                        ? null
                        : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };
            });

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }
}

using System.Text;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Infrastructure.Authorization;
using Diten.AuthService.Infrastructure.Middleware;
using Diten.AuthService.Infrastructure.Services;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Diten.AuthService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Settings
        services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));
        services.Configure<InternalEventAuthSettings>(configuration.GetSection("InternalEventAuth"));
        var jwtSettings = configuration.GetSection("JwtSettings").Get<JwtSettings>()
            ?? new JwtSettings();

        // Authentication
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
                    ClockSkew = TimeSpan.Zero
                };
            });

        // Authorization (Permission-based)
        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        // Services
        services.AddScoped<PasswordHasher>(); // concrete class registration
        services.AddScoped<IPasswordHasher>(sp => sp.GetRequiredService<PasswordHasher>());
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IRefreshTokenHasher, RefreshTokenHasher>();
        services.AddScoped<IInternalEventAuthService, InternalEventAuthService>();
        services.AddTransient<TenantPropagationHandler>();
        services.AddHttpClient("TenantAwareClient").AddHttpMessageHandler<TenantPropagationHandler>();

        // Tenant Context Registration
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

        return services;
    }

    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
    {
        app.UseMiddleware<TenantResolutionMiddleware>();
        return app;
    }
}

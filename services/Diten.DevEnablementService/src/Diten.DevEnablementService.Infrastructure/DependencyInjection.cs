using Diten.DevEnablementService.Application.Common;
using Diten.DevEnablementService.Application.Interfaces;
using Diten.DevEnablementService.Infrastructure.Authorization;
using Diten.DevEnablementService.Infrastructure.Middleware;
using Diten.DevEnablementService.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.DevEnablementService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // TenantContext: hem ITenantContext hem de concrete TenantContext olarak erişilebilir
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

        services.AddHttpContextAccessor();
        services.AddTransient<TenantPropagationHandler>();
        services.AddHttpClient("TenantAwareClient").AddHttpMessageHandler<TenantPropagationHandler>();
        services.AddScoped<ICurrentUserContext, UserContext.CurrentUserContext>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        return services;
    }

    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
    {
        app.UseMiddleware<TenantResolutionMiddleware>();
        return app;
    }

    public static IApplicationBuilder UseModuleSeedBootstrap(this IApplicationBuilder app)
    {
        app.UseMiddleware<ModuleSeedBootstrapMiddleware>();
        return app;
    }
}

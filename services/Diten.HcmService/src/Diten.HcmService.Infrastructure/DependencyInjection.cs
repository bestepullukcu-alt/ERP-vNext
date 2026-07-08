using Diten.HcmService.Application.Common;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster;
using Diten.HcmService.Infrastructure.Audit;
using Diten.HcmService.Infrastructure.Authorization;
using Diten.HcmService.Infrastructure.Middleware;
using Diten.HcmService.Infrastructure.ReferenceValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.HcmService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddHttpContextAccessor();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<IDraftAuditService, DraftAuditService>();
        services.AddHttpClient<IHcmAuditAppendClient, GovernedHcmAuditAppendClient>();
        services.AddHttpClient<IReferenceValidationClient, GatewayReferenceValidationClient>();

        return services;
    }

    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
    {
        app.UseMiddleware<TenantResolutionMiddleware>();
        return app;
    }
}

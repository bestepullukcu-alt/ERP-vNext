using Diten.AuthService.Application.Common.Behaviors;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.AuthService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ExceptionHandlingBehavior<,>));
            cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);
        services.AddScoped<IRoleProvisioningService, RoleProvisioningService>();
        services.AddScoped<IEntitlementPermissionSyncService, EntitlementPermissionSyncService>();
        services.AddScoped<IPasswordPolicyService, PasswordPolicyService>();
        services.AddScoped<IFullCatalogPermissionGrantService, FullCatalogPermissionGrantService>();
        services.AddScoped<IRbacAuditRecorder, RbacAuditRecorder>(); // FEAT-AUDIT-RBAC

        return services;
    }
}


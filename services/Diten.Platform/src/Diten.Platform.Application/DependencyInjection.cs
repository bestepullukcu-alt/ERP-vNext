using Diten.Platform.Application.Contracts.Behaviors;
using Diten.Platform.Application.Security;
using Diten.Platform.Application.Features.InterfaceRegistry.Auditing;
using Diten.Platform.Application.Features.Quotas.Services;
using Diten.Platform.Application.Services;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.Platform.Application;

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
            cfg.AddOpenBehavior(typeof(ExceptionBehavior<,>));
            cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);
        services.AddAutoMapper(_ => { }, assembly);
        services.AddSingleton<IInterfaceRegistryAuditSink, NullInterfaceRegistryAuditSink>();
        services.AddScoped<ITenantModuleAccessService, TenantModuleAccessService>();
        services.AddScoped<IActorSafetyGuard, ActorSafetyGuard>();
        services.AddScoped<IQuotaService, QuotaService>();

        return services;
    }
}

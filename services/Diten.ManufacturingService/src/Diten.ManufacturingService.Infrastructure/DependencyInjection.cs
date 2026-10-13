using Diten.ManufacturingService.Application.Common;
using Diten.ManufacturingService.Application.Interfaces;
using Diten.ManufacturingService.Infrastructure.Authorization;
using Diten.ManufacturingService.Infrastructure.LegalEntities;
using Diten.ManufacturingService.Infrastructure.Middleware;
using Diten.BuildingBlocks.Eventing;
using Diten.ManufacturingService.Infrastructure.Eventing;
using Diten.ManufacturingService.Infrastructure.ProductMaster;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.ManufacturingService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // TenantContext: hem ITenantContext hem de concrete TenantContext olarak erişilebilir.
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserContext, UserContext.CurrentUserContext>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        // PRODUCT-MASTER (MOD-0290) seam — pack §7. Permissive until the 0290 validate endpoint is live (F-0193-07).
        var mode = configuration["ProductMaster:Mode"] ?? "Permissive";
        if (string.Equals(mode, "Http", StringComparison.OrdinalIgnoreCase))
        {
            var baseUrl = configuration["ProductMaster:BaseUrl"]
                ?? throw new InvalidOperationException("ProductMaster:BaseUrl is required when ProductMaster:Mode is Http.");
            services.AddHttpClient<IProductReferenceValidator, HttpProductReferenceValidator>(client =>
            {
                client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(5);
            });
        }
        else if (string.Equals(mode, "Permissive", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IProductReferenceValidator, PermissiveProductReferenceValidator>();
        }
        else
        {
            throw new InvalidOperationException($"ProductMaster:Mode '{mode}' is not supported (Permissive | Http).");
        }

        // MDM legal-entity proof (MVP-1 pattern) — no permissive mode and no bypass: every request names a legal entity
        // and MDM confirms it belongs to the tenant and is ACTIVE (fail-closed; LegalEntityValidationMiddleware).
        services.AddHttpClient<ILegalEntityReferenceValidator, MdmLegalEntityReferenceValidator>();

        // MOD-0209 Change Control seam — waiver W-0193-01 (format-only until 0209 exists).
        services.AddSingleton<IChangeControlGate, FormatOnlyChangeControlGate>();

        AddBomEventTransport(services, configuration);
        return services;
    }

    /// <summary>
    /// BOM-version events (contract v1.2.0): the outbox is always written (journal, same transaction); the broker
    /// transport is opt-in — <c>EventTransport:Enabled=true</c> with a RabbitMQ host and credentials, using the
    /// repository's existing MassTransit pattern. Off (default): no transport is registered and the publisher worker
    /// reports that events stay Pending. Enabled but incomplete: startup fails instead of silently not delivering.
    /// </summary>
    private static void AddBomEventTransport(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHostedService<BomOutboxPublisherWorker>();
        var section = configuration.GetSection("EventTransport");
        if (!section.GetValue("Enabled", false))
        {
            return;
        }

        var host = section["RabbitMqHost"];
        var user = section["RabbitMqUsername"];
        var password = section["RabbitMqPassword"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "EventTransport:Enabled is true but EventTransport:RabbitMqHost / RabbitMqUsername / RabbitMqPassword are not all set.");
        }

        var port = (ushort)section.GetValue("RabbitMqPort", 5672);
        var virtualHost = section["RabbitMqVirtualHost"] ?? "/";
        services.AddMassTransit(bus => bus.UsingRabbitMq((context, rabbit) =>
        {
            rabbit.Host(host, port, virtualHost, h =>
            {
                h.Username(user);
                h.Password(password);
            });
            rabbit.ConfigureEndpoints(context);
        }));
        services.AddScoped<IEventTransportPublisher, MassTransitBomEventTransportPublisher>();
    }

    public static IApplicationBuilder UseCorrelation(this IApplicationBuilder app) => app.UseMiddleware<CorrelationMiddleware>();

    public static IApplicationBuilder UseLegalEntityValidation(this IApplicationBuilder app) => app.UseMiddleware<LegalEntityValidationMiddleware>();

    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
    {
        app.UseMiddleware<TenantResolutionMiddleware>();
        return app;
    }
}

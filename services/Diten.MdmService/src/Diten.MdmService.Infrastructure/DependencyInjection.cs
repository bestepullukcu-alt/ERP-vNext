using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Infrastructure.Authorization;
using Diten.MdmService.Infrastructure.Middleware;
using Diten.MdmService.Infrastructure.Security;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Infrastructure.ReferenceData;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.MdmService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddHttpContextAccessor();
        services.AddScoped<IProductIdentityActorContext, ProductIdentityActorContext>();
        services.AddScoped<IProductAbbreviationActorContext, ProductAbbreviationActorContext>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.Configure<VerifiedGskuResolverOptions>(
            configuration.GetSection(VerifiedGskuResolverOptions.SectionName));
        services.AddHttpClient<IVerifiedGskuReferenceResolver, PlatformVerifiedGskuResolverClient>();
        services.Configure<VerifiedMarketResolverOptions>(
            configuration.GetSection(VerifiedMarketResolverOptions.SectionName));
        services.AddHttpClient<IVerifiedMarketReferenceResolver, PlatformVerifiedMarketResolverClient>();

        // WP-VW-W4 (W4-BE-b1) — MOD-0023 approvals inside the MDM (the CrmService WP-CL-BE-4 / W3-BE-c wiring, copied): the
        // template / reconcile configuration, the Gateway workflow client (caller's token + tenant forwarded, never a service
        // token), the approval actor, the outcome applier (each approvable kind registers a WorkflowApprovalTarget — W4-BE-b2
        // adds the competitor proposal), the flow, reconcile-on-read, and the completion-event consumer (only when
        // Eventing:Transport=RabbitMQ).
        services.AddSingleton<Application.Features.Approvals.IMdmApprovalSettings, Workflow.ConfigurationMdmApprovalSettings>();
        services.AddHttpClient<Application.Features.Approvals.IMdmWorkflowClient, Workflow.GatewayWorkflowClient>(
            client => client.Timeout = TimeSpan.FromSeconds(15));
        services.AddScoped<Application.Features.Approvals.IMdmActorContext, MdmActorContext>();
        services.AddScoped<Application.Features.Approvals.WorkflowApprovalOutcomeApplier>();
        services.AddScoped<Application.Features.Approvals.WorkflowApprovalFlow>();
        services.AddScoped<Application.Features.Approvals.WorkflowApprovalReconciler>();
        // WP-VW-W4 (W4-BE-b2) — the reimbursement status set read (Gateway, caller's token) and the permission question.
        services.AddHttpClient<IMdmReferenceValueReader, GatewayReferenceValueReader>(client => client.Timeout = TimeSpan.FromSeconds(15));
        services.AddScoped<IMdmPermissionContext, MdmPermissionContext>();
        AddWorkflowOutcomeEventing(services, configuration);

        return services;
    }

    // WP-VW-W4 (W4-BE-b1) — the CrmService AddClaimWorkflowEventing pattern. Transport=InMemory (default) ⇒ MassTransit is
    // NOT registered at all, so a host without a broker starts exactly as before. RabbitMQ (same broker as Platform) ⇒ the
    // outcome consumer binds to Platform's EventTransportMessage exchange on its own queue (MdmWorkflowOutcome). MassTransit
    // connects in the background: a broker that is down is retried, it never blocks the start-up.
    internal static void AddWorkflowOutcomeEventing(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<Eventing.MdmEventingOptions>(configuration.GetSection(Eventing.MdmEventingOptions.SectionName));
        var options = configuration.GetSection(Eventing.MdmEventingOptions.SectionName).Get<Eventing.MdmEventingOptions>()
                      ?? new Eventing.MdmEventingOptions();
        if (!options.UseRabbitMq)
        {
            return;
        }

        services.AddMassTransit(x =>
        {
            x.AddConsumer<Eventing.MdmWorkflowOutcomeConsumer>();
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(options.Host, options.Port, options.VirtualHost, h =>
                {
                    h.Username(options.Username);
                    h.Password(options.Password);
                    if (options.UseTls)
                    {
                        h.UseSsl(s => s.Protocol = System.Security.Authentication.SslProtocols.Tls12);
                    }
                });

                cfg.UseMessageRetry(r => r.Exponential(
                    options.RetryCount,
                    TimeSpan.FromSeconds(options.InitialRetryDelaySeconds),
                    TimeSpan.FromSeconds(options.MaxRetryDelaySeconds),
                    TimeSpan.FromSeconds(options.InitialRetryDelaySeconds)));
                cfg.ConfigureEndpoints(context);
            });
        });
    }

    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
    {
        app.UseMiddleware<TenantResolutionMiddleware>();
        return app;
    }
}

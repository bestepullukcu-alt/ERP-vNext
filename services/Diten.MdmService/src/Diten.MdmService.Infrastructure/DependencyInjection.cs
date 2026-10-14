using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Contracts.Authorization;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Audit;
using Diten.MdmService.Infrastructure.Authorization;
using Diten.MdmService.Infrastructure.Audit;
using Diten.MdmService.Infrastructure.Middleware;
using Diten.MdmService.Infrastructure.Security;
using Diten.MdmService.Infrastructure.Workflow;
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
        services.AddScoped<IProductIdentityLifecycleActorContext, ProductIdentityLifecycleActorContext>();
        services.AddScoped<IFinishedGoodHumanAdmissionContext, FinishedGoodHumanAdmissionContext>();
        services.AddScoped<IProductAbbreviationActorContext, ProductAbbreviationActorContext>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.Configure<VerifiedGskuResolverOptions>(
            configuration.GetSection(VerifiedGskuResolverOptions.SectionName));
        services.AddHttpClient<IVerifiedGskuReferenceResolver, PlatformVerifiedGskuResolverClient>();
        // MOD-0290-FU04 S1: no provider for the item lists and no tenant-wide stock query yet — both fail closed.
        // WP-PLT-ITEM-LISTS-01 (CT decision A): the item lists come from Platform's item universal catalog (ITEM-UNIVERSAL-Vn) resolver — always
        // this client, like the GSKU / market clients. Without its "VerifiedGskuResolver" configuration it fails closed AT
        // THE CALL (the item answers 503 ITEM_REFERENCE_LIST_UNAVAILABLE + REFERENCE_PROVIDER_CONFIGURATION_INVALID, with a
        // warning); the start is not refused (ItemReferenceProviderStartupWarning says it once outside Development).
        // FIX1 R1 (the rule of this file): no redirect is followed — the credential and the caller's token never reach
        // another host — and the secret headers are masked in the HTTP client logs.
        services.AddHttpClient<IItemReferenceResolver, PlatformItemReferenceResolverClient>()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
            .RedactLoggedHeaders([
                "Authorization",
                PlatformItemReferenceResolverClient.CredentialIdHeader,
                PlatformItemReferenceResolverClient.CredentialSecretHeader,
                PlatformItemReferenceResolverClient.AudienceHeader
            ]);
        // S2 FIX1 item 1: the approval worker reads the same lists with MDM's service identity (no user request there).
        services.AddHttpClient(nameof(PlatformWorkflowItemReferenceResolverClient), client =>
            {
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
            .RedactLoggedHeaders([
                "Authorization",
                PlatformItemReferenceResolverClient.CredentialIdHeader,
                PlatformItemReferenceResolverClient.CredentialSecretHeader,
                PlatformItemReferenceResolverClient.AudienceHeader
            ]);
        services.AddScoped<IWorkflowItemReferenceResolver, PlatformWorkflowItemReferenceResolverClient>();
        services.AddSingleton<Diten.MdmService.Application.Contracts.Inventory.IInventoryStockAvailabilityClient,
            Diten.MdmService.Infrastructure.Inventory.UnavailableInventoryStockAvailabilityClient>();
        services.Configure<VerifiedMarketResolverOptions>(
            configuration.GetSection(VerifiedMarketResolverOptions.SectionName));
        services.AddHttpClient<IVerifiedMarketReferenceResolver, PlatformVerifiedMarketResolverClient>();
        services.Configure<AuthWorkflowVerifiedMarketServiceIdentityProviderOptions>(
            configuration.GetSection(AuthWorkflowVerifiedMarketServiceIdentityProviderOptions.SectionName));
        services.AddHttpClient(nameof(AuthWorkflowVerifiedMarketServiceIdentityProvider), client =>
            {
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
            .RedactLoggedHeaders([
                AuthWorkflowVerifiedMarketServiceIdentityProvider.ClientIdHeader,
                AuthWorkflowVerifiedMarketServiceIdentityProvider.ClientSecretHeader
            ]);
        services.AddHttpClient(nameof(PlatformWorkflowVerifiedGskuResolverClient), client =>
            {
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
            .RedactLoggedHeaders([
                "Authorization",
                PlatformWorkflowVerifiedGskuResolverClient.CredentialIdHeader,
                PlatformWorkflowVerifiedGskuResolverClient.CredentialSecretHeader,
                PlatformWorkflowVerifiedGskuResolverClient.AudienceHeader
            ]);
        services.AddSingleton<IWorkflowVerifiedMarketServiceIdentityProvider,
            AuthWorkflowVerifiedMarketServiceIdentityProvider>();
        services.AddScoped<IWorkflowVerifiedGskuReferenceResolver,
            PlatformWorkflowVerifiedGskuResolverClient>();
        services.AddHttpClient(nameof(PlatformWorkflowVerifiedMarketResolverClient), client =>
            {
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
            .RedactLoggedHeaders([
                "Authorization",
                PlatformWorkflowVerifiedMarketResolverClient.CredentialIdHeader,
                PlatformWorkflowVerifiedMarketResolverClient.CredentialSecretHeader,
                PlatformWorkflowVerifiedMarketResolverClient.AudienceHeader
            ]);
        services.AddScoped<IWorkflowVerifiedMarketReferenceResolver, PlatformWorkflowVerifiedMarketResolverClient>();
        services.Configure<TrustedLegalEntityScopeProviderOptions>(
            configuration.GetSection(TrustedLegalEntityScopeProviderOptions.SectionName));
        services.AddHttpClient<ITrustedLegalEntityScopeProvider, PlatformTrustedLegalEntityScopeProviderClient>(client =>
            {
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false
            })
            .RedactLoggedHeaders([
                "Authorization",
                PlatformTrustedLegalEntityScopeProviderClient.CredentialIdHeader,
                PlatformTrustedLegalEntityScopeProviderClient.CredentialSecretHeader,
                PlatformTrustedLegalEntityScopeProviderClient.AudienceHeader
            ]);
        services.AddScoped<ProductLegalEntityScopeCandidateFacade>();
        services.AddScoped<IProductLegalEntityScopeWriterAuthorityProvider,
            ProductLegalEntityScopeWriterAuthorityProvider>();
        services.Configure<AuthTrustedSourceAuditServiceIdentityProviderOptions>(configuration.GetSection(AuthTrustedSourceAuditServiceIdentityProviderOptions.SectionName));
        services.Configure<TrustedSourceAuditIntentClientOptions>(configuration.GetSection(TrustedSourceAuditIntentClientOptions.SectionName));
        services.AddHttpClient(nameof(AuthTrustedSourceAuditServiceIdentityProvider), client => client.Timeout = Timeout.InfiniteTimeSpan).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false }).RedactLoggedHeaders([AuthTrustedSourceAuditServiceIdentityProvider.ClientIdHeader, AuthTrustedSourceAuditServiceIdentityProvider.ClientSecretHeader]);
        services.AddHttpClient(nameof(PlatformTrustedSourceAuditIntentClient), client => client.Timeout = Timeout.InfiniteTimeSpan).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false }).RedactLoggedHeaders(["Authorization"]);
        services.AddSingleton<ITrustedSourceAuditServiceIdentityProvider, AuthTrustedSourceAuditServiceIdentityProvider>();
        services.AddScoped<ITrustedSourceAuditIntentClient, PlatformTrustedSourceAuditIntentClient>();
        services.AddScoped<AuditIntentDeliveryProcessor>();
        services.Configure<AuthProductIdentityWorkflowServiceIdentityProviderOptions>(
            configuration.GetSection(AuthProductIdentityWorkflowServiceIdentityProviderOptions.SectionName));
        services.Configure<ProductIdentityWorkflowClientOptions>(
            configuration.GetSection(ProductIdentityWorkflowClientOptions.SectionName));
        services.AddSingleton<IProductIdentityWorkflowTransportReadiness, ProductIdentityWorkflowTransportReadiness>();
        services.AddHttpClient(nameof(AuthProductIdentityWorkflowServiceIdentityProvider), client =>
            {
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
            .RedactLoggedHeaders([
                AuthProductIdentityWorkflowServiceIdentityProvider.ClientIdHeader,
                AuthProductIdentityWorkflowServiceIdentityProvider.ClientSecretHeader
            ]);
        services.AddHttpClient(nameof(PlatformProductIdentityWorkflowClient), client =>
            {
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
            .RedactLoggedHeaders([
                "Authorization",
                PlatformProductIdentityWorkflowClient.DelegatedAuthorizationHeader,
                PlatformProductIdentityWorkflowClient.IdempotencyKeyHeader
            ]);
        services.AddSingleton<IProductIdentityWorkflowServiceIdentityProvider,
            AuthProductIdentityWorkflowServiceIdentityProvider>();
        services.AddScoped<IProductIdentityWorkflowClient, PlatformProductIdentityWorkflowClient>();
        services.AddScoped<IProductIdentityDelegatedTokenAccessor,
            HttpContextProductIdentityDelegatedTokenAccessor>();
        // MOD-0290-FU04 S3 — the stock-behaviour commands read the same two MOD-0048 item lists as the Item (S1's
        // contract, fail-closed until Platform publishes them). TryAdd: S1 registers the same pair; whichever is first wins.
        Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddSingleton<
            IItemReferenceResolver, UnavailableItemReferenceResolver>(services);

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

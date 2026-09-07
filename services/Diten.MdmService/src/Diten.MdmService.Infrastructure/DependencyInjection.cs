using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Contracts.Authorization;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Audit;
using Diten.MdmService.Infrastructure.Authorization;
using Diten.MdmService.Infrastructure.Audit;
using Diten.MdmService.Infrastructure.Middleware;
using Diten.MdmService.Infrastructure.Security;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Infrastructure.ReferenceData;
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
        services.Configure<AuthTrustedSourceAuditServiceIdentityProviderOptions>(configuration.GetSection(AuthTrustedSourceAuditServiceIdentityProviderOptions.SectionName));
        services.Configure<TrustedSourceAuditIntentClientOptions>(configuration.GetSection(TrustedSourceAuditIntentClientOptions.SectionName));
        services.AddHttpClient(nameof(AuthTrustedSourceAuditServiceIdentityProvider), client => client.Timeout = Timeout.InfiniteTimeSpan).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false }).RedactLoggedHeaders([AuthTrustedSourceAuditServiceIdentityProvider.ClientIdHeader, AuthTrustedSourceAuditServiceIdentityProvider.ClientSecretHeader]);
        services.AddHttpClient(nameof(PlatformTrustedSourceAuditIntentClient), client => client.Timeout = Timeout.InfiniteTimeSpan).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false }).RedactLoggedHeaders(["Authorization"]);
        services.AddSingleton<ITrustedSourceAuditServiceIdentityProvider, AuthTrustedSourceAuditServiceIdentityProvider>();
        services.AddScoped<ITrustedSourceAuditIntentClient, PlatformTrustedSourceAuditIntentClient>();
        services.AddScoped<AuditIntentDeliveryProcessor>();

        return services;
    }

    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
    {
        app.UseMiddleware<TenantResolutionMiddleware>();
        return app;
    }
}

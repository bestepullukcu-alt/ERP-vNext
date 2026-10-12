using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Infrastructure.Authorization;
using Diten.InventoryService.Infrastructure.Middleware;
using Diten.InventoryService.Infrastructure.Upstream;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Diten.InventoryService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<CorrelationContext>();
        services.AddScoped<ICorrelationContext>(sp => sp.GetRequiredService<CorrelationContext>());

        services.AddHttpContextAccessor();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ContractAuthorizationResultHandler>();
        // WP-0173-PM2: service keys of the internal operations (own key pairs, Inventory:InternalServiceKeys).
        services.AddSingleton<InternalAccess.InternalServiceKeyAuthenticator>();

        // ── Policy: the Location-LE check may be off ONLY in the Mock environment (ASSUMPTION-0173-07). ──
        var policy = configuration.GetSection(InventoryPolicyOptions.SectionName).Get<InventoryPolicyOptions>() ?? new InventoryPolicyOptions();
        if (!policy.EnforceLocationLegalEntityMatch && !environment.IsEnvironment(InventoryPolicyOptions.MockEnvironmentName))
        {
            throw new InvalidOperationException(
                $"'{InventoryPolicyOptions.SectionName}:EnforceLocationLegalEntityMatch' may be false only in the " +
                $"'{InventoryPolicyOptions.MockEnvironmentName}' environment (current: '{environment.EnvironmentName}').");
        }

        services.Configure<InventoryPolicyOptions>(configuration.GetSection(InventoryPolicyOptions.SectionName));

        // ── Upstream masters (frozen contracts, HTTP only, fail-closed). ──
        var upstream = configuration.GetSection(UpstreamServicesOptions.SectionName).Get<UpstreamServicesOptions>() ?? new UpstreamServicesOptions();
        var productMaster = RequireAbsolute(upstream.ProductMasterBaseUrl, "ProductMasterBaseUrl");
        var locationMaster = RequireAbsolute(upstream.LocationMasterBaseUrl, "LocationMasterBaseUrl");
        var legalEntityMaster = RequireAbsolute(upstream.LegalEntityMasterBaseUrl, "LegalEntityMasterBaseUrl");
        var orgUnitDirectory = RequireAbsolute(upstream.OrgUnitDirectoryBaseUrl, "OrgUnitDirectoryBaseUrl");
        var timeout = TimeSpan.FromSeconds(upstream.TimeoutSeconds > 0 ? upstream.TimeoutSeconds : 5);

        services.AddTransient<ForwardCallerHeadersHandler>();
        services.AddHttpClient<IProductMasterClient, ProductMasterHttpClient>(http =>
            {
                http.BaseAddress = productMaster;
                http.Timeout = timeout;
            })
            .AddHttpMessageHandler<ForwardCallerHeadersHandler>();
        services.AddHttpClient<ILocationMasterClient, LocationMasterHttpClient>(http =>
            {
                http.BaseAddress = locationMaster;
                http.Timeout = timeout;
            })
            .AddHttpMessageHandler<ForwardCallerHeadersHandler>();
        // W-0173-02: MDM legal-entity master through the Gateway, caller token forwarded; membership answers cached <= 60 s.
        services.AddHttpClient<ILegalEntityMasterClient, LegalEntityMasterHttpClient>(http =>
            {
                http.BaseAddress = legalEntityMaster;
                http.Timeout = timeout;
            })
            .AddHttpMessageHandler<ForwardCallerHeadersHandler>();
        // MOD-0173-FU02 §7: org-unit soft link checked once on a location write, caller token forwarded; never cached.
        services.AddHttpClient<IOrgUnitDirectoryClient, OrgUnitDirectoryHttpClient>(http =>
            {
                http.BaseAddress = orgUnitDirectory;
                http.Timeout = timeout;
            })
            .AddHttpMessageHandler<ForwardCallerHeadersHandler>();
        services.AddMemoryCache();

        return services;
    }

    /// <summary>Correlation first: every response, refusals included, carries the id.</summary>
    public static IApplicationBuilder UseInventoryCorrelation(this IApplicationBuilder app)
        => app.UseMiddleware<CorrelationIdMiddleware>();

    /// <summary>After authentication, before authorization.</summary>
    public static IApplicationBuilder UseInventoryScopeResolution(this IApplicationBuilder app)
        => app.UseMiddleware<InventoryScopeResolutionMiddleware>();

    private static Uri RequireAbsolute(string value, string name)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"Configuration error: '{UpstreamServicesOptions.SectionName}:{name}' must be an absolute URL.");
        }

        // Relative contract paths ("products/{id}") resolve under the base only when it ends with '/'.
        return uri.AbsoluteUri.EndsWith('/') ? uri : new Uri(uri.AbsoluteUri + "/");
    }
}

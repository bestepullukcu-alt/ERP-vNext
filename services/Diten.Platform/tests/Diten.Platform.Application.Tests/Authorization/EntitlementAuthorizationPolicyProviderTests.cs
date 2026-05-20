using Diten.Platform.Application.Services;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

public sealed class EntitlementAuthorizationPolicyProviderTests
{
    [Fact]
    public async Task GetPolicyAsync_creates_module_policy_with_tenant_module_requirement()
    {
        var provider = CreateProvider();

        var policy = await provider.GetPolicyAsync("RequiresModule:HR");

        Assert.NotNull(policy);
        var requirement = Assert.Single(policy.Requirements.OfType<TenantModuleRequirement>());
        Assert.Equal("HR", requirement.ModuleCode);
    }

    [Fact]
    public async Task GetPolicyAsync_creates_feature_policy_with_tenant_feature_requirement()
    {
        var provider = CreateProvider();

        var policy = await provider.GetPolicyAsync("RequiresFeature:ADVANCED_REPORTING");

        Assert.NotNull(policy);
        var requirement = Assert.Single(policy.Requirements.OfType<TenantFeatureRequirement>());
        Assert.Equal("ADVANCED_REPORTING", requirement.FeatureCode);
    }

    [Fact]
    public async Task GetPolicyAsync_preserves_default_provider_for_unknown_policy()
    {
        var provider = CreateProvider();

        var policy = await provider.GetPolicyAsync("UnknownPolicy");

        Assert.Null(policy);
    }

    [Fact]
    public async Task GetPolicyAsync_empty_module_target_falls_back_without_throwing()
    {
        var provider = CreateProvider();

        var exception = await Record.ExceptionAsync(() => provider.GetPolicyAsync("RequiresModule:"));

        Assert.Null(exception);
    }

    [Fact]
    public void Entitlement_authorization_services_can_be_resolved_from_service_collection()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddOptions();
        services.Configure<EntitlementCacheOptions>(options => options.CacheTtlSeconds = 300);
        services.AddSingleton<IAuthorizationPolicyProvider, EntitlementAuthorizationPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, TenantModuleAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, TenantFeatureAuthorizationHandler>();
        services.AddSingleton<IEntitlementAuditSink, NullEntitlementAuditSink>();
        services.AddSingleton<EntitlementCacheService>();
        services.AddScoped<IEntitlementChecker, EntitlementChecker>();
        services.AddScoped(_ => Mock.Of<ITenantModuleAccessService>());
        services.AddScoped(_ => Mock.Of<ITenantSubscriptionRepository>());
        services.AddScoped(_ => Mock.Of<ISubscriptionPlanRepository>());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<EntitlementAuthorizationPolicyProvider>(
            scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>());
        Assert.Contains(
            scope.ServiceProvider.GetRequiredService<IEnumerable<IAuthorizationHandler>>(),
            handler => handler is TenantModuleAuthorizationHandler);
        Assert.Contains(
            scope.ServiceProvider.GetRequiredService<IEnumerable<IAuthorizationHandler>>(),
            handler => handler is TenantFeatureAuthorizationHandler);
        Assert.IsType<NullEntitlementAuditSink>(
            scope.ServiceProvider.GetRequiredService<IEntitlementAuditSink>());
        Assert.IsType<EntitlementChecker>(
            scope.ServiceProvider.GetRequiredService<IEntitlementChecker>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<EntitlementCacheService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IMemoryCache>());
    }

    private static EntitlementAuthorizationPolicyProvider CreateProvider()
    {
        return new EntitlementAuthorizationPolicyProvider(Options.Create(new AuthorizationOptions()));
    }
}

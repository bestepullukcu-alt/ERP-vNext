using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements;
using Diten.Platform.Application.Services;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

public sealed class EntitlementCheckerFailureSemanticsTests
{
    private static readonly Guid TenantId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task IsModuleEntitledAsync_returns_safe_deny_for_empty_module_code(string? moduleCode)
    {
        var moduleAccessService = new Mock<ITenantModuleAccessService>();
        var checker = CreateChecker(moduleAccessService: moduleAccessService);

        var result = await checker.IsModuleEntitledAsync(TenantId, moduleCode!, CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.True(result.IsCacheable);
        Assert.Equal(EntitlementKind.Module, result.Kind);
        Assert.Equal(EntitlementDenyReason.ModuleNotEntitled, result.DenyReason);
        Assert.False(string.IsNullOrWhiteSpace(result.Code));
        moduleAccessService.Verify(
            x => x.GetEffectiveAccessDetailAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task IsFeatureEnabledAsync_returns_safe_deny_for_empty_feature_code(string? featureCode)
    {
        var tenantSubscriptionRepository = new Mock<ITenantSubscriptionRepository>();
        var checker = CreateChecker(tenantSubscriptionRepository: tenantSubscriptionRepository);

        var result = await checker.IsFeatureEnabledAsync(TenantId, featureCode!, CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.True(result.IsCacheable);
        Assert.Equal(EntitlementKind.Feature, result.Kind);
        Assert.Equal(EntitlementDenyReason.FeatureNotEnabled, result.DenyReason);
        Assert.False(string.IsNullOrWhiteSpace(result.Code));
        tenantSubscriptionRepository.Verify(
            x => x.GetCurrentByTenantIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task IsModuleEntitledAsync_does_not_cache_transient_dependency_failure()
    {
        var moduleAccessService = new Mock<ITenantModuleAccessService>();
        moduleAccessService
            .Setup(x => x.GetEffectiveAccessDetailAsync(TenantId, "HR", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));
        var checker = CreateChecker(moduleAccessService: moduleAccessService);

        var first = await checker.IsModuleEntitledAsync(TenantId, "HR", CancellationToken.None);
        var second = await checker.IsModuleEntitledAsync(TenantId, "HR", CancellationToken.None);

        Assert.False(first.IsAllowed);
        Assert.False(second.IsAllowed);
        Assert.False(first.IsCacheable);
        Assert.False(second.IsCacheable);
        moduleAccessService.Verify(
            x => x.GetEffectiveAccessDetailAsync(TenantId, "HR", It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task IsFeatureEnabledAsync_does_not_cache_transient_dependency_failure()
    {
        var tenantSubscriptionRepository = new Mock<ITenantSubscriptionRepository>();
        tenantSubscriptionRepository
            .Setup(x => x.GetCurrentByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));
        var checker = CreateChecker(tenantSubscriptionRepository: tenantSubscriptionRepository);

        var first = await checker.IsFeatureEnabledAsync(TenantId, "ADVANCED_REPORTING", CancellationToken.None);
        var second = await checker.IsFeatureEnabledAsync(TenantId, "ADVANCED_REPORTING", CancellationToken.None);

        Assert.False(first.IsAllowed);
        Assert.False(second.IsAllowed);
        Assert.False(first.IsCacheable);
        Assert.False(second.IsCacheable);
        tenantSubscriptionRepository.Verify(
            x => x.GetCurrentByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task EntitlementCacheService_caches_business_deny_results()
    {
        var cacheService = CreateCacheService();
        var factoryCalls = 0;

        var first = await cacheService.GetOrCreateModuleAsync(
            TenantId,
            "HR",
            () =>
            {
                factoryCalls++;
                return Task.FromResult(EntitlementCheckResult.Denied(
                    EntitlementKind.Module,
                    "HR",
                    EntitlementDenyReason.ModuleNotEntitled));
            });
        var second = await cacheService.GetOrCreateModuleAsync(
            TenantId,
            "HR",
            () =>
            {
                factoryCalls++;
                return Task.FromResult(EntitlementCheckResult.Allowed(EntitlementKind.Module, "HR"));
            });

        Assert.False(first.IsAllowed);
        Assert.False(second.IsAllowed);
        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public async Task EntitlementCacheService_evicts_module_cache_by_tenant_and_module_code()
    {
        var cacheService = CreateCacheService();
        var factoryCalls = 0;

        await cacheService.GetOrCreateModuleAsync(TenantId, "HR", CreateAllowedModuleFactory("HR", () => factoryCalls++));
        await cacheService.GetOrCreateModuleAsync(TenantId, "HR", CreateAllowedModuleFactory("HR", () => factoryCalls++));

        cacheService.EvictModule(TenantId, "hr");

        await cacheService.GetOrCreateModuleAsync(TenantId, "HR", CreateAllowedModuleFactory("HR", () => factoryCalls++));

        Assert.Equal(2, factoryCalls);
    }

    [Fact]
    public async Task EntitlementCacheService_evicts_feature_cache_by_tenant_and_feature_code()
    {
        var cacheService = CreateCacheService();
        var factoryCalls = 0;

        await cacheService.GetOrCreateFeatureAsync(TenantId, "ADVANCED_REPORTING", CreateAllowedFeatureFactory("ADVANCED_REPORTING", () => factoryCalls++));
        await cacheService.GetOrCreateFeatureAsync(TenantId, "ADVANCED_REPORTING", CreateAllowedFeatureFactory("ADVANCED_REPORTING", () => factoryCalls++));

        cacheService.EvictFeature(TenantId, "advanced_reporting");

        await cacheService.GetOrCreateFeatureAsync(TenantId, "ADVANCED_REPORTING", CreateAllowedFeatureFactory("ADVANCED_REPORTING", () => factoryCalls++));

        Assert.Equal(2, factoryCalls);
    }

    [Fact]
    public async Task EntitlementCacheService_evicts_all_module_and_feature_cache_for_tenant_only()
    {
        var otherTenantId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var cacheService = CreateCacheService();
        var tenantModuleCalls = 0;
        var tenantFeatureCalls = 0;
        var otherTenantModuleCalls = 0;
        var otherTenantFeatureCalls = 0;

        await cacheService.GetOrCreateModuleAsync(TenantId, "HR", CreateAllowedModuleFactory("HR", () => tenantModuleCalls++));
        await cacheService.GetOrCreateFeatureAsync(TenantId, "ADVANCED_REPORTING", CreateAllowedFeatureFactory("ADVANCED_REPORTING", () => tenantFeatureCalls++));
        await cacheService.GetOrCreateModuleAsync(otherTenantId, "HR", CreateAllowedModuleFactory("HR", () => otherTenantModuleCalls++));
        await cacheService.GetOrCreateFeatureAsync(otherTenantId, "ADVANCED_REPORTING", CreateAllowedFeatureFactory("ADVANCED_REPORTING", () => otherTenantFeatureCalls++));

        cacheService.EvictTenant(TenantId);

        await cacheService.GetOrCreateModuleAsync(TenantId, "HR", CreateAllowedModuleFactory("HR", () => tenantModuleCalls++));
        await cacheService.GetOrCreateFeatureAsync(TenantId, "ADVANCED_REPORTING", CreateAllowedFeatureFactory("ADVANCED_REPORTING", () => tenantFeatureCalls++));
        await cacheService.GetOrCreateModuleAsync(otherTenantId, "HR", CreateAllowedModuleFactory("HR", () => otherTenantModuleCalls++));
        await cacheService.GetOrCreateFeatureAsync(otherTenantId, "ADVANCED_REPORTING", CreateAllowedFeatureFactory("ADVANCED_REPORTING", () => otherTenantFeatureCalls++));

        Assert.Equal(2, tenantModuleCalls);
        Assert.Equal(2, tenantFeatureCalls);
        Assert.Equal(1, otherTenantModuleCalls);
        Assert.Equal(1, otherTenantFeatureCalls);
    }

    [Fact]
    public async Task EntitlementCacheService_keeps_existing_cache_hit_behavior_for_modules_and_features()
    {
        var cacheService = CreateCacheService();
        var moduleFactoryCalls = 0;
        var featureFactoryCalls = 0;

        await cacheService.GetOrCreateModuleAsync(TenantId, "HR", CreateAllowedModuleFactory("HR", () => moduleFactoryCalls++));
        await cacheService.GetOrCreateModuleAsync(TenantId, "HR", CreateAllowedModuleFactory("HR", () => moduleFactoryCalls++));
        await cacheService.GetOrCreateFeatureAsync(TenantId, "ADVANCED_REPORTING", CreateAllowedFeatureFactory("ADVANCED_REPORTING", () => featureFactoryCalls++));
        await cacheService.GetOrCreateFeatureAsync(TenantId, "ADVANCED_REPORTING", CreateAllowedFeatureFactory("ADVANCED_REPORTING", () => featureFactoryCalls++));

        Assert.Equal(1, moduleFactoryCalls);
        Assert.Equal(1, featureFactoryCalls);
    }

    private static EntitlementChecker CreateChecker(
        Mock<ITenantModuleAccessService>? moduleAccessService = null,
        Mock<ITenantSubscriptionRepository>? tenantSubscriptionRepository = null,
        Mock<ISubscriptionPlanRepository>? subscriptionPlanRepository = null)
    {
        if (moduleAccessService is null)
        {
            moduleAccessService = new Mock<ITenantModuleAccessService>();
            moduleAccessService
                .Setup(x => x.GetEffectiveAccessDetailAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid tenantId, string moduleCode, CancellationToken _) => new TenantModuleEffectiveAccessDto(
                    tenantId,
                    moduleCode,
                    moduleCode,
                    "None",
                    TenantModuleEffectiveAccess.NoAccess,
                    HasAccess: false,
                    null,
                    null));
        }

        if (tenantSubscriptionRepository is null)
        {
            tenantSubscriptionRepository = new Mock<ITenantSubscriptionRepository>();
            tenantSubscriptionRepository
                .Setup(x => x.GetCurrentByTenantIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TenantSubscription { TenantId = TenantId, PlanId = Guid.Parse("55555555-5555-5555-5555-555555555555") });
        }

        subscriptionPlanRepository ??= new Mock<ISubscriptionPlanRepository>();
        subscriptionPlanRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionPlan { IncludedFeatures = [] });

        return new EntitlementChecker(
            moduleAccessService.Object,
            tenantSubscriptionRepository.Object,
            subscriptionPlanRepository.Object,
            CreateCacheService());
    }

    private static EntitlementCacheService CreateCacheService()
    {
        return new EntitlementCacheService(
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new EntitlementCacheOptions { CacheTtlSeconds = 300 }));
    }

    private static Func<Task<EntitlementCheckResult>> CreateAllowedModuleFactory(string moduleCode, Action beforeReturn)
    {
        return () =>
        {
            beforeReturn();
            return Task.FromResult(EntitlementCheckResult.Allowed(EntitlementKind.Module, moduleCode));
        };
    }

    private static Func<Task<EntitlementCheckResult>> CreateAllowedFeatureFactory(string featureCode, Action beforeReturn)
    {
        return () =>
        {
            beforeReturn();
            return Task.FromResult(EntitlementCheckResult.Allowed(EntitlementKind.Feature, featureCode));
        };
    }
}

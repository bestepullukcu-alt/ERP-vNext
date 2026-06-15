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

public sealed class EntitlementResolutionSourcePropagationTests
{
    private static readonly Guid TenantId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid PlanId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    [Theory]
    [InlineData("System", EntitlementResolutionSource.Plan)]
    [InlineData("ManualOverride", EntitlementResolutionSource.Override)]
    [InlineData("Addon", EntitlementResolutionSource.Addon)]
    [InlineData("Trial", EntitlementResolutionSource.Trial)]
    [InlineData("Plan", EntitlementResolutionSource.Plan)]
    public async Task IsModuleEntitledAsync_maps_allow_source_to_resolution_source(string detailSource, EntitlementResolutionSource expected)
    {
        var moduleAccessService = new Mock<ITenantModuleAccessService>();
        moduleAccessService
            .Setup(x => x.GetEffectiveAccessDetailAsync(TenantId, "HR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantModuleEffectiveAccessDto(
                TenantId,
                "HR",
                "HR",
                detailSource,
                TenantModuleEffectiveAccess.Active,
                HasAccess: true,
                Reason: null,
                ExpiryDateUtc: null));
        var checker = CreateChecker(moduleAccessService: moduleAccessService);

        var result = await checker.IsModuleEntitledAsync(TenantId, "HR", CancellationToken.None);

        Assert.True(result.IsAllowed);
        Assert.Equal(expected, result.ResolvedFrom);
    }

    [Fact]
    public async Task IsModuleEntitledAsync_allow_path_sets_resolved_at_utc()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var moduleAccessService = new Mock<ITenantModuleAccessService>();
        moduleAccessService
            .Setup(x => x.GetEffectiveAccessDetailAsync(TenantId, "HR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantModuleEffectiveAccessDto(
                TenantId,
                "HR",
                "HR",
                "Plan",
                TenantModuleEffectiveAccess.Active,
                HasAccess: true,
                Reason: null,
                ExpiryDateUtc: null));
        var checker = CreateChecker(moduleAccessService: moduleAccessService);

        var result = await checker.IsModuleEntitledAsync(TenantId, "HR", CancellationToken.None);

        var after = DateTimeOffset.UtcNow.AddSeconds(1);
        Assert.InRange(result.ResolvedAtUtc, before, after);
    }

    [Fact]
    public async Task IsModuleEntitledAsync_falls_back_to_unknown_for_unrecognized_source()
    {
        var moduleAccessService = new Mock<ITenantModuleAccessService>();
        moduleAccessService
            .Setup(x => x.GetEffectiveAccessDetailAsync(TenantId, "HR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantModuleEffectiveAccessDto(
                TenantId,
                "HR",
                "HR",
                "SomeFutureSource",
                TenantModuleEffectiveAccess.Active,
                HasAccess: true,
                Reason: null,
                ExpiryDateUtc: null));
        var checker = CreateChecker(moduleAccessService: moduleAccessService);

        var result = await checker.IsModuleEntitledAsync(TenantId, "HR", CancellationToken.None);

        Assert.True(result.IsAllowed);
        Assert.Equal(EntitlementResolutionSource.Unknown, result.ResolvedFrom);
    }

    [Fact]
    public async Task IsModuleEntitledAsync_deny_path_leaves_resolved_from_unknown()
    {
        var moduleAccessService = new Mock<ITenantModuleAccessService>();
        moduleAccessService
            .Setup(x => x.GetEffectiveAccessDetailAsync(TenantId, "HR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantModuleEffectiveAccessDto(
                TenantId,
                "HR",
                "HR",
                "None",
                TenantModuleEffectiveAccess.NoAccess,
                HasAccess: false,
                Reason: null,
                ExpiryDateUtc: null));
        var checker = CreateChecker(moduleAccessService: moduleAccessService);

        var result = await checker.IsModuleEntitledAsync(TenantId, "HR", CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.Equal(EntitlementResolutionSource.Unknown, result.ResolvedFrom);
        Assert.Equal(EntitlementDenyReason.ModuleNotEntitled, result.DenyReason);
    }

    [Fact]
    public async Task IsModuleEntitledAsync_transient_failure_yields_unknown_and_not_cacheable()
    {
        var moduleAccessService = new Mock<ITenantModuleAccessService>();
        moduleAccessService
            .Setup(x => x.GetEffectiveAccessDetailAsync(TenantId, "HR", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));
        var checker = CreateChecker(moduleAccessService: moduleAccessService);

        var result = await checker.IsModuleEntitledAsync(TenantId, "HR", CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.False(result.IsCacheable);
        Assert.Equal(EntitlementResolutionSource.Unknown, result.ResolvedFrom);
    }

    [Fact]
    public async Task IsFeatureEnabledAsync_allow_path_resolves_from_plan()
    {
        var checker = CreateCheckerWithFeature("ADVANCED_REPORTING", includedFeatures: new[] { "ADVANCED_REPORTING" });

        var result = await checker.IsFeatureEnabledAsync(TenantId, "ADVANCED_REPORTING", CancellationToken.None);

        Assert.True(result.IsAllowed);
        Assert.Equal(EntitlementResolutionSource.Plan, result.ResolvedFrom);
    }

    [Fact]
    public async Task IsFeatureEnabledAsync_allow_path_sets_resolved_at_utc()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var checker = CreateCheckerWithFeature("ADVANCED_REPORTING", includedFeatures: new[] { "ADVANCED_REPORTING" });

        var result = await checker.IsFeatureEnabledAsync(TenantId, "ADVANCED_REPORTING", CancellationToken.None);

        var after = DateTimeOffset.UtcNow.AddSeconds(1);
        Assert.InRange(result.ResolvedAtUtc, before, after);
    }

    [Fact]
    public async Task IsFeatureEnabledAsync_deny_path_leaves_resolved_from_unknown()
    {
        var checker = CreateCheckerWithFeature("ADVANCED_REPORTING", includedFeatures: Array.Empty<string>());

        var result = await checker.IsFeatureEnabledAsync(TenantId, "ADVANCED_REPORTING", CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.Equal(EntitlementResolutionSource.Unknown, result.ResolvedFrom);
        Assert.Equal(EntitlementDenyReason.FeatureNotEnabled, result.DenyReason);
    }

    [Fact]
    public async Task IsFeatureEnabledAsync_transient_failure_yields_unknown_and_not_cacheable()
    {
        var tenantSubscriptionRepository = new Mock<ITenantSubscriptionRepository>();
        tenantSubscriptionRepository
            .Setup(x => x.GetCurrentByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));
        var checker = CreateChecker(tenantSubscriptionRepository: tenantSubscriptionRepository);

        var result = await checker.IsFeatureEnabledAsync(TenantId, "ADVANCED_REPORTING", CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.False(result.IsCacheable);
        Assert.Equal(EntitlementResolutionSource.Unknown, result.ResolvedFrom);
    }

    [Fact]
    public async Task IsModuleEntitledAsync_preserves_allow_deny_parity_across_sources()
    {
        var allowedSources = new[] { "System", "ManualOverride", "Addon", "Trial", "Plan" };
        foreach (var source in allowedSources)
        {
            var moduleAccessService = new Mock<ITenantModuleAccessService>();
            moduleAccessService
                .Setup(x => x.GetEffectiveAccessDetailAsync(TenantId, "HR", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TenantModuleEffectiveAccessDto(
                    TenantId,
                    "HR",
                    "HR",
                    source,
                    TenantModuleEffectiveAccess.Active,
                    HasAccess: true,
                    Reason: null,
                    ExpiryDateUtc: null));
            var checker = CreateChecker(moduleAccessService: moduleAccessService);

            var result = await checker.IsModuleEntitledAsync(TenantId, "HR", CancellationToken.None);

            Assert.True(result.IsAllowed);
            Assert.True(result.IsCacheable);
            Assert.Equal(EntitlementKind.Module, result.Kind);
            Assert.Equal("HR", result.Code);
            Assert.Null(result.DenyReason);
        }
    }

    private static EntitlementChecker CreateChecker(
        Mock<ITenantModuleAccessService>? moduleAccessService = null,
        Mock<ITenantSubscriptionRepository>? tenantSubscriptionRepository = null,
        Mock<ISubscriptionPlanRepository>? subscriptionPlanRepository = null)
    {
        moduleAccessService ??= new Mock<ITenantModuleAccessService>();

        if (tenantSubscriptionRepository is null)
        {
            tenantSubscriptionRepository = new Mock<ITenantSubscriptionRepository>();
            tenantSubscriptionRepository
                .Setup(x => x.GetCurrentByTenantIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TenantSubscription { TenantId = TenantId, PlanId = PlanId });
        }

        if (subscriptionPlanRepository is null)
        {
            subscriptionPlanRepository = new Mock<ISubscriptionPlanRepository>();
            subscriptionPlanRepository
                .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SubscriptionPlan { IncludedFeatures = [] });
        }

        return new EntitlementChecker(
            moduleAccessService.Object,
            tenantSubscriptionRepository.Object,
            subscriptionPlanRepository.Object,
            CreateCacheService());
    }

    private static EntitlementChecker CreateCheckerWithFeature(string featureCode, IReadOnlyList<string> includedFeatures)
    {
        var tenantSubscriptionRepository = new Mock<ITenantSubscriptionRepository>();
        tenantSubscriptionRepository
            .Setup(x => x.GetCurrentByTenantIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantSubscription { TenantId = TenantId, PlanId = PlanId });

        var subscriptionPlanRepository = new Mock<ISubscriptionPlanRepository>();
        subscriptionPlanRepository
            .Setup(x => x.GetByIdAsync(PlanId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionPlan { IncludedFeatures = includedFeatures });

        return CreateChecker(
            tenantSubscriptionRepository: tenantSubscriptionRepository,
            subscriptionPlanRepository: subscriptionPlanRepository);
    }

    private static EntitlementCacheService CreateCacheService()
    {
        return new EntitlementCacheService(
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new EntitlementCacheOptions { CacheTtlSeconds = 300 }));
    }
}

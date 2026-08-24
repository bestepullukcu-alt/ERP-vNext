using Diten.Platform.Application.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.AccessGovernance;

public sealed class PlatformSystemTenantModuleAccessTests
{
    private const string ModuleCode = "PRODUCT-MASTER";
    private static readonly Guid CustomerTenantId = Guid.NewGuid();

    [Fact]
    public async Task Active_assignable_module_allows_platform_system_tenant_without_entitlement_or_plan()
    {
        var fixture = CreateFixture(ActiveAssignableModule());

        var detail = await fixture.Service.GetEffectiveAccessDetailAsync(
            SystemTenantRules.PlatformSystemTenantId,
            ModuleCode.ToLowerInvariant());

        Assert.True(detail.HasAccess);
        Assert.Equal(TenantModuleEffectiveAccess.Active, detail.EffectiveAccess);
        Assert.Equal("PlatformSystemTenant", detail.Source);
        Assert.Equal("PlatformSystemTenant", detail.Reason);
        fixture.VerifyNoEntitlementOrPlanReads();
    }

    [Fact]
    public async Task Same_module_does_not_allow_customer_without_entitlement_or_plan()
    {
        var fixture = CreateFixture(ActiveAssignableModule());
        fixture.Entitlements
            .Setup(x => x.GetByTenantAndModuleAsync(CustomerTenantId, ModuleCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        fixture.Subscriptions
            .Setup(x => x.GetCurrentByTenantIdAsync(CustomerTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantSubscription?)null);

        var detail = await fixture.Service.GetEffectiveAccessDetailAsync(CustomerTenantId, ModuleCode);

        Assert.False(detail.HasAccess);
        Assert.Equal(TenantModuleEffectiveAccess.NoAccess, detail.EffectiveAccess);
        Assert.Equal("None", detail.Source);
    }

    [Fact]
    public async Task Existing_customer_addon_entitlement_keeps_existing_allow_behavior()
    {
        var fixture = CreateFixture(ActiveAssignableModule());
        fixture.Entitlements
            .Setup(x => x.GetByTenantAndModuleAsync(CustomerTenantId, ModuleCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Entitlement(EntitlementSource.Addon, isEnabled: true)]);
        fixture.Subscriptions
            .Setup(x => x.GetCurrentByTenantIdAsync(CustomerTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantSubscription?)null);

        var detail = await fixture.Service.GetEffectiveAccessDetailAsync(CustomerTenantId, ModuleCode);

        Assert.True(detail.HasAccess);
        Assert.Equal(TenantModuleEffectiveAccess.Active, detail.EffectiveAccess);
        Assert.Equal("Addon", detail.Source);
    }

    [Fact]
    public async Task Unknown_module_denies_platform_system_tenant()
    {
        var fixture = CreateFixture(module: null);

        var detail = await fixture.Service.GetEffectiveAccessDetailAsync(
            SystemTenantRules.PlatformSystemTenantId,
            ModuleCode);

        Assert.False(detail.HasAccess);
        Assert.Equal(TenantModuleEffectiveAccess.NoAccess, detail.EffectiveAccess);
        fixture.VerifyNoEntitlementOrPlanReads();
    }

    [Theory]
    [InlineData(ModuleCatalogStatus.Inactive, true, false)]
    [InlineData(ModuleCatalogStatus.Active, false, false)]
    [InlineData(ModuleCatalogStatus.Active, true, true)]
    public async Task Ineligible_module_denies_platform_system_tenant(
        ModuleCatalogStatus status,
        bool isTenantAssignable,
        bool isDeleted)
    {
        var fixture = CreateFixture(new ModuleCatalogItem
        {
            ModuleCode = ModuleCode,
            DisplayName = "Product Master",
            Status = status,
            IsTenantAssignable = isTenantAssignable,
            IsDeleted = isDeleted
        });

        var detail = await fixture.Service.GetEffectiveAccessDetailAsync(
            SystemTenantRules.PlatformSystemTenantId,
            ModuleCode);

        Assert.False(detail.HasAccess);
        Assert.Equal(TenantModuleEffectiveAccess.NoAccess, detail.EffectiveAccess);
        fixture.VerifyNoEntitlementOrPlanReads();
    }

    [Fact]
    public async Task Customer_manual_override_and_expiry_behavior_remains_unchanged()
    {
        var disabledFixture = CreateFixture(ActiveAssignableModule());
        disabledFixture.Entitlements
            .Setup(x => x.GetByTenantAndModuleAsync(CustomerTenantId, ModuleCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Entitlement(EntitlementSource.ManualOverride, isEnabled: false)]);
        disabledFixture.Subscriptions
            .Setup(x => x.GetCurrentByTenantIdAsync(CustomerTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantSubscription?)null);

        var disabled = await disabledFixture.Service.GetEffectiveAccessDetailAsync(CustomerTenantId, ModuleCode);

        Assert.False(disabled.HasAccess);
        Assert.Equal(TenantModuleEffectiveAccess.BlockedByOverride, disabled.EffectiveAccess);

        var expiredFixture = CreateFixture(ActiveAssignableModule());
        expiredFixture.Entitlements
            .Setup(x => x.GetByTenantAndModuleAsync(CustomerTenantId, ModuleCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Entitlement(
                EntitlementSource.Trial,
                isEnabled: true,
                expiryDateUtc: DateTimeOffset.UtcNow.AddMinutes(-1))]);
        expiredFixture.Subscriptions
            .Setup(x => x.GetCurrentByTenantIdAsync(CustomerTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantSubscription?)null);

        var expired = await expiredFixture.Service.GetEffectiveAccessDetailAsync(CustomerTenantId, ModuleCode);

        Assert.False(expired.HasAccess);
        Assert.Equal(TenantModuleEffectiveAccess.Expired, expired.EffectiveAccess);
    }

    private static ModuleCatalogItem ActiveAssignableModule() => new()
    {
        ModuleCode = ModuleCode,
        DisplayName = "Product Master",
        Status = ModuleCatalogStatus.Active,
        IsTenantAssignable = true
    };

    private static TenantModuleEntitlement Entitlement(
        EntitlementSource source,
        bool isEnabled,
        DateTimeOffset? expiryDateUtc = null) => new()
    {
        TenantId = CustomerTenantId,
        ModuleCode = ModuleCode,
        Source = source,
        IsEnabled = isEnabled,
        ExpiryDateUtc = expiryDateUtc
    };

    private static Fixture CreateFixture(ModuleCatalogItem? module)
    {
        var entitlements = new Mock<ITenantModuleEntitlementRepository>(MockBehavior.Strict);
        var subscriptions = new Mock<ITenantSubscriptionRepository>(MockBehavior.Strict);
        var plans = new Mock<ISubscriptionPlanRepository>(MockBehavior.Strict);
        var catalog = new Mock<IModuleCatalogRepository>(MockBehavior.Strict);
        catalog
            .Setup(x => x.GetByCodeAsync(ModuleCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(module);

        return new Fixture(
            new TenantModuleAccessService(entitlements.Object, catalog.Object, subscriptions.Object, plans.Object),
            entitlements,
            subscriptions,
            plans);
    }

    private sealed record Fixture(
        TenantModuleAccessService Service,
        Mock<ITenantModuleEntitlementRepository> Entitlements,
        Mock<ITenantSubscriptionRepository> Subscriptions,
        Mock<ISubscriptionPlanRepository> Plans)
    {
        public void VerifyNoEntitlementOrPlanReads()
        {
            Entitlements.VerifyNoOtherCalls();
            Subscriptions.VerifyNoOtherCalls();
            Plans.VerifyNoOtherCalls();
        }
    }
}

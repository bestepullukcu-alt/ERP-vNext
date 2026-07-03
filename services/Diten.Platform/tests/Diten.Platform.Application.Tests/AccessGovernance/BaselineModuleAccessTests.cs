using Diten.Platform.Application.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.AccessGovernance;

// FEAT-BASELINE-MODULES-S1 — a baseline module is entitlement-free: HasAccessAsync returns true for EVERY tenant
// even with no entitlement row and no subscription. Non-baseline modules keep the existing entitlement behaviour
// (no access without an entitlement/plan) — regression guard.
public sealed class BaselineModuleAccessTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    [Fact]
    public async Task Baseline_module_grants_access_without_any_entitlement_or_subscription()
    {
        var entitlements = new Mock<ITenantModuleEntitlementRepository>(MockBehavior.Strict);
        var subscriptions = new Mock<ITenantSubscriptionRepository>(MockBehavior.Strict);
        var plans = new Mock<ISubscriptionPlanRepository>(MockBehavior.Strict);
        var catalog = new Mock<IModuleCatalogRepository>();
        catalog.Setup(x => x.GetByCodeAsync("ACCESS-GOVERNANCE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModuleCatalogItem { ModuleCode = "ACCESS-GOVERNANCE", DisplayName = "Access Governance", IsBaseline = true });

        var service = new TenantModuleAccessService(entitlements.Object, catalog.Object, subscriptions.Object, plans.Object);

        Assert.True(await service.HasAccessAsync(Tenant, "access-governance"));
        // The entitlement/subscription repos are never touched for a baseline module (strict mocks would throw).
        entitlements.VerifyNoOtherCalls();
        subscriptions.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Non_baseline_module_without_entitlement_or_plan_has_no_access()
    {
        var entitlements = new Mock<ITenantModuleEntitlementRepository>();
        entitlements.Setup(x => x.GetByTenantAndModuleAsync(Tenant, "SOME-MODULE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TenantModuleEntitlement>());
        var subscriptions = new Mock<ITenantSubscriptionRepository>();
        subscriptions.Setup(x => x.GetCurrentByTenantIdAsync(Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantSubscription?)null);
        var plans = new Mock<ISubscriptionPlanRepository>();
        var catalog = new Mock<IModuleCatalogRepository>();
        catalog.Setup(x => x.GetByCodeAsync("SOME-MODULE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModuleCatalogItem { ModuleCode = "SOME-MODULE", IsBaseline = false });

        var service = new TenantModuleAccessService(entitlements.Object, catalog.Object, subscriptions.Object, plans.Object);

        Assert.False(await service.HasAccessAsync(Tenant, "some-module"));
    }
}

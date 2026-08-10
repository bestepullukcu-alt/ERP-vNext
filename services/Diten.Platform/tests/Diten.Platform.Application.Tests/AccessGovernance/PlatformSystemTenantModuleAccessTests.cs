using Diten.Platform.Application.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.AccessGovernance;

// BL-059 — the platform system tenant (…0001) passes the tenant ENTITLEMENT gate for every catalog module that is
// active and tenant-assignable, so a newly self-registered module needs no manual entitlement row. The bypass is
// deliberately narrow, and the negative side is the real proof:
//   · CUSTOMER tenants keep the exact entitlement behaviour they have today,
//   · inactive / soft-deleted / non-tenant-assignable modules stay closed EVEN for the system tenant,
//   · IsBaseline semantics are untouched,
//   · this only removes the tenant entitlement wall — the per-user permission gate downstream still applies.
public sealed class PlatformSystemTenantModuleAccessTests
{
    private static readonly Guid SystemTenant = SystemTenantRules.PlatformSystemTenantId;
    private static readonly Guid CustomerTenant = Guid.NewGuid();

    private static ModuleCatalogItem Module(
        ModuleCatalogStatus status = ModuleCatalogStatus.Active,
        bool isTenantAssignable = true,
        bool isDeleted = false,
        bool isBaseline = false) =>
        new()
        {
            ModuleCode = "TASKS",
            ModuleName = "Tasks",
            DisplayName = "Görev Yönetimi",
            Status = status,
            IsTenantAssignable = isTenantAssignable,
            IsDeleted = isDeleted,
            IsBaseline = isBaseline
        };

    // Builds the service with NO entitlement row, NO subscription and NO plan for the tenant under test.
    private static TenantModuleAccessService BuildServiceWithoutEntitlements(Guid tenantId, ModuleCatalogItem? module)
    {
        var entitlements = new Mock<ITenantModuleEntitlementRepository>();
        entitlements.Setup(x => x.GetByTenantAndModuleAsync(tenantId, "TASKS", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TenantModuleEntitlement>());
        var subscriptions = new Mock<ITenantSubscriptionRepository>();
        subscriptions.Setup(x => x.GetCurrentByTenantIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantSubscription?)null);
        var plans = new Mock<ISubscriptionPlanRepository>();
        var catalog = new Mock<IModuleCatalogRepository>();
        catalog.Setup(x => x.GetByCodeAsync("TASKS", It.IsAny<CancellationToken>()))
            .ReturnsAsync(module);

        return new TenantModuleAccessService(entitlements.Object, catalog.Object, subscriptions.Object, plans.Object);
    }

    // ---- positive: the one behaviour BL-059 adds -------------------------------------------------------------

    [Fact]
    public async Task System_tenant_reaches_active_assignable_module_without_any_entitlement()
    {
        var service = BuildServiceWithoutEntitlements(SystemTenant, Module());

        Assert.True(await service.HasAccessAsync(SystemTenant, "tasks"));
    }

    [Fact]
    public async Task System_tenant_access_is_reported_as_PlatformSystemTenant_not_Baseline()
    {
        var service = BuildServiceWithoutEntitlements(SystemTenant, Module());

        var detail = await service.GetEffectiveAccessDetailAsync(SystemTenant, "tasks");

        Assert.True(detail.HasAccess);
        Assert.Equal("PlatformSystemTenant", detail.Source);
        Assert.Equal(TenantModuleEffectiveAccess.Active, detail.EffectiveAccess);
    }

    // ---- negative: the real proof ----------------------------------------------------------------------------

    [Fact]
    public async Task Customer_tenant_without_entitlement_still_has_no_access()
    {
        var service = BuildServiceWithoutEntitlements(CustomerTenant, Module());

        var detail = await service.GetEffectiveAccessDetailAsync(CustomerTenant, "tasks");

        Assert.False(detail.HasAccess);
        Assert.Equal("None", detail.Source);
        Assert.Equal(TenantModuleEffectiveAccess.NoAccess, detail.EffectiveAccess);
    }

    [Fact]
    public async Task System_tenant_does_not_reach_inactive_module()
    {
        var service = BuildServiceWithoutEntitlements(SystemTenant, Module(status: ModuleCatalogStatus.Inactive));

        Assert.False(await service.HasAccessAsync(SystemTenant, "tasks"));
    }

    [Fact]
    public async Task System_tenant_does_not_reach_soft_deleted_module()
    {
        var service = BuildServiceWithoutEntitlements(SystemTenant, Module(isDeleted: true));

        Assert.False(await service.HasAccessAsync(SystemTenant, "tasks"));
    }

    [Fact]
    public async Task System_tenant_does_not_reach_non_tenant_assignable_module()
    {
        var service = BuildServiceWithoutEntitlements(SystemTenant, Module(isTenantAssignable: false));

        Assert.False(await service.HasAccessAsync(SystemTenant, "tasks"));
    }

    [Fact]
    public async Task System_tenant_does_not_reach_module_missing_from_the_catalog()
    {
        var service = BuildServiceWithoutEntitlements(SystemTenant, module: null);

        Assert.False(await service.HasAccessAsync(SystemTenant, "tasks"));
    }

    // ---- IsBaseline semantics unchanged -----------------------------------------------------------------------

    [Fact]
    public async Task Baseline_module_still_reports_Baseline_for_a_customer_tenant()
    {
        var service = BuildServiceWithoutEntitlements(CustomerTenant, Module(isBaseline: true));

        var detail = await service.GetEffectiveAccessDetailAsync(CustomerTenant, "tasks");

        Assert.True(detail.HasAccess);
        Assert.Equal("Baseline", detail.Source);
    }

    [Fact]
    public async Task Baseline_wins_over_the_system_tenant_reason_so_baseline_semantics_are_unchanged()
    {
        var service = BuildServiceWithoutEntitlements(SystemTenant, Module(isBaseline: true));

        var detail = await service.GetEffectiveAccessDetailAsync(SystemTenant, "tasks");

        Assert.True(detail.HasAccess);
        Assert.Equal("Baseline", detail.Source);
    }
}

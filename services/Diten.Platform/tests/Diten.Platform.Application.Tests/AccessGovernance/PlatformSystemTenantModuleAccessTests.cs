using Diten.Platform.Application.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.AccessGovernance;

/*
 * ⚠ MERGE 2026-08-26 — TWO BRANCHES WROTE THIS FILE INDEPENDENTLY, and it is the union of both, not a choice.
 *
 * Both suites test one behaviour (BL-059: the platform system tenant passes the tenant ENTITLEMENT gate for
 * every active, tenant-assignable catalog module), and neither was a superset of the other:
 *
 *   FIVE cases were genuinely measured twice — the positive case, the customer-without-entitlement case, the
 *   unknown module, and the three ineligible-module states. They were compared BY READING BOTH, not by name:
 *   main's assert everything this branch's assert (access · effective access · source) and add `Reason` and the
 *   structural guard, so main's are kept and this branch's copies are dropped as true duplicates. Nothing is
 *   lost by that — the same inputs reach the same asserts, plus more.
 *
 *   Kept from MAIN and absent here: an existing add-on entitlement still allowing a customer, manual-override
 *   and expiry behaviour, and `VerifyNoEntitlementOrPlanReads` — a STRUCTURAL guard that the system-tenant path
 *   reads no entitlement and no plan at all. That guard is why main's control flow (an early NoAccess return for
 *   an ineligible module) was the one kept in TenantModuleAccessService; this branch fell through to the
 *   entitlement lookup, which is both looser and unprovable.
 *
 *   Kept from THIS BRANCH and absent in main: `IsBaseline` semantics — a baseline module still reports
 *   "Baseline" for a customer, and baseline still WINS over the system-tenant reason. Main tests the new reason
 *   and never checks that the older one it sits next to survived. Also kept: one test through
 *   `HasAccessAsync`, the boolean entry point every caller actually uses, which main's suite never enters.
 *
 * ⚠ The names are in two conventions and stay that way. Renaming for tidiness would mean rewriting tests whose
 * behaviour nobody reviewed today, and a uniform file is worth less than a file whose history is readable.
 */

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

    // ── kept from the WC-1 branch: IsBaseline semantics, which main's suite does not touch ──────────────────
    //
    // BL-059 added a NEW access reason next to an existing one. These two say the older one still works and
    // still wins: a change that quietly promoted every baseline module to "PlatformSystemTenant", or demoted a
    // customer's baseline access, would pass every other test in this file.

    [Fact]
    public async Task Baseline_module_still_reports_Baseline_for_a_customer_tenant()
    {
        var fixture = CreateFixture(BaselineModule());

        var detail = await fixture.Service.GetEffectiveAccessDetailAsync(CustomerTenantId, ModuleCode);

        Assert.True(detail.HasAccess);
        Assert.Equal("Baseline", detail.Source);
        // Baseline is decided before the entitlement wall, so this path reads nothing either.
        fixture.VerifyNoEntitlementOrPlanReads();
    }

    [Fact]
    public async Task Baseline_wins_over_the_system_tenant_reason_so_baseline_semantics_are_unchanged()
    {
        var fixture = CreateFixture(BaselineModule());

        var detail = await fixture.Service.GetEffectiveAccessDetailAsync(
            SystemTenantRules.PlatformSystemTenantId,
            ModuleCode);

        Assert.True(detail.HasAccess);
        // ⚠ "Baseline", NOT "PlatformSystemTenant": the system tenant reaching a baseline module must keep
        // reporting the reason it always had, or the older rule has been quietly replaced by the newer one.
        Assert.Equal("Baseline", detail.Source);
        fixture.VerifyNoEntitlementOrPlanReads();
    }

    // ── kept from the WC-1 branch: the BOOLEAN entry point ──────────────────────────────────────────────────
    //
    // Every caller in the product asks `HasAccessAsync`; main's suite only ever calls
    // `GetEffectiveAccessDetailAsync`. The two are one implementation today, and a test that never enters the
    // door the callers use cannot notice the day they stop being one.

    [Fact]
    public async Task System_tenant_reaches_active_assignable_module_through_the_boolean_entry_point()
    {
        var fixture = CreateFixture(ActiveAssignableModule());

        Assert.True(await fixture.Service.HasAccessAsync(
            SystemTenantRules.PlatformSystemTenantId,
            ModuleCode.ToLowerInvariant()));
        fixture.VerifyNoEntitlementOrPlanReads();
    }

    private static ModuleCatalogItem BaselineModule() => new()
    {
        ModuleCode = ModuleCode,
        DisplayName = "Product Master",
        Status = ModuleCatalogStatus.Active,
        IsTenantAssignable = true,
        IsBaseline = true
    };

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

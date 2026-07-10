using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Queries;
using Diten.Platform.Common.Catalog;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Tenants.Commercial.Entitlements;

public sealed class TenantCommercialEntitlementQueryTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task GetTenantAvailableModulesForAssignment_UsesPlatformCatalogContract_AndMapsResult()
    {
        // Arrange — tenant has no entitlements, so the whole assignable catalog is offered.
        var mockContract = new Mock<IPlatformCatalogContract>();
        var items = new List<AssignableModuleInfo>
        {
            new AssignableModuleInfo(Guid.NewGuid(), "HR", "HR Module", "HR Display", "Desc", "D", "S", "Active", "1.0", false, true, 1, DateTimeOffset.UtcNow, null),
            new AssignableModuleInfo(Guid.NewGuid(), "CRM", "CRM Module", "CRM Display", "Desc", "D", "S", "Active", "1.0", false, true, 2, DateTimeOffset.UtcNow, null)
        };

        mockContract.Setup(x => x.GetAssignableModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var handler = BuildHandler(mockContract);
        var query = new GetTenantAvailableModulesForAssignmentQuery(TenantId);

        // Act
        var response = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.IsSuccessful);
        Assert.NotNull(response.Data);
        Assert.Equal(2, response.Data!.Count);

        Assert.Equal("HR", response.Data[0].ModuleCode);
        Assert.Equal("HR Module", response.Data[0].ModuleName);
        Assert.Equal("HR Display", response.Data[0].DisplayName);

        Assert.Equal("CRM", response.Data[1].ModuleCode);
        Assert.Equal("CRM Module", response.Data[1].ModuleName);
        Assert.Equal("CRM Display", response.Data[1].DisplayName);

        mockContract.Verify(x => x.GetAssignableModulesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTenantAvailableModulesForAssignment_ExcludesAlreadyEntitledModules()
    {
        // Arrange — catalog of four; "GOLDENCOMPACT" is plan-derived, "HR" is an ENABLED manual row, "BILLING"
        // is a DISABLED manual row. A disabled row is still an existing entitlement, so it must ALSO be excluded
        // (re-enable is done via the row's Enable action, not by re-adding it); only "CRM" remains offerable.
        var mockContract = new Mock<IPlatformCatalogContract>();
        var items = new List<AssignableModuleInfo>
        {
            new AssignableModuleInfo(Guid.NewGuid(), "GOLDENCOMPACT", "Golden Compact", "Golden Compact", "Desc", "D", "S", "Active", "1.0", false, true, 1, DateTimeOffset.UtcNow, null),
            new AssignableModuleInfo(Guid.NewGuid(), "HR", "HR Module", "HR Display", "Desc", "D", "S", "Active", "1.0", false, true, 2, DateTimeOffset.UtcNow, null),
            new AssignableModuleInfo(Guid.NewGuid(), "CRM", "CRM Module", "CRM Display", "Desc", "D", "S", "Active", "1.0", false, true, 3, DateTimeOffset.UtcNow, null),
            new AssignableModuleInfo(Guid.NewGuid(), "BILLING", "Billing", "Billing", "Desc", "D", "S", "Active", "1.0", false, true, 4, DateTimeOffset.UtcNow, null)
        };
        mockContract.Setup(x => x.GetAssignableModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var entitlementRepo = new Mock<ITenantModuleEntitlementRepository>();
        entitlementRepo.Setup(x => x.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantModuleEntitlement>
            {
                new() { TenantId = TenantId, ModuleCode = "HR", Source = EntitlementSource.ManualOverride, IsEnabled = true },
                new() { TenantId = TenantId, ModuleCode = "BILLING", Source = EntitlementSource.ManualOverride, IsEnabled = false } // disabled → also excluded
            });

        var subscriptionRepo = new Mock<ITenantSubscriptionRepository>();
        var planId = Guid.NewGuid();
        subscriptionRepo.Setup(x => x.GetCurrentByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantSubscription { TenantId = TenantId, PlanId = planId });

        var planRepo = new Mock<ISubscriptionPlanRepository>();
        planRepo.Setup(x => x.GetByIdAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionPlan { IncludedModuleKeys = new[] { "goldencompact" } }); // case-insensitive

        var handler = new GetTenantAvailableModulesForAssignmentQueryHandler(
            mockContract.Object, entitlementRepo.Object, subscriptionRepo.Object, planRepo.Object);

        // Act
        var response = await handler.Handle(new GetTenantAvailableModulesForAssignmentQuery(TenantId), CancellationToken.None);

        // Assert — plan-derived GOLDENCOMPACT, enabled HR, and DISABLED BILLING are all hidden; only CRM remains.
        Assert.True(response.IsSuccessful);
        var codes = response.Data!.Select(x => x.ModuleCode).ToList();
        Assert.DoesNotContain("GOLDENCOMPACT", codes);
        Assert.DoesNotContain("HR", codes);
        Assert.DoesNotContain("BILLING", codes); // disabled entitlement must NOT be re-offered
        Assert.Contains("CRM", codes);
        Assert.Single(codes);
    }

    // FEAT-BASELINE-MODULES — a baseline module is entitlement-free (every tenant auto-has it) and must NEVER appear
    // in the grantable picker, even when it is not in the tenant's entitled set. The exclusion keys off IsBaseline,
    // NOT a hardcoded code list: the synthetic "ZZZSYNTHETIC-BASELINE" code (no special-casing anywhere) is excluded
    // purely because IsBaseline=true, while a normal product module with IsBaseline=false still appears.
    [Fact]
    public async Task GetTenantAvailableModulesForAssignment_ExcludesBaselineModules_KeyedOffIsBaseline_NotCodeList()
    {
        var mockContract = new Mock<IPlatformCatalogContract>();
        var items = new List<AssignableModuleInfo>
        {
            // Baseline via arbitrary/synthetic code — excluded solely because IsBaseline == true.
            new AssignableModuleInfo(Guid.NewGuid(), "ZZZSYNTHETIC-BASELINE", "Synthetic Baseline", "Synthetic Baseline", "Desc", "D", "S", "Active", "1.0", false, true, 1, DateTimeOffset.UtcNow, null, null, true),
            // Normal product module — non-baseline, not entitled → must still be offered.
            new AssignableModuleInfo(Guid.NewGuid(), "LEGALENTITY", "Legal Entity", "Legal Entity", "Desc", "D", "S", "Active", "1.0", false, true, 2, DateTimeOffset.UtcNow, null, null, false)
        };
        mockContract.Setup(x => x.GetAssignableModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var handler = BuildHandler(mockContract);

        var response = await handler.Handle(new GetTenantAvailableModulesForAssignmentQuery(TenantId), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var codes = response.Data!.Select(x => x.ModuleCode).ToList();
        Assert.DoesNotContain("ZZZSYNTHETIC-BASELINE", codes); // baseline excluded despite not being entitled
        Assert.Contains("LEGALENTITY", codes);                 // normal product module still offered
    }

    private static GetTenantAvailableModulesForAssignmentQueryHandler BuildHandler(Mock<IPlatformCatalogContract> contract)
    {
        var entitlementRepo = new Mock<ITenantModuleEntitlementRepository>();
        entitlementRepo.Setup(x => x.GetByTenantIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantModuleEntitlement>());

        var subscriptionRepo = new Mock<ITenantSubscriptionRepository>();
        subscriptionRepo.Setup(x => x.GetCurrentByTenantIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantSubscription?)null);

        var planRepo = new Mock<ISubscriptionPlanRepository>();

        return new GetTenantAvailableModulesForAssignmentQueryHandler(
            contract.Object, entitlementRepo.Object, subscriptionRepo.Object, planRepo.Object);
    }
}

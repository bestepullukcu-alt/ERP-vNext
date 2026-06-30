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
        // Arrange — catalog of three; "GOLDENCOMPACT" is plan-derived, "HR" is an active manual row,
        // "hr-disabled" simulates a disabled row that must NOT be excluded (re-enable path stays selectable).
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
                new() { TenantId = TenantId, ModuleCode = "BILLING", Source = EntitlementSource.ManualOverride, IsEnabled = false } // disabled → still offered
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

        // Assert — plan-derived GOLDENCOMPACT and active HR are hidden; CRM and disabled BILLING remain.
        Assert.True(response.IsSuccessful);
        var codes = response.Data!.Select(x => x.ModuleCode).ToList();
        Assert.DoesNotContain("GOLDENCOMPACT", codes);
        Assert.DoesNotContain("HR", codes);
        Assert.Contains("CRM", codes);
        Assert.Contains("BILLING", codes);
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

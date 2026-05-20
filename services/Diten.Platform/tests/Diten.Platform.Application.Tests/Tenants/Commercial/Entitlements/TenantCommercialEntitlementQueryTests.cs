using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Queries;
using Diten.Platform.Common.Catalog;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Tenants.Commercial.Entitlements;

public sealed class TenantCommercialEntitlementQueryTests
{
    [Fact]
    public async Task GetTenantAvailableModulesForAssignment_UsesPlatformCatalogContract_AndMapsResult()
    {
        // Arrange
        var mockContract = new Mock<IPlatformCatalogContract>();
        var items = new List<AssignableModuleInfo>
        {
            new AssignableModuleInfo(Guid.NewGuid(), "HR", "HR Module", "HR Display", "Desc", "D", "S", "Active", "1.0", false, true, 1, DateTimeOffset.UtcNow, null),
            new AssignableModuleInfo(Guid.NewGuid(), "CRM", "CRM Module", "CRM Display", "Desc", "D", "S", "Active", "1.0", false, true, 2, DateTimeOffset.UtcNow, null)
        };

        mockContract.Setup(x => x.GetAssignableModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var handler = new GetTenantAvailableModulesForAssignmentQueryHandler(mockContract.Object);
        var query = new GetTenantAvailableModulesForAssignmentQuery(Guid.NewGuid());

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
}

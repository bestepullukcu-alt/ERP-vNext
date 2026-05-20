using Diten.Platform.Application.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.ModuleCatalog;

public sealed class PlatformCatalogContractTests
{
    private readonly Mock<IModuleCatalogRepository> _mockRepo;
    private readonly PlatformCatalogContract _contract;

    public PlatformCatalogContractTests()
    {
        _mockRepo = new Mock<IModuleCatalogRepository>();
        _contract = new PlatformCatalogContract(_mockRepo.Object);
    }

    [Fact]
    public async Task GetAssignableModulesAsync_MapsAndSorts_Successfully()
    {
        // Arrange
        var items = new List<ModuleCatalogItem>
        {
            new() { Id = Guid.NewGuid(), ModuleCode = "Z-MODULE", ModuleName = "Z Name", DisplayName = "Z", Domain = "D", Service = "S", Status = ModuleCatalogStatus.Active, ModuleVersion = "1.0", IsCoreModule = false, IsTenantAssignable = true, SortOrder = 10, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = null },
            new() { Id = Guid.NewGuid(), ModuleCode = "A-MODULE", ModuleName = "A Name", DisplayName = "A", Domain = "D", Service = "S", Status = ModuleCatalogStatus.Active, ModuleVersion = "1.0", IsCoreModule = true, IsTenantAssignable = true, SortOrder = 5, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = null },
            new() { Id = Guid.NewGuid(), ModuleCode = "B-MODULE", ModuleName = "B Name", DisplayName = "B", Domain = "D", Service = "S", Status = ModuleCatalogStatus.Active, ModuleVersion = "1.0", IsCoreModule = false, IsTenantAssignable = true, SortOrder = 5, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = null },
        };

        _mockRepo.Setup(x => x.GetAssignableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        // Act
        var result = await _contract.GetAssignableModulesAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        
        // Check sorting: SortOrder then ModuleCode
        Assert.Equal("A-MODULE", result[0].ModuleCode); // SortOrder 5, Code A
        Assert.Equal("B-MODULE", result[1].ModuleCode); // SortOrder 5, Code B
        Assert.Equal("Z-MODULE", result[2].ModuleCode); // SortOrder 10, Code Z

        // Check mapping of first item
        var first = result[0];
        Assert.Equal(items[1].Id, first.Id);
        Assert.Equal("A-MODULE", first.ModuleCode);
        Assert.Equal("A Name", first.ModuleName); // Notice AssignableModuleInfo uses ModuleName
        Assert.Equal("D", first.Domain);
        Assert.Equal("S", first.Service);
        Assert.Equal("1.0", first.ModuleVersion);
        Assert.True(first.IsCoreModule);
        Assert.Equal(5, first.SortOrder);
    }

    [Fact]
    public async Task GetAssignableModulesAsync_EmptyRepository_ReturnsEmptyList()
    {
        // Arrange
        _mockRepo.Setup(x => x.GetAssignableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ModuleCatalogItem>());

        // Act
        var result = await _contract.GetAssignableModulesAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAssignableModulesAsync_RepositoryThrows_ExceptionBubblesUp()
    {
        // Arrange
        _mockRepo.Setup(x => x.GetAssignableAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB Failure"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _contract.GetAssignableModulesAsync(CancellationToken.None));
            
        Assert.Equal("DB Failure", exception.Message);
    }
}

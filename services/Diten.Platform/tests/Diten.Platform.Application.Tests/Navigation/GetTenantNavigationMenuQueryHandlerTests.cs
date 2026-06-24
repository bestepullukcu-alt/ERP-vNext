using Diten.Platform.Application.Features.Navigation.Handlers;
using Diten.Platform.Application.Features.Navigation.Queries;
using Diten.Platform.Application.Services;
using Diten.Platform.Common.Catalog;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Navigation;

public sealed class GetTenantNavigationMenuQueryHandlerTests
{
    private static AssignableModuleInfo Module(string code, string display, int sort) =>
        new(Guid.NewGuid(), code, $"{code} Module", display, "Desc", "D", "S", "Active", "1.0", false, true, sort, DateTimeOffset.UtcNow, null);

    private static ModulePageDescriptor Page(
        string moduleCode,
        string pageCode,
        ModulePageStatus status = ModulePageStatus.Active,
        bool navVisible = true,
        string? requiredPermission = null,
        string? parentPageCode = null,
        int sortOrder = 0) =>
        new()
        {
            TenantId = Guid.Empty, // platform-scope template, like self-registration writes
            ModuleCode = moduleCode,
            PageCode = pageCode,
            DisplayName = $"{pageCode} Page",
            RoutePath = $"/{moduleCode}/{pageCode}",
            RequiredPermission = requiredPermission,
            ParentPageCode = parentPageCode,
            IsNavigationVisible = navVisible,
            Status = status,
            SortOrder = sortOrder
        };

    [Fact]
    public async Task EntitledModule_WithActiveVisibleDescriptor_ReturnsItem_AndFiltersDraftAndHidden()
    {
        var tenantId = Guid.NewGuid();
        var catalog = new Mock<IPlatformCatalogContract>();
        catalog.Setup(x => x.GetAssignableModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AssignableModuleInfo> { Module("GOLDENSLIM", "Golden Slim", 1) });

        var access = new Mock<ITenantModuleAccessService>();
        access.Setup(x => x.HasAccessAsync(tenantId, "GOLDENSLIM", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var repo = new Mock<IModulePageDescriptorRepository>();
        repo.Setup(x => x.GetByModuleAsync("GOLDENSLIM", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ModulePageDescriptor>
            {
                Page("GOLDENSLIM", "RECORDS", requiredPermission: "goldenslim.records.read", sortOrder: 10),
                Page("GOLDENSLIM", "DRAFTPAGE", status: ModulePageStatus.Draft, sortOrder: 20),
                Page("GOLDENSLIM", "HIDDENPAGE", navVisible: false, sortOrder: 30)
            });

        var handler = new GetTenantNavigationMenuQueryHandler(catalog.Object, access.Object, repo.Object, new Mock<ITenantContext>().Object);

        var response = await handler.Handle(new GetTenantNavigationMenuQuery(tenantId), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.NotNull(response.Data);
        var group = Assert.Single(response.Data!);
        Assert.Equal("GOLDENSLIM", group.ModuleCode);
        Assert.Equal("Golden Slim", group.ModuleDisplayName);
        var item = Assert.Single(group.Items); // Draft + hidden filtered out
        Assert.Equal("RECORDS", item.PageCode);
        Assert.Equal("/GOLDENSLIM/RECORDS", item.RoutePath);
        Assert.Equal("goldenslim.records.read", item.RequiredPermission);
    }

    [Fact]
    public async Task NotEntitledModule_ReturnsEmpty_AndNeverReadsDescriptors()
    {
        var tenantId = Guid.NewGuid();
        var catalog = new Mock<IPlatformCatalogContract>();
        catalog.Setup(x => x.GetAssignableModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AssignableModuleInfo> { Module("GOLDENSLIM", "Golden Slim", 1) });

        var access = new Mock<ITenantModuleAccessService>();
        access.Setup(x => x.HasAccessAsync(tenantId, "GOLDENSLIM", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var repo = new Mock<IModulePageDescriptorRepository>();

        var handler = new GetTenantNavigationMenuQueryHandler(catalog.Object, access.Object, repo.Object, new Mock<ITenantContext>().Object);

        var response = await handler.Handle(new GetTenantNavigationMenuQuery(tenantId), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.NotNull(response.Data);
        Assert.Empty(response.Data!);
        repo.Verify(x => x.GetByModuleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EntitledModule_WithNoVisibleDescriptors_OmitsGroup()
    {
        var tenantId = Guid.NewGuid();
        var catalog = new Mock<IPlatformCatalogContract>();
        catalog.Setup(x => x.GetAssignableModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AssignableModuleInfo> { Module("HR", "Human Resources", 1) });

        var access = new Mock<ITenantModuleAccessService>();
        access.Setup(x => x.HasAccessAsync(tenantId, "HR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var repo = new Mock<IModulePageDescriptorRepository>();
        repo.Setup(x => x.GetByModuleAsync("HR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ModulePageDescriptor>()); // self-registered but no nav descriptors yet

        var handler = new GetTenantNavigationMenuQueryHandler(catalog.Object, access.Object, repo.Object, new Mock<ITenantContext>().Object);

        var response = await handler.Handle(new GetTenantNavigationMenuQuery(tenantId), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Empty(response.Data!);
    }
}

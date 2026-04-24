using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Application.Features.Tenants.Handlers;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests;

public sealed class TenantHandlersTests
{
    [Fact]
    public async Task RegisterTenant_ShouldInitializeProvisioningAndActivity()
    {
        var repository = new Mock<ITenantRegistryRepository>();
        repository.Setup(x => x.GetByDomainAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);
        repository.Setup(x => x.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);
        repository.Setup(x => x.CreateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, CancellationToken _) => t);

        var defaults = new Mock<ITenantDefaultsProvider>();
        defaults.SetupGet(x => x.DefaultRegion).Returns("US");
        defaults.SetupGet(x => x.DefaultEnvironment).Returns("Production");
        defaults.SetupGet(x => x.DefaultTier).Returns("Standard");
        defaults.SetupGet(x => x.DefaultLanguage).Returns("en");
        defaults.SetupGet(x => x.DefaultTimezone).Returns("UTC");
        defaults.SetupGet(x => x.DefaultCurrency).Returns("USD");
        defaults.SetupGet(x => x.AppUrlTemplate).Returns("https://{tenant}.diten.tech");

        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(x => x.IsAuthenticated).Returns(false);
        user.SetupGet(x => x.UserId).Returns(Guid.Empty);

        var handler = new RegisterTenantCommandHandler(repository.Object, defaults.Object, user.Object);

        var id = await handler.Handle(new RegisterTenantCommand("Acme", "diten.tech", "acme"), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, id);
        repository.Verify(x => x.CreateAsync(It.Is<Tenant>(t =>
            t.Status == TenantStatus.Provisioning &&
            t.ProvisioningStatus == "Started" &&
            t.ProvisioningSteps.Count >= 2 &&
            t.ActivityTimeline.Any(a => a.EventType == "tenant.created") &&
            t.ActivityTimeline.Any(a => a.EventType == "tenant.provisioning.started")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SuspendTenant_ShouldRejectDeactivatedTenant()
    {
        var repository = new Mock<ITenantRegistryRepository>();
        repository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tenant
            {
                Code = "TEN123",
                Name = "Acme",
                DisplayName = "Acme",
                Domain = "acme.diten.tech",
                Status = TenantStatus.Deactivated
            });

        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(x => x.IsAuthenticated).Returns(false);

        var handler = new SuspendTenantCommandHandler(repository.Object, user.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new SuspendTenantCommand(Guid.NewGuid(), null), CancellationToken.None));
    }

    [Fact]
    public async Task ReactivateTenant_ShouldMoveToActiveAndCompleteProvisioning()
    {
        var tenant = new Tenant
        {
            Code = "TEN123",
            Name = "Acme",
            DisplayName = "Acme",
            Domain = "acme.diten.tech",
            Status = TenantStatus.Suspended,
            ProvisioningStatus = "Started",
            ProvisioningSteps =
            [
                new TenantProvisioningStep
                {
                    Key = "bootstrap-platform",
                    Label = "Platform Bootstrap",
                    Status = "InProgress"
                }
            ]
        };

        var repository = new Mock<ITenantRegistryRepository>();
        repository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        repository.Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(x => x.IsAuthenticated).Returns(false);

        var handler = new ReactivateTenantCommandHandler(repository.Object, user.Object);
        var result = await handler.Handle(new ReactivateTenantCommand(Guid.NewGuid(), null), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Active", result.Status);
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Equal("Completed", tenant.ProvisioningStatus);
        Assert.Equal("Completed", tenant.ProvisioningSteps[0].Status);
    }
}

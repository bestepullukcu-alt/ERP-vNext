using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Application.Features.Tenants.Handlers;
using Diten.Platform.Contracts.Events;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Tenants;

// C3d: the platform system tenant (TenantType.Internal) must never be suspended or deleted, regardless of
// its lifecycle Status — the guard runs before the Status checks. Suspending it then deleting would break
// the platform. Verified independent of the UI (which only hides the action).
public sealed class SystemTenantGuardTests
{
    private static readonly Guid SystemTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    [Theory]
    [InlineData(TenantStatus.Active)]
    [InlineData(TenantStatus.Suspended)]
    public async Task DeleteTenant_RejectsInternalSystemTenant(TenantStatus status)
    {
        var tenant = CreateSystemTenant(status);
        var repository = new Mock<ITenantRegistryRepository>();
        repository.Setup(x => x.GetByIdAsync(SystemTenantId, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);

        var domainRepository = new Mock<ITenantDomainRepository>();
        var user = new Mock<ICurrentUserContext>();
        var eventBus = new Mock<IEventBus>();

        var handler = new DeleteTenantCommandHandler(repository.Object, domainRepository.Object, user.Object, eventBus.Object);

        var result = await handler.Handle(new DeleteTenantCommand(SystemTenantId), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
        repository.Verify(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.DoesNotContain(eventBus.Invocations, i => i.Method.Name == nameof(IEventBus.PublishAsync));
    }

    [Theory]
    [InlineData(TenantStatus.Active)]
    [InlineData(TenantStatus.Provisioning)]
    public async Task SuspendTenant_RejectsInternalSystemTenant(TenantStatus status)
    {
        var tenant = CreateSystemTenant(status);
        var repository = new Mock<ITenantRegistryRepository>();
        repository.Setup(x => x.GetByIdAsync(SystemTenantId, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        repository.Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(x => x.ActorName).Returns("platform-admin");
        var eventBus = new Mock<IEventBus>();

        var handler = new SuspendTenantCommandHandler(repository.Object, user.Object, eventBus.Object);

        var result = await handler.Handle(new SuspendTenantCommand(SystemTenantId, null), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
        repository.Verify(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.DoesNotContain(eventBus.Invocations, i => i.Method.Name == nameof(IEventBus.PublishAsync));
    }

    [Fact]
    public void IsSystemTenant_TrueOnlyForInternal()
    {
        Assert.True(SystemTenantRules.IsSystemTenant(new Tenant { Code = "PLATFORM", Slug = "platform", Name = "Platform", DisplayName = "Platform", Domain = "platform.local", TenantType = TenantType.Internal }));
        Assert.False(SystemTenantRules.IsSystemTenant(new Tenant { Code = "ACME", Slug = "acme", Name = "Acme", DisplayName = "Acme", Domain = "acme.local", TenantType = TenantType.Customer }));
    }

    // FIX-TENANTLIST — the customer LIST/stats hide only the specific platform system tenant by Id, so
    // operator-created Internal tenants stay visible. The constant must be …0001 (NOT the audit …2121).
    [Fact]
    public void IsSystemTenantId_MatchesOnlyThePlatformSystemTenantId()
    {
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000001"), SystemTenantRules.PlatformSystemTenantId);
        Assert.True(SystemTenantRules.IsSystemTenantId(SystemTenantRules.PlatformSystemTenantId));
        Assert.False(SystemTenantRules.IsSystemTenantId(Guid.NewGuid()));
        // An operator-created Internal tenant (different Id) is NOT treated as the system tenant by the list filter.
        Assert.False(SystemTenantRules.IsSystemTenantId(Guid.Parse("00000000-0000-0000-0000-000000009999")));
    }

    private static Tenant CreateSystemTenant(TenantStatus status) => new()
    {
        Id = SystemTenantId,
        Code = "PLATFORM",
        Slug = "platform",
        Name = "Platform Admin Tenant",
        DisplayName = "Platform Admin Tenant",
        Domain = "platform.diten.tech",
        Status = status,
        TenantType = TenantType.Internal
    };
}

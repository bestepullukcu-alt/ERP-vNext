using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Quotas.Services;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Application.Features.Tenants.Handlers;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Tenants;

public sealed class InviteTenantAdminUserCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenTenantIsInternal_DoesNotRequireCommercialQuotaConfiguration()
    {
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var adminUserId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Code = "PLATFORM",
            Slug = "platform",
            Name = "Platform Admin Tenant",
            DisplayName = "Platform Admin Tenant",
            Domain = "platform.diten.tech",
            TenantType = TenantType.Internal,
            Status = TenantStatus.Active,
            CreatedBy = "test",
            AdminUsers =
            [
                new TenantAdminUser
                {
                    Id = adminUserId,
                    Name = "admin",
                    Email = "admin@diten.com",
                    Status = TenantAdminUserStatus.PendingInvitation
                }
            ]
        };

        var repository = new Mock<ITenantRegistryRepository>(MockBehavior.Strict);
        repository
            .Setup(x => x.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        repository
            .Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var currentUser = new Mock<ICurrentUserContext>(MockBehavior.Strict);
        currentUser.SetupGet(x => x.ActorName).Returns("test-admin");

        var invitationService = new Mock<IAdminUserInvitationService>(MockBehavior.Strict);
        invitationService
            .Setup(x => x.InviteAsync(tenant, tenant.AdminUsers[0], It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminUserInvitationResult(
                "http://localhost:5001/account/login",
                "temporary",
                UserProvisioned: true,
                InvitationEmailSent: true));

        var quotaService = new Mock<IQuotaService>(MockBehavior.Strict);
        var handler = new InviteTenantAdminUserCommandHandler(
            repository.Object,
            currentUser.Object,
            invitationService.Object,
            quotaService.Object,
            NullLogger<InviteTenantAdminUserCommandHandler>.Instance);

        var response = await handler.Handle(new InviteTenantAdminUserCommand(tenantId, adminUserId), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.NotNull(response.Data);
        Assert.Equal(nameof(TenantAdminUserStatus.Invited), response.Data!.Status);
        Assert.Equal(TenantAdminUserStatus.Invited, tenant.AdminUsers[0].Status);
        Assert.Equal(1, tenant.ActiveUserCount);

        quotaService.VerifyNoOtherCalls();
        repository.Verify(x => x.UpdateAsync(It.Is<Tenant>(t => t.Id == tenantId), It.IsAny<CancellationToken>()), Times.Once);
        invitationService.Verify(x => x.InviteAsync(tenant, tenant.AdminUsers[0], It.IsAny<CancellationToken>()), Times.Once);
    }
}

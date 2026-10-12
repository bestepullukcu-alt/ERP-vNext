using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Quotas.Services;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Application.Features.Tenants.Handlers;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Tenants;

/// <summary>
/// BL-459 F1 — users.max is AuthService's live count of Active + Invited users, so deleting a tenant administrator
/// gives nothing back: a release here was the second half of the double release (Auth Users screen + this list).
/// </summary>
public sealed class TenantAdminUserSeatCountTests
{
    [Fact]
    public async Task Deleting_a_tenant_admin_touches_no_quota()
    {
        var tenantId = Guid.NewGuid();
        var admin = new TenantAdminUser { Name = "a@acme.test", Email = "a@acme.test", Status = TenantAdminUserStatus.Active };
        var tenant = new Tenant
        {
            Id = tenantId, Code = "TEN123", Slug = "acme", Name = "Acme", DisplayName = "Acme",
            Domain = "acme.ditenteknoloji.com", AdminUsers = [admin]
        };
        var repository = new Mock<ITenantRegistryRepository>();
        repository.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        repository.Setup(r => r.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.SetupGet(u => u.ActorName).Returns("platform-admin");
        var quota = new Mock<IQuotaService>(MockBehavior.Strict); // any quota call throws

        var result = await new DeleteTenantAdminUserCommandHandler(repository.Object, currentUser.Object, quota.Object)
            .Handle(new DeleteTenantAdminUserCommand(tenantId, admin.Id), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Empty(tenant.AdminUsers);
        quota.VerifyNoOtherCalls();
    }
}

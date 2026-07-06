using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Application.Features.Tenants.Handlers;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Tenants;

// FIX-TENANT-ADMIN-INVITE-ACTIVATION (Part B) — the AuthService callback flips the matching TenantAdminUser
// Invited → Active. Idempotent + fail-safe across the match/no-match/already-active cases.
public sealed class ActivateTenantAdminUserCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static (ActivateTenantAdminUserCommandHandler handler, Mock<ITenantRegistryRepository> repo) Build(Tenant? tenant)
    {
        var repo = new Mock<ITenantRegistryRepository>();
        repo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        repo.Setup(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var handler = new ActivateTenantAdminUserCommandHandler(repo.Object, NullLogger<ActivateTenantAdminUserCommandHandler>.Instance);
        return (handler, repo);
    }

    private static Tenant TenantWithAdmin(string email, TenantAdminUserStatus status) => new()
    {
        Id = TenantId,
        Name = "Acme",
        Code = "ACME",
        Slug = "acme",
        DisplayName = "Acme",
        Domain = "acme.example.com",
        AdminUsers = { new TenantAdminUser { Name = "Admin", Email = email, Status = status } }
    };

    [Fact]
    public async Task Invited_admin_is_flipped_to_active_and_stamped_and_persisted()
    {
        var tenant = TenantWithAdmin("admin@test.com", TenantAdminUserStatus.Invited);
        var (handler, repo) = Build(tenant);

        var result = await handler.Handle(new ActivateTenantAdminUserCommand(TenantId, "Admin@Test.com"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        var admin = Assert.Single(tenant.AdminUsers);
        Assert.Equal(TenantAdminUserStatus.Active, admin.Status);
        Assert.NotNull(admin.ActivatedAt);
        repo.Verify(x => x.UpdateAsync(tenant, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Already_active_admin_is_a_no_op_no_persist()
    {
        var tenant = TenantWithAdmin("admin@test.com", TenantAdminUserStatus.Active);
        var (handler, repo) = Build(tenant);

        var result = await handler.Handle(new ActivateTenantAdminUserCommand(TenantId, "admin@test.com"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Null(Assert.Single(tenant.AdminUsers).ActivatedAt); // untouched
        repo.Verify(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task No_matching_admin_email_is_a_no_op()
    {
        var tenant = TenantWithAdmin("someone-else@test.com", TenantAdminUserStatus.Invited);
        var (handler, repo) = Build(tenant);

        var result = await handler.Handle(new ActivateTenantAdminUserCommand(TenantId, "admin@test.com"), CancellationToken.None);

        Assert.True(result.IsSuccessful); // non-admin tenant_users aren't tracked → no-op success
        repo.Verify(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Unknown_tenant_is_a_no_op()
    {
        var (handler, repo) = Build(tenant: null);

        var result = await handler.Handle(new ActivateTenantAdminUserCommand(TenantId, "admin@test.com"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        repo.Verify(x => x.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_email_is_a_no_op_without_repository_lookup(string email)
    {
        var (handler, repo) = Build(TenantWithAdmin("admin@test.com", TenantAdminUserStatus.Invited));

        var result = await handler.Handle(new ActivateTenantAdminUserCommand(TenantId, email), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        repo.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

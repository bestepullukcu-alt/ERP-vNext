using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Features.Users.Handlers.QueryHandlers;
using Diten.AuthService.Application.Features.Users.Queries;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Tests.Users;

public sealed class ValidateUserReferenceQueryHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherTenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task ValidActiveSameTenantNonDeletedUserSucceeds()
    {
        var userId = Guid.NewGuid();
        var handler = CreateHandler(TenantId, [CreateUser(userId, TenantId)]);

        var result = await handler.Handle(new ValidateUserReferenceQuery(userId), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(userId, result.Data!.UserId);
        Assert.True(result.Data.Referenceable);
    }

    [Fact]
    public async Task MissingUserReturnsNotFound()
    {
        var handler = CreateHandler(TenantId, []);

        var result = await handler.Handle(new ValidateUserReferenceQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task CrossTenantUserReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        var handler = CreateHandler(TenantId, [CreateUser(userId, OtherTenantId)]);

        var result = await handler.Handle(new ValidateUserReferenceQuery(userId), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task SoftDeletedUserReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, TenantId);
        user.IsDeleted = true;
        var handler = CreateHandler(TenantId, [user]);

        var result = await handler.Handle(new ValidateUserReferenceQuery(userId), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task InactiveUserReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, TenantId);
        user.Deactivate();
        var handler = CreateHandler(TenantId, [user]);

        var result = await handler.Handle(new ValidateUserReferenceQuery(userId), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task EmptyUserIdReturnsBadRequest()
    {
        var handler = CreateHandler(TenantId, []);

        var result = await handler.Handle(new ValidateUserReferenceQuery(Guid.Empty), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task MissingTenantContextReturnsBadRequest()
    {
        var repository = new InMemoryUserRepository([]);
        var handler = new ValidateUserReferenceQueryHandler(repository, new TestTenantContext());

        var result = await handler.Handle(new ValidateUserReferenceQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
    }

    private static ValidateUserReferenceQueryHandler CreateHandler(Guid tenantId, IEnumerable<User> users)
    {
        var tenantContext = new TestTenantContext();
        tenantContext.SetTenant(tenantId);
        return new ValidateUserReferenceQueryHandler(new InMemoryUserRepository(users), tenantContext);
    }

    private static User CreateUser(Guid id, Guid tenantId)
    {
        return new User("user@diten.com", "hash", "First", "Last", tenantId)
        {
            Id = id
        };
    }

    private sealed class TestTenantContext : ITenantContext
    {
        private Guid _tenantId;

        public Guid TenantId => IsResolved ? _tenantId : throw new InvalidOperationException("Tenant not resolved.");
        public bool IsResolved { get; private set; }
        public bool IsPlatformContext => false;
        public Guid? TargetTenantId => null;

        public void SetTenant(Guid tenantId)
        {
            _tenantId = tenantId;
            IsResolved = true;
        }

        public void SetPlatformContext(Guid targetTenantId) => SetTenant(targetTenantId);
    }
}

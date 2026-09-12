using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Features.Users.Handlers.QueryHandlers;
using Diten.AuthService.Application.Features.Users.Queries;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.Enums;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// WP-INFRA-AUTH-ACCOUNT-KIND-01 — the assertion is a FACT, not a verdict: an inactive account answers 200 with
/// active=false; a missing account and another tenant's account answer the same 404 (K1-e: let the two bodies differ
/// and the identity test below goes red).
/// </summary>
public sealed class GetAccountAssertionQueryHandlerTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Theory]
    [InlineData(AccountKind.Unknown, "Unknown")]
    [InlineData(AccountKind.Human, "Human")]
    [InlineData(AccountKind.Service, "Service")]
    public async Task Reports_the_kind_by_NAME_and_the_active_flag(AccountKind kind, string expectedName)
    {
        var user = new User("u@acme.test", "hash:x", "U", "Ser", TenantA);
        user.SetAccountKind(kind);
        var handler = Handler(TenantA, [user]);
        var before = DateTimeOffset.UtcNow;

        var result = await handler.Handle(new GetAccountAssertionQuery(user.Id), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(user.Id, result.Data!.UserId);
        Assert.True(result.Data.Active);
        Assert.Equal(expectedName, result.Data.AccountKind);
        Assert.InRange(result.Data.AssertedAt, before.AddSeconds(-1), DateTimeOffset.UtcNow.AddSeconds(1));
        Assert.Equal(user.UpdatedAt, result.Data.UserUpdatedAt);
    }

    [Fact]
    public async Task An_inactive_account_is_still_asserted_with_active_false()
    {
        var user = new User("p@acme.test", "hash:x", "Pa", "Sif", TenantA);
        user.SetAccountKind(AccountKind.Human);
        user.Deactivate();
        var handler = Handler(TenantA, [user]);

        var result = await handler.Handle(new GetAccountAssertionQuery(user.Id), CancellationToken.None);

        Assert.Equal(200, result.StatusCode);
        Assert.False(result.Data!.Active);
        Assert.Equal("Human", result.Data.AccountKind);
    }

    [Fact]
    public async Task Missing_and_cross_tenant_are_the_identical_404()
    {
        var foreign = new User("f@other.test", "hash:x", "F", "O", TenantB);
        foreign.SetAccountKind(AccountKind.Human);
        var handler = Handler(TenantA, [foreign]);

        var missing = await handler.Handle(new GetAccountAssertionQuery(Guid.NewGuid()), CancellationToken.None);
        var crossTenant = await handler.Handle(new GetAccountAssertionQuery(foreign.Id), CancellationToken.None);

        Assert.Equal(404, missing.StatusCode);
        Assert.Equal(404, crossTenant.StatusCode);
        Assert.False(missing.IsSuccessful);
        Assert.False(crossTenant.IsSuccessful);
        Assert.Null(missing.Data);
        Assert.Null(crossTenant.Data);
        Assert.Equal(missing.Errors, crossTenant.Errors);
        Assert.Equal(missing.ErrorCodes, crossTenant.ErrorCodes);
        Assert.Equal(new[] { GetAccountAssertionQueryHandler.NotFoundMessage }, crossTenant.Errors);
    }

    [Fact]
    public async Task Soft_deleted_user_is_not_found()
    {
        var user = new User("d@acme.test", "hash:x", "De", "Leted", TenantA) { IsDeleted = true };
        var handler = Handler(TenantA, [user]);

        var result = await handler.Handle(new GetAccountAssertionQuery(user.Id), CancellationToken.None);

        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Empty_id_and_unresolved_tenant_are_400()
    {
        var resolved = Handler(TenantA, []);
        var unresolved = new GetAccountAssertionQueryHandler(new InMemoryUserRepository([]), new TestTenantContext());

        Assert.Equal(400, (await resolved.Handle(new GetAccountAssertionQuery(Guid.Empty), CancellationToken.None)).StatusCode);
        Assert.Equal(400, (await unresolved.Handle(new GetAccountAssertionQuery(Guid.NewGuid()), CancellationToken.None)).StatusCode);
    }

    private static GetAccountAssertionQueryHandler Handler(Guid tenantId, IEnumerable<User> users)
    {
        var ctx = new TestTenantContext();
        ctx.SetTenant(tenantId);
        return new GetAccountAssertionQueryHandler(new InMemoryUserRepository(users), ctx);
    }

    private sealed class TestTenantContext : ITenantContext
    {
        private Guid _tenantId;
        public Guid TenantId => IsResolved ? _tenantId : throw new InvalidOperationException("Tenant not resolved.");
        public bool IsResolved { get; private set; }
        public bool IsPlatformContext { get; private set; }
        public Guid? TargetTenantId { get; private set; }
        public void SetTenant(Guid tenantId) { _tenantId = tenantId; IsResolved = true; }
        public void SetPlatformContext(Guid targetTenantId) { _tenantId = targetTenantId; IsResolved = true; IsPlatformContext = true; TargetTenantId = targetTenantId; }
    }
}

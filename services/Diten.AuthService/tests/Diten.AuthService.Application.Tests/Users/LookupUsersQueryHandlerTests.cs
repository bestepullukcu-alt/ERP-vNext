using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Users.Handlers.QueryHandlers;
using Diten.AuthService.Application.Features.Users.Queries;
using Diten.AuthService.Application.Features.Users.Validators;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// WP-INFRA-AUTH-ACCOUNT-KIND-01 — the lookup's SHAPE (id + label, nothing else) and its bounds. The active/tenant
/// filter is the repository's contract; the in-memory repository mirrors it here and the Mongo one is proven over
/// HTTP by AccountKindEndpointTests.
/// </summary>
public sealed class LookupUsersQueryHandlerTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task Returns_id_and_display_label_only_for_active_users_of_the_tenant()
    {
        var active = new User("zehra@acme.test", "hash:x", "Zehra", "Yıldız", TenantA);
        var passive = new User("pasif@acme.test", "hash:x", "Pasif", "Demir", TenantA);
        passive.Deactivate();
        var deleted = new User("gone@acme.test", "hash:x", "Gone", "User", TenantA) { IsDeleted = true };
        var foreign = new User("fatma@other.test", "hash:x", "Fatma", "Öztürk", TenantB);
        var handler = Handler([active, passive, deleted, foreign]);

        var result = await handler.Handle(new LookupUsersQuery(null, 20), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        var item = Assert.Single(result.Data!);
        Assert.Equal(active.Id, item.UserId);
        Assert.Equal("Zehra Yıldız", item.DisplayLabel);
    }

    [Fact]
    public void The_lookup_row_type_carries_no_email_role_status_or_kind()
    {
        // The DTO IS the disclosure boundary of auth.users.lookup: two members, by name.
        var members = typeof(UserLookupItemDto).GetProperties().Select(p => p.Name).OrderBy(n => n).ToArray();

        Assert.Equal(new[] { "DisplayLabel", "UserId" }, members);
    }

    [Fact]
    public async Task Search_tokens_match_first_or_last_name_and_the_limit_caps_the_result()
    {
        var users = new[]
        {
            new User("a@acme.test", "hash:x", "Ayşe", "Kaya", TenantA),
            new User("b@acme.test", "hash:x", "Ali", "Kaya", TenantA),
            new User("c@acme.test", "hash:x", "Zehra", "Yıldız", TenantA)
        };
        var handler = Handler(users);

        var kayas = await handler.Handle(new LookupUsersQuery("kaya", 20), CancellationToken.None);
        var aliKaya = await handler.Handle(new LookupUsersQuery("ali kay", 20), CancellationToken.None);
        var limited = await handler.Handle(new LookupUsersQuery(null, 2), CancellationToken.None);
        var byEmail = await handler.Handle(new LookupUsersQuery("a@acme.test", 20), CancellationToken.None);

        Assert.Equal(2, kayas.Data!.Count);
        Assert.Equal("Ali Kaya", Assert.Single(aliKaya.Data!).DisplayLabel);
        Assert.Equal(2, limited.Data!.Count);
        Assert.Empty(byEmail.Data!); // e-mail is never a match field
    }

    [Fact]
    public async Task Unresolved_tenant_context_is_a_400_not_a_cross_tenant_read()
    {
        var handler = new LookupUsersQueryHandler(new InMemoryUserRepository([]), new TestTenantContext());

        var result = await handler.Handle(new LookupUsersQuery("a", 20), CancellationToken.None);

        Assert.Equal(400, result.StatusCode);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(50, true)]
    [InlineData(51, false)]
    public void Validator_bounds_the_limit_to_1_50(int limit, bool expectedValid)
    {
        var result = new LookupUsersQueryValidator().Validate(new LookupUsersQuery("a", limit));

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Validator_bounds_the_search_to_100_characters(int length, bool expectedValid)
    {
        var result = new LookupUsersQueryValidator().Validate(new LookupUsersQuery(new string('a', length), 20));

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData("Zehra", "Yıldız", "Zehra Yıldız")]
    [InlineData("Zehra", "", "Zehra")]
    [InlineData("", "Yıldız", "Yıldız")]
    [InlineData(" Zehra ", " Yıldız ", "Zehra Yıldız")]
    public void Display_label_is_first_and_last_name_with_a_missing_half_collapsed(string first, string last, string expected)
    {
        Assert.Equal(expected, LookupUsersQueryHandler.DisplayLabel(first, last));
    }

    private static LookupUsersQueryHandler Handler(IEnumerable<User> users)
    {
        var ctx = new TestTenantContext();
        ctx.SetTenant(TenantA);
        return new LookupUsersQueryHandler(new InMemoryUserRepository(users), ctx);
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

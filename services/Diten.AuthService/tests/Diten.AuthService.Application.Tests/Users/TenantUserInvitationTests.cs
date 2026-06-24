using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Commands;
using Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;
using Diten.AuthService.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.AuthService.Application.Tests.Users;

// MOD-0018 Users invitation flow (admin sets no password; user receives a set-password link).
// Covers: (a) passwordless create → invitation, (b) set-password redemption, (c) resend,
// (d) self-service (password) create regression, plus tenant isolation.
public sealed class TenantUserInvitationTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    // ── (a) Passwordless create → invitation: Inactive + MustChangePassword + 7-day token ──
    [Fact]
    public async Task Create_without_password_creates_inactive_invited_user_with_token()
    {
        var repo = new InMemoryUserRepository([]);
        var email = new FakeInvitationEmailService();
        var handler = CreateUserHandler(repo, email, devEnvironment: true);

        var before = DateTime.UtcNow;
        var result = await handler.Handle(new CreateUserCommand("invitee@acme.test", null, "In", "Vitee"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(201, result.StatusCode);

        var created = Assert.Single(await repo.GetAllByTenantAsync(TenantA, 1, 50, CancellationToken.None));
        Assert.False(created.IsActive);                       // invited users start inactive
        Assert.True(created.MustChangePassword);              // must set password before login
        Assert.False(created.EmailConfirmed);
        Assert.False(string.IsNullOrWhiteSpace(created.PasswordResetTokenHash));
        Assert.NotNull(created.PasswordResetTokenExpiresAt);
        Assert.InRange(created.PasswordResetTokenExpiresAt!.Value, before.AddDays(7).AddMinutes(-2), before.AddDays(7).AddMinutes(2));
        Assert.Single(email.Sends);                           // invitation email attempted
        Assert.Equal("invitee@acme.test", email.Sends[0].email);
    }

    [Fact]
    public async Task Create_without_password_skips_password_policy_validation()
    {
        var policy = new FakePasswordPolicyService();
        var handler = CreateUserHandler(new InMemoryUserRepository([]), new FakeInvitationEmailService(), policy: policy);

        await handler.Handle(new CreateUserCommand("invitee@acme.test", null, "In", "Vitee"), CancellationToken.None);

        Assert.Empty(policy.Calls); // no password supplied → nothing to validate
    }

    [Fact]
    public async Task Create_without_password_surfaces_setup_url_in_development_only()
    {
        var devResult = await CreateUserHandler(new InMemoryUserRepository([]), new FakeInvitationEmailService(), devEnvironment: true)
            .Handle(new CreateUserCommand("dev@acme.test", null, "D", "V"), CancellationToken.None);
        Assert.False(string.IsNullOrWhiteSpace(devResult.Data!.SetupUrl));

        var prodResult = await CreateUserHandler(new InMemoryUserRepository([]), new FakeInvitationEmailService(), devEnvironment: false)
            .Handle(new CreateUserCommand("prod@acme.test", null, "P", "V"), CancellationToken.None);
        Assert.Null(prodResult.Data!.SetupUrl); // prod never leaks the link
    }

    // ── (d) Self-service (password supplied) create regression: active, no invite token ──
    [Fact]
    public async Task Create_with_password_keeps_self_service_active_user()
    {
        var repo = new InMemoryUserRepository([]);
        var policy = new FakePasswordPolicyService();
        var handler = CreateUserHandler(repo, new FakeInvitationEmailService(), policy: policy);

        var result = await handler.Handle(new CreateUserCommand("self@acme.test", "Sup3rSecret!", "Self", "Serve"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(201, result.StatusCode);
        Assert.Null(result.Data!.SetupUrl);

        var created = Assert.Single(await repo.GetAllByTenantAsync(TenantA, 1, 50, CancellationToken.None));
        Assert.True(created.IsActive);
        Assert.False(created.MustChangePassword);
        Assert.Null(created.PasswordResetTokenHash);
        Assert.Equal("hash:Sup3rSecret!", created.PasswordHash);
        Assert.Equal(("create_user", TenantA), (policy.Calls.Single().context, policy.Calls.Single().tenantId));
    }

    [Fact]
    public async Task Create_duplicate_email_conflicts()
    {
        var existing = new User("dupe@acme.test", "hash:x", "Du", "Pe", TenantA);
        var handler = CreateUserHandler(new InMemoryUserRepository([existing]), new FakeInvitationEmailService());

        var result = await handler.Handle(new CreateUserCommand("dupe@acme.test", null, "Du", "Pe"), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
    }

    // ── (b) Set-password redemption → Active + password set + token cleared + refresh revoked ──
    [Fact]
    public async Task SetPassword_redeems_token_activates_user_and_clears_token()
    {
        var hasher = new FakeRefreshTokenHasher();
        var invited = NewInvitedUser("redeem@acme.test", TenantA, hasher.Hash("token-1"), DateTime.UtcNow.AddDays(3));
        var repo = new InMemoryUserRepository([invited]);
        var refreshTokens = new FakeRefreshTokenRepository();
        var handler = SetPasswordHandler(repo, refreshTokens, hasher);

        var result = await handler.Handle(new SetTenantPasswordCommand("redeem@acme.test", "token-1", "BrandN3w!"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(204, result.StatusCode);
        Assert.True(invited.IsActive);
        Assert.True(invited.EmailConfirmed);
        Assert.False(invited.MustChangePassword);
        Assert.Null(invited.PasswordResetTokenHash);             // single-use: cleared
        Assert.Equal("hash:BrandN3w!", invited.PasswordHash);
        Assert.Equal((invited.Id, TenantA), refreshTokens.RevokeAllCall); // sessions revoked, tenant-scoped
    }

    [Fact]
    public async Task SetPassword_with_expired_token_fails()
    {
        var hasher = new FakeRefreshTokenHasher();
        var invited = NewInvitedUser("expired@acme.test", TenantA, hasher.Hash("token-1"), DateTime.UtcNow.AddMinutes(-1));
        var handler = SetPasswordHandler(new InMemoryUserRepository([invited]), new FakeRefreshTokenRepository(), hasher);

        var result = await handler.Handle(new SetTenantPasswordCommand("expired@acme.test", "token-1", "BrandN3w!"), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
        Assert.False(invited.IsActive);
    }

    [Fact]
    public async Task SetPassword_with_mismatched_email_fails()
    {
        var hasher = new FakeRefreshTokenHasher();
        var invited = NewInvitedUser("owner@acme.test", TenantA, hasher.Hash("token-1"), DateTime.UtcNow.AddDays(3));
        var handler = SetPasswordHandler(new InMemoryUserRepository([invited]), new FakeRefreshTokenRepository(), hasher);

        // Valid token but a different email than the token owner → rejected (defense in depth).
        var result = await handler.Handle(new SetTenantPasswordCommand("attacker@acme.test", "token-1", "BrandN3w!"), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task SetPassword_resolves_correct_user_across_tenants()
    {
        var hasher = new FakeRefreshTokenHasher();
        var userA = NewInvitedUser("a@acme.test", TenantA, hasher.Hash("token-A"), DateTime.UtcNow.AddDays(3));
        var userB = NewInvitedUser("b@acme.test", TenantB, hasher.Hash("token-B"), DateTime.UtcNow.AddDays(3));
        var policy = new FakePasswordPolicyService();
        var refreshTokens = new FakeRefreshTokenRepository();
        var handler = SetPasswordHandler(new InMemoryUserRepository([userA, userB]), refreshTokens, hasher, policy);

        // token-B belongs to tenant B; redemption must operate strictly on tenant B.
        var result = await handler.Handle(new SetTenantPasswordCommand("b@acme.test", "token-B", "BrandN3w!"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.True(userB.IsActive);
        Assert.False(userA.IsActive);                         // tenant A untouched
        Assert.Equal(TenantB, policy.Calls.Single().tenantId); // policy validated against the owner's tenant
        Assert.Equal((userB.Id, TenantB), refreshTokens.RevokeAllCall);
    }

    // ── (c) Resend invitation → fresh 7-day token, email re-sent ──
    [Fact]
    public async Task Resend_issues_new_token_and_resends_email()
    {
        var hasher = new FakeRefreshTokenHasher();
        var invited = NewInvitedUser("resend@acme.test", TenantA, hasher.Hash("old-token"), DateTime.UtcNow.AddDays(1));
        var repo = new InMemoryUserRepository([invited]);
        var email = new FakeInvitationEmailService();
        var handler = ResendHandler(repo, email, hasher, TenantA);

        var before = DateTime.UtcNow;
        var result = await handler.Handle(new ResendUserInvitationCommand(invited.Id), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(200, result.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Data!.SetupUrl)); // dev: copyable link surfaced
        Assert.NotEqual(hasher.Hash("old-token"), invited.PasswordResetTokenHash); // token rotated
        Assert.InRange(invited.PasswordResetTokenExpiresAt!.Value, before.AddDays(7).AddMinutes(-2), before.AddDays(7).AddMinutes(2));
        Assert.Single(email.Sends);
    }

    [Fact]
    public async Task Resend_for_already_active_user_conflicts()
    {
        var active = new User("done@acme.test", "hash:x", "Do", "Ne", TenantA); // active, no MustChangePassword
        var handler = ResendHandler(new InMemoryUserRepository([active]), new FakeInvitationEmailService(), new FakeRefreshTokenHasher(), TenantA);

        var result = await handler.Handle(new ResendUserInvitationCommand(active.Id), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public async Task Resend_for_other_tenant_user_returns_not_found()
    {
        var hasher = new FakeRefreshTokenHasher();
        var invited = NewInvitedUser("other@acme.test", TenantB, hasher.Hash("t"), DateTime.UtcNow.AddDays(1));
        // Handler tenant context = TenantA, but the user belongs to TenantB → not found (isolation).
        var handler = ResendHandler(new InMemoryUserRepository([invited]), new FakeInvitationEmailService(), hasher, TenantA);

        var result = await handler.Handle(new ResendUserInvitationCommand(invited.Id), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    // ── Builders ──
    private static User NewInvitedUser(string email, Guid tenantId, string tokenHash, DateTime expiresAt)
    {
        var user = new User(email, "hash:placeholder", "Inv", "Ited", tenantId);
        user.SetPasswordResetToken(tokenHash, expiresAt);
        user.Deactivate();
        user.RequirePasswordChange(null);
        return user;
    }

    private static CreateUserCommandHandler CreateUserHandler(
        InMemoryUserRepository repo,
        FakeInvitationEmailService email,
        FakePasswordPolicyService? policy = null,
        bool devEnvironment = true)
    {
        return new CreateUserCommandHandler(
            repo,
            new FakePasswordHasher(),
            policy ?? new FakePasswordPolicyService(),
            TenantContextFor(TenantA),
            new FakeTokenService(),
            new FakeRefreshTokenHasher(),
            new FakeHostEnvironment(devEnvironment),
            email,
            NullLogger<CreateUserCommandHandler>.Instance);
    }

    private static SetTenantPasswordCommandHandler SetPasswordHandler(
        InMemoryUserRepository repo,
        FakeRefreshTokenRepository refreshTokens,
        FakeRefreshTokenHasher hasher,
        FakePasswordPolicyService? policy = null)
    {
        return new SetTenantPasswordCommandHandler(
            repo,
            new FakePasswordHasher(),
            policy ?? new FakePasswordPolicyService(),
            hasher,
            refreshTokens,
            NullLogger<SetTenantPasswordCommandHandler>.Instance);
    }

    private static ResendUserInvitationCommandHandler ResendHandler(
        InMemoryUserRepository repo,
        FakeInvitationEmailService email,
        FakeRefreshTokenHasher hasher,
        Guid contextTenant)
    {
        return new ResendUserInvitationCommandHandler(
            repo,
            TenantContextFor(contextTenant),
            new FakeTokenService(),
            hasher,
            email,
            new FakeHostEnvironment(isDevelopment: true),
            NullLogger<ResendUserInvitationCommandHandler>.Instance);
    }

    private static TestTenantContext TenantContextFor(Guid tenantId)
    {
        var ctx = new TestTenantContext();
        ctx.SetTenant(tenantId);
        return ctx;
    }

    // ── Inline fakes (codebase convention: hand-written, no Moq) ──
    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => "hash:" + password;
        public bool Verify(string password, string hash) => hash == "hash:" + password;
    }

    private sealed class FakeRefreshTokenHasher : IRefreshTokenHasher
    {
        public string Hash(string refreshToken) => "rh:" + refreshToken;
    }

    private sealed class FakeTokenService : ITokenService
    {
        private int _counter;
        public string GenerateRefreshToken() => "rt-" + System.Threading.Interlocked.Increment(ref _counter);
        public string GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions) => throw new NotSupportedException();
        public string GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes) => throw new NotSupportedException();
        public string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions) => throw new NotSupportedException();
        public string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes) => throw new NotSupportedException();
        public string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes, bool requiresPasswordChange) => throw new NotSupportedException();
        public System.Security.Claims.ClaimsPrincipal GetPrincipalFromExpiredToken(string token) => throw new NotSupportedException();
    }

    private sealed class FakePasswordPolicyService : IPasswordPolicyService
    {
        public List<(Guid tenantId, Guid? userId, string password, string context)> Calls { get; } = [];
        public Task ValidateTenantPasswordAsync(Guid tenantId, Guid? userId, string password, string context, CancellationToken ct)
        {
            Calls.Add((tenantId, userId, password, context));
            return Task.CompletedTask;
        }
        public string GenerateTemporaryPassword(TenantLoginSettingsSnapshot settings) => throw new NotSupportedException();
    }

    private sealed class FakeInvitationEmailService : ITenantUserInvitationEmailService
    {
        public bool ThrowOnSend { get; init; }
        public List<(string email, string token)> Sends { get; } = [];
        public string BuildTenantSetPasswordUrl(string email, string setupToken) =>
            $"http://localhost:5001/account/set-password?email={email}&token={setupToken}";
        public Task SendTenantUserInvitationAsync(string email, string setupToken, CancellationToken ct)
        {
            if (ThrowOnSend) throw new InvalidOperationException("smtp down");
            Sends.Add((email, setupToken));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        public (Guid userId, Guid tenantId)? RevokeAllCall { get; private set; }
        public Task RevokeAllByUserAsync(Guid userId, Guid tenantId, CancellationToken ct)
        {
            RevokeAllCall = (userId, tenantId);
            return Task.CompletedTask;
        }
        public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct) => throw new NotSupportedException();
        public Task CreateAsync(RefreshToken refreshToken, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(RefreshToken refreshToken, CancellationToken ct) => throw new NotSupportedException();
        public Task RevokeAsync(string token, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeHostEnvironment(bool isDevelopment) : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = isDevelopment ? "Development" : "Production";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class TestTenantContext : ITenantContext
    {
        private Guid _tenantId;
        public Guid TenantId => IsResolved ? _tenantId : throw new InvalidOperationException("Tenant not resolved.");
        public bool IsResolved { get; private set; }
        public bool IsPlatformContext => false;
        public Guid? TargetTenantId => null;
        public void SetTenant(Guid tenantId) { _tenantId = tenantId; IsResolved = true; }
        public void SetPlatformContext(Guid targetTenantId) => SetTenant(targetTenantId);
    }
}

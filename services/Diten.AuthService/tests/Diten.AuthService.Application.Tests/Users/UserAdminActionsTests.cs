using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Commands;
using Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;
using Diten.AuthService.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.AuthService.Application.Tests.Users;

// MOD-0018 Users Admin actions: enable/disable (with refresh revoke on disable) and admin reset
// (active → force-change; pending → 409). Mirrors the invitation test fakes (hand-written, no Moq).
public sealed class UserAdminActionsTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    // ── Disable / Enable ──
    [Fact]
    public async Task Disable_deactivates_and_revokes_refresh_tokens()
    {
        var user = new User("u@acme.test", "hash:x", "U", "Ser", TenantA); // active
        var refreshTokens = new FakeRefreshTokenRepository();
        var handler = StatusHandler(new InMemoryUserRepository([user]), refreshTokens);

        var result = await handler.Handle(new SetUserActiveStatusCommand(user.Id, false), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(204, result.StatusCode);
        Assert.False(user.IsActive);
        Assert.Equal((user.Id, TenantA), refreshTokens.RevokeAllCall); // sessions terminated, tenant-scoped
    }

    [Fact]
    public async Task Enable_activates_without_revoking()
    {
        var user = new User("u@acme.test", "hash:x", "U", "Ser", TenantA);
        user.Deactivate();
        var refreshTokens = new FakeRefreshTokenRepository();
        var handler = StatusHandler(new InMemoryUserRepository([user]), refreshTokens);

        var result = await handler.Handle(new SetUserActiveStatusCommand(user.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.True(user.IsActive);
        Assert.Null(refreshTokens.RevokeAllCall); // enabling never revokes
    }

    [Fact]
    public async Task Disable_then_enable_round_trip_revokes_only_on_disable()
    {
        var user = new User("u@acme.test", "hash:x", "U", "Ser", TenantA);
        var refreshTokens = new FakeRefreshTokenRepository();
        var handler = StatusHandler(new InMemoryUserRepository([user]), refreshTokens);

        await handler.Handle(new SetUserActiveStatusCommand(user.Id, false), CancellationToken.None);
        Assert.False(user.IsActive);
        Assert.Equal(1, refreshTokens.RevokeAllCount);

        await handler.Handle(new SetUserActiveStatusCommand(user.Id, true), CancellationToken.None);
        Assert.True(user.IsActive);
        Assert.Equal(1, refreshTokens.RevokeAllCount); // still 1 — enable did not revoke
    }

    [Fact]
    public async Task SetActiveStatus_user_not_found_returns_404()
    {
        var handler = StatusHandler(new InMemoryUserRepository([]), new FakeRefreshTokenRepository());

        var result = await handler.Handle(new SetUserActiveStatusCommand(Guid.NewGuid(), false), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    // ── Admin reset ──
    [Fact]
    public async Task AdminReset_for_active_user_issues_token_and_forces_change()
    {
        var hasher = new FakeRefreshTokenHasher();
        var user = new User("active@acme.test", "hash:x", "A", "C", TenantA); // active, no MustChangePassword
        var email = new FakeInvitationEmailService();
        var handler = ResetHandler(new InMemoryUserRepository([user]), email, hasher);

        var before = DateTime.UtcNow;
        var result = await handler.Handle(new AdminResetPasswordCommand(user.Id), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(200, result.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Data!.SetupUrl)); // dev: copyable link surfaced
        Assert.False(string.IsNullOrWhiteSpace(user.PasswordResetTokenHash)); // token issued
        Assert.InRange(user.PasswordResetTokenExpiresAt!.Value, before.AddDays(7).AddMinutes(-2), before.AddDays(7).AddMinutes(2));
        Assert.True(user.MustChangePassword); // force-change applied
        Assert.Single(email.Sends);
    }

    [Fact]
    public async Task AdminReset_for_pending_user_conflicts_and_leaves_token_untouched()
    {
        var hasher = new FakeRefreshTokenHasher();
        var user = new User("pending@acme.test", "hash:x", "P", "C", TenantA);
        user.SetPasswordResetToken(hasher.Hash("orig"), DateTime.UtcNow.AddDays(3));
        user.RequirePasswordChange(null); // pending invitation
        var email = new FakeInvitationEmailService();
        var handler = ResetHandler(new InMemoryUserRepository([user]), email, hasher);

        var result = await handler.Handle(new AdminResetPasswordCommand(user.Id), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(hasher.Hash("orig"), user.PasswordResetTokenHash); // unchanged
        Assert.Empty(email.Sends);
    }

    [Fact]
    public async Task AdminReset_user_not_found_returns_404()
    {
        var handler = ResetHandler(new InMemoryUserRepository([]), new FakeInvitationEmailService(), new FakeRefreshTokenHasher());

        var result = await handler.Handle(new AdminResetPasswordCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    // ── Builders ──
    private static SetUserActiveStatusCommandHandler StatusHandler(InMemoryUserRepository repo, FakeRefreshTokenRepository refreshTokens) =>
        new(repo, refreshTokens, TenantContextFor(TenantA), NullLogger<SetUserActiveStatusCommandHandler>.Instance);

    private static AdminResetPasswordCommandHandler ResetHandler(InMemoryUserRepository repo, FakeInvitationEmailService email, FakeRefreshTokenHasher hasher) =>
        new(repo, TenantContextFor(TenantA), new FakeTokenService(), hasher, email, new FakeHostEnvironment(isDevelopment: true), NullLogger<AdminResetPasswordCommandHandler>.Instance);

    private static TestTenantContext TenantContextFor(Guid tenantId)
    {
        var ctx = new TestTenantContext();
        ctx.SetTenant(tenantId);
        return ctx;
    }

    // ── Inline fakes ──
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

    private sealed class FakeInvitationEmailService : ITenantUserInvitationEmailService
    {
        public List<(string email, string token)> Sends { get; } = [];
        public string BuildTenantSetPasswordUrl(string email, string setupToken) =>
            $"http://localhost:5001/account/set-password?email={email}&token={setupToken}";
        public Task SendTenantUserInvitationAsync(string email, string setupToken, CancellationToken ct)
        {
            Sends.Add((email, setupToken));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        public (Guid userId, Guid tenantId)? RevokeAllCall { get; private set; }
        public int RevokeAllCount { get; private set; }
        public Task RevokeAllByUserAsync(Guid userId, Guid tenantId, CancellationToken ct)
        {
            RevokeAllCall = (userId, tenantId);
            RevokeAllCount++;
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

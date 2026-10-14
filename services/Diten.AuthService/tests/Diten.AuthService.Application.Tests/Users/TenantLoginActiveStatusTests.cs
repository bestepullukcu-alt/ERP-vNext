using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Authorization;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Application.Features.Auth.Commands;
using Diten.AuthService.Application.Features.Auth.Handlers.CommandHandlers;
using Diten.AuthService.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.AuthService.Application.Tests.Users;

// MOD-0018 security fix: a disabled (IsActive == false) tenant user cannot log in even with the
// correct password. Active users still authenticate (no regression).
public sealed class TenantLoginActiveStatusTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private const string CorrectPassword = "Correct-Horse-1!";

    [Fact]
    public async Task Disabled_user_with_correct_password_is_denied_401()
    {
        var user = new User("disabled@acme.test", "hash:x", "Dis", "Abled", TenantA);
        user.Deactivate();
        var audit = new FakeAuditService();
        var handler = CreateHandler(user, audit);

        var result = await handler.Handle(Login("disabled@acme.test"), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(401, result.StatusCode);
        Assert.Contains("disabled", string.Join(" ", result.Errors), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tenant_login_denied_inactive", audit.Events); // audited
    }

    [Fact]
    public async Task Active_user_with_correct_password_succeeds()
    {
        var user = new User("active@acme.test", "hash:x", "Act", "Ive", TenantA); // active by default
        var handler = CreateHandler(user, new FakeAuditService());

        var result = await handler.Handle(Login("active@acme.test"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.False(string.IsNullOrWhiteSpace(result.Data!.AccessToken));
    }

    [Fact]
    public async Task Wrong_password_still_fails_before_active_check()
    {
        var user = new User("active@acme.test", "hash:x", "Act", "Ive", TenantA);
        var handler = CreateHandler(user, new FakeAuditService());

        var result = await handler.Handle(new LoginCommand("active@acme.test", "wrong", "127.0.0.1", null), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(401, result.StatusCode);
    }

    private static LoginCommand Login(string email) => new(email, CorrectPassword, "127.0.0.1", "test-agent");

    /// <summary>WP-USERS-ROLES-CLOSE-FIX1 FIX2 item 3 — a tenant whose login settings cannot be read (an unknown GUID) still
    /// gets LOGIN_UNAVAILABLE every time, but the anonymous audit row is written within a global budget, never once per
    /// request.</summary>
    [Fact]
    public async Task An_unknown_tenant_is_answered_every_time_but_audited_within_a_budget()
    {
        var audit = new FakeAuditService();
        var handler = CreateHandler(new User("x@acme.test", "hash:x", "X", "Y", TenantA), audit, new ThrowingSettings());
        var answers = new List<Response<Diten.AuthService.Application.DTOs.AuthResponse>>();

        for (var i = 0; i < 3 * AnonymousAuditBudget.RowsPerWindow; i++)
            answers.Add(await handler.Handle(Login("x@acme.test"), CancellationToken.None));

        Assert.All(answers, answer => Assert.Equal(AuthRefusalCodes.LoginUnavailable, Assert.Single(answer.ErrorCodes).Code));
        Assert.InRange(audit.Events.Count(e => e == "tenant_login_settings_read_failed"), 0, AnonymousAuditBudget.RowsPerWindow);
    }

    /// <summary>FIX3 item 4 — the e-mail-sign-in-switched-off row (no user) goes through the same budget.</summary>
    [Fact]
    public async Task A_tenant_with_email_sign_in_off_is_answered_every_time_but_audited_within_the_budget()
    {
        var audit = new FakeAuditService();
        var handler = CreateHandler(new User("x@acme.test", "hash:x", "X", "Y", TenantA), audit, new EmailOffSettings());

        for (var i = 0; i < 3 * AnonymousAuditBudget.RowsPerWindow; i++)
            Assert.Equal(AuthRefusalCodes.EmailLoginDisabled, Assert.Single((await handler.Handle(Login("x@acme.test"), CancellationToken.None)).ErrorCodes).Code);

        Assert.InRange(audit.Events.Count(e => e == "tenant_login_denied_by_settings"), 0, AnonymousAuditBudget.RowsPerWindow);
    }

    private sealed class EmailOffSettings : ITenantLoginSettingsClient
    {
        public Task<TenantLoginSettingsSnapshot> GetAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult(new TenantLoginSettingsSnapshot(
                tenantId, TwoFactorEnabled: false, MfaRequired: false, EmailLoginEnabled: false, PhoneLoginEnabled: false,
                PasswordMinLength: 8, PasswordRequireUppercase: false, PasswordRequireLowercase: false,
                PasswordRequireDigit: false, PasswordRequireSpecialChar: false, PasswordExpirationDays: null,
                SessionTimeoutMinutes: 60, RefreshTokenLifetimeDays: 7, MaxFailedLoginAttempts: 5, LockoutDurationMinutes: 15));
    }

    [Fact]
    public void The_anonymous_audit_budget_refills_each_window()
    {
        var time = new MovableTime(new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero));
        var budget = new AnonymousAuditBudget(time);

        Assert.Equal(AnonymousAuditBudget.RowsPerWindow, Enumerable.Range(0, 50).Count(_ => budget.TryTake()));
        time.Advance(AnonymousAuditBudget.Window);
        Assert.True(budget.TryTake());
    }

    private sealed class MovableTime(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class ThrowingSettings : ITenantLoginSettingsClient
    {
        public Task<TenantLoginSettingsSnapshot> GetAsync(Guid tenantId, CancellationToken ct) => throw new HttpRequestException("no such tenant");
    }

    private static LoginCommandHandler CreateHandler(User user, FakeAuditService audit, ITenantLoginSettingsClient? settings = null)
    {
        var tenantContext = new TestTenantContext();
        tenantContext.SetTenant(TenantA);
        return new LoginCommandHandler(
            new InMemoryUserRepository([user]),
            new FakeUserRoleRepository(),
            new FakeRoleRepository(),
            new FakeRolePermissionRepository(),
            new FakeTokenService(),
            new FakeRefreshTokenHasher(),
            new FakeRefreshTokenRepository(),
            new FakePasswordHasher(),
            audit,
            settings ?? new FakeTenantLoginSettingsClient(),
            new FakeMfaChallengeService(),
            new PassthroughEffectivePermissionResolver(),
            tenantContext,
            NullLogger<LoginCommandHandler>.Instance);
    }

    // ── Fakes ──
    private sealed class PassthroughEffectivePermissionResolver : ITenantEffectivePermissionResolver
    {
        public Task<IReadOnlyList<string>> ResolveAsync(Guid tenantId, IEnumerable<string>? rolePermissions, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>(rolePermissions?.ToArray() ?? []);
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => "hash:" + password;
        public bool Verify(string password, string hash) => password == CorrectPassword;
    }

    private sealed class FakeAuditService : IAuthAuditService
    {
        public List<string> Events { get; } = [];
        public Task WriteEmptyRoleLoginAsync(Guid userId, Guid tenantId, string email, CancellationToken ct = default) { Events.Add("empty_role"); return Task.CompletedTask; }
        public Task WriteAsync(string eventName, Guid? userId, Guid tenantId, string metadata, CancellationToken ct = default) { Events.Add(eventName); return Task.CompletedTask; }
    }

    private sealed class FakeTenantLoginSettingsClient : ITenantLoginSettingsClient
    {
        public Task<TenantLoginSettingsSnapshot> GetAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult(new TenantLoginSettingsSnapshot(
                tenantId, TwoFactorEnabled: false, MfaRequired: false, EmailLoginEnabled: true, PhoneLoginEnabled: false,
                PasswordMinLength: 8, PasswordRequireUppercase: false, PasswordRequireLowercase: false,
                PasswordRequireDigit: false, PasswordRequireSpecialChar: false, PasswordExpirationDays: null,
                SessionTimeoutMinutes: 60, RefreshTokenLifetimeDays: 7, MaxFailedLoginAttempts: 5, LockoutDurationMinutes: 15));
    }

    private sealed class FakeUserRoleRepository : IUserRoleRepository
    {
        public Task<IEnumerable<string>> GetRolesByUserAsync(Guid userId, Guid tenantId, CancellationToken ct) => Task.FromResult<IEnumerable<string>>(["Member"]);
        public Task AssignAsync(UserRole userRole, CancellationToken ct) => Task.CompletedTask;
        public Task RevokeAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> ExistsAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct) => Task.FromResult(false);
        public Task<IReadOnlyCollection<Guid>> GetUserIdsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<Guid>>([]);
    }

    private sealed class FakeRoleRepository : IRoleRepository
    {
        public Task<Role?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct) => Task.FromResult<Role?>(null);
        public Task<Role?> GetByNameAndTenantAsync(string name, Guid tenantId, CancellationToken ct) => Task.FromResult<Role?>(new Role(name, name, null, tenantId));
        public Task<IEnumerable<Role>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct) => Task.FromResult<IEnumerable<Role>>([]);
        public Task<Role> CreateAsync(Role role, CancellationToken ct) => Task.FromResult(role);
        public Task<Role> UpsertSystemRoleAsync(string name, string displayName, string? description, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> UpdateAsync(Role role, CancellationToken ct) => Task.FromResult(role);
        public Task DeleteAsync(Guid id, Guid tenantId, string deletedBy, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeRolePermissionRepository : IRolePermissionRepository
    {
        public Task<IEnumerable<string>> GetPermissionsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) => Task.FromResult<IEnumerable<string>>([]);
        public Task<IEnumerable<string>> GetPermissionsByRolesAsync(List<Guid> roleIds, Guid tenantId, CancellationToken ct) => Task.FromResult<IEnumerable<string>>([]);
        public Task AssignAsync(RolePermission rolePermission, CancellationToken ct) => Task.CompletedTask;
        public Task RevokeAsync(Guid roleId, Guid permissionId, Guid tenantId, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<RolePermission>> GetByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) => Task.FromResult<IReadOnlyList<RolePermission>>([]);
        public Task RemoveByIdAsync(Guid id, Guid tenantId, CancellationToken ct) => Task.CompletedTask;
        public Task<long> RemoveByPermissionIdAsync(Guid permissionId, CancellationToken ct) => Task.FromResult(0L);
    }

    private sealed class FakeTokenService : ITokenService
    {
        public string GenerateRefreshToken() => "refresh-token";
        public string GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions) => "access-token";
        public string GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes) => "access-token";
        public string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions) => throw new NotSupportedException();
        public string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes) => throw new NotSupportedException();
        public string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes, bool requiresPasswordChange) => throw new NotSupportedException();
        public System.Security.Claims.ClaimsPrincipal GetPrincipalFromExpiredToken(string token) => throw new NotSupportedException();
    }

    private sealed class FakeRefreshTokenHasher : IRefreshTokenHasher
    {
        public string Hash(string refreshToken) => "rh:" + refreshToken;
    }

    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        public Task<long> RevokeLiveSessionsAsync(Guid userId, Guid tenantId, string reason, CancellationToken ct) => RevokeAllByUserAsync(userId, tenantId, ct);
        public Task<bool> TryRotateAsync(Guid tokenId, string replacedByTokenHash, string? revokedByIp, CancellationToken ct) => Task.FromResult(true);
        public Task<bool> RevokeIfLiveAsync(string token, string reason, CancellationToken ct) => Task.FromResult(true);
        public Task<long> RevokeAllByUserAsync(Guid userId, Guid tenantId, CancellationToken ct) => Task.FromResult(0L);
        public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct) => Task.FromResult<RefreshToken?>(null);
        public Task CreateAsync(RefreshToken refreshToken, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(RefreshToken refreshToken, CancellationToken ct) => Task.CompletedTask;
        public Task RevokeAsync(string token, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeMfaChallengeService : IMfaChallengeService
    {
        public bool IsBoundToCurrentPassword(MfaChallenge challenge, User user) => true;
        public Task<MfaChallengeCreated> CreateEmailChallengeAsync(User user, string requestIp, string? userAgent, CancellationToken ct) => throw new NotSupportedException();
        public Task<MfaChallengeCreated> ResendEmailChallengeAsync(string challengeId, string requestIp, string? userAgent, CancellationToken ct) => throw new NotSupportedException();
        public Task<MfaChallenge> VerifyAsync(string challengeId, string code, CancellationToken ct) => throw new NotSupportedException();
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

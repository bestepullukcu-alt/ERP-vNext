using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Services;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// BL-529 (WP-AUTH-ADMIN-RESET-01) — an administrator's password reset ends the old password and every open session at
/// once, on every path that resets: the Users screen (<c>POST api/users/{id}/reset-password</c>), Platform's re-invite
/// of an existing platform administrator (<c>POST api/platform-auth/platform-admins/provision</c>) and Platform's
/// re-invite of an existing tenant administrator (<c>POST internal/events/tenant-admin-invited</c>).
/// <para>Real HTTP on a test-owned mongod, disposable accounts only. The Platform edges these doors ask (login settings,
/// administrator status) are fixed answers, the e-mails are captured instead of sent, and the internal key is a value
/// this host generated — so a test can redeem the link exactly as the user would.</para>
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class AdminResetInvalidatesEndpointTests : IClassFixture<AdminResetInvalidatesEndpointTests.Host>
{
    private static readonly Guid PlatformTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private const string OldPassword = "Old!Passw0rd-529";
    private const string NewPassword = "New!Passw0rd-529";
    private const string WrongPassword = "Wrong!Passw0rd-529";

    private readonly Host _host;

    public AdminResetInvalidatesEndpointTests(Host host) => _host = host;

    public sealed class Host : AccountKindAcceptance.AuthTestHost
    {
        public string InternalKey { get; } = $"bl529-{Guid.NewGuid():N}";
        public CapturingTenantEmails TenantEmails { get; } = new();
        public CapturingPlatformEmails PlatformEmails { get; } = new();

        protected override void ConfigureTestServices(IServiceCollection services)
        {
            base.ConfigureTestServices(services);
            services.AddScoped<ITenantLoginSettingsClient, PlainSettings>();
            services.AddScoped<IPlatformAdministratorStatusClient, AlwaysActive>();
            services.AddScoped<ITenantUserInvitationEmailService>(_ => TenantEmails);
            services.AddScoped<IPlatformAuthEmailService>(_ => PlatformEmails);
            services.AddScoped<IInternalEventAuthService>(_ => new OneKey(InternalKey));
        }
    }

    // ── Users screen: tenant administrator resets a tenant user ──────────────────────────────────────────────────

    [Fact]
    public async Task After_a_users_screen_reset_the_old_password_is_refused_exactly_like_a_wrong_one()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        Assert.Equal(HttpStatusCode.OK, (await TenantLoginAsync(tenantId, user.Email, OldPassword)).StatusCode); // works before

        Assert.Equal(HttpStatusCode.OK, (await ResetOnUsersScreenAsync(tenantId, user.Id)).StatusCode);

        var old = await TenantLoginAsync(tenantId, user.Email, OldPassword);
        var wrong = await TenantLoginAsync(tenantId, user.Email, WrongPassword);
        var unknown = await TenantLoginAsync(tenantId, $"nobody.{Guid.NewGuid():N}@reset.test", WrongPassword);
        var oldBody = await old.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
        Assert.Equal("""{"data":null,"statusCode":401,"isSuccessful":false,"errors":["Invalid email or password."],"errorCodes":[]}""", oldBody);
        Assert.Equal(await wrong.Content.ReadAsStringAsync(), oldBody);
        Assert.Equal(await unknown.Content.ReadAsStringAsync(), oldBody);
        // The same path as a wrong password: the failure counter moved for both refusals.
        Assert.Equal(2, (await ReadUserAsync(user.Id)).FailedLoginAttempts);
    }

    [Fact]
    public async Task A_refresh_token_issued_before_a_users_screen_reset_no_longer_refreshes()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        var session = await TenantSessionAsync(tenantId, user.Email, OldPassword);

        Assert.Equal(HttpStatusCode.OK, (await ResetOnUsersScreenAsync(tenantId, user.Id)).StatusCode);

        var refresh = await RefreshAsync(tenantId, session);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        Assert.Empty(await LiveRefreshTokensAsync(user.Id, tenantId));
    }

    [Fact]
    public async Task The_link_from_a_users_screen_reset_sets_the_new_password_and_lifts_the_forced_change()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);

        Assert.Equal(HttpStatusCode.OK, (await ResetOnUsersScreenAsync(tenantId, user.Id)).StatusCode);
        Assert.True((await ReadUserAsync(user.Id)).MustChangePassword);

        using var anonymous = _host.Client();
        var redeem = await anonymous.PostAsJsonAsync("api/users/set-password",
            new { email = user.Email, token = _host.TenantEmails.LastTokenFor(user.Email), newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.NoContent, redeem.StatusCode);

        var login = await TenantLoginAsync(tenantId, user.Email, NewPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.False((await ReadUserAsync(user.Id)).MustChangePassword);
        Assert.Equal(HttpStatusCode.Unauthorized, (await TenantLoginAsync(tenantId, user.Email, OldPassword)).StatusCode);
    }

    [Fact]
    public async Task The_reset_audit_row_counts_the_ended_sessions_and_carries_no_token_link_or_password()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        await TenantSessionAsync(tenantId, user.Email, OldPassword);
        await TenantSessionAsync(tenantId, user.Email, OldPassword);

        Assert.Equal(HttpStatusCode.OK, (await ResetOnUsersScreenAsync(tenantId, user.Id)).StatusCode);

        var row = Assert.Single(await _host.Database.GetCollection<AuthAuditLog>("authAuditLogs")
            .Find(r => r.EventName == UserAuditEvents.PasswordResetByAdmin && r.TenantId == tenantId).ToListAsync(),
            r => r.Metadata.Contains(user.Id.ToString(), StringComparison.OrdinalIgnoreCase));
        using var metadata = JsonDocument.Parse(row.Metadata);
        Assert.Equal(2, metadata.RootElement.GetProperty("sessionsRevoked").GetInt64());

        var token = _host.TenantEmails.LastTokenFor(user.Email);
        Assert.DoesNotContain(token, row.Metadata, StringComparison.Ordinal);
        Assert.DoesNotContain(Uri.EscapeDataString(token), row.Metadata, StringComparison.Ordinal);
        Assert.DoesNotContain("set-password", row.Metadata, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(OldPassword, row.Metadata, StringComparison.Ordinal);
    }

    // ── Platform re-invites an existing platform administrator ──────────────────────────────────────────────────

    [Fact]
    public async Task A_platform_reinvite_of_an_existing_administrator_ends_the_old_password_and_every_session()
    {
        var email = $"padmin.{Guid.NewGuid():N}@reset.test";
        await AssertOkAsync(await ProvisionPlatformAdminAsync(email)); // first invite: new account
        await RedeemPlatformLinkAsync(email, OldPassword);
        var session = await PlatformSessionAsync(email, OldPassword);

        await AssertOkAsync(await ProvisionPlatformAdminAsync(email)); // "Resend invite"

        var old = await PlatformLoginAsync(email, OldPassword);
        var wrong = await PlatformLoginAsync(email, WrongPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
        Assert.Equal(await wrong.Content.ReadAsStringAsync(), await old.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(PlatformTenantId, session)).StatusCode);

        await RedeemPlatformLinkAsync(email, NewPassword);
        Assert.Equal(HttpStatusCode.OK, (await PlatformLoginAsync(email, NewPassword)).StatusCode);
    }

    // ── Platform re-invites an existing account as tenant administrator ─────────────────────────────────────────

    [Fact]
    public async Task A_tenant_admin_reinvite_of_an_existing_account_ends_the_old_password_and_every_session()
    {
        var tenantId = Guid.NewGuid(); // a disposable tenant of its own: this door provisions the tenant's default roles
        var user = await SeedTenantUserAsync(tenantId);
        var session = await TenantSessionAsync(tenantId, user.Email, OldPassword);

        using var platform = _host.Client();
        platform.DefaultRequestHeaders.Add("X-Internal-Api-Key", _host.InternalKey);
        var invited = await platform.PostAsJsonAsync("internal/events/tenant-admin-invited",
            new { tenantId, adminUserId = Guid.NewGuid(), tenantCode = "BL529", tenantName = "BL-529", email = user.Email, name = "Reset Admin" });
        Assert.Equal(HttpStatusCode.OK, invited.StatusCode);
        using var answer = JsonDocument.Parse(await invited.Content.ReadAsStringAsync());
        Assert.False(answer.RootElement.GetProperty("userProvisioned").GetBoolean());
        var temporaryPassword = answer.RootElement.GetProperty("temporaryPassword").GetString()!;

        var old = await TenantLoginAsync(tenantId, user.Email, OldPassword);
        var wrong = await TenantLoginAsync(tenantId, user.Email, WrongPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
        Assert.Equal(await wrong.Content.ReadAsStringAsync(), await old.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(tenantId, session)).StatusCode);
        Assert.Empty(await LiveRefreshTokensAsync(user.Id, tenantId));

        var temporary = await TenantLoginAsync(tenantId, user.Email, temporaryPassword);
        Assert.Equal(HttpStatusCode.OK, temporary.StatusCode);
        Assert.True((await ReadUserAsync(user.Id)).MustChangePassword);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────────────

    private sealed record Session(string AccessToken, string RefreshToken);

    private IServiceScope Scope(Guid tenantId)
    {
        var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(tenantId);
        return scope;
    }

    private async Task<User> SeedTenantUserAsync(Guid tenantId)
    {
        using var scope = Scope(tenantId);
        var sp = scope.ServiceProvider;
        var user = new User($"reset.{Guid.NewGuid():N}@reset.test", sp.GetRequiredService<IPasswordHasher>().Hash(OldPassword), "Reset", "Subject", tenantId);
        user.ConfirmEmail();
        return await sp.GetRequiredService<IUserRepository>().CreateAsync(user, CancellationToken.None);
    }

    private async Task<User> ReadUserAsync(Guid userId)
        => await _host.Database.GetCollection<User>("users").Find(u => u.Id == userId).SingleAsync();

    private async Task<List<RefreshToken>> LiveRefreshTokensAsync(Guid userId, Guid tenantId)
        => await _host.Database.GetCollection<RefreshToken>("refreshTokens")
            .Find(t => t.UserId == userId && t.TenantId == tenantId && t.RevokedAt == null).ToListAsync();

    private async Task<HttpResponseMessage> ResetOnUsersScreenAsync(Guid tenantId, Guid userId)
    {
        string adminToken;
        using (var scope = Scope(tenantId))
        {
            var sp = scope.ServiceProvider;
            var admin = new User($"admin.{Guid.NewGuid():N}@reset.test", "hash:x", "Tenant", "Admin", tenantId);
            admin.ConfirmEmail();
            admin = await sp.GetRequiredService<IUserRepository>().CreateAsync(admin, CancellationToken.None);
            adminToken = sp.GetRequiredService<ITokenService>().GenerateAccessToken(admin, ["ad-hoc-admin"], ["auth.users.update"], expiresInMinutes: 60);
        }

        using var client = _host.Client(adminToken, tenantId);
        return await client.PostAsync($"api/users/{userId}/reset-password", null);
    }

    private async Task<HttpResponseMessage> TenantLoginAsync(Guid tenantId, string email, string password)
    {
        using var client = _host.Client(null, tenantId);
        return await client.PostAsJsonAsync("api/auth/login", new { email, password, rememberMe = false });
    }

    private async Task<Session> TenantSessionAsync(Guid tenantId, string email, string password)
        => await SessionFromAsync(await TenantLoginAsync(tenantId, email, password));

    private async Task<HttpResponseMessage> PlatformLoginAsync(string email, string password)
    {
        using var client = _host.Client(null, PlatformTenantId);
        return await client.PostAsJsonAsync("api/platform-auth/login", new { email, password, rememberMe = false });
    }

    private async Task<Session> PlatformSessionAsync(string email, string password)
        => await SessionFromAsync(await PlatformLoginAsync(email, password));

    private static async Task<Session> SessionFromAsync(HttpResponseMessage login)
    {
        var body = await login.Content.ReadAsStringAsync();
        Assert.True(login.StatusCode == HttpStatusCode.OK, $"login failed {(int)login.StatusCode}: {body}");
        using var doc = JsonDocument.Parse(body);
        var data = doc.RootElement.GetProperty("data");
        return new Session(data.GetProperty("accessToken").GetString()!, data.GetProperty("refreshToken").GetString()!);
    }

    private async Task<HttpResponseMessage> RefreshAsync(Guid tenantId, Session session)
    {
        using var client = _host.Client(null, tenantId);
        return await client.PostAsJsonAsync("api/auth/refresh-token", new { accessToken = session.AccessToken, refreshToken = session.RefreshToken });
    }

    private async Task<HttpResponseMessage> ProvisionPlatformAdminAsync(string email)
    {
        // The tenant header is the value the Development bypass supplies to Platform's header-less call (this path is not
        // in TenantResolutionMiddleware's public list; the test host runs with the bypass OFF, as production does).
        using var platform = _host.Client(null, PlatformTenantId);
        platform.DefaultRequestHeaders.Add("X-Internal-Api-Key", _host.InternalKey);
        return await platform.PostAsJsonAsync("api/platform-auth/platform-admins/provision", new
        {
            email,
            userName = email.Split('@')[0],
            displayName = "Reset Platform",
            actorType = "platform_admin",
            roles = new[] { "ReadOnly" },
            requirePasswordChange = true
        });
    }

    private static async Task AssertOkAsync(HttpResponseMessage response)
        => Assert.True(response.StatusCode == HttpStatusCode.OK, $"expected 200, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

    private async Task RedeemPlatformLinkAsync(string email, string newPassword)
    {
        using var client = _host.Client(null, PlatformTenantId);
        var response = await client.PostAsJsonAsync("api/platform-auth/reset-password",
            new { email, token = _host.PlatformEmails.LastTokenFor(email), newPassword });
        Assert.True(response.IsSuccessStatusCode, $"platform link redemption failed {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    // ── doubles ─────────────────────────────────────────────────────────────────────────────────────────────────

    public sealed class CapturingTenantEmails : ITenantUserInvitationEmailService
    {
        private readonly ConcurrentDictionary<string, string> _last = new(StringComparer.OrdinalIgnoreCase);

        public string LastTokenFor(string email) => _last[email];

        public string BuildTenantSetPasswordUrl(string email, string setupToken)
            => $"http://localhost/set-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(setupToken)}";

        public Task SendTenantUserInvitationAsync(string email, string setupToken, CancellationToken ct)
        {
            _last[email] = setupToken;
            return Task.CompletedTask;
        }
    }

    public sealed class CapturingPlatformEmails : IPlatformAuthEmailService
    {
        private readonly ConcurrentDictionary<string, string> _last = new(StringComparer.OrdinalIgnoreCase);

        public string LastTokenFor(string email) => _last[email];

        public string BuildPlatformPasswordResetUrl(string email, string resetToken)
            => $"http://localhost/platform/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(resetToken)}";

        public Task SendPlatformPasswordResetAsync(string email, string resetToken, CancellationToken ct)
        {
            _last[email] = resetToken;
            return Task.CompletedTask;
        }
    }

    private sealed class OneKey(string key) : IInternalEventAuthService
    {
        public bool IsAuthorized(string? apiKeyHeaderValue) => string.Equals(apiKeyHeaderValue, key, StringComparison.Ordinal);
    }

    private sealed class AlwaysActive : IPlatformAdministratorStatusClient
    {
        public Task<bool> IsActiveAsync(string email, CancellationToken ct) => Task.FromResult(true);
        public Task MarkLoginAcceptedAsync(string email, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class PlainSettings : ITenantLoginSettingsClient
    {
        public Task<TenantLoginSettingsSnapshot> GetAsync(Guid tenantId, CancellationToken ct) => Task.FromResult(new TenantLoginSettingsSnapshot(
            tenantId, TwoFactorEnabled: false, MfaRequired: false, EmailLoginEnabled: true, PhoneLoginEnabled: false,
            PasswordMinLength: 8, PasswordRequireUppercase: true, PasswordRequireLowercase: true, PasswordRequireDigit: true,
            PasswordRequireSpecialChar: true, PasswordExpirationDays: null, SessionTimeoutMinutes: 60, RefreshTokenLifetimeDays: 7,
            MaxFailedLoginAttempts: 5, LockoutDurationMinutes: 15));
    }
}

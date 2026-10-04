using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Services;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// BL-529 (WP-AUTH-ADMIN-RESET-01) — an administrator's password reset ends the old password and every open session at
/// once, on every path that resets: the Users screen (<c>POST api/users/{id}/reset-password</c>), "Resend invitation" of
/// an account that is no longer a pending invitation, a platform administrator's reset of another
/// (<c>POST api/platform-auth/platform-admins/reset-password</c>), Platform's re-invite of an existing platform
/// administrator (<c>POST api/platform-auth/platform-admins/provision</c>) and of an existing tenant administrator
/// (<c>POST internal/events/tenant-admin-invited</c>).
/// <para>Real HTTP on a test-owned mongod, disposable accounts only, and a NON-Development host ("Staging"): the
/// tenant-resolution bypass is off exactly as in production, and the platform doors are called WITHOUT a tenant header,
/// the way Platform and the Web call them. The Platform edges (login settings, administrator status) are fixed answers,
/// the e-mails are captured instead of sent, and the internal key is the host's own test-owned value.</para>
/// <para>The races (a sign-in or a refresh that read the account before a reset and writes after it) are made
/// deterministic with a barrier: the production hasher's Verify, or the login-settings read of a refresh, holds the
/// request at exactly that point until the test has run the reset.</para>
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class AdminResetInvalidatesEndpointTests : IClassFixture<AdminResetInvalidatesEndpointTests.Host>
{
    private static readonly Guid PlatformTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private const string OldPassword = "Old!Passw0rd-529";
    private const string NewPassword = "New!Passw0rd-529";
    private const string WrongPassword = "Wrong!Passw0rd-529";
    private const string LoginRefused = """{"data":null,"statusCode":401,"isSuccessful":false,"errors":["Invalid email or password."],"errorCodes":[]}""";

    private readonly Host _host;

    public AdminResetInvalidatesEndpointTests(Host host) => _host = host;

    public sealed class Host : AccountKindAcceptance.AuthTestHost
    {
        public CapturingTenantEmails TenantEmails { get; } = new();
        public CapturingPlatformEmails PlatformEmails { get; } = new();
        public Barrier Gate { get; } = new();

        protected override string HostEnvironmentName => "Staging";

        public string InternalKey => InternalApiKey ?? throw new InvalidOperationException("a non-Development host has a test-owned internal key");

        protected override void ConfigureTestServices(IServiceCollection services)
        {
            base.ConfigureTestServices(services);
            services.AddScoped<ITenantLoginSettingsClient>(_ => new GatedSettings(Gate));
            services.AddScoped<IPlatformAdministratorStatusClient>(_ => new GatedStatus(Gate));
            services.AddScoped<ITenantUserInvitationEmailService>(_ => TenantEmails);
            services.AddScoped<IPlatformAuthEmailService>(_ => PlatformEmails);
            services.AddScoped<IPasswordHasher>(_ => new GatedHasher(Gate));
            // BL-529 FIX2 — the production repository, with two points a test can hold a request at.
            services.AddScoped<IUserRepository>(sp => new GatedUserRepository(
                ActivatorUtilities.CreateInstance<Diten.AuthService.Persistence.Repositories.UserRepository>(sp), Gate));
            services.AddSingleton<IOtpDeliveryService>(Otp);
            services.Configure<Diten.AuthService.Infrastructure.Settings.MfaOptions>(o => o.HashSecret = MfaSecret);
            // The per-address limit is production's; the per-client one is raised so this class's many anonymous calls
            // (all from one test client) do not trip it — PasswordDoorRateLimiterTests measures that limit on its own.
            services.AddSingleton(new Diten.AuthService.Infrastructure.Security.PasswordDoorRateLimiter(
                Diten.AuthService.Infrastructure.Security.PasswordDoorRateLimiter.DefaultPerAddressLimit, 10_000,
                Diten.AuthService.Infrastructure.Security.PasswordDoorRateLimiter.DefaultWindow));
        }

        public CapturingOtp Otp { get; } = new();

        private readonly string MfaSecret = $"bl529-mfa-{Guid.NewGuid():N}";
    }

    /// <summary>The tenant whose login settings require the e-mail code (MFA).</summary>
    public static readonly Guid MfaTenantId = Guid.Parse("52952952-9529-5295-2952-952952952952");

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
        Assert.Equal(LoginRefused, oldBody);
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

        Assert.Equal(HttpStatusCode.NoContent, (await RedeemTenantLinkAsync(user.Email, NewPassword)).StatusCode);

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

        var row = await SingleResetRowAsync(tenantId, user.Id);
        using var metadata = JsonDocument.Parse(row.Metadata);
        Assert.Equal(2, metadata.RootElement.GetProperty("sessionsRevoked").GetInt64());
        Assert.Equal("users-screen", metadata.RootElement.GetProperty("via").GetString());
        Assert.Equal("reset", metadata.RootElement.GetProperty("outcome").GetString());

        var token = _host.TenantEmails.LastTokenFor(user.Email);
        Assert.DoesNotContain(token, row.Metadata, StringComparison.Ordinal);
        Assert.DoesNotContain(Uri.EscapeDataString(token), row.Metadata, StringComparison.Ordinal);
        Assert.DoesNotContain("set-password", row.Metadata, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(OldPassword, row.Metadata, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_administrator_cannot_reset_their_own_password_on_the_users_screen()
    {
        var tenantId = _host.Seeded.TenantId;
        var self = await SeedTenantUserAsync(tenantId);
        var session = await TenantSessionAsync(tenantId, self.Email, OldPassword);
        using var client = _host.Client(AdminToken(self, tenantId), tenantId);

        var response = await client.PostAsync($"api/users/{self.Id}/reset-password", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(UserErrorCodes.ResetSelf, await response.Content.ReadAsStringAsync());
        // Nothing was written: the password and the session still work, no audit row.
        Assert.Equal(HttpStatusCode.OK, (await TenantLoginAsync(tenantId, self.Email, OldPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(tenantId, session)).StatusCode);
        Assert.Empty(await ResetRowsAsync(tenantId, self.Id));
    }

    // ── Item 1 + 2: a sign-in or a refresh that read the account BEFORE the reset ─────────────────────────────────

    [Fact]
    public async Task A_sign_in_that_read_the_account_before_a_reset_cannot_undo_it_nor_keep_a_session()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);

        // The old password is verified (it is still the account's), then the reset runs, then the sign-in goes on.
        var login = await RaceAsync("verify:" + OldPassword,
            () => TenantLoginAsync(tenantId, user.Email, OldPassword),
            async () => Assert.Equal(HttpStatusCode.OK, (await ResetOnUsersScreenAsync(tenantId, user.Id)).StatusCode));

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal(LoginRefused, await login.Content.ReadAsStringAsync());
        Assert.Empty(await LiveRefreshTokensAsync(user.Id, tenantId));

        // The reset stands: forced change, the link, and the old password refused.
        var after = await ReadUserAsync(user.Id);
        Assert.True(after.MustChangePassword);
        Assert.NotNull(after.PasswordResetTokenHash);
        Assert.Equal(HttpStatusCode.Unauthorized, (await TenantLoginAsync(tenantId, user.Email, OldPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await RedeemTenantLinkAsync(user.Email, NewPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await TenantLoginAsync(tenantId, user.Email, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task A_failed_sign_in_racing_a_reset_does_not_write_the_old_account_back()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);

        var login = await RaceAsync("verify:" + WrongPassword,
            () => TenantLoginAsync(tenantId, user.Email, WrongPassword),
            async () => Assert.Equal(HttpStatusCode.OK, (await ResetOnUsersScreenAsync(tenantId, user.Id)).StatusCode));

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        var after = await ReadUserAsync(user.Id);
        Assert.True(after.MustChangePassword);
        Assert.NotNull(after.PasswordResetTokenHash);
        Assert.Equal(1, after.FailedLoginAttempts); // the failure itself is still counted
        Assert.Equal(HttpStatusCode.Unauthorized, (await TenantLoginAsync(tenantId, user.Email, OldPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await RedeemTenantLinkAsync(user.Email, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task A_platform_sign_in_racing_a_reset_cannot_undo_it_nor_keep_a_session()
    {
        var email = await ProvisionedPlatformAdminAsync(OldPassword);
        var userId = (await PlatformUserAsync(email)).Id;

        var login = await RaceAsync("verify:" + OldPassword,
            () => PlatformLoginAsync(email, OldPassword),
            async () => await AssertOkAsync(await ProvisionPlatformAdminAsync(email)));

        var wrong = await PlatformLoginAsync(email, WrongPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal(await wrong.Content.ReadAsStringAsync(), await login.Content.ReadAsStringAsync());
        Assert.Empty(await LiveRefreshTokensAsync(userId, PlatformTenantId));
        Assert.Equal(HttpStatusCode.Unauthorized, (await PlatformLoginAsync(email, OldPassword)).StatusCode);
        await RedeemPlatformLinkAsync(email, NewPassword);
        Assert.Equal(HttpStatusCode.OK, (await PlatformLoginAsync(email, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task A_refresh_racing_a_reset_leaves_no_live_session()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        var session = await TenantSessionAsync(tenantId, user.Email, OldPassword);

        // The refresh has read the account and its token; the reset ends every session; then the refresh writes.
        var refresh = await RaceAsync("settings:" + tenantId,
            () => RefreshAsync(tenantId, session),
            async () => Assert.Equal(HttpStatusCode.OK, (await ResetOnUsersScreenAsync(tenantId, user.Id)).StatusCode));

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        Assert.Empty(await LiveRefreshTokensAsync(user.Id, tenantId));
    }

    // ── Item 4: "Resend invitation" of an account that is not a pending invitation IS a reset ───────────────────

    [Fact]
    public async Task Resending_to_an_account_reset_before_BL529_ends_its_old_password()
    {
        var tenantId = _host.Seeded.TenantId;
        // An account reset before this change: confirmed, used, must change its password — and its old hash still valid.
        var user = await SeedTenantUserAsync(tenantId, mustChangePassword: true);
        var session = await TenantSessionAsync(tenantId, user.Email, OldPassword); // the old password still signs in today

        using var client = _host.Client(await TenantAdminTokenAsync(tenantId, "auth.users.create"), tenantId);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"api/users/resend-invite/{user.Id}", null)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await TenantLoginAsync(tenantId, user.Email, OldPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(tenantId, session)).StatusCode);
        using var metadata = JsonDocument.Parse((await SingleResetRowAsync(tenantId, user.Id)).Metadata);
        Assert.Equal("resend-invitation", metadata.RootElement.GetProperty("via").GetString());
        Assert.Equal(1, metadata.RootElement.GetProperty("sessionsRevoked").GetInt64());
        Assert.Equal(HttpStatusCode.NoContent, (await RedeemTenantLinkAsync(user.Email, NewPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await TenantLoginAsync(tenantId, user.Email, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task Resending_a_pending_invitation_still_only_resends_it()
    {
        var tenantId = _host.Seeded.TenantId;
        using var client = _host.Client(await TenantAdminTokenAsync(tenantId, "auth.users.create"), tenantId);
        var email = $"invited.{Guid.NewGuid():N}@reset.test";
        var created = await client.PostAsJsonAsync("api/users", new { email, firstName = "In", lastName = "Vited" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var invited = await TenantUserByEmailAsync(email, tenantId);
        var hashBefore = invited.PasswordHash;

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"api/users/resend-invite/{invited.Id}", null)).StatusCode);

        Assert.Equal(hashBefore, (await ReadUserAsync(invited.Id)).PasswordHash); // the invitation flow is unchanged
        Assert.Empty(await ResetRowsAsync(tenantId, invited.Id));
        Assert.Equal(HttpStatusCode.NoContent, (await RedeemTenantLinkAsync(email, NewPassword)).StatusCode);
    }

    // ── Item 5: a platform administrator resets another platform administrator ──────────────────────────────────

    [Fact]
    public async Task A_platform_administrator_reset_by_another_ends_the_old_password_and_every_session()
    {
        var email = await ProvisionedPlatformAdminAsync(OldPassword);
        var target = await PlatformUserAsync(email);
        var session = await PlatformSessionAsync(email, OldPassword);
        using var caller = _host.Client(await PlatformCallerTokenAsync("platform.administrators.update"));

        var response = await caller.PostAsJsonAsync("api/platform-auth/platform-admins/reset-password", new { email });

        await AssertOkAsync(response);
        var wrong = await PlatformLoginAsync(email, WrongPassword);
        var old = await PlatformLoginAsync(email, OldPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
        Assert.Equal(await wrong.Content.ReadAsStringAsync(), await old.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(null, session)).StatusCode);
        using var metadata = JsonDocument.Parse((await SingleResetRowAsync(PlatformTenantId, target.Id)).Metadata);
        Assert.Equal("platform-administrator-reset", metadata.RootElement.GetProperty("via").GetString());
        Assert.Equal(1, metadata.RootElement.GetProperty("sessionsRevoked").GetInt64());
        await RedeemPlatformLinkAsync(email, NewPassword);
        Assert.Equal(HttpStatusCode.OK, (await PlatformLoginAsync(email, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task A_platform_administrator_cannot_reset_themselves_and_needs_the_update_permission()
    {
        var email = await ProvisionedPlatformAdminAsync(OldPassword);
        var self = await PlatformUserAsync(email);

        using var selfCaller = _host.Client(PlatformToken(self, "platform.administrators.update"));
        var selfReset = await selfCaller.PostAsJsonAsync("api/platform-auth/platform-admins/reset-password", new { email });
        Assert.Equal(HttpStatusCode.Conflict, selfReset.StatusCode);
        Assert.Contains(UserErrorCodes.ResetSelf, await selfReset.Content.ReadAsStringAsync());

        using var readOnly = _host.Client(await PlatformCallerTokenAsync("platform.administrators.read"));
        Assert.Equal(HttpStatusCode.Forbidden, (await readOnly.PostAsJsonAsync("api/platform-auth/platform-admins/reset-password", new { email })).StatusCode);

        using var tenantActor = _host.Client(await TenantAdminTokenAsync(_host.Seeded.TenantId, "platform.administrators.update"), _host.Seeded.TenantId);
        Assert.Equal(HttpStatusCode.Forbidden, (await tenantActor.PostAsJsonAsync("api/platform-auth/platform-admins/reset-password", new { email })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await PlatformLoginAsync(email, OldPassword)).StatusCode); // nothing was reset
        Assert.Empty(await ResetRowsAsync(PlatformTenantId, self.Id));
    }

    // ── Platform re-invites an existing platform administrator ──────────────────────────────────────────────────

    [Fact]
    public async Task A_platform_reinvite_of_an_existing_administrator_ends_the_old_password_and_every_session()
    {
        var email = await ProvisionedPlatformAdminAsync(OldPassword);
        var target = await PlatformUserAsync(email);
        var session = await PlatformSessionAsync(email, OldPassword);

        await AssertOkAsync(await ProvisionPlatformAdminAsync(email)); // "Resend invite"

        var old = await PlatformLoginAsync(email, OldPassword);
        var wrong = await PlatformLoginAsync(email, WrongPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
        Assert.Equal(await wrong.Content.ReadAsStringAsync(), await old.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(null, session)).StatusCode);
        using var metadata = JsonDocument.Parse((await SingleResetRowAsync(PlatformTenantId, target.Id)).Metadata);
        Assert.Equal("platform-administrator-reinvite", metadata.RootElement.GetProperty("via").GetString());
        Assert.Equal(1, metadata.RootElement.GetProperty("sessionsRevoked").GetInt64());

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
        using var metadata = JsonDocument.Parse((await SingleResetRowAsync(tenantId, user.Id)).Metadata);
        Assert.Equal("tenant-administrator-reinvite", metadata.RootElement.GetProperty("via").GetString());
        Assert.Equal(1, metadata.RootElement.GetProperty("sessionsRevoked").GetInt64());
        Assert.DoesNotContain(temporaryPassword, (await SingleResetRowAsync(tenantId, user.Id)).Metadata, StringComparison.Ordinal);

        var temporary = await TenantLoginAsync(tenantId, user.Email, temporaryPassword);
        Assert.Equal(HttpStatusCode.OK, temporary.StatusCode);
        Assert.True((await ReadUserAsync(user.Id)).MustChangePassword);
    }

    // ── Item 6: the platform doors work without a tenant outside Development — and only they ────────────────────

    [Fact]
    public async Task The_platform_doors_resolve_the_platform_tenant_without_a_header_and_nothing_else_does()
    {
        var email = $"padmin.{Guid.NewGuid():N}@reset.test";
        using var anonymous = _host.Client();

        // provision: with the internal key 200, without it 401 (the door's own check still stands).
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("api/platform-auth/platform-admins/provision", ProvisionBody(email))).StatusCode);
        await AssertOkAsync(await ProvisionPlatformAdminAsync(email));
        // sync: same door shape.
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("api/platform-auth/platform-admins/sync",
            new { email, userName = email.Split('@')[0], displayName = "Reset Platform", actorType = "platform_admin", roles = new[] { "ReadOnly" } })).StatusCode);
        // reset-password (the link) and forgot-password, anonymous, no header.
        await RedeemPlatformLinkAsync(email, OldPassword);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.PostAsJsonAsync("api/platform-auth/forgot-password", new { email })).StatusCode);

        // Exact paths, not a prefix: a neighbour of a listed door, and a tenant door, still need their tenant.
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsJsonAsync("api/platform-auth/platform-admins/provision/x", ProvisionBody(email))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsJsonAsync("api/platform-auth/reset-passwordx", new { email })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.GetAsync("api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsJsonAsync("api/auth/login", new { email, password = OldPassword, rememberMe = false })).StatusCode);
    }

    // ── Item 9: what the reset ends, what the link and a deactivation do ────────────────────────────────────────

    [Fact]
    public async Task Only_live_sessions_of_the_tenant_are_ended_counted_and_marked_as_an_admin_reset()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        var live = await SeedRefreshTokenAsync(user.Id, tenantId, DateTime.UtcNow.AddDays(1), revoked: false);
        var expired = await SeedRefreshTokenAsync(user.Id, tenantId, DateTime.UtcNow.AddMinutes(-5), revoked: false);
        var revoked = await SeedRefreshTokenAsync(user.Id, tenantId, DateTime.UtcNow.AddDays(1), revoked: true);
        var foreign = await SeedRefreshTokenAsync(user.Id, Guid.NewGuid(), DateTime.UtcNow.AddDays(1), revoked: false);
        var revokedBefore = await TokenAsync(revoked);

        Assert.Equal(HttpStatusCode.OK, (await ResetOnUsersScreenAsync(tenantId, user.Id)).StatusCode);

        using var metadata = JsonDocument.Parse((await SingleResetRowAsync(tenantId, user.Id)).Metadata);
        Assert.Equal(1, metadata.RootElement.GetProperty("sessionsRevoked").GetInt64());
        var liveAfter = await TokenAsync(live);
        Assert.NotNull(liveAfter.RevokedAt);
        Assert.Equal(AdminPasswordReset.RevokeReason, liveAfter.RevokedReason);
        Assert.Null((await TokenAsync(expired)).RevokedAt);           // nothing to end
        var revokedAfter = await TokenAsync(revoked);
        Assert.Equal(revokedBefore.RevokedAt, revokedAfter.RevokedAt); // its own time and reason kept
        Assert.Equal(revokedBefore.RevokedReason, revokedAfter.RevokedReason);
        Assert.Null((await TokenAsync(foreign)).RevokedAt);           // another tenant's session is not this reset's
    }

    [Fact]
    public async Task A_link_redeemed_after_a_lockout_lets_the_owner_in()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        for (var i = 0; i < 5; i++)
        {
            await TenantLoginAsync(tenantId, user.Email, WrongPassword); // 5 = the tenant's lockout threshold
        }

        Assert.NotNull((await ReadUserAsync(user.Id)).LockoutEnd);

        Assert.Equal(HttpStatusCode.OK, (await ResetOnUsersScreenAsync(tenantId, user.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await RedeemTenantLinkAsync(user.Email, NewPassword)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await TenantLoginAsync(tenantId, user.Email, NewPassword)).StatusCode);
        var after = await ReadUserAsync(user.Id);
        Assert.Null(after.LockoutEnd);
        Assert.Equal(0, after.FailedLoginAttempts);
    }

    [Fact]
    public async Task Deactivating_an_account_ends_its_pending_reset_link()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        Assert.Equal(HttpStatusCode.OK, (await ResetOnUsersScreenAsync(tenantId, user.Id)).StatusCode);

        using var client = _host.Client(await TenantAdminTokenAsync(tenantId, "auth.users.update"), tenantId);
        Assert.True((await client.PostAsync($"api/users/{user.Id}/disable", null)).IsSuccessStatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await RedeemTenantLinkAsync(user.Email, NewPassword)).StatusCode);
        var after = await ReadUserAsync(user.Id);
        Assert.False(after.IsActive); // the link no longer switches the account back on
        Assert.Null(after.PasswordResetTokenHash);
    }

    [Fact]
    public async Task A_deactivated_invitation_keeps_its_link_but_the_link_does_not_switch_it_on()
    {
        var tenantId = _host.Seeded.TenantId;
        using var client = _host.Client(await TenantAdminTokenAsync(tenantId, "auth.users.create", "auth.users.update"), tenantId);
        var email = $"invited.{Guid.NewGuid():N}@reset.test";
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("api/users", new { email, firstName = "In", lastName = "Vited" })).StatusCode);
        var invited = await TenantUserByEmailAsync(email, tenantId);

        var disable = await client.PostAsync($"api/users/{invited.Id}/disable", null);

        Assert.True(disable.IsSuccessStatusCode, $"{(int)disable.StatusCode}: {await disable.Content.ReadAsStringAsync()}");
        Assert.NotNull((await ReadUserAsync(invited.Id)).PasswordResetTokenHash); // the invitation itself is unchanged
        // FIX2 — but redeeming it does not undo the administrator's deactivation.
        Assert.Equal(HttpStatusCode.Conflict, (await RedeemTenantLinkAsync(email, NewPassword)).StatusCode);
        Assert.False((await ReadUserAsync(invited.Id)).IsActive);
    }

    [Fact]
    public async Task A_pending_invitation_that_was_never_deactivated_redeems_as_before()
    {
        var tenantId = _host.Seeded.TenantId;
        using var client = _host.Client(await TenantAdminTokenAsync(tenantId, "auth.users.create"), tenantId);
        var email = $"invited.{Guid.NewGuid():N}@reset.test";
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("api/users", new { email, firstName = "In", lastName = "Vited" })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await RedeemTenantLinkAsync(email, NewPassword)).StatusCode);
        Assert.True((await TenantUserByEmailAsync(email, tenantId)).IsActive);
    }

    // ── Item 8 (H1): the replacement hash is unguessable ────────────────────────────────────────────────────────

    [Fact]
    public void The_unusable_hash_is_random_and_matches_no_known_password()
    {
        using var scope = _host.Factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var hasher = new PasswordHasher(); // the production BCrypt hasher, not a double that accepts anything

        var first = AdminPasswordReset.UnusableHash(hasher, tokens);
        var second = AdminPasswordReset.UnusableHash(hasher, tokens);

        Assert.NotEqual(first, second);
        Assert.StartsWith("$2", first, StringComparison.Ordinal); // a real BCrypt hash
        foreach (var guess in new[] { string.Empty, " ", OldPassword, NewPassword, "password", first, second })
        {
            Assert.False(hasher.Verify(guess, first));
            Assert.False(hasher.Verify(guess, second));
        }
    }

    // ── FIX2 item 1: a session written INSIDE the reset's window (before its write, after its start) is ended ────

    [Fact]
    public async Task A_sign_in_completed_while_a_reset_is_in_flight_does_not_survive_it()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        Session? session = null;

        // The reset has begun (and computed its hash) and is held right before its write; a full sign-in runs now.
        var reset = await RaceAsync("write:" + user.Id,
            () => ResetOnUsersScreenAsync(tenantId, user.Id),
            async () => session = await TenantSessionAsync(tenantId, user.Email, OldPassword));

        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(tenantId, session!)).StatusCode);
        Assert.Empty(await LiveRefreshTokensAsync(user.Id, tenantId));
    }

    [Fact]
    public async Task A_refresh_completed_while_a_reset_is_in_flight_does_not_survive_it()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        var before = await TenantSessionAsync(tenantId, user.Email, OldPassword);
        Session? rotated = null;

        var reset = await RaceAsync("write:" + user.Id,
            () => ResetOnUsersScreenAsync(tenantId, user.Id),
            async () => rotated = await SessionFromAsync(await RefreshAsync(tenantId, before)));

        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(tenantId, rotated!)).StatusCode);
        Assert.Empty(await LiveRefreshTokensAsync(user.Id, tenantId));
    }

    [Fact]
    public async Task An_mfa_sign_in_completed_while_a_reset_is_in_flight_does_not_survive_it()
    {
        var user = await SeedTenantUserAsync(MfaTenantId);
        var challenge = await MfaChallengeAsync(user.Email);
        Session? session = null;

        var reset = await RaceAsync("write:" + user.Id,
            () => ResetOnUsersScreenAsync(MfaTenantId, user.Id),
            async () => session = await SessionFromAsync(await VerifyMfaAsync(challenge, _host.Otp.LastCodeFor(user.Email))));

        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(MfaTenantId, session!)).StatusCode);
        Assert.Empty(await LiveRefreshTokensAsync(user.Id, MfaTenantId));
    }

    [Fact]
    public async Task A_refresh_that_read_its_token_before_the_reset_ended_it_is_not_rotated_back_to_life()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        var session = await TenantSessionAsync(tenantId, user.Email, OldPassword);
        var tokenId = Assert.Single(await LiveRefreshTokensAsync(user.Id, tenantId)).Id;

        // The refresh has read its (still live) token and is held where it reads the account; the reset runs to the end.
        var refresh = await RaceAsync("read:" + user.Id,
            () => RefreshAsync(tenantId, session),
            async () => Assert.Equal(HttpStatusCode.OK, (await ResetOnUsersScreenAsync(tenantId, user.Id)).StatusCode));

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        Assert.Empty(await LiveRefreshTokensAsync(user.Id, tenantId));
        Assert.Equal(AdminPasswordReset.RevokeReason, (await TokenAsync(tokenId)).RevokedReason); // the reset's reason stands
    }

    [Fact]
    public async Task A_sign_in_whose_account_is_deleted_meanwhile_keeps_no_session()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);

        var login = await RaceAsync("verify:" + OldPassword,
            () => TenantLoginAsync(tenantId, user.Email, OldPassword),
            async () =>
            {
                using var scope = Scope(tenantId);
                await scope.ServiceProvider.GetRequiredService<IUserRepository>().SoftDeleteAsync(user.Id, tenantId, CancellationToken.None);
            });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Empty(await LiveRefreshTokensAsync(user.Id, tenantId));
    }

    // ── FIX2 item 2: "forgot password" never writes the password hash; the anonymous doors are rate-limited ──────

    [Fact]
    public async Task A_forgot_password_racing_a_reset_does_not_write_the_old_password_back()
    {
        var email = await ProvisionedPlatformAdminAsync(OldPassword);

        // The forgot-password has read the account and is held at Platform's status check; the re-invitation resets.
        var forgot = await RaceAsync("status:" + email,
            () => AnonymousPostAsync("api/platform-auth/forgot-password", new { email }),
            async () => await AssertOkAsync(await ProvisionPlatformAdminAsync(email)));

        Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PlatformLoginAsync(email, OldPassword)).StatusCode);
        await RedeemPlatformLinkAsync(email, NewPassword); // the forgot-password's link works on the reset account
        Assert.Equal(HttpStatusCode.OK, (await PlatformLoginAsync(email, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task The_anonymous_platform_password_doors_answer_429_with_a_code_past_the_per_address_limit()
    {
        var email = $"flood.{Guid.NewGuid():N}@reset.test";
        for (var i = 0; i < Diten.AuthService.Infrastructure.Security.PasswordDoorRateLimiter.DefaultPerAddressLimit; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await AnonymousPostAsync("api/platform-auth/forgot-password", new { email })).StatusCode);
        }

        var refused = await AnonymousPostAsync("api/platform-auth/forgot-password", new { email });
        Assert.Equal((HttpStatusCode)429, refused.StatusCode);
        Assert.Contains(Diten.AuthService.Infrastructure.Security.PasswordDoorRateLimiter.TooManyRequestsCode, await refused.Content.ReadAsStringAsync());
        Assert.True(refused.Headers.RetryAfter is not null);

        // The link door counts on its own.
        var link = await AnonymousPostAsync("api/platform-auth/reset-password", new { email, token = "x", newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.BadRequest, link.StatusCode);
    }

    // ── FIX2 item 3: the owner's own flows write only over the password they verified; a link only while it holds ──

    [Fact]
    public async Task A_password_change_racing_a_reset_is_refused_and_does_not_overwrite_it()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        var session = await TenantSessionAsync(tenantId, user.Email, OldPassword); // an access token from before the reset
        using var client = _host.Client(session.AccessToken, tenantId);

        var change = await RaceAsync("verify:" + OldPassword,
            () => client.PostAsJsonAsync("api/auth/change-password", new { currentPassword = OldPassword, newPassword = WrongPassword }),
            async () => Assert.Equal(HttpStatusCode.OK, (await ResetOnUsersScreenAsync(tenantId, user.Id)).StatusCode));

        Assert.Equal(HttpStatusCode.Conflict, change.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await TenantLoginAsync(tenantId, user.Email, WrongPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await RedeemTenantLinkAsync(user.Email, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task A_platform_forced_change_racing_a_reset_is_refused_and_does_not_overwrite_it()
    {
        var email = await ProvisionedPlatformAdminAsync(OldPassword);
        var session = await PlatformSessionAsync(email, OldPassword);
        using var client = _host.Client(session.AccessToken);

        var change = await RaceAsync("verify:" + OldPassword,
            () => client.PostAsJsonAsync("api/platform-auth/change-password/forced", new { currentPassword = OldPassword, newPassword = WrongPassword, rememberMe = false }),
            async () => await AssertOkAsync(await ProvisionPlatformAdminAsync(email)));

        Assert.Equal(HttpStatusCode.Conflict, change.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PlatformLoginAsync(email, WrongPassword)).StatusCode);
    }

    [Fact]
    public async Task A_link_redeemed_while_a_newer_reset_replaced_it_writes_nothing()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        Assert.Equal(HttpStatusCode.OK, (await ResetOnUsersScreenAsync(tenantId, user.Id)).StatusCode);
        var firstLink = _host.TenantEmails.LastTokenFor(user.Email);

        // Held at the password-policy read (after the link was checked); a second reset issues a new link meanwhile.
        var redeem = await RaceAsync("settings:" + tenantId,
            () => AnonymousPostAsync("api/users/set-password", new { email = user.Email, token = firstLink, newPassword = WrongPassword }),
            async () => Assert.Equal(HttpStatusCode.OK, (await ResetAgainAsync(tenantId, user.Id)).StatusCode));

        Assert.Equal(HttpStatusCode.BadRequest, redeem.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await TenantLoginAsync(tenantId, user.Email, WrongPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await RedeemTenantLinkAsync(user.Email, NewPassword)).StatusCode); // the newer link
    }

    [Fact]
    public async Task A_platform_link_redeemed_while_a_newer_reset_replaced_it_writes_nothing()
    {
        var email = await ProvisionedPlatformAdminAsync(OldPassword);
        await AssertOkAsync(await ProvisionPlatformAdminAsync(email));
        var firstLink = _host.PlatformEmails.LastTokenFor(email);

        var redeem = await RaceAsync("status:" + email,
            () => AnonymousPostAsync("api/platform-auth/reset-password", new { email, token = firstLink, newPassword = WrongPassword }),
            async () => await AssertOkAsync(await ProvisionPlatformAdminAsync(email)));

        Assert.Equal(HttpStatusCode.BadRequest, redeem.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PlatformLoginAsync(email, WrongPassword)).StatusCode);
    }

    // ── FIX2 items 4 + 7: who may use the platform administrator reset; a self resend on the Users screen ──────

    [Fact]
    public async Task A_partner_administrator_or_a_foreign_tenant_token_cannot_reset_a_platform_administrator()
    {
        var email = await ProvisionedPlatformAdminAsync(OldPassword);

        using var partner = _host.Client(await PlatformCallerTokenAsync("partner_admin", PlatformTenantId, "platform.administrators.update"));
        Assert.Equal(HttpStatusCode.Forbidden, (await partner.PostAsJsonAsync("api/platform-auth/platform-admins/reset-password", new { email })).StatusCode);

        using var foreign = _host.Client(await PlatformCallerTokenAsync("platform_admin", Guid.NewGuid(), "platform.administrators.update"));
        Assert.Equal(HttpStatusCode.Forbidden, (await foreign.PostAsJsonAsync("api/platform-auth/platform-admins/reset-password", new { email })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await PlatformLoginAsync(email, OldPassword)).StatusCode);
    }

    [Fact]
    public async Task Resending_to_ones_own_must_change_account_is_refused()
    {
        var tenantId = _host.Seeded.TenantId;
        var self = await SeedTenantUserAsync(tenantId, mustChangePassword: true);
        using var client = _host.Client(AdminToken(self, tenantId, "auth.users.create"), tenantId);

        var response = await client.PostAsync($"api/users/resend-invite/{self.Id}", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(UserErrorCodes.ResetSelf, await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await TenantLoginAsync(tenantId, self.Email, OldPassword)).StatusCode);
    }

    // ── FIX2 item 7: the store's own filters (real Mongo, not the in-memory double) ─────────────────────────────

    [Fact]
    public async Task The_conditional_writes_hold_on_the_real_store()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        using var scope = Scope(tenantId);
        var repository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var stored = await ReadUserAsync(user.Id);

        var copy = await ReadUserAsync(user.Id);
        copy.UpdateProfile("Stale", "Copy");
        Assert.False(await repository.TryUpdateForTenantIfPasswordHashAsync(copy, tenantId, "not-the-hash", CancellationToken.None));
        Assert.False(await repository.TryUpdateForTenantIfResetTokenAsync(copy, tenantId, "not-the-link", CancellationToken.None));
        Assert.Equal(stored.FirstName, (await ReadUserAsync(user.Id)).FirstName); // nothing written

        await repository.SoftDeleteAsync(user.Id, tenantId, CancellationToken.None);
        Assert.False(await repository.TryUpdateForTenantIfPasswordHashAsync(copy, tenantId, stored.PasswordHash, CancellationToken.None));
        Assert.True((await ReadUserAsync(user.Id)).IsDeleted); // a reset racing a delete does not bring the account back
        Assert.False(await repository.SetPasswordResetTokenAsync(user.Id, tenantId, "h", DateTime.UtcNow.AddHours(1), CancellationToken.None));
    }

    // ── FIX2 item 8: the failure counter, the MFA challenge issued before a reset ──────────────────────────────

    [Fact]
    public async Task Two_wrong_passwords_in_parallel_are_two_failures()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);

        // A holds after its (failed) verify; B fails completely; then A records its failure.
        var first = await RaceAsync("verify:" + WrongPassword,
            () => TenantLoginAsync(tenantId, user.Email, WrongPassword),
            async () => Assert.Equal(HttpStatusCode.Unauthorized, (await TenantLoginAsync(tenantId, user.Email, "Other!Wr0ng-529")).StatusCode));

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(2, (await ReadUserAsync(user.Id)).FailedLoginAttempts);
    }

    [Fact]
    public async Task An_mfa_code_issued_before_a_reset_no_longer_signs_in()
    {
        var user = await SeedTenantUserAsync(MfaTenantId);
        var challenge = await MfaChallengeAsync(user.Email); // the password step passed with the old password

        Assert.Equal(HttpStatusCode.OK, (await ResetOnUsersScreenAsync(MfaTenantId, user.Id)).StatusCode);

        var verify = await VerifyMfaAsync(challenge, _host.Otp.LastCodeFor(user.Email));
        Assert.Equal(HttpStatusCode.Unauthorized, verify.StatusCode);
        Assert.Empty(await LiveRefreshTokensAsync(user.Id, MfaTenantId));
    }

    [Fact]
    public async Task An_mfa_code_with_an_unchanged_password_signs_in_as_before()
    {
        var user = await SeedTenantUserAsync(MfaTenantId);
        var challenge = await MfaChallengeAsync(user.Email);

        var verify = await VerifyMfaAsync(challenge, _host.Otp.LastCodeFor(user.Email));

        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────────────

    private sealed record Session(string AccessToken, string RefreshToken);

    /// <summary>Starts <paramref name="request"/>, waits until it is held at <paramref name="gateKey"/>, runs
    /// <paramref name="between"/>, then lets it go on and returns its answer.</summary>
    private async Task<HttpResponseMessage> RaceAsync(string gateKey, Func<Task<HttpResponseMessage>> request, Func<Task> between)
    {
        _host.Gate.Arm(gateKey);
        var pending = Task.Run(request);
        var reached = await Task.WhenAny(_host.Gate.Reached, Task.Delay(TimeSpan.FromSeconds(30)));
        Assert.True(reached == _host.Gate.Reached, $"the request never reached {gateKey}");
        try
        {
            await between();
        }
        finally
        {
            _host.Gate.Release();
        }

        return await pending;
    }

    private IServiceScope Scope(Guid tenantId)
    {
        var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(tenantId);
        return scope;
    }

    private async Task<User> SeedTenantUserAsync(Guid tenantId, bool mustChangePassword = false)
    {
        using var scope = Scope(tenantId);
        var sp = scope.ServiceProvider;
        var user = new User($"reset.{Guid.NewGuid():N}@reset.test", new PasswordHasher().Hash(OldPassword), "Reset", "Subject", tenantId);
        user.ConfirmEmail();
        if (mustChangePassword)
        {
            user.RequirePasswordChange(null);
        }

        return await sp.GetRequiredService<IUserRepository>().CreateAsync(user, CancellationToken.None);
    }

    private async Task<User> ReadUserAsync(Guid userId)
        => await _host.Database.GetCollection<User>("users").Find(u => u.Id == userId).SingleAsync();

    private async Task<User> TenantUserByEmailAsync(string email, Guid tenantId)
        => await _host.Database.GetCollection<User>("users").Find(u => u.Email == email && u.TenantId == tenantId).SingleAsync();

    private async Task<User> PlatformUserAsync(string email) => await TenantUserByEmailAsync(email, PlatformTenantId);

    private async Task<List<RefreshToken>> LiveRefreshTokensAsync(Guid userId, Guid tenantId)
        => await _host.Database.GetCollection<RefreshToken>("refreshTokens")
            .Find(t => t.UserId == userId && t.TenantId == tenantId && t.RevokedAt == null).ToListAsync();

    private async Task<Guid> SeedRefreshTokenAsync(Guid userId, Guid tenantId, DateTime expiresAt, bool revoked)
    {
        var token = new RefreshToken(userId, $"seed-{Guid.NewGuid():N}", expiresAt, "127.0.0.1", tenantId, "tenant_user", "bl529");
        if (revoked)
        {
            token.Revoke(null, "127.0.0.1", "rotated");
        }

        await _host.Database.GetCollection<RefreshToken>("refreshTokens").InsertOneAsync(token);
        return token.Id;
    }

    private async Task<RefreshToken> TokenAsync(Guid id)
        => await _host.Database.GetCollection<RefreshToken>("refreshTokens").Find(t => t.Id == id).SingleAsync();

    private async Task<List<AuthAuditLog>> ResetRowsAsync(Guid tenantId, Guid userId)
    {
        var rows = await _host.Database.GetCollection<AuthAuditLog>("authAuditLogs")
            .Find(r => r.EventName == UserAuditEvents.PasswordResetByAdmin && r.TenantId == tenantId).ToListAsync();
        return rows.Where(r => r.Metadata.Contains(userId.ToString(), StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private async Task<AuthAuditLog> SingleResetRowAsync(Guid tenantId, Guid userId)
        => Assert.Single(await ResetRowsAsync(tenantId, userId));

    private string AdminToken(User actor, Guid tenantId, params string[] permissions)
    {
        using var scope = Scope(tenantId);
        return scope.ServiceProvider.GetRequiredService<ITokenService>()
            .GenerateAccessToken(actor, ["ad-hoc-admin"], permissions.Length == 0 ? ["auth.users.update"] : permissions, expiresInMinutes: 60);
    }

    private async Task<string> TenantAdminTokenAsync(Guid tenantId, params string[] permissions)
    {
        using var scope = Scope(tenantId);
        var sp = scope.ServiceProvider;
        var admin = new User($"admin.{Guid.NewGuid():N}@reset.test", "hash:x", "Tenant", "Admin", tenantId);
        admin.ConfirmEmail();
        admin = await sp.GetRequiredService<IUserRepository>().CreateAsync(admin, CancellationToken.None);
        return sp.GetRequiredService<ITokenService>().GenerateAccessToken(admin, ["ad-hoc-admin"], permissions, expiresInMinutes: 60);
    }

    private string PlatformToken(User actor, params string[] permissions)
    {
        using var scope = Scope(PlatformTenantId);
        return scope.ServiceProvider.GetRequiredService<ITokenService>().GeneratePlatformAccessToken(
            actor.Id, actor.Email, actor.FirstName, actor.LastName, PlatformTenantId, "platform_admin", ["SuperAdmin"], permissions, 15);
    }

    private async Task<string> PlatformCallerTokenAsync(params string[] permissions)
    {
        using var scope = Scope(PlatformTenantId);
        var caller = new User($"pcaller.{Guid.NewGuid():N}@reset.test", "hash:x", "Platform", "Caller", PlatformTenantId);
        caller.ConfirmEmail();
        caller = await scope.ServiceProvider.GetRequiredService<IUserRepository>().CreateAsync(caller, CancellationToken.None);
        return PlatformToken(caller, permissions);
    }

    // A second Users-screen reset of an account whose first reset is still pending: the Users screen refuses that (the
    // forced change is armed), so the second one is "Resend invitation", which resets a non-pending account the same way.
    private async Task<HttpResponseMessage> ResetAgainAsync(Guid tenantId, Guid userId)
    {
        using var client = _host.Client(await TenantAdminTokenAsync(tenantId, "auth.users.create"), tenantId);
        return await client.PostAsync($"api/users/resend-invite/{userId}", null);
    }

    private async Task<HttpResponseMessage> AnonymousPostAsync(string path, object body)
    {
        using var client = _host.Client();
        return await client.PostAsJsonAsync(path, body);
    }

    private async Task<string> MfaChallengeAsync(string email)
    {
        var login = await TenantLoginAsync(MfaTenantId, email, OldPassword);
        var body = await login.Content.ReadAsStringAsync();
        Assert.True(login.StatusCode == HttpStatusCode.OK, body);
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.GetProperty("data").GetProperty("requiresMfa").GetBoolean(), body);
        return doc.RootElement.GetProperty("data").GetProperty("challengeId").GetString()!;
    }

    private async Task<HttpResponseMessage> VerifyMfaAsync(string challengeId, string code)
    {
        using var client = _host.Client(null, MfaTenantId);
        return await client.PostAsJsonAsync("api/tenant-auth/mfa/verify", new { challengeId, code });
    }

    private async Task<string> PlatformCallerTokenAsync(string actorType, Guid tenantId, params string[] permissions)
    {
        using var scope = Scope(PlatformTenantId);
        var caller = new User($"pcaller.{Guid.NewGuid():N}@reset.test", "hash:x", "Platform", "Caller", PlatformTenantId);
        caller.ConfirmEmail();
        caller = await scope.ServiceProvider.GetRequiredService<IUserRepository>().CreateAsync(caller, CancellationToken.None);
        return scope.ServiceProvider.GetRequiredService<ITokenService>().GeneratePlatformAccessToken(
            caller.Id, caller.Email, caller.FirstName, caller.LastName, tenantId, actorType, ["SuperAdmin"], permissions, 15);
    }

    private async Task<HttpResponseMessage> ResetOnUsersScreenAsync(Guid tenantId, Guid userId)
    {
        using var client = _host.Client(await TenantAdminTokenAsync(tenantId, "auth.users.update"), tenantId);
        return await client.PostAsync($"api/users/{userId}/reset-password", null);
    }

    private async Task<HttpResponseMessage> TenantLoginAsync(Guid tenantId, string email, string password)
    {
        using var client = _host.Client(null, tenantId);
        return await client.PostAsJsonAsync("api/auth/login", new { email, password, rememberMe = false });
    }

    private async Task<Session> TenantSessionAsync(Guid tenantId, string email, string password)
        => await SessionFromAsync(await TenantLoginAsync(tenantId, email, password));

    private async Task<HttpResponseMessage> RedeemTenantLinkAsync(string email, string newPassword)
    {
        using var anonymous = _host.Client();
        return await anonymous.PostAsJsonAsync("api/users/set-password",
            new { email, token = _host.TenantEmails.LastTokenFor(email), newPassword });
    }

    // Platform doors are called the way Platform and the Web call them: no tenant header.
    private async Task<HttpResponseMessage> PlatformLoginAsync(string email, string password)
    {
        using var client = _host.Client();
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

    private async Task<HttpResponseMessage> RefreshAsync(Guid? tenantId, Session session)
    {
        using var client = _host.Client(null, tenantId);
        return await client.PostAsJsonAsync("api/auth/refresh-token", new { accessToken = session.AccessToken, refreshToken = session.RefreshToken });
    }

    private static object ProvisionBody(string email) => new
    {
        email,
        userName = email.Split('@')[0],
        displayName = "Reset Platform",
        actorType = "platform_admin",
        roles = new[] { "ReadOnly" },
        requirePasswordChange = true
    };

    private async Task<HttpResponseMessage> ProvisionPlatformAdminAsync(string email)
    {
        using var platform = _host.Client(); // no tenant header: the way Platform calls it
        platform.DefaultRequestHeaders.Add("X-Internal-Api-Key", _host.InternalKey);
        return await platform.PostAsJsonAsync("api/platform-auth/platform-admins/provision", ProvisionBody(email));
    }

    private async Task<string> ProvisionedPlatformAdminAsync(string password)
    {
        var email = $"padmin.{Guid.NewGuid():N}@reset.test";
        await AssertOkAsync(await ProvisionPlatformAdminAsync(email)); // first invite: new account
        await RedeemPlatformLinkAsync(email, password);
        return email;
    }

    private static async Task AssertOkAsync(HttpResponseMessage response)
        => Assert.True(response.StatusCode == HttpStatusCode.OK, $"expected 200, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

    private async Task RedeemPlatformLinkAsync(string email, string newPassword)
    {
        using var client = _host.Client(); // anonymous, no tenant header
        var response = await client.PostAsJsonAsync("api/platform-auth/reset-password",
            new { email, token = _host.PlatformEmails.LastTokenFor(email), newPassword });
        Assert.True(response.IsSuccessStatusCode, $"platform link redemption failed {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    // ── doubles ─────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Holds ONE request at a named point (synchronously, the way the production code calls it) until released.</summary>
    public sealed class Barrier
    {
        private readonly object _lock = new();
        private string? _armed;
        private TaskCompletionSource _reached = New();
        private TaskCompletionSource _release = New();

        public Task Reached
        {
            get { lock (_lock) return _reached.Task; }
        }

        public void Arm(string key)
        {
            lock (_lock)
            {
                _armed = key;
                _reached = New();
                _release = New();
            }
        }

        public void Release()
        {
            lock (_lock) _release.TrySetResult();
        }

        public void Pass(string key)
        {
            TaskCompletionSource? release = null;
            lock (_lock)
            {
                if (_armed == key)
                {
                    _armed = null;
                    _reached.TrySetResult();
                    release = _release;
                }
            }

            release?.Task.Wait(TimeSpan.FromSeconds(30));
        }

        private static TaskCompletionSource New() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>The production BCrypt hasher; Verify can be held AFTER it has checked the password.</summary>
    private sealed class GatedHasher(Barrier gate) : IPasswordHasher
    {
        private readonly PasswordHasher _inner = new();

        public string Hash(string password) => _inner.Hash(password);

        public bool Verify(string password, string hash)
        {
            var ok = _inner.Verify(password, hash);
            gate.Pass("verify:" + password);
            return ok;
        }
    }

    private sealed class GatedSettings(Barrier gate) : ITenantLoginSettingsClient
    {
        public Task<TenantLoginSettingsSnapshot> GetAsync(Guid tenantId, CancellationToken ct)
        {
            gate.Pass("settings:" + tenantId);
            return Task.FromResult(new TenantLoginSettingsSnapshot(
                tenantId, TwoFactorEnabled: tenantId == MfaTenantId, MfaRequired: tenantId == MfaTenantId, EmailLoginEnabled: true, PhoneLoginEnabled: false,
                PasswordMinLength: 8, PasswordRequireUppercase: true, PasswordRequireLowercase: true, PasswordRequireDigit: true,
                PasswordRequireSpecialChar: true, PasswordExpirationDays: null, SessionTimeoutMinutes: 60, RefreshTokenLifetimeDays: 7,
                MaxFailedLoginAttempts: 5, LockoutDurationMinutes: 15));
        }
    }

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

    private sealed class GatedStatus(Barrier gate) : IPlatformAdministratorStatusClient
    {
        public Task<bool> IsActiveAsync(string email, CancellationToken ct)
        {
            gate.Pass("status:" + email);
            return Task.FromResult(true);
        }

        public Task MarkLoginAcceptedAsync(string email, CancellationToken ct) => Task.CompletedTask;
    }

    public sealed class CapturingOtp : IOtpDeliveryService
    {
        private readonly ConcurrentDictionary<string, string> _last = new(StringComparer.OrdinalIgnoreCase);

        public string LastCodeFor(string email) => _last[email];

        public Task SendEmailOtpAsync(string email, string code, DateTime expiresAtUtc, CancellationToken ct)
        {
            _last[email] = code;
            return Task.CompletedTask;
        }
    }

    /// <summary>The production user repository; a request can be held where it reads an account by id
    /// (<c>read:{id}</c>) or right before a reset's conditional write (<c>write:{id}</c>).</summary>
    private sealed class GatedUserRepository(IUserRepository inner, Barrier gate) : IUserRepository
    {
        public Task<User?> GetByEmailAndTenantAsync(string email, Guid tenantId, CancellationToken ct) => inner.GetByEmailAndTenantAsync(email, tenantId, ct);
        public Task<User?> GetByUserNameAndTenantAsync(string normalizedUserName, Guid tenantId, CancellationToken ct) => inner.GetByUserNameAndTenantAsync(normalizedUserName, tenantId, ct);
        public Task<User?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct)
        {
            gate.Pass("read:" + id);
            return inner.GetByIdAndTenantAsync(id, tenantId, ct);
        }
        public Task<User?> GetByPasswordResetTokenHashAsync(string tokenHash, CancellationToken ct) => inner.GetByPasswordResetTokenHashAsync(tokenHash, ct);
        public Task<IEnumerable<User>> GetAllByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct) => inner.GetAllByTenantAsync(tenantId, page, pageSize, ct);
        public Task<IReadOnlyList<User>> SearchActiveAsync(Guid tenantId, string? term, int limit, CancellationToken ct) => inner.SearchActiveAsync(tenantId, term, limit, ct);
        public Task<long> GetCountByTenantAsync(Guid tenantId, CancellationToken ct) => inner.GetCountByTenantAsync(tenantId, ct);
        public Task<User> CreateAsync(User user, CancellationToken ct) => inner.CreateAsync(user, ct);
        public Task<User> UpdateAsync(User user, CancellationToken ct) => inner.UpdateAsync(user, ct);
        public Task<User> UpdateForTenantAsync(User user, Guid tenantId, CancellationToken ct) => inner.UpdateForTenantAsync(user, tenantId, ct);
        public Task SoftDeleteAsync(Guid id, Guid tenantId, CancellationToken ct) => inner.SoftDeleteAsync(id, tenantId, ct);
        public Task RecordLoginOutcomeAsync(User user, Guid tenantId, CancellationToken ct) => inner.RecordLoginOutcomeAsync(user, tenantId, ct);
        public Task<bool> TryUpdateForTenantIfPasswordHashAsync(User user, Guid tenantId, string expectedPasswordHash, CancellationToken ct)
        {
            gate.Pass("write:" + user.Id);
            return inner.TryUpdateForTenantIfPasswordHashAsync(user, tenantId, expectedPasswordHash, ct);
        }
        public Task<bool> TryUpdateForTenantIfResetTokenAsync(User user, Guid tenantId, string expectedResetTokenHash, CancellationToken ct) => inner.TryUpdateForTenantIfResetTokenAsync(user, tenantId, expectedResetTokenHash, ct);
        public Task<bool> SetPasswordResetTokenAsync(Guid userId, Guid tenantId, string tokenHash, DateTime expiresAtUtc, CancellationToken ct) => inner.SetPasswordResetTokenAsync(userId, tenantId, tokenHash, expiresAtUtc, ct);
        public Task<LoginFailureOutcome> RecordLoginFailureAsync(Guid userId, Guid tenantId, int maxFailedAttempts, int lockoutDurationMinutes, CancellationToken ct) => inner.RecordLoginFailureAsync(userId, tenantId, maxFailedAttempts, lockoutDurationMinutes, ct);
    }
}

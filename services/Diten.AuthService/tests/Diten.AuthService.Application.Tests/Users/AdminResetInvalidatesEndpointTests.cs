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
using MongoDB.Bson;
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
                ActivatorUtilities.CreateInstance<Diten.AuthService.Persistence.Repositories.UserRepository>(sp), Gate, FailOnce));
            // BL-454 stage D FIX3 (3) — the production user-role repository, with a one-shot failure point (FailOnce).
            services.AddScoped<IUserRoleRepository>(sp => new FailingOnceUserRoles(
                ActivatorUtilities.CreateInstance<Diten.AuthService.Persistence.Repositories.UserRoleRepository>(sp), FailOnce));
            services.AddSingleton<IOtpDeliveryService>(Otp);
            services.Configure<Diten.AuthService.Infrastructure.Settings.MfaOptions>(o => o.HashSecret = MfaSecret);
            // FIX3 — the production refresh-token repository with two hold points: before a session is written
            // (create:{user}) and before the reset's sweep (scan:{user}).
            services.AddScoped<IRefreshTokenRepository>(sp => new GatedRefreshTokens(
                ActivatorUtilities.CreateInstance<Diten.AuthService.Persistence.Repositories.RefreshTokenRepository>(sp), Gate));
            // FIX3 — every request arrives from its own client address (X-Test-Peer names one), so the production rate
            // limits apply as they are and one test's requests never spend another's allowance.
            services.AddSingleton<Microsoft.AspNetCore.Hosting.IStartupFilter, TestPeerStartupFilter>();
            // FIX4 — a configured trusted proxy (one no test request comes from): the per-client limit is ON, as in a
            // deployment with ClientAddress:TrustedProxies filled in. ClientAddressProductionDefaultsTests runs the empty list.
            services.AddSingleton(new Diten.AuthService.Infrastructure.Security.ClientAddressResolver(
                [System.Net.IPAddress.Parse("10.255.255.254")]));
        }

        public CapturingOtp Otp { get; } = new();

        /// <summary>BL-454 stage D FIX3 (3) — "create:{email}" / "assign:{tenantId}": the next such write throws, once.</summary>
        public ConcurrentDictionary<string, bool> FailOnce { get; } = new();

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
        // CT (lane, BL-529 × BL-454): a resend that resets a non-pending account sends the RESET mail, not the invitation.
        Assert.Equal(1, _host.TenantEmails.ResetsTo(user.Email));
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
        Assert.Equal(0, _host.TenantEmails.ResetsTo(email)); // a pending invitation gets the invitation mail again
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
            new { tenantId, adminUserId = Guid.NewGuid(), tenantCode = "BL529", tenantName = "BL-529", email = user.Email, name = "Reset Admin", trigger = "operator-invite" });
        Assert.Equal(HttpStatusCode.OK, invited.StatusCode);
        using var answer = JsonDocument.Parse(await invited.Content.ReadAsStringAsync());
        Assert.False(answer.RootElement.GetProperty("userProvisioned").GetBoolean());
        // BL-454 slice 2 stage D — a one-time set-password link, never a password.
        Assert.False(answer.RootElement.TryGetProperty("temporaryPassword", out _));
        var setupToken = answer.RootElement.GetProperty("setupToken").GetString()!;

        var old = await TenantLoginAsync(tenantId, user.Email, OldPassword);
        var wrong = await TenantLoginAsync(tenantId, user.Email, WrongPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
        Assert.Equal(await wrong.Content.ReadAsStringAsync(), await old.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(tenantId, session)).StatusCode);
        Assert.Empty(await LiveRefreshTokensAsync(user.Id, tenantId));
        using var metadata = JsonDocument.Parse((await SingleResetRowAsync(tenantId, user.Id)).Metadata);
        Assert.Equal("tenant-administrator-reinvite", metadata.RootElement.GetProperty("via").GetString());
        Assert.Equal(1, metadata.RootElement.GetProperty("sessionsRevoked").GetInt64());
        Assert.DoesNotContain(setupToken, (await SingleResetRowAsync(tenantId, user.Id)).Metadata, StringComparison.Ordinal);
        Assert.True((await ReadUserAsync(user.Id)).MustChangePassword);

        // BL-529's redemption door, as every invitation link goes through it: once, and never twice.
        using var anonymous = _host.Client();
        var first = await anonymous.PostAsJsonAsync("api/users/set-password", new { email = user.Email, token = setupToken, newPassword = NewPassword });
        var second = await anonymous.PostAsJsonAsync("api/users/set-password", new { email = user.Email, token = setupToken, newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await TenantLoginAsync(tenantId, user.Email, NewPassword)).StatusCode);
    }

    // ── BL-454 stage D FIX1: the tenant-created event only CREATES ──────────────────────────────────────────────

    private async Task<HttpResponseMessage> TenantAdminInvitedByEventAsync(Guid tenantId, string email, string? trigger = "tenant-created-event")
    {
        using var platform = _host.Client();
        platform.DefaultRequestHeaders.Add("X-Internal-Api-Key", _host.InternalKey);
        return await platform.PostAsJsonAsync("internal/events/tenant-admin-invited",
            new { tenantId, adminUserId = Guid.NewGuid(), tenantCode = "S2DF", tenantName = "Stage D fix", email, name = "First Admin", trigger });
    }

    [Theory]
    [InlineData("tenant-created-event")]
    [InlineData(null)] // a caller that does not say who it is gets the create-only path
    public async Task The_tenant_created_event_over_an_existing_account_changes_nothing(string? trigger)
    {
        var tenantId = Guid.NewGuid();
        var user = await SeedTenantUserAsync(tenantId);
        var session = await TenantSessionAsync(tenantId, user.Email, OldPassword);
        var before = await ReadUserAsync(user.Id);

        var invited = await TenantAdminInvitedByEventAsync(tenantId, user.Email, trigger);

        Assert.Equal(HttpStatusCode.OK, invited.StatusCode);
        using var answer = JsonDocument.Parse(await invited.Content.ReadAsStringAsync());
        Assert.False(answer.RootElement.GetProperty("userProvisioned").GetBoolean());
        Assert.Equal(JsonValueKind.Null, answer.RootElement.GetProperty("setupToken").ValueKind); // no link
        Assert.Equal("account_exists", answer.RootElement.GetProperty("message").GetString());
        var after = await ReadUserAsync(user.Id);
        Assert.Equal(before.PasswordHash, after.PasswordHash);                    // no reset
        Assert.Null(after.PasswordResetTokenHash);
        Assert.Empty(await ResetRowsAsync(tenantId, user.Id));
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(tenantId, session)).StatusCode); // the session lives
        Assert.Equal(HttpStatusCode.OK, (await TenantLoginAsync(tenantId, user.Email, OldPassword)).StatusCode);
    }

    [Fact]
    public async Task An_account_its_administrator_deactivated_stays_deactivated_when_the_tenant_created_event_comes_again()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        using (var admin = _host.Client(await TenantAdminTokenAsync(tenantId, "auth.users.update"), tenantId))
        {
            Assert.True((await admin.PostAsync($"api/users/{user.Id}/disable", null)).IsSuccessStatusCode);
        }

        var invited = await TenantAdminInvitedByEventAsync(tenantId, user.Email);

        Assert.Equal(HttpStatusCode.OK, invited.StatusCode);
        var after = await ReadUserAsync(user.Id);
        Assert.False(after.IsActive);
        Assert.True(after.DeactivatedByAdministrator);
        Assert.Null(after.PasswordResetTokenHash);
    }

    [Fact]
    public async Task The_same_tenant_created_event_twice_creates_once_and_the_retry_after_success_resets_nothing()
    {
        var tenantId = Guid.NewGuid();
        var email = $"twice.{Guid.NewGuid():N}@invite.test";

        var first = await TenantAdminInvitedByEventAsync(tenantId, email);
        using var firstAnswer = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var token = firstAnswer.RootElement.GetProperty("setupToken").GetString()!;
        var second = await TenantAdminInvitedByEventAsync(tenantId, email); // a MassTransit retry after Auth succeeded

        using var secondAnswer = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.True(firstAnswer.RootElement.GetProperty("userProvisioned").GetBoolean());
        Assert.False(secondAnswer.RootElement.GetProperty("userProvisioned").GetBoolean());
        Assert.Equal(JsonValueKind.Null, secondAnswer.RootElement.GetProperty("setupToken").ValueKind);
        using var anonymous = _host.Client();
        Assert.Equal(HttpStatusCode.NoContent, (await anonymous.PostAsJsonAsync("api/users/set-password",
            new { email, token, newPassword = NewPassword })).StatusCode); // the first link still works: nothing was reset
    }

    [Fact]
    public async Task A_new_tenant_administrator_account_is_audited_the_moment_it_exists()
    {
        var tenantId = Guid.NewGuid();
        var email = $"audited.{Guid.NewGuid():N}@invite.test";

        Assert.Equal(HttpStatusCode.OK, (await TenantAdminInvitedByEventAsync(tenantId, email)).StatusCode);

        var user = await TenantUserByEmailAsync(email, tenantId);
        var rows = await _host.Database.GetCollection<AuthAuditLog>("authAuditLogs")
            .Find(r => r.EventName == UserAuditEvents.Invited && r.TenantId == tenantId).ToListAsync();
        var row = Assert.Single(rows, r => r.Metadata.Contains(user.Id.ToString(), StringComparison.OrdinalIgnoreCase));
        Assert.Contains("tenant-administrator-invitation", row.Metadata);
        Assert.DoesNotContain(email, row.Metadata, StringComparison.OrdinalIgnoreCase); // no address
    }

    [Fact]
    public async Task The_platform_tenant_never_gets_a_tenant_administrator()
    {
        var refused = await TenantAdminInvitedByEventAsync(PlatformTenantId, $"nobody.{Guid.NewGuid():N}@invite.test");

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("PLATFORM_TENANT_REFUSED", await refused.Content.ReadAsStringAsync());
    }

    // ── BL-454 stage D FIX2: the event's own door, create only by construction ───────────────────────────────────

    private async Task<HttpResponseMessage> TenantAdminCreatedAsync(Guid tenantId, string email)
    {
        using var platform = _host.Client();
        platform.DefaultRequestHeaders.Add("X-Internal-Api-Key", _host.InternalKey);
        return await platform.PostAsJsonAsync("internal/events/tenant-admin-created",
            new { tenantId, adminUserId = Guid.NewGuid(), tenantCode = "S2DF2", tenantName = "Stage D fix 2", email, name = "First Admin" });
    }

    private async Task<HttpResponseMessage> OperatorInviteAsync(Guid tenantId, string email)
    {
        using var platform = _host.Client();
        platform.DefaultRequestHeaders.Add("X-Internal-Api-Key", _host.InternalKey);
        return await platform.PostAsJsonAsync("internal/events/tenant-admin-invited",
            new { tenantId, adminUserId = Guid.NewGuid(), tenantCode = "S2DF2", tenantName = "Stage D fix 2", email, name = "First Admin", trigger = "operator-invite" });
    }

    private async Task<List<UserRole>> RolesOfAsync(Guid userId)
        => await _host.Database.GetCollection<UserRole>("userRoles").Find(r => r.UserId == userId).ToListAsync();

    private async Task<List<TenantUserMembership>> MembershipsOfAsync(Guid userId)
        => await _host.Database.GetCollection<TenantUserMembership>("tenant_user_memberships").Find(m => m.UserId == userId).ToListAsync();

    private async Task<string> InvitedRowMetadataAsync(Guid tenantId, Guid userId)
    {
        var rows = await _host.Database.GetCollection<AuthAuditLog>("authAuditLogs")
            .Find(r => r.EventName == UserAuditEvents.Invited && r.TenantId == tenantId).ToListAsync();
        return Assert.Single(rows, r => r.Metadata.Contains(userId.ToString(), StringComparison.OrdinalIgnoreCase)).Metadata;
    }

    [Fact]
    public async Task The_create_only_door_leaves_an_existing_account_exactly_as_it_is()
    {
        var tenantId = Guid.NewGuid();
        var user = await SeedTenantUserAsync(tenantId);
        var session = await TenantSessionAsync(tenantId, user.Email, OldPassword);
        var before = await ReadUserAsync(user.Id);

        var created = await TenantAdminCreatedAsync(tenantId, user.Email);

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var answer = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal("account_exists", answer.RootElement.GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.Null, answer.RootElement.GetProperty("setupToken").ValueKind);
        Assert.Equal(before.PasswordHash, (await ReadUserAsync(user.Id)).PasswordHash);
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(tenantId, session)).StatusCode);
        // K10 — the existence check comes FIRST: a call that does nothing provisions nothing of the (fresh) tenant either.
        Assert.Equal(0, await TenantRoleCountAsync(tenantId));
    }

    private async Task<long> TenantRoleCountAsync(Guid tenantId)
        => await _host.Database.GetCollection<Role>("roles").CountDocumentsAsync(r => r.TenantId == tenantId);

    [Fact]
    public async Task An_existing_account_that_is_not_an_administrator_is_never_raised_to_Admin_by_the_event()
    {
        // FIX2 K13 — privilege escalation guard: a tenant user (no Admin role, no membership written by this door) whose
        // address the event names gets nothing.
        var tenantId = Guid.NewGuid();
        var user = await SeedTenantUserAsync(tenantId);
        var rolesBefore = (await RolesOfAsync(user.Id)).Count;
        var membershipsBefore = (await MembershipsOfAsync(user.Id)).Count;

        Assert.Equal(HttpStatusCode.OK, (await TenantAdminCreatedAsync(tenantId, user.Email)).StatusCode);

        Assert.Equal(rolesBefore, (await RolesOfAsync(user.Id)).Count);
        Assert.Equal(membershipsBefore, (await MembershipsOfAsync(user.Id)).Count);
    }

    [Fact]
    public async Task A_request_without_any_trigger_property_is_create_only()
    {
        // FIX2 K13 — raw JSON with no "trigger" key at all (an older Platform): an existing account is not reset.
        var tenantId = Guid.NewGuid();
        var user = await SeedTenantUserAsync(tenantId);
        var before = await ReadUserAsync(user.Id);
        using var platform = _host.Client();
        platform.DefaultRequestHeaders.Add("X-Internal-Api-Key", _host.InternalKey);
        var json = $"{{\"tenantId\":\"{tenantId}\",\"adminUserId\":\"{Guid.NewGuid()}\",\"tenantCode\":\"RAW\",\"tenantName\":\"Raw\",\"email\":\"{user.Email}\",\"name\":\"Raw Admin\"}}";

        var invited = await platform.PostAsync("internal/events/tenant-admin-invited", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, invited.StatusCode);
        Assert.Contains("account_exists", await invited.Content.ReadAsStringAsync());
        Assert.Equal(before.PasswordHash, (await ReadUserAsync(user.Id)).PasswordHash);
        Assert.Empty(await ResetRowsAsync(tenantId, user.Id));
    }

    [Theory]
    [InlineData(false, "tenant-created-event")]
    [InlineData(true, "operator-invite")]
    public async Task A_new_account_is_audited_with_the_trigger_that_really_created_it(bool byOperator, string trigger)
    {
        // CT E2 — the row names the real caller.
        var tenantId = Guid.NewGuid();
        var email = $"trigger.{Guid.NewGuid():N}@invite.test";

        var response = byOperator ? await OperatorInviteAsync(tenantId, email) : await TenantAdminCreatedAsync(tenantId, email);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await TenantUserByEmailAsync(email, tenantId);
        using var metadata = JsonDocument.Parse(await InvitedRowMetadataAsync(tenantId, user.Id));
        Assert.Equal(trigger, metadata.RootElement.GetProperty("trigger").GetString());
    }

    [Fact]
    public async Task A_half_made_administrator_is_left_alone_by_the_event_and_repaired_by_the_operators_invite()
    {
        // FIX2 (1) — an account an earlier attempt left without its Admin role (the state an older AuthService could leave
        // behind): the event changes nothing (it cannot tell this from someone else's account); the operator's "Invite"
        // repairs it — the BL-529 reset, a new link, the role.
        var tenantId = Guid.NewGuid();
        var email = $"half.{Guid.NewGuid():N}@invite.test";
        Assert.Equal(HttpStatusCode.OK, (await TenantAdminCreatedAsync(tenantId, email)).StatusCode);
        var user = await TenantUserByEmailAsync(email, tenantId);
        await _host.Database.GetCollection<UserRole>("userRoles").DeleteManyAsync(r => r.UserId == user.Id);

        var again = await TenantAdminCreatedAsync(tenantId, email);
        Assert.Contains("account_exists", await again.Content.ReadAsStringAsync());
        Assert.Empty(await RolesOfAsync(user.Id));

        var repaired = await OperatorInviteAsync(tenantId, email);
        using var answer = JsonDocument.Parse(await repaired.Content.ReadAsStringAsync());
        var token = answer.RootElement.GetProperty("setupToken").GetString()!;
        Assert.Single(await RolesOfAsync(user.Id));
        using var anonymous = _host.Client();
        Assert.Equal(HttpStatusCode.NoContent, (await anonymous.PostAsJsonAsync("api/users/set-password", new { email, token, newPassword = NewPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await TenantLoginAsync(tenantId, email, NewPassword)).StatusCode);
    }

    // ── BL-454 stage D FIX3 ─────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("create")] // the account write itself fails
    [InlineData("assign")] // a grant fails: the account must not exist yet (it is written last)
    public async Task A_new_administrator_whose_first_attempt_fails_midway_is_made_whole_by_the_retry(string failurePoint)
    {
        // FIX3 (3) — "the account is written last", measured: whatever fails part-way, no account exists afterwards; the
        // retry makes it whole; and the grants a failed attempt left name an id no live account has.
        var tenantId = Guid.NewGuid();
        var email = $"midway.{failurePoint}.{Guid.NewGuid():N}@invite.test";
        _host.FailOnce[failurePoint == "create" ? "create:" + email : "assign:" + tenantId] = true;

        var first = await TenantAdminCreatedAsync(tenantId, email);

        Assert.False(first.IsSuccessStatusCode);
        Assert.Null(await _host.Database.GetCollection<User>("users").Find(u => u.Email == email).FirstOrDefaultAsync());

        var retry = await TenantAdminCreatedAsync(tenantId, email);

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        using var answer = JsonDocument.Parse(await retry.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrEmpty(answer.RootElement.GetProperty("setupToken").GetString()));
        var user = await TenantUserByEmailAsync(email, tenantId);
        var adminRole = await _host.Database.GetCollection<Role>("roles").Find(r => r.TenantId == tenantId && r.Name == "Admin").SingleAsync();
        Assert.Contains(await RolesOfAsync(user.Id), r => r.RoleId == adminRole.Id && r.TenantId == tenantId);
        Assert.Single(await MembershipsOfAsync(user.Id), m => m.TenantId == tenantId);

        var grantedIds = (await _host.Database.GetCollection<UserRole>("userRoles").Find(r => r.TenantId == tenantId).ToListAsync())
            .Select(r => r.UserId)
            .Concat((await _host.Database.GetCollection<TenantUserMembership>("tenant_user_memberships").Find(m => m.TenantId == tenantId).ToListAsync()).Select(m => m.UserId))
            .Distinct();
        foreach (var orphan in grantedIds.Where(id => id != user.Id))
        {
            Assert.Null(await _host.Database.GetCollection<User>("users").Find(u => u.Id == orphan).FirstOrDefaultAsync());
        }
    }

    [Theory]
    [InlineData(false, HttpStatusCode.OK)]       // the event's door: exactly as for an account found first
    [InlineData(true, HttpStatusCode.Conflict)]  // the operator's door says so
    public async Task An_account_created_by_another_request_meanwhile_is_answered_not_thrown(bool byOperator, HttpStatusCode expected)
    {
        // FIX3 — the existence check and the write are not one step: the unique index decides, and the loser is answered.
        var tenantId = Guid.NewGuid();
        var email = $"race.{Guid.NewGuid():N}@invite.test";
        _host.FailOnce["race:" + email] = true;

        var response = byOperator ? await OperatorInviteAsync(tenantId, email) : await TenantAdminCreatedAsync(tenantId, email);

        Assert.Equal(expected, response.StatusCode);
        if (!byOperator)
        {
            Assert.Contains("account_exists", await response.Content.ReadAsStringAsync());
        }

        var winner = await TenantUserByEmailAsync(email, tenantId); // exactly one account, the winner's, untouched
        Assert.Equal("Race", winner.FirstName);
        Assert.Empty(await RolesOfAsync(winner.Id)); // the loser's grants name the loser's id, never the winner's
    }

    [Fact]
    public async Task The_create_only_door_ignores_a_trigger_in_its_body()
    {
        // FIX3 — the event's door has no trigger to get wrong: "operator-invite" in its body changes nothing.
        var tenantId = Guid.NewGuid();
        var user = await SeedTenantUserAsync(tenantId);
        var before = await ReadUserAsync(user.Id);
        using var platform = _host.Client();
        platform.DefaultRequestHeaders.Add("X-Internal-Api-Key", _host.InternalKey);

        var created = await platform.PostAsJsonAsync("internal/events/tenant-admin-created",
            new { tenantId, adminUserId = Guid.NewGuid(), tenantCode = "S2DF3", tenantName = "Stage D fix 3", email = user.Email, name = "First Admin", trigger = "operator-invite" });

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Contains("account_exists", await created.Content.ReadAsStringAsync());
        Assert.Equal(before.PasswordHash, (await ReadUserAsync(user.Id)).PasswordHash);
        Assert.Empty(await ResetRowsAsync(tenantId, user.Id));
    }

    [Fact]
    public async Task The_create_only_door_refuses_a_caller_without_the_internal_key()
    {
        var tenantId = Guid.NewGuid();
        var email = $"nokey.{Guid.NewGuid():N}@invite.test";
        using var anonymous = _host.Client();

        var created = await anonymous.PostAsJsonAsync("internal/events/tenant-admin-created",
            new { tenantId, adminUserId = Guid.NewGuid(), tenantCode = "S2DF3", tenantName = "Stage D fix 3", email, name = "First Admin" });

        Assert.Equal(HttpStatusCode.Unauthorized, created.StatusCode);
        Assert.Null(await _host.Database.GetCollection<User>("users").Find(u => u.Email == email).FirstOrDefaultAsync());
        Assert.Equal(0, await TenantRoleCountAsync(tenantId));
    }

    [Fact]
    public async Task The_create_only_door_refuses_the_platform_tenant_by_name()
    {
        var email = $"platform.{Guid.NewGuid():N}@invite.test";

        var created = await TenantAdminCreatedAsync(PlatformTenantId, email);

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Contains("PLATFORM_TENANT_REFUSED", await created.Content.ReadAsStringAsync());
        Assert.Null(await _host.Database.GetCollection<User>("users").Find(u => u.Email == email).FirstOrDefaultAsync());
    }

    [Theory]
    [InlineData("tenant-created-event", "tenant-created-event")]
    [InlineData(null, "unspecified")]
    [InlineData("<script>made-up</script>", "unspecified")] // the raw request string is never written
    public async Task The_operators_door_audits_only_an_allowed_trigger(string? trigger, string audited)
    {
        var tenantId = Guid.NewGuid();
        var email = $"allowed.{Guid.NewGuid():N}@invite.test";

        Assert.Equal(HttpStatusCode.OK, (await TenantAdminInvitedByEventAsync(tenantId, email, trigger)).StatusCode);

        var user = await TenantUserByEmailAsync(email, tenantId);
        var metadata = await InvitedRowMetadataAsync(tenantId, user.Id);
        using var row = JsonDocument.Parse(metadata);
        Assert.Equal(audited, row.RootElement.GetProperty("trigger").GetString());
        Assert.DoesNotContain("made-up", metadata, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_new_tenant_administrator_is_a_pending_invitation_whose_link_works_once()
    {
        // BL-454 slice 2 stage D — the first administrator of a new tenant: no password anywhere, an invitation that the
        // set-password link (and only it) completes.
        var tenantId = Guid.NewGuid();
        var email = $"first.admin.{Guid.NewGuid():N}@invite.test";
        using var platform = _host.Client();
        platform.DefaultRequestHeaders.Add("X-Internal-Api-Key", _host.InternalKey);

        var invited = await platform.PostAsJsonAsync("internal/events/tenant-admin-invited",
            new { tenantId, adminUserId = Guid.NewGuid(), tenantCode = "S2D", tenantName = "Stage D", email, name = "First Admin" });

        Assert.Equal(HttpStatusCode.OK, invited.StatusCode);
        using var answer = JsonDocument.Parse(await invited.Content.ReadAsStringAsync());
        Assert.True(answer.RootElement.GetProperty("userProvisioned").GetBoolean());
        Assert.False(answer.RootElement.TryGetProperty("temporaryPassword", out _));
        var setupToken = answer.RootElement.GetProperty("setupToken").GetString()!;
        var expiresAt = answer.RootElement.GetProperty("setupExpiresAtUtc").GetDateTime();
        Assert.InRange(expiresAt, DateTime.UtcNow.AddDays(6.9), DateTime.UtcNow.AddDays(7.1));

        using var anonymous = _host.Client();
        var first = await anonymous.PostAsJsonAsync("api/users/set-password", new { email, token = setupToken, newPassword = NewPassword });
        var second = await anonymous.PostAsJsonAsync("api/users/set-password", new { email, token = setupToken, newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await TenantLoginAsync(tenantId, email, NewPassword)).StatusCode);
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
        var state = repository.CaptureState(copy);
        copy.UpdateProfile("Stale", "Copy");
        Assert.False(await repository.TryWriteChangesAsync(copy, state, tenantId, new UserWriteCondition(PasswordHash: "not-the-hash"), CancellationToken.None));
        Assert.False(await repository.TryWriteChangesAsync(copy, state, tenantId, new UserWriteCondition(PasswordResetTokenHash: "not-the-link"), CancellationToken.None));
        Assert.False(await repository.TryWriteChangesAsync(copy, state, tenantId, new UserWriteCondition(IsActive: false), CancellationToken.None));
        Assert.Equal(stored.FirstName, (await ReadUserAsync(user.Id)).FirstName); // nothing written

        await repository.SoftDeleteAsync(user.Id, tenantId, CancellationToken.None);
        Assert.False(await repository.TryWriteChangesAsync(copy, state, tenantId, new UserWriteCondition(PasswordHash: stored.PasswordHash), CancellationToken.None));
        Assert.True((await ReadUserAsync(user.Id)).IsDeleted); // a reset racing a delete does not bring the account back
        Assert.False(await repository.SetPasswordResetTokenAsync(user.Id, tenantId, "h", DateTime.UtcNow.AddHours(1), CancellationToken.None));
    }

    [Fact]
    public async Task A_targeted_write_changes_only_the_fields_the_caller_changed()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        using var scope = Scope(tenantId);
        var repository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var stale = await ReadUserAsync(user.Id);            // read before the password changes
        var state = repository.CaptureState(stale);
        var other = await ReadUserAsync(user.Id);
        other.UpdatePassword("changed-elsewhere");
        Assert.True(await repository.TryWriteChangesAsync(other, repository.CaptureState(await ReadUserAsync(user.Id)), tenantId, UserWriteCondition.None, CancellationToken.None));

        stale.UpdateProfile("Renamed", "Only");
        Assert.True(await repository.TryWriteChangesAsync(stale, state, tenantId, UserWriteCondition.None, CancellationToken.None));

        var after = await ReadUserAsync(user.Id);
        Assert.Equal("Renamed", after.FirstName);
        Assert.Equal("changed-elsewhere", after.PasswordHash); // the stale copy's hash was not written back
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

    // ── FIX3 item 1: the mark moves only on a real switch; "Resend invitation" lifts it ───────────────────────────

    [Fact]
    public async Task Editing_a_pending_invitation_does_not_lock_it()
    {
        var tenantId = _host.Seeded.TenantId;
        using var client = _host.Client(await TenantAdminTokenAsync(tenantId, "auth.users.create", "auth.users.update"), tenantId);
        var email = $"invited.{Guid.NewGuid():N}@reset.test";
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("api/users", new { email, firstName = "In", lastName = "Vited" })).StatusCode);
        var invited = await TenantUserByEmailAsync(email, tenantId);

        var edit = await client.PutAsJsonAsync($"api/users/{invited.Id}", new { firstName = "Renamed", lastName = "Invitee", isActive = false });

        Assert.True(edit.IsSuccessStatusCode, $"{(int)edit.StatusCode}: {await edit.Content.ReadAsStringAsync()}");
        Assert.False((await ReadUserAsync(invited.Id)).DeactivatedByAdministrator);
        Assert.Equal(HttpStatusCode.NoContent, (await RedeemTenantLinkAsync(email, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task Resending_a_deactivated_invitation_lifts_the_mark_and_its_new_link_works()
    {
        var tenantId = _host.Seeded.TenantId;
        using var client = _host.Client(await TenantAdminTokenAsync(tenantId, "auth.users.create", "auth.users.update"), tenantId);
        var email = $"invited.{Guid.NewGuid():N}@reset.test";
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("api/users", new { email, firstName = "In", lastName = "Vited" })).StatusCode);
        var invited = await TenantUserByEmailAsync(email, tenantId);
        Assert.True((await client.PostAsync($"api/users/{invited.Id}/disable", null)).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await RedeemTenantLinkAsync(email, NewPassword)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"api/users/resend-invite/{invited.Id}", null)).StatusCode);

        Assert.False((await ReadUserAsync(invited.Id)).DeactivatedByAdministrator);
        var row = Assert.Single((await _host.Database.GetCollection<AuthAuditLog>("authAuditLogs")
            .Find(r => r.EventName == UserAuditEvents.InvitationResent && r.TenantId == tenantId).ToListAsync())
            .Where(r => r.Metadata.Contains(invited.Id.ToString(), StringComparison.OrdinalIgnoreCase)));
        using var metadata = JsonDocument.Parse(row.Metadata);
        Assert.True(metadata.RootElement.GetProperty("markLifted").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, (await RedeemTenantLinkAsync(email, NewPassword)).StatusCode);
    }

    // ── FIX3 item 2: a refresh is bound to the password the session was opened with ──────────────────────────────

    [Fact]
    public async Task A_refresh_between_the_resets_write_and_its_sweep_gets_nothing()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        var session = await TenantSessionAsync(tenantId, user.Email, OldPassword);

        // The reset has written the new hash and is held before its sweep; a refresh that reads the NEW hash runs now,
        // and — if it got that far — would be held right before writing its new token.
        _host.Gate.Arm("scan:" + user.Id);
        var reset = Task.Run(() => ResetOnUsersScreenAsync(tenantId, user.Id));
        await AwaitReachedAsync("scan:" + user.Id);
        _host.Gate.Arm("create:" + user.Id);
        var refresh = Task.Run(() => RefreshAsync(tenantId, session));
        var first = await Task.WhenAny(refresh, _host.Gate.ReachedOf("create:" + user.Id), Task.Delay(TimeSpan.FromSeconds(30)));
        _host.Gate.ReleaseOf("scan:" + user.Id);
        Assert.Equal(HttpStatusCode.OK, (await reset).StatusCode);
        _host.Gate.ReleaseOf("create:" + user.Id);

        Assert.Same(refresh, first); // refused before it could write anything
        Assert.Equal(HttpStatusCode.Unauthorized, (await refresh).StatusCode);
        Assert.Empty(await LiveRefreshTokensAsync(user.Id, tenantId));
    }

    [Fact]
    public async Task A_refresh_chain_keeps_working_while_the_password_is_unchanged()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        var session = await TenantSessionAsync(tenantId, user.Email, OldPassword);

        for (var i = 0; i < 3; i++)
        {
            session = await SessionFromAsync(await RefreshAsync(tenantId, session));
        }

        Assert.Single(await LiveRefreshTokensAsync(user.Id, tenantId));
    }

    [Fact]
    public async Task A_refresh_token_minted_before_the_binding_is_refused_once()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        string access;
        var raw = $"pre-binding-{Guid.NewGuid():N}";
        using (var scope = Scope(tenantId))
        {
            var sp = scope.ServiceProvider;
            access = sp.GetRequiredService<ITokenService>().GenerateAccessToken(await ReadUserAsync(user.Id), [], [], expiresInMinutes: 60);
            var token = new RefreshToken(user.Id, sp.GetRequiredService<IRefreshTokenHasher>().Hash(raw), DateTime.UtcNow.AddDays(1), "127.0.0.1", tenantId, "tenant_user", "bl529");
            await _host.Database.GetCollection<RefreshToken>("refreshTokens").InsertOneAsync(token); // no fingerprint
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(tenantId, new Session(access, raw))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await TenantLoginAsync(tenantId, user.Email, OldPassword)).StatusCode); // sign in once again
    }

    // ── FIX3 item 3: the guard's revocation never overwrites the sweep's ──────────────────────────────────────────

    [Fact]
    public async Task A_session_ended_by_the_sweep_keeps_the_admin_reset_reason()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);

        // The sign-in has written its token and is held where the guard reads the account; the whole reset runs.
        var login = await RaceAsync("read:" + user.Id,
            () => TenantLoginAsync(tenantId, user.Email, OldPassword),
            async () => Assert.Equal(HttpStatusCode.OK, (await ResetOnUsersScreenAsync(tenantId, user.Id)).StatusCode));

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        var token = Assert.Single(await _host.Database.GetCollection<RefreshToken>("refreshTokens").Find(t => t.UserId == user.Id).ToListAsync());
        Assert.Equal(AdminPasswordReset.RevokeReason, token.RevokedReason);
    }

    // ── FIX3 item 4: administrator writes never put back a password a reset replaced (and the reverse) ─────────────

    [Fact]
    public async Task An_edit_racing_a_reset_does_not_write_the_old_password_back()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        using var client = _host.Client(await TenantAdminTokenAsync(tenantId, "auth.users.update"), tenantId);

        var edit = await RaceAsync("write:" + user.Id,
            () => client.PutAsJsonAsync($"api/users/{user.Id}", new { firstName = "Renamed", lastName = "Subject", isActive = true }),
            async () => Assert.Equal(HttpStatusCode.OK, (await ResetOnUsersScreenAsync(tenantId, user.Id)).StatusCode));

        Assert.True(edit.IsSuccessStatusCode);
        Assert.Equal("Renamed", (await ReadUserAsync(user.Id)).FirstName);
        Assert.Equal(HttpStatusCode.Unauthorized, (await TenantLoginAsync(tenantId, user.Email, OldPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await RedeemTenantLinkAsync(user.Email, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task A_deactivation_racing_a_reset_does_not_write_the_old_password_back()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        using var client = _host.Client(await TenantAdminTokenAsync(tenantId, "auth.users.update"), tenantId);

        var disable = await RaceAsync("write:" + user.Id,
            () => client.PostAsync($"api/users/{user.Id}/disable", null),
            async () => Assert.Equal(HttpStatusCode.OK, (await ResetOnUsersScreenAsync(tenantId, user.Id)).StatusCode));

        Assert.True(disable.IsSuccessStatusCode);
        var after = await ReadUserAsync(user.Id);
        Assert.False(after.IsActive);
        Assert.NotEqual((await ReadUserAsync(user.Id)).PasswordHash, user.PasswordHash); // the reset's hash stands
    }

    [Fact]
    public async Task A_platform_sync_racing_a_reset_does_not_write_the_old_password_back()
    {
        var email = await ProvisionedPlatformAdminAsync(OldPassword);
        var admin = await PlatformUserAsync(email);
        using var platform = _host.Client();
        platform.DefaultRequestHeaders.Add("X-Internal-Api-Key", _host.InternalKey);

        var sync = await RaceAsync("write:" + admin.Id,
            () => platform.PostAsJsonAsync("api/platform-auth/platform-admins/sync",
                new { email, userName = email.Split('@')[0], displayName = "Synced Name", actorType = "platform_admin", roles = new[] { "ReadOnly" } }),
            async () => await AssertOkAsync(await ProvisionPlatformAdminAsync(email)));

        Assert.True(sync.IsSuccessStatusCode, $"{(int)sync.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, (await PlatformLoginAsync(email, OldPassword)).StatusCode);
    }

    [Fact]
    public async Task A_reset_racing_a_deactivation_leaves_the_account_off_and_mails_no_link()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        using var client = _host.Client(await TenantAdminTokenAsync(tenantId, "auth.users.update"), tenantId);

        // The reset is held before its write; the deactivation lands first.
        var reset = await RaceAsync("write:" + user.Id,
            () => ResetOnUsersScreenAsync(tenantId, user.Id),
            async () => Assert.True((await client.PostAsync($"api/users/{user.Id}/disable", null)).IsSuccessStatusCode));

        Assert.Equal(HttpStatusCode.Conflict, reset.StatusCode);
        Assert.Contains(UserErrorCodes.ResetConflict, await reset.Content.ReadAsStringAsync());
        var after = await ReadUserAsync(user.Id);
        Assert.False(after.IsActive);
        Assert.True(after.DeactivatedByAdministrator);
        Assert.Equal(0, _host.TenantEmails.SentTo(user.Email));
    }

    // ── FIX3 items 5 + 6: the client is who connects; the link door does not count by address alone ─────────────

    [Fact]
    public async Task One_client_is_refused_past_its_limit_whatever_addresses_and_forwarded_headers_it_sends()
    {
        var peer = RandomPeer();
        HttpResponseMessage? last = null;
        for (var i = 0; i <= Diten.AuthService.Infrastructure.Security.PasswordDoorRateLimiter.DefaultPerClientLimit; i++)
        {
            last = await PeerPostAsync(peer, "api/platform-auth/forgot-password", new { email = $"x{i}.{Guid.NewGuid():N}@reset.test" },
                forwardedFor: $"203.0.113.{i % 250}"); // a fresh made-up forwarded address each time: never believed
        }

        Assert.Equal((HttpStatusCode)429, last!.StatusCode);
        Assert.Contains(AuthRefusalCodes.TooManyRequests, await last.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Junk_from_another_client_does_not_lock_the_owners_link()
    {
        var email = await ProvisionedPlatformAdminAsync(OldPassword);
        await AssertOkAsync(await ProvisionPlatformAdminAsync(email)); // a fresh link for the owner
        var link = _host.PlatformEmails.LastTokenFor(email);
        var attacker = RandomPeer();

        HttpResponseMessage? junk = null;
        for (var i = 0; i <= Diten.AuthService.Infrastructure.Security.PasswordDoorRateLimiter.DefaultPerAddressLimit; i++)
        {
            junk = await PeerPostAsync(attacker, "api/platform-auth/reset-password", new { email, token = "junk", newPassword = NewPassword });
        }

        Assert.Equal((HttpStatusCode)429, junk!.StatusCode); // the link door limits the client that floods it
        var owner = await PeerPostAsync(RandomPeer(), "api/platform-auth/reset-password", new { email, token = link, newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.NoContent, owner.StatusCode);
    }

    // ── FIX3 item 7: the guard runs AFTER the token is written (held right before the write) ────────────────────

    [Fact]
    public async Task A_sign_in_held_before_its_token_write_while_a_reset_runs_keeps_no_session()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);

        var login = await RaceAsync("create:" + user.Id,
            () => TenantLoginAsync(tenantId, user.Email, OldPassword),
            async () => Assert.Equal(HttpStatusCode.OK, (await ResetOnUsersScreenAsync(tenantId, user.Id)).StatusCode));

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Empty(await LiveRefreshTokensAsync(user.Id, tenantId));
    }

    [Fact]
    public async Task A_platform_sign_in_held_before_its_token_write_while_a_reset_runs_keeps_no_session()
    {
        var email = await ProvisionedPlatformAdminAsync(OldPassword);
        var admin = await PlatformUserAsync(email);

        var login = await RaceAsync("create:" + admin.Id,
            () => PlatformLoginAsync(email, OldPassword),
            async () => await AssertOkAsync(await ProvisionPlatformAdminAsync(email)));

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Empty(await LiveRefreshTokensAsync(admin.Id, PlatformTenantId));
    }

    [Fact]
    public async Task A_refresh_held_before_its_token_write_while_a_reset_runs_keeps_no_session()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        var session = await TenantSessionAsync(tenantId, user.Email, OldPassword);

        var refresh = await RaceAsync("create:" + user.Id,
            () => RefreshAsync(tenantId, session),
            async () => Assert.Equal(HttpStatusCode.OK, (await ResetOnUsersScreenAsync(tenantId, user.Id)).StatusCode));

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        Assert.Empty(await LiveRefreshTokensAsync(user.Id, tenantId));
    }

    // ── FIX3 item 8: every new refusal carries its code ───────────────────────────────────────────────────────────

    [Fact]
    public async Task The_new_refusals_carry_their_codes()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        var session = await TenantSessionAsync(tenantId, user.Email, OldPassword);
        using var client = _host.Client(session.AccessToken, tenantId);
        var change = await RaceAsync("verify:" + OldPassword,
            () => client.PostAsJsonAsync("api/auth/change-password", new { currentPassword = OldPassword, newPassword = WrongPassword }),
            async () => Assert.Equal(HttpStatusCode.OK, (await ResetOnUsersScreenAsync(tenantId, user.Id)).StatusCode));
        Assert.Contains(AuthRefusalCodes.PasswordChangedMeanwhile, await change.Content.ReadAsStringAsync());

        using var admin = _host.Client(await TenantAdminTokenAsync(tenantId, "auth.users.create", "auth.users.update"), tenantId);
        var email = $"invited.{Guid.NewGuid():N}@reset.test";
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("api/users", new { email, firstName = "In", lastName = "Vited" })).StatusCode);
        Assert.True((await admin.PostAsync($"api/users/{(await TenantUserByEmailAsync(email, tenantId)).Id}/disable", null)).IsSuccessStatusCode);
        Assert.Contains(AuthRefusalCodes.AccountDeactivated, await (await RedeemTenantLinkAsync(email, NewPassword)).Content.ReadAsStringAsync());
    }

    // ── FIX3 item 9: a tab left open from before the reset does not end the new session ─────────────────────────

    [Fact]
    public async Task A_stale_tab_after_a_reset_is_refused_without_ending_the_new_session()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        var oldTab = await TenantSessionAsync(tenantId, user.Email, OldPassword);
        Assert.Equal(HttpStatusCode.OK, (await ResetOnUsersScreenAsync(tenantId, user.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await RedeemTenantLinkAsync(user.Email, NewPassword)).StatusCode);
        var newSession = await TenantSessionAsync(tenantId, user.Email, NewPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(tenantId, oldTab)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(tenantId, newSession)).StatusCode);
    }

    [Fact]
    public async Task A_reused_rotated_token_still_ends_every_session()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        var first = await TenantSessionAsync(tenantId, user.Email, OldPassword);
        var second = await TenantSessionAsync(tenantId, user.Email, OldPassword);
        await SessionFromAsync(await RefreshAsync(tenantId, first)); // "first" is now rotated

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(tenantId, first)).StatusCode); // reuse → theft signal
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(tenantId, second)).StatusCode);
    }

    // ── FIX4 item 1: a platform re-invitation stores every change it makes ─────────────────────────────────────

    [Fact]
    public async Task A_platform_reinvite_stores_the_new_name_user_name_and_actor_type()
    {
        var email = await ProvisionedPlatformAdminAsync(OldPassword);
        var newUserName = $"renamed.{Guid.NewGuid():N}"[..20];

        using var platform = _host.Client();
        platform.DefaultRequestHeaders.Add("X-Internal-Api-Key", _host.InternalKey);
        await AssertOkAsync(await platform.PostAsJsonAsync("api/platform-auth/platform-admins/provision", new
        {
            email,
            userName = newUserName,
            displayName = "Renamed Administrator",
            actorType = "PartnerAdmin",
            roles = new[] { "ReadOnly" },
            requirePasswordChange = true
        }));

        var stored = await PlatformUserAsync(email);
        Assert.Equal(newUserName, stored.UserName);
        Assert.Equal("Renamed", stored.FirstName);
        Assert.Equal("Administrator", stored.LastName);
        Assert.Equal("partner_admin", stored.PlatformActorType);
    }

    [Fact]
    public async Task A_platform_reinvite_of_a_passive_account_reinvites_it()
    {
        var email = await ProvisionedPlatformAdminAsync(OldPassword);
        var admin = await PlatformUserAsync(email);
        using (var scope = Scope(PlatformTenantId))
        {
            var repository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var state = repository.CaptureState(admin);
            admin.DeactivateByAdministrator();
            Assert.True(await repository.TryWriteChangesAsync(admin, state, PlatformTenantId, UserWriteCondition.None, CancellationToken.None));
        }

        await AssertOkAsync(await ProvisionPlatformAdminAsync(email)); // not 409

        var after = await PlatformUserAsync(email);
        Assert.True(after.IsActive);
        Assert.False(after.DeactivatedByAdministrator);
        await RedeemPlatformLinkAsync(email, NewPassword);
        Assert.Equal(HttpStatusCode.OK, (await PlatformLoginAsync(email, NewPassword)).StatusCode);
    }

    // ── FIX4 item 3: a link redeemed while the invitation is deactivated writes nothing ──────────────────────────

    [Fact]
    public async Task A_link_redeemed_while_the_invitation_is_deactivated_writes_nothing()
    {
        var tenantId = _host.Seeded.TenantId;
        using var admin = _host.Client(await TenantAdminTokenAsync(tenantId, "auth.users.create", "auth.users.update"), tenantId);
        var email = $"invited.{Guid.NewGuid():N}@reset.test";
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("api/users", new { email, firstName = "In", lastName = "Vited" })).StatusCode);
        var invited = await TenantUserByEmailAsync(email, tenantId);

        // The redemption has checked the mark (not set yet) and is held at the password-policy read; the kebab disables.
        var redeem = await RaceAsync("settings:" + tenantId,
            () => AnonymousPostAsync("api/users/set-password", new { email, token = _host.TenantEmails.LastTokenFor(email), newPassword = NewPassword }),
            async () => Assert.True((await admin.PostAsync($"api/users/{invited.Id}/disable", null)).IsSuccessStatusCode));

        Assert.Equal(HttpStatusCode.BadRequest, redeem.StatusCode);
        var after = await ReadUserAsync(invited.Id);
        Assert.False(after.IsActive);
        Assert.True(after.DeactivatedByAdministrator);
    }

    // ── FIX4 (CT D1): a tab left open from before the OWNER'S change is a stale tab too ─────────────────────────

    [Fact]
    public async Task A_stale_tab_after_the_owners_own_change_is_refused_without_ending_the_new_session()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        var oldTab = await TenantSessionAsync(tenantId, user.Email, OldPassword);
        var changing = await TenantSessionAsync(tenantId, user.Email, OldPassword);
        using (var client = _host.Client(changing.AccessToken, tenantId))
        {
            var change = await client.PostAsJsonAsync("api/auth/change-password", new { currentPassword = OldPassword, newPassword = NewPassword });
            Assert.True(change.IsSuccessStatusCode, $"{(int)change.StatusCode}: {await change.Content.ReadAsStringAsync()}");
        }

        var token = await _host.Database.GetCollection<RefreshToken>("refreshTokens")
            .Find(t => t.UserId == user.Id && t.RevokedReason == SessionRevocationReasons.PasswordChanged).FirstOrDefaultAsync();
        Assert.NotNull(token); // the old tab was ended for the password change
        var newSession = await TenantSessionAsync(tenantId, user.Email, NewPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(tenantId, oldTab)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(tenantId, newSession)).StatusCode); // not swept as a theft
    }

    // ── FIX5: invalid links are counted per client when clients are told apart; Platform's sync switches on cleanly ─

    [Fact]
    public async Task One_client_sending_wrong_links_for_many_addresses_is_refused_past_its_limit()
    {
        var peer = RandomPeer();
        HttpResponseMessage? last = null;
        for (var i = 0; i <= Diten.AuthService.Infrastructure.Security.PasswordDoorRateLimiter.DefaultPerClientLimit; i++)
        {
            last = await PeerPostAsync(peer, "api/platform-auth/reset-password", new { email = $"nobody{i}.{Guid.NewGuid():N}@reset.test", token = "junk", newPassword = NewPassword });
        }

        Assert.Equal((HttpStatusCode)429, last!.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PeerPostAsync(RandomPeer(), "api/platform-auth/reset-password",
            new { email = $"nobody.{Guid.NewGuid():N}@reset.test", token = "junk", newPassword = NewPassword })).StatusCode); // another client
    }

    [Fact]
    public async Task Platform_sync_of_a_passive_marked_account_switches_it_on_and_lifts_the_mark()
    {
        var email = await ProvisionedPlatformAdminAsync(OldPassword);
        var admin = await PlatformUserAsync(email);
        using (var scope = Scope(PlatformTenantId))
        {
            var repository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var state = repository.CaptureState(admin);
            admin.DeactivateByAdministrator();
            Assert.True(await repository.TryWriteChangesAsync(admin, state, PlatformTenantId, UserWriteCondition.None, CancellationToken.None));
        }

        using var platform = _host.Client();
        platform.DefaultRequestHeaders.Add("X-Internal-Api-Key", _host.InternalKey);
        var sync = await platform.PostAsJsonAsync("api/platform-auth/platform-admins/sync",
            new { email, userName = email.Split('@')[0], displayName = "Synced Admin", actorType = "platform_admin", roles = new[] { "ReadOnly" } });

        Assert.True(sync.IsSuccessStatusCode, $"{(int)sync.StatusCode}");
        var after = await PlatformUserAsync(email);
        Assert.True(after.IsActive);
        Assert.False(after.DeactivatedByAdministrator); // never "active AND marked"
    }

    // ── FIX6: Platform's sync holds on the mark too; an expired link is an invalid link on both doors ───────────────

    [Fact]
    public async Task A_deactivation_of_a_passive_account_landing_during_a_platform_sync_is_not_switched_back_on()
    {
        var email = await ProvisionedPlatformAdminAsync(OldPassword);
        var admin = await PlatformUserAsync(email);
        await WriteAccountAsync(admin, a => a.Deactivate()); // passive, not marked: the sync reads IsActive = false

        using var platform = _host.Client();
        platform.DefaultRequestHeaders.Add("X-Internal-Api-Key", _host.InternalKey);
        // The sync is held before its write; an administrator deactivates meanwhile — IsActive stays false, the mark is set.
        var sync = await RaceAsync("write:" + admin.Id,
            () => platform.PostAsJsonAsync("api/platform-auth/platform-admins/sync",
                new { email, userName = email.Split('@')[0], displayName = "Synced Admin", actorType = "platform_admin", roles = new[] { "ReadOnly" } }),
            async () => await WriteAccountAsync(await PlatformUserAsync(email), a => a.DeactivateByAdministrator()));

        Assert.Equal(HttpStatusCode.Conflict, sync.StatusCode);
        var after = await PlatformUserAsync(email);
        Assert.False(after.IsActive);
        Assert.True(after.DeactivatedByAdministrator);
    }

    [Fact]
    public async Task An_expired_platform_link_is_refused_and_counted_as_an_invalid_one()
    {
        var email = await ProvisionedPlatformAdminAsync(OldPassword);
        await AssertOkAsync(await ProvisionPlatformAdminAsync(email)); // a fresh link …
        var link = _host.PlatformEmails.LastTokenFor(email);
        await ExpireLinkAsync(await PlatformUserAsync(email)); // … that has expired

        var peer = RandomPeer();
        for (var i = 0; i < Diten.AuthService.Infrastructure.Security.PasswordDoorRateLimiter.DefaultPerAddressLimit; i++)
        {
            Assert.Equal(HttpStatusCode.BadRequest,
                (await PeerPostAsync(peer, "api/platform-auth/reset-password", new { email, token = link, newPassword = NewPassword })).StatusCode);
        }

        // Counted like any wrong link: past the (client, address) limit the same client is refused.
        Assert.Equal((HttpStatusCode)429,
            (await PeerPostAsync(peer, "api/platform-auth/reset-password", new { email, token = link, newPassword = NewPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PlatformLoginAsync(email, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task An_expired_tenant_link_is_refused()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        Assert.Equal(HttpStatusCode.OK, (await ResetOnUsersScreenAsync(tenantId, user.Id)).StatusCode);
        await ExpireLinkAsync(await ReadUserAsync(user.Id));

        Assert.Equal(HttpStatusCode.BadRequest, (await RedeemTenantLinkAsync(user.Email, NewPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await TenantLoginAsync(tenantId, user.Email, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task The_owner_gets_through_from_the_same_client_that_sent_the_junk()
    {
        var email = await ProvisionedPlatformAdminAsync(OldPassword);
        await AssertOkAsync(await ProvisionPlatformAdminAsync(email)); // a fresh link for the owner
        var link = _host.PlatformEmails.LastTokenFor(email);
        var shared = RandomPeer(); // one client — e.g. an office behind one address

        HttpResponseMessage? junk = null;
        for (var i = 0; i <= Diten.AuthService.Infrastructure.Security.PasswordDoorRateLimiter.DefaultPerAddressLimit; i++)
        {
            junk = await PeerPostAsync(shared, "api/platform-auth/reset-password", new { email, token = "junk", newPassword = NewPassword });
        }

        Assert.Equal((HttpStatusCode)429, junk!.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await PeerPostAsync(shared, "api/platform-auth/reset-password", new { email, token = link, newPassword = NewPassword })).StatusCode);
    }

    private async Task WriteAccountAsync(User account, Action<User> change)
    {
        using var scope = Scope(account.TenantId);
        var repository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var state = repository.CaptureState(account);
        change(account);
        Assert.True(await repository.TryWriteChangesAsync(account, state, account.TenantId, UserWriteCondition.None, CancellationToken.None));
    }

    private async Task ExpireLinkAsync(User account)
        => await _host.Database.GetCollection<User>("users").UpdateOneAsync(u => u.Id == account.Id,
            Builders<User>.Update.Set(u => u.PasswordResetTokenExpiresAt, (DateTime?)DateTime.UtcNow.AddMinutes(-1)));

    // ── FIX7 item 1: an account written before BL-529 has no DeactivatedByAdministrator field at all ───────────────

    [Fact]
    public async Task Platform_sync_of_an_account_written_before_BL529_succeeds()
    {
        var email = await ProvisionedPlatformAdminAsync(OldPassword);
        var admin = await PlatformUserAsync(email);
        await UnsetAsync("users", admin.Id, nameof(User.DeactivatedByAdministrator));

        using var platform = _host.Client();
        platform.DefaultRequestHeaders.Add("X-Internal-Api-Key", _host.InternalKey);
        var sync = await platform.PostAsJsonAsync("api/platform-auth/platform-admins/sync",
            new { email, userName = email.Split('@')[0], displayName = "Legacy Admin", actorType = "platform_admin", roles = new[] { "ReadOnly" } });

        Assert.Equal(HttpStatusCode.NoContent, sync.StatusCode);
        Assert.True((await PlatformUserAsync(email)).IsActive);
    }

    [Fact]
    public async Task Users_screen_reset_of_an_account_written_before_BL529_succeeds()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        await UnsetAsync("users", user.Id, nameof(User.DeactivatedByAdministrator));

        Assert.Equal(HttpStatusCode.OK, (await ResetOnUsersScreenAsync(tenantId, user.Id)).StatusCode);
        Assert.NotEqual(user.PasswordHash, (await ReadUserAsync(user.Id)).PasswordHash);
    }

    [Fact]
    public async Task The_tenant_link_of_an_account_written_before_BL529_sets_the_password()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        Assert.Equal(HttpStatusCode.OK, (await ResetOnUsersScreenAsync(tenantId, user.Id)).StatusCode);
        await UnsetAsync("users", user.Id, nameof(User.DeactivatedByAdministrator));

        Assert.Equal(HttpStatusCode.NoContent, (await RedeemTenantLinkAsync(user.Email, NewPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await TenantLoginAsync(tenantId, user.Email, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task A_session_stored_without_its_binding_field_is_refused_once()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        var session = await TenantSessionAsync(tenantId, user.Email, OldPassword);
        var stored = await _host.Database.GetCollection<RefreshToken>("refreshTokens")
            .Find(t => t.UserId == user.Id && t.RevokedAt == null).SingleAsync();
        await UnsetAsync("refreshTokens", stored.Id, nameof(RefreshToken.PasswordFingerprint));

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(tenantId, session)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await TenantLoginAsync(tenantId, user.Email, OldPassword)).StatusCode);
    }

    [Fact]
    public async Task An_mfa_code_whose_challenge_has_no_binding_field_is_refused()
    {
        var user = await SeedTenantUserAsync(MfaTenantId);
        var challenge = await MfaChallengeAsync(user.Email);
        var code = _host.Otp.LastCodeFor(user.Email);
        await _host.Database.GetCollection<BsonDocument>("mfaChallenges").UpdateManyAsync(
            Builders<BsonDocument>.Filter.Eq("UserId", new BsonBinaryData(user.Id, GuidRepresentation.Standard)),
            Builders<BsonDocument>.Update.Unset(nameof(MfaChallenge.PasswordFingerprint)));

        Assert.Equal(HttpStatusCode.Unauthorized, (await VerifyMfaAsync(challenge, code)).StatusCode);
    }

    // ── FIX7 item 4: a marked platform account is not switched on by its link ─────────────────────────────────────

    [Fact]
    public async Task A_marked_platform_account_is_not_switched_on_by_its_link_and_the_attempts_are_not_counted()
    {
        // A pending platform invitation (never redeemed) holding its link, with the administrator's mark on it — the state
        // the item names, written directly (the paths that lead to it are not this test's subject). Platform still reports
        // the administrator active.
        var email = $"pending.{Guid.NewGuid():N}@reset.test";
        await AssertOkAsync(await ProvisionPlatformAdminAsync(email));
        var link = _host.PlatformEmails.LastTokenFor(email);
        var pending = await PlatformUserAsync(email);
        await _host.Database.GetCollection<BsonDocument>("users").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(pending.Id, GuidRepresentation.Standard)),
            Builders<BsonDocument>.Update.Set(nameof(User.IsActive), false).Set(nameof(User.DeactivatedByAdministrator), true));
        Assert.NotNull((await PlatformUserAsync(email)).PasswordResetTokenHash); // non-vacuity: the link is the account's
        var peer = RandomPeer();

        HttpResponseMessage? last = null;
        for (var i = 0; i <= Diten.AuthService.Infrastructure.Security.PasswordDoorRateLimiter.DefaultPerAddressLimit; i++)
        {
            last = await PeerPostAsync(peer, "api/platform-auth/reset-password", new { email, token = link, newPassword = NewPassword });
        }

        Assert.Equal(HttpStatusCode.Conflict, last!.StatusCode); // never 429: a valid link is not counted
        Assert.Contains(AuthRefusalCodes.AccountDeactivated, await last.Content.ReadAsStringAsync());
        var after = await PlatformUserAsync(email);
        Assert.False(after.IsActive);
        Assert.True(after.DeactivatedByAdministrator);
    }

    // ── FIX7 item 6: the sync's IsActive condition (CT G3) ─────────────────────────────────────────────────────────

    [Fact]
    public async Task A_plain_deactivation_landing_during_a_platform_sync_is_not_overwritten()
    {
        var email = await ProvisionedPlatformAdminAsync(OldPassword);
        var admin = await PlatformUserAsync(email);
        Assert.True(admin.IsActive);
        using var platform = _host.Client();
        platform.DefaultRequestHeaders.Add("X-Internal-Api-Key", _host.InternalKey);

        // The sync is held before its write; a write that switches the account off WITHOUT the mark lands meanwhile.
        var sync = await RaceAsync("write:" + admin.Id,
            () => platform.PostAsJsonAsync("api/platform-auth/platform-admins/sync",
                new { email, userName = email.Split('@')[0], displayName = "Synced Admin", actorType = "platform_admin", roles = new[] { "ReadOnly" } }),
            async () => await WriteAccountAsync(await PlatformUserAsync(email), a => a.Deactivate()));

        Assert.Equal(HttpStatusCode.Conflict, sync.StatusCode);
        var after = await PlatformUserAsync(email);
        Assert.False(after.IsActive); // the racer's value stands
        Assert.False(after.DeactivatedByAdministrator);
    }

    // FIX8 item 4 — the unset is measured: it touched exactly the one document, and the field is gone from it.
    private async Task UnsetAsync(string collection, Guid id, string field)
    {
        var documents = _host.Database.GetCollection<BsonDocument>(collection);
        var byId = Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(id, GuidRepresentation.Standard));
        var result = await documents.UpdateOneAsync(byId, Builders<BsonDocument>.Update.Unset(field));
        Assert.Equal(1, result.MatchedCount);
        Assert.False((await documents.Find(byId).SingleAsync()).Contains(field), $"{field} is still on the document");
    }

    // ── FIX8 item 2: the seeder does not switch a marked admin back on ──────────────────────────────────────────────

    [Fact]
    public async Task A_restart_does_not_switch_on_the_seeded_admin_an_administrator_deactivated()
    {
        // A fixed-name database of its own (the seeder writes the DefaultTenant admin: never the host's), emptied first.
        const string seedDatabase = "bl529_fix8_seed";
        await _host.Database.Client.DropDatabaseAsync(seedDatabase);
        var database = _host.Database.Client.GetDatabase(seedDatabase);
        await Diten.AuthService.Persistence.Seed.DataSeeder.SeedAsync(database, seedMockUsers: false, logger: null, beforeSeedSteps: null);
        var users = database.GetCollection<User>("users");
        var admin = await users.Find(u => u.Email == "admin@diten.com").SingleAsync();
        Assert.True(admin.IsActive); // non-vacuity: seeded on
        await users.UpdateOneAsync(u => u.Id == admin.Id, Builders<User>.Update
            .Set(u => u.IsActive, false).Set(u => u.DeactivatedByAdministrator, true));

        await Diten.AuthService.Persistence.Seed.DataSeeder.SeedAsync(database, seedMockUsers: false, logger: null, beforeSeedSteps: null);

        var after = await users.Find(u => u.Id == admin.Id).SingleAsync();
        Assert.False(after.IsActive);
        Assert.True(after.DeactivatedByAdministrator);
    }

    // ── FIX8 item 3: the platform link's write holds on the mark too ───────────────────────────────────────────────

    [Fact]
    public async Task A_platform_link_redeemed_while_the_account_is_marked_writes_nothing()
    {
        var email = $"pending.{Guid.NewGuid():N}@reset.test";
        await AssertOkAsync(await ProvisionPlatformAdminAsync(email));
        var before = await PlatformUserAsync(email);

        // The redemption has checked the mark (not set yet) and is held at Platform's status check; the mark lands.
        var redeem = await RaceAsync("status:" + email,
            () => AnonymousPostAsync("api/platform-auth/reset-password", new { email, token = _host.PlatformEmails.LastTokenFor(email), newPassword = NewPassword }),
            async () => await _host.Database.GetCollection<BsonDocument>("users").UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(before.Id, GuidRepresentation.Standard)),
                Builders<BsonDocument>.Update.Set(nameof(User.DeactivatedByAdministrator), true)));

        Assert.Equal(HttpStatusCode.BadRequest, redeem.StatusCode);
        var after = await PlatformUserAsync(email);
        Assert.Equal(before.IsActive, after.IsActive);
        Assert.Equal(before.PasswordHash, after.PasswordHash);
        Assert.True(after.DeactivatedByAdministrator);
    }

    // ── FIX8 item 5: a reset of a passive account racing a deactivation that only marks it ─────────────────────────

    [Fact]
    public async Task A_reset_of_a_passive_account_racing_a_mark_only_deactivation_is_a_conflict()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        var byId = Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(user.Id, GuidRepresentation.Standard));
        await _host.Database.GetCollection<BsonDocument>("users").UpdateOneAsync(byId, Builders<BsonDocument>.Update.Set(nameof(User.IsActive), false));

        // The reset is held before its write; an administrator's deactivation lands — on a passive account it changes only the mark.
        var reset = await RaceAsync("write:" + user.Id,
            () => ResetOnUsersScreenAsync(tenantId, user.Id),
            async () => await _host.Database.GetCollection<BsonDocument>("users").UpdateOneAsync(byId,
                Builders<BsonDocument>.Update.Set(nameof(User.DeactivatedByAdministrator), true)));

        Assert.Equal(HttpStatusCode.Conflict, reset.StatusCode);
        Assert.Contains(UserErrorCodes.ResetConflict, await reset.Content.ReadAsStringAsync());
        var after = await ReadUserAsync(user.Id);
        Assert.Equal(user.PasswordHash, after.PasswordHash);
        Assert.True(after.DeactivatedByAdministrator);
    }

    // ── FIX8 item 6: a blank token is a wrong link, never a 500 ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_token_at_the_platform_door_is_a_wrong_link(string token)
    {
        var email = $"blank.{Guid.NewGuid():N}@reset.test";
        await AssertOkAsync(await ProvisionPlatformAdminAsync(email)); // a live link exists

        var response = await AnonymousPostAsync("api/platform-auth/reset-password", new { email, token, newPassword = NewPassword });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── FIX8 item 1 (b): a link never switches on an account an administrator switched off before BL-529 ────────────

    [Fact]
    public async Task A_link_does_not_switch_on_an_account_deactivated_before_BL529_until_an_administrator_activates_it()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        Assert.Equal(HttpStatusCode.OK, (await ResetOnUsersScreenAsync(tenantId, user.Id)).StatusCode); // a live link
        var link = _host.TenantEmails.LastTokenFor(user.Email);
        // What a deactivation before BL-529 left: inactive, the link kept, no mark, not a pending invitation.
        var byId = Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(user.Id, GuidRepresentation.Standard));
        await _host.Database.GetCollection<BsonDocument>("users").UpdateOneAsync(byId, Builders<BsonDocument>.Update
            .Set(nameof(User.IsActive), false).Set(nameof(User.MustChangePassword), false));
        await UnsetAsync("users", user.Id, nameof(User.DeactivatedByAdministrator));
        Assert.False((await ReadUserAsync(user.Id)).IsInvitationPending()); // non-vacuity

        HttpResponseMessage? refused = null;
        for (var i = 0; i <= Diten.AuthService.Infrastructure.Security.PasswordDoorRateLimiter.DefaultPerAddressLimit; i++)
        {
            using var anonymous = _host.Client();
            refused = await anonymous.PostAsJsonAsync("api/users/set-password", new { email = user.Email, token = link, newPassword = NewPassword });
        }

        Assert.Equal(HttpStatusCode.Conflict, refused!.StatusCode); // every time: the valid link is not counted
        Assert.Contains(AuthRefusalCodes.AccountDeactivated, await refused.Content.ReadAsStringAsync());
        Assert.False((await ReadUserAsync(user.Id)).IsActive);

        // The administrator switches it on: the SAME link now sets the password.
        using var admin = _host.Client(await TenantAdminTokenAsync(tenantId, "auth.users.update"), tenantId);
        Assert.True((await admin.PostAsync($"api/users/{user.Id}/enable", null)).IsSuccessStatusCode);
        using (var anonymous = _host.Client())
        {
            Assert.Equal(HttpStatusCode.NoContent,
                (await anonymous.PostAsJsonAsync("api/users/set-password", new { email = user.Email, token = link, newPassword = NewPassword })).StatusCode);
        }

        Assert.True((await ReadUserAsync(user.Id)).IsActive);
    }

    [Fact]
    public async Task A_platform_link_does_not_switch_on_an_account_deactivated_before_BL529()
    {
        var email = $"legacy.{Guid.NewGuid():N}@reset.test";
        await AssertOkAsync(await ProvisionPlatformAdminAsync(email)); // a live link; the account is created active, e-mail confirmed
        var admin = await PlatformUserAsync(email);
        await _host.Database.GetCollection<BsonDocument>("users").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(admin.Id, GuidRepresentation.Standard)),
            Builders<BsonDocument>.Update.Set(nameof(User.IsActive), false));
        await UnsetAsync("users", admin.Id, nameof(User.DeactivatedByAdministrator));

        var redeem = await AnonymousPostAsync("api/platform-auth/reset-password",
            new { email, token = _host.PlatformEmails.LastTokenFor(email), newPassword = NewPassword });

        Assert.Equal(HttpStatusCode.Conflict, redeem.StatusCode);
        Assert.Contains(AuthRefusalCodes.AccountDeactivated, await redeem.Content.ReadAsStringAsync());
        Assert.False((await PlatformUserAsync(email)).IsActive);
    }

    // ── FIX8 item 1 (a) / FIX9 items 1–3, 8: the one-time marker of accounts deactivated before BL-529 ─────────────

    private const string MarkerDatabase = "bl529_fix9_marker";

    /// <summary>The marker's database (fixed name, emptied first) seeded with every shape the rule must tell apart.</summary>
    private async Task<(IMongoDatabase Database, MarkerRows Rows)> MarkerDatabaseAsync()
    {
        await _host.Database.Client.DropDatabaseAsync(MarkerDatabase);
        var database = _host.Database.Client.GetDatabase(MarkerDatabase);
        var tenant = Guid.NewGuid();
        var secondTenant = Guid.NewGuid();
        User Legacy(Guid tenantId, string name)
        {
            var user = new User($"{name}.{Guid.NewGuid():N}@reset.test", "hash", "Le", "Gacy", tenantId);
            user.ConfirmEmail();
            user.Deactivate();
            user.SetPasswordResetToken("live-link-hash", DateTime.UtcNow.AddDays(3));
            return user;
        }

        var legacy = Legacy(tenant, "legacy");                          // deactivated before BL-529: field missing
        var storedFalse = Legacy(tenant, "stored-false");               // the field stored as false
        var secondTenantLegacy = Legacy(secondTenant, "second");        // another tenant
        var platformLegacy = Legacy(PlatformTenantId, "platform");      // the platform tenant
        var softDeleted = Legacy(tenant, "deleted");                    // deleted: never touched
        softDeleted.IsDeleted = true;
        var pending = new User($"pending.{Guid.NewGuid():N}@reset.test", "hash", "Pen", "Ding", tenant); // a pending invitation
        pending.Deactivate();
        pending.RequirePasswordChange(null);
        pending.SetPasswordResetToken("invite-hash", DateTime.UtcNow.AddDays(3));
        var active = new User($"active.{Guid.NewGuid():N}@reset.test", "hash", "Ac", "Tive", tenant);
        active.ConfirmEmail();
        var marked = Legacy(tenant, "marked");
        marked.DeactivateByAdministrator();
        await database.GetCollection<User>("users").InsertManyAsync([legacy, storedFalse, secondTenantLegacy, platformLegacy, softDeleted, pending, active, marked]);
        var raw = database.GetCollection<BsonDocument>("users");
        foreach (var unset in new[] { legacy, secondTenantLegacy, platformLegacy, softDeleted, pending, active })
        {
            await raw.UpdateOneAsync(ById(unset.Id), Builders<BsonDocument>.Update.Unset(nameof(User.DeactivatedByAdministrator)));
        }

        Assert.False((await raw.Find(ById(legacy.Id)).SingleAsync()).Contains(nameof(User.DeactivatedByAdministrator))); // non-vacuity
        Assert.False((await raw.Find(ById(storedFalse.Id)).SingleAsync())[nameof(User.DeactivatedByAdministrator)].AsBoolean);
        return (database, new MarkerRows([legacy.Id, storedFalse.Id, secondTenantLegacy.Id, platformLegacy.Id], pending.Id, softDeleted.Id, active.Id, marked.Id));
    }

    private sealed record MarkerRows(Guid[] Legacy, Guid Pending, Guid SoftDeleted, Guid Active, Guid Marked);

    private static FilterDefinition<BsonDocument> ById(Guid id) =>
        Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(id, GuidRepresentation.Standard));

    private static async Task<List<BsonDocument>> SnapshotAsync(IMongoDatabase database) =>
        await database.GetCollection<BsonDocument>("users").Find(Builders<BsonDocument>.Filter.Empty).Sort(Builders<BsonDocument>.Sort.Ascending("_id")).ToListAsync();

    private Func<string, string?> MarkerEnvironment(string? connection = null) => name => name switch
    {
        Diten.AuthService.LegacyDeactivationMarker.MarkerCommand.ConnectionVariable => connection ?? _host.ConnectionString,
        Diten.AuthService.LegacyDeactivationMarker.MarkerCommand.DatabaseVariable => MarkerDatabase,
        _ => null
    };

    [Fact]
    public async Task The_marker_finds_exactly_the_deactivated_accounts_that_are_not_pending_in_every_tenant()
    {
        var (database, rows) = await MarkerDatabaseAsync();

        // The tool's own entry: the connection string, opened in Persistence.
        var opened = Diten.AuthService.Persistence.Operations.LegacyDeactivationMarker.OpenDatabase(_host.ConnectionString, MarkerDatabase);
        var findings = await Diten.AuthService.Persistence.Operations.LegacyDeactivationMarker.FindAsync(opened);

        Assert.Equal(rows.Legacy.OrderBy(id => id), findings.Found.Select(a => a.UserId).OrderBy(id => id));
        Assert.Equal([rows.Pending], findings.SkippedPending.Select(s => s.Account.UserId));
        Assert.NotNull(findings.SkippedPending.Single().LinkExpiresAt);

        var result = await Diten.AuthService.Persistence.Operations.LegacyDeactivationMarker.MarkAsync(opened, findings.Found);

        Assert.Equal(findings.Found.Count, result.Marked.Count);
        Assert.Empty(result.NotMarked);
        var users = database.GetCollection<User>("users");
        foreach (var id in rows.Legacy)
        {
            var after = await users.Find(u => u.Id == id).SingleAsync();
            Assert.True(after.DeactivatedByAdministrator);
            Assert.Null(after.PasswordResetTokenHash);
            Assert.False(after.IsActive);
        }

        Assert.Equal("invite-hash", (await users.Find(u => u.Id == rows.Pending).SingleAsync()).PasswordResetTokenHash);
        Assert.Empty((await Diten.AuthService.Persistence.Operations.LegacyDeactivationMarker.FindAsync(opened)).Found); // idempotent
    }

    [Fact]
    public async Task The_marker_never_overwrites_an_account_that_changed_after_the_list_was_read()
    {
        var (database, rows) = await MarkerDatabaseAsync();
        var findings = await Diten.AuthService.Persistence.Operations.LegacyDeactivationMarker.FindAsync(database);
        var raw = database.GetCollection<BsonDocument>("users");
        var (activated, markedMeanwhile, deleted, reinvited) = (rows.Legacy[0], rows.Legacy[1], rows.Legacy[2], rows.Legacy[3]);
        await raw.UpdateOneAsync(ById(activated), Builders<BsonDocument>.Update.Set(nameof(User.IsActive), true));                  // (a)
        await raw.UpdateOneAsync(ById(markedMeanwhile), Builders<BsonDocument>.Update.Set(nameof(User.DeactivatedByAdministrator), true)); // (b)
        await raw.UpdateOneAsync(ById(deleted), Builders<BsonDocument>.Update.Set(nameof(User.IsDeleted), true));                   // (c)
        await raw.UpdateOneAsync(ById(reinvited), Builders<BsonDocument>.Update                                                    // (d) pending now
            .Set(nameof(User.MustChangePassword), true).Set(nameof(User.EmailConfirmed), false));
        var before = await SnapshotAsync(database);

        var result = await Diten.AuthService.Persistence.Operations.LegacyDeactivationMarker.MarkAsync(database, findings.Found);

        Assert.Empty(result.Marked);
        Assert.Equal(4, result.NotMarked.Count);
        Assert.Equal(before, await SnapshotAsync(database)); // not one byte changed
    }

    [Fact]
    public async Task The_marker_dry_run_writes_nothing_and_records_one_audit_row_without_personal_data()
    {
        var (database, rows) = await MarkerDatabaseAsync();
        var before = await SnapshotAsync(database);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await Diten.AuthService.LegacyDeactivationMarker.MarkerCommand.RunAsync([], MarkerEnvironment(), output, error);

        Assert.Equal(0, exit);
        Assert.Equal(before, await SnapshotAsync(database));
        Assert.Contains($"Database: {MarkerDatabase}", output.ToString());
        Assert.Contains(rows.Pending.ToString("D"), output.ToString()); // the skipped pending invitation is listed
        var audit = Assert.Single(await database.GetCollection<AuthAuditLog>("authAuditLogs").Find(_ => true).ToListAsync());
        Assert.Equal(Diten.AuthService.Persistence.Operations.LegacyDeactivationMarker.AuditEventName, audit.EventName);
        Assert.DoesNotContain("@", audit.Metadata);
        Assert.Contains(rows.Legacy[0].ToString("D"), audit.Metadata);
    }

    [Fact]
    public async Task The_marker_reports_a_write_that_skipped_an_account_with_a_non_zero_exit()
    {
        var (database, rows) = await MarkerDatabaseAsync();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await Diten.AuthService.LegacyDeactivationMarker.MarkerCommand.RunAsync(["--apply"], MarkerEnvironment(), output, error,
            afterFind: () => database.GetCollection<BsonDocument>("users").UpdateOneAsync(ById(rows.Legacy[0]),
                Builders<BsonDocument>.Update.Set(nameof(User.IsActive), true))); // an administrator activates one meanwhile

        Assert.Equal(4, exit);
        Assert.Contains(rows.Legacy[0].ToString("D"), error.ToString());
        Assert.False((await database.GetCollection<User>("users").Find(u => u.Id == rows.Legacy[0]).SingleAsync()).DeactivatedByAdministrator);
    }

    [Fact]
    public async Task The_marker_writes_nothing_when_the_count_is_not_the_expected_one()
    {
        var (database, _) = await MarkerDatabaseAsync();
        var before = await SnapshotAsync(database);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await Diten.AuthService.LegacyDeactivationMarker.MarkerCommand.RunAsync(["--apply", "--expect", "3"], MarkerEnvironment(), output, error);

        Assert.Equal(5, exit);
        Assert.Equal(before, await SnapshotAsync(database));
        Assert.Contains("\"mode\":\"refused\"", (await AuditRowAsync(database)).Metadata); // FIX10 (K3): not "dry-run"
    }

    private static async Task<AuthAuditLog> AuditRowAsync(IMongoDatabase database) =>
        Assert.Single(await database.GetCollection<AuthAuditLog>("authAuditLogs").Find(_ => true).ToListAsync());

    // ── FIX10 item 1: the write's tenant bound and the pending rule's "never signed in" ───────────────────────────

    [Fact]
    public async Task The_marker_never_marks_an_account_named_under_another_tenant()
    {
        var (database, rows) = await MarkerDatabaseAsync();
        var before = await SnapshotAsync(database);

        var result = await Diten.AuthService.Persistence.Operations.LegacyDeactivationMarker.MarkAsync(database,
            [new Diten.AuthService.Persistence.Operations.LegacyDeactivationMarker.Account(Guid.NewGuid(), rows.Legacy[0])]);

        Assert.Empty(result.Marked);
        Assert.Single(result.NotMarked);
        Assert.Equal(before, await SnapshotAsync(database));
    }

    [Fact]
    public async Task An_account_that_has_signed_in_is_marked_even_with_an_unconfirmed_email_and_a_forced_change()
    {
        var (database, _) = await MarkerDatabaseAsync();
        var signedIn = new User($"signed.{Guid.NewGuid():N}@reset.test", "hash", "Si", "Gned", Guid.NewGuid());
        signedIn.Deactivate();
        signedIn.RequirePasswordChange(null);
        signedIn.RecordLoginSuccess();
        await database.GetCollection<User>("users").InsertOneAsync(signedIn);
        Assert.False((await database.GetCollection<User>("users").Find(u => u.Id == signedIn.Id).SingleAsync()).EmailConfirmed); // non-vacuity

        var findings = await Diten.AuthService.Persistence.Operations.LegacyDeactivationMarker.FindAsync(database);
        Assert.Contains(findings.Found, a => a.UserId == signedIn.Id);
        var result = await Diten.AuthService.Persistence.Operations.LegacyDeactivationMarker.MarkAsync(database, findings.Found);

        Assert.Contains(result.Marked, a => a.UserId == signedIn.Id); // the write's $nor holds it too: it has signed in
        Assert.True((await database.GetCollection<User>("users").Find(u => u.Id == signedIn.Id).SingleAsync()).DeactivatedByAdministrator);
    }

    // ── FIX10 item 2: the command an operator runs ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_operator_command_marks_exactly_the_expected_accounts_and_records_them()
    {
        var (database, rows) = await MarkerDatabaseAsync();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await Diten.AuthService.LegacyDeactivationMarker.MarkerCommand.RunAsync(["--apply", "--expect", "4"], MarkerEnvironment(), output, error);

        Assert.Equal(0, exit);
        Assert.Contains("APPLIED — 4 of 4", output.ToString());
        var users = database.GetCollection<User>("users");
        foreach (var id in rows.Legacy)
        {
            Assert.True((await users.Find(u => u.Id == id).SingleAsync()).DeactivatedByAdministrator);
        }

        var audit = await AuditRowAsync(database);
        Assert.Contains("\"mode\":\"apply\"", audit.Metadata);
        Assert.All(rows.Legacy, id => Assert.Contains(id.ToString("D"), audit.Metadata));
        Assert.Contains("\"markedIds\"", audit.Metadata);
    }

    // ── FIX10 item 3: a run that stops part-way (CT K4) ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_run_that_stops_part_way_lists_what_it_wrote_and_what_it_did_not_reach_and_records_it()
    {
        var (database, _) = await MarkerDatabaseAsync();
        var found = (await Diten.AuthService.Persistence.Operations.LegacyDeactivationMarker.FindAsync(database)).Found;
        var blocked = found[^1].TenantId;                         // the store refuses the mark for this one tenant
        var stopAt = found.ToList().FindIndex(a => a.TenantId == blocked);
        Assert.True(stopAt > 0); // non-vacuity: something is written before the stop
        await database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            { "collMod", "users" },
            {
                "validator", new BsonDocument("$or", new BsonArray
                {
                    new BsonDocument(nameof(User.TenantId), new BsonDocument("$ne", new BsonBinaryData(blocked, GuidRepresentation.Standard))),
                    new BsonDocument(nameof(User.DeactivatedByAdministrator), new BsonDocument("$ne", true))
                })
            },
            { "validationAction", "error" }
        });
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await Diten.AuthService.LegacyDeactivationMarker.MarkerCommand.RunAsync(["--apply"], MarkerEnvironment(), output, error);

        Assert.Equal(3, exit);
        var lines = output.ToString().Split('\n');
        Assert.Equal(found.Take(stopAt).Select(a => a.UserId.ToString("D")),
            lines.Where(l => l.TrimStart().StartsWith("marked ")).Select(l => l.Trim()[^36..]));
        Assert.Equal(found.Skip(stopAt).Select(a => a.UserId.ToString("D")),
            lines.Where(l => l.TrimStart().StartsWith("not reached ")).Select(l => l.Trim()[^36..]));
        Assert.Contains($"{stopAt} marked, {found.Count - stopAt} not reached", error.ToString());
        var audit = await AuditRowAsync(database);
        Assert.Contains("\"stoppedPartWay\":true", audit.Metadata);
        Assert.Contains($"\"notReached\":{found.Count - stopAt}", audit.Metadata);
    }

    // CT acceptance (K10): the apply path says "done, not on record" with the same code as the dry run — its marks landed.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_run_whose_audit_row_cannot_be_written_says_so_with_its_own_exit_code(bool apply)
    {
        var (database, _) = await MarkerDatabaseAsync();
        await database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            { "create", "authAuditLogs" },
            { "validator", new BsonDocument("EventName", "never") },
            { "validationAction", "error" }
        });
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await Diten.AuthService.LegacyDeactivationMarker.MarkerCommand.RunAsync(
            apply ? ["--apply"] : [], MarkerEnvironment(), output, error);

        Assert.Equal(6, exit); // FIX10 (K2): done, but not on record — neither a connection failure nor a success
        Assert.Contains("audit row could not be written", error.ToString());
        if (apply)
        {
            Assert.Contains("APPLIED", output.ToString());
        }
    }

    // ── FIX10 item 5 (CT K3): the read never loads a password hash ─────────────────────────────────────────────────

    [Fact]
    public async Task The_marker_read_never_asks_for_a_password_hash()
    {
        await MarkerDatabaseAsync();
        var projections = new System.Collections.Concurrent.ConcurrentQueue<List<string>>();
        var settings = MongoClientSettings.FromConnectionString(_host.ConnectionString);
        settings.ClusterConfigurator = cluster => cluster.Subscribe<MongoDB.Driver.Core.Events.CommandStartedEvent>(started =>
        {
            if (started.CommandName == "find" && started.Command.GetValue("find", "").ToString() == "users")
            {
                // Copied here: the event's command document is released once the callback returns.
                projections.Enqueue(started.Command.GetValue("projection", new BsonDocument()).AsBsonDocument.Names.ToList());
            }
        });
        var observed = new MongoClient(settings).GetDatabase(MarkerDatabase);

        await Diten.AuthService.Persistence.Operations.LegacyDeactivationMarker.FindAsync(observed);

        var projection = Assert.Single(projections);
        Assert.True(projection.Contains(nameof(User.EmailConfirmed))); // non-vacuity: an inclusion projection was sent
        Assert.False(projection.Contains(nameof(User.PasswordHash)));
    }

    // ── FIX10 item 4: the seed's warning, and its condition ───────────────────────────────────────────────────────

    [Fact]
    public async Task The_seed_warns_and_writes_nothing_when_the_admin_it_read_cannot_be_written()
    {
        var (database, adminId) = await SeededAdminDatabaseAsync();
        var raw = database.GetCollection<BsonDocument>("users");
        // Soft-deleted: the seed still finds it by e-mail, but the targeted write (live accounts only) does not match.
        await raw.UpdateOneAsync(ById(adminId), Builders<BsonDocument>.Update
            .Set(nameof(User.IsDeleted), true).Set(nameof(User.IsActive), false).Set(nameof(User.EmailConfirmed), false));
        var before = await raw.Find(ById(adminId)).SingleAsync();
        var logger = new WarningCapture();

        await Diten.AuthService.Persistence.Seed.DataSeeder.SeedAsync(database, seedMockUsers: false, logger: logger, beforeSeedSteps: null);

        Assert.Equal(before, await raw.Find(ById(adminId)).SingleAsync());
        Assert.Contains(logger.Warnings, w => w.Contains("seeded admin changed", StringComparison.Ordinal));
    }

    private sealed class WarningCapture : Microsoft.Extensions.Logging.ILogger
    {
        public System.Collections.Concurrent.ConcurrentQueue<string> Warnings { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == Microsoft.Extensions.Logging.LogLevel.Warning)
            {
                Warnings.Enqueue(formatter(state, exception));
            }
        }
    }

    [Fact]
    public async Task The_marker_process_never_prints_a_broken_connection_string()
    {
        // As a process: the tool's own executable, the environment as an operator sets it.
        // FIX10 (K4) — assembled at run time (no credential-shaped literal in the source), and measured: the driver's own
        // message quotes it, so "never printed" means something.
        // (The driver hides a user name and password in its messages, so the marker sits where it does NOT hide it.)
        var marker = string.Concat("do-not", "-print-", "7f3a");
        var secret = new[]
            {
                string.Concat("mongodb", "://", "user", ":", "pw", "@", marker, ":", "99999999", "/"),
                string.Concat("mongodb", "://", "localhost", "/?", "authMechanism", "=", marker),
                string.Concat("mongodb", "://", "localhost", "/?", "replicaSet", "=", marker, "&", "connect", "=", marker)
            }
            .FirstOrDefault(candidate =>
                (Record.Exception(() => MongoClientSettings.FromConnectionString(candidate))?.Message ?? string.Empty)
                    .Contains(marker, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(secret); // measured: the driver's own message quotes this string
        var dll = Path.Combine(AppContext.BaseDirectory, "Diten.AuthService.LegacyDeactivationMarker.dll");
        Assert.True(File.Exists(dll), dll);
        var start = new System.Diagnostics.ProcessStartInfo("dotnet", $"\"{dll}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.Environment[Diten.AuthService.LegacyDeactivationMarker.MarkerCommand.ConnectionVariable] = secret!;
        start.Environment[Diten.AuthService.LegacyDeactivationMarker.MarkerCommand.DatabaseVariable] = MarkerDatabase;
        using var process = System.Diagnostics.Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(60_000));

        Assert.Equal(1, process.ExitCode);
        Assert.DoesNotContain(marker, await stdout, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(marker, await stderr, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("connection error", await stderr);
    }

    // ── FIX9 item 4: the seeder's targeted write ─────────────────────────────────────────────────────────────────────

    private async Task<(IMongoDatabase Database, Guid AdminId)> SeededAdminDatabaseAsync()
    {
        const string seedDatabase = "bl529_fix9_seed";
        await _host.Database.Client.DropDatabaseAsync(seedDatabase);
        var database = _host.Database.Client.GetDatabase(seedDatabase);
        await Diten.AuthService.Persistence.Seed.DataSeeder.SeedAsync(database, seedMockUsers: false, logger: null, beforeSeedSteps: null);
        var admin = await database.GetCollection<User>("users").Find(u => u.Email == "admin@diten.com").SingleAsync();
        return (database, admin.Id);
    }

    [Fact]
    public async Task The_seed_writes_only_its_own_fields_and_repairs_an_unmarked_admin()
    {
        var (database, adminId) = await SeededAdminDatabaseAsync();
        var raw = database.GetCollection<BsonDocument>("users");
        await raw.UpdateOneAsync(ById(adminId), Builders<BsonDocument>.Update
            // (1) a field the seed does not change, stored as Int64: a whole-document write from the copy read re-serializes
            //     it as the model's Int32; a targeted $set never touches it. (Not an unknown field: whether the class map
            //     ignores extra elements depends on which test built it first.)
            .Set(nameof(User.FailedLoginAttempts), new BsonInt64(0))
            .Set(nameof(User.IsActive), false)                 // (2) unmarked, passive, unnamed, untyped, unconfirmed
            .Set(nameof(User.NormalizedUserName), "")
            .Unset(nameof(User.PlatformActorType))
            .Set(nameof(User.EmailConfirmed), false));

        await Diten.AuthService.Persistence.Seed.DataSeeder.SeedAsync(database, seedMockUsers: false, logger: null, beforeSeedSteps: null);

        var document = await raw.Find(ById(adminId)).SingleAsync();
        Assert.Equal(BsonType.Int64, document[nameof(User.FailedLoginAttempts)].BsonType);
        var admin = await database.GetCollection<User>("users").Find(u => u.Id == adminId).SingleAsync();
        Assert.True(admin.IsActive);
        Assert.Equal("admin", admin.NormalizedUserName);
        Assert.Equal("platform_admin", admin.PlatformActorType);
        Assert.True(admin.EmailConfirmed);
    }

    [Fact]
    public async Task The_seed_repairs_a_marked_admin_but_leaves_it_off()
    {
        var (database, adminId) = await SeededAdminDatabaseAsync();
        await database.GetCollection<BsonDocument>("users").UpdateOneAsync(ById(adminId), Builders<BsonDocument>.Update
            .Set(nameof(User.IsActive), false).Set(nameof(User.DeactivatedByAdministrator), true)
            .Unset(nameof(User.PlatformActorType)).Set(nameof(User.EmailConfirmed), false));

        await Diten.AuthService.Persistence.Seed.DataSeeder.SeedAsync(database, seedMockUsers: false, logger: null, beforeSeedSteps: null);

        var admin = await database.GetCollection<User>("users").Find(u => u.Id == adminId).SingleAsync();
        Assert.False(admin.IsActive);
        Assert.True(admin.DeactivatedByAdministrator);
        Assert.Equal("platform_admin", admin.PlatformActorType);
        Assert.True(admin.EmailConfirmed);
    }

    // ── FIX9 item 5: an account that has signed in is never a pending invitation (CT G5) ───────────────────────────

    [Fact]
    public async Task A_passive_account_that_has_signed_in_before_is_not_a_pending_invitation()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        Assert.Equal(HttpStatusCode.OK, (await ResetOnUsersScreenAsync(tenantId, user.Id)).StatusCode); // a live link
        await _host.Database.GetCollection<BsonDocument>("users").UpdateOneAsync(ById(user.Id), Builders<BsonDocument>.Update
            .Set(nameof(User.IsActive), false).Set(nameof(User.MustChangePassword), true).Set(nameof(User.EmailConfirmed), false)
            .Set(nameof(User.LastLoginAt), DateTime.UtcNow.AddDays(-10)));
        await UnsetAsync("users", user.Id, nameof(User.DeactivatedByAdministrator));

        var redeem = await RedeemTenantLinkAsync(user.Email, NewPassword);

        Assert.Equal(HttpStatusCode.Conflict, redeem.StatusCode);
        Assert.False((await ReadUserAsync(user.Id)).IsActive);
    }

    // ── FIX9 item 7: the doors' writes hold on the switch as read ──────────────────────────────────────────────────

    [Fact]
    public async Task A_plain_deactivation_landing_during_a_tenant_link_redemption_is_not_undone()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        Assert.Equal(HttpStatusCode.OK, (await ResetOnUsersScreenAsync(tenantId, user.Id)).StatusCode); // active, with a live link

        // Held at the password-policy read; an old instance switches it off without the mark meanwhile.
        var redeem = await RaceAsync("settings:" + tenantId,
            () => RedeemTenantLinkAsync(user.Email, NewPassword),
            async () => await _host.Database.GetCollection<BsonDocument>("users").UpdateOneAsync(ById(user.Id),
                Builders<BsonDocument>.Update.Set(nameof(User.IsActive), false)));

        Assert.Equal(HttpStatusCode.BadRequest, redeem.StatusCode);
        Assert.False((await ReadUserAsync(user.Id)).IsActive);
    }

    [Fact]
    public async Task A_plain_deactivation_landing_during_a_platform_link_redemption_is_not_undone()
    {
        var email = $"pending.{Guid.NewGuid():N}@reset.test";
        await AssertOkAsync(await ProvisionPlatformAdminAsync(email)); // active (created so), with a live link
        var admin = await PlatformUserAsync(email);

        var redeem = await RaceAsync("status:" + email,
            () => AnonymousPostAsync("api/platform-auth/reset-password", new { email, token = _host.PlatformEmails.LastTokenFor(email), newPassword = NewPassword }),
            async () => await _host.Database.GetCollection<BsonDocument>("users").UpdateOneAsync(ById(admin.Id),
                Builders<BsonDocument>.Update.Set(nameof(User.IsActive), false)));

        Assert.Equal(HttpStatusCode.BadRequest, redeem.StatusCode);
        Assert.False((await PlatformUserAsync(email)).IsActive);
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

    private static string RandomPeer() => new System.Net.IPAddress(Guid.NewGuid().ToByteArray()[..4]).ToString();

    private async Task<HttpResponseMessage> PeerPostAsync(string peer, string path, object body, string? forwardedFor = null)
    {
        using var client = _host.Client();
        client.DefaultRequestHeaders.Add("X-Test-Peer", peer);
        if (forwardedFor is not null) client.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
        return await client.PostAsJsonAsync(path, body);
    }

    private async Task AwaitReachedAsync(string key)
    {
        var reached = await Task.WhenAny(_host.Gate.ReachedOf(key), Task.Delay(TimeSpan.FromSeconds(30)));
        Assert.True(reached == _host.Gate.ReachedOf(key), $"the request never reached {key}");
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
        private sealed class Gate
        {
            public TaskCompletionSource Reached { get; } = New();
            public TaskCompletionSource Release { get; } = New();
            public bool Passed { get; set; }
        }

        private readonly object _lock = new();
        private readonly Dictionary<string, Gate> _gates = new(StringComparer.Ordinal);
        private string? _last;

        /// <summary>Holds the FIRST request that passes <paramref name="key"/> until <see cref="ReleaseOf"/>. Several keys may be armed.</summary>
        public void Arm(string key)
        {
            lock (_lock)
            {
                _gates[key] = new Gate();
                _last = key;
            }
        }

        public Task ReachedOf(string key)
        {
            lock (_lock) return _gates[key].Reached.Task;
        }

        public void ReleaseOf(string key)
        {
            lock (_lock)
            {
                if (_gates.TryGetValue(key, out var gate)) gate.Release.TrySetResult();
            }
        }

        public Task Reached => ReachedOf(_last!);

        public void Release() => ReleaseOf(_last!);

        public void Pass(string key)
        {
            Gate? held = null;
            lock (_lock)
            {
                if (_gates.TryGetValue(key, out var gate) && !gate.Passed)
                {
                    gate.Passed = true;
                    gate.Reached.TrySetResult();
                    held = gate;
                }
            }

            held?.Release.Task.Wait(TimeSpan.FromSeconds(30));
        }

        private static TaskCompletionSource New() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>FIX3 — sets the connection's peer address: X-Test-Peer when the test names one, a fresh one otherwise.</summary>
    private sealed class TestPeerStartupFilter : Microsoft.AspNetCore.Hosting.IStartupFilter
    {
        public Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> Configure(Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> next)
            => app =>
            {
                Microsoft.AspNetCore.Builder.UseExtensions.Use(app, async (context, nextMiddleware) =>
                {
                    context.Connection.RemoteIpAddress = context.Request.Headers.TryGetValue("X-Test-Peer", out var named)
                        ? System.Net.IPAddress.Parse(named.ToString())
                        : new System.Net.IPAddress(Guid.NewGuid().ToByteArray()[..4]);
                    await nextMiddleware();
                });
                next(app);
            };
    }

    /// <summary>FIX3 — the production refresh-token repository, held before a session is written or before a sweep.</summary>
    private sealed class GatedRefreshTokens(IRefreshTokenRepository inner, Barrier gate) : IRefreshTokenRepository
    {
        public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct) => inner.GetByTokenAsync(token, ct);
        public Task CreateAsync(RefreshToken refreshToken, CancellationToken ct)
        {
            gate.Pass("create:" + refreshToken.UserId);
            return inner.CreateAsync(refreshToken, ct);
        }
        public Task UpdateAsync(RefreshToken refreshToken, CancellationToken ct) => inner.UpdateAsync(refreshToken, ct);
        public Task RevokeAsync(string token, CancellationToken ct) => inner.RevokeAsync(token, ct);
        public Task<bool> RevokeIfLiveAsync(string token, string reason, CancellationToken ct) => inner.RevokeIfLiveAsync(token, reason, ct);
        public Task<long> RevokeAllByUserAsync(Guid userId, Guid tenantId, CancellationToken ct) => inner.RevokeAllByUserAsync(userId, tenantId, ct);
        public Task<long> RevokeLiveSessionsAsync(Guid userId, Guid tenantId, string reason, CancellationToken ct)
        {
            gate.Pass("scan:" + userId);
            return inner.RevokeLiveSessionsAsync(userId, tenantId, reason, ct);
        }
        public Task<bool> TryRotateAsync(Guid tokenId, string replacedByTokenHash, string? revokedByIp, CancellationToken ct) => inner.TryRotateAsync(tokenId, replacedByTokenHash, revokedByIp, ct);
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
        private readonly ConcurrentDictionary<string, int> _sent = new(StringComparer.OrdinalIgnoreCase);

        public string LastTokenFor(string email) => _last[email];

        public int SentTo(string email) => _sent.TryGetValue(email, out var n) ? n : 0;

        public string BuildTenantSetPasswordUrl(string email, string setupToken)
            => $"http://localhost/set-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(setupToken)}";

        public Task SendTenantUserInvitationAsync(string email, string setupToken, CancellationToken ct)
        {
            _sent.AddOrUpdate(email, 1, (_, n) => n + 1);
            _last[email] = setupToken;
            return Task.CompletedTask;
        }

        private readonly ConcurrentDictionary<string, int> _resets = new(StringComparer.OrdinalIgnoreCase);

        // How many of the mails to this address were the RESET mail (not the invitation).
        public int ResetsTo(string email) => _resets.TryGetValue(email, out var n) ? n : 0;

        // BL-454 (lane) — the Users-screen reset now sends the reset mail; it carries the same link.
        public Task SendTenantUserPasswordResetAsync(string email, string setupToken, CancellationToken ct)
        {
            _resets.AddOrUpdate(email, 1, (_, n) => n + 1);
            return SendTenantUserInvitationAsync(email, setupToken, ct);
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

        public Task SendEmailOtpAsync(Guid tenantId, string email, string code, DateTime expiresAtUtc, CancellationToken ct)
        {
            _last[email] = code;
            return Task.CompletedTask;
        }
    }

    /// <summary>The production user repository; a request can be held where it reads an account by id
    /// (<c>read:{id}</c>) or right before a reset's conditional write (<c>write:{id}</c>).</summary>
    /// <summary>The production user-role repository; an assignment in a tenant named in FailOnce throws, once.</summary>
    private sealed class FailingOnceUserRoles(IUserRoleRepository inner, ConcurrentDictionary<string, bool> failOnce) : IUserRoleRepository
    {
        public Task<IEnumerable<string>> GetRolesByUserAsync(Guid userId, Guid tenantId, CancellationToken ct) => inner.GetRolesByUserAsync(userId, tenantId, ct);
        public Task AssignAsync(UserRole userRole, CancellationToken ct) => failOnce.TryRemove("assign:" + userRole.TenantId, out _)
            ? throw new InvalidOperationException("injected: the role assignment fails once")
            : inner.AssignAsync(userRole, ct);
        public Task RevokeAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct) => inner.RevokeAsync(userId, roleId, tenantId, ct);
        public Task<bool> ExistsAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct) => inner.ExistsAsync(userId, roleId, tenantId, ct);
        public Task<IReadOnlyCollection<Guid>> GetUserIdsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) => inner.GetUserIdsByRoleAsync(roleId, tenantId, ct);
    }

    private sealed class GatedUserRepository(IUserRepository inner, Barrier gate, ConcurrentDictionary<string, bool> failOnce) : IUserRepository
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
        public async Task<User> CreateAsync(User user, CancellationToken ct)
        {
            if (failOnce.TryRemove("create:" + user.Email, out _))
            {
                throw new InvalidOperationException("injected: the account write fails once");
            }

            if (failOnce.TryRemove("race:" + user.Email, out _))
            {
                // Another request wins the race: the same address in the same tenant is written first; the unique index
                // then refuses this write exactly as in production.
                await inner.CreateAsync(new User(user.Email, "race-winner-hash", "Race", "Winner", user.TenantId), ct);
            }

            return await inner.CreateAsync(user, ct);
        }
        public Task SoftDeleteAsync(Guid id, Guid tenantId, CancellationToken ct) => inner.SoftDeleteAsync(id, tenantId, ct);
        public Task RecordLoginOutcomeAsync(User user, Guid tenantId, CancellationToken ct) => inner.RecordLoginOutcomeAsync(user, tenantId, ct);
        public object CaptureState(User user) => inner.CaptureState(user);
        public Task<bool> TryWriteChangesAsync(User user, object capturedState, Guid tenantId, UserWriteCondition condition, CancellationToken ct)
        {
            gate.Pass("write:" + user.Id);
            return inner.TryWriteChangesAsync(user, capturedState, tenantId, condition, ct);
        }
        public Task<bool> SetPasswordResetTokenAsync(Guid userId, Guid tenantId, string tokenHash, DateTime expiresAtUtc, CancellationToken ct) => inner.SetPasswordResetTokenAsync(userId, tenantId, tokenHash, expiresAtUtc, ct);
        public Task<LoginFailureOutcome> RecordLoginFailureAsync(Guid userId, Guid tenantId, int maxFailedAttempts, int lockoutDurationMinutes, CancellationToken ct) => inner.RecordLoginFailureAsync(userId, tenantId, maxFailedAttempts, lockoutDurationMinutes, ct);
    }
}

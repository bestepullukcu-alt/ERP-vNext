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
            services.AddScoped<IPlatformAdministratorStatusClient, AlwaysActive>();
            services.AddScoped<ITenantUserInvitationEmailService>(_ => TenantEmails);
            services.AddScoped<IPlatformAuthEmailService>(_ => PlatformEmails);
            services.AddScoped<IPasswordHasher>(_ => new GatedHasher(Gate));
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
    public async Task Deactivating_a_pending_invitation_keeps_its_link_as_before()
    {
        var tenantId = _host.Seeded.TenantId;
        using var client = _host.Client(await TenantAdminTokenAsync(tenantId, "auth.users.create", "auth.users.update"), tenantId);
        var email = $"invited.{Guid.NewGuid():N}@reset.test";
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("api/users", new { email, firstName = "In", lastName = "Vited" })).StatusCode);
        var invited = await TenantUserByEmailAsync(email, tenantId);

        var disable = await client.PostAsync($"api/users/{invited.Id}/disable", null);

        Assert.True(disable.IsSuccessStatusCode, $"{(int)disable.StatusCode}: {await disable.Content.ReadAsStringAsync()}");
        Assert.NotNull((await ReadUserAsync(invited.Id)).PasswordResetTokenHash);
        Assert.Equal(HttpStatusCode.NoContent, (await RedeemTenantLinkAsync(email, NewPassword)).StatusCode); // unchanged flow
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
                tenantId, TwoFactorEnabled: false, MfaRequired: false, EmailLoginEnabled: true, PhoneLoginEnabled: false,
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

    private sealed class AlwaysActive : IPlatformAdministratorStatusClient
    {
        public Task<bool> IsActiveAsync(string email, CancellationToken ct) => Task.FromResult(true);
        public Task MarkLoginAcceptedAsync(string email, CancellationToken ct) => Task.CompletedTask;
    }
}

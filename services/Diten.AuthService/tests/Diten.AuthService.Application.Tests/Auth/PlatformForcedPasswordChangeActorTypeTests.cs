using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.AuthService.Api.Controllers;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Auth;

/// <summary>
/// BL-521 FIX2 item 1 — <c>POST api/platform-auth/change-password/forced</c> mints a platform token, so its actor type
/// comes from the administrator's Platform RECORD. Before: it came from Auth's copy on the user through a normaliser that
/// knew only "PartnerAdmin" — the stored "partner_admin" came out as platform_admin, so a partner administrator calling
/// this door with its own current password received a platform_admin token AND a platform_admin refresh row.
///
/// <para>Item 3 — Platform's definitive no ends every session; NO answer (503) ends nothing and changes nothing.</para>
///
/// <para>FIX3 item 4 — THE DOOR IS NARROWER THAN LOGIN, NOT WIDER. It accepted any valid JWT (a tenant_user token of a
/// user in the platform tenant included) and checked neither the account's own state nor that a change was pending, so
/// an account disabled in Auth but with a live Platform record could mint itself a fresh platform session with every new
/// token, indefinitely. Now: a platform actor's token only (403), an active account (401), a pending forced change (409 —
/// an ordinary change has its own door), an unexpired temporary password (401).</para>
///
/// <para>Real HTTP on the production Api over a test-owned mongod (the shared acceptance host); only the outbound
/// Platform edges are fixed answers. Disposable platform users only — never the seeded admin.</para>
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class PlatformForcedPasswordChangeActorTypeTests : IClassFixture<PlatformForcedPasswordChangeActorTypeTests.Host>
{
    private static readonly Guid PlatformTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private const string CurrentPassword = "Current!Passw0rd";
    private const string NewPassword = "Fresh!Passw0rd2026";

    private readonly Host _host;

    public PlatformForcedPasswordChangeActorTypeTests(Host host) => _host = host;

    public sealed class Host : AccountKindAcceptance.AuthTestHost
    {
        /// <summary>What Platform answers per administrator e-mail (each test seeds its own address).</summary>
        public static readonly ConcurrentDictionary<string, PlatformAdministratorRecordAnswer> Records = new(StringComparer.OrdinalIgnoreCase);

        protected override void ConfigureTestServices(IServiceCollection services)
        {
            base.ConfigureTestServices(services);
            services.AddScoped<IPlatformAdministratorStatusClient, RecordByEmail>();
            services.AddScoped<ITenantLoginSettingsClient, TenCharactersUpperAndSpecial>();
        }
    }

    [Theory]
    [InlineData("partner_admin")] // the stored spelling the old normaliser turned into platform_admin
    [InlineData("platform_admin")] // a copy a failed sync left behind
    public async Task A_partner_changing_its_password_receives_a_partner_token_and_refresh_row_whatever_the_auth_copy_says(string authCopy)
    {
        var seeded = await SeedAsync(authCopy, PlatformAdministratorRecordAnswer.ConfirmedAs("partner_admin"), tokenActor: "partner_admin");
        using var client = _host.Client(seeded.AccessToken, PlatformTenantId);

        var response = await ChangeAsync(client);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {body}");
        using var json = JsonDocument.Parse(body);
        var data = json.RootElement.GetProperty("data");
        var issued = new JwtSecurityTokenHandler().ReadJwtToken(data.GetProperty("accessToken").GetString());
        Assert.Equal("partner_admin", issued.Claims.Single(c => c.Type == "actor_type").Value);
        var row = await RefreshRowAsync(data.GetProperty("refreshToken").GetString()!);
        Assert.Equal("partner_admin", row!.ActorType);
    }

    [Fact]
    public async Task A_record_platform_refuses_ends_every_session_and_changes_nothing()
    {
        var seeded = await SeedAsync("platform_admin", PlatformAdministratorRecordAnswer.Refused);
        using var client = _host.Client(seeded.AccessToken, PlatformTenantId);

        var response = await ChangeAsync(client);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull((await RefreshRowAsync(seeded.ExistingRefreshToken))!.RevokedAt);
        Assert.True(await PasswordIsStillAsync(seeded.UserId, CurrentPassword));
    }

    [Fact]
    public async Task When_platform_cannot_answer_the_change_is_refused_503_and_no_session_ends()
    {
        var seeded = await SeedAsync("partner_admin", PlatformAdministratorRecordAnswer.Unavailable, tokenActor: "partner_admin");
        using var client = _host.Client(seeded.AccessToken, PlatformTenantId);

        var response = await ChangeAsync(client);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.ServiceUnavailable, $"{(int)response.StatusCode}: {body}");
        Assert.Contains(PlatformAdministratorRecordAnswer.UnavailableMessage, body, StringComparison.Ordinal);
        Assert.Null((await RefreshRowAsync(seeded.ExistingRefreshToken))!.RevokedAt);
        Assert.True(await PasswordIsStillAsync(seeded.UserId, CurrentPassword));
    }

    // ── FIX3 item 4 — who may use the door at all ─────────────────────────────────────────────────────────

    /// <summary>A user of the platform tenant holding a TENANT token — even with a pending change and a live record — is
    /// not a platform actor: refused before anything is read or changed.</summary>
    [Fact]
    public async Task A_tenant_user_token_is_refused_403_and_changes_nothing()
    {
        var seeded = await SeedAsync("platform_admin", PlatformAdministratorRecordAnswer.ConfirmedAs("platform_admin"), tokenActor: "tenant_user");
        using var client = _host.Client(seeded.AccessToken, PlatformTenantId);

        var response = await ChangeAsync(client);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{(int)response.StatusCode}: {body}");
        Assert.Contains(PlatformAuthController.ForcedChangeRequiresPlatformActorCode, body, StringComparison.Ordinal);
        Assert.True(await PasswordIsStillAsync(seeded.UserId, CurrentPassword));
        Assert.Null((await RefreshRowAsync(seeded.ExistingRefreshToken))!.RevokedAt);
    }

    /// <summary>Disabled in Auth, live in Platform: before, every new token opened a fresh platform session here.</summary>
    [Fact]
    public async Task An_account_disabled_in_auth_is_refused_even_while_its_platform_record_is_active()
    {
        var seeded = await SeedAsync("platform_admin", PlatformAdministratorRecordAnswer.ConfirmedAs("platform_admin"), active: false);
        using var client = _host.Client(seeded.AccessToken, PlatformTenantId);

        var response = await ChangeAsync(client);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(await PasswordIsStillAsync(seeded.UserId, CurrentPassword));
        Assert.Empty(await NewRefreshRowsAsync(seeded));
    }

    /// <summary>No forced change pending: this door is not the ordinary password change — 409 with its code, nothing
    /// changed, no token minted.</summary>
    [Fact]
    public async Task Without_a_pending_forced_change_the_door_answers_409_and_changes_nothing()
    {
        var seeded = await SeedAsync("platform_admin", PlatformAdministratorRecordAnswer.ConfirmedAs("platform_admin"), mustChange: false);
        using var client = _host.Client(seeded.AccessToken, PlatformTenantId);

        var response = await ChangeAsync(client);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.Conflict, $"{(int)response.StatusCode}: {body}");
        Assert.Contains(PlatformAuthController.ForcedChangeNotPendingCode, body, StringComparison.Ordinal);
        Assert.True(await PasswordIsStillAsync(seeded.UserId, CurrentPassword));
        Assert.Empty(await NewRefreshRowsAsync(seeded));
    }

    [Fact]
    public async Task An_expired_temporary_password_is_refused_and_changes_nothing()
    {
        var seeded = await SeedAsync("platform_admin", PlatformAdministratorRecordAnswer.ConfirmedAs("platform_admin"),
            temporaryPasswordExpiresAt: DateTime.UtcNow.AddMinutes(-5));
        using var client = _host.Client(seeded.AccessToken, PlatformTenantId);

        var response = await ChangeAsync(client);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"{(int)response.StatusCode}: {body}");
        Assert.Contains(PlatformAuthController.TemporaryPasswordExpiredCode, body, StringComparison.Ordinal);
        Assert.True(await PasswordIsStillAsync(seeded.UserId, CurrentPassword));
        Assert.Empty(await NewRefreshRowsAsync(seeded));
    }

    /// <summary>The door's own case still works: a platform administrator with a pending change and an unexpired
    /// temporary password changes it, and the pending flag is cleared.</summary>
    [Fact]
    public async Task A_platform_administrator_with_a_pending_change_and_a_live_temporary_password_changes_it()
    {
        var seeded = await SeedAsync("platform_admin", PlatformAdministratorRecordAnswer.ConfirmedAs("platform_admin"),
            temporaryPasswordExpiresAt: DateTime.UtcNow.AddHours(1));
        using var client = _host.Client(seeded.AccessToken, PlatformTenantId);

        var response = await ChangeAsync(client);

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        Assert.True(await PasswordIsStillAsync(seeded.UserId, NewPassword));
        Assert.False((await UserAsync(seeded.UserId)).MustChangePassword);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    private sealed record Seeded(Guid UserId, string AccessToken, string ExistingRefreshToken);

    private static Task<HttpResponseMessage> ChangeAsync(HttpClient client) =>
        client.PostAsJsonAsync("api/platform-auth/change-password/forced",
            new { currentPassword = CurrentPassword, newPassword = NewPassword, rememberMe = false });

    /// <param name="tokenActor">The token's actor type: a platform actor's (<c>platform_admin</c> / <c>partner_admin</c>)
    /// minted the way login mints it, or <c>tenant_user</c> (a tenant token of the same user).</param>
    private async Task<Seeded> SeedAsync(
        string authCopy,
        PlatformAdministratorRecordAnswer record,
        string tokenActor = "platform_admin",
        bool mustChange = true,
        DateTime? temporaryPasswordExpiresAt = null,
        bool active = true)
    {
        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(PlatformTenantId);

        var user = new User($"forced.{Guid.NewGuid():N}@bl521.test", sp.GetRequiredService<IPasswordHasher>().Hash(CurrentPassword), "Forced", "Change", PlatformTenantId);
        user.ConfirmEmail();
        user.SetPlatformActorType(authCopy);
        if (mustChange)
        {
            user.RequirePasswordChange(temporaryPasswordExpiresAt);
        }

        if (!active)
        {
            user.Deactivate();
        }

        user = await sp.GetRequiredService<IUserRepository>().CreateAsync(user, CancellationToken.None);
        Host.Records[user.Email] = record;

        var existing = $"existing-{Guid.NewGuid():N}";
        await sp.GetRequiredService<IRefreshTokenRepository>().CreateAsync(new RefreshToken(
            user.Id, sp.GetRequiredService<IRefreshTokenHasher>().Hash(existing), DateTime.UtcNow.AddDays(7), "127.0.0.1",
            PlatformTenantId, authCopy, "bl521-fix2"), CancellationToken.None);

        var tokens = sp.GetRequiredService<ITokenService>();
        var accessToken = tokenActor == "tenant_user"
            ? tokens.GenerateAccessToken(user, [], [], expiresInMinutes: 60)
            : tokens.GeneratePlatformAccessToken(user.Id, user.Email, user.FirstName, user.LastName, PlatformTenantId, tokenActor, [], [], 60, mustChange);
        return new Seeded(user.Id, accessToken, existing);
    }

    private async Task<RefreshToken?> RefreshRowAsync(string rawToken)
    {
        using var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(PlatformTenantId);
        return await scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>().GetByTokenAsync(rawToken, CancellationToken.None);
    }

    /// <summary>Refresh rows the door minted for this user (anything beyond the one the seed wrote).</summary>
    private async Task<IReadOnlyList<RefreshToken>> NewRefreshRowsAsync(Seeded seeded)
    {
        using var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(PlatformTenantId);
        var existingHash = scope.ServiceProvider.GetRequiredService<IRefreshTokenHasher>().Hash(seeded.ExistingRefreshToken);
        var rows = await _host.Database.GetCollection<RefreshToken>("refreshTokens")
            .Find(Builders<RefreshToken>.Filter.Eq(r => r.UserId, seeded.UserId)).ToListAsync();
        return rows.Where(r => r.Token != existingHash).ToList();
    }

    private async Task<User> UserAsync(Guid userId)
    {
        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(PlatformTenantId);
        return (await sp.GetRequiredService<IUserRepository>().GetByIdAndTenantAsync(userId, PlatformTenantId, CancellationToken.None))!;
    }

    private async Task<bool> PasswordIsStillAsync(Guid userId, string password)
    {
        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var user = await UserAsync(userId);
        return sp.GetRequiredService<IPasswordHasher>().Verify(password, user.PasswordHash);
    }

    private sealed class RecordByEmail : IPlatformAdministratorStatusClient
    {
        private static PlatformAdministratorRecordAnswer For(string email) =>
            Host.Records.TryGetValue(email, out var record) ? record : PlatformAdministratorRecordAnswer.Refused;

        public Task<bool> IsActiveAsync(string email, CancellationToken ct) =>
            Task.FromResult(For(email).Verdict == PlatformAdministratorRecordVerdict.Confirmed);
        public Task MarkLoginAcceptedAsync(string email, CancellationToken ct) => Task.CompletedTask;
        public Task<PlatformAdministratorRecordAnswer> GetActiveActorTypeAsync(string email, CancellationToken ct) => Task.FromResult(For(email));
    }

    private sealed class TenCharactersUpperAndSpecial : ITenantLoginSettingsClient
    {
        public Task<TenantLoginSettingsSnapshot> GetAsync(Guid tenantId, CancellationToken ct) => Task.FromResult(new TenantLoginSettingsSnapshot(
            tenantId, TwoFactorEnabled: false, MfaRequired: false, EmailLoginEnabled: true, PhoneLoginEnabled: false,
            PasswordMinLength: 10, PasswordRequireUppercase: true, PasswordRequireLowercase: true, PasswordRequireDigit: true,
            PasswordRequireSpecialChar: true, PasswordExpirationDays: null, SessionTimeoutMinutes: 60, RefreshTokenLifetimeDays: 7,
            MaxFailedLoginAttempts: 5, LockoutDurationMinutes: 15));
    }
}

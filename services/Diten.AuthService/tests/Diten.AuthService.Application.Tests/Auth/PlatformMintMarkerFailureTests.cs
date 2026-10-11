using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Auth;

/// <summary>
/// BL-521 FIX8 item 1 — "A MINT THAT CANNOT MARK ITS ACCOUNT MINTS NOTHING", MEASURED. FIX7 wrote the record's actor type onto the
/// Auth account at every platform-door mint and said a failing write fails the mint — but no test ever made the write fail (a
/// try/catch around it, CT's Q2, changed nothing that was measured), and a write that matched NO account (the account deleted or
/// moved between the sign-in's read and the write) was ignored: the loaded user was marked in memory and the token was minted.
///
/// <para>Here the production Api runs over the test-owned mongod with ONE change: the user repository the handlers get is wrapped so
/// that, for a chosen account, the marker write (a) THROWS, or (b) first deletes the account and then reaches the REAL repository,
/// whose targeted write then matches nothing. For the platform login, the platform refresh and the platform forced change alike:
/// 500, no platform token in the answer, no refresh row minted, none revoked, the password unchanged — and without the fault the
/// same call succeeds and marks the account (the control).</para>
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class PlatformMintMarkerFailureTests : IClassFixture<PlatformMintMarkerFailureTests.Host>
{
    private static readonly Guid PlatformTenant = PlatformSystemTenant.Id;
    private const string Password = "Measure!Door2026";
    private const string NewPassword = "Fresh!Passw0rd2026";

    private readonly Host _host;

    public PlatformMintMarkerFailureTests(Host host) => _host = host;

    public sealed class Host : AccountKindAcceptance.AuthTestHost
    {
        public static readonly ConcurrentDictionary<string, PlatformAdministratorRecordAnswer> Records = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>What the marker write does for an account (by id): "throws", or "vanishes" (the account is deleted first).</summary>
        public static readonly ConcurrentDictionary<Guid, string> Faults = new();

        protected override void ConfigureTestServices(IServiceCollection services)
        {
            base.ConfigureTestServices(services);
            services.AddScoped<ITenantLoginSettingsClient, Settings>();
            services.AddScoped<ITenantEffectivePermissionResolver, Passthrough>();
            services.AddScoped<IPlatformAdministratorStatusClient, RecordByEmail>();
            // the production repository (decorator over the Mongo one), wrapped: ONLY the marker write can misbehave
            services.AddScoped<IUserRepository>(sp => new FaultingMarkerWrites(
                new PlatformAccountScopedUserRepository(sp.GetRequiredService<UserRepository>(), sp.GetRequiredService<IPlatformAccountVisibility>())));
        }
    }

    [Theory]
    [InlineData("login", "throws", "platform_admin")]
    [InlineData("login", "vanishes", "partner_admin")]
    [InlineData("refresh", "throws", "partner_admin")]
    [InlineData("refresh", "vanishes", "platform_admin")]
    [InlineData("forced-change", "throws", "platform_admin")]
    [InlineData("forced-change", "vanishes", "partner_admin")]
    public async Task A_platform_mint_whose_marker_write_fails_or_matches_nothing_mints_nothing(string entry, string fault, string actorType)
    {
        var account = await SeedAsync(mustChange: entry == "forced-change");
        Host.Records[account.Email] = PlatformAdministratorRecordAnswer.ConfirmedAs(actorType);
        var seededRow = entry == "refresh" ? await AddRefreshRowAsync(account, actorType) : null;
        var token = entry == "login" ? null : await MintPlatformTokenAsync(account, actorType);
        var passwordBefore = (await StoredAsync(account.Id))!.PasswordHash;
        Host.Faults[account.Id] = fault;

        var (status, body) = await CallAsync(entry, account, actorType, token, seededRow);

        Assert.Equal(HttpStatusCode.InternalServerError, status);
        Assert.DoesNotContain("accessToken", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refreshToken", body, StringComparison.OrdinalIgnoreCase);

        // nothing minted, nothing revoked, nothing changed
        var rows = await _host.Database.GetCollection<RefreshToken>("refreshTokens").Find(r => r.UserId == account.Id).ToListAsync();
        if (entry == "refresh")
        {
            var row = Assert.Single(rows); // the one that was there: no new row …
            Assert.Null(row.RevokedAt);    // … and the old one was not rotated away
        }
        else
        {
            Assert.Empty(rows);
        }

        var stored = (await StoredAsync(account.Id))!;
        Assert.Null(stored.PlatformActorType); // the account was not marked, in the store …
        Assert.Equal(passwordBefore, stored.PasswordHash);
        Assert.Equal(fault == "vanishes", stored.IsDeleted); // … and "vanishes" is the account really gone, so the real write matched nothing
    }

    /// <summary>The control for the test above: with no fault the same three calls succeed and the account carries the record's
    /// actor type afterwards.</summary>
    [Theory]
    [InlineData("login", "platform_admin")]
    [InlineData("refresh", "partner_admin")]
    [InlineData("forced-change", "partner_admin")]
    public async Task Without_a_fault_each_platform_mint_succeeds_and_marks_the_account(string entry, string actorType)
    {
        var account = await SeedAsync(mustChange: entry == "forced-change");
        Host.Records[account.Email] = PlatformAdministratorRecordAnswer.ConfirmedAs(actorType);
        var seededRow = entry == "refresh" ? await AddRefreshRowAsync(account, actorType) : null;
        var token = entry == "login" ? null : await MintPlatformTokenAsync(account, actorType);

        var (status, body) = await CallAsync(entry, account, actorType, token, seededRow);

        Assert.True(status == HttpStatusCode.OK, $"{(int)status}: {body}");
        Assert.Contains("accessToken", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(actorType, (await StoredAsync(account.Id))!.PlatformActorType);
    }

    private async Task<(HttpStatusCode Status, string Body)> CallAsync(string entry, Account account, string actorType, string? token, string? rawRefresh)
    {
        using var client = token is null ? _host.Client(tenantHeader: PlatformTenant) : _host.Client(token, PlatformTenant);
        using var response = entry switch
        {
            "login" => await client.PostAsJsonAsync("api/platform-auth/login", new { email = account.Email, password = Password, rememberMe = false }),
            "refresh" => await client.PostAsJsonAsync("api/auth/refresh-token", new { accessToken = token, refreshToken = rawRefresh }),
            "forced-change" => await client.PostAsJsonAsync("api/platform-auth/change-password/forced", new { currentPassword = Password, newPassword = NewPassword, rememberMe = false }),
            _ => throw new ArgumentOutOfRangeException(nameof(entry)),
        };
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    private sealed record Account(Guid Id, string Email);

    /// <summary>An UNMARKED account of the platform system tenant holding the SuperAdmin role — what an older provisioning left.</summary>
    private async Task<Account> SeedAsync(bool mustChange)
    {
        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(PlatformTenant);
        var user = new User($"mark.{Guid.NewGuid():N}@bl521.test", sp.GetRequiredService<IPasswordHasher>().Hash(Password), "Mark", "Probe", PlatformTenant);
        user.ConfirmEmail();
        if (mustChange)
        {
            user.RequirePasswordChange(null);
        }

        user = await sp.GetRequiredService<IUserRepository>().CreateAsync(user, CancellationToken.None);
        var roles = sp.GetRequiredService<IRoleRepository>();
        var role = await roles.GetByNameAndTenantAsync("SuperAdmin", PlatformTenant, CancellationToken.None)
                   ?? await roles.UpsertSystemRoleAsync("SuperAdmin", "SuperAdmin", "Platform administrator role", PlatformTenant, CancellationToken.None);
        await sp.GetRequiredService<IUserRoleRepository>().AssignAsync(new UserRole(user.Id, role.Id, PlatformTenant, "bl521-fix8"), CancellationToken.None);
        return new Account(user.Id, user.Email);
    }

    /// <summary>The account as the collection holds it, deleted or not.</summary>
    private Task<User?> StoredAsync(Guid id) => _host.Database.GetCollection<User>("users").Find(u => u.Id == id).FirstOrDefaultAsync()!;

    private async Task<string> AddRefreshRowAsync(Account account, string actorType)
    {
        var raw = $"refresh-{Guid.NewGuid():N}";
        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(PlatformTenant);
        var hasher = sp.GetRequiredService<IRefreshTokenHasher>();
        var row = new RefreshToken(
            account.Id, hasher.Hash(raw), DateTime.UtcNow.AddDays(7), "127.0.0.1",
            PlatformTenant, actorType, "bl521-fix8");
        // Merge WP-CT-LANE-BL521-MERGE-01 — bound to the account's password as every mint binds it since BL-529 FIX3 (an unbound
        // row is refused as a pre-BL-529 session before any BL-521 rule is reached).
        Diten.AuthService.Application.Features.Auth.Services.SessionPasswordBinding.Bind(row, hasher, (await StoredAsync(account.Id))!);
        await sp.GetRequiredService<IRefreshTokenRepository>().CreateAsync(row, CancellationToken.None);
        return raw;
    }

    private async Task<string> MintPlatformTokenAsync(Account account, string actorType)
    {
        using var scope = _host.Factory.Services.CreateScope();
        var user = await StoredAsync(account.Id);
        return scope.ServiceProvider.GetRequiredService<ITokenService>().GeneratePlatformAccessToken(
            user!.Id, user.Email, user.FirstName, user.LastName, PlatformTenant, actorType, ["SuperAdmin"], [], 60, false);
    }

    private sealed class Settings : ITenantLoginSettingsClient
    {
        public Task<TenantLoginSettingsSnapshot> GetAsync(Guid tenantId, CancellationToken ct) => Task.FromResult(new TenantLoginSettingsSnapshot(
            tenantId, TwoFactorEnabled: false, MfaRequired: false, EmailLoginEnabled: true, PhoneLoginEnabled: false,
            PasswordMinLength: 8, PasswordRequireUppercase: false, PasswordRequireLowercase: false, PasswordRequireDigit: false,
            PasswordRequireSpecialChar: false, PasswordExpirationDays: null, SessionTimeoutMinutes: 60, RefreshTokenLifetimeDays: 7,
            MaxFailedLoginAttempts: 5, LockoutDurationMinutes: 15));
    }

    private sealed class Passthrough : ITenantEffectivePermissionResolver
    {
        public Task<IReadOnlyList<string>> ResolveAsync(Guid tenantId, IEnumerable<string>? rolePermissions, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>(rolePermissions?.ToArray() ?? []);
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

    /// <summary>The production user repository, except for the marker write of an account listed in <see cref="Host.Faults"/>.</summary>
    private sealed class FaultingMarkerWrites(IUserRepository inner) : IUserRepository
    {
        public async Task<bool> SetPlatformActorTypeAsync(Guid userId, Guid tenantId, string actorType, CancellationToken ct)
        {
            if (Host.Faults.TryGetValue(userId, out var fault))
            {
                if (fault == "throws")
                {
                    throw new InvalidOperationException("bl521-fix8: the marker write fails here");
                }

                // "vanishes": the account is deleted between the sign-in's read and this write — the REAL write below matches nothing
                await inner.SoftDeleteAsync(userId, tenantId, ct);
            }

            return await inner.SetPlatformActorTypeAsync(userId, tenantId, actorType, ct);
        }

        public Task<User?> GetByEmailAndTenantAsync(string email, Guid tenantId, CancellationToken ct) => inner.GetByEmailAndTenantAsync(email, tenantId, ct);
        public Task<User?> GetByUserNameAndTenantAsync(string normalizedUserName, Guid tenantId, CancellationToken ct) => inner.GetByUserNameAndTenantAsync(normalizedUserName, tenantId, ct);
        public Task<User?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct) => inner.GetByIdAndTenantAsync(id, tenantId, ct);
        public Task<User?> GetByPasswordResetTokenHashAsync(string tokenHash, CancellationToken ct) => inner.GetByPasswordResetTokenHashAsync(tokenHash, ct);
        public Task<IEnumerable<User>> GetAllByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct) => inner.GetAllByTenantAsync(tenantId, page, pageSize, ct);
        public Task<IReadOnlyList<User>> SearchActiveAsync(Guid tenantId, string? term, int limit, CancellationToken ct) => inner.SearchActiveAsync(tenantId, term, limit, ct);
        public Task<long> GetCountByTenantAsync(Guid tenantId, CancellationToken ct) => inner.GetCountByTenantAsync(tenantId, ct);
        public Task<User> CreateAsync(User user, CancellationToken ct) => inner.CreateAsync(user, ct);
        // Merge WP-CT-LANE-BL521-MERGE-01 — BL-529 FIX4 removed the whole-document UpdateAsync / UpdateForTenantAsync; its targeted writes take their place.
        public Task RecordLoginOutcomeAsync(User user, Guid tenantId, CancellationToken ct) => inner.RecordLoginOutcomeAsync(user, tenantId, ct);
        public object CaptureState(User user) => inner.CaptureState(user);
        public Task<bool> TryWriteChangesAsync(User user, object capturedState, Guid tenantId, UserWriteCondition condition, CancellationToken ct) => inner.TryWriteChangesAsync(user, capturedState, tenantId, condition, ct);
        public Task<bool> SetPasswordResetTokenAsync(Guid userId, Guid tenantId, string tokenHash, DateTime expiresAtUtc, CancellationToken ct) => inner.SetPasswordResetTokenAsync(userId, tenantId, tokenHash, expiresAtUtc, ct);
        public Task<LoginFailureOutcome> RecordLoginFailureAsync(Guid userId, Guid tenantId, int maxFailedAttempts, int lockoutDurationMinutes, CancellationToken ct) => inner.RecordLoginFailureAsync(userId, tenantId, maxFailedAttempts, lockoutDurationMinutes, ct);
        public Task SoftDeleteAsync(Guid id, Guid tenantId, CancellationToken ct) => inner.SoftDeleteAsync(id, tenantId, ct);
        public Task<IReadOnlyCollection<Guid>> VisibleUserIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> userIds, CancellationToken ct) => inner.VisibleUserIdsAsync(tenantId, userIds, ct);
    }
}

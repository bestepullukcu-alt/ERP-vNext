using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Auth.Commands;
using Diten.AuthService.Application.Features.Auth.Handlers.CommandHandlers;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Auth;

/// <summary>
/// BL-521 FIX5 item 13 — A PLATFORM ACCOUNT NEVER GETS A TENANT TOKEN OF THE PLATFORM SYSTEM TENANT. This class began as the
/// measurement of the tenant sign-in door (<c>POST api/tenant-auth/login</c>) asked with the platform tenant's id in
/// <c>X-Tenant-Id</c>; what it measured (real HTTP, real Api, test-owned mongod, the production seeder's SuperAdmin and its
/// full-catalog permissions): the door answered 200 with a <c>tenant_user</c> token carrying 274 permission claims
/// (<c>auth.users.read / update / assign-role</c> among them) that opened the user list and could disable, enable, reset the
/// password of and assign roles to ANOTHER platform administrator — without ever asking Platform's administrator record, which
/// the platform door asks every time (a suspended or partner administrator got in). It is now the guard of the decision (CT:
/// A + D):
///
/// <para>A — the tenant door is closed for that tenant: sign-in (and an MFA verification or a tenant refresh of it) answers the
/// door's own generic 401, names no account, writes no audit row, mints nothing. D — defense in depth where tenant
/// resolution already runs: a <c>tenant_user</c> token whose tenant is the platform system tenant is refused 403 on every
/// non-platform route, so a token that already exists (or any other way of getting one) opens nothing.</para>
///
/// <para>The controls: a tenant user of another tenant signs in, refreshes and lists its own users as before; the platform
/// door still signs in and refreshes, and a platform-door token passes the guard.</para>
///
/// <para>Real HTTP on the production Api. Only Platform's answers the doors need are fixed (login settings, entitlement, the
/// administrator record). Disposable users only — never the seeded admin.</para>
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class TenantDoorPlatformTenantMeasurementTests : IClassFixture<TenantDoorPlatformTenantMeasurementTests.Host>
{
    private static readonly Guid PlatformTenantId = PlatformSystemTenant.Id;
    private const string Password = "Measure!Door2026";
    private static readonly string[] UserAdminPermissions = ["auth.users.read", "auth.users.update", "auth.users.assign-role", "auth.users.create", "auth.users.delete"];

    private readonly Host _host;

    public TenantDoorPlatformTenantMeasurementTests(Host host) => _host = host;

    public sealed class Host : AccountKindAcceptance.AuthTestHost
    {
        /// <summary>What Platform's administrator record answers per address (the platform door's own check).</summary>
        public static readonly ConcurrentDictionary<string, PlatformAdministratorRecordAnswer> Records = new(StringComparer.OrdinalIgnoreCase);

        protected override void ConfigureTestServices(IServiceCollection services)
        {
            base.ConfigureTestServices(services);
            services.AddScoped<ITenantLoginSettingsClient, DefaultSettings>();
            services.AddScoped<ITenantEffectivePermissionResolver, Passthrough>();
            services.AddScoped<IPlatformAdministratorStatusClient, RecordByEmail>();
        }
    }

    // ── A: the tenant door ──────────────────────────────────────────────────────────────────────────────

    /// <summary>The platform account, right password, platform tenant header: the SAME 401 a wrong password gets (and a stranger's
    /// address gets), no token, no refresh row, no audit row.</summary>
    [Fact]
    public async Task A_platform_account_gets_the_generic_401_at_the_tenant_door_and_nothing_is_minted_or_written()
    {
        var account = await SeedAsync(PlatformTenantId, roles: ["SuperAdmin"]);

        var answer = await TenantLoginAsync(account.Email, Password, PlatformTenantId);
        var strangerAnswer = await TenantLoginAsync($"nobody.{Guid.NewGuid():N}@bl521.test", Password, PlatformTenantId);
        var wrongPasswordAnswer = await TenantLoginAsync(account.Email, "Not-The-Password-1!", PlatformTenantId);

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Equal(("Invalid email or password."), Assert.Single(answer.Errors));
        Assert.Equal(strangerAnswer.Status, answer.Status);
        Assert.Equal(strangerAnswer.Errors, answer.Errors);
        Assert.Equal(wrongPasswordAnswer.Status, answer.Status);
        Assert.Equal(wrongPasswordAnswer.Errors, answer.Errors);
        Assert.False(answer.HasToken);
        Assert.Equal(0, await _host.Database.GetCollection<RefreshToken>("refreshTokens").CountDocumentsAsync(r => r.UserId == account.Id));
        Assert.Equal(0, await _host.Database.GetCollection<AuthAuditLog>("authAuditLogs").CountDocumentsAsync(a => a.UserId == account.Id));
    }

    /// <summary>The control: a tenant user of ANOTHER tenant still signs in at the tenant door.</summary>
    [Fact]
    public async Task A_tenant_user_of_another_tenant_still_signs_in_at_the_tenant_door()
    {
        var tenant = Guid.NewGuid();
        var user = await SeedAsync(tenant, roles: []);

        var answer = await TenantLoginAsync(user.Email, Password, tenant);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.True(answer.HasToken);
    }

    /// <summary>The MFA verification of a challenge of the platform tenant (one created before the door was closed) is answered like
    /// a wrong code and mints nothing: nothing past the answer is ever reached (every other dependency of the handler is absent,
    /// so reaching one fails the test).</summary>
    [Fact]
    public async Task An_mfa_challenge_of_the_platform_tenant_is_answered_like_a_wrong_code_and_mints_nothing()
    {
        var challenge = new MfaChallenge(PlatformTenantId, Guid.NewGuid(), "hash", "email", "destination-hash", "code-hash", DateTime.UtcNow.AddMinutes(5), 5, "127.0.0.1", null);
        var handler = new VerifyMfaCommandHandler(
            new ReturnsChallenge(challenge), null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);

        var result = await handler.Handle(new VerifyMfaCommand("challenge", "123456", "127.0.0.1", null), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(401, result.StatusCode);
        Assert.Equal("Invalid verification code.", Assert.Single(result.Errors));
    }

    /// <summary>A TENANT session of the platform tenant (one minted before the door was closed) is not refreshed: 401, nothing minted,
    /// the row is not revoked (the account's platform sessions share its user and tenant). The same request for a tenant user of
    /// another tenant is refreshed as before.</summary>
    [Fact]
    public async Task A_tenant_session_of_the_platform_tenant_is_not_refreshed_and_a_session_of_another_tenant_is()
    {
        var platformUser = await SeedAsync(PlatformTenantId, roles: ["SuperAdmin"]);
        var tenant = Guid.NewGuid();
        var tenantUser = await SeedAsync(tenant, roles: []);

        var refusedAnswer = await TenantRefreshAsync(platformUser, PlatformTenantId);
        var allowedAnswer = await TenantRefreshAsync(tenantUser, tenant);

        Assert.Equal(HttpStatusCode.Unauthorized, refusedAnswer.Response.StatusCode);
        Assert.Contains("Invalid refresh token.", refusedAnswer.Body, StringComparison.Ordinal);
        Assert.Null((await RefreshRowAsync(refusedAnswer.RawRefreshToken))!.RevokedAt);
        Assert.Equal(HttpStatusCode.OK, allowedAnswer.Response.StatusCode);
    }

    // ── D: the guard where tenant resolution runs ───────────────────────────────────────────────────────

    /// <summary>The token the measurement was made with — hand-minted here, with the full set a platform SuperAdmin's tenant token
    /// carried — is refused 403 on every route that reads or changes users and roles; the victim account is left exactly as it was.</summary>
    [Fact]
    public async Task A_tenant_user_token_of_the_platform_tenant_is_refused_403_on_the_user_routes_and_changes_nothing()
    {
        var actor = await SeedAsync(PlatformTenantId, roles: ["SuperAdmin"]);
        var victim = await SeedAsync(PlatformTenantId, roles: []);
        var readOnly = await RoleIdAsync("ReadOnly");
        using var client = _host.Client(await MintTenantTokenAsync(actor, UserAdminPermissions), PlatformTenantId);

        var statuses = new List<(string Call, HttpStatusCode Status)>
        {
            ("GET api/users", (await client.GetAsync("api/users")).StatusCode),
            ($"GET api/users/{{id}}", (await client.GetAsync($"api/users/{victim.Id}")).StatusCode),
            ("POST disable", (await client.PostAsync($"api/users/{victim.Id}/disable", null)).StatusCode),
            ("POST enable", (await client.PostAsync($"api/users/{victim.Id}/enable", null)).StatusCode),
            ("POST reset-password", (await client.PostAsync($"api/users/{victim.Id}/reset-password", null)).StatusCode),
            ("POST roles", (await client.PostAsJsonAsync($"api/users/{victim.Id}/roles", new { roleId = readOnly })).StatusCode),
            ("DELETE roles", (await client.DeleteAsync($"api/users/{victim.Id}/roles/{readOnly}")).StatusCode),
        };

        Assert.All(statuses, s => Assert.True(s.Status == HttpStatusCode.Forbidden, $"{s.Call}: {(int)s.Status}"));
        var after = await UserAsync(victim.Id, PlatformTenantId);
        Assert.True(after!.IsActive);
        Assert.Empty(await RolesAsync(victim.Id, PlatformTenantId));
        Assert.Null(after.PasswordResetTokenHash);
    }

    /// <summary>The control: a tenant user of another tenant, with the same permission claims, lists its own tenant's users.</summary>
    [Fact]
    public async Task A_tenant_user_token_of_another_tenant_is_unaffected_by_the_guard()
    {
        var tenant = Guid.NewGuid();
        var user = await SeedAsync(tenant, roles: []);
        using var client = _host.Client(await MintTenantTokenAsync(user, UserAdminPermissions), tenant);

        var response = await client.GetAsync("api/users");

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    // ── the platform door is untouched ──────────────────────────────────────────────────────────────────

    /// <summary>The platform door still signs the platform account in (its record is active), its session still refreshes, and a
    /// platform-door token passes the guard on a route the guard protects for tenant tokens (it is not a tenant_user token).</summary>
    [Fact]
    public async Task The_platform_door_still_signs_in_and_refreshes_and_its_token_passes_the_guard()
    {
        var account = await SeedAsync(PlatformTenantId, roles: ["SuperAdmin"]);
        Host.Records[account.Email] = PlatformAdministratorRecordAnswer.ConfirmedAs("platform_admin");
        using var anonymous = _host.Client(tenantHeader: PlatformTenantId);

        var login = await anonymous.PostAsJsonAsync("api/platform-auth/login", new { email = account.Email, password = Password, rememberMe = false });
        var loginBody = await login.Content.ReadAsStringAsync();

        Assert.True(login.StatusCode == HttpStatusCode.OK, $"{(int)login.StatusCode}: {loginBody}");
        using var json = JsonDocument.Parse(loginBody);
        var data = json.RootElement.GetProperty("data");
        var accessToken = data.GetProperty("accessToken").GetString()!;
        var refreshToken = data.GetProperty("refreshToken").GetString()!;
        Assert.Equal("platform_admin", new JwtSecurityTokenHandler().ReadJwtToken(accessToken).Claims.Single(c => c.Type == "actor_type").Value);

        using var platformClient = _host.Client(accessToken, PlatformTenantId);
        var list = await platformClient.GetAsync("api/users");
        Assert.DoesNotContain("This token is not accepted here.", await list.Content.ReadAsStringAsync(), StringComparison.Ordinal); // the guard did not refuse a platform-door token

        var refresh = await anonymous.PostAsJsonAsync("api/auth/refresh-token", new { accessToken, refreshToken });
        Assert.True(refresh.StatusCode == HttpStatusCode.OK, $"{(int)refresh.StatusCode}: {await refresh.Content.ReadAsStringAsync()}");
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    private sealed record Answer(HttpStatusCode Status, IReadOnlyList<string> Errors, bool HasToken);

    private async Task<Answer> TenantLoginAsync(string email, string password, Guid tenant)
    {
        using var client = _host.Client(tenantHeader: tenant);
        using var response = await client.PostAsJsonAsync("api/tenant-auth/login", new { email, password, rememberMe = false });
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        var errors = json.RootElement.TryGetProperty("errors", out var e) && e.ValueKind == JsonValueKind.Array
            ? e.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToList()
            : [];
        var hasToken = json.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
                       && data.TryGetProperty("accessToken", out var token) && token.ValueKind == JsonValueKind.String;
        return new Answer(response.StatusCode, errors, hasToken);
    }

    private sealed record RefreshAnswer(HttpResponseMessage Response, string Body, string RawRefreshToken);

    /// <summary>A tenant refresh: a tenant_user token and its refresh row, for the given account.</summary>
    private async Task<RefreshAnswer> TenantRefreshAsync(Account account, Guid tenant)
    {
        var raw = $"refresh-{Guid.NewGuid():N}";
        using (var scope = _host.Factory.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            sp.GetRequiredService<TenantContext>().SetTenant(tenant);
            await sp.GetRequiredService<IRefreshTokenRepository>().CreateAsync(new RefreshToken(
                account.Id, sp.GetRequiredService<IRefreshTokenHasher>().Hash(raw), DateTime.UtcNow.AddDays(7), "127.0.0.1",
                tenant, "tenant_user", "bl521-fix5"), CancellationToken.None);
        }

        var accessToken = await MintTenantTokenAsync(account, []);
        using var client = _host.Client(tenantHeader: tenant);
        var response = await client.PostAsJsonAsync("api/auth/refresh-token", new { accessToken, refreshToken = raw });
        return new RefreshAnswer(response, await response.Content.ReadAsStringAsync(), raw);
    }

    private async Task<RefreshToken?> RefreshRowAsync(string rawToken)
    {
        using var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(PlatformTenantId);
        return await scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>().GetByTokenAsync(rawToken, CancellationToken.None);
    }

    private sealed record Account(Guid Id, string Email, Guid TenantId);

    /// <summary>A tenant_user token of the account's tenant carrying the given permission claims — what the tenant door mints.</summary>
    private async Task<string> MintTenantTokenAsync(Account account, IEnumerable<string> permissions)
    {
        var user = await UserAsync(account.Id, account.TenantId);
        using var scope = _host.Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ITokenService>().GenerateAccessToken(user!, ["SuperAdmin"], permissions.ToArray(), 60);
    }

    private async Task<Account> SeedAsync(Guid tenantId, string[] roles)
    {
        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(tenantId);
        var user = new User($"door.{Guid.NewGuid():N}@bl521.test", sp.GetRequiredService<IPasswordHasher>().Hash(Password), "Door", "Probe", tenantId);
        user.ConfirmEmail();
        user.SetPlatformActorType("platform_admin");
        user = await sp.GetRequiredService<IUserRepository>().CreateAsync(user, CancellationToken.None);
        foreach (var roleName in roles)
        {
            var roleId = await RoleIdAsync(roleName, tenantId);
            await sp.GetRequiredService<IUserRoleRepository>().AssignAsync(new UserRole(user.Id, roleId, tenantId, "bl521-fix5"), CancellationToken.None);
        }

        return new Account(user.Id, user.Email, tenantId);
    }

    /// <summary>A role of the tenant by name — the production seeder's own SuperAdmin for the platform tenant.</summary>
    private async Task<Guid> RoleIdAsync(string name, Guid? tenantId = null)
    {
        var tenant = tenantId ?? PlatformTenantId;
        using var scope = _host.Factory.Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<IRoleRepository>();
        return (await roles.GetByNameAndTenantAsync(name, tenant, CancellationToken.None)
                ?? await roles.UpsertSystemRoleAsync(name, name, "Platform administrator role", tenant, CancellationToken.None)).Id;
    }

    private async Task<User?> UserAsync(Guid id, Guid tenantId)
    {
        using var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(tenantId);
        return await scope.ServiceProvider.GetRequiredService<IUserRepository>().GetByIdAndTenantAsync(id, tenantId, CancellationToken.None);
    }

    private async Task<string[]> RolesAsync(Guid id, Guid tenantId)
    {
        using var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(tenantId);
        return (await scope.ServiceProvider.GetRequiredService<IUserRoleRepository>().GetRolesByUserAsync(id, tenantId, CancellationToken.None)).ToArray();
    }

    private sealed class DefaultSettings : ITenantLoginSettingsClient
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

    private sealed class ReturnsChallenge(MfaChallenge challenge) : IMfaChallengeService
    {
        public Task<MfaChallengeCreated> CreateEmailChallengeAsync(User user, string requestIp, string? userAgent, CancellationToken ct) => throw new NotSupportedException();
        public Task<MfaChallengeCreated> ResendEmailChallengeAsync(string challengeId, string requestIp, string? userAgent, CancellationToken ct) => throw new NotSupportedException();
        public Task<MfaChallenge> VerifyAsync(string challengeId, string code, CancellationToken ct) => Task.FromResult(challenge);
    }
}

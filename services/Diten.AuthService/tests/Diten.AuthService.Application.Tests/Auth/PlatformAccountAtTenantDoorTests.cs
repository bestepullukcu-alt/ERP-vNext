using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Application.Features.Auth.Commands;
using Diten.AuthService.Application.Features.Auth.Handlers.CommandHandlers;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Application.Tests.Users;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Diten.AuthService.Persistence.Repositories;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Auth;

/// <summary>
/// BL-521 FIX6 item 13 — A PLATFORM ACCOUNT, NOT THE PLATFORM TENANT. The platform system tenant (…0001) is ALSO the default
/// business tenant: ordinary users live next to the platform administrators' accounts, and only the latter carry a
/// <c>PlatformActorType</c>. FIX5 measured that the tenant sign-in door minted a platform account a <c>tenant_user</c> token
/// carrying the whole permission set of its platform roles — enough to disable, reset the password of and re-role ANOTHER
/// administrator — and closed the door for the WHOLE tenant (and refused every tenant token of it), which locked out the
/// ordinary users too. This class (it began as that measurement, hence its file name) now pins the narrowed rule:
///
/// <para>A' — a platform ACCOUNT is refused wherever a tenant token is minted (tenant sign-in, MFA verification, tenant
/// refresh, the tenant forced change; registration of its address is the ordinary "already in use"), with the door's own
/// generic answer, before any password is looked at, with no token, no refresh row and no audit row. Ordinary users of the same
/// tenant — and of any other — sign in, refresh and change their password exactly as before.</para>
///
/// <para>D' — a tenant actor (<c>actor_type=tenant_user</c>) never sees or acts on a platform account: list, search, lookup and
/// export leave it out (counters included); get, validate, assert, label, classify, update, delete, disable, enable,
/// reset-password, role assignment and removal, and resending its invitation answer 404 — the very answer for an id that does
/// not exist (not 403: nothing to enumerate) — and the account is left exactly as it was. The rule lives in the user repository
/// and the list reader, so no endpoint carries a copy. A platform-door actor is unaffected.</para>
///
/// <para>Real HTTP on the production Api over a test-owned mongod (the production seeder has run there; the tenant …0001 of
/// this database is the test's own). Only Platform's answers the doors need are fixed. Disposable users only.</para>
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class PlatformAccountAtTenantDoorTests : IClassFixture<PlatformAccountAtTenantDoorTests.Host>
{
    private static readonly Guid BusinessAndPlatformTenant = PlatformSystemTenant.Id;
    private const string Password = "Measure!Door2026";
    private const string NewPassword = "Fresh!Passw0rd2026";

    private static readonly string[] UserAdminPermissions =
    [
        "auth.users.read", "auth.users.update", "auth.users.assign-role", "auth.users.create", "auth.users.delete",
        "auth.users.lookup", "auth.users.lookup-validation", "auth.users.export", ExplicitGrantOnlyPermissions.UsersAccountKindManage
    ];

    private readonly Host _host;

    public PlatformAccountAtTenantDoorTests(Host host) => _host = host;

    public sealed class Host : AccountKindAcceptance.AuthTestHost
    {
        /// <summary>What Platform's administrator record answers per address (the platform door's own check).</summary>
        public static readonly ConcurrentDictionary<string, PlatformAdministratorRecordAnswer> Records = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>FIX7 - tenants whose login settings demand MFA (a test adds its own fresh tenant).</summary>
        public static readonly ConcurrentDictionary<Guid, bool> MfaTenants = new();

        /// <summary>FIX7 - every (address, code) the OTP delivery was asked to mail; nothing is mailed.</summary>
        public static readonly ConcurrentQueue<(string Email, string Code)> SentOtps = new();

        /// <summary>FIX7 - what the handlers on the doors' paths logged (Information and above), with their structured fields.</summary>
        public static readonly ConcurrentQueue<LogLine> LogLines = new();

        protected override void ConfigureTestServices(IServiceCollection services)
        {
            base.ConfigureTestServices(services);
            services.AddScoped<ITenantLoginSettingsClient, DefaultSettings>();
            services.AddScoped<ITenantEffectivePermissionResolver, Passthrough>();
            services.AddScoped<IPlatformAdministratorStatusClient, RecordByEmail>();
            services.AddScoped<IOtpDeliveryService, RecordingOtp>();
            // the MFA challenge hashes with a configured secret; this host's own, random per run, never printed
            services.Configure<Diten.AuthService.Infrastructure.Settings.MfaOptions>(
                o => o.HashSecret = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
            services.AddSingleton<ILogger<LoginCommandHandler>>(new LineSink<LoginCommandHandler>(LogLines));
            services.AddSingleton<ILogger<RefreshTokenCommandHandler>>(new LineSink<RefreshTokenCommandHandler>(LogLines));
            services.AddSingleton<ILogger<VerifyMfaCommandHandler>>(new LineSink<VerifyMfaCommandHandler>(LogLines));
            services.AddSingleton<ILogger<ForcedChangeTenantPasswordCommandHandler>>(new LineSink<ForcedChangeTenantPasswordCommandHandler>(LogLines));
            services.AddSingleton<ILogger<PlatformLoginCommandHandler>>(new LineSink<PlatformLoginCommandHandler>(LogLines));
            services.AddSingleton<ILogger<Diten.AuthService.Api.Controllers.PlatformAuthController>>(new LineSink<Diten.AuthService.Api.Controllers.PlatformAuthController>(LogLines));
        }
    }

    // ── A': the tenant doors ────────────────────────────────────────────────────────────────────────────

    /// <summary>The control that FIX5 broke: an ordinary user of the tenant that also holds the platform accounts signs in.</summary>
    [Fact]
    public async Task An_ordinary_user_of_the_platform_tenant_signs_in_at_the_tenant_door()
    {
        var user = await SeedAsync(BusinessAndPlatformTenant);

        var answer = await TenantLoginAsync(user.Email, Password, BusinessAndPlatformTenant);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.True(answer.HasToken);
    }

    /// <summary>The platform account, right password: the SAME 401 a wrong password and a stranger's address get — no token, no
    /// refresh row, no audit row (the answer is given before the password is looked at). Whatever tenant it sits in.</summary>
    [Theory]
    [InlineData(true, "platform_admin")]  // the tenant that is also the platform tenant
    [InlineData(false, "platform_admin")] // any other tenant: what makes it a platform account is the account, not the tenant
    [InlineData(true, "partner_admin")]   // FIX7: a partner administrator's account is a platform account too
    [InlineData(false, "partner_admin")]
    public async Task A_platform_account_gets_the_generic_401_at_the_tenant_door_and_nothing_is_minted_or_written(bool inThePlatformTenant, string actorType)
    {
        var tenant = inThePlatformTenant ? BusinessAndPlatformTenant : Guid.NewGuid();
        var account = await SeedAsync(tenant, platform: true, roles: inThePlatformTenant ? ["SuperAdmin"] : [], actorType: actorType);
        var ordinary = await SeedAsync(tenant);

        var answer = await TenantLoginAsync(account.Email, Password, tenant);
        var strangerAnswer = await TenantLoginAsync($"nobody.{Guid.NewGuid():N}@bl521.test", Password, tenant);
        var wrongPasswordAnswer = await TenantLoginAsync(ordinary.Email, "Not-The-Password-1!", tenant);

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Equal("Invalid email or password.", Assert.Single(answer.Errors));
        Assert.Equal(strangerAnswer.Status, answer.Status);
        Assert.Equal(strangerAnswer.Errors, answer.Errors);
        Assert.Equal(wrongPasswordAnswer.Status, answer.Status);
        Assert.Equal(wrongPasswordAnswer.Errors, answer.Errors);
        Assert.False(answer.HasToken);
        Assert.Equal(0, await _host.Database.GetCollection<RefreshToken>("refreshTokens").CountDocumentsAsync(r => r.UserId == account.Id));
        Assert.Equal(0, await _host.Database.GetCollection<AuthAuditLog>("authAuditLogs").CountDocumentsAsync(a => a.UserId == account.Id));
    }

    /// <summary>A user of ANOTHER tenant signs in as before.</summary>
    [Fact]
    public async Task A_user_of_another_tenant_still_signs_in_at_the_tenant_door()
    {
        var tenant = Guid.NewGuid();
        var user = await SeedAsync(tenant);

        var answer = await TenantLoginAsync(user.Email, Password, tenant);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.True(answer.HasToken);
    }

    /// <summary>MFA: a challenge of a platform account (created before the doors refused it) is answered like a wrong code and
    /// mints nothing; the challenge of an ordinary user of the same tenant is verified and a token is minted.</summary>
    [Theory]
    [InlineData("platform_admin")]
    [InlineData("partner_admin")]
    public async Task Mfa_verification_refuses_a_platform_account_but_not_an_ordinary_user_of_the_same_tenant(string actorType)
    {
        var platform = NewUser(platform: true, actorType);
        var ordinary = NewUser(platform: false);
        var users = new InMemoryUserRepository([platform, ordinary]);

        var refused = new MfaWorld(users, platform);
        var refusedResult = await refused.VerifyAsync();
        var allowed = new MfaWorld(users, ordinary);
        var allowedResult = await allowed.VerifyAsync();

        Assert.False(refusedResult.IsSuccessful);
        Assert.Equal(401, refusedResult.StatusCode);
        Assert.Equal("Invalid verification code.", Assert.Single(refusedResult.Errors));
        Assert.False(refused.MintedAnything);
        Assert.True(allowedResult.IsSuccessful, string.Join("; ", allowedResult.Errors));
        Assert.True(allowed.MintedAnything);
    }

    /// <summary>A TENANT session of a platform account (minted before the doors refused it) is not refreshed: 401, the answer of
    /// an invalid refresh token, nothing minted, the row not revoked (the account's platform sessions share its user and tenant).
    /// The same request for an ordinary user of that tenant is refreshed as before; so is the account's own PLATFORM session.</summary>
    [Theory]
    [InlineData("platform_admin")]
    [InlineData("partner_admin")]
    public async Task A_tenant_session_of_a_platform_account_is_not_refreshed_an_ordinary_one_and_the_platform_one_are(string actorType)
    {
        var platformAccount = await SeedAsync(BusinessAndPlatformTenant, platform: true, roles: ["SuperAdmin"], actorType: actorType);
        var ordinary = await SeedAsync(BusinessAndPlatformTenant);
        Host.Records[platformAccount.Email] = PlatformAdministratorRecordAnswer.ConfirmedAs(actorType);

        var refused = await RefreshAsync(platformAccount, BusinessAndPlatformTenant, "tenant_user");
        var allowed = await RefreshAsync(ordinary, BusinessAndPlatformTenant, "tenant_user");
        var platformSession = await RefreshAsync(platformAccount, BusinessAndPlatformTenant, actorType);

        Assert.Equal(HttpStatusCode.Unauthorized, refused.Response.StatusCode);
        Assert.Contains("Invalid refresh token.", refused.Body, StringComparison.Ordinal);
        Assert.Null((await RefreshRowAsync(refused.RawRefreshToken))!.RevokedAt);
        Assert.Equal(HttpStatusCode.OK, allowed.Response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, platformSession.Response.StatusCode);
    }

    /// <summary>Registration mints a token only for the account it CREATES. An address that belongs to a platform account is
    /// "already in use" — the same 409 an ordinary user's address gets — and creates, mints and writes nothing.</summary>
    [Theory]
    [InlineData("platform_admin")]
    [InlineData("partner_admin")]
    public async Task Registering_the_address_of_a_platform_account_is_the_ordinary_409_and_mints_nothing(string actorType)
    {
        var platformAccount = await SeedAsync(BusinessAndPlatformTenant, platform: true, actorType: actorType);
        var ordinary = await SeedAsync(BusinessAndPlatformTenant);
        var before = await _host.Database.GetCollection<User>("users").CountDocumentsAsync(u => u.TenantId == BusinessAndPlatformTenant);

        var platformAnswer = await RegisterAsync(platformAccount.Email);
        var ordinaryAnswer = await RegisterAsync(ordinary.Email);

        Assert.Equal(HttpStatusCode.Conflict, platformAnswer.Status);
        Assert.Equal(ordinaryAnswer.Status, platformAnswer.Status);
        Assert.Equal(ordinaryAnswer.Errors, platformAnswer.Errors);
        Assert.False(platformAnswer.HasToken);
        Assert.Equal(before, await _host.Database.GetCollection<User>("users").CountDocumentsAsync(u => u.TenantId == BusinessAndPlatformTenant));
        Assert.Equal(0, await _host.Database.GetCollection<RefreshToken>("refreshTokens").CountDocumentsAsync(r => r.UserId == platformAccount.Id));
    }

    /// <summary>The tenant forced change mints a token and a refresh row: an ordinary user of the tenant changes its password; a
    /// platform account — over HTTP — gets the door's 401 and nothing changes.</summary>
    [Theory]
    [InlineData("platform_admin")]
    [InlineData("partner_admin")]
    public async Task The_tenant_forced_change_refuses_a_platform_account_but_not_an_ordinary_user(string actorType)
    {
        var platformAccount = await SeedAsync(BusinessAndPlatformTenant, platform: true, mustChange: true, actorType: actorType);
        var ordinary = await SeedAsync(BusinessAndPlatformTenant, mustChange: true);

        using var platformClient = _host.Client(await MintTenantTokenAsync(platformAccount, []), BusinessAndPlatformTenant);
        var refused = await platformClient.PostAsJsonAsync("api/tenant-auth/change-password/forced", new { currentPassword = Password, newPassword = NewPassword, rememberMe = false });
        using var ordinaryClient = _host.Client(await MintTenantTokenAsync(ordinary, []), BusinessAndPlatformTenant);
        var allowed = await ordinaryClient.PostAsJsonAsync("api/tenant-auth/change-password/forced", new { currentPassword = Password, newPassword = NewPassword, rememberMe = false });

        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        Assert.True(await PasswordIsAsync(platformAccount, Password));
        Assert.Equal(0, await _host.Database.GetCollection<RefreshToken>("refreshTokens").CountDocumentsAsync(r => r.UserId == platformAccount.Id));
        Assert.True(allowed.StatusCode == HttpStatusCode.OK, $"{(int)allowed.StatusCode}: {await allowed.Content.ReadAsStringAsync()}");
        Assert.True(await PasswordIsAsync(ordinary, NewPassword));
    }

    /// <summary>The forced change's own check, with no request in play (so no repository hiding): a platform account is refused with
    /// the door's 401 before its password is looked at; nothing is changed, nothing is minted.</summary>
    [Theory]
    [InlineData("platform_admin")]
    [InlineData("partner_admin")]
    public async Task The_forced_change_handler_itself_refuses_a_platform_account_before_looking_at_its_password(string actorType)
    {
        var platform = NewUser(platform: true, actorType);
        var handler = new ForcedChangeTenantPasswordCommandHandler(
            new InMemoryUserRepository([platform]), null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!,
            NullLogger<ForcedChangeTenantPasswordCommandHandler>.Instance);

        var result = await handler.Handle(
            new ForcedChangeTenantPasswordCommand(platform.Id, BusinessAndPlatformTenant, Password, NewPassword, "127.0.0.1", null), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(401, result.StatusCode);
        Assert.Equal("Unauthorized.", Assert.Single(result.Errors));
    }

    // ── D': target protection ───────────────────────────────────────────────────────────────────────────

    /// <summary>List (both contracts), search lookup, export and the header counters leave the platform accounts out — in the
    /// tenant that also is the platform tenant, and in any other. The ordinary users are all there.</summary>
    [Theory]
    [InlineData(true, "platform_admin")]
    [InlineData(false, "platform_admin")]
    [InlineData(true, "partner_admin")]
    [InlineData(false, "partner_admin")]
    public async Task A_tenant_actor_does_not_see_platform_accounts_in_the_list_the_search_or_the_export(bool inThePlatformTenant, string actorType)
    {
        var tenant = inThePlatformTenant ? BusinessAndPlatformTenant : Guid.NewGuid();
        var tag = $"Q{Guid.NewGuid():N}"[..12]; // a first name of its own: the search finds these three and no other test's users
        var admin = await SeedAsync(tenant, firstName: tag);
        var colleague = await SeedAsync(tenant, firstName: tag);
        var platformAccount = await SeedAsync(tenant, platform: true, firstName: tag, actorType: actorType);
        using var client = _host.Client(await MintTenantTokenAsync(admin, UserAdminPermissions), tenant);

        var list = await client.GetAsync("api/users?start=0&length=200");
        var legacy = await client.GetAsync("api/users?page=1&pageSize=500");
        var lookup = await client.GetAsync($"api/users/lookup?search={tag}&limit=50");
        var export = await client.GetAsync("api/users/export?format=csv");

        var listBody = await list.Content.ReadAsStringAsync();
        Assert.True(list.StatusCode == HttpStatusCode.OK, $"{(int)list.StatusCode}: {listBody}");
        using var listJson = JsonDocument.Parse(listBody);
        var listed = listJson.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().Select(i => i.GetProperty("email").GetString()).ToList();
        Assert.Contains(admin.Email, listed);
        Assert.Contains(colleague.Email, listed);
        Assert.DoesNotContain(platformAccount.Email, listed);
        var data = listJson.RootElement.GetProperty("data");
        Assert.Equal(listed.Count, data.GetProperty("filteredTotal").GetInt64()); // the counter does not count what the rows hide
        Assert.Equal(listed.Count, data.GetProperty("summary").GetProperty("total").GetInt64());

        var legacyBody = await legacy.Content.ReadAsStringAsync();
        Assert.True(legacy.StatusCode == HttpStatusCode.OK, $"{(int)legacy.StatusCode}: {legacyBody}");
        Assert.DoesNotContain(platformAccount.Email, legacyBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(colleague.Email, legacyBody, StringComparison.OrdinalIgnoreCase);

        var lookupBody = await lookup.Content.ReadAsStringAsync();
        Assert.True(lookup.StatusCode == HttpStatusCode.OK, $"{(int)lookup.StatusCode}: {lookupBody}");
        Assert.DoesNotContain(platformAccount.Id.ToString(), lookupBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(colleague.Id.ToString(), lookupBody, StringComparison.OrdinalIgnoreCase);

        var exportBody = await export.Content.ReadAsStringAsync();
        Assert.True(export.StatusCode == HttpStatusCode.OK, $"{(int)export.StatusCode}: {exportBody}");
        Assert.DoesNotContain(platformAccount.Email, exportBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(colleague.Email, exportBody, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Every route that reads or changes ONE user answers a platform account exactly as it answers an id that does not
    /// exist — 404, the same body — and the account is left exactly as it was: active, same roles, same password, no reset token,
    /// not deleted, still a platform account.</summary>
    [Theory]
    [InlineData("platform_admin")]
    [InlineData("partner_admin")]
    public async Task A_tenant_actor_gets_the_answer_of_an_absent_user_for_a_platform_account_on_every_user_route_and_changes_nothing(string actorType)
    {
        var admin = await SeedAsync(BusinessAndPlatformTenant);
        var platformAccount = await SeedAsync(BusinessAndPlatformTenant, platform: true, roles: ["SuperAdmin"], actorType: actorType);
        var readOnly = await RoleIdAsync("ReadOnly");
        var before = await UserAsync(platformAccount.Id, BusinessAndPlatformTenant);
        var rolesBefore = await RolesAsync(platformAccount.Id, BusinessAndPlatformTenant);
        using var client = _host.Client(await MintTenantTokenAsync(admin, UserAdminPermissions), BusinessAndPlatformTenant);

        var routes = new (string Name, HttpMethod Method, string Template, object? Body)[]
        {
            ("GET user", HttpMethod.Get, "api/users/{id}", null),
            ("GET lookup-validation", HttpMethod.Get, "api/users/{id}/lookup-validation", null),
            ("GET account-assertion", HttpMethod.Get, "api/users/{id}/account-assertion", null),
            ("GET display-label", HttpMethod.Get, "api/users/{id}/display-label", null),
            ("POST account-kind", HttpMethod.Post, "api/users/{id}/account-kind", new { kind = "Human" }),
            ("PUT user", HttpMethod.Put, "api/users/{id}", new { firstName = "Changed", lastName = "Name", isActive = true }),
            ("POST disable", HttpMethod.Post, "api/users/{id}/disable", null),
            ("POST enable", HttpMethod.Post, "api/users/{id}/enable", null),
            ("POST reset-password", HttpMethod.Post, "api/users/{id}/reset-password", null),
            ("POST resend-invite", HttpMethod.Post, "api/users/resend-invite/{id}", null),
            ("POST roles", HttpMethod.Post, "api/users/{id}/roles", new { roleId = readOnly }),
            ("DELETE roles", HttpMethod.Delete, "api/users/{id}/roles/" + readOnly, null),
            ("DELETE user", HttpMethod.Delete, "api/users/{id}", null),
        };

        foreach (var route in routes)
        {
            var absent = await SendAsync(client, route.Method, route.Template.Replace("{id}", Guid.NewGuid().ToString()), route.Body);
            var platform = await SendAsync(client, route.Method, route.Template.Replace("{id}", platformAccount.Id.ToString()), route.Body);

            Assert.True(platform.Status == HttpStatusCode.NotFound, $"{route.Name}: {(int)platform.Status} {platform.Body}");
            Assert.True(absent.Shape == platform.Shape, $"{route.Name}: absent id answers [{absent.Shape}], the platform account answers [{platform.Shape}]");
        }

        var after = await UserAsync(platformAccount.Id, BusinessAndPlatformTenant);
        Assert.True(after!.IsActive);
        Assert.False(after.IsDeleted);
        Assert.Equal(before!.PasswordHash, after.PasswordHash);
        Assert.Equal(before.FirstName, after.FirstName);
        Assert.Null(after.PasswordResetTokenHash);
        Assert.Equal(actorType, after.PlatformActorType);
        Assert.Equal(rolesBefore, await RolesAsync(platformAccount.Id, BusinessAndPlatformTenant));
    }

    /// <summary>The control: the same token manages an ORDINARY user of the same tenant — read, update, disable, enable,
    /// reset-password, role assignment and removal all succeed.</summary>
    [Fact]
    public async Task A_tenant_actor_with_the_user_permissions_still_manages_an_ordinary_user_of_the_platform_tenant()
    {
        var admin = await SeedAsync(BusinessAndPlatformTenant);
        var colleague = await SeedAsync(BusinessAndPlatformTenant);
        var readOnly = await RoleIdAsync("ReadOnly");
        using var client = _host.Client(await MintTenantTokenAsync(admin, UserAdminPermissions), BusinessAndPlatformTenant);

        var statuses = new List<(string Call, HttpStatusCode Status)>
        {
            ("GET user", (await client.GetAsync($"api/users/{colleague.Id}")).StatusCode),
            ("GET account-assertion", (await client.GetAsync($"api/users/{colleague.Id}/account-assertion")).StatusCode),
            ("PUT user", (await client.PutAsJsonAsync($"api/users/{colleague.Id}", new { firstName = "Changed", lastName = "Name", isActive = true })).StatusCode),
            ("POST disable", (await client.PostAsync($"api/users/{colleague.Id}/disable", null)).StatusCode),
            ("POST enable", (await client.PostAsync($"api/users/{colleague.Id}/enable", null)).StatusCode),
            ("POST reset-password", (await client.PostAsync($"api/users/{colleague.Id}/reset-password", null)).StatusCode),
            ("POST roles", (await client.PostAsJsonAsync($"api/users/{colleague.Id}/roles", new { roleId = readOnly })).StatusCode),
            ("DELETE roles", (await client.DeleteAsync($"api/users/{colleague.Id}/roles/{readOnly}")).StatusCode),
        };

        Assert.All(statuses, s => Assert.True((int)s.Status is >= 200 and < 300, $"{s.Call}: {(int)s.Status}"));
    }

    /// <summary>A user of ANOTHER tenant is managed by its tenant's administrator as before.</summary>
    [Fact]
    public async Task Another_tenants_administrator_manages_its_own_users_as_before()
    {
        var tenant = Guid.NewGuid();
        var admin = await SeedAsync(tenant);
        var colleague = await SeedAsync(tenant);
        using var client = _host.Client(await MintTenantTokenAsync(admin, UserAdminPermissions), tenant);

        var get = await client.GetAsync($"api/users/{colleague.Id}");
        var disable = await client.PostAsync($"api/users/{colleague.Id}/disable", null);

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, disable.StatusCode);
    }

    /// <summary>A platform-DOOR actor is not a tenant actor: it still sees the platform accounts in the list and reads one.</summary>
    [Theory]
    [InlineData("platform_admin")]
    [InlineData("partner_admin")]
    public async Task A_platform_door_actor_still_sees_and_reads_platform_accounts(string actorType)
    {
        var actor = await SeedAsync(BusinessAndPlatformTenant, platform: true, roles: ["SuperAdmin"], actorType: actorType);
        var other = await SeedAsync(BusinessAndPlatformTenant, platform: true, actorType: actorType);
        var user = await UserAsync(actor.Id, BusinessAndPlatformTenant);
        string token;
        using (var scope = _host.Factory.Services.CreateScope())
        {
            token = scope.ServiceProvider.GetRequiredService<ITokenService>().GeneratePlatformAccessToken(
                user!.Id, user.Email, user.FirstName, user.LastName, BusinessAndPlatformTenant, actorType, ["SuperAdmin"],
                ["auth.users.read"], 60, false);
        }

        using var client = _host.Client(token, BusinessAndPlatformTenant);
        var list = await client.GetAsync("api/users?start=0&length=200");
        var get = await client.GetAsync($"api/users/{other.Id}");

        var listBody = await list.Content.ReadAsStringAsync();
        Assert.True(list.StatusCode == HttpStatusCode.OK, $"{(int)list.StatusCode}: {listBody}");
        Assert.Contains(other.Email, listBody, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }

    /// <summary>The platform door is untouched: the platform account signs in there (its record is active) and its session refreshes.</summary>
    [Theory]
    [InlineData("platform_admin")]
    [InlineData("partner_admin")]
    public async Task The_platform_door_still_signs_in_and_refreshes(string actorType)
    {
        var account = await SeedAsync(BusinessAndPlatformTenant, platform: true, roles: ["SuperAdmin"], actorType: actorType);
        Host.Records[account.Email] = PlatformAdministratorRecordAnswer.ConfirmedAs(actorType);
        using var anonymous = _host.Client(tenantHeader: BusinessAndPlatformTenant);

        var login = await anonymous.PostAsJsonAsync("api/platform-auth/login", new { email = account.Email, password = Password, rememberMe = false });
        var loginBody = await login.Content.ReadAsStringAsync();

        Assert.True(login.StatusCode == HttpStatusCode.OK, $"{(int)login.StatusCode}: {loginBody}");
        using var json = JsonDocument.Parse(loginBody);
        var data = json.RootElement.GetProperty("data");
        var accessToken = data.GetProperty("accessToken").GetString()!;
        var refreshToken = data.GetProperty("refreshToken").GetString()!;
        Assert.Equal(actorType, new JwtSecurityTokenHandler().ReadJwtToken(accessToken).Claims.Single(c => c.Type == "actor_type").Value);

        var refresh = await anonymous.PostAsJsonAsync("api/auth/refresh-token", new { accessToken, refreshToken });
        Assert.True(refresh.StatusCode == HttpStatusCode.OK, $"{(int)refresh.StatusCode}: {await refresh.Content.ReadAsStringAsync()}");
    }

    // ═══ BL-521 FIX7 ═══════════════════════════════════════════════════════════════════════════════════════

    // ── item 1: the discriminator follows the record at every platform-door mint ─────────────────────────

    /// <summary>The hole: an account Platform's record knows as a platform administrator but whose copy on the Auth account is
    /// missing (or says the other type) is an ORDINARY user at the tenant door and in every tenant actor's user list. One platform
    /// login writes the record's actor type onto the account — targeted, and not undone by the sign-in's own replace of the document
    /// — so from then on the tenant door refuses it and tenant actors cannot see it.</summary>
    [Theory]
    [InlineData(null, "platform_admin")]
    [InlineData(null, "partner_admin")]
    [InlineData("platform_admin", "partner_admin")]
    [InlineData("partner_admin", "platform_admin")]
    public async Task A_platform_login_writes_the_records_actor_type_onto_the_account_and_the_tenant_door_then_refuses_it(string? stored, string recordType)
    {
        var tag = $"H{Guid.NewGuid():N}"[..12];
        var account = await SeedAsync(BusinessAndPlatformTenant, platform: stored is not null, roles: ["SuperAdmin"], firstName: tag, actorType: stored ?? "platform_admin");
        var admin = await SeedAsync(BusinessAndPlatformTenant);
        Host.Records[account.Email] = PlatformAdministratorRecordAnswer.ConfirmedAs(recordType);
        using var adminClient = _host.Client(await MintTenantTokenAsync(admin, UserAdminPermissions), BusinessAndPlatformTenant);
        Assert.Equal(stored, (await UserAsync(account.Id, BusinessAndPlatformTenant))!.PlatformActorType);

        if (stored is null)
        {
            // the premise — what is closed here: with no copy the tenant door signs the account in and the tenant actor lists it
            Assert.Equal(HttpStatusCode.OK, (await TenantLoginAsync(account.Email, Password, BusinessAndPlatformTenant)).Status);
            Assert.Contains(account.Id, await LookupIdsAsync(adminClient, tag, 50));
        }

        using var anonymous = _host.Client(tenantHeader: BusinessAndPlatformTenant);
        var login = await anonymous.PostAsJsonAsync("api/platform-auth/login", new { email = account.Email, password = Password, rememberMe = false });
        Assert.True(login.StatusCode == HttpStatusCode.OK, $"{(int)login.StatusCode}: {await login.Content.ReadAsStringAsync()}");

        var healed = (await UserAsync(account.Id, BusinessAndPlatformTenant))!;
        Assert.Equal(recordType, healed.PlatformActorType);
        Assert.NotNull(healed.LastLoginAt); // the sign-in's own replace of the document went through, and kept the healed value

        var refused = await TenantLoginAsync(account.Email, Password, BusinessAndPlatformTenant);
        Assert.Equal(HttpStatusCode.Unauthorized, refused.Status);
        Assert.Equal("Invalid email or password.", Assert.Single(refused.Errors));
        Assert.False(refused.HasToken);
        Assert.DoesNotContain(account.Id, await LookupIdsAsync(adminClient, tag, 50));
        Assert.Equal(HttpStatusCode.NotFound, (await adminClient.GetAsync($"api/users/{account.Id}")).StatusCode);
    }

    /// <summary>The same for the platform REFRESH: a platform session of an account with no copy refreshes, the copy is written, and
    /// the tenant door refuses the account afterwards.</summary>
    [Theory]
    [InlineData("platform_admin")]
    [InlineData("partner_admin")]
    public async Task A_platform_refresh_writes_the_records_actor_type_onto_an_account_that_has_none(string recordType)
    {
        var account = await SeedAsync(BusinessAndPlatformTenant, roles: ["SuperAdmin"]);
        Host.Records[account.Email] = PlatformAdministratorRecordAnswer.ConfirmedAs(recordType);
        Assert.Null((await UserAsync(account.Id, BusinessAndPlatformTenant))!.PlatformActorType);

        var refreshed = await RefreshAsync(account, BusinessAndPlatformTenant, recordType);

        Assert.True(refreshed.Response.StatusCode == HttpStatusCode.OK, $"{(int)refreshed.Response.StatusCode}: {refreshed.Body}");
        Assert.Equal(recordType, (await UserAsync(account.Id, BusinessAndPlatformTenant))!.PlatformActorType);
        Assert.Equal(HttpStatusCode.Unauthorized, (await TenantLoginAsync(account.Email, Password, BusinessAndPlatformTenant)).Status);
    }

    /// <summary>The same for the platform FORCED CHANGE, which replaces the whole document with the new password: the healed copy is in
    /// the replaced document, so the new password does not undo it.</summary>
    [Theory]
    [InlineData("platform_admin")]
    [InlineData("partner_admin")]
    public async Task A_platform_forced_change_writes_the_records_actor_type_and_its_own_replace_does_not_undo_it(string recordType)
    {
        var account = await SeedAsync(BusinessAndPlatformTenant, roles: ["SuperAdmin"], mustChange: true);
        Host.Records[account.Email] = PlatformAdministratorRecordAnswer.ConfirmedAs(recordType);
        Assert.Null((await UserAsync(account.Id, BusinessAndPlatformTenant))!.PlatformActorType);

        using var client = _host.Client(await MintPlatformTokenAsync(account, recordType), BusinessAndPlatformTenant);
        var response = await client.PostAsJsonAsync("api/platform-auth/change-password/forced", new { currentPassword = Password, newPassword = NewPassword, rememberMe = false });

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        Assert.True(await PasswordIsAsync(account, NewPassword)); // the replace happened
        Assert.Equal(recordType, (await UserAsync(account.Id, BusinessAndPlatformTenant))!.PlatformActorType); // and kept the healed copy
        Assert.Equal(HttpStatusCode.Unauthorized, (await TenantLoginAsync(account.Email, NewPassword, BusinessAndPlatformTenant)).Status);
    }

    /// <summary>"When missing or different": an account whose copy already matches the record is not touched (no correction is
    /// logged), one that differs is corrected exactly once — and the correction names no e-mail.</summary>
    [Fact]
    public async Task The_correction_is_made_only_when_the_copy_is_missing_or_different_and_is_logged_once()
    {
        var matching = await SeedAsync(BusinessAndPlatformTenant, platform: true, roles: ["SuperAdmin"]);
        var different = await SeedAsync(BusinessAndPlatformTenant, platform: true, roles: ["SuperAdmin"]);
        Host.Records[matching.Email] = PlatformAdministratorRecordAnswer.ConfirmedAs("platform_admin");
        Host.Records[different.Email] = PlatformAdministratorRecordAnswer.ConfirmedAs("partner_admin");
        using var anonymous = _host.Client(tenantHeader: BusinessAndPlatformTenant);

        foreach (var account in new[] { matching, different })
        {
            var login = await anonymous.PostAsJsonAsync("api/platform-auth/login", new { email = account.Email, password = Password, rememberMe = false });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        }

        bool Corrected(Account account) => Host.LogLines.Any(l =>
            l.Fields.GetValueOrDefault("EventCode") == PlatformAccountDiscriminator.HealedEventCode && l.Fields.GetValueOrDefault("UserId") == account.Id.ToString());
        Assert.False(Corrected(matching));
        Assert.Single(Host.LogLines, l => l.Fields.GetValueOrDefault("EventCode") == PlatformAccountDiscriminator.HealedEventCode && l.Fields.GetValueOrDefault("UserId") == different.Id.ToString());
        Assert.DoesNotContain(Host.LogLines.Where(l => l.Fields.GetValueOrDefault("EventCode") == PlatformAccountDiscriminator.HealedEventCode),
            l => l.Message.Contains(different.Email, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("platform_admin", (await UserAsync(matching.Id, BusinessAndPlatformTenant))!.PlatformActorType);
        Assert.Equal("partner_admin", (await UserAsync(different.Id, BusinessAndPlatformTenant))!.PlatformActorType);
    }

    /// <summary>The helper itself: only an account of the platform system tenant is ever marked, and the in-memory user is marked
    /// with the stored one (a later replace of the loaded object must not undo the write).</summary>
    [Theory]
    [InlineData(true, null, "platform_admin", "platform_admin")]
    [InlineData(true, "platform_admin", "partner_admin", "partner_admin")]
    [InlineData(true, "partner_admin", "partner_admin", "partner_admin")]
    [InlineData(false, null, "platform_admin", null)]
    public async Task The_discriminator_helper_marks_only_platform_tenant_accounts_and_the_loaded_user_too(bool inThePlatformTenant, string? stored, string recordType, string? expected)
    {
        var tenant = inThePlatformTenant ? BusinessAndPlatformTenant : Guid.NewGuid();
        var user = new User($"helper.{Guid.NewGuid():N}@bl521.test", "hash:x", "Door", "Probe", tenant);
        if (stored is not null)
        {
            user.SetPlatformActorType(stored);
        }

        var store = new InMemoryUserRepository([user]);
        await PlatformAccountDiscriminator.EnsureAsync(store, user, recordType, NullLogger.Instance, CancellationToken.None);

        Assert.Equal(expected, user.PlatformActorType);
        Assert.Equal(expected, (await store.GetByIdAndTenantAsync(user.Id, tenant, CancellationToken.None))!.PlatformActorType);
    }

    // ── item 2: the lookup's rows are counted AFTER the platform accounts are out ───────────────────────

    /// <summary>A lookup with a small limit whose first rows (by last name, first name) are platform accounts still answers the
    /// ordinary matches: the limit counts only what the tenant actor may see. (Dropping them in memory after Mongo's limit
    /// answered nothing for limit=1.)</summary>
    [Theory]
    [InlineData("platform_admin")]
    [InlineData("partner_admin")]
    public async Task A_lookup_with_a_small_limit_still_answers_the_ordinary_match_behind_platform_accounts(string actorType)
    {
        var tenant = Guid.NewGuid();
        var tag = $"L{Guid.NewGuid():N}"[..12];
        var admin = await SeedAsync(tenant);
        for (var i = 1; i <= 3; i++)
        {
            await SeedAsync(tenant, platform: true, firstName: $"{tag}A{i}", actorType: actorType); // sort first
        }

        var first = await SeedAsync(tenant, firstName: $"{tag}B1");
        var second = await SeedAsync(tenant, firstName: $"{tag}B2");
        using var client = _host.Client(await MintTenantTokenAsync(admin, UserAdminPermissions), tenant);

        Assert.Equal([first.Id], await LookupIdsAsync(client, tag, 1));
        Assert.Equal([first.Id, second.Id], await LookupIdsAsync(client, tag, 2));
        Assert.Equal([first.Id, second.Id], await LookupIdsAsync(client, tag, 5));
    }

    /// <summary>limit=20 returns 20 when 20 ordinary matches exist, however many platform accounts sort ahead of them.</summary>
    [Theory]
    [InlineData("platform_admin")]
    [InlineData("partner_admin")]
    public async Task A_lookup_limit_of_twenty_returns_twenty_when_twenty_ordinary_matches_exist(string actorType)
    {
        var tenant = Guid.NewGuid();
        var tag = $"M{Guid.NewGuid():N}"[..12];
        var admin = await SeedAsync(tenant);
        for (var i = 1; i <= 3; i++)
        {
            await SeedAsync(tenant, platform: true, firstName: $"{tag}A{i}", actorType: actorType);
        }

        var ordinary = new List<Guid>();
        for (var i = 1; i <= 20; i++)
        {
            ordinary.Add((await SeedAsync(tenant, firstName: $"{tag}B{i:00}")).Id);
        }

        using var client = _host.Client(await MintTenantTokenAsync(admin, UserAdminPermissions), tenant);
        var found = await LookupIdsAsync(client, tag, 20);

        Assert.Equal(20, found.Count);
        Assert.Equal(ordinary, found);
    }

    /// <summary>The page of the repository the internal sweeps use counts only what the caller may see: three platform accounts
    /// that come first and three ordinary users, page size 2 → pages of 2, 1 and 0 ordinary users; a caller that is not hidden
    /// from anything gets all six.</summary>
    [Theory]
    [InlineData("platform_admin")]
    [InlineData("partner_admin")]
    public async Task The_repository_page_counts_only_what_the_caller_may_see(string actorType)
    {
        var tenant = Guid.NewGuid();
        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(tenant);
        var store = sp.GetRequiredService<IUserRepository>(); // outside a request: nothing is hidden, so it can seed
        // e-mails chosen so that a scan in insertion order and a scan in e-mail order both put the platform accounts first
        for (var i = 1; i <= 3; i++)
        {
            await store.CreateAsync(PlatformMarked(new User($"a{i}.{Guid.NewGuid():N}@bl521.test", "hash:x", "Page", "Platform", tenant)), CancellationToken.None);
        }

        for (var i = 1; i <= 3; i++)
        {
            await store.CreateAsync(new User($"z{i}.{Guid.NewGuid():N}@bl521.test", "hash:x", "Page", "Ordinary", tenant), CancellationToken.None);
        }

        var inner = sp.GetRequiredService<UserRepository>();
        var hidden = new PlatformAccountScopedUserRepository(inner, new HideAccountsFromCaller());
        var visible = new PlatformAccountScopedUserRepository(inner, new ShowEveryAccount());

        var pages = new[]
        {
            (await hidden.GetAllByTenantAsync(tenant, 1, 2, CancellationToken.None)).ToList(),
            (await hidden.GetAllByTenantAsync(tenant, 2, 2, CancellationToken.None)).ToList(),
            (await hidden.GetAllByTenantAsync(tenant, 3, 2, CancellationToken.None)).ToList(),
        };

        Assert.Equal([2, 1, 0], pages.Select(p => p.Count).ToArray());
        Assert.All(pages.SelectMany(p => p), u => Assert.False(u.IsPlatformAccount));
        Assert.Equal(6, (await visible.GetAllByTenantAsync(tenant, 1, 10, CancellationToken.None)).Count());

        User PlatformMarked(User user)
        {
            user.SetPlatformActorType(actorType);
            return user;
        }
    }

    // ── item 3: role-level reach ────────────────────────────────────────────────────────────────────────

    /// <summary>A tenant actor revoking a permission from a role the platform account holds ends the sessions of the ORDINARY
    /// holder only — the platform account's sessions are untouched; the per-role user counts the same actor reads leave the platform
    /// account out, while a platform-door actor's count includes it.</summary>
    [Theory]
    [InlineData("platform_admin")]
    [InlineData("partner_admin")]
    public async Task A_tenant_actor_changing_a_role_does_not_end_a_platform_accounts_sessions_and_does_not_count_it(string actorType)
    {
        var tenant = Guid.NewGuid();
        var admin = await SeedAsync(tenant);
        var ordinary = await SeedAsync(tenant);
        var platformAccount = await SeedAsync(tenant, platform: true, actorType: actorType);
        var (roleId, permissionId) = await SeedRoleWithGrantAsync(tenant, ordinary, platformAccount);
        var ordinaryRow = await AddRefreshRowAsync(ordinary, tenant, "tenant_user");
        var platformRow = await AddRefreshRowAsync(platformAccount, tenant, actorType);
        using var client = _host.Client(await MintTenantTokenAsync(admin, ["auth.roles.read", "auth.roles.assign-permission"]), tenant);

        Assert.Equal(1, await RoleUserCountAsync(client, roleId)); // the ordinary holder only

        var revoke = await client.DeleteAsync($"api/roles/{roleId}/permissions/{permissionId}");

        Assert.True(revoke.StatusCode == HttpStatusCode.NoContent, $"{(int)revoke.StatusCode}: {await revoke.Content.ReadAsStringAsync()}");
        Assert.NotNull((await RefreshRowAsync(ordinaryRow, tenant))!.RevokedAt);
        Assert.Null((await RefreshRowAsync(platformRow, tenant))!.RevokedAt);
    }

    /// <summary>The platform-door actor is not hidden from anything: the count it reads for a role includes the platform accounts
    /// that hold it (the same role, counted for the tenant actor above without them).</summary>
    [Theory]
    [InlineData("platform_admin")]
    [InlineData("partner_admin")]
    public async Task A_platform_door_actor_counts_the_platform_accounts_holding_a_role(string actorType)
    {
        var superAdmin = await RoleIdAsync("SuperAdmin");
        var holder = await SeedAsync(BusinessAndPlatformTenant, platform: true, roles: ["SuperAdmin"], actorType: actorType);
        var ordinary = await SeedAsync(BusinessAndPlatformTenant, roles: ["SuperAdmin"]);
        var admin = await SeedAsync(BusinessAndPlatformTenant);
        var platformToken = await MintPlatformTokenAsync(holder, actorType, ["auth.roles.read"]);

        using var platformClient = _host.Client(platformToken, BusinessAndPlatformTenant);
        using var tenantClient = _host.Client(await MintTenantTokenAsync(admin, ["auth.roles.read"]), BusinessAndPlatformTenant);
        var seenByPlatform = await RoleUserCountAsync(platformClient, superAdmin);
        var seenByTenant = await RoleUserCountAsync(tenantClient, superAdmin);

        int platformHolders;
        using (var scope = _host.Factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(BusinessAndPlatformTenant);
            var ids = (await scope.ServiceProvider.GetRequiredService<IUserRoleRepository>().GetUserIdsByRoleAsync(superAdmin, BusinessAndPlatformTenant, CancellationToken.None)).ToList();
            platformHolders = (int)await _host.Database.GetCollection<User>("users").CountDocumentsAsync(u => ids.Contains(u.Id) && u.PlatformActorType != null);
        }

        Assert.True(platformHolders >= 1);
        Assert.Equal(seenByPlatform - platformHolders, seenByTenant);
        Assert.True(seenByTenant >= 1); // the ordinary SuperAdmin holder of this test (and of others) is counted
    }

    // ── item 4: no fail-open visibility ─────────────────────────────────────────────────────────────────

    /// <summary>The production container resolves the user repository and the list reader WITH the visibility: the same request
    /// principal drives both, so a tenant actor does not get the platform account from either, a platform-door actor and a call with
    /// no request do.</summary>
    [Theory]
    [InlineData("platform_admin")]
    [InlineData("partner_admin")]
    public async Task The_production_container_resolves_both_with_the_visibility_and_both_follow_the_request(string actorType)
    {
        var tenant = Guid.NewGuid();
        var platformAccount = await SeedAsync(tenant, platform: true, actorType: actorType);
        var ordinary = await SeedAsync(tenant);
        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(tenant);
        var accessor = sp.GetRequiredService<IHttpContextAccessor>();
        Assert.IsType<HttpPlatformAccountVisibility>(sp.GetRequiredService<IPlatformAccountVisibility>());
        var users = sp.GetRequiredService<IUserRepository>();
        var reader = sp.GetRequiredService<IUserListReader>();
        Assert.IsType<PlatformAccountScopedUserRepository>(users);
        Assert.IsType<UserListReader>(reader);

        static DefaultHttpContext As(string actorType) => new()
        {
            User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim("actor_type", actorType) }, "Bearer"))
        };

        async Task<(bool ById, long Total)> SeenAsync()
        {
            var byId = await users.GetByIdAndTenantAsync(platformAccount.Id, tenant, CancellationToken.None);
            var summary = await reader.GetSummaryAsync(tenant, CancellationToken.None);
            return (byId is not null, summary.Total);
        }

        try
        {
            accessor.HttpContext = As("tenant_user");
            Assert.Equal((false, 1L), await SeenAsync()); // the ordinary user only
            accessor.HttpContext = As("");              // FIX8: authenticated, but no actor type: hidden like a tenant actor
            Assert.Equal((false, 1L), await SeenAsync());
            accessor.HttpContext = As("unknown_actor"); // … and an actor type this service does not know
            Assert.Equal((false, 1L), await SeenAsync());
            accessor.HttpContext = As(actorType);       // only a platform-door actor sees
            Assert.Equal((true, 2L), await SeenAsync());
            accessor.HttpContext = null;
            Assert.Equal((true, 2L), await SeenAsync());
        }
        finally
        {
            accessor.HttpContext = null;
        }

        Assert.NotEqual(Guid.Empty, ordinary.Id);
    }

    /// <summary>Neither class lets the visibility be left out: the constructor argument is required (no default, no null), and a
    /// container that does not register it cannot build the repository or the reader — they never quietly show every platform account
    /// to every tenant actor.</summary>
    [Fact]
    public void Neither_the_repository_nor_the_list_reader_can_be_built_without_the_visibility()
    {
        foreach (var type in new[] { typeof(PlatformAccountScopedUserRepository), typeof(UserListReader) })
        {
            var parameter = Assert.Single(Assert.Single(type.GetConstructors()).GetParameters(), p => p.ParameterType == typeof(IPlatformAccountVisibility));
            Assert.False(parameter.HasDefaultValue, $"{type.Name}: the visibility has a default");
        }

        using var scope = _host.Factory.Services.CreateScope();
        var inner = scope.ServiceProvider.GetRequiredService<UserRepository>();
        Assert.Throws<ArgumentNullException>(() => new PlatformAccountScopedUserRepository(inner, null!));
        Assert.Throws<ArgumentNullException>(() => new UserListReader(_host.Database, null!));

        var bare = new ServiceCollection()
            .AddSingleton(inner)
            .AddSingleton(_host.Database)
            .AddScoped<IUserRepository, PlatformAccountScopedUserRepository>()
            .AddScoped<IUserListReader, UserListReader>()
            .BuildServiceProvider();
        Assert.Throws<InvalidOperationException>(() => bare.GetRequiredService<IUserRepository>());
        Assert.Throws<InvalidOperationException>(() => bare.GetRequiredService<IUserListReader>());
    }

    // ── item 5: the refusal leaves one log line, with an event code and the tenant id only ──────────────

    /// <summary>The tenant doors refuse a platform account with the same answer as before AND write one warning each: the event
    /// code, the door and the tenant id — no e-mail, no user id, nothing else. Over HTTP that is sign-in, MFA verification and
    /// refresh (anonymous requests, so the repository does not hide the account and the handler's own check answers); the tenant
    /// forced change is called with a tenant_user token, so the REPOSITORY has already made the account absent and the handler
    /// answers "unauthorized" for a user it cannot find — that door's own line is pinned at handler level below.</summary>
    [Theory]
    [InlineData("platform_admin")]
    [InlineData("partner_admin")]
    public async Task Every_tenant_door_refusal_of_a_platform_account_leaves_one_log_line_with_the_event_code_and_the_tenant_only(string actorType)
    {
        var tenant = Guid.NewGuid();
        var account = await SeedAsync(tenant, platform: true, mustChange: true, actorType: actorType);
        var ordinary = await SeedAsync(tenant);
        var (challengeId, code) = await CreateChallengeAsync(account, tenant);

        var signIn = await TenantLoginAsync(account.Email, Password, tenant);
        var refresh = await RefreshAsync(account, tenant, "tenant_user");
        using var forcedClient = _host.Client(await MintTenantTokenAsync(account, []), tenant);
        var forced = await forcedClient.PostAsJsonAsync("api/tenant-auth/change-password/forced", new { currentPassword = Password, newPassword = NewPassword, rememberMe = false });
        var verify = await VerifyMfaAsync(tenant, challengeId, code);
        var control = await TenantLoginAsync(ordinary.Email, Password, tenant); // an ordinary user logs nothing of the kind

        // the answers are the ones the doors always gave
        Assert.Equal(HttpStatusCode.Unauthorized, signIn.Status);
        Assert.Equal("Invalid email or password.", Assert.Single(signIn.Errors));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.Response.StatusCode);
        Assert.Contains("Invalid refresh token.", refresh.Body, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Unauthorized, forced.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, verify.Status);
        Assert.Equal("Invalid verification code.", Assert.Single(verify.Errors));
        Assert.Equal(HttpStatusCode.OK, control.Status);

        // …and each of the three left exactly one line
        var lines = Host.LogLines.Where(l =>
            l.Fields.GetValueOrDefault("EventCode") == TenantDoorPlatformAccountRefusal.EventCode
            && l.Fields.GetValueOrDefault("TenantId") == tenant.ToString()).ToList();
        Assert.Equal(
            ["mfa-verify", "refresh", "sign-in"],
            lines.Select(l => l.Fields["Door"]).OrderBy(d => d, StringComparer.Ordinal).ToArray());
        foreach (var line in lines)
        {
            Assert.Equal(LogLevel.Warning, line.Level);
            Assert.Equal(["Door", "EventCode", "TenantId", "{OriginalFormat}"], line.Fields.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
        }

        // nothing logged by the loggers on these paths names the account
        Assert.DoesNotContain(Host.LogLines, l =>
            l.Message.Contains(account.Email, StringComparison.OrdinalIgnoreCase)
            || l.Message.Contains(account.Id.ToString(), StringComparison.OrdinalIgnoreCase)
            || l.Fields.Values.Any(v => v.Contains(account.Email, StringComparison.OrdinalIgnoreCase) || v.Contains(account.Id.ToString(), StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>The tenant forced change's own refusal (reached when nothing has made the account absent first — no request in play)
    /// writes the line too: the event code, the door and the tenant id, no e-mail, no user id; the answer is the same 401.</summary>
    [Theory]
    [InlineData("platform_admin")]
    [InlineData("partner_admin")]
    public async Task The_forced_change_handlers_own_refusal_of_a_platform_account_leaves_the_line_and_the_same_401(string actorType)
    {
        var platform = NewUser(platform: true, actorType);
        var lines = new ConcurrentQueue<LogLine>();
        var handler = new ForcedChangeTenantPasswordCommandHandler(
            new InMemoryUserRepository([platform]), null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!,
            new LineSink<ForcedChangeTenantPasswordCommandHandler>(lines));

        var result = await handler.Handle(
            new ForcedChangeTenantPasswordCommand(platform.Id, BusinessAndPlatformTenant, Password, NewPassword, "127.0.0.1", null), CancellationToken.None);

        Assert.Equal(401, result.StatusCode);
        Assert.Equal("Unauthorized.", Assert.Single(result.Errors));
        var line = Assert.Single(lines);
        Assert.Equal(LogLevel.Warning, line.Level);
        Assert.Equal(TenantDoorPlatformAccountRefusal.EventCode, line.Fields["EventCode"]);
        Assert.Equal("forced-change", line.Fields["Door"]);
        Assert.Equal(BusinessAndPlatformTenant.ToString(), line.Fields["TenantId"]);
        Assert.Equal(["Door", "EventCode", "TenantId", "{OriginalFormat}"], line.Fields.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain(platform.Email, line.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(platform.Id.ToString(), line.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── item 7: MFA ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The tenant requires MFA. A platform account that gets the password right is still refused at the door's first check:
    /// the generic 401 a stranger gets, no challenge (nothing in the answer, no row), no e-mail with a code. The ordinary user of the
    /// same tenant — the control — is challenged and its code mailed, so the setup really does demand MFA.</summary>
    [Theory]
    [InlineData("platform_admin")]
    [InlineData("partner_admin")]
    public async Task A_platform_account_at_the_tenant_door_with_mfa_required_gets_no_challenge_no_mail_and_the_generic_401(string actorType)
    {
        var tenant = Guid.NewGuid();
        Host.MfaTenants[tenant] = true;
        var account = await SeedAsync(tenant, platform: true, actorType: actorType);
        var ordinary = await SeedAsync(tenant);
        var challenges = _host.Database.GetCollection<MfaChallenge>("mfaChallenges");

        var stranger = await TenantLoginAsync($"nobody.{Guid.NewGuid():N}@bl521.test", Password, tenant);
        var refused = await TenantLoginAsync(account.Email, Password, tenant);
        var refusedRaw = await RawTenantLoginAsync(account.Email, Password, tenant);
        var challenged = await RawTenantLoginAsync(ordinary.Email, Password, tenant);

        Assert.Equal(HttpStatusCode.Unauthorized, refused.Status);
        Assert.Equal(stranger.Errors, refused.Errors);
        Assert.False(refused.HasToken);
        Assert.DoesNotContain("challenge", refusedRaw.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mfaRequired\":true", refusedRaw.Body.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await challenges.CountDocumentsAsync(c => c.UserId == account.Id));
        Assert.DoesNotContain(Host.SentOtps, o => string.Equals(o.Email, account.Email, StringComparison.OrdinalIgnoreCase));

        // the control: MFA really is demanded here
        Assert.Equal(HttpStatusCode.OK, challenged.Status);
        Assert.Contains("challenge", challenged.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await challenges.CountDocumentsAsync(c => c.UserId == ordinary.Id));
        Assert.Single(Host.SentOtps, o => string.Equals(o.Email, ordinary.Email, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>MFA verification over real HTTP: the challenge of a platform account (created before the doors refused such
    /// accounts) with the RIGHT code is answered like a wrong code — 401, no token, no refresh row; the ordinary user's challenge,
    /// verified the same way, mints its token.</summary>
    [Theory]
    [InlineData("platform_admin")]
    [InlineData("partner_admin")]
    public async Task Mfa_verification_over_http_refuses_a_platform_account_and_accepts_an_ordinary_user(string actorType)
    {
        var tenant = Guid.NewGuid();
        var account = await SeedAsync(tenant, platform: true, actorType: actorType);
        var ordinary = await SeedAsync(tenant);
        var (platformChallenge, platformCode) = await CreateChallengeAsync(account, tenant);
        var (ordinaryChallenge, ordinaryCode) = await CreateChallengeAsync(ordinary, tenant);
        var (wrongChallenge, _) = await CreateChallengeAsync(ordinary, tenant);

        var refused = await VerifyMfaAsync(tenant, platformChallenge, platformCode);
        var wrongCode = await VerifyMfaAsync(tenant, wrongChallenge, "000000");
        var accepted = await VerifyMfaAsync(tenant, ordinaryChallenge, ordinaryCode);

        Assert.Equal(HttpStatusCode.Unauthorized, refused.Status);
        Assert.Equal("Invalid verification code.", Assert.Single(refused.Errors));
        Assert.Equal(wrongCode.Status, refused.Status);
        Assert.Equal(wrongCode.Errors, refused.Errors);
        Assert.False(refused.HasToken);
        Assert.Equal(0, await _host.Database.GetCollection<RefreshToken>("refreshTokens").CountDocumentsAsync(r => r.UserId == account.Id));
        Assert.Equal(HttpStatusCode.OK, accepted.Status);
        Assert.True(accepted.HasToken);
    }

    // ═══ BL-521 FIX8 ═══════════════════════════════════════════════════════════════════════════════════════

    // ── item 1: the marker write fails closed (the HTTP end of it is PlatformMintMarkerFailureTests) ─────

    /// <summary>The helper itself: a write that throws propagates, and a write that matched no account is a failure too — in both
    /// cases the loaded user is NOT marked (nothing downstream may treat the account as marked, or mint from it).</summary>
    [Theory]
    [InlineData("throws")]
    [InlineData("matches-nothing")]
    public async Task The_discriminator_helper_fails_and_leaves_the_user_unmarked_when_the_write_throws_or_matches_nothing(string outcome)
    {
        var user = new User($"helper.{Guid.NewGuid():N}@bl521.test", "hash:x", "Door", "Probe", BusinessAndPlatformTenant);
        var store = new InMemoryUserRepository([user]) { MarkerWrites = outcome == "throws" ? MarkerWrite.Throws : MarkerWrite.MatchesNothing };

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            PlatformAccountDiscriminator.EnsureAsync(store, user, "platform_admin", NullLogger.Instance, CancellationToken.None));

        Assert.Null(user.PlatformActorType);
        if (outcome == "matches-nothing")
        {
            Assert.Equal(PlatformAccountDiscriminator.MarkerNotWrittenMessage, failure.Message);
        }
    }

    /// <summary>The real repository's write reports whether it MATCHED a live account of that id and tenant: yes for a live one
    /// (also when it already holds the value), no for a deleted account, another tenant's, an unknown id — and what matched nothing
    /// wrote nothing.</summary>
    [Fact]
    public async Task The_marker_write_reports_whether_it_matched_a_live_account_of_that_tenant()
    {
        var tenant = Guid.NewGuid();
        var live = await SeedAsync(tenant);
        var deleted = await SeedAsync(tenant);
        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(tenant);
        var repository = sp.GetRequiredService<UserRepository>();
        await repository.SoftDeleteAsync(deleted.Id, tenant, CancellationToken.None);

        Assert.True(await repository.SetPlatformActorTypeAsync(live.Id, tenant, "partner_admin", CancellationToken.None));
        Assert.True(await repository.SetPlatformActorTypeAsync(live.Id, tenant, "partner_admin", CancellationToken.None)); // the value it holds still matches
        Assert.False(await repository.SetPlatformActorTypeAsync(deleted.Id, tenant, "platform_admin", CancellationToken.None));
        Assert.False(await repository.SetPlatformActorTypeAsync(live.Id, Guid.NewGuid(), "platform_admin", CancellationToken.None));
        Assert.False(await repository.SetPlatformActorTypeAsync(Guid.NewGuid(), tenant, "platform_admin", CancellationToken.None));

        var users = _host.Database.GetCollection<User>("users");
        Assert.Equal("partner_admin", (await users.Find(u => u.Id == live.Id).FirstAsync()).PlatformActorType);
        Assert.Null((await users.Find(u => u.Id == deleted.Id).FirstAsync()).PlatformActorType);
    }

    // ── item 2: only a platform-door actor sees platform accounts; every other authenticated request does not ─

    /// <summary>A token that IS authenticated but carries an empty or an unknown actor type is not a platform-door actor: it gets the
    /// answer of an absent user for a platform account (404), and the platform account is not in its lookup, its list or its counter.
    /// (FIX6 hid only <c>tenant_user</c>: anything else saw every account.) The control: the same token reads the ordinary colleague.</summary>
    [Theory]
    [InlineData("", "platform_admin")]
    [InlineData("service", "partner_admin")]
    [InlineData("unknown_actor", "platform_admin")]
    public async Task An_authenticated_token_with_an_empty_or_unknown_actor_type_does_not_see_platform_accounts(string tokenActorType, string platformType)
    {
        var tenant = Guid.NewGuid();
        var tag = $"U{Guid.NewGuid():N}"[..12];
        var admin = await SeedAsync(tenant, firstName: tag);
        var colleague = await SeedAsync(tenant, firstName: tag);
        var platformAccount = await SeedAsync(tenant, platform: true, firstName: tag, actorType: platformType);
        using var client = _host.Client(await MintPlatformTokenAsync(admin, tokenActorType, UserAdminPermissions), tenant);

        var getPlatform = await client.GetAsync($"api/users/{platformAccount.Id}");
        var getColleague = await client.GetAsync($"api/users/{colleague.Id}");
        var lookup = await LookupIdsAsync(client, tag, 50);
        var list = await client.GetAsync("api/users?start=0&length=200");

        Assert.Equal(HttpStatusCode.NotFound, getPlatform.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getColleague.StatusCode);
        Assert.Contains(colleague.Id, lookup);
        Assert.DoesNotContain(platformAccount.Id, lookup);
        var listBody = await list.Content.ReadAsStringAsync();
        Assert.True(list.StatusCode == HttpStatusCode.OK, $"{(int)list.StatusCode}: {listBody}");
        Assert.DoesNotContain(platformAccount.Email, listBody, StringComparison.OrdinalIgnoreCase);
        using var listJson = JsonDocument.Parse(listBody);
        Assert.Equal(2, listJson.RootElement.GetProperty("data").GetProperty("summary").GetProperty("total").GetInt64()); // admin + colleague
    }

    // ── item 3: an empty or whitespace actor type is "not a platform account" in every filter, as in memory ─

    /// <summary>An account whose stored actor type is "", whitespace only or a lone no-break space is — by <c>User.IsPlatformAccount</c>
    /// — an ordinary user, and now every rule agrees: the tenant actor sees it (get, lookup, list, counter), a role-permission removal
    /// ends its sessions, and the tenant door signs it in. A real platform account beside it stays hidden and untouched.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("\t  ")]
    public async Task An_account_with_a_blank_actor_type_is_an_ordinary_user_to_every_rule(string blank)
    {
        var tenant = Guid.NewGuid();
        var tag = $"B{Guid.NewGuid():N}"[..12];
        var admin = await SeedAsync(tenant, firstName: tag);
        var blankAccount = await SeedAsync(tenant, firstName: tag);
        var platformAccount = await SeedAsync(tenant, platform: true, firstName: tag);
        await SetStoredActorTypeAsync(blankAccount.Id, blank);
        Assert.False((await UserAsync(blankAccount.Id, tenant))!.IsPlatformAccount); // the memory rule this must agree with
        var (roleId, permissionId) = await SeedRoleWithGrantAsync(tenant, blankAccount, platformAccount);
        var blankRow = await AddRefreshRowAsync(blankAccount, tenant, "tenant_user");
        var platformRow = await AddRefreshRowAsync(platformAccount, tenant, "platform_admin");
        using var client = _host.Client(await MintTenantTokenAsync(admin, [.. UserAdminPermissions, "auth.roles.read", "auth.roles.assign-permission"]), tenant);

        // seen: get, lookup, list, counter
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"api/users/{blankAccount.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"api/users/{platformAccount.Id}")).StatusCode);
        var lookup = await LookupIdsAsync(client, tag, 50);
        Assert.Contains(blankAccount.Id, lookup);
        Assert.DoesNotContain(platformAccount.Id, lookup);
        var list = await client.GetAsync("api/users?start=0&length=200");
        var listBody = await list.Content.ReadAsStringAsync();
        Assert.Contains(blankAccount.Email, listBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(platformAccount.Email, listBody, StringComparison.OrdinalIgnoreCase);
        using (var listJson = JsonDocument.Parse(listBody))
        {
            Assert.Equal(2, listJson.RootElement.GetProperty("data").GetProperty("summary").GetProperty("total").GetInt64()); // admin + the blank one
        }

        // counted as a holder, and its sessions end with a permission removal; the platform account's do not
        Assert.Equal(1, await RoleUserCountAsync(client, roleId));
        var revoke = await client.DeleteAsync($"api/roles/{roleId}/permissions/{permissionId}");
        Assert.True(revoke.StatusCode == HttpStatusCode.NoContent, $"{(int)revoke.StatusCode}: {await revoke.Content.ReadAsStringAsync()}");
        Assert.NotNull((await RefreshRowAsync(blankRow, tenant))!.RevokedAt);
        Assert.Null((await RefreshRowAsync(platformRow, tenant))!.RevokedAt);

        // and the tenant door signs it in
        var signIn = await TenantLoginAsync(blankAccount.Email, Password, tenant);
        Assert.Equal(HttpStatusCode.OK, signIn.Status);
        Assert.True(signIn.HasToken);
    }

    /// <summary>The two Mongo filters (and the role-holder lookup) are the in-memory rule, over real documents: for every kind of
    /// value — missing, empty, ASCII and Unicode whitespace, spaces around a real value, real and unknown types — the accounts the
    /// platform-account filter finds are exactly the ones <c>IsPlatformAccount</c> says are, and the accounts the exclusion keeps
    /// (page, lookup, counter) are exactly the rest.</summary>
    [Fact]
    public async Task The_mongo_filters_agree_with_IsPlatformAccount_for_every_kind_of_actor_type_value()
    {
        var tenant = Guid.NewGuid();
        var values = new string?[] { null, "", " ", "\t", " ", "  　", "platform_admin", "partner_admin", " partner_admin ", "x", "service" };
        var ids = new List<Guid>();
        foreach (var value in values)
        {
            var account = await SeedAsync(tenant);
            if (value is not null)
            {
                await SetStoredActorTypeAsync(account.Id, value);
            }

            ids.Add(account.Id);
        }

        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(tenant);
        var repository = sp.GetRequiredService<UserRepository>();
        var everyone = (await repository.GetAllByTenantAsync(tenant, 1, 100, CancellationToken.None)).ToList();
        var expectedPlatform = everyone.Where(u => u.IsPlatformAccount).Select(u => u.Id).Order().ToList();
        var expectedOrdinary = everyone.Where(u => !u.IsPlatformAccount).Select(u => u.Id).Order().ToList();
        Assert.Equal(values.Length, everyone.Count);
        Assert.Equal(5, expectedPlatform.Count);  // not vacuous: five carry a real value …
        Assert.Equal(6, expectedOrdinary.Count);  // … six carry none (missing, empty or whitespace)

        var found = (await repository.PlatformAccountIdsAmongAsync(tenant, ids, CancellationToken.None)).Order().ToList();
        var page = (await repository.GetAllByTenantAsync(tenant, 1, 100, excludePlatformAccounts: true, CancellationToken.None)).Select(u => u.Id).Order().ToList();
        var search = (await repository.SearchActiveAsync(tenant, null, 50, excludePlatformAccounts: true, CancellationToken.None)).Select(u => u.Id).Order().ToList();
        var summary = await new UserListReader(_host.Database, new HideAccountsFromCaller()).GetSummaryAsync(tenant, CancellationToken.None);

        Assert.Equal(expectedPlatform, found);
        Assert.Equal(expectedOrdinary, page);
        Assert.Equal(expectedOrdinary, search);
        Assert.Equal(expectedOrdinary.Count, summary.Total);
    }

    // ── the visibility itself ───────────────────────────────────────────────────────────────────────────

    /// <summary>FIX8 item 2 — THE RULE IS THE OTHER WAY ROUND: inside a request ONLY a platform-door actor (platform_admin /
    /// partner_admin, any letter case) sees platform accounts; every other AUTHENTICATED principal hides them — a tenant actor, but
    /// also one with no, an empty or an unknown actor type. An unauthenticated request (the anonymous doors, the internal calls that
    /// authenticate by key) and a call with no request at all (background work) are not hidden from anything.</summary>
    [Theory]
    [InlineData("tenant_user", true, true)]
    [InlineData("TENANT_USER", true, true)]
    [InlineData(null, true, true)]
    [InlineData("", true, true)]
    [InlineData("   ", true, true)]
    [InlineData("service", true, true)]
    [InlineData("unknown_actor", true, true)]
    [InlineData("platform_admin", true, false)]
    [InlineData("PLATFORM_ADMIN", true, false)]
    [InlineData("partner_admin", true, false)]
    [InlineData("Partner_Admin", true, false)]
    [InlineData("tenant_user", false, false)]
    [InlineData(null, false, false)]
    [InlineData("unknown_actor", false, false)]
    public void An_authenticated_request_hides_platform_accounts_unless_it_is_a_platform_door_actor(string? actorType, bool authenticated, bool hides)
    {
        var context = new DefaultHttpContext();
        var claims = actorType is null ? [] : new[] { new System.Security.Claims.Claim("actor_type", actorType) };
        context.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(claims, authenticated ? "Bearer" : null));

        Assert.Equal(hides, new HttpPlatformAccountVisibility(new HttpContextAccessor { HttpContext = context }).HidesPlatformAccounts);
    }

    [Fact]
    public void A_call_with_no_request_hides_nothing()
    {
        Assert.False(new HttpPlatformAccountVisibility(new HttpContextAccessor { HttpContext = null }).HidesPlatformAccounts);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    private sealed record Answer(HttpStatusCode Status, IReadOnlyList<string> Errors, bool HasToken);

    private async Task<Answer> TenantLoginAsync(string email, string password, Guid tenant)
    {
        using var client = _host.Client(tenantHeader: tenant);
        using var response = await client.PostAsJsonAsync("api/tenant-auth/login", new { email, password, rememberMe = false });
        return await ReadAnswerAsync(response);
    }

    private async Task<Answer> RegisterAsync(string email)
    {
        using var client = _host.Client(tenantHeader: BusinessAndPlatformTenant);
        using var response = await client.PostAsJsonAsync("api/tenant-auth/register",
            new { email, password = NewPassword, firstName = "Reg", lastName = "Probe" });
        return await ReadAnswerAsync(response);
    }

    private static async Task<Answer> ReadAnswerAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        var errors = json.RootElement.TryGetProperty("errors", out var e) && e.ValueKind == JsonValueKind.Array
            ? e.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToList()
            : [];
        var hasToken = json.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
                       && data.TryGetProperty("accessToken", out var token) && token.ValueKind == JsonValueKind.String;
        return new Answer(response.StatusCode, errors, hasToken);
    }

    private sealed record Reply(HttpStatusCode Status, string Body, string Shape);

    /// <summary>One request and what its answer says: the status and the error sentences / codes (never ids, never correlation).</summary>
    private static async Task<Reply> SendAsync(HttpClient client, HttpMethod method, string url, object? body)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        var shape = $"{(int)response.StatusCode}";
        try
        {
            using var json = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
            string Join(string name) => json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Array
                ? string.Join(";", p.EnumerateArray().Select(x => x.ToString()))
                : string.Empty;
            shape += $"|{Join("errors")}|{Join("errorCodes")}";
        }
        catch (JsonException)
        {
            shape += "|" + text;
        }

        return new Reply(response.StatusCode, text, shape);
    }

    private sealed record RefreshAnswer(HttpResponseMessage Response, string Body, string RawRefreshToken);

    /// <summary>A refresh: a token of the given actor type and its refresh row, for the account.</summary>
    private async Task<RefreshAnswer> RefreshAsync(Account account, Guid tenant, string actorType)
    {
        var raw = $"refresh-{Guid.NewGuid():N}";
        using (var scope = _host.Factory.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            sp.GetRequiredService<TenantContext>().SetTenant(tenant);
            await sp.GetRequiredService<IRefreshTokenRepository>().CreateAsync(new RefreshToken(
                account.Id, sp.GetRequiredService<IRefreshTokenHasher>().Hash(raw), DateTime.UtcNow.AddDays(7), "127.0.0.1",
                tenant, actorType, "bl521-fix6"), CancellationToken.None);
        }

        var accessToken = actorType == "tenant_user" ? await MintTenantTokenAsync(account, []) : await MintPlatformTokenAsync(account, actorType);
        using var client = _host.Client(tenantHeader: tenant);
        var response = await client.PostAsJsonAsync("api/auth/refresh-token", new { accessToken, refreshToken = raw });
        return new RefreshAnswer(response, await response.Content.ReadAsStringAsync(), raw);
    }

    private async Task<RefreshToken?> RefreshRowAsync(string rawToken)
    {
        using var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(BusinessAndPlatformTenant);
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

    private async Task<string> MintPlatformTokenAsync(Account account, string actorType, string[]? permissions = null)
    {
        var user = await UserAsync(account.Id, account.TenantId);
        using var scope = _host.Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ITokenService>().GeneratePlatformAccessToken(
            user!.Id, user.Email, user.FirstName, user.LastName, account.TenantId, actorType, ["SuperAdmin"], permissions ?? [], 60, false);
    }

    private static User NewUser(bool platform, string actorType = "platform_admin")
    {
        var user = new User($"mfa.{Guid.NewGuid():N}@bl521.test", "hash:x", "Door", "Probe", BusinessAndPlatformTenant);
        user.ConfirmEmail();
        if (platform)
        {
            user.SetPlatformActorType(actorType);
        }

        return user;
    }

    private async Task<Account> SeedAsync(Guid tenantId, bool platform = false, string[]? roles = null, bool mustChange = false, string firstName = "Door", string actorType = "platform_admin")
    {
        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(tenantId);
        var user = new User($"door.{Guid.NewGuid():N}@bl521.test", sp.GetRequiredService<IPasswordHasher>().Hash(Password), firstName, "Probe", tenantId);
        user.ConfirmEmail();
        if (platform)
        {
            user.SetPlatformActorType(actorType);
        }

        if (mustChange)
        {
            user.RequirePasswordChange(null);
        }

        user = await sp.GetRequiredService<IUserRepository>().CreateAsync(user, CancellationToken.None);
        foreach (var roleName in roles ?? [])
        {
            var roleId = await RoleIdAsync(roleName, tenantId);
            await sp.GetRequiredService<IUserRoleRepository>().AssignAsync(new UserRole(user.Id, roleId, tenantId, "bl521-fix6"), CancellationToken.None);
        }

        return new Account(user.Id, user.Email, tenantId);
    }

    /// <summary>A role of the tenant by name — the production seeder's own SuperAdmin for the platform tenant.</summary>
    private async Task<Guid> RoleIdAsync(string name, Guid? tenantId = null)
    {
        var tenant = tenantId ?? BusinessAndPlatformTenant;
        using var scope = _host.Factory.Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<IRoleRepository>();
        return (await roles.GetByNameAndTenantAsync(name, tenant, CancellationToken.None)
                ?? await roles.UpsertSystemRoleAsync(name, name, "Platform administrator role", tenant, CancellationToken.None)).Id;
    }

    /// <summary>The account as the store holds it — read outside any request, so nothing is hidden.</summary>
    private async Task<User?> UserAsync(Guid id, Guid tenantId)
    {
        using var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(tenantId);
        return await scope.ServiceProvider.GetRequiredService<IUserRepository>().GetByIdAndTenantAsync(id, tenantId, CancellationToken.None);
    }

    private async Task<bool> PasswordIsAsync(Account account, string password)
    {
        var user = await UserAsync(account.Id, account.TenantId);
        using var scope = _host.Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Verify(password, user!.PasswordHash);
    }

    private async Task<string[]> RolesAsync(Guid id, Guid tenantId)
    {
        using var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(tenantId);
        return (await scope.ServiceProvider.GetRequiredService<IUserRoleRepository>().GetRolesByUserAsync(id, tenantId, CancellationToken.None)).OrderBy(r => r, StringComparer.Ordinal).ToArray();
    }

    // ── FIX7 helpers ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The ids a lookup answers (the answer is read as data: every "userId" in it), in the order the server gave them.</summary>
    private static async Task<List<Guid>> LookupIdsAsync(HttpClient client, string search, int limit)
    {
        using var response = await client.GetAsync($"api/users/lookup?search={search}&limit={limit}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {body}");
        using var json = JsonDocument.Parse(body);
        var ids = new List<Guid>();
        void Walk(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        if (string.Equals(property.Name, "userId", StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.String)
                        {
                            ids.Add(property.Value.GetGuid());
                        }
                        else
                        {
                            Walk(property.Value);
                        }
                    }

                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        Walk(item);
                    }

                    break;
            }
        }

        Walk(json.RootElement);
        return ids;
    }

    private async Task<Reply> RawTenantLoginAsync(string email, string password, Guid tenant)
    {
        using var client = _host.Client(tenantHeader: tenant);
        return await SendAsync(client, HttpMethod.Post, "api/tenant-auth/login", new { email, password, rememberMe = false });
    }

    /// <summary>A refresh row of the given actor type for the account; its raw token.</summary>
    private async Task<string> AddRefreshRowAsync(Account account, Guid tenant, string actorType)
    {
        var raw = $"refresh-{Guid.NewGuid():N}";
        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(tenant);
        await sp.GetRequiredService<IRefreshTokenRepository>().CreateAsync(new RefreshToken(
            account.Id, sp.GetRequiredService<IRefreshTokenHasher>().Hash(raw), DateTime.UtcNow.AddDays(7), "127.0.0.1",
            tenant, actorType, "bl521-fix7"), CancellationToken.None);
        return raw;
    }

    private async Task<RefreshToken?> RefreshRowAsync(string rawToken, Guid tenant)
    {
        using var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(tenant);
        return await scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>().GetByTokenAsync(rawToken, CancellationToken.None);
    }

    /// <summary>A challenge for the account as the tenant door created them before it refused platform accounts, and the code the
    /// (recording) delivery was asked to mail.</summary>
    private async Task<(string ChallengeId, string Code)> CreateChallengeAsync(Account account, Guid tenant)
    {
        var user = await UserAsync(account.Id, tenant);
        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(tenant);
        var created = await sp.GetRequiredService<IMfaChallengeService>().CreateEmailChallengeAsync(user!, "127.0.0.1", null, CancellationToken.None);
        var code = Host.SentOtps.Last(o => string.Equals(o.Email, account.Email, StringComparison.OrdinalIgnoreCase)).Code;
        return (created.ChallengeId, code);
    }

    private async Task<Answer> VerifyMfaAsync(Guid tenant, string challengeId, string code)
    {
        using var client = _host.Client(tenantHeader: tenant);
        using var response = await client.PostAsJsonAsync("api/tenant-auth/mfa/verify", new { challengeId, code });
        return await ReadAnswerAsync(response);
    }

    /// <summary>A role of the tenant held by the given accounts, with one MANUAL permission grant on it.</summary>
    private async Task<(Guid RoleId, Guid PermissionId)> SeedRoleWithGrantAsync(Guid tenant, params Account[] holders)
    {
        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(tenant);
        var role = await sp.GetRequiredService<IRoleRepository>().CreateAsync(
            new Role($"fix7-{Guid.NewGuid():N}"[..16], "FIX7 probe role", null, tenant), CancellationToken.None);
        var permissionId = Guid.NewGuid();
        await sp.GetRequiredService<IRolePermissionRepository>().AssignAsync(
            new RolePermission(role.Id, permissionId, tenant, "bl521-fix7", GrantSource.Manual, sourceModuleCode: null), CancellationToken.None);
        foreach (var holder in holders)
        {
            await sp.GetRequiredService<IUserRoleRepository>().AssignAsync(new UserRole(holder.Id, role.Id, tenant, "bl521-fix7"), CancellationToken.None);
        }

        return (role.Id, permissionId);
    }

    /// <summary>The holder count the roles list shows for a role.</summary>
    private static async Task<int> RoleUserCountAsync(HttpClient client, Guid roleId)
    {
        using var response = await client.GetAsync("api/roles");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {body}");
        using var json = JsonDocument.Parse(body);
        var roles = json.RootElement.GetProperty("data");
        foreach (var role in roles.EnumerateArray())
        {
            if (role.GetProperty("id").GetGuid() == roleId)
            {
                return role.GetProperty("userCount").GetInt32();
            }
        }

        throw new Xunit.Sdk.XunitException($"role {roleId} is not in the roles list: {body}");
    }

    private sealed class DefaultSettings : ITenantLoginSettingsClient
    {
        public Task<TenantLoginSettingsSnapshot> GetAsync(Guid tenantId, CancellationToken ct)
        {
            var mfa = Host.MfaTenants.ContainsKey(tenantId); // FIX7: a tenant a test marked as demanding MFA
            return Task.FromResult(new TenantLoginSettingsSnapshot(
                tenantId, TwoFactorEnabled: mfa, MfaRequired: mfa, EmailLoginEnabled: true, PhoneLoginEnabled: false,
                PasswordMinLength: 8, PasswordRequireUppercase: false, PasswordRequireLowercase: false, PasswordRequireDigit: false,
                PasswordRequireSpecialChar: false, PasswordExpirationDays: null, SessionTimeoutMinutes: 60, RefreshTokenLifetimeDays: 7,
                MaxFailedLoginAttempts: 5, LockoutDurationMinutes: 15));
        }
    }

    /// <summary>The OTP delivery of the test host: nothing is mailed, every (address, code) is remembered.</summary>
    private sealed class RecordingOtp : IOtpDeliveryService
    {
        public Task SendEmailOtpAsync(string email, string code, DateTime expiresAtUtc, CancellationToken ct)
        {
            Host.SentOtps.Enqueue((email, code));
            return Task.CompletedTask;
        }
    }

    /// <summary>Writes the actor type into the stored document as it is (no entity normalisation): how a blank value gets there.</summary>
    private async Task SetStoredActorTypeAsync(Guid userId, string? value) =>
        await _host.Database.GetCollection<User>("users").UpdateOneAsync(u => u.Id == userId, Builders<User>.Update.Set("PlatformActorType", value));

    /// <summary>One server log line as a handler wrote it: its level, its rendered message and its structured fields.</summary>
    public sealed record LogLine(string Category, LogLevel Level, string Message, IReadOnlyDictionary<string, string> Fields);

    /// <summary>The Api logs through Serilog (UseSerilog replaces the provider list), so — as FakePlatformEdge does — the sink is the
    /// handler's own ILogger&lt;T&gt;, registered closed; it keeps the structured fields the log line was written with.</summary>
    public sealed class LineSink<T>(ConcurrentQueue<LogLine> into) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var fields = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? pairs.ToDictionary(p => p.Key, p => p.Value?.ToString() ?? string.Empty)
                : new Dictionary<string, string>();
            into.Enqueue(new LogLine(typeof(T).Name, logLevel, formatter(state, exception), fields));
        }
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

    // ── the MFA verification, over in-memory stores ─────────────────────────────────────────────────────

    private sealed class MfaWorld
    {
        private readonly IUserRepository _users;
        private readonly User _user;
        private readonly Mint _mint = new();

        public MfaWorld(IUserRepository users, User user)
        {
            _users = users;
            _user = user;
        }

        public bool MintedAnything => _mint.TokensIssued > 0 || _mint.RefreshRows > 0;

        public Task<Response<Diten.AuthService.Application.DTOs.AuthResponse>> VerifyAsync() =>
            new VerifyMfaCommandHandler(
                    new ChallengeFor(new MfaChallenge(_user.TenantId, _user.Id, "hash", "email", "destination-hash", "code-hash", DateTime.UtcNow.AddMinutes(5), 5, "127.0.0.1", null)),
                    new DefaultSettings(),
                    _users,
                    new NoRoles(),
                    new NoRoleDocuments(),
                    new NoPermissions(),
                    new Passthrough(),
                    _mint,
                    _mint,
                    _mint,
                    new NoAudit(),
                    NullLogger<VerifyMfaCommandHandler>.Instance)
                .Handle(new VerifyMfaCommand("challenge", "123456", "127.0.0.1", null), CancellationToken.None);
    }

    private sealed class ChallengeFor(MfaChallenge challenge) : IMfaChallengeService
    {
        public Task<MfaChallengeCreated> CreateEmailChallengeAsync(User user, string requestIp, string? userAgent, CancellationToken ct) => throw new NotSupportedException();
        public Task<MfaChallengeCreated> ResendEmailChallengeAsync(string challengeId, string requestIp, string? userAgent, CancellationToken ct) => throw new NotSupportedException();
        public Task<MfaChallenge> VerifyAsync(string challengeId, string code, CancellationToken ct) => Task.FromResult(challenge);
    }

    /// <summary>Token service, refresh hasher and refresh store in one: counts what was minted.</summary>
    private sealed class Mint : ITokenService, IRefreshTokenHasher, IRefreshTokenRepository
    {
        public int TokensIssued { get; private set; }
        public int RefreshRows { get; private set; }

        public string GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes) { TokensIssued++; return "access"; }
        public string GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions) { TokensIssued++; return "access"; }
        public string GenerateRefreshToken() => "refresh";
        public string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes, bool requiresPasswordChange) => throw new NotSupportedException();
        public string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions) => throw new NotSupportedException();
        public string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes) => throw new NotSupportedException();
        public System.Security.Claims.ClaimsPrincipal GetPrincipalFromExpiredToken(string token) => throw new NotSupportedException();

        public string Hash(string refreshToken) => "rh:" + refreshToken;

        public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct) => Task.FromResult<RefreshToken?>(null);
        public Task CreateAsync(RefreshToken refreshToken, CancellationToken ct) { RefreshRows++; return Task.CompletedTask; }
        public Task UpdateAsync(RefreshToken refreshToken, CancellationToken ct) => Task.CompletedTask;
        public Task RevokeAsync(string token, CancellationToken ct) => Task.CompletedTask;
        public Task RevokeAllByUserAsync(Guid userId, Guid tenantId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class NoRoles : IUserRoleRepository
    {
        public Task<IEnumerable<string>> GetRolesByUserAsync(Guid userId, Guid tenantId, CancellationToken ct) => Task.FromResult<IEnumerable<string>>(["Viewer"]);
        public Task AssignAsync(UserRole userRole, CancellationToken ct) => Task.CompletedTask;
        public Task RevokeAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> ExistsAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct) => Task.FromResult(false);
        public Task<IReadOnlyCollection<Guid>> GetUserIdsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<Guid>>([]);
    }

    private sealed class NoRoleDocuments : IRoleRepository
    {
        public Task<Role?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct) => Task.FromResult<Role?>(null);
        public Task<Role?> GetByNameAndTenantAsync(string name, Guid tenantId, CancellationToken ct) => Task.FromResult<Role?>(null);
        public Task<IEnumerable<Role>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct) => Task.FromResult<IEnumerable<Role>>([]);
        public Task<Role> CreateAsync(Role role, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> UpsertSystemRoleAsync(string name, string displayName, string? description, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> UpdateAsync(Role role, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, Guid tenantId, string deletedBy, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class NoPermissions : IRolePermissionRepository
    {
        public Task<IEnumerable<string>> GetPermissionsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) => Task.FromResult<IEnumerable<string>>([]);
        public Task<IEnumerable<string>> GetPermissionsByRolesAsync(List<Guid> roleIds, Guid tenantId, CancellationToken ct) => Task.FromResult<IEnumerable<string>>([]);
        public Task AssignAsync(RolePermission rolePermission, CancellationToken ct) => Task.CompletedTask;
        public Task RevokeAsync(Guid roleId, Guid permissionId, Guid tenantId, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<RolePermission>> GetByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) => Task.FromResult<IReadOnlyList<RolePermission>>([]);
        public Task RemoveByIdAsync(Guid id, Guid tenantId, CancellationToken ct) => Task.CompletedTask;
        public Task<long> RemoveByPermissionIdAsync(Guid permissionId, CancellationToken ct) => Task.FromResult(0L);
    }

    private sealed class NoAudit : IAuthAuditService
    {
        public Task WriteEmptyRoleLoginAsync(Guid userId, Guid tenantId, string email, CancellationToken ct = default) => Task.CompletedTask;
        public Task WriteAsync(string eventName, Guid? userId, Guid tenantId, string metadata, CancellationToken ct = default) => Task.CompletedTask;
    }
}

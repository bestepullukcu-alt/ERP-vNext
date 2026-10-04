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
using Diten.AuthService.Application.Tests.Users;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
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

        protected override void ConfigureTestServices(IServiceCollection services)
        {
            base.ConfigureTestServices(services);
            services.AddScoped<ITenantLoginSettingsClient, DefaultSettings>();
            services.AddScoped<ITenantEffectivePermissionResolver, Passthrough>();
            services.AddScoped<IPlatformAdministratorStatusClient, RecordByEmail>();
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
    [InlineData(true)]  // the tenant that is also the platform tenant
    [InlineData(false)] // any other tenant: what makes it a platform account is the account, not the tenant
    public async Task A_platform_account_gets_the_generic_401_at_the_tenant_door_and_nothing_is_minted_or_written(bool inThePlatformTenant)
    {
        var tenant = inThePlatformTenant ? BusinessAndPlatformTenant : Guid.NewGuid();
        var account = await SeedAsync(tenant, platform: true, roles: inThePlatformTenant ? ["SuperAdmin"] : []);
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
    [Fact]
    public async Task Mfa_verification_refuses_a_platform_account_but_not_an_ordinary_user_of_the_same_tenant()
    {
        var platform = NewUser(platform: true);
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
    [Fact]
    public async Task A_tenant_session_of_a_platform_account_is_not_refreshed_an_ordinary_one_and_the_platform_one_are()
    {
        var platformAccount = await SeedAsync(BusinessAndPlatformTenant, platform: true, roles: ["SuperAdmin"]);
        var ordinary = await SeedAsync(BusinessAndPlatformTenant);
        Host.Records[platformAccount.Email] = PlatformAdministratorRecordAnswer.ConfirmedAs("platform_admin");

        var refused = await RefreshAsync(platformAccount, BusinessAndPlatformTenant, "tenant_user");
        var allowed = await RefreshAsync(ordinary, BusinessAndPlatformTenant, "tenant_user");
        var platformSession = await RefreshAsync(platformAccount, BusinessAndPlatformTenant, "platform_admin");

        Assert.Equal(HttpStatusCode.Unauthorized, refused.Response.StatusCode);
        Assert.Contains("Invalid refresh token.", refused.Body, StringComparison.Ordinal);
        Assert.Null((await RefreshRowAsync(refused.RawRefreshToken))!.RevokedAt);
        Assert.Equal(HttpStatusCode.OK, allowed.Response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, platformSession.Response.StatusCode);
    }

    /// <summary>Registration mints a token only for the account it CREATES. An address that belongs to a platform account is
    /// "already in use" — the same 409 an ordinary user's address gets — and creates, mints and writes nothing.</summary>
    [Fact]
    public async Task Registering_the_address_of_a_platform_account_is_the_ordinary_409_and_mints_nothing()
    {
        var platformAccount = await SeedAsync(BusinessAndPlatformTenant, platform: true);
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
    [Fact]
    public async Task The_tenant_forced_change_refuses_a_platform_account_but_not_an_ordinary_user()
    {
        var platformAccount = await SeedAsync(BusinessAndPlatformTenant, platform: true, mustChange: true);
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
    [Fact]
    public async Task The_forced_change_handler_itself_refuses_a_platform_account_before_looking_at_its_password()
    {
        var platform = NewUser(platform: true);
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
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_tenant_actor_does_not_see_platform_accounts_in_the_list_the_search_or_the_export(bool inThePlatformTenant)
    {
        var tenant = inThePlatformTenant ? BusinessAndPlatformTenant : Guid.NewGuid();
        var tag = $"Q{Guid.NewGuid():N}"[..12]; // a first name of its own: the search finds these three and no other test's users
        var admin = await SeedAsync(tenant, firstName: tag);
        var colleague = await SeedAsync(tenant, firstName: tag);
        var platformAccount = await SeedAsync(tenant, platform: true, firstName: tag);
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
    [Fact]
    public async Task A_tenant_actor_gets_the_answer_of_an_absent_user_for_a_platform_account_on_every_user_route_and_changes_nothing()
    {
        var admin = await SeedAsync(BusinessAndPlatformTenant);
        var platformAccount = await SeedAsync(BusinessAndPlatformTenant, platform: true, roles: ["SuperAdmin"]);
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
        Assert.Equal("platform_admin", after.PlatformActorType);
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
    [Fact]
    public async Task A_platform_door_actor_still_sees_and_reads_platform_accounts()
    {
        var actor = await SeedAsync(BusinessAndPlatformTenant, platform: true, roles: ["SuperAdmin"]);
        var other = await SeedAsync(BusinessAndPlatformTenant, platform: true);
        var user = await UserAsync(actor.Id, BusinessAndPlatformTenant);
        string token;
        using (var scope = _host.Factory.Services.CreateScope())
        {
            token = scope.ServiceProvider.GetRequiredService<ITokenService>().GeneratePlatformAccessToken(
                user!.Id, user.Email, user.FirstName, user.LastName, BusinessAndPlatformTenant, "platform_admin", ["SuperAdmin"],
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
    [Fact]
    public async Task The_platform_door_still_signs_in_and_refreshes()
    {
        var account = await SeedAsync(BusinessAndPlatformTenant, platform: true, roles: ["SuperAdmin"]);
        Host.Records[account.Email] = PlatformAdministratorRecordAnswer.ConfirmedAs("platform_admin");
        using var anonymous = _host.Client(tenantHeader: BusinessAndPlatformTenant);

        var login = await anonymous.PostAsJsonAsync("api/platform-auth/login", new { email = account.Email, password = Password, rememberMe = false });
        var loginBody = await login.Content.ReadAsStringAsync();

        Assert.True(login.StatusCode == HttpStatusCode.OK, $"{(int)login.StatusCode}: {loginBody}");
        using var json = JsonDocument.Parse(loginBody);
        var data = json.RootElement.GetProperty("data");
        var accessToken = data.GetProperty("accessToken").GetString()!;
        var refreshToken = data.GetProperty("refreshToken").GetString()!;
        Assert.Equal("platform_admin", new JwtSecurityTokenHandler().ReadJwtToken(accessToken).Claims.Single(c => c.Type == "actor_type").Value);

        var refresh = await anonymous.PostAsJsonAsync("api/auth/refresh-token", new { accessToken, refreshToken });
        Assert.True(refresh.StatusCode == HttpStatusCode.OK, $"{(int)refresh.StatusCode}: {await refresh.Content.ReadAsStringAsync()}");
    }

    // ── the visibility itself ───────────────────────────────────────────────────────────────────────────

    /// <summary>Only a tenant actor's authenticated principal hides platform accounts: a platform-door actor, an anonymous request
    /// and a call with no request at all (internal service calls, background work) are not hidden from anything.</summary>
    [Theory]
    [InlineData("tenant_user", true, true)]
    [InlineData("TENANT_USER", true, true)]
    [InlineData("platform_admin", true, false)]
    [InlineData("partner_admin", true, false)]
    [InlineData(null, true, false)]
    [InlineData("tenant_user", false, false)]
    public void Only_an_authenticated_tenant_actor_hides_platform_accounts(string? actorType, bool authenticated, bool hides)
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

    private async Task<string> MintPlatformTokenAsync(Account account, string actorType)
    {
        var user = await UserAsync(account.Id, account.TenantId);
        using var scope = _host.Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ITokenService>().GeneratePlatformAccessToken(
            user!.Id, user.Email, user.FirstName, user.LastName, account.TenantId, actorType, ["SuperAdmin"], [], 60, false);
    }

    private static User NewUser(bool platform)
    {
        var user = new User($"mfa.{Guid.NewGuid():N}@bl521.test", "hash:x", "Door", "Probe", BusinessAndPlatformTenant);
        user.ConfirmEmail();
        if (platform)
        {
            user.SetPlatformActorType("platform_admin");
        }

        return user;
    }

    private async Task<Account> SeedAsync(Guid tenantId, bool platform = false, string[]? roles = null, bool mustChange = false, string firstName = "Door")
    {
        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(tenantId);
        var user = new User($"door.{Guid.NewGuid():N}@bl521.test", sp.GetRequiredService<IPasswordHasher>().Hash(Password), firstName, "Probe", tenantId);
        user.ConfirmEmail();
        if (platform)
        {
            user.SetPlatformActorType("platform_admin");
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
                    new NoAudit())
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

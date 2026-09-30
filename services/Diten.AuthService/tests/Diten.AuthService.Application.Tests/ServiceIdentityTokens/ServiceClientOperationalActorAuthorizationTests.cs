using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using Diten.AuthService.Api.Configuration;
using Diten.AuthService.Api.Services.ServiceIdentityTokens;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Infrastructure.Settings;
using Diten.BuildingBlocks.Security.Secrets;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Diten.AuthService.Application.Tests.ServiceIdentityTokens;

public sealed class ServiceClientOperationalActorAuthorizationTests
{
    [Theory]
    [InlineData("iat")]
    [InlineData("nbf")]
    [InlineData("legacy")]
    public async Task Fresh_signed_platform_actor_requires_current_persisted_grant(string timeShape)
    {
        var harness = new OperationalActorHarness();
        var result = await harness.Authorizer().AuthorizeAsync(harness.Token(timeShape), OperationalTestData.Marker, CancellationToken.None);
        Assert.Equal(harness.User.Id, result.UserId);
        Assert.Equal(OperationalTestData.PlatformTenant, result.TenantId);
        Assert.Equal("platform_admin", result.ActorType);
        Assert.True(harness.Reads >= 6);
        Assert.Equal(0, harness.Writes);
    }

    [Theory]
    [InlineData("duplicate-sub")]
    [InlineData("duplicate-tenant")]
    [InlineData("tenant-alias")]
    [InlineData("subject-alias")]
    [InlineData("foreign-tenant")]
    [InlineData("actor")]
    [InlineData("duplicate-actor")]
    [InlineData("password")]
    [InlineData("permission")]
    [InlineData("noncanonical-sub")]
    [InlineData("stale")]
    [InlineData("future")]
    [InlineData("short-expiry")]
    [InlineData("long-lifetime")]
    [InlineData("bad-signature")]
    [InlineData("wrong-audience")]
    [InlineData("duplicate-iat")]
    public async Task Ambiguous_or_stale_signed_claims_are_rejected_before_repository_reads(string fault)
    {
        var harness = new OperationalActorHarness();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => harness.Authorizer().AuthorizeAsync(harness.Token(fault: fault), OperationalTestData.Marker, CancellationToken.None));
        Assert.Equal(0, harness.Reads);
    }

    [Theory]
    [InlineData("deleted-user")]
    [InlineData("inactive-user")]
    [InlineData("wrong-persisted-actor")]
    [InlineData("force-password")]
    [InlineData("deleted-permission")]
    [InlineData("tenant-permission")]
    [InlineData("deleted-role")]
    [InlineData("absent-user-role")]
    [InlineData("deleted-grant")]
    [InlineData("foreign-grant")]
    public async Task Claimed_permission_never_substitutes_for_current_database_authority(string fault)
    {
        var harness = new OperationalActorHarness();
        switch (fault)
        {
            case "deleted-user": harness.User.IsDeleted = true; break;
            case "inactive-user": harness.User.Deactivate(); break;
            case "wrong-persisted-actor": harness.User.SetPlatformActorType("tenant_user"); break;
            case "force-password": harness.User.RequirePasswordChange(null); break;
            case "deleted-permission": harness.Permission.IsDeleted = true; break;
            case "tenant-permission": harness.Permission.SetScope(PermissionScope.Tenant); break;
            case "deleted-role": harness.Role.IsDeleted = true; break;
            case "absent-user-role": harness.HasUserRole = false; break;
            case "deleted-grant": harness.Grant.IsDeleted = true; break;
            case "foreign-grant": harness.Grant = RolePermission.SystemGrant(harness.Role.Id, harness.Permission.Id, Guid.NewGuid(), "fixture"); break;
        }
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => harness.Authorizer().AuthorizeAsync(harness.Token(), OperationalTestData.Marker, CancellationToken.None));
        Assert.Equal(0, harness.Writes);
    }

    [Fact]
    public async Task Marker_attempt_is_one_shot_even_after_failure_and_options_are_immutable()
    {
        var harness = new OperationalActorHarness();
        var authorizer = harness.Authorizer();
        harness.Options.OperationalMarkerSha256 = new string('0', 64);
        Assert.NotNull(await authorizer.AuthorizeAsync(harness.Token(), OperationalTestData.Marker, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => authorizer.AuthorizeAsync(harness.Token(), OperationalTestData.Marker, CancellationToken.None));
        var rejected = harness.Authorizer();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => rejected.AuthorizeAsync(harness.Token(), "wrong-marker", CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => rejected.AuthorizeAsync(harness.Token(), OperationalTestData.Marker, CancellationToken.None));
    }
}

internal sealed class OperationalActorHarness
{
    internal readonly User User;
    internal readonly Role Role;
    internal readonly Permission Permission;
    internal RolePermission Grant;
    internal bool HasUserRole = true;
    internal int Reads;
    internal int Writes;
    internal readonly byte[] Key = System.Text.Encoding.UTF8.GetBytes(Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)));
    internal readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    internal readonly ServiceClientOperationalProvisioningOptions Options = OperationalTestData.Options(OperationalTestData.Request());
    internal OperationalActorHarness()
    {
        User = new User("fixture@example.invalid", "not-a-login-credential", "Fixture", "Operator", OperationalTestData.PlatformTenant) { Id = OperationalTestData.ActorId };
        User.SetUserName("fixture-operator"); User.ConfirmEmail(); User.SetPlatformActorType("platform_admin");
        Role = new Role("FixtureOperator", "Fixture Operator", null, OperationalTestData.PlatformTenant);
        var parts = ServiceClientOperationalActorAuthorizer.RequiredPermission.Split('.');
        Permission = new Permission(parts[0], parts[1], parts[2], "Fixture provision", null, scope: PermissionScope.PlatformAdmin);
        Grant = RolePermission.SystemGrant(Role.Id, Permission.Id, OperationalTestData.PlatformTenant, "fixture");
    }

    internal ServiceClientOperationalActorAuthorizer Authorizer() => new(
        Microsoft.Extensions.Options.Options.Create(new JwtSettings { Issuer = "fixture-issuer", Audience = "fixture-audience" }),
        Microsoft.Extensions.Options.Options.Create(Options), new TestKeys(Key), Proxy<IUserRepository>(), Proxy<IUserRoleRepository>(),
        Proxy<IRoleRepository>(), Proxy<IRolePermissionRepository>(), Proxy<IPermissionRepository>(), new FixedOperationalClock(Now));

    private T Proxy<T>() where T : class => OperationalRepositoryProxy.Create<T>((method, args) =>
    {
        if (!(method.StartsWith("Get", StringComparison.Ordinal) || method == "ExistsAsync")) { Writes++; throw new InvalidOperationException("Unexpected write."); }
        Reads++;
        return method switch
        {
            "GetByIdAndTenantAsync" => Task.FromResult<User?>(User),
            "GetByKeyAsync" => Task.FromResult<Permission?>(Permission),
            "GetRolesByUserAsync" => Task.FromResult<IEnumerable<string>>([Role.Name]),
            "GetByNameAndTenantAsync" => Task.FromResult<Role?>(Role),
            "ExistsAsync" => Task.FromResult(HasUserRole),
            "GetByRoleAsync" => Task.FromResult<IReadOnlyList<RolePermission>>([Grant]),
            _ => throw new InvalidOperationException("Unexpected repository call: " + method)
        };
    });

    internal string Token(string timeShape = "iat", string? fault = null)
    {
        var issued = Now.AddSeconds(-10);
        var expires = issued.AddMinutes(15);
        if (fault == "stale") issued = Now.AddSeconds(-301);
        if (fault == "future") issued = Now.AddSeconds(1);
        if (fault == "short-expiry") expires = Now.AddSeconds(20);
        if (fault == "long-lifetime") expires = issued.AddMinutes(16);
        var claims = new List<Claim>
        {
            new("sub", fault == "noncanonical-sub" ? User.Id.ToString("N") : User.Id.ToString()),
            new("tenant_id", (fault == "foreign-tenant" ? Guid.NewGuid() : OperationalTestData.PlatformTenant).ToString()),
            new("actor_type", fault == "actor" ? "tenant_user" : "platform_admin"),
            new("pwd_change_required", fault == "password" ? "true" : "false"),
            new("permission", fault == "permission" ? "unrelated" : ServiceClientOperationalActorAuthorizer.RequiredPermission),
            new("exp", expires.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };
        if (timeShape == "iat") claims.Add(new("iat", issued.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64));
        if (timeShape == "nbf") claims.Add(new("nbf", issued.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64));
        if (fault == "duplicate-sub") claims.Add(new("sub", User.Id.ToString()));
        if (fault == "duplicate-tenant") claims.Add(new("tenant_id", OperationalTestData.PlatformTenant.ToString()));
        if (fault == "duplicate-actor") claims.Add(new("actor_type", "platform_admin"));
        if (fault == "duplicate-iat") claims.Add(new("iat", issued.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64));
        if (fault == "tenant-alias") claims.Add(new("tenantId", Guid.NewGuid().ToString()));
        if (fault == "subject-alias") claims.Add(new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));
        var key = fault == "bad-signature" ? RandomNumberGenerator.GetBytes(64) : Key;
        var token = new JwtSecurityToken("fixture-issuer", fault == "wrong-audience" ? "other" : "fixture-audience", claims,
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    private sealed class TestKeys(byte[] bytes) : ISecretRotationResolver
    {
        public SecurityKey GetCurrentSigningKey() => new SymmetricSecurityKey(bytes);
        public IReadOnlyList<SecurityKey> GetValidationKeys() => [GetCurrentSigningKey()];
    }
}

public class OperationalRepositoryProxy : DispatchProxy
{
    private Func<string, object?[]?, object?> _invoke = null!;
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => _invoke(targetMethod!.Name, args);
    internal static T Create<T>(Func<string, object?[]?, object?> invoke) where T : class
    {
        var proxy = Create<T, OperationalRepositoryProxy>();
        ((OperationalRepositoryProxy)(object)proxy)._invoke = invoke;
        return proxy;
    }
}

internal sealed class FixedOperationalClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

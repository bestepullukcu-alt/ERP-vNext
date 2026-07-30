using System.IdentityModel.Tokens.Jwt;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Infrastructure.Services;
using Diten.AuthService.Infrastructure.Settings;
using Diten.BuildingBlocks.Security.Secrets;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Diten.AuthService.Application.Tests.Roles;

public sealed class PpmEntitlementTokenClaimTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private const string PermissionKey = "ppm.projects.read";

    [Fact]
    public async Task Re_entitlement_preserves_current_explicit_grant_and_current_membership_claim()
    {
        var permission = new Permission("ppm", "projects", "read", "Read projects", null);
        var (sync, grants, roles) = PpmEntitlementTestHarness.Create(TenantId, [permission]);
        var adminRoleId = roles.IdOf("Admin");
        grants.Rows.Add(RolePermission.ManualGrant(adminRoleId, permission.Id, TenantId, "actor"));

        await sync.RevokeModuleAsync(TenantId, "PPM", "sync");
        await sync.GrantModuleAsync(TenantId, "PPM", "sync");

        Assert.Contains(PermissionKey, TokenPermissions(grants.Rows, [adminRoleId], [permission]));
    }

    [Fact]
    public async Task Re_entitlement_does_not_reconstruct_deleted_explicit_grant()
    {
        var permission = new Permission("ppm", "projects", "read", "Read projects", null);
        var (sync, grants, roles) = PpmEntitlementTestHarness.Create(TenantId, [permission]);

        await sync.GrantModuleAsync(TenantId, "PPM", "sync");

        Assert.DoesNotContain(PermissionKey, TokenPermissions(grants.Rows, [roles.IdOf("Admin")], [permission]));
    }

    [Fact]
    public async Task Re_entitlement_does_not_reconstruct_removed_role_membership_or_expose_claim()
    {
        var permission = new Permission("ppm", "projects", "read", "Read projects", null);
        var (sync, grants, roles) = PpmEntitlementTestHarness.Create(TenantId, [permission]);
        grants.Rows.Add(RolePermission.ManualGrant(roles.IdOf("Admin"), permission.Id, TenantId, "actor"));
        var currentMembershipRoleIds = Array.Empty<Guid>();

        await sync.GrantModuleAsync(TenantId, "PPM", "sync");

        Assert.DoesNotContain(PermissionKey, TokenPermissions(grants.Rows, currentMembershipRoleIds, [permission]));
    }

    [Fact]
    public async Task Entitlement_alone_produces_no_ppm_permission_claim()
    {
        var permission = new Permission("ppm", "projects", "read", "Read projects", null);
        var (sync, grants, roles) = PpmEntitlementTestHarness.Create(TenantId, [permission]);

        await sync.GrantModuleWithKeysAsync(TenantId, "PPM", [PermissionKey], "sync");

        Assert.DoesNotContain(PermissionKey, TokenPermissions(grants.Rows, [roles.IdOf("Admin")], [permission]));
    }

    private static IReadOnlyList<string> TokenPermissions(
        IReadOnlyList<RolePermission> grants,
        IReadOnlyCollection<Guid> currentRoleIds,
        IReadOnlyList<Permission> catalog)
    {
        var permissionIds = grants
            .Where(x => x.TenantId == TenantId && currentRoleIds.Contains(x.RoleId) && !x.IsDeleted)
            .Select(x => x.PermissionId)
            .ToHashSet();
        var effective = catalog.Where(x => permissionIds.Contains(x.Id)).Select(x => x.Key).ToArray();
        var user = new User("ppm-test@diten.test", "hash", "PPM", "Tester", TenantId);
        var token = CreateTokenService().GenerateAccessToken(user, ["Admin"], effective);
        return new JwtSecurityTokenHandler().ReadJwtToken(token).Claims
            .Where(x => x.Type == "permission")
            .Select(x => x.Value)
            .ToArray();
    }

    private static TokenService CreateTokenService() => new(
        Options.Create(new JwtSettings
        {
            Secret = "ppm-test-secret-that-is-long-enough-for-hs256",
            Issuer = "tests",
            Audience = "tests",
            AccessTokenExpirationMinutes = 5
        }),
        new NoOpRotationResolver());

    private sealed class NoOpRotationResolver : ISecretRotationResolver
    {
        public SecurityKey GetCurrentSigningKey() => throw new NotSupportedException();
        public IReadOnlyList<SecurityKey> GetValidationKeys() => [];
    }
}

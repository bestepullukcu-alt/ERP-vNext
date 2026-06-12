using System.Reflection;
using Diten.AuthService.Api.Controllers;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Roles.Handlers.QueryHandlers;
using Diten.AuthService.Application.Features.Roles.Queries;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.AuthService.Application.Tests.Roles;

// S4 / AG-INFRA-COMPLETION (BE-A) — GET /api/roles/{id}/permissions read endpoint.
public sealed class GetRolePermissionsQueryHandlerTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static List<Permission> Catalog() =>
    [
        new("auth", "users", "read", "Read User", null),
        new("mdm", "legal-entities", "read", "Read Legal Entity", null),
        new("mdm", "legal-entities", "delete", "Delete Legal Entity", null)
    ];

    [Fact]
    public async Task Returns_role_id_and_assigned_permissions_with_canonical_key_and_grant_source()
    {
        var catalog = Catalog();
        var role = SystemRole("Admin", TenantA);
        var rolePerms = new FakeRolePermissionRepository();
        rolePerms.Seed(RolePermission.SystemGrant(role.Id, catalog[0].Id, TenantA, "system"));         // auth.users.read
        rolePerms.Seed(RolePermission.ModuleGrant(role.Id, catalog[2].Id, TenantA, "entitlement-sync", "mdm")); // mdm...delete
        rolePerms.Seed(RolePermission.ManualGrant(role.Id, catalog[1].Id, TenantA, "operator"));        // mdm...read

        var handler = Build(role, rolePerms, catalog, TenantA);
        var result = await handler.Handle(new GetRolePermissionsQuery(role.Id), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(role.Id, result.Data!.RoleId);

        // Ordered by canonical key.
        Assert.Equal(
            new[] { "auth.users.read", "mdm.legal-entities.delete", "mdm.legal-entities.read" },
            result.Data.Permissions.Select(p => p.Key).ToList());

        var authRead = result.Data.Permissions.Single(p => p.Key == "auth.users.read");
        Assert.Equal("System", authRead.GrantSource);
        Assert.Null(authRead.SourceModuleCode);

        var mdmDelete = result.Data.Permissions.Single(p => p.Key == "mdm.legal-entities.delete");
        Assert.Equal("Module", mdmDelete.GrantSource);
        Assert.Equal("mdm", mdmDelete.SourceModuleCode);

        var mdmRead = result.Data.Permissions.Single(p => p.Key == "mdm.legal-entities.read");
        Assert.Equal("Manual", mdmRead.GrantSource);
    }

    [Fact]
    public async Task Empty_role_returns_empty_list()
    {
        var role = SystemRole("Viewer", TenantA);
        var handler = Build(role, new FakeRolePermissionRepository(), Catalog(), TenantA);

        var result = await handler.Handle(new GetRolePermissionsQuery(role.Id), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Empty(result.Data!.Permissions);
    }

    [Fact]
    public async Task Role_from_another_tenant_is_not_found_no_cross_tenant_leak()
    {
        var role = SystemRole("Admin", TenantA);
        var rolePerms = new FakeRolePermissionRepository();
        rolePerms.Seed(RolePermission.SystemGrant(role.Id, Catalog()[0].Id, TenantA, "system"));

        // Caller is in TenantB; the role belongs to TenantA.
        var handler = Build(role, rolePerms, Catalog(), callerTenant: TenantB);

        var result = await handler.Handle(new GetRolePermissionsQuery(role.Id), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
        Assert.Null(result.Data);
    }

    [Fact]
    public void Endpoint_is_read_only_http_get_and_guarded_by_auth_roles_read()
    {
        var method = typeof(RolesController).GetMethod(nameof(RolesController.GetPermissions))!;

        Assert.NotNull(method.GetCustomAttribute<HttpGetAttribute>());
        Assert.Null(method.GetCustomAttribute<HttpPostAttribute>());
        Assert.Null(method.GetCustomAttribute<HttpPutAttribute>());
        Assert.Null(method.GetCustomAttribute<HttpDeleteAttribute>());

        var permission = method.GetCustomAttribute<HasPermissionAttribute>();
        Assert.NotNull(permission);
        Assert.Equal("Permission:auth.roles.read", permission!.Policy);
    }

    // ── harness ──

    private static Role SystemRole(string name, Guid tenantId)
    {
        var role = new Role(name, name, null, tenantId);
        role.MarkAsSystem();
        return role;
    }

    private static GetRolePermissionsQueryHandler Build(Role role, FakeRolePermissionRepository rolePerms, List<Permission> catalog, Guid callerTenant)
    {
        var tenant = new TestTenantContext();
        tenant.SetTenant(callerTenant);
        return new GetRolePermissionsQueryHandler(
            new FakeRoleRepository(role),
            rolePerms,
            new FakePermissionRepository(catalog),
            tenant);
    }

    private sealed class FakeRoleRepository(Role role) : IRoleRepository
    {
        public Task<Role?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct)
            => Task.FromResult(role.Id == id && role.TenantId == tenantId ? role : null);

        public Task<Role?> GetByNameAndTenantAsync(string name, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<Role>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> CreateAsync(Role role, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> UpsertSystemRoleAsync(string name, string displayName, string? description, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> UpdateAsync(Role role, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakePermissionRepository(List<Permission> catalog) : IPermissionRepository
    {
        public Task<IEnumerable<Permission>> GetAllAsync(CancellationToken ct) => Task.FromResult<IEnumerable<Permission>>(catalog);
        public Task<Permission?> GetByIdAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<Permission?> GetByKeyAsync(string key, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<Permission>> GetByModuleAsync(string module, CancellationToken ct) => throw new NotSupportedException();
        public Task<Permission> CreateAsync(Permission permission, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeRolePermissionRepository : IRolePermissionRepository
    {
        private readonly List<RolePermission> _rows = [];
        public void Seed(RolePermission rp) => _rows.Add(rp);

        public Task<IReadOnlyList<RolePermission>> GetByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<RolePermission>>(
                _rows.Where(rp => rp.RoleId == roleId && rp.TenantId == tenantId && !rp.IsDeleted).ToList());

        public Task<IEnumerable<string>> GetPermissionsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<string>> GetPermissionsByRolesAsync(List<Guid> roleIds, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task AssignAsync(RolePermission rolePermission, CancellationToken ct) => throw new NotSupportedException();
        public Task RevokeAsync(Guid roleId, Guid permissionId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task RemoveByIdAsync(Guid id, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class TestTenantContext : ITenantContext
    {
        private Guid _tenantId;
        public Guid TenantId => IsResolved ? _tenantId : throw new InvalidOperationException("Tenant not resolved.");
        public bool IsResolved { get; private set; }
        public bool IsPlatformContext => false;
        public Guid? TargetTenantId => null;
        public void SetTenant(Guid tenantId) { _tenantId = tenantId; IsResolved = true; }
        public void SetPlatformContext(Guid targetTenantId) => SetTenant(targetTenantId);
    }
}

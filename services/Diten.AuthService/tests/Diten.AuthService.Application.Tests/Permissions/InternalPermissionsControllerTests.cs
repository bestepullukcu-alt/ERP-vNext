using Diten.AuthService.Api.Controllers;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.AuthService.Application.Tests.Permissions;

// MODULE-CATALOG AUTOMATION Phase 1 — internal upsert endpoint: auth gate, format gate, idempotency.
public sealed class InternalPermissionsControllerTests
{
    private const string ApiKey = "internal-key";
    private const string Header = "X-Internal-Api-Key";

    [Fact]
    public async Task Missing_api_key_is_unauthorized_and_no_permission_written()
    {
        var repo = new FakePermissionRepository();
        var controller = Build(repo, authorized: false);

        var result = await controller.Sync(new InternalPermissionsController.SyncPermissionRequest("goldenslim.records.read", null, null), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Empty(repo.Items);
    }

    [Theory]
    [InlineData("goldenslim.records")]   // 2 segments
    [InlineData("not a key")]
    [InlineData("goldenslim.records.read_all")]
    public async Task Malformed_key_is_bad_request_and_no_permission_written(string key)
    {
        var repo = new FakePermissionRepository();
        var controller = Build(repo, authorized: true);

        var result = await controller.Sync(new InternalPermissionsController.SyncPermissionRequest(key, null, null), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(repo.Items);
    }

    [Fact]
    public async Task First_sync_creates_permission()
    {
        var repo = new FakePermissionRepository();
        var controller = Build(repo, authorized: true);

        var result = await controller.Sync(new InternalPermissionsController.SyncPermissionRequest("goldenslim.records.read", "Read Golden Slim", null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<InternalPermissionsController.SyncPermissionResponse>(ok.Value);
        Assert.Equal("goldenslim.records.read", body.Key);
        Assert.Equal("created", body.Status);
        var permission = Assert.Single(repo.Items);
        Assert.Equal("goldenslim.records.read", permission.Key);
        Assert.Equal("Read Golden Slim", permission.DisplayName);
    }

    [Fact]
    public async Task Repeated_sync_of_same_key_is_idempotent_single_record()
    {
        var repo = new FakePermissionRepository();
        var controller = Build(repo, authorized: true);

        await controller.Sync(new InternalPermissionsController.SyncPermissionRequest("goldenslim.records.read", "First", null), CancellationToken.None);
        var second = await controller.Sync(new InternalPermissionsController.SyncPermissionRequest("GOLDENSLIM.records.read", "Second", "desc"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(second);
        var body = Assert.IsType<InternalPermissionsController.SyncPermissionResponse>(ok.Value);
        Assert.Equal("updated", body.Status);
        // Same key (case-insensitive) → no duplicate row; display metadata refreshed.
        var permission = Assert.Single(repo.Items);
        Assert.Equal("goldenslim.records.read", permission.Key);
        Assert.Equal("Second", permission.DisplayName);
        Assert.Equal("desc", permission.Description);
    }

    [Fact]
    public async Task First_sync_grants_new_permission_to_full_catalog_role()
    {
        var repo = new FakePermissionRepository();
        var grant = new FakeFullCatalogPermissionGrantService();
        var controller = Build(repo, authorized: true, grant);

        await controller.Sync(new InternalPermissionsController.SyncPermissionRequest("platform.workflow.definitions.view", "View", null), CancellationToken.None);

        var created = Assert.Single(repo.Items);
        var grantedId = Assert.Single(grant.GrantedPermissionIds);
        Assert.Equal(created.Id, grantedId);
    }

    [Fact]
    public async Task Repeated_sync_does_not_grant_again()
    {
        var repo = new FakePermissionRepository();
        var grant = new FakeFullCatalogPermissionGrantService();
        var controller = Build(repo, authorized: true, grant);

        await controller.Sync(new InternalPermissionsController.SyncPermissionRequest("platform.workflow.definitions.view", "View", null), CancellationToken.None);
        await controller.Sync(new InternalPermissionsController.SyncPermissionRequest("platform.workflow.definitions.view", "View again", null), CancellationToken.None);

        // Grant only fires on first-time creation; the second sync is an "updated" no-op for grants.
        Assert.Single(grant.GrantedPermissionIds);
    }

    [Fact]
    public async Task GetModules_without_api_key_is_unauthorized()
    {
        var controller = Build(new FakePermissionRepository(), authorized: false);

        var result = await controller.GetModules(CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task GetModules_returns_distinct_modules_with_counts_and_preserves_case()
    {
        var repo = new FakePermissionRepository();
        repo.Items.Add(new Permission("goldenslim", "records", "read", "x", null));
        repo.Items.Add(new Permission("goldenslim", "records", "create", "x", null));
        repo.Items.Add(new Permission("mdm", "legal-entities", "read", "x", null));
        var controller = Build(repo, authorized: true);

        var result = await controller.GetModules(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var modules = Assert.IsAssignableFrom<IEnumerable<InternalPermissionsController.PermissionModuleSummary>>(ok.Value).ToList();
        Assert.Equal(2, modules.Count);
        Assert.Contains(modules, m => m.Module == "goldenslim" && m.PermissionCount == 2);
        Assert.Contains(modules, m => m.Module == "mdm" && m.PermissionCount == 1);
    }

    // ── FEAT-CATALOG-PERM-DELETE-SYNC — DELETE endpoint ──────────────────────────

    [Fact]
    public async Task Delete_unknown_key_is_idempotent_no_content()
    {
        var repo = new FakePermissionRepository();
        var rolePerms = new FakeRolePermissionRepository();
        var controller = Build(repo, authorized: true, rolePerms: rolePerms);

        var result = await controller.Delete("goldenslim.records.read", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Null(rolePerms.RemovedPermissionId); // nothing to clear
    }

    [Fact]
    public async Task Delete_system_permission_is_conflict_and_not_removed()
    {
        var repo = new FakePermissionRepository();
        // A plain Permission is IsSystem=true (seeded/system) — the catalog does not own it.
        repo.Items.Add(new Permission("auth", "users", "read", "Read users", null));
        var rolePerms = new FakeRolePermissionRepository();
        var controller = Build(repo, authorized: true, rolePerms: rolePerms);

        var result = await controller.Delete("auth.users.read", CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Single(repo.Items);                   // not deleted
        Assert.Null(rolePerms.RemovedPermissionId);  // grants untouched
    }

    [Fact]
    public async Task Delete_user_defined_permission_clears_grants_deletes_and_audits()
    {
        var repo = new FakePermissionRepository();
        var permission = new Permission("goldenslim", "records", "read", "Read", null);
        permission.MarkAsUserDefined(); // catalog-sourced → deletable
        repo.Items.Add(permission);
        var rolePerms = new FakeRolePermissionRepository();
        var audit = new FakeRbacAuditRecorder();
        var controller = Build(repo, authorized: true, rolePerms: rolePerms, audit: audit);

        var result = await controller.Delete("goldenslim.records.read", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.True(Assert.Single(repo.Items).IsDeleted);           // permission soft-deleted
        Assert.Equal(permission.Id, rolePerms.RemovedPermissionId); // its grants cleared first
        Assert.Equal("permission_catalog_removed", audit.EventName); // audited
    }

    [Fact]
    public async Task Sync_delete_resync_reactivates_without_duplicate_and_regrants()
    {
        var repo = new FakePermissionRepository();
        var grant = new FakeFullCatalogPermissionGrantService();
        var rolePerms = new FakeRolePermissionRepository();
        var controller = Build(repo, authorized: true, grant, rolePerms);

        // 1) create
        await controller.Sync(new InternalPermissionsController.SyncPermissionRequest("goldenslim.records.read", "Read", null), CancellationToken.None);
        Assert.Single(repo.Items);
        Assert.Single(grant.GrantedPermissionIds);

        // 2) delete (soft)
        Assert.IsType<NoContentResult>(await controller.Delete("goldenslim.records.read", CancellationToken.None));

        // 3) re-sync → REACTIVATE, no duplicate insert (previously E11000 → 500)
        var resync = await controller.Sync(new InternalPermissionsController.SyncPermissionRequest("goldenslim.records.read", "Read again", null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(resync);
        var body = Assert.IsType<InternalPermissionsController.SyncPermissionResponse>(ok.Value);
        Assert.Equal("reactivated", body.Status);
        var permission = Assert.Single(repo.Items);       // still ONE doc — no duplicate
        Assert.False(permission.IsDeleted);               // revived
        Assert.False(permission.IsSystem);                // stays catalog-owned (user-defined)
        Assert.Equal("Read again", permission.DisplayName);
        Assert.Equal(2, grant.GrantedPermissionIds.Count); // re-granted to the full-catalog role
    }

    [Fact]
    public async Task Sync_delete_resync_delete_full_cycle_is_idempotent()
    {
        var repo = new FakePermissionRepository();
        var controller = Build(repo, authorized: true);

        await controller.Sync(new InternalPermissionsController.SyncPermissionRequest("goldenslim.records.read", "Read", null), CancellationToken.None);
        Assert.IsType<NoContentResult>(await controller.Delete("goldenslim.records.read", CancellationToken.None));

        var resync = await controller.Sync(new InternalPermissionsController.SyncPermissionRequest("goldenslim.records.read", "Read", null), CancellationToken.None);
        Assert.Equal("reactivated", Assert.IsType<InternalPermissionsController.SyncPermissionResponse>(Assert.IsType<OkObjectResult>(resync).Value).Status);

        // Delete again closes the cycle: soft-deleted once more, still a single doc.
        Assert.IsType<NoContentResult>(await controller.Delete("goldenslim.records.read", CancellationToken.None));
        Assert.True(Assert.Single(repo.Items).IsDeleted);
    }

    [Fact]
    public async Task Delete_without_api_key_is_unauthorized()
    {
        var controller = Build(new FakePermissionRepository(), authorized: false);

        var result = await controller.Delete("goldenslim.records.read", CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    private static InternalPermissionsController Build(
        FakePermissionRepository repo,
        bool authorized,
        IFullCatalogPermissionGrantService? grant = null,
        IRolePermissionRepository? rolePerms = null,
        IRbacAuditRecorder? audit = null)
    {
        var controller = new InternalPermissionsController(
            new FakeInternalEventAuthService(authorized ? ApiKey : null),
            repo,
            grant ?? new FakeFullCatalogPermissionGrantService(),
            rolePerms ?? new FakeRolePermissionRepository(),
            audit ?? new FakeRbacAuditRecorder(),
            NullLogger<InternalPermissionsController>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[Header] = ApiKey;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private sealed class FakeFullCatalogPermissionGrantService : IFullCatalogPermissionGrantService
    {
        public List<Guid> GrantedPermissionIds { get; } = [];

        public Task GrantToFullCatalogRolesAsync(Guid permissionId, CancellationToken ct)
        {
            GrantedPermissionIds.Add(permissionId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeInternalEventAuthService(string? expectedKey) : IInternalEventAuthService
    {
        public bool IsAuthorized(string? apiKeyHeaderValue) =>
            expectedKey is not null && string.Equals(expectedKey, apiKeyHeaderValue, StringComparison.Ordinal);
    }

    private sealed class FakePermissionRepository : IPermissionRepository
    {
        public List<Permission> Items { get; } = [];

        // Live query (matches production: soft-deleted rows are hidden).
        public Task<Permission?> GetByKeyAsync(string key, CancellationToken ct) =>
            Task.FromResult(Items.FirstOrDefault(p => p.Key == key.ToLowerInvariant() && !p.IsDeleted));

        // FIX-CATALOG-PERM-RESYNC-DUPKEY — includes soft-deleted rows (the unique key still owns them).
        public Task<Permission?> GetByKeyIncludingDeletedAsync(string key, CancellationToken ct) =>
            Task.FromResult(Items.FirstOrDefault(p => p.Key == key.ToLowerInvariant()));

        public Task<Permission> CreateAsync(Permission permission, CancellationToken ct)
        {
            Items.Add(permission);
            return Task.FromResult(permission);
        }

        // Mirror production ReplaceOneAsync(And(IsDeletedFilter, Id==...)): a soft-deleted doc is NOT matched, so a
        // filtered update never revives it (the bug FIX-CATALOG-PERM-REACTIVATE-PERSIST addresses — reactivation
        // must go through ReactivateAsync's Id-only update instead).
        public Task UpdateAsync(Permission permission, CancellationToken ct)
        {
            var index = Items.FindIndex(p => p.Id == permission.Id && !p.IsDeleted);
            if (index >= 0)
            {
                Items[index] = permission;
            }

            return Task.CompletedTask;
        }

        // FIX-CATALOG-PERM-REACTIVATE-PERSIST — Id-only revive (mirrors DeleteAsync), works on a soft-deleted doc.
        public Task ReactivateAsync(Guid id, string displayName, string? description, CancellationToken ct)
        {
            var p = Items.FirstOrDefault(x => x.Id == id);
            if (p is not null)
            {
                p.IsDeleted = false;
                p.MarkAsUserDefined();
                p.Update(displayName, description);
            }

            return Task.CompletedTask;
        }

        public Task<IEnumerable<Permission>> GetAllAsync(CancellationToken ct) => Task.FromResult<IEnumerable<Permission>>(Items);
        public Task<Permission?> GetByIdAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<Permission>> GetByModuleAsync(string module, CancellationToken ct) => throw new NotSupportedException();
        // Soft-delete (mirrors production GlobalRepositoryBase): the row stays, IsDeleted=true.
        public Task DeleteAsync(Guid id, CancellationToken ct)
        {
            var p = Items.FirstOrDefault(x => x.Id == id);
            if (p is not null) p.IsDeleted = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRolePermissionRepository : IRolePermissionRepository
    {
        public Guid? RemovedPermissionId { get; private set; }

        public Task<long> RemoveByPermissionIdAsync(Guid permissionId, CancellationToken ct)
        {
            RemovedPermissionId = permissionId;
            return Task.FromResult(2L);
        }

        public Task<IEnumerable<string>> GetPermissionsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<string>> GetPermissionsByRolesAsync(List<Guid> roleIds, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task AssignAsync(RolePermission rolePermission, CancellationToken ct) => throw new NotSupportedException();
        public Task RevokeAsync(Guid roleId, Guid permissionId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<RolePermission>> GetByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task RemoveByIdAsync(Guid id, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeRbacAuditRecorder : IRbacAuditRecorder
    {
        public string? EventName { get; private set; }

        public Task RecordAsync(string eventName, Guid tenantId, object metadata, CancellationToken ct = default)
        {
            EventName = eventName;
            return Task.CompletedTask;
        }
    }
}

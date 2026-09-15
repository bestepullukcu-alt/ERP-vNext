using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Roles.Commands;
using Diten.AuthService.Application.Features.Roles.Handlers.CommandHandlers;
using Diten.AuthService.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.AuthService.Application.Tests.Roles;

// BL-412 — a role created, edited or deleted by a person (POST / PUT / DELETE api/roles) used to leave CreatedBy empty
// and UpdatedBy unset; only authAuditLogs knew who did it. The document now names the acting user, taken from the same
// ICurrentUserAccessor the RBAC audit row uses. Real handlers, hand-written fakes (codebase convention).
public sealed class RoleActorStampTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ActorId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private const string OriginalCreator = "11111111-1111-1111-1111-111111111111";

    [Fact]
    public async Task Create_role_records_the_acting_user_as_CreatedBy()
    {
        var roles = new FakeRoleRepository(existing: null);
        var version = new FakeRoleAssignmentVersionService();
        var handler = CreateHandler(roles, version, ActorId);

        var result = await handler.Handle(new CreateRoleCommand("qa-lead", "QA Lead", null), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(201, result.StatusCode);
        var created = Assert.IsType<Role>(roles.Created);
        Assert.Equal(ActorId.ToString(), created.CreatedBy);
        Assert.Equal(1, version.IncrementCount);
    }

    [Fact]
    public async Task Create_role_without_an_authenticated_actor_is_refused_with_401_and_writes_nothing()
    {
        var roles = new FakeRoleRepository(existing: null);
        var version = new FakeRoleAssignmentVersionService();
        var handler = CreateHandler(roles, version, actor: null);

        var result = await handler.Handle(new CreateRoleCommand("qa-lead", "QA Lead", null), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(401, result.StatusCode);
        Assert.Null(roles.Created);
        Assert.Equal(0, version.IncrementCount);
    }

    [Fact]
    public async Task Update_role_records_the_acting_user_as_UpdatedBy_and_keeps_the_original_CreatedBy()
    {
        var existing = new Role("qa-lead", "QA Lead", null, TenantId) { CreatedBy = OriginalCreator };
        var roles = new FakeRoleRepository(existing);
        var version = new FakeRoleAssignmentVersionService();
        var handler = UpdateHandler(roles, version, ActorId);

        var result = await handler.Handle(new UpdateRoleCommand(existing.Id, "QA Lead (EU)", "regional"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        var updated = Assert.IsType<Role>(roles.Updated);
        Assert.Equal(ActorId.ToString(), updated.UpdatedBy);
        Assert.Equal(OriginalCreator, updated.CreatedBy);
        Assert.Equal(1, version.IncrementCount);
    }

    [Fact]
    public async Task Update_role_without_an_authenticated_actor_is_refused_with_401_and_writes_nothing()
    {
        var existing = new Role("qa-lead", "QA Lead", null, TenantId) { CreatedBy = OriginalCreator };
        var roles = new FakeRoleRepository(existing);
        var version = new FakeRoleAssignmentVersionService();
        var handler = UpdateHandler(roles, version, actor: null);

        var result = await handler.Handle(new UpdateRoleCommand(existing.Id, "QA Lead (EU)", "regional"), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(401, result.StatusCode);
        Assert.Null(roles.Updated);
        Assert.Null(existing.UpdatedBy);
        Assert.Equal("QA Lead", existing.DisplayName);
        Assert.Equal(0, version.IncrementCount);
    }

    // BL-412 F1 — the soft delete names who deleted the role: the actor reaches the repository's single atomic update.
    [Fact]
    public async Task Delete_role_passes_the_acting_user_to_the_soft_delete()
    {
        var existing = new Role("qa-lead", "QA Lead", null, TenantId) { CreatedBy = OriginalCreator };
        var roles = new FakeRoleRepository(existing);
        var version = new FakeRoleAssignmentVersionService();
        var handler = DeleteHandler(roles, version, ActorId);

        var result = await handler.Handle(new DeleteRoleCommand(existing.Id), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(204, result.StatusCode);
        Assert.Equal((existing.Id, TenantId, ActorId.ToString()), roles.DeletedCall);
        Assert.Equal(1, version.IncrementCount);
    }

    [Fact]
    public async Task Delete_role_without_an_authenticated_actor_is_refused_with_401_and_deletes_nothing()
    {
        var existing = new Role("qa-lead", "QA Lead", null, TenantId) { CreatedBy = OriginalCreator };
        var roles = new FakeRoleRepository(existing);
        var version = new FakeRoleAssignmentVersionService();
        var handler = DeleteHandler(roles, version, actor: null);

        var result = await handler.Handle(new DeleteRoleCommand(existing.Id), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(401, result.StatusCode);
        Assert.Null(roles.DeletedCall);
        Assert.Equal(0, version.IncrementCount);
    }

    private static TenantContext Tenant()
    {
        var context = new TenantContext();
        context.SetTenant(TenantId);
        return context;
    }

    private static CreateRoleCommandHandler CreateHandler(FakeRoleRepository roles, FakeRoleAssignmentVersionService version, Guid? actor)
        => new(roles, version, Tenant(), new NoOpRbacAuditRecorder(), new FakeCurrentUser(actor), NullLogger<CreateRoleCommandHandler>.Instance);

    private static UpdateRoleCommandHandler UpdateHandler(FakeRoleRepository roles, FakeRoleAssignmentVersionService version, Guid? actor)
        => new(roles, new FakeRolePermissionRepository(), version, Tenant(), new NoOpRbacAuditRecorder(), new FakeCurrentUser(actor));

    private static DeleteRoleCommandHandler DeleteHandler(FakeRoleRepository roles, FakeRoleAssignmentVersionService version, Guid? actor)
        => new(roles, version, Tenant(), new NoOpRbacAuditRecorder(), new FakeCurrentUser(actor), NullLogger<DeleteRoleCommandHandler>.Instance);

    // ── Minimal inline fakes ──

    private sealed class FakeRoleRepository(Role? existing) : IRoleRepository
    {
        public Role? Created { get; private set; }
        public Role? Updated { get; private set; }
        public (Guid id, Guid tenantId, string deletedBy)? DeletedCall { get; private set; }

        public Task<Role?> GetByNameAndTenantAsync(string name, Guid tenantId, CancellationToken ct) => Task.FromResult<Role?>(null);
        public Task<Role?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct) => Task.FromResult(existing);

        public Task<Role> CreateAsync(Role role, CancellationToken ct)
        {
            Created = role;
            return Task.FromResult(role);
        }

        public Task<Role> UpdateAsync(Role role, CancellationToken ct)
        {
            Updated = role;
            return Task.FromResult(role);
        }

        public Task DeleteAsync(Guid id, Guid tenantId, string deletedBy, CancellationToken ct)
        {
            DeletedCall = (id, tenantId, deletedBy);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<Role>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> UpsertSystemRoleAsync(string name, string displayName, string? description, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeRolePermissionRepository : IRolePermissionRepository
    {
        public Task<IEnumerable<string>> GetPermissionsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct)
            => Task.FromResult(Enumerable.Empty<string>());

        public Task AssignAsync(RolePermission rolePermission, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<RolePermission>> GetByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<string>> GetPermissionsByRolesAsync(List<Guid> roleIds, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task RevokeAsync(Guid roleId, Guid permissionId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task RemoveByIdAsync(Guid id, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<long> RemoveByPermissionIdAsync(Guid permissionId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class NoOpRbacAuditRecorder : IRbacAuditRecorder
    {
        public Task RecordAsync(string eventName, Guid tenantId, object metadata, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeCurrentUser(Guid? userId) : ICurrentUserAccessor
    {
        public Guid? UserId { get; } = userId;
    }
}

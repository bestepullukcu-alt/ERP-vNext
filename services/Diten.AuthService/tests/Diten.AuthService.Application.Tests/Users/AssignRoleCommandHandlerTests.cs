using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Commands;
using Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;
using Diten.AuthService.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.AuthService.Application.Tests.Users;

// BL-412 F1 — assigning a role to a user (POST api/users/{id}/roles) used to store AssignedBy "System", so the row
// claimed the system did it. It now names the acting user — the id ICurrentUserAccessor gives the user_role_assigned
// audit row. Measured: UsersController is the only dispatcher; system flows write UserRole directly (guarded by
// SystemGrantPathsActorGuardTests), so a missing actor is never a system call and nothing is written.
public sealed class AssignRoleCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ActorId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    [Fact]
    public async Task Assign_records_the_acting_user_not_System_as_AssignedBy_and_CreatedBy()
    {
        var (user, role) = Subject();
        var userRoles = new FakeUserRoleRepository();
        var version = new FakeRoleAssignmentVersionService();
        var handler = CreateHandler(user, role, userRoles, version, ActorId);

        var result = await handler.Handle(new AssignRoleCommand(user.Id, role.Id), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(204, result.StatusCode);
        var assignment = Assert.IsType<UserRole>(userRoles.Assigned);
        Assert.Equal((user.Id, role.Id, TenantId), (assignment.UserId, assignment.RoleId, assignment.TenantId));
        Assert.Equal(ActorId.ToString(), assignment.AssignedBy);
        Assert.Equal(ActorId.ToString(), assignment.CreatedBy);
        Assert.NotEqual("System", assignment.AssignedBy, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(1, version.IncrementCount);
    }

    [Fact]
    public async Task Assign_without_an_authenticated_actor_is_refused_with_401_and_writes_nothing()
    {
        var (user, role) = Subject();
        var userRoles = new FakeUserRoleRepository();
        var version = new FakeRoleAssignmentVersionService();
        var handler = CreateHandler(user, role, userRoles, version, actor: null);

        var result = await handler.Handle(new AssignRoleCommand(user.Id, role.Id), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(401, result.StatusCode);
        Assert.Null(userRoles.Assigned);
        Assert.Equal(0, version.IncrementCount);
    }

    private static (User user, Role role) Subject() =>
        (new User("subject@assign-role.invalid", "not-a-real-hash", "Sub", "Ject", TenantId), new Role("qa-lead", "QA Lead", null, TenantId));

    private static AssignRoleCommandHandler CreateHandler(
        User user,
        Role role,
        FakeUserRoleRepository userRoles,
        FakeRoleAssignmentVersionService version,
        Guid? actor)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(TenantId);

        return new AssignRoleCommandHandler(
            new InMemoryUserRepository([user]),
            new FakeRoleRepository(role),
            userRoles,
            version,
            tenantContext,
            new NoOpRbacAuditRecorder(),
            new FakeCurrentUser(actor),
            NullLogger<AssignRoleCommandHandler>.Instance);
    }

    // ── Minimal inline fakes (codebase convention: hand-written, no Moq) ──

    private sealed class FakeRoleRepository(Role role) : IRoleRepository
    {
        public Task<Role?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct)
            => Task.FromResult<Role?>(id == role.Id && tenantId == role.TenantId ? role : null);

        public Task<Role?> GetByNameAndTenantAsync(string name, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<Role>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> CreateAsync(Role role, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> UpsertSystemRoleAsync(string name, string displayName, string? description, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> UpdateAsync(Role role, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, Guid tenantId, string deletedBy, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeUserRoleRepository : IUserRoleRepository
    {
        public UserRole? Assigned { get; private set; }

        public Task<bool> ExistsAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct) => Task.FromResult(false);

        public Task AssignAsync(UserRole userRole, CancellationToken ct)
        {
            Assigned = userRole;
            return Task.CompletedTask;
        }

        public Task<IEnumerable<string>> GetRolesByUserAsync(Guid userId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task RevokeAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<Guid>> GetUserIdsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
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

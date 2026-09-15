using System.Security.Claims;
using System.Text.Json;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Auth.Commands;
using Diten.AuthService.Application.Features.Auth.Handlers.CommandHandlers;
using Diten.AuthService.Application.Tests.Users;
using Diten.AuthService.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.AuthService.Application.Tests.Auth;

// BL-412 (CT benchmark D6, 21 CFR Part 11 attributability) — anonymous self-registration has no current user.
// The default Viewer row is tenant policy's act, so it says "system" (lowercase, like every other system writer); the
// registration itself is the attributable act, so ONE audit event names the new user and says where the role came from.
public sealed class RegisterCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private const string Password = "Disposable#Register2026!";

    [Fact]
    public async Task Register_stamps_the_default_role_row_system_and_names_the_new_user_on_the_audit_event()
    {
        var viewer = new Role("Viewer", "Viewer", null, TenantId);
        var users = new InMemoryUserRepository([]);
        var userRoles = new RecordingUserRoleRepository();
        var audit = new RecordingAuthAuditService();
        var handler = CreateHandler(users, viewer, userRoles, audit);

        var result = await handler.Handle(Command("new.registrant@register.invalid"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(201, result.StatusCode);
        var created = Assert.Single(await users.GetAllByTenantAsync(TenantId, 1, 10, CancellationToken.None));

        var row = Assert.Single(userRoles.Assigned);
        Assert.Equal((created.Id, viewer.Id, TenantId), (row.UserId, row.RoleId, row.TenantId));
        Assert.Equal("system", row.AssignedBy);
        Assert.Equal("system", row.CreatedBy);
        Assert.NotEqual(created.Id.ToString(), row.AssignedBy, StringComparer.OrdinalIgnoreCase);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal("tenant_user_self_registered", entry.EventName);
        Assert.Equal(created.Id, entry.UserId);
        Assert.Equal(TenantId, entry.TenantId);

        using var metadata = JsonDocument.Parse(entry.Metadata);
        var root = metadata.RootElement;
        Assert.Equal(created.Id.ToString(), root.GetProperty("actorId").GetString());
        Assert.Equal("Viewer", root.GetProperty("defaultRole").GetString());
        Assert.Equal(viewer.Id.ToString(), root.GetProperty("defaultRoleId").GetString());
        Assert.Equal("system", root.GetProperty("roleAssignedBy").GetString());
        var reason = root.GetProperty("reason").GetString();
        Assert.Contains("self-registration", reason, StringComparison.Ordinal);
        Assert.Contains("default role Viewer", reason, StringComparison.Ordinal);
        Assert.Contains("tenant policy", reason, StringComparison.Ordinal);

        // IDs and the role name only: the registrant's email and name stay out of the audit metadata.
        Assert.DoesNotContain("new.registrant", entry.Metadata, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Registrant", entry.Metadata, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Register_with_an_email_already_in_use_writes_no_role_row_and_no_audit_event()
    {
        var viewer = new Role("Viewer", "Viewer", null, TenantId);
        var existing = new User("taken@register.invalid", "not-a-real-hash", "Al", "Ready", TenantId);
        var users = new InMemoryUserRepository([existing]);
        var userRoles = new RecordingUserRoleRepository();
        var audit = new RecordingAuthAuditService();
        var handler = CreateHandler(users, viewer, userRoles, audit);

        var result = await handler.Handle(Command("taken@register.invalid"), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Empty(userRoles.Assigned);
        Assert.Empty(audit.Entries);
    }

    private static RegisterCommand Command(string email) =>
        new(email, Password, "New", "Registrant", "203.0.113.10", "register-tests");

    private static RegisterCommandHandler CreateHandler(
        InMemoryUserRepository users,
        Role viewer,
        RecordingUserRoleRepository userRoles,
        RecordingAuthAuditService audit)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(TenantId);

        return new RegisterCommandHandler(
            users,
            new ViewerRoleRepository(viewer),
            userRoles,
            new UnexpectedRoleProvisioning(),
            new FakeTokenService(),
            new FakeRefreshTokenHasher(),
            new FakeRefreshTokenRepository(),
            new FakePasswordHasher(),
            new FixedTenantLoginSettingsClient(),
            new AcceptingPasswordPolicy(),
            tenantContext,
            audit,
            NullLogger<RegisterCommandHandler>.Instance);
    }

    // ── Minimal inline fakes (codebase convention: hand-written, no Moq) ──

    private sealed record AuditEntry(string EventName, Guid? UserId, Guid TenantId, string Metadata);

    private sealed class RecordingAuthAuditService : IAuthAuditService
    {
        public List<AuditEntry> Entries { get; } = [];

        public Task WriteEmptyRoleLoginAsync(Guid userId, Guid tenantId, string email, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task WriteAsync(string eventName, Guid? userId, Guid tenantId, string metadata, CancellationToken ct = default)
        {
            Entries.Add(new AuditEntry(eventName, userId, tenantId, metadata));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingUserRoleRepository : IUserRoleRepository
    {
        public List<UserRole> Assigned { get; } = [];

        public Task AssignAsync(UserRole userRole, CancellationToken ct)
        {
            Assigned.Add(userRole);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<string>> GetRolesByUserAsync(Guid userId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task RevokeAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<Guid>> GetUserIdsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class ViewerRoleRepository(Role viewer) : IRoleRepository
    {
        public Task<Role?> GetByNameAndTenantAsync(string name, Guid tenantId, CancellationToken ct)
            => Task.FromResult<Role?>(name == viewer.Name && tenantId == viewer.TenantId ? viewer : null);

        public Task<Role?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<Role>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> CreateAsync(Role role, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> UpsertSystemRoleAsync(string name, string displayName, string? description, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> UpdateAsync(Role role, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, Guid tenantId, string deletedBy, CancellationToken ct) => throw new NotSupportedException();
    }

    // The Viewer role exists, so the ensure-defaults fallback must not run.
    private sealed class UnexpectedRoleProvisioning : IRoleProvisioningService
    {
        public Task EnsureDefaultRolesAsync(Guid tenantId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeTokenService : ITokenService
    {
        public string GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions) => "access";
        public string GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes) => "access";
        public string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions) => throw new NotSupportedException();
        public string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes) => throw new NotSupportedException();
        public string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes, bool requiresPasswordChange) => throw new NotSupportedException();
        public string GenerateRefreshToken() => "refresh";
        public ClaimsPrincipal GetPrincipalFromExpiredToken(string token) => throw new NotSupportedException();
    }

    private sealed class FakeRefreshTokenHasher : IRefreshTokenHasher
    {
        public string Hash(string refreshToken) => "hash:" + refreshToken;
    }

    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        public Task CreateAsync(RefreshToken refreshToken, CancellationToken ct) => Task.CompletedTask;
        public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(RefreshToken refreshToken, CancellationToken ct) => throw new NotSupportedException();
        public Task RevokeAsync(string token, CancellationToken ct) => throw new NotSupportedException();
        public Task RevokeAllByUserAsync(Guid userId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => "hashed";
        public bool Verify(string password, string hash) => throw new NotSupportedException();
    }

    private sealed class FixedTenantLoginSettingsClient : ITenantLoginSettingsClient
    {
        public Task<TenantLoginSettingsSnapshot> GetAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult(new TenantLoginSettingsSnapshot(
                tenantId, false, false, true, false, 8, true, true, true, false, null, 30, 7, 5, 15));
    }

    private sealed class AcceptingPasswordPolicy : IPasswordPolicyService
    {
        public Task ValidateTenantPasswordAsync(Guid tenantId, Guid? userId, string password, string context, CancellationToken ct) => Task.CompletedTask;
        public string GenerateTemporaryPassword(TenantLoginSettingsSnapshot settings) => throw new NotSupportedException();
    }
}

using System.Security.Claims;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Auth.Commands;
using Diten.AuthService.Application.Features.Auth.Handlers.CommandHandlers;
using Diten.AuthService.Application.Tests.Users;
using Diten.AuthService.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.AuthService.Application.Tests.Auth;

/// <summary>
/// BL-521 FIX2 item 1 — platform LOGIN mints its token with the actor type the administrator's Platform RECORD names,
/// never with Auth's own copy on the user (<c>User.PlatformActorType</c>). That copy is written best-effort after the
/// record is saved (UpdatePlatformAdministratorHandler syncs it afterwards), so a failed sync left it at platform_admin
/// and every login of a partner minted platform_admin. Item 3 — Platform's definitive no is 401; no answer is 503.
/// Measured on the production handler.
/// </summary>
public sealed class PlatformLoginActorTypeFromRecordTests
{
    private static readonly Guid PlatformTenant = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private const string Email = "login.probe@bl521.test";

    [Fact]
    public async Task A_partner_whose_auth_copy_still_says_platform_admin_logs_in_as_a_partner()
    {
        var run = new Run(authCopy: "platform_admin", PlatformAdministratorRecordAnswer.ConfirmedAs("partner_admin"));

        var result = await run.LoginAsync();

        Assert.True(result.IsSuccessful, string.Join("; ", result.Errors));
        Assert.Equal("partner_admin", run.Tokens.IssuedActorType);
        Assert.Equal("partner_admin", Assert.Single(run.RefreshTokens.Created).ActorType);
    }

    [Fact]
    public async Task A_platform_administrator_whose_auth_copy_says_partner_logs_in_as_what_its_record_says()
    {
        var run = new Run(authCopy: "partner_admin", PlatformAdministratorRecordAnswer.ConfirmedAs("platform_admin"));

        var result = await run.LoginAsync();

        Assert.True(result.IsSuccessful, string.Join("; ", result.Errors));
        Assert.Equal("platform_admin", run.Tokens.IssuedActorType);
        Assert.Equal("platform_admin", Assert.Single(run.RefreshTokens.Created).ActorType);
    }

    [Fact]
    public async Task A_record_platform_refuses_is_not_logged_in()
    {
        var run = new Run(authCopy: "platform_admin", PlatformAdministratorRecordAnswer.Refused);

        var result = await run.LoginAsync();

        Assert.False(result.IsSuccessful);
        Assert.Equal(401, result.StatusCode);
        Assert.Null(run.Tokens.IssuedActorType);
        Assert.Empty(run.RefreshTokens.Created);
    }

    [Fact]
    public async Task When_platform_cannot_answer_the_login_is_refused_503_not_called_inactive()
    {
        var run = new Run(authCopy: "platform_admin", PlatformAdministratorRecordAnswer.Unavailable);

        var result = await run.LoginAsync();

        Assert.False(result.IsSuccessful);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal(PlatformAdministratorRecordAnswer.UnavailableMessage, Assert.Single(result.Errors));
        Assert.Null(run.Tokens.IssuedActorType);
        Assert.Empty(run.RefreshTokens.Created);
    }

    private sealed class Run
    {
        private readonly PlatformLoginCommandHandler _handler;

        public Run(string authCopy, PlatformAdministratorRecordAnswer record)
        {
            var user = new User(Email, "hash:x", "Lo", "Gin", PlatformTenant);
            user.SetPlatformActorType(authCopy);
            Tokens = new RecordingTokenService();
            RefreshTokens = new RecordingRefreshTokens();
            _handler = new PlatformLoginCommandHandler(
                new InMemoryUserRepository([user]),
                new SuperAdminRole(),
                new NoRoleLookup(),
                new NoPermissions(),
                new AcceptingHasher(),
                new PrefixHasher(),
                RefreshTokens,
                Tokens,
                new RecordStatus(record),
                NullLogger<PlatformLoginCommandHandler>.Instance);
        }

        public RecordingTokenService Tokens { get; }
        public RecordingRefreshTokens RefreshTokens { get; }

        public Task<Response<AuthResponse>> LoginAsync() =>
            _handler.Handle(new PlatformLoginCommand(Email, "any", "127.0.0.1", "test-agent"), CancellationToken.None);
    }

    private sealed class RecordStatus(PlatformAdministratorRecordAnswer record) : IPlatformAdministratorStatusClient
    {
        public Task<bool> IsActiveAsync(string email, CancellationToken ct) =>
            Task.FromResult(record.Verdict == PlatformAdministratorRecordVerdict.Confirmed);
        public Task MarkLoginAcceptedAsync(string email, CancellationToken ct) => Task.CompletedTask;
        public Task<PlatformAdministratorRecordAnswer> GetActiveActorTypeAsync(string email, CancellationToken ct) => Task.FromResult(record);
    }

    private sealed class RecordingTokenService : ITokenService
    {
        public string? IssuedActorType { get; private set; }

        public string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes, bool requiresPasswordChange)
        {
            IssuedActorType = actorType;
            return "new-access-token";
        }

        public ClaimsPrincipal GetPrincipalFromExpiredToken(string token) => throw new NotSupportedException();
        public string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions) => throw new NotSupportedException();
        public string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes) => throw new NotSupportedException();
        public string GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions) => throw new NotSupportedException();
        public string GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes) => throw new NotSupportedException();
        public string GenerateRefreshToken() => "new-refresh";
    }

    private sealed class RecordingRefreshTokens : IRefreshTokenRepository
    {
        public List<RefreshToken> Created { get; } = [];

        public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct) => Task.FromResult<RefreshToken?>(null);
        public Task CreateAsync(RefreshToken refreshToken, CancellationToken ct) { Created.Add(refreshToken); return Task.CompletedTask; }
        public Task UpdateAsync(RefreshToken refreshToken, CancellationToken ct) => Task.CompletedTask;
        public Task RevokeAsync(string token, CancellationToken ct) => Task.CompletedTask;
        public Task RevokeAllByUserAsync(Guid userId, Guid tenantId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class AcceptingHasher : IPasswordHasher
    {
        public string Hash(string password) => "hash:" + password;
        public bool Verify(string password, string hash) => true;
    }

    private sealed class PrefixHasher : IRefreshTokenHasher
    {
        public string Hash(string refreshToken) => "rh:" + refreshToken;
    }

    private sealed class SuperAdminRole : IUserRoleRepository
    {
        public Task<IEnumerable<string>> GetRolesByUserAsync(Guid userId, Guid tenantId, CancellationToken ct) => Task.FromResult<IEnumerable<string>>(["SuperAdmin"]);
        public Task AssignAsync(UserRole userRole, CancellationToken ct) => Task.CompletedTask;
        public Task RevokeAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> ExistsAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct) => Task.FromResult(false);
        public Task<IReadOnlyCollection<Guid>> GetUserIdsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<Guid>>([]);
    }

    private sealed class NoRoleLookup : IRoleRepository
    {
        public Task<Role?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct) => Task.FromResult<Role?>(null);
        public Task<Role?> GetByNameAndTenantAsync(string name, Guid tenantId, CancellationToken ct) => Task.FromResult<Role?>(null);
        public Task<IEnumerable<Role>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct) => Task.FromResult<IEnumerable<Role>>([]);
        public Task<Role> CreateAsync(Role role, CancellationToken ct) => Task.FromResult(role);
        public Task<Role> UpsertSystemRoleAsync(string name, string displayName, string? description, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> UpdateAsync(Role role, CancellationToken ct) => Task.FromResult(role);
        public Task DeleteAsync(Guid id, Guid tenantId, string deletedBy, CancellationToken ct) => Task.CompletedTask;
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
}

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
/// BL-521 FIX1 F1(b) — a platform actor's refresh re-issues its token with the actor type its Platform administrator
/// RECORD names, not the one the old token carried. Before: the refresh copied the old token's <c>actor_type</c> and asked
/// Platform only "is it active?", so a platform administrator demoted to partner kept <c>platform_admin</c> for as long
/// as it kept refreshing. Measured on the production handler.
/// </summary>
public sealed class PlatformRefreshActorTypeFromRecordTests
{
    private static readonly Guid PlatformTenant = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private const string RawRefreshToken = "raw-refresh";

    [Fact]
    public async Task A_demoted_platform_administrator_is_refreshed_as_a_partner()
    {
        var run = new Run(tokenActorType: "platform_admin", PlatformAdministratorRecordAnswer.ConfirmedAs("partner_admin"));

        var result = await run.RefreshAsync();

        Assert.True(result.IsSuccessful, string.Join("; ", result.Errors));
        Assert.Equal("partner_admin", run.Tokens.IssuedActorType);
        Assert.Equal("partner_admin", Assert.Single(run.RefreshTokens.Created).ActorType);
    }

    [Fact]
    public async Task A_promoted_partner_is_refreshed_as_a_platform_administrator()
    {
        var run = new Run(tokenActorType: "partner_admin", PlatformAdministratorRecordAnswer.ConfirmedAs("platform_admin"));

        var result = await run.RefreshAsync();

        Assert.True(result.IsSuccessful, string.Join("; ", result.Errors));
        Assert.Equal("platform_admin", run.Tokens.IssuedActorType);
        Assert.Equal("platform_admin", Assert.Single(run.RefreshTokens.Created).ActorType);
    }

    /// <summary>Platform's definitive no (no record, not active, unknown type): refused 401 and every session revoked.</summary>
    [Fact]
    public async Task A_platform_actor_whose_record_platform_refuses_is_not_refreshed_and_loses_its_sessions()
    {
        var run = new Run(tokenActorType: "platform_admin", PlatformAdministratorRecordAnswer.Refused);

        var result = await run.RefreshAsync();

        Assert.False(result.IsSuccessful);
        Assert.Equal(401, result.StatusCode);
        Assert.Null(run.Tokens.IssuedActorType);
        Assert.Empty(run.RefreshTokens.Created);
        Assert.True(run.RefreshTokens.RevokedAll);
    }

    /// <summary>
    /// BL-521 FIX2 item 3 — Platform gave NO answer (unreachable, 5xx, unreadable): this refresh is refused 503 and
    /// nothing is revoked — not the sessions, not even the presented refresh token (it is not rotated), so the client can
    /// retry with it. Before: any non-2xx reply ended every session of the administrator.
    /// </summary>
    [Fact]
    public async Task A_platform_actor_whose_record_platform_cannot_answer_is_refused_503_and_keeps_its_sessions()
    {
        var run = new Run(tokenActorType: "platform_admin", PlatformAdministratorRecordAnswer.Unavailable);

        var result = await run.RefreshAsync();

        Assert.False(result.IsSuccessful);
        Assert.Equal(503, result.StatusCode);
        Assert.Null(run.Tokens.IssuedActorType);
        Assert.Empty(run.RefreshTokens.Created);
        Assert.False(run.RefreshTokens.RevokedAll);
        Assert.False(run.RefreshTokens.Rotated);
    }

    private sealed class Run
    {
        private readonly User _user = new("demoted.admin@bl521.test", "hash:x", "De", "Moted", PlatformTenant);
        private readonly string _tokenActorType;
        private readonly RefreshTokenCommandHandler _handler;

        public Run(string tokenActorType, PlatformAdministratorRecordAnswer record)
        {
            _tokenActorType = tokenActorType;
            Tokens = new RecordingTokenService(_user.Id, tokenActorType);
            var row = new RefreshToken(
                _user.Id, "rh:" + RawRefreshToken, DateTime.UtcNow.AddDays(3), "127.0.0.1", PlatformTenant, tokenActorType);
            // Merge WP-CT-LANE-BL521-MERGE-01 — bound to the account's password as every mint binds it since BL-529 FIX3 (an unbound
            // row is refused as a pre-BL-529 session before any BL-521 rule is reached).
            Diten.AuthService.Application.Features.Auth.Services.SessionPasswordBinding.Bind(row, new PrefixHasher(), _user);
            RefreshTokens = new RecordingRefreshTokens(row);
            _handler = new RefreshTokenCommandHandler(
                new InMemoryUserRepository([_user]),
                new NoRoles(),
                new NoRoleLookup(),
                new NoPermissions(),
                new PassthroughPermissions(),
                Tokens,
                new NoTenantSettings(),
                new PrefixHasher(),
                RefreshTokens,
                new UnresolvedTenantContext(),
                new RecordStatus(record),
                NullLogger<RefreshTokenCommandHandler>.Instance);
        }

        public RecordingTokenService Tokens { get; }
        public RecordingRefreshTokens RefreshTokens { get; }

        public Task<Response<AuthResponse>> RefreshAsync() =>
            _handler.Handle(new RefreshTokenCommand("old-access-token", RawRefreshToken, "127.0.0.1", "test-agent"), CancellationToken.None);
    }

    /// <summary>Platform's answer about the record (confirmed with its actor type, refused, or no answer).</summary>
    private sealed class RecordStatus(PlatformAdministratorRecordAnswer record) : IPlatformAdministratorStatusClient
    {
        public Task<bool> IsActiveAsync(string email, CancellationToken ct) =>
            Task.FromResult(record.Verdict == PlatformAdministratorRecordVerdict.Confirmed);
        public Task MarkLoginAcceptedAsync(string email, CancellationToken ct) => Task.CompletedTask;
        public Task<PlatformAdministratorRecordAnswer> GetActiveActorTypeAsync(string email, CancellationToken ct) => Task.FromResult(record);
    }

    private sealed class RecordingTokenService(Guid userId, string tokenActorType) : ITokenService
    {
        public string? IssuedActorType { get; private set; }

        public ClaimsPrincipal GetPrincipalFromExpiredToken(string token) => new(new ClaimsIdentity(
        [
            new Claim("sub", userId.ToString()),
            new Claim("tenant_id", PlatformTenant.ToString()),
            new Claim("actor_type", tokenActorType)
        ], "Bearer"));

        public string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes, bool requiresPasswordChange)
        {
            IssuedActorType = actorType;
            return "new-access-token";
        }

        public string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions) => throw new NotSupportedException();
        public string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes) => throw new NotSupportedException();
        public string GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions) => throw new NotSupportedException();
        public string GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes) => throw new NotSupportedException();
        public string GenerateRefreshToken() => "new-refresh";
    }

    private sealed class RecordingRefreshTokens(RefreshToken existing) : IRefreshTokenRepository
    {
        public List<RefreshToken> Created { get; } = [];
        public bool RevokedAll { get; private set; }
        public bool Rotated { get; private set; }

        public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct) =>
            Task.FromResult<RefreshToken?>(token == RawRefreshToken ? existing : null);
        public Task CreateAsync(RefreshToken refreshToken, CancellationToken ct) { Created.Add(refreshToken); return Task.CompletedTask; }
        public Task UpdateAsync(RefreshToken refreshToken, CancellationToken ct) { Rotated = true; return Task.CompletedTask; }
        public Task RevokeAsync(string token, CancellationToken ct) => Task.CompletedTask;
        public Task<long> RevokeAllByUserAsync(Guid userId, Guid tenantId, CancellationToken ct) { RevokedAll = true; return Task.FromResult(1L); }
        // Merge WP-CT-LANE-BL521-MERGE-01 — BL-529's refresh-store members (this fake predates them). The rotation is BL-529's conditional TryRotateAsync now (it
        // replaced the whole-row UpdateAsync on refresh), so it is what marks Rotated.
        public Task<bool> RevokeIfLiveAsync(string token, string reason, CancellationToken ct) => Task.FromResult(true);
        public Task<long> RevokeLiveSessionsAsync(Guid userId, Guid tenantId, string reason, CancellationToken ct) => Task.FromResult(0L);
        public Task<bool> TryRotateAsync(Guid tokenId, string replacedByTokenHash, string? revokedByIp, CancellationToken ct) { Rotated = true; return Task.FromResult(true); }
    }

    private sealed class PrefixHasher : IRefreshTokenHasher
    {
        public string Hash(string refreshToken) => "rh:" + refreshToken;
    }

    private sealed class NoRoles : IUserRoleRepository
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

    private sealed class PassthroughPermissions : ITenantEffectivePermissionResolver
    {
        public Task<IReadOnlyList<string>> ResolveAsync(Guid tenantId, IEnumerable<string>? rolePermissions, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>(rolePermissions?.ToArray() ?? []);
    }

    private sealed class NoTenantSettings : ITenantLoginSettingsClient
    {
        public Task<TenantLoginSettingsSnapshot> GetAsync(Guid tenantId, CancellationToken ct) =>
            throw new NotSupportedException("A platform refresh reads no tenant login settings.");
    }

    private sealed class UnresolvedTenantContext : ITenantContext
    {
        public Guid TenantId => throw new InvalidOperationException("Tenant not resolved.");
        public bool IsResolved => false;
        public bool IsPlatformContext => false;
        public Guid? TargetTenantId => null;
        public void SetTenant(Guid tenantId) { }
        public void SetPlatformContext(Guid targetTenantId) { }
    }
}

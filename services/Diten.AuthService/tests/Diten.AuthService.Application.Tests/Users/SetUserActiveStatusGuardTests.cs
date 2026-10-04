using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Commands;
using Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;
using Diten.AuthService.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Diten.AuthService.Application.Tests.Testing;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// YOU CANNOT SWITCH YOURSELF OFF — owner finding, control round 2026-09-24 (finding 33 in the Users record).
///
/// <para>Signed in as the administrator, the owner opened their own row's menu, chose "Devre Dışı Bırak", and the
/// server obliged: the account went inactive and every refresh token was revoked, so the seat that did it was locked
/// out on the spot. The screen now hides the action on the viewer's own row, but a screen is not a rule; this file
/// pins the SERVER refusal, with the stable code the frontend bridge translates (BL-450).</para>
///
/// <para>Sister of <see cref="DeleteUserGuardTests"/>: same shape, same status, same "before anything is written".</para>
/// </summary>
public sealed class SetUserActiveStatusGuardTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task You_cannot_deactivate_the_account_you_are_signed_in_with()
    {
        var me = User("me@acme.test");
        var repo = new InMemoryUserRepository([me]);
        var tokens = new CountingRefreshTokens();

        var result = await Handler(repo, tokens, actor: me.Id)
            .Handle(new SetUserActiveStatusCommand(me.Id, IsActive: false), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains(result.ErrorCodes, e => e.Code == SetUserActiveStatusCommandHandler.SelfDeactivateCode);
        Assert.True((await repo.GetByIdAndTenantAsync(me.Id, TenantA, CancellationToken.None))!.IsActive);
        Assert.Equal(0, tokens.RevokeAllCount);
    }

    [Fact]
    public async Task Deactivating_someone_else_is_still_allowed_and_revokes_their_sessions()
    {
        var me = User("me@acme.test");
        var other = User("other@acme.test");
        var repo = new InMemoryUserRepository([me, other]);
        var tokens = new CountingRefreshTokens();

        var result = await Handler(repo, tokens, actor: me.Id)
            .Handle(new SetUserActiveStatusCommand(other.Id, IsActive: false), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.False((await repo.GetByIdAndTenantAsync(other.Id, TenantA, CancellationToken.None))!.IsActive);
        Assert.Equal(1, tokens.RevokeAllCount);
    }

    [Fact]
    public async Task The_refusal_is_about_switching_OFF_only_reactivating_yourself_is_not_a_case()
    {
        // An inactive account cannot be signed in, so "activate myself" cannot happen through this seat; the rule must
        // not accidentally block an administrator who is re-activating an account that shares their id in a test.
        var me = User("me@acme.test");
        me.Deactivate();
        var repo = new InMemoryUserRepository([me]);

        var result = await Handler(repo, new CountingRefreshTokens(), actor: me.Id)
            .Handle(new SetUserActiveStatusCommand(me.Id, IsActive: true), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.True((await repo.GetByIdAndTenantAsync(me.Id, TenantA, CancellationToken.None))!.IsActive);
    }

    // ── builders ──
    private static User User(string email) => new(email, "hash:x", "Ad", "Soyad", TenantA);

    private static SetUserActiveStatusCommandHandler Handler(IUserRepository users, IRefreshTokenRepository tokens, Guid actor)
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(TenantA);
        return new SetUserActiveStatusCommandHandler(
            users, tokens, tenant, new FakeCurrentUser(actor), UserAuditForTests.None(), new RecordingUserQuotaClient(), NullLogger<SetUserActiveStatusCommandHandler>.Instance);
    }

    private sealed class FakeTenantContext : ITenantContext
    {
        private Guid _tenantId;
        public Guid TenantId => IsResolved ? _tenantId : throw new InvalidOperationException("Tenant not resolved.");
        public bool IsResolved { get; private set; }
        public bool IsPlatformContext => false;
        public Guid? TargetTenantId => null;
        public void SetTenant(Guid tenantId) { _tenantId = tenantId; IsResolved = true; }
        public void SetPlatformContext(Guid targetTenantId) => throw new NotSupportedException();
    }

    private sealed class FakeCurrentUser(Guid id) : ICurrentUserAccessor
    {
        public Guid? UserId => id;
    }

    private sealed class CountingRefreshTokens : IRefreshTokenRepository
    {
        public int RevokeAllCount { get; private set; }
        public Task<long> RevokeAllByUserAsync(Guid userId, Guid tenantId, CancellationToken ct) { RevokeAllCount++; return Task.FromResult(0L); }
        public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct) => throw new NotSupportedException();
        public Task CreateAsync(RefreshToken refreshToken, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(RefreshToken refreshToken, CancellationToken ct) => throw new NotSupportedException();
        public Task RevokeAsync(string token, CancellationToken ct) => throw new NotSupportedException();
    }
}
